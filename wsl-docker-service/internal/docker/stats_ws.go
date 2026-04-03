package docker

import (
	"context"
	"encoding/json"
	"io"
	"log"
	"net/http"
	"strconv"
	"time"

	"github.com/gorilla/websocket"

	"docklite-wsl/internal/dockerengine"
)

var statsWSUpgrader = websocket.Upgrader{
	CheckOrigin: func(r *http.Request) bool { return true },
}

// StreamContainerStatsWebSocket nâng cấp WebSocket và gửi JSON statsSnapshot (mỗi tin nhắn văn bản một mẫu, cùng schema GET /api/containers/{id}/stats).
func StreamContainerStatsWebSocket(w http.ResponseWriter, r *http.Request, containerID string) {
	intervalMs := 1000
	if v := r.URL.Query().Get("intervalMs"); v != "" {
		if n, err := strconv.Atoi(v); err == nil && n >= 500 && n <= 5000 {
			intervalMs = n
		}
	}

	conn, err := statsWSUpgrader.Upgrade(w, r, nil)
	if err != nil {
		log.Printf("websocket stats upgrade: %v", err)
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
		_ = conn.WriteMessage(websocket.TextMessage, statsErrBytes(err.Error()))
		return
	}

	cs, err := dc.ContainerStats(ctx, containerID, true)
	if err != nil {
		_ = conn.WriteMessage(websocket.TextMessage, statsErrBytes(err.Error()))
		return
	}
	defer cs.Body.Close()

	dec := json.NewDecoder(cs.Body)
	minInterval := time.Duration(intervalMs) * time.Millisecond
	var lastSend time.Time

	for {
		if err := ctx.Err(); err != nil {
			return
		}

		var wire statsWire
		if err := dec.Decode(&wire); err != nil {
			if err != io.EOF {
				_ = conn.WriteMessage(websocket.TextMessage, statsErrBytes("stream: "+err.Error()))
			}
			return
		}

		now := time.Now()
		if !lastSend.IsZero() && now.Sub(lastSend) < minInterval {
			continue
		}
		lastSend = now

		rx, tx := sumNetworks(wire)
		br, bw := sumBlkioReadWrite(wire.BlkioStats)
		out := statsSnapshot{
			ReadAt:           wire.Read.UTC().Format(time.RFC3339),
			CPUUsagePercent:  computeCPUPercent(wire),
			MemoryUsageBytes: wire.MemoryStats.Usage,
			MemoryLimitBytes: wire.MemoryStats.Limit,
			NetworkRxBytes:   rx,
			NetworkTxBytes:   tx,
			BlockReadBytes:   br,
			BlockWriteBytes:  bw,
		}
		b, err := json.Marshal(out)
		if err != nil {
			continue
		}
		if err := conn.WriteMessage(websocket.TextMessage, b); err != nil {
			return
		}
	}
}

func statsErrBytes(msg string) []byte {
	b, err := json.Marshal(map[string]string{"error": msg})
	if err != nil {
		return []byte(`{"error":"lỗi ghi JSON"}`)
	}
	return b
}
