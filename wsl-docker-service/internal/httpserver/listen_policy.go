package httpserver

import (
	"fmt"
	"net"
)

// ListenRequiresToken trả về (true, lý_do) nếu bắt buộc phải có DOCKLITE_API_TOKEN để lắng nghe an toàn;
// (false, "") nếu được phép khởi động khi token rỗng (chỉ loopback).
func ListenRequiresToken(addr string) (bool, string) {
	tcpAddr, err := net.ResolveTCPAddr("tcp", addr)
	if err != nil {
		return true, fmt.Sprintf("không phân giải DOCKLITE_ADDR: %v", err)
	}
	ip := tcpAddr.IP
	if ip == nil {
		// Trường hợp hiếm (tên miền không phân giải thành IP); thất bại an toàn.
		return true, "không xác định được IP cho địa chỉ lắng nghe — bắt buộc đặt DOCKLITE_API_TOKEN khi mở mạng."
	}
	if ip.IsLoopback() {
		return false, ""
	}
	if ip.IsUnspecified() {
		return true, "DOCKLITE_ADDR lắng nghe trên mọi giao diện (0.0.0.0 hoặc ::) — bắt buộc đặt DOCKLITE_API_TOKEN."
	}
	return true, "DOCKLITE_ADDR không phải địa chỉ loopback — bắt buộc đặt DOCKLITE_API_TOKEN khi lắng nghe trên mạng LAN."
}
