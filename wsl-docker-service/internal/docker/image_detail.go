package docker

import (
	"encoding/json"
	"io"
	"net/http"
	"strings"

	"github.com/docker/docker/api/types/image"

	"docklite-wsl/internal/apiresponse"
	"docklite-wsl/internal/dockerengine"
)

const (
	maxPullLogBytes  = 512 * 1024
	maxImageLoadBody = 512 << 20 // 512 MiB
)

// ImagesPath xử lý GET /api/images/{id}/inspect|history|export.
func ImagesPath(w http.ResponseWriter, r *http.Request) {
	rest := strings.TrimPrefix(r.URL.Path, "/api/images/")
	if rest == "" {
		http.NotFound(w, r)
		return
	}
	parts := strings.SplitN(rest, "/", 2)
	id := strings.TrimSpace(parts[0])
	if id == "" || len(parts) < 2 {
		http.NotFound(w, r)
		return
	}
	action := strings.TrimSpace(parts[1])
	switch action {
	case "inspect":
		if r.Method != http.MethodGet {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}
		imageInspect(w, r, id)
	case "history":
		if r.Method != http.MethodGet {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}
		imageHistory(w, r, id)
	case "export":
		if r.Method != http.MethodGet {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}
		imageExport(w, r, id)
	default:
		http.NotFound(w, r)
	}
}

func imageInspect(w http.ResponseWriter, r *http.Request, id string) {
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	_, raw, err := dc.ImageInspectWithRaw(ctx, id)
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	var payload struct {
		Inspect json.RawMessage `json:"inspect"`
	}
	payload.Inspect = raw
	apiresponse.WriteSuccess(w, payload, http.StatusOK)
}

func imageHistory(w http.ResponseWriter, r *http.Request, id string) {
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	hist, err := dc.ImageHistory(ctx, id)
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	items := make([]map[string]interface{}, 0, len(hist))
	for _, layer := range hist {
		items = append(items, map[string]interface{}{
			"id":        layer.ID,
			"created":   layer.Created,
			"createdBy": layer.CreatedBy,
			"size":      layer.Size,
			"comment":   layer.Comment,
		})
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"items": items}, http.StatusOK)
}

func imageExport(w http.ResponseWriter, r *http.Request, id string) {
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	reader, err := dc.ImageSave(ctx, []string{id})
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	defer func() { _ = reader.Close() }()
	w.Header().Set("Content-Type", "application/x-tar")
	w.Header().Set("Content-Disposition", `attachment; filename="image-export.tar"`)
	w.WriteHeader(http.StatusOK)
	_, err = io.Copy(w, reader)
	if err != nil {
		// Đã gửi header; client có thể nhận stream lỗi giữa chừng.
		return
	}
}

type imagePullBody struct {
	Reference string `json:"reference"`
}

// ImagePull xử lý POST /api/images/pull — đọc hết stream pull rồi trả log (giới hạn độ dài).
func ImagePull(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost || r.URL.Path != "/api/images/pull" {
		http.NotFound(w, r)
		return
	}
	var body imagePullBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	ref := strings.TrimSpace(body.Reference)
	if ref == "" {
		apiresponse.WriteError(w, apiresponse.CodeValidation, "thiếu reference", http.StatusBadRequest)
		return
	}
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	reader, err := dc.ImagePull(ctx, ref, image.PullOptions{})
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	defer func() { _ = reader.Close() }()
	var buf strings.Builder
	_, err = io.Copy(&buf, io.LimitReader(reader, maxPullLogBytes))
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	_, _ = io.Copy(io.Discard, reader)
	logStr := buf.String()
	if len(logStr) > maxPullLogBytes {
		logStr = logStr[:maxPullLogBytes]
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"log": logStr}, http.StatusOK)
}

// ImageLoad xử lý POST /api/images/load — thân application/x-tar (tối đa maxImageLoadBody).
func ImageLoad(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost || r.URL.Path != "/api/images/load" {
		http.NotFound(w, r)
		return
	}
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	body := http.MaxBytesReader(w, r.Body, maxImageLoadBody)
	resp, err := dc.ImageLoad(ctx, body, false)
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	defer resp.Body.Close()
	msg, err := io.ReadAll(io.LimitReader(resp.Body, 256*1024))
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusInternalServerError)
		return
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"message": string(msg)}, http.StatusOK)
}
