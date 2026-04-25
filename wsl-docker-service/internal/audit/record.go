// Package audit ghi bản ghi JSON nhạy cảm ra stdout và file xoay (AUD-03, AUD-04).
package audit

// Record mô tả một dòng audit (một số tên thuộc tính JSON cố định theo yêu cầu).
type Record struct {
	Ts         string  `json:"ts"`
	RemoteIP   string  `json:"remote_ip"`
	UserAgent  string  `json:"user_agent"`
	Method     string  `json:"method"`
	Path       string  `json:"path"`
	Status     int     `json:"status"`
	AuthStatus string  `json:"auth_status"`
	RequestID  string  `json:"request_id"`
	LatencyMs  float64 `json:"latency_ms"`
}
