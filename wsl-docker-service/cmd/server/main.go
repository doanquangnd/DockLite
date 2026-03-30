package main

import (
	"log"
	"net/http"
	"os"

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

	srv := &http.Server{
		Addr:              addr,
		Handler:           httpserver.LogRequests(mux),
		ReadHeaderTimeout: httpserver.ReadHeaderTimeout,
	}

	log.Printf("docklite-wsl lắng nghe %s (REST + WS + compose + images + prune)", addr)
	if err := srv.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		log.Fatal(err)
	}
}
