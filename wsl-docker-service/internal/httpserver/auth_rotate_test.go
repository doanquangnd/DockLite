package httpserver

import (
	"bytes"
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"strings"
	"testing"
)

func TestHandleAuthRotate_success(t *testing.T) {
	st := NewMutableToken("secret-initial")
	mux := http.NewServeMux()
	mux.Handle("POST /api/auth/rotate", HandleAuthRotate(st))
	srv := httptest.NewServer(mux)
	t.Cleanup(srv.Close)

	body := `{"current_token":"secret-initial"}`
	req, _ := http.NewRequest(http.MethodPost, srv.URL+"/api/auth/rotate", strings.NewReader(body))
	req.Header.Set("Content-Type", "application/json")
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		t.Fatal(err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusOK {
		t.Fatalf("status: %d", resp.StatusCode)
	}

	var env struct {
		Success bool `json:"success"`
		Data    struct {
			NewToken string `json:"new_token"`
		} `json:"data"`
	}
	if err := json.NewDecoder(resp.Body).Decode(&env); err != nil {
		t.Fatal(err)
	}
	if !env.Success || len(env.Data.NewToken) < 8 {
		t.Fatalf("envelope: %+v", env)
	}
	if st.CompareBytes([]byte("secret-initial")) {
		t.Fatal("mật khẩu phải đổi sau khi thành công")
	}
}

func TestHandleAuthRotate_wrongCurrent(t *testing.T) {
	st := NewMutableToken("good")
	mux := http.NewServeMux()
	mux.Handle("POST /api/auth/rotate", HandleAuthRotate(st))
	srv := httptest.NewServer(mux)
	t.Cleanup(srv.Close)

	req, _ := http.NewRequest(http.MethodPost, srv.URL+"/api/auth/rotate", bytes.NewBufferString(`{"current_token":"bad"}`))
	req.Header.Set("Content-Type", "application/json")
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		t.Fatal(err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusUnauthorized {
		t.Fatalf("kỳ vọng 401, nhận %d", resp.StatusCode)
	}
}

func TestHandleAuthRotate_authOff(t *testing.T) {
	st := NewMutableToken("")
	mux := http.NewServeMux()
	mux.Handle("POST /api/auth/rotate", HandleAuthRotate(st))
	srv := httptest.NewServer(mux)
	t.Cleanup(srv.Close)

	req, _ := http.NewRequest(http.MethodPost, srv.URL+"/api/auth/rotate", bytes.NewBufferString(`{"current_token":"x"}`))
	req.Header.Set("Content-Type", "application/json")
	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		t.Fatal(err)
	}
	defer resp.Body.Close()
	if resp.StatusCode != http.StatusNotFound {
		t.Fatalf("status: %d", resp.StatusCode)
	}
}
