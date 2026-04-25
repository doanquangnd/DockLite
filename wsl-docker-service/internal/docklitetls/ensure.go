// Package docklitetls tạo/tái sử dụng cert TLS tự ký (ECDSA P-256) cho service HTTP.
package docklitetls

import (
	"crypto/ecdsa"
	"crypto/elliptic"
	"crypto/rand"
	"crypto/sha256"
	"crypto/x509"
	"crypto/x509/pkix"
	"encoding/hex"
	"encoding/pem"
	"errors"
	"math/big"
	"net"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"time"
)

const (
	// Tên tệp cert và key bên dưới thư mục tls (trong $HOME/.docklite/tls).
	certPem = "cert.pem"
	keyPem  = "key.pem"
	// Tên mục gán cho Subject CN (TLS-02).
	cnName = "DockLite WSL Service"
)

// EnsurePaths tạo hoặc tái sử dụng cặp cert/key trong thư mục $HOME/.docklite/tls.
// Trả về đường dẫn tuyệt đối tới cert.pem và key.pem. Key được chmod 0600 trên hệ thống Unix.
func EnsurePaths() (certPath, keyPath string, err error) {
	home, err := os.UserHomeDir()
	if err != nil {
		return "", "", err
	}
	dir := filepath.Join(home, ".docklite", "tls")
	if err = os.MkdirAll(dir, 0o700); err != nil {
		return "", "", err
	}
	certPath = filepath.Join(dir, certPem)
	keyPath = filepath.Join(dir, keyPem)
	if canReuse, e := canReuseKeypair(certPath, keyPath); e != nil {
		return "", "", e
	} else if canReuse {
		if err = chmodFile0600IfUnix(keyPath); err != nil {
			return "", "", err
		}
		return certPath, keyPath, nil
	}
	if err = generateToFiles(certPath, keyPath); err != nil {
		return "", "", err
	}
	if err = chmodFile0600IfUnix(keyPath); err != nil {
		return "", "", err
	}
	return certPath, keyPath, nil
}

func canReuseKeypair(certPath, keyPath string) (bool, error) {
	cb, err := os.ReadFile(certPath)
	if err != nil {
		if os.IsNotExist(err) {
			return false, nil
		}
		return false, err
	}
	kb, err := os.ReadFile(keyPath)
	if err != nil {
		if os.IsNotExist(err) {
			return false, nil
		}
		return false, err
	}
	certBlock, _ := pem.Decode(cb)
	if certBlock == nil {
		return false, nil
	}
	cert, err := x509.ParseCertificate(certBlock.Bytes)
	if err != nil {
		return false, nil
	}
	if time.Now().After(cert.NotAfter) {
		return false, nil
	}
	_ = cert
	kBlock, _ := pem.Decode(kb)
	if kBlock == nil {
		return false, nil
	}
	if kAny, err := x509.ParsePKCS8PrivateKey(kBlock.Bytes); err == nil {
		if _, ok := kAny.(*ecdsa.PrivateKey); ok {
			return true, nil
		}
		return false, nil
	}
	if _, err := x509.ParseECPrivateKey(kBlock.Bytes); err == nil {
		return true, nil
	}
	return false, nil
}

func chmodFile0600IfUnix(p string) error {
	if runtime.GOOS == "windows" {
		return nil
	}
	return os.Chmod(p, 0o600)
}

