package httpserver

import (
	"crypto/subtle"
	"net/http"
	"strings"
)

// RequireBearerToken bọc middleware khi token đang bật (từ biến môi trường lúc khởi động, có thể cập nhật sau khi xoay).
func RequireBearerToken(m *MutableToken, next http.Handler) http.Handler {
	if m == nil || m.IsEmpty() {
		return next
	}

	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		exp := m.BytesCopy()
		if exp == nil {
			next.ServeHTTP(w, r)
			return
		}

		if !matchBearerOrHeader(r, exp) {
			w.Header().Set("WWW-Authenticate", `Bearer realm="docklite"`)
			http.Error(w, "unauthorized", http.StatusUnauthorized)
			return
		}

		next.ServeHTTP(w, r)
	})
}

func matchBearerOrHeader(r *http.Request, expected []byte) bool {
	if len(expected) == 0 {
		return false
	}

	auth := strings.TrimSpace(r.Header.Get("Authorization"))
	const prefix = "Bearer "
	if len(auth) >= len(prefix) && strings.EqualFold(auth[:len(prefix)], prefix) {
		got := strings.TrimSpace(auth[len(prefix):])
		return constantTimeEqualBytes([]byte(got), expected)
	}

	alt := strings.TrimSpace(r.Header.Get("X-DockLite-Token"))
	return constantTimeEqualBytes([]byte(alt), expected)
}

func constantTimeEqualBytes(a, b []byte) bool {
	if len(a) != len(b) {
		return false
	}

	return subtle.ConstantTimeCompare(a, b) == 1
}
