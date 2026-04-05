package docker

import (
	"encoding/json"
	"net/http"
	"strings"

	"github.com/docker/docker/api/types/filters"
	"github.com/docker/docker/api/types/volume"

	"docklite-wsl/internal/apiresponse"
	"docklite-wsl/internal/dockerengine"
)

// VolumesList xử lý GET /api/volumes.
func VolumesList(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path != "/api/volumes" {
		http.NotFound(w, r)
		return
	}
	if r.Method != http.MethodGet {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	volList, err := dc.VolumeList(ctx, volume.ListOptions{Filters: filters.NewArgs()})
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	items := make([]map[string]interface{}, 0, len(volList.Volumes))
	for _, v := range volList.Volumes {
		if v == nil {
			continue
		}
		items = append(items, map[string]interface{}{
			"name":       v.Name,
			"driver":     v.Driver,
			"mountpoint": v.Mountpoint,
			"scope":      v.Scope,
			"createdAt":  v.CreatedAt,
		})
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"items": items}, http.StatusOK)
}

type volumeRemoveBody struct {
	Name string `json:"name"`
}

// VolumesRemove xử lý POST /api/volumes/remove — xóa một volume theo tên (docker volume rm).
func VolumesRemove(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost || r.URL.Path != "/api/volumes/remove" {
		http.NotFound(w, r)
		return
	}
	var body volumeRemoveBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	name := strings.TrimSpace(body.Name)
	if name == "" {
		apiresponse.WriteError(w, apiresponse.CodeValidation, "thiếu name", http.StatusBadRequest)
		return
	}
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	// force=false: từ chối nếu volume đang được container dùng (an toàn hơn).
	if err := dc.VolumeRemove(ctx, name, false); err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	apiresponse.WriteSuccess(w, struct{}{}, http.StatusOK)
}
