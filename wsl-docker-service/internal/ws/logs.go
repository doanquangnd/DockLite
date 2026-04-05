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
	"docklite-wsl/internal/wslimit"
)

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

// handleLogs nâng cấp WebSocket và stream log container qua Docker Engine API (không spawn docker logs).
func handleLogs(w http.ResponseWriter, r *http.Request) {
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
	if !wslimit.TryAcquireWebSocket() {
		http.Error(w, "quá nhiều kết nối WebSocket đồng thời (xem DOCKLITE_WS_MAX_CONNECTIONS)", http.StatusServiceUnavailable)
		return
	}
	defer wslimit.ReleaseWebSocket()

	conn, err := wslimit.Upgrader.Upgrade(w, r, nil)
	if err != nil {
		log.Printf("websocket upgrade: %v", err)
		return
	}
	defer conn.Close()
	wslimit.ConfigureConnAfterUpgrade(conn)

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
