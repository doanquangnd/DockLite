package httpserver

import (
	"net/http"
	"time"
)

// Timeout đọc/ghi toàn bộ request/response (image load, compose dài). WebSocket sau Upgrade không dùng giới hạn này.
// Số kết nối WebSocket đồng thời: biến môi trường DOCKLITE_WS_MAX_CONNECTIONS (mặc định 64, tối đa 4096), gói internal/wslimit.
const (
	ReadTimeout  = 30 * time.Minute
	WriteTimeout = 30 * time.Minute
	IdleTimeout  = 2 * time.Minute
	// MaxBodyBytesPOST giới hạn thân POST/PATCH/PUT (trừ /api/images/load — đã có MaxBytesReader riêng).
	MaxBodyBytesPOST = 64 << 20
)

// LimitRequestBody giới hạn kích thước thân cho hầu hết POST; bỏ qua load image (tar lớn).
func LimitRequestBody(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch r.Method {
		case http.MethodPost, http.MethodPut, http.MethodPatch:
			if r.URL.Path == "/api/images/load" {
				break
			}
			r.Body = http.MaxBytesReader(w, r.Body, MaxBodyBytesPOST)
		default:
		}
		next.ServeHTTP(w, r)
	})
}
