package httpserver

import (
	"net/http"
	"strings"
	"time"
	"unicode/utf8"

	"docklite-wsl/internal/audit"
	"log/slog"
)

type captureStatus struct {
	http.ResponseWriter
	status int
}

func (c *captureStatus) WriteHeader(code int) {
	if c.status == 0 {
		c.status = code
	}
	c.ResponseWriter.WriteHeader(code)
}

func (c *captureStatus) Write(b []byte) (int, error) {
	if c.status == 0 {
		c.status = http.StatusOK
	}
	return c.ResponseWriter.Write(b)
}

// AuditSecuritySensitive ghi bản ghi JSON cho endpoint nêu tại yêu cầu AUD-03; không log token/Authorization.
func AuditSecuritySensitive(m *MutableToken, next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		if !isAuditedPath(r.Method, r.URL.Path) {
			next.ServeHTTP(w, r)
			return
		}
		start := time.Now()
		cw := &captureStatus{ResponseWriter: w, status: 0}
		next.ServeHTTP(cw, r)
		st := cw.status
		if st == 0 {
			st = http.StatusOK
		}
		rec := audit.Record{
			RemoteIP:  clientIP(r),
			UserAgent: truncateRunes(r.Header.Get("User-Agent"), 256),
			Method:    r.Method,
			Path:      r.URL.Path,
			Status:    st,
			AuthStatus: authStatusForRecord(m, st),
			RequestID: RequestIDFromContext(r.Context()),
			LatencyMs: float64(time.Since(start).Milliseconds()),
		}
		if err := audit.WriteJSON(rec); err != nil {
			slog.Error("audit_write_failed", "err", err)
		}
	})
}

func authStatusForRecord(m *MutableToken, status int) string {
	if m == nil || m.IsEmpty() {
		return "none"
	}
	if status == http.StatusUnauthorized {
		return "invalid"
	}
	return "valid"
}

// isAuditedPath endpoint nhạy cảm theo AUD-03.
func isAuditedPath(method, path string) bool {
	switch {
	case method == http.MethodPost && path == "/api/system/prune":
		return true
	case method == http.MethodPost && path == "/api/compose/up":
		return true
	case method == http.MethodPost && path == "/api/compose/down":
		return true
	case method == http.MethodPost && path == "/api/images/load":
		return true
	case method == http.MethodPost && path == "/api/images/pull":
		return true
	case method == http.MethodPost && path == "/api/images/pull/stream":
		return true
	case method == http.MethodPost && path == "/api/images/remove":
		return true
	case method == http.MethodPost && path == "/api/images/prune":
		return true
	case method == http.MethodPost && path == "/api/auth/rotate":
		return true
	}
	return false
}

func truncateRunes(s string, max int) string {
	if max <= 0 {
		return ""
	}
	if utf8.RuneCountInString(s) <= max {
		return s
	}
	var b strings.Builder
	n := 0
	for _, r := range s {
		if n >= max {
			break
		}
		b.WriteRune(r)
		n++
	}
	return b.String()
}
