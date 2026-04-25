// Package httpserver đăng ký toàn bộ route HTTP cho docklite-wsl.
package httpserver

import (
	"net/http"
	"time"

	"docklite-wsl/internal/compose"
	"docklite-wsl/internal/docker"
	"docklite-wsl/internal/hostresources"
	"docklite-wsl/internal/ws"
)

// ReadHeaderTimeout giá trị mặc định cho http.Server.ReadHeaderTimeout.
const ReadHeaderTimeout = 5 * time.Second

// Register gắn mọi handler REST + WebSocket vào mux. state dùng cho POST /api/auth/rotate (chỉ khi mật khẩu bật lúc khởi động).
func Register(mux *http.ServeMux, state *MutableToken) {
	mux.HandleFunc("/api/openapi.json", OpenAPI)
	mux.HandleFunc("/api/metrics", Metrics)
	mux.HandleFunc("/api/health", docker.Health)
	mux.HandleFunc("/api/wsl/host-resources", hostresources.HTTPHandler)
	mux.HandleFunc("/api/docker/info", docker.DockerInfo)
	mux.HandleFunc("/api/docker/events/stream", docker.EventsStream)
	mux.HandleFunc("/api/containers/top-by-memory", docker.TopContainersByMemory)
	mux.HandleFunc("/api/containers/top-by-cpu", docker.TopContainersByCPU)
	mux.HandleFunc("/api/containers/stats-batch", docker.ContainerStatsBatch)
	mux.HandleFunc("/api/containers", docker.ContainersCollection)
	mux.HandleFunc("/api/containers/", docker.ContainersItem)
	mux.HandleFunc("/ws/containers/", ws.HandleContainersPath)
	compose.Register(mux)
	mux.HandleFunc("/api/images/pull/stream", docker.ImagePullStream)
	mux.HandleFunc("/api/images/trivy-scan", docker.ImageTrivyScan)
	mux.HandleFunc("/api/images/pull", docker.ImagePull)
	mux.HandleFunc("/api/images/load", docker.ImageLoad)
	mux.HandleFunc("/api/images/prune", docker.ImagesPrune)
	mux.HandleFunc("/api/images/remove", docker.ImagesRemove)
	mux.HandleFunc("/api/networks", docker.NetworksList)
	mux.HandleFunc("/api/volumes/remove", docker.VolumesRemove)
	mux.HandleFunc("/api/volumes", docker.VolumesList)
	mux.HandleFunc("/api/images", docker.ImagesRoot)
	mux.HandleFunc("/api/images/", docker.ImagesPath)
	mux.HandleFunc("/api/system/prune", docker.SystemPrune)
	if state != nil && !state.IsEmpty() {
		mux.Handle("POST /api/auth/rotate", HandleAuthRotate(state))
	}
}
