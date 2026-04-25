package httpserver

import (
	"net/http"
	"net/http/httptest"
	"testing"
)

func TestPerIPRateLimit_429_Va_Retry_After(t *testing.T) {
	h := http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.WriteHeader(http.StatusOK)
	})
	srv := httptest.NewServer(PerIPRateLimit(h))
	t.Cleanup(srv.Close)

	var n429, withRetry int
	for i := 0; i < 200; i++ {
		res, err := http.Get(srv.URL)
		if err != nil {
			t.Fatal(err)
		}
		if res.StatusCode == http.StatusTooManyRequests {
			n429++
			if res.Header.Get("Retry-After") != "" {
				withRetry++
			}
		}
		_ = res.Body.Close()
	}
	if n429 < 1 {
		t.Fatalf("kỳ vọng ít nhất 1 lần 429, thực tế: %d", n429)
	}
	if withRetry < 1 {
		t.Fatal("kỳ vọng 429 kèm Retry-After")
	}
}
