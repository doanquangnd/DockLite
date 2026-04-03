package compose

import (
	"path/filepath"
	"strings"
)

// dockerComposeArgs tạo đối số sau lệnh `docker`: compose [-f ...] <rest...>
func dockerComposeArgs(p Project, rest ...string) []string {
	args := []string{"compose"}
	for _, f := range p.ComposeFiles {
		f = strings.TrimSpace(f)
		if f == "" {
			continue
		}
		args = append(args, "-f", filepath.Clean(f))
	}
	args = append(args, rest...)
	return args
}
