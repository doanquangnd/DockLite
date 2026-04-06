package compose

import (
	"path/filepath"
	"strings"
)

// dockerComposeArgs tạo đối số sau lệnh `docker`: compose [-f ...] [--profile ...] <rest...>
func dockerComposeArgs(p Project, profiles []string, rest ...string) []string {
	args := []string{"compose"}
	for _, f := range p.ComposeFiles {
		f = strings.TrimSpace(f)
		if f == "" {
			continue
		}
		args = append(args, "-f", filepath.Clean(f))
	}
	for _, prof := range profiles {
		prof = strings.TrimSpace(prof)
		if prof == "" {
			continue
		}
		args = append(args, "--profile", prof)
	}
	args = append(args, rest...)
	return args
}
