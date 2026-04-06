package docker

import (
	"encoding/json"
	"net/http"
	"strings"
	"time"

	"github.com/docker/docker/api/types/filters"
	"github.com/docker/docker/api/types/image"
	"github.com/docker/go-units"

	"docklite-wsl/internal/apiresponse"
	"docklite-wsl/internal/dockerengine"
)

type imageSummaryJSON struct {
	ID         string `json:"id"`
	Repository string `json:"repository"`
	Tag        string `json:"tag"`
	Size       string `json:"size"`
	SizeBytes  int64  `json:"sizeBytes"`
	CreatedAt  string `json:"createdAt,omitempty"`
}

// ImagesRoot xử lý GET /api/images.
func ImagesRoot(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path != "/api/images" {
		http.NotFound(w, r)
		return
	}
	if r.Method != http.MethodGet {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	listImages(w, r)
}

func splitRepoTag(repoTag string) (repository, tag string) {
	i := strings.LastIndex(repoTag, ":")
	if i <= 0 || strings.HasPrefix(repoTag, "sha256:") {
		return repoTag, ""
	}
	return repoTag[:i], repoTag[i+1:]
}

func listImages(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	summaries, err := dc.ImageList(ctx, image.ListOptions{All: true})
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	var items []imageSummaryJSON
	for _, s := range summaries {
		sizeStr := units.HumanSize(float64(s.Size))
		createdAt := time.Unix(s.Created, 0).UTC().Format(time.RFC3339)
		if len(s.RepoTags) == 0 {
			shortID := strings.TrimPrefix(s.ID, "sha256:")
			if len(shortID) > 12 {
				shortID = shortID[:12]
			}
			items = append(items, imageSummaryJSON{
				ID:         shortID,
				Repository: "<none>",
				Tag:        "<none>",
				Size:       sizeStr,
				SizeBytes:  s.Size,
				CreatedAt:  createdAt,
			})
			continue
		}
		for _, rt := range s.RepoTags {
			repo, tag := splitRepoTag(rt)
			if tag == "" {
				tag = "<none>"
			}
			shortID := strings.TrimPrefix(s.ID, "sha256:")
			if len(shortID) > 12 {
				shortID = shortID[:12]
			}
			items = append(items, imageSummaryJSON{
				ID:         shortID,
				Repository: repo,
				Tag:        tag,
				Size:       sizeStr,
				SizeBytes:  s.Size,
				CreatedAt:  createdAt,
			})
		}
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"items": items}, http.StatusOK)
}

type imageRemoveBody struct {
	ID string `json:"id"`
}

// ImagesRemove xử lý POST /api/images/remove.
func ImagesRemove(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost || r.URL.Path != "/api/images/remove" {
		http.NotFound(w, r)
		return
	}
	var body imageRemoveBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	id := strings.TrimSpace(body.ID)
	if id == "" {
		apiresponse.WriteError(w, apiresponse.CodeValidation, "thiếu id", http.StatusBadRequest)
		return
	}
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	_, err = dc.ImageRemove(ctx, id, image.RemoveOptions{Force: true})
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	apiresponse.WriteSuccess(w, struct{}{}, http.StatusOK)
}

type imagePruneBody struct {
	AllUnused bool `json:"allUnused"`
}

// ImagesPrune xử lý POST /api/images/prune.
func ImagesPrune(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost || r.URL.Path != "/api/images/prune" {
		http.NotFound(w, r)
		return
	}
	var body imagePruneBody
	_ = json.NewDecoder(r.Body).Decode(&body)
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	f := filters.NewArgs()
	if body.AllUnused {
		f.Add("dangling", "false")
	} else {
		f.Add("dangling", "true")
	}
	rep, err := dc.ImagesPrune(ctx, f)
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"output": formatImagesPruneReport(rep)}, http.StatusOK)
}
