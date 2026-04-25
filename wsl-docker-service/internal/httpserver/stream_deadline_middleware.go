package httpserver

import (
	"net/http"
	"strings"
	"time"
)

const longLivedRequestDuration = 30 * time.Minute

// ExtendLongLivedRequestDeadlines nới deadline đọc/ghi TCP thân kết nối cho route streaming / dài (AUD-02);
// server mặc định Read/Write 60s; route trong whitelist tới 30 phút.
func ExtendLongLivedRequestDeadlines(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if isLongLivedPath(r.Method, r.URL.Path) {
			d := time.Now().Add(longLivedRequestDuration)
			rc := http.NewResponseController(w)
			_ = rc.SetReadDeadline(d)
			_ = rc.SetWriteDeadline(d)
		}
		next.ServeHTTP(w, r)
	})
}

// isLongLivedPath trùng tài liệu yêu cầu + /ws/ và /api/docker/events/stream (stream dài thực tế).
func isLongLivedPath(method, path string) bool {
	if strings.HasPrefix(path, "/ws/") {
		return true
	}
	if path == "/api/docker/events/stream" && method == http.MethodGet {
		return true
	}
	if path == "/api/images/trivy-scan" && method == http.MethodPost {
		return true
	}
	switch {
	case method == http.MethodPost && path == "/api/compose/up":
		return true
	case method == http.MethodPost && path == "/api/compose/down":
		return true
	case method == http.MethodPost && path == "/api/compose/service/logs":
		return true
	case method == http.MethodPost && path == "/api/images/load":
		return true
	case method == http.MethodPost && path == "/api/images/pull":
		return true
	case method == http.MethodPost && path == "/api/images/pull/stream":
		return true
	}
	// Suffix: /api/containers/.../logs/stream hoặc .../stats/stream (nếu có)
	if strings.HasPrefix(path, "/api/containers/") {
		if strings.HasSuffix(path, "/logs/stream") || strings.HasSuffix(path, "/stats/stream") {
			return true
		}
	}
	return false
}
