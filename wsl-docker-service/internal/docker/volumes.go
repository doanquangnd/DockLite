package docker

import (
	"net/http"

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
