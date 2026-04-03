package httpserver

import (
	"context"
	"net/http"
	"strings"
	"time"
)

// RequestContextTimeout gắn context có deadline cho một số đường dẫn (inspect/stats/batch); 0 = không đổi.
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
	default:
		return 0
	}
}
