package docker

import (
	"encoding/json"
	"net/http"

	"github.com/docker/docker/api/types"

	"docklite-wsl/internal/dockerengine"
)

// EventsStream xử lý GET /api/docker/events/stream — NDJSON từ Docker Engine (mỗi dòng một sự kiện).
func EventsStream(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet || r.URL.Path != "/api/docker/events/stream" {
		http.NotFound(w, r)
		return
	}
	c, err := dockerengine.Client()
	if err != nil {
		http.Error(w, err.Error(), http.StatusServiceUnavailable)
		return
	}
	ctx := r.Context()
	evCh, errCh := c.Events(ctx, types.EventsOptions{})
	flusher, ok := w.(http.Flusher)
	if !ok {
		http.Error(w, "stream không hỗ trợ flush", http.StatusInternalServerError)
		return
	}
	w.Header().Set("Content-Type", "application/x-ndjson; charset=utf-8")
	w.WriteHeader(http.StatusOK)
	enc := json.NewEncoder(w)
	for {
		select {
		case msg, open := <-evCh:
			if !open {
				return
			}
			if err := enc.Encode(msg); err != nil {
				return
			}
			flusher.Flush()
		case err := <-errCh:
			if err != nil {
				return
			}
		case <-ctx.Done():
			return
		}
	}
}
