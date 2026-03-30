package docker

import (
	"encoding/json"
	"net/http"

	"docklite-wsl/internal/apiresponse"
	"docklite-wsl/internal/dockerengine"
)

type healthResponse struct {
	Status  string `json:"status"`
	Service string `json:"service"`
	Version string `json:"version"`
}

// Health trả JSON trạng thái service.
func Health(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	resp := healthResponse{Status: "ok", Service: "docklite-wsl", Version: "0.1.0"}
	_ = json.NewEncoder(w).Encode(resp)
}

// DockerInfo gọi Docker Engine API /info và trả tóm tắt JSON.
func DockerInfo(w http.ResponseWriter, r *http.Request) {
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
	info, err := dc.Info(ctx)
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{
		"serverVersion":     info.ServerVersion,
		"containers":        info.Containers,
		"containersRunning": info.ContainersRunning,
		"images":            info.Images,
		"osType":            info.OSType,
		"kernelVersion":     info.KernelVersion,
		"operatingSystem":   info.OperatingSystem,
	}, http.StatusOK)
}