func generateToFiles(certPath, keyPath string) error {
	key, err := ecdsa.GenerateKey(elliptic.P256(), rand.Reader)
	if err != nil {
		return err
	}
	keyBytes, err := x509.MarshalPKCS8PrivateKey(key)
	if err != nil {
		return err
	}
	keyPemB := pem.EncodeToMemory(&pem.Block{Type: "PRIVATE KEY", Bytes: keyBytes})
	if err = os.WriteFile(keyPath, keyPemB, 0o600); err != nil {
		return err
	}
	if runtime.GOOS != "windows" {
		_ = os.Chmod(keyPath, 0o600)
	}

	serial, err := randomSerial()
	if err != nil {
		return err
	}
	ips, dns, err := collectSanDnsAndIp()
	if err != nil {
		return err
	}
	tpl := x509.Certificate{
		SerialNumber:    serial,
		Subject:         pkix.Name{CommonName: cnName},
		NotBefore:       time.Now().Add(-1 * time.Hour),
		NotAfter:        time.Now().AddDate(10, 0, 0), // 10 năm (TLS-02)
		KeyUsage:        x509.KeyUsageDigitalSignature | x509.KeyUsageKeyEncipherment,
		ExtKeyUsage:     []x509.ExtKeyUsage{x509.ExtKeyUsageServerAuth},
		BasicConstraintsValid: true,
		IsCA:            false,
		SignatureAlgorithm:    x509.ECDSAWithSHA256,
		IPAddresses:     ips,
		DNSNames:        dns,
	}
	certDer, err := x509.CreateCertificate(rand.Reader, &tpl, &tpl, &key.PublicKey, key)
	if err != nil {
		return err
	}
	certPemB := pem.EncodeToMemory(&pem.Block{Type: "CERTIFICATE", Bytes: certDer})
	if err = os.WriteFile(certPath, certPemB, 0o644); err != nil {
		return err
	}
	return nil
}

func randomSerial() (*big.Int, error) {
	b := make([]byte, 8)
	if _, err := rand.Read(b); err != nil {
		return nil, err
	}
	return new(big.Int).SetBytes(b), nil
}

// collectSanDnsAndIp thu thập 127.0.0.1, ::1, hostname, IP từ giao diện, và bổ sung từ DOCKLITE_TLS_EXTRA_SAN.
func collectSanDnsAndIp() ([]net.IP, []string, error) {
	ipByName := make(map[string]net.IP)
	dnsSet := make(map[string]struct{})

	addIP := func(ip net.IP) {
		if len(ip) == 0 {
			return
		}
		// Cùng giao diện: chuỗi chuẩn để gỡ trùng (IPv4 / IPv6).
		if v4 := ip.To4(); v4 != nil {
			ip = v4
		}
		ipByName[ip.String()] = ip
	}
	addIP(net.IPv4(127, 0, 0, 1))
	addIP(net.IPv6loopback)

	hostname, err := os.Hostname()
	if err == nil && hostname != "" {
		if ip := net.ParseIP(hostname); ip != nil {
			addIP(ip)
		} else {
			dnsSet[hostname] = struct{}{}
		}
	}

	ifaces, err := net.Interfaces()
	if err == nil {
		for _, ni := range ifaces {
			addrs, aerr := ni.Addrs()
			if aerr != nil {
				continue
			}
			for _, a := range addrs {
				switch v := a.(type) {
				case *net.IPNet:
					if v.IP == nil {
						continue
					}
					if v.IP.IsLoopback() {
						continue
					}
					if v4 := v.IP.To4(); v4 != nil {
						addIP(v4)
					} else {
						addIP(v.IP)
					}
				case *net.IPAddr:
					if v.IP == nil || v.IP.IsLoopback() {
						continue
					}
					addIP(v.IP)
				}
			}
		}
	}

	if extra := strings.TrimSpace(os.Getenv("DOCKLITE_TLS_EXTRA_SAN")); extra != "" {
		for p := range strings.SplitSeq(extra, ",") {
			p = strings.TrimSpace(p)
			if p == "" {
				continue
			}
			if ip := net.ParseIP(p); ip != nil {
				addIP(ip)
			} else {
				dnsSet[p] = struct{}{}
			}
		}
	}

	ips := make([]net.IP, 0, len(ipByName))
	for _, ip := range ipByName {
		ips = append(ips, ip)
	}
	dns := make([]string, 0, len(dnsSet))
	for d := range dnsSet {
		dns = append(dns, d)
	}
	return ips, dns, nil
}

// CertFingerprintSHA256 tính SHA-256 (hex) của tệp cert (cho debug/log).
func CertFingerprintSHA256(certPemPath string) (string, error) {
	b, err := os.ReadFile(certPemPath)
	if err != nil {
		return "", err
	}
	block, _ := pem.Decode(b)
	if block == nil {
		return "", errors.New("PEM rỗng")
	}
	c, err := x509.ParseCertificate(block.Bytes)
	if err != nil {
		return "", err
	}
	h := sha256.Sum256(c.Raw)
	return strings.ToLower(hex.EncodeToString(h[:])), nil
}
