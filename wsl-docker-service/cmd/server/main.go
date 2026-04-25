package main

import (
	"crypto/tls"
	"fmt"
	"log"
	"log/slog"
	"net/http"
	"os"
	"strings"

	"docklite-wsl/internal/audit"
	"docklite-wsl/internal/docklitetls"
	"docklite-wsl/internal/httpserver"
	_ "docklite-wsl/internal/settings" // gói dành cho cấu hình server sau này
)

func main() {
	// Mặc định chỉ lắng nghe trên loopback (127.0.0.1). Để lắng nghe trên LAN / mọi giao diện, đặt DOCKLITE_ADDR
	// (ví dụ 0.0.0.0:17890) và bắt buộc DOCKLITE_API_TOKEN (xem .env.example).
	addr := "127.0.0.1:17890"
	if v := os.Getenv("DOCKLITE_ADDR"); v != "" {
		addr = v
	}

	token := strings.TrimSpace(os.Getenv("DOCKLITE_API_TOKEN"))
	if token == "" {
		if need, reason := httpserver.ListenRequiresToken(addr); need {
			slog.Error("docklite-wsl_refuse_start", "addr", addr, "reason", reason)
			fmt.Fprintln(os.Stderr, "docklite-wsl: không thể khởi động — thiếu DOCKLITE_API_TOKEN nhưng địa chỉ lắng nghe mở ra ngoài loopback.")
			fmt.Fprintln(os.Stderr, reason)
			os.Exit(2)
		}
	}

	if err := audit.Init(); err != nil {
		slog.Warn("audit_file_init_ignored", "err", err)
	}

	mux := http.NewServeMux()
	state := httpserver.NewMutableToken(token)
	httpserver.Register(mux, state)

	inner := http.Handler(mux)
	inner = httpserver.RequireBearerToken(state, inner)
	inner = httpserver.ExtendLongLivedRequestDeadlines(inner)
	inner = httpserver.LimitRequestBody(inner)
	inner = httpserver.RequestContextTimeout(inner)
	inner = httpserver.AuditSecuritySensitive(state, inner)
	inner = httpserver.PerIPRateLimit(inner)
	handler := httpserver.LogRequests(inner)

	srv := &http.Server{
		Addr:              addr,
		Handler:           handler,
		ReadHeaderTimeout: httpserver.ReadHeaderTimeout,
		ReadTimeout:       httpserver.ReadTimeout,
		WriteTimeout:      httpserver.WriteTimeout,
		IdleTimeout:       httpserver.IdleTimeout,
	}

	tlsOn := strings.EqualFold(strings.TrimSpace(os.Getenv("DOCKLITE_TLS_ENABLED")), "true")
	if tlsOn {
		certFile, keyFile, err := docklitetls.EnsurePaths()
		if err != nil {
			slog.Error("docklite-wsl_tls_cert_failed", "err", err)
			fmt.Fprintln(os.Stderr, "docklite-wsl: không tạo/đọc cert TLS tại ~/.docklite/tls:", err)
			os.Exit(2)
		}
		srv.TLSConfig = &tls.Config{MinVersion: tls.VersionTLS12}
		slog.Info("docklite-wsl_listen", "addr", addr, "tls", true, "cert_pem", certFile)
		if err := srv.ListenAndServeTLS(certFile, keyFile); err != nil && err != http.ErrServerClosed {
			log.Fatal(err)
		}
		return
	}

	slog.Info("docklite-wsl_listen", "addr", addr, "tls", false)
	if err := srv.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		log.Fatal(err)
	}
}
