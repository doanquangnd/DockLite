package wslimit

import "testing"

func TestTryAcquireRelease(t *testing.T) {
	if !TryAcquireWebSocket() {
		t.Fatal("first acquire must succeed")
	}
	ReleaseWebSocket()
}
