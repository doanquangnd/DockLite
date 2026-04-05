//go:build integration

// Package integration chứa test tích hợp HTTP + Docker Engine (chạy: go test -tags=integration ./...).
package integration_test

import (
	"context"
	"encoding/json"
	"io"
	"net/http"
	"net/http/httptest"
	"testing"

	"docklite-wsl/internal/dockerengine"
	"docklite-wsl/internal/httpserver"
)

func TestAPIHealth(t *testing.T) {
	mux := http.NewServeMux()
	httpserver.Register(mux)
	srv := httptest.NewServer(mux)
	defer srv.Close()

	resp, err := http.Get(srv.URL + "/api/health")
	if err != nil {
		t.Fatal(err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		b, _ := io.ReadAll(resp.Body)
		t.Fatalf("GET /api/health: %d %s", resp.StatusCode, string(b))
	}
	var payload map[string]any
	if err := json.NewDecoder(resp.Body).Decode(&payload); err != nil {
		t.Fatal(err)
	}
	if payload["status"] != "ok" {
		t.Fatalf("status: %v", payload["status"])
	}
}

func TestAPIOpenAPIJSON(t *testing.T) {
	mux := http.NewServeMux()
	httpserver.Register(mux)
	srv := httptest.NewServer(mux)
	defer srv.Close()

	resp, err := http.Get(srv.URL + "/api/openapi.json")
	if err != nil {
		t.Fatal(err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		b, _ := io.ReadAll(resp.Body)
		t.Fatalf("GET /api/openapi.json: %d %s", resp.StatusCode, string(b))
	}
	var root map[string]any
	if err := json.NewDecoder(resp.Body).Decode(&root); err != nil {
		t.Fatal(err)
	}
	if root["openapi"] == nil {
		t.Fatal("thiếu trường openapi")
	}
}

func TestAPIDockerInfoWithEngine(t *testing.T) {
	ctx := context.Background()
	dc, err := dockerengine.Client()
	if err != nil {
		t.Skip("Docker client:", err)
	}
	if _, err := dc.Ping(ctx); err != nil {
		t.Skip("Docker Engine không sẵn sàng:", err)
	}

	mux := http.NewServeMux()
	httpserver.Register(mux)
	srv := httptest.NewServer(mux)
	defer srv.Close()

	resp, err := http.Get(srv.URL + "/api/docker/info")
	if err != nil {
		t.Fatal(err)
	}
	defer resp.Body.Close()
	body, _ := io.ReadAll(resp.Body)
	if resp.StatusCode != http.StatusOK {
		t.Fatalf("GET /api/docker/info: %d %s", resp.StatusCode, string(body))
	}
	var env map[string]any
	if err := json.Unmarshal(body, &env); err != nil {
		t.Fatal(err)
	}
	success, ok := env["success"].(bool)
	if !ok || !success {
		t.Fatalf("envelope không thành công: %s", string(body))
	}
}
