package docklitetls

import (
	"crypto/x509"
	"encoding/pem"
	"os"
	"testing"
)

func TestEnsurePaths_CreatesKeypair(t *testing.T) {
	home := t.TempDir()
	t.Setenv("HOME", home)
	cert, key, err := EnsurePaths()
	if err != nil {
		t.Fatal(err)
	}
	if _, err = os.Stat(cert); err != nil {
		t.Fatalf("cert: %v", err)
	}
	if _, err = os.Stat(key); err != nil {
		t.Fatalf("key: %v", err)
	}
	b, err := os.ReadFile(cert)
	if err != nil {
		t.Fatal(err)
	}
	block, _ := pem.Decode(b)
	if block == nil {
		t.Fatal("PEM rỗng")
	}
	c, err := x509.ParseCertificate(block.Bytes)
	if err != nil {
		t.Fatal(err)
	}
	if c.Subject.CommonName != cnName {
		t.Fatalf("CN = %q, muốn %q", c.Subject.CommonName, cnName)
	}
}

func TestEnsurePaths_ReuseFiles(t *testing.T) {
	home := t.TempDir()
	t.Setenv("HOME", home)
	c1, _, err := EnsurePaths()
	if err != nil {
		t.Fatal(err)
	}
	b1, err := os.ReadFile(c1)
	if err != nil {
		t.Fatal(err)
	}
	_, _, err = EnsurePaths()
	if err != nil {
		t.Fatal(err)
	}
	b2, err := os.ReadFile(c1)
	if err != nil {
		t.Fatal(err)
	}
	if string(b1) != string(b2) {
		t.Fatal("lần hai phải tái sử dụng cùng tệp cert")
	}
}
