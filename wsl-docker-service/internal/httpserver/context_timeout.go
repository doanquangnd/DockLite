package httpserver

import (
	"context"
	"net/http"
	"strings"
	"time"
)

// RequestContextTimeout gắn context có deadline cho một số đường dẫn (inspect/stats/batch, compose, image pull/load, system prune); 0 = không đổi.
func RequestContextTimeout(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		d := timeoutForRequest(r.URL.Path, r.Method)
		if d <= 0 {
			next.ServeHTTP(w, r)
			return
		}
		ctx, cancel := context.WithTimeout(r.Context(), d)
		defer cancel()
		next.ServeHTTP(w, r.WithContext(ctx))
	})
}

func timeoutForRequest(path, method string) time.Duration {
	switch {
	case strings.HasPrefix(path, "/ws/"):
		return 0
	case path == "/api/containers/stats-batch" && method == http.MethodPost:
		return 3 * time.Minute
	case strings.Contains(path, "/api/containers/") && strings.HasSuffix(path, "/inspect"):
		return 2 * time.Minute
	case strings.Contains(path, "/api/containers/") && strings.HasSuffix(path, "/stats"):
		return 90 * time.Second
	case composeLongTimeoutPOST(path, method):
		return 30 * time.Minute
	case path == "/api/images/pull" && method == http.MethodPost:
		return 30 * time.Minute
	case path == "/api/images/pull/stream" && method == http.MethodPost:
		return 30 * time.Minute
	case path == "/api/docker/events/stream" && method == http.MethodGet:
		return 0
	case path == "/api/images/trivy-scan" && method == http.MethodPost:
		return 30 * time.Minute
	case path == "/api/images/load" && method == http.MethodPost:
		return 30 * time.Minute
	case path == "/api/system/prune" && method == http.MethodPost:
		return 15 * time.Minute
	default:
		return 0
	}
}

func composeLongTimeoutPOST(path, method string) bool {
	if method != http.MethodPost {
		return false
	}
	switch path {
	case "/api/compose/up", "/api/compose/down", "/api/compose/ps",
		"/api/compose/config/validate", "/api/compose/config/services",
		"/api/compose/service/start", "/api/compose/service/stop",
		"/api/compose/service/logs", "/api/compose/service/exec":
		return true
	default:
		return false
	}
}
