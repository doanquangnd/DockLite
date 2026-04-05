package httpserver

import (
	"crypto/subtle"
	"net/http"
	"strings"
)

// RequireBearerToken bọc middleware khi token đã được trim và khác rỗng (từ DOCKLITE_API_TOKEN).
func RequireBearerToken(token string, next http.Handler) http.Handler {
	expected := strings.TrimSpace(token)
	if expected == "" {
		return next
	}

	exp := []byte(expected)
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
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
