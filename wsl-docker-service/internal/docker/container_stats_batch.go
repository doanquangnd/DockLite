package docker

import (
	"encoding/json"
	"net/http"
	"strings"

	"docklite-wsl/internal/apiresponse"
	"docklite-wsl/internal/dockerengine"
)

const statsBatchMaxIDs = 32

// ContainerStatsBatch xử lý POST /api/containers/stats-batch — nhiều snapshot trong một vòng (tối đa 32 id).
func ContainerStatsBatch(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	var body struct {
		IDs []string `json:"ids"`
	}
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		apiresponse.WriteError(w, apiresponse.CodeValidation, "json: "+err.Error(), http.StatusBadRequest)
		return
	}
	if len(body.IDs) == 0 {
		apiresponse.WriteError(w, apiresponse.CodeValidation, "thiếu ids", http.StatusBadRequest)
		return
	}
	if len(body.IDs) > statsBatchMaxIDs {
		apiresponse.WriteError(w, apiresponse.CodeValidation, "tối đa 32 id", http.StatusBadRequest)
		return
	}
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	items := make([]map[string]interface{}, 0, len(body.IDs))
	for _, rawID := range body.IDs {
		id := strings.TrimSpace(rawID)
		if id == "" {
			continue
		}
		snap, err := snapshotStatsForContainer(ctx, dc, id)
		if err != nil {
			items = append(items, map[string]interface{}{
				"id":    id,
				"ok":    false,
				"error": err.Error(),
			})
			continue
		}
		items = append(items, map[string]interface{}{
			"id":    id,
			"ok":    true,
			"stats": snap,
		})
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"items": items}, http.StatusOK)
}
