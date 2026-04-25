package httpserver

import (
	"net"
	"net/http"
	"strings"
)

// clientIP lấy địa chỉ client (không cổng) cho rate-limit / audit; ưu tiên X-Forwarded-For phần tử đầu nếu có.
func clientIP(r *http.Request) string {
	if xff := strings.TrimSpace(r.Header.Get("X-Forwarded-For")); xff != "" {
		parts := strings.SplitN(xff, ",", 2)
		return strings.TrimSpace(parts[0])
	}
	h, _, err := net.SplitHostPort(r.RemoteAddr)
	if err != nil {
		return r.RemoteAddr
	}
	return h
}
