package main

import (
	"log"
	"log/slog"
	"net/http"
	"os"
	"strings"

	"docklite-wsl/internal/httpserver"
	_ "docklite-wsl/internal/settings" // gói dành cho cấu hình server sau này
)

func main() {
	// 0.0.0.0 giúp Windows (localhost:17890) forward vào WSL ổn định hơn so với chỉ 127.0.0.1 trong một số cấu hình WSL2.
	addr := "0.0.0.0:17890"
	if v := os.Getenv("DOCKLITE_ADDR"); v != "" {
		addr = v
	}

	mux := http.NewServeMux()
	httpserver.Register(mux)

	inner := http.Handler(mux)
	if t := strings.TrimSpace(os.Getenv("DOCKLITE_API_TOKEN")); t != "" {
		inner = httpserver.RequireBearerToken(t, inner)
	}

	handler := httpserver.LogRequests(httpserver.RequestContextTimeout(httpserver.LimitRequestBody(inner)))
	srv := &http.Server{
		Addr:              addr,
		Handler:           handler,
		ReadHeaderTimeout: httpserver.ReadHeaderTimeout,
		ReadTimeout:       httpserver.ReadTimeout,
		WriteTimeout:      httpserver.WriteTimeout,
		IdleTimeout:       httpserver.IdleTimeout,
	}

	slog.Info("docklite-wsl_listen", "addr", addr)
	if err := srv.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		log.Fatal(err)
	}
}
