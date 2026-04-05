package httpserver

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
)

func TestOpenAPISpec_embeddedJSONValid(t *testing.T) {
	var root map[string]interface{}
	if err := json.Unmarshal(openAPISpec, &root); err != nil {
		t.Fatalf("openapi.json: %v", err)
	}
	if v, ok := root["openapi"].(string); !ok || v == "" {
		t.Fatal("thiếu hoặc sai trường openapi")
	}
	paths, ok := root["paths"].(map[string]interface{})
	if !ok || len(paths) < 5 {
		t.Fatal("thiếu paths")
	}
}

func TestOpenAPI_handlerGET(t *testing.T) {
	req := httptest.NewRequest(http.MethodGet, "/api/openapi.json", nil)
	rec := httptest.NewRecorder()
	OpenAPI(rec, req)
	if rec.Code != http.StatusOK {
		t.Fatalf("status=%d", rec.Code)
	}
	ct := rec.Header().Get("Content-Type")
	if !strings.HasPrefix(ct, "application/json") {
		t.Fatalf("Content-Type=%q", ct)
	}
}

func TestOpenAPI_handlerMethodNotAllowed(t *testing.T) {
	req := httptest.NewRequest(http.MethodPost, "/api/openapi.json", nil)
	rec := httptest.NewRecorder()
	OpenAPI(rec, req)
	if rec.Code != http.StatusMethodNotAllowed {
		t.Fatalf("status=%d", rec.Code)
	}
}
