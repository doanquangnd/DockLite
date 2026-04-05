package httpserver

import "strings"

const maxIncomingRequestIDLen = 64

// normalizeIncomingRequestID chấp nhận client gửi X-Request-ID: chỉ ký tự an toàn, giới hạn độ dài.
func normalizeIncomingRequestID(s string) string {
	s = strings.TrimSpace(s)
	if s == "" {
		return ""
	}
	if len(s) > maxIncomingRequestIDLen {
		s = s[:maxIncomingRequestIDLen]
	}
	for _, r := range s {
		switch {
		case r >= 'a' && r <= 'z':
		case r >= 'A' && r <= 'Z':
		case r >= '0' && r <= '9':
		case r == '-' || r == '_':
		default:
			return ""
		}
	}
	return s
}
