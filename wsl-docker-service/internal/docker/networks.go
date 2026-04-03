package docker

import (
	"net/http"
	"time"

	"github.com/docker/docker/api/types"

	"docklite-wsl/internal/apiresponse"
	"docklite-wsl/internal/dockerengine"
)

// NetworksList xử lý GET /api/networks.
func NetworksList(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path != "/api/networks" {
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
	list, err := dc.NetworkList(ctx, types.NetworkListOptions{})
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	items := make([]map[string]interface{}, 0, len(list))
	for _, n := range list {
		created := ""
		if !n.Created.IsZero() {
			created = n.Created.UTC().Format(time.RFC3339)
		}
		items = append(items, map[string]interface{}{
			"id":        n.ID,
			"name":      n.Name,
			"driver":    n.Driver,
			"scope":     n.Scope,
			"createdAt": created,
		})
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"items": items}, http.StatusOK)
}
