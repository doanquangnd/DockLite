package httpserver

import (
	"sync"
)

// MutableToken giữ mật khẩu API trong RAM; khi bắt đầu rỗng thì tắt middleware Bearer.
type MutableToken struct {
	mu  sync.RWMutex
	raw []byte
}

// NewMutableToken tạo trạng thái từ DOCKLITE_API_TOKEN (đã trim).
func NewMutableToken(initial string) *MutableToken {
	m := &MutableToken{}
	if initial != "" {
		m.raw = []byte(initial)
	}
	return m
}

// IsEmpty trả về true nếu chưa bật xác thực Bearer.
func (m *MutableToken) IsEmpty() bool {
	if m == nil {
		return true
	}
	m.mu.RLock()
	defer m.mu.RUnlock()
	return len(m.raw) == 0
}

// CompareBytes so khớp hằng thời gian hằng với giá trị lưu.
func (m *MutableToken) CompareBytes(b []byte) bool {
	if m == nil {
		return false
	}
	m.mu.RLock()
	defer m.mu.RUnlock()
	if len(m.raw) == 0 {
		return false
	}
	return constantTimeEqualBytes(m.raw, b)
}

// BytesCopy trả bản sao bí mật dùng cho middleware.
func (m *MutableToken) BytesCopy() []byte {
	if m == nil {
		return nil
	}
	m.mu.RLock()
	defer m.mu.RUnlock()
	if len(m.raw) == 0 {
		return nil
	}
	out := make([]byte, len(m.raw))
	copy(out, m.raw)
	return out
}

// Update thay bí mật toàn bộ (sau khi xoay).
func (m *MutableToken) Update(s string) {
	if m == nil {
		return
	}
	m.mu.Lock()
	defer m.mu.Unlock()
	m.raw = []byte(s)
}
