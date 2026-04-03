package compose

import "sync"

// Bộ nhớ đệm danh sách project trong RAM để tránh đọc compose_projects.json lặp lại
// (GET list, resolveComposeProject, docker compose, v.v.). Đồng bộ sau mỗi saveProjects.
var (
	projectCacheMu     sync.RWMutex
	projectCache       []Project
	projectCacheLoaded bool
)

// cloneProjects tạo bản sao sâu (ComposeFiles) để caller có thể sửa mà không ảnh hưởng cache.
func cloneProjects(items []Project) []Project {
	if len(items) == 0 {
		return nil
	}
	out := make([]Project, len(items))
	for i := range items {
		out[i] = items[i]
		if len(items[i].ComposeFiles) > 0 {
			out[i].ComposeFiles = append([]string(nil), items[i].ComposeFiles...)
		}
	}
	return out
}
