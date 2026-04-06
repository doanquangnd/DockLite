package compose

import (
	"fmt"
	"strings"
)

// normalizeComposeProfiles chuẩn hóa danh sách profile (tối đa 32, mỗi tên tối đa 128 ký tự).
func normalizeComposeProfiles(in []string) ([]string, error) {
	var out []string
	for _, s := range in {
		s = strings.TrimSpace(s)
		if s == "" {
			continue
		}
		if len(s) > 128 {
			return nil, fmt.Errorf("tên profile quá dài")
		}
		if strings.ContainsAny(s, ";&|`$\n\r") {
			return nil, fmt.Errorf("tên profile không hợp lệ: %s", s)
		}
		out = append(out, s)
		if len(out) >= 32 {
			break
		}
	}
	return out, nil
}
