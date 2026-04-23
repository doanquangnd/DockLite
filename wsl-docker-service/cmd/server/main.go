package main

import (
	"fmt"
	"log"
	"log/slog"
	"net/http"
	"os"
	"strings"

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

	mux := http.NewServeMux()
	httpserver.Register(mux)

	inner := http.Handler(mux)
	if token != "" {
		inner = httpserver.RequireBearerToken(token, inner)
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
