package httpserver

import (
	"net/http"
	"testing"
)

func TestIsLongLivedPath(t *testing.T) {
	cases := []struct {
		m, p   string
		long   bool
		reason string
	}{
		{http.MethodPost, "/api/compose/up", true, "compose up"},
		{http.MethodGet, "/api/health", false, "ngắn"},
		{http.MethodGet, "/ws/containers/abc", true, "ws"},
		{http.MethodPost, "/api/images/load", true, "load"},
		{http.MethodGet, "/api/docker/events/stream", true, "events"},
		{http.MethodPost, "/api/x", false, "khác"},
	}
	for _, c := range cases {
		if got := isLongLivedPath(c.m, c.p); got != c.long {
			t.Errorf("%s %s: want %v, got %v (%s)", c.m, c.p, c.long, got, c.reason)
		}
	}
}
