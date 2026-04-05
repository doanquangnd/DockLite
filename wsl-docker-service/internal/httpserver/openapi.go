package httpserver

import (
	_ "embed"
	"net/http"
)

//go:embed openapi.json
var openAPISpec []byte

// OpenAPI trả tài liệu OpenAPI 3.0 (JSON) mô tả endpoint service.
func OpenAPI(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}

	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.Header().Set("Cache-Control", "public, max-age=3600")
	w.WriteHeader(http.StatusOK)
	_, _ = w.Write(openAPISpec)
}
