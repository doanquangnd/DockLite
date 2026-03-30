// Package ws phục vụ WebSocket (log container theo luồng).
package ws

import (
	"context"
	"errors"
	"io"
	"log"
	"net/http"
	"strings"
	"sync"

	"github.com/docker/docker/api/types/container"
	"github.com/docker/docker/pkg/stdcopy"
	"github.com/gorilla/websocket"

	"docklite-wsl/internal/dockerengine"
)

var upgrader = websocket.Upgrader{
	CheckOrigin: func(r *http.Request) bool { return true },
}

type textWriter struct {
	conn *websocket.Conn
	mu   sync.Mutex
}

func (w *textWriter) Write(p []byte) (int, error) {
	if len(p) == 0 {
		return 0, nil
	}
	w.mu.Lock()
	defer w.mu.Unlock()
	err := w.conn.WriteMessage(websocket.TextMessage, p)
	if err != nil {
		return 0, err
	}
	return len(p), nil
}

// HandleLogs nâng cấp WebSocket và stream log container qua Docker Engine API (không spawn docker logs).
func HandleLogs(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	prefix := "/ws/containers/"
	if !strings.HasPrefix(r.URL.Path, prefix) || !strings.HasSuffix(r.URL.Path, "/logs") {
		http.NotFound(w, r)
		return
	}
	rest := strings.TrimPrefix(r.URL.Path, prefix)
	id := strings.TrimSpace(strings.TrimSuffix(rest, "/logs"))
	if id == "" {
		http.NotFound(w, r)
		return
	}
	conn, err := upgrader.Upgrade(w, r, nil)
	if err != nil {
		log.Printf("websocket upgrade: %v", err)
		return
	}
	defer conn.Close()

	ctx, cancel := context.WithCancel(r.Context())
	defer cancel()

	go func() {
		for {
			_, _, err := conn.ReadMessage()
			if err != nil {
				cancel()
				return
			}
		}
	}()

	dc, err := dockerengine.Client()
	if err != nil {
		_ = conn.WriteMessage(websocket.TextMessage, []byte("lỗi: "+err.Error()))
		return
	}
	reader, err := dc.ContainerLogs(ctx, id, container.LogsOptions{
		ShowStdout: true,
		ShowStderr: true,
		Follow:     true,
	})
	if err != nil {
		_ = conn.WriteMessage(websocket.TextMessage, []byte("lỗi: "+err.Error()))
		return
	}
	defer reader.Close()

	wr := &textWriter{conn: conn}
	_, err = stdcopy.StdCopy(wr, wr, reader)
	if err != nil && !errors.Is(err, io.EOF) && !errors.Is(err, context.Canceled) {
		_ = conn.WriteMessage(websocket.TextMessage, []byte("lỗi stream: "+err.Error()))
	}
}
