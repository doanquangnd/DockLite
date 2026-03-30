package docker

import (
	"encoding/json"
	"net/http"
	"strings"

	"github.com/docker/docker/api/types"
	"github.com/docker/docker/api/types/filters"

	"docklite-wsl/internal/apiresponse"
	"docklite-wsl/internal/dockerengine"
)

type systemPruneBody struct {
	Kind        string `json:"kind"`
	WithVolumes bool   `json:"withVolumes"`
}

// SystemPrune xử lý POST /api/system/prune.
func SystemPrune(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost || r.URL.Path != "/api/system/prune" {
		http.NotFound(w, r)
		return
	}
	var body systemPruneBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	kind := strings.TrimSpace(strings.ToLower(body.Kind))
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	switch kind {
	case "containers":
		rep, err := dc.ContainersPrune(ctx, filters.NewArgs())
		if err != nil {
			dockerengine.WriteError(w, err)
			return
		}
		apiresponse.WriteSuccess(w, map[string]interface{}{"output": formatContainersPruneReport(rep)}, http.StatusOK)
	case "images":
		f := filters.NewArgs()
		f.Add("dangling", "true")
		rep, err := dc.ImagesPrune(ctx, f)
		if err != nil {
			dockerengine.WriteError(w, err)
			return
		}
		apiresponse.WriteSuccess(w, map[string]interface{}{"output": formatImagesPruneReport(rep)}, http.StatusOK)
	case "volumes":
		rep, err := dc.VolumesPrune(ctx, filters.NewArgs())
		if err != nil {
			dockerengine.WriteError(w, err)
			return
		}
		apiresponse.WriteSuccess(w, map[string]interface{}{"output": formatVolumesPruneReport(rep)}, http.StatusOK)
	case "networks":
		rep, err := dc.NetworksPrune(ctx, filters.NewArgs())
		if err != nil {
			dockerengine.WriteError(w, err)
			return
		}
		apiresponse.WriteSuccess(w, map[string]interface{}{"output": formatNetworksPruneReport(rep)}, http.StatusOK)
	case "system":
		cont, err := dc.ContainersPrune(ctx, filters.NewArgs())
		if err != nil {
			dockerengine.WriteError(w, err)
			return
		}
		imgF := filters.NewArgs()
		imgF.Add("dangling", "true")
		img, err := dc.ImagesPrune(ctx, imgF)
		if err != nil {
			dockerengine.WriteError(w, err)
			return
		}
		net, err := dc.NetworksPrune(ctx, filters.NewArgs())
		if err != nil {
			dockerengine.WriteError(w, err)
			return
		}
		var vol *types.VolumesPruneReport
		if body.WithVolumes {
			v, err := dc.VolumesPrune(ctx, filters.NewArgs())
			if err != nil {
				dockerengine.WriteError(w, err)
				return
			}
			vol = &v
		}
		bc, err := dc.BuildCachePrune(ctx, types.BuildCachePruneOptions{})
		if err != nil {
			dockerengine.WriteError(w, err)
			return
		}
		out := formatSystemPruneCombined(cont, img, net, vol, bc)
		apiresponse.WriteSuccess(w, map[string]interface{}{"output": out}, http.StatusOK)
	default:
		apiresponse.WriteError(w, apiresponse.CodeValidation, "kind phải là: containers, images, volumes, networks, system", http.StatusBadRequest)
	}
}
