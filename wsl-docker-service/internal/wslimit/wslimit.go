// Package wslimit giới hạn tài nguyên WebSocket trên service (số kết nối đồng thời, buffer nâng cấp, kích thước tin nhắn đọc từ client).
package wslimit

import (
	"net/http"
	"net/url"
	"os"
	"strconv"
	"strings"

	"github.com/gorilla/websocket"
)

// MaxWebSocketMessageBytes là giới hạn một tin nhắn đọc từ client sau Upgrade (gorilla mặc định không giới hạn).
const MaxWebSocketMessageBytes = 1 << 20

var wsSlots chan struct{}

func init() {
	max := 64
	if v := os.Getenv("DOCKLITE_WS_MAX_CONNECTIONS"); v != "" {
		if n, err := strconv.Atoi(v); err == nil && n > 0 {
			if n > 4096 {
				n = 4096
			}
			max = n
		}
	}
	wsSlots = make(chan struct{}, max)
}

// Upgrader dùng chung cho luồng log và stats (buffer cố định, tránh chiến động bộ nhớ mặc định quá lớn trên mỗi kết nối).
var Upgrader = websocket.Upgrader{
	CheckOrigin:     buildCheckOriginFunc(),
	ReadBufferSize:  4096,
	WriteBufferSize: 4096,
}

func buildCheckOriginFunc() func(*http.Request) bool {
	allowedCanon := parseAllowedOrigins(os.Getenv("DOCKLITE_ALLOWED_ORIGINS"))
	return func(r *http.Request) bool {
		return checkOriginRequest(r, allowedCanon)
	}
}

func parseAllowedOrigins(raw string) []string {
	if strings.TrimSpace(raw) == "" {
		return nil
	}
	parts := strings.Split(raw, ",")
	out := make([]string, 0, len(parts))
	for _, p := range parts {
		p = strings.TrimSpace(p)
		if p == "" {
			continue
		}
		u, err := url.Parse(p)
		if err != nil || u.Scheme == "" || u.Host == "" {
			continue
		}
		out = append(out, canonicalOriginURL(u))
	}
	return out
}

func canonicalOriginURL(u *url.URL) string {
	return strings.ToLower(u.Scheme) + "://" + strings.ToLower(u.Host)
}

func originHostAllowed(host string) bool {
	h := strings.ToLower(strings.TrimSpace(host))
	switch h {
	case "localhost", "127.0.0.1", "::1":
		return true
	default:
		return false
	}
}

// checkOriginRequest áp dụng cùng logic với Upgrader.CheckOrigin (dùng trong test).
func checkOriginRequest(r *http.Request, allowedCanon []string) bool {
	origin := strings.TrimSpace(r.Header.Get("Origin"))
	if origin == "" {
		return true
	}
	u, err := url.Parse(origin)
	if err != nil || u.Host == "" {
		return false
	}
	if originHostAllowed(u.Hostname()) {
		return true
	}
	canon := canonicalOriginURL(u)
	for _, a := range allowedCanon {
		if canon == a {
			return true
		}
	}
	return false
}

// CheckOriginRequest xuất ra cho test: trả về true nếu Origin được phép nâng cấp WebSocket.
func CheckOriginRequest(r *http.Request) bool {
	return checkOriginRequest(r, parseAllowedOrigins(os.Getenv("DOCKLITE_ALLOWED_ORIGINS")))
}

// TryAcquireWebSocket trả về false khi đã đạt giới hạn kết nối WebSocket đồng thời (HTTP 503 trước Upgrade).
func TryAcquireWebSocket() bool {
	select {
	case wsSlots <- struct{}{}:
		return true
	default:
		return false
	}
}

// ReleaseWebSocket giải phóng một slot đã lấy bằng TryAcquireWebSocket (mỗi acquire đúng một release).
func ReleaseWebSocket() {
	<-wsSlots
}

// ConfigureConnAfterUpgrade giới hạn đọc tin nhắn từ phía client (ping/đóng không cần vượt ngưỡng).
func ConfigureConnAfterUpgrade(conn *websocket.Conn) {
	conn.SetReadLimit(MaxWebSocketMessageBytes)
}
