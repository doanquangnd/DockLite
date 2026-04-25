package httpserver

import (
	"net/http"
	"testing"
)

func TestIsAuditedPath(t *testing.T) {
	if !isAuditedPath(http.MethodPost, "/api/system/prune") {
		t.Fatal("prune nên audit")
	}
	if isAuditedPath(http.MethodGet, "/api/system/prune") {
		t.Fatal("prune GET không nên")
	}
	if isAuditedPath(http.MethodGet, "/api/health") {
		t.Fatal("health không audit")
	}
	if !isAuditedPath(http.MethodPost, "/api/auth/rotate") {
		t.Fatal("rotate audit")
	}
}
