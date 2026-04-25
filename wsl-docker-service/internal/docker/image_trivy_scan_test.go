package docker

import "testing"

func TestValidateTrivyImageRef_RejectLeadingDash(t *testing.T) {
	cases := []string{
		"--format=json",
		"-alpine:latest",
		" -foo",
	}
	for _, c := range cases {
		if err := validateTrivyImageRef(c); err == nil {
			t.Fatalf("mong đợi reject cho %q", c)
		}
	}
}

func TestValidateTrivyImageRef_AcceptNormalRef(t *testing.T) {
	if err := validateTrivyImageRef("alpine:latest"); err != nil {
		t.Fatalf("không mong đợi lỗi: %v", err)
	}
}
