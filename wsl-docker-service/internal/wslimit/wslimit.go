// Package wslimit giới hạn tài nguyên WebSocket trên service (số kết nối đồng thời, buffer nâng cấp, kích thước tin nhắn đọc từ client).
package wslimit

import (
	"net/http"
	"os"
	"strconv"

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
	CheckOrigin:     func(r *http.Request) bool { return true },
	ReadBufferSize:  4096,
	WriteBufferSize: 4096,
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
