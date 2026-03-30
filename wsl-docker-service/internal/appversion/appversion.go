package appversion

import (
	_ "embed"
	"strings"
)

//go:embed VERSION
var raw string

// String trả phiên bản mã nguồn (một dòng trong file VERSION cùng thư mục package; go:embed không cho phép ../).
func String() string {
	return strings.TrimSpace(raw)
}
