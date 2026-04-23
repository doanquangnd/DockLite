package wslimit

import (
	"net/http/httptest"
	"testing"
)

func TestTryAcquireRelease(t *testing.T) {
	if !TryAcquireWebSocket() {
		t.Fatal("first acquire must succeed")
	}
	ReleaseWebSocket()
}

func TestCheckOriginRequest_rejectsUnknownOrigin(t *testing.T) {
	t.Setenv("DOCKLITE_ALLOWED_ORIGINS", "")
	req := httptest.NewRequest("GET", "/", nil)
	req.Header.Set("Origin", "http://evil.com")
	if CheckOriginRequest(req) {
		t.Fatal("Origin http://evil.com phải bị từ chối")
	}
}

func TestCheckOriginRequest_allowsLoopbackOrigin(t *testing.T) {
	t.Setenv("DOCKLITE_ALLOWED_ORIGINS", "")
	req := httptest.NewRequest("GET", "/", nil)
	req.Header.Set("Origin", "http://127.0.0.1:9999")
	if !CheckOriginRequest(req) {
		t.Fatal("Origin 127.0.0.1 phải được chấp nhận")
	}
}

func TestCheckOriginRequest_emptyOriginAllowed(t *testing.T) {
	req := httptest.NewRequest("GET", "/", nil)
	if !CheckOriginRequest(req) {
		t.Fatal("Origin rỗng (client không gửi) phải được chấp nhận")
	}
}

func TestCheckOriginRequest_allowedOriginsEnv(t *testing.T) {
	t.Setenv("DOCKLITE_ALLOWED_ORIGINS", "http://192.168.1.10:80")
	req := httptest.NewRequest("GET", "/", nil)
	req.Header.Set("Origin", "http://192.168.1.10:80")
	if !CheckOriginRequest(req) {
		t.Fatal("Origin trong DOCKLITE_ALLOWED_ORIGINS phải được chấp nhận")
	}
}
