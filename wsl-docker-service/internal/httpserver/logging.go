package httpserver

import (
	"crypto/rand"
	"encoding/hex"
	"log/slog"
	"net/http"
	"time"
)

func requestID() string {
	b := make([]byte, 4)
	if _, err := rand.Read(b); err != nil {
		return "00000000"
	}
	return hex.EncodeToString(b)
}

// LogRequests ghi nhật ký HTTP có req_id và thời gian xử lý (slog, key-value).
func LogRequests(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		IncRequestCount()
		id := requestID()
		start := time.Now()
		slog.Info("http_request", "method", r.Method, "path", r.URL.Path, "req_id", id)
		next.ServeHTTP(w, r)
		slog.Info("http_request_done", "method", r.Method, "path", r.URL.Path, "req_id", id, "ms", time.Since(start).Milliseconds())
	})
}
