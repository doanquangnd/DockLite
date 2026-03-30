package httpserver

import (
	"net/http"
	"strconv"
	"sync/atomic"
)

var httpRequestTotal uint64

// Metrics trả về số đếm request HTTP đơn giản (Prometheus-style text).
func Metrics(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "text/plain; charset=utf-8")
	n := atomic.LoadUint64(&httpRequestTotal)
	_, _ = w.Write([]byte("docklite_http_requests_total " + strconv.FormatUint(n, 10) + "\n"))
}

// IncRequestCount tăng bộ đếm (gọi từ middleware).
func IncRequestCount() {
	atomic.AddUint64(&httpRequestTotal, 1)
}
