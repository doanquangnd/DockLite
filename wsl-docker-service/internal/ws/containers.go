package ws

import (
	"net/http"
	"strings"

	"docklite-wsl/internal/docker"
)

// HandleContainersPath phân luồng /ws/containers/{id}/logs và /ws/containers/{id}/stats.
func HandleContainersPath(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	path := r.URL.Path
	if strings.HasSuffix(path, "/logs") {
		handleLogs(w, r)
		return
	}
	if strings.HasSuffix(path, "/stats") {
		id := extractWsContainerID(path, "/stats")
		if id == "" {
			http.NotFound(w, r)
			return
		}
		docker.StreamContainerStatsWebSocket(w, r, id)
		return
	}
	http.NotFound(w, r)
}

func extractWsContainerID(path, suffix string) string {
	rest := strings.TrimPrefix(path, "/ws/containers/")
	rest = strings.TrimSuffix(rest, suffix)
	return strings.TrimSpace(rest)
}
