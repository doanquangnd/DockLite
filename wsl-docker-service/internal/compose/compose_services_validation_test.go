package compose

import "testing"

func TestValidateComposeServiceName_RejectLeadingDash(t *testing.T) {
	cases := []string{"-foo", " -bar", "--service"}
	for _, c := range cases {
		if err := validateComposeServiceName(c); err == nil {
			t.Fatalf("mong đợi reject cho %q", c)
		}
	}
}

func TestNormalizeComposeProfiles_RejectShellCharacters(t *testing.T) {
	cases := [][]string{
		{"default", "dev;rm"},
		{"safe", "prod|cat"},
		{"ok", "x&y"},
	}
	for _, c := range cases {
		if _, err := normalizeComposeProfiles(c); err == nil {
			t.Fatalf("mong đợi reject cho profile %#v", c)
		}
	}
}
