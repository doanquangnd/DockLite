package httpserver

import "testing"

func TestListenRequiresToken(t *testing.T) {
	tests := []struct {
		addr    string
		want    bool
		nameTag string
	}{
		{"127.0.0.1:17890", false, "loopback_ipv4"},
		{"[::1]:17890", false, "loopback_ipv6"},
		{"0.0.0.0:17890", true, "unspecified_ipv4"},
		{"[::]:17890", true, "unspecified_ipv6"},
		{"192.168.1.1:17890", true, "lan_literal"},
	}
	for _, tt := range tests {
		t.Run(tt.nameTag, func(t *testing.T) {
			got, _ := ListenRequiresToken(tt.addr)
			if got != tt.want {
				t.Fatalf("ListenRequiresToken(%q) requiresToken = %v, want %v", tt.addr, got, tt.want)
			}
		})
	}
}
