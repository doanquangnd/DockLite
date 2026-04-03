// Package compose lưu project Docker Compose trên đĩa và HTTP handler tương ứng.
package compose

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"strings"

	"docklite-wsl/internal/apiresponse"
)

func normalizeComposeWslPath(p string) string {
	return strings.TrimRight(strings.TrimSpace(p), "/")
}

func validateDefaultComposeFileInDir(wslPath string) error {
	norm := normalizeComposeWslPath(wslPath)
	if norm == "" {
		return fmt.Errorf("đường dẫn trống")
	}
	st, err := os.Stat(norm)
	if err != nil {
		if os.IsNotExist(err) {
			return fmt.Errorf("thư mục không tồn tại: %s", norm)
		}
		return fmt.Errorf("không truy cập được thư mục: %v", err)
	}
	if !st.IsDir() {
		return fmt.Errorf("không phải thư mục: %s", norm)
	}
	names := []string{"docker-compose.yml", "compose.yml", "docker-compose.yaml", "compose.yaml"}
	for _, name := range names {
		fp := filepath.Join(norm, name)
		if st2, err := os.Stat(fp); err == nil && !st2.IsDir() {
			return nil
		}
	}
	return fmt.Errorf("không tìm thấy docker-compose.yml hoặc compose.yml trong thư mục")
}

func validateComposeFilesInDir(wslPath string, files []string) error {
	norm := normalizeComposeWslPath(wslPath)
	if norm == "" {
		return fmt.Errorf("đường dẫn trống")
	}
	st, err := os.Stat(norm)
	if err != nil {
		if os.IsNotExist(err) {
			return fmt.Errorf("thư mục không tồn tại: %s", norm)
		}
		return fmt.Errorf("không truy cập được thư mục: %v", err)
	}
	if !st.IsDir() {
		return fmt.Errorf("không phải thư mục: %s", norm)
	}
	if len(files) == 0 {
		return fmt.Errorf("danh sách file compose rỗng")
	}
	if len(files) > 16 {
		return fmt.Errorf("tối đa 16 file -f")
	}
	for _, rel := range files {
		rel = strings.TrimSpace(rel)
		if rel == "" {
			return fmt.Errorf("tên file compose rỗng")
		}
		if filepath.IsAbs(rel) {
			return fmt.Errorf("chỉ dùng đường dẫn tương đối trong thư mục project (không dùng đường dẫn tuyệt đối): %s", rel)
		}
		clean := filepath.Clean(rel)
		fp := filepath.Join(norm, clean)
		rel2, err := filepath.Rel(norm, fp)
		if err != nil || strings.HasPrefix(rel2, "..") {
			return fmt.Errorf("file không nằm trong thư mục project: %s", rel)
		}
		st2, err := os.Stat(fp)
		if err != nil {
			if os.IsNotExist(err) {
				return fmt.Errorf("không thấy file: %s", rel)
			}
			return fmt.Errorf("không đọc được file %s: %v", rel, err)
		}
		if st2.IsDir() {
			return fmt.Errorf("không phải file: %s", rel)
		}
	}
	return nil
}

func validateComposeProjectDirOrFiles(wslPath string, composeFiles []string) error {
	if len(composeFiles) == 0 {
		return validateDefaultComposeFileInDir(wslPath)
	}
	return validateComposeFilesInDir(wslPath, composeFiles)
}

func hasDuplicateComposePath(items []Project, wslPath string) bool {
	n := normalizeComposeWslPath(wslPath)
	for _, it := range items {
		if normalizeComposeWslPath(it.WslPath) == n {
			return true
		}
	}
	return false
}

const storeDir = ".docklite"
const storeFile = "compose_projects.json"

// Project là một mục trong file JSON lưu trên đĩa.
type Project struct {
	ID           string   `json:"id"`
	WslPath      string   `json:"wslPath"`
	Name         string   `json:"name"`
	ComposeFiles []string `json:"composeFiles,omitempty"`
}

type projectsPayload struct {
	Items []Project `json:"items"`
}

type addBody struct {
	WindowsPath  string   `json:"windowsPath"`
	WslPath      string   `json:"wslPath"`
	ComposeFiles []string `json:"composeFiles,omitempty"`
}

type patchBody struct {
	ComposeFiles []string `json:"composeFiles"`
}

type idBody struct {
	ID string `json:"id"`
}

func storePath() (string, error) {
	home, err := os.UserHomeDir()
	if err != nil {
		return "", err
	}
	dir := filepath.Join(home, storeDir)
	if err := os.MkdirAll(dir, 0o755); err != nil {
		return "", err
	}
	return filepath.Join(dir, storeFile), nil
}

// loadProjectsFromDisk đọc file JSON; chỉ gọi khi cache chưa có (trong mutex ghi).
func loadProjectsFromDisk() ([]Project, error) {
	path, err := storePath()
	if err != nil {
		return nil, err
	}
	data, err := os.ReadFile(path)
	if err != nil {
		if os.IsNotExist(err) {
			return []Project{}, nil
		}
		return nil, err
	}
	var p projectsPayload
	if err := json.Unmarshal(data, &p); err != nil {
		return nil, err
	}
	return p.Items, nil
}

// loadProjects trả về bản sao danh sách project; dùng cache RAM sau lần đọc đĩa đầu tiên hoặc sau save.
func loadProjects() ([]Project, error) {
	projectCacheMu.RLock()
	if projectCacheLoaded {
		out := cloneProjects(projectCache)
		projectCacheMu.RUnlock()
		return out, nil
	}
	projectCacheMu.RUnlock()

	projectCacheMu.Lock()
	defer projectCacheMu.Unlock()
	if projectCacheLoaded {
		return cloneProjects(projectCache), nil
	}
	items, err := loadProjectsFromDisk()
	if err != nil {
		return nil, err
	}
	projectCache = cloneProjects(items)
	projectCacheLoaded = true
	return cloneProjects(projectCache), nil
}

func saveProjects(items []Project) error {
	path, err := storePath()
	if err != nil {
		return err
	}
	p := projectsPayload{Items: items}
	data, err := json.MarshalIndent(p, "", "  ")
	if err != nil {
		return err
	}
	if err := os.WriteFile(path, data, 0o644); err != nil {
		return err
	}
	projectCacheMu.Lock()
	projectCache = cloneProjects(items)
	projectCacheLoaded = true
	projectCacheMu.Unlock()
	return nil
}

// uncWslNetworkToUnixPath chuyển UNC \\wsl.localhost\<distro>\... hoặc \\wsl$\<distro>\... (đã đổi \ thành /)
// sang đường tuyệt đối trong filesystem Linux của distro (ví dụ /home/user/proj).
func uncWslNetworkToUnixPath(p string) (string, bool) {
	parts := strings.Split(p, "/")
	var segs []string
	for _, s := range parts {
		if s != "" {
			segs = append(segs, s)
		}
	}
	if len(segs) < 3 {
		return "", false
	}
	first := strings.ToLower(segs[0])
	if first == "wsl.localhost" || first == "wsl$" {
		return "/" + strings.Join(segs[2:], "/"), true
	}
	return "", false
}

// ToWslPath chuyển đường dẫn Windows hoặc WSL sang dạng WSL.
func ToWslPath(p string) (string, error) {
	p = strings.TrimSpace(p)
	p = strings.TrimPrefix(p, "\ufeff")
	if p == "" {
		return "", fmt.Errorf("đường dẫn trống")
	}
	p = strings.ReplaceAll(p, "\\", "/")
	if u, ok := uncWslNetworkToUnixPath(p); ok {
		return u, nil
	}
	if strings.HasPrefix(p, "/mnt/") {
		return p, nil
	}
	if len(p) >= 2 && p[1] == ':' {
		drive := strings.ToLower(string(p[0]))
		rest := strings.TrimPrefix(p[2:], "/")
		return "/mnt/" + drive + "/" + rest, nil
	}
	if strings.HasPrefix(p, "/") {
		return p, nil
	}
	return "", fmt.Errorf("không chuyển được đường dẫn Windows sang WSL")
}

func randomID() string {
	b := make([]byte, 8)
	_, _ = rand.Read(b)
	return hex.EncodeToString(b)
}

// Register gắn các route /api/compose/* vào mux.
func Register(mux *http.ServeMux) {
	mux.HandleFunc("/api/compose/projects", projectsCollection)
	mux.HandleFunc("/api/compose/projects/", projectItem)
	mux.HandleFunc("/api/compose/up", composeUp)
	mux.HandleFunc("/api/compose/down", composeDown)
	mux.HandleFunc("/api/compose/ps", composePs)
	mux.HandleFunc("/api/compose/config/services", composeConfigServices)
	mux.HandleFunc("/api/compose/service/start", composeServiceStart)
	mux.HandleFunc("/api/compose/service/stop", composeServiceStop)
	mux.HandleFunc("/api/compose/service/logs", composeServiceLogs)
	mux.HandleFunc("/api/compose/service/exec", composeServiceExec)
}

func projectsCollection(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path != "/api/compose/projects" {
		http.NotFound(w, r)
		return
	}
	switch r.Method {
	case http.MethodGet:
		items, err := loadProjects()
		if err != nil {
			apiresponse.WriteError(w, apiresponse.CodeInternal, err.Error(), http.StatusInternalServerError)
			return
		}
		apiresponse.WriteSuccess(w, map[string]interface{}{"items": items}, http.StatusOK)
	case http.MethodPost:
		var body addBody
		if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
			http.Error(w, "bad json", http.StatusBadRequest)
			return
		}
		var wslPath string
		var err error
		if strings.TrimSpace(body.WslPath) != "" {
			wslPath = strings.TrimSpace(body.WslPath)
		} else {
			wslPath, err = ToWslPath(body.WindowsPath)
			if err != nil {
				apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
				return
			}
		}
		name := filepath.Base(strings.TrimRight(wslPath, "/"))
		if name == "" || name == "." {
			name = "project"
		}
		wslPath = normalizeComposeWslPath(wslPath)
		var composeFiles []string
		for _, f := range body.ComposeFiles {
			f = strings.TrimSpace(f)
			if f != "" {
				composeFiles = append(composeFiles, f)
			}
		}
		if err := validateComposeProjectDirOrFiles(wslPath, composeFiles); err != nil {
			apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
			return
		}
		items, err := loadProjects()
		if err != nil {
			apiresponse.WriteError(w, apiresponse.CodeInternal, err.Error(), http.StatusInternalServerError)
			return
		}
		if hasDuplicateComposePath(items, wslPath) {
			apiresponse.WriteError(w, apiresponse.CodeConflict, "project với đường dẫn này đã tồn tại", http.StatusConflict)
			return
		}
		proj := Project{
			ID:           randomID(),
			WslPath:      wslPath,
			Name:         name,
			ComposeFiles: composeFiles,
		}
		items = append(items, proj)
		if err := saveProjects(items); err != nil {
			apiresponse.WriteError(w, apiresponse.CodeInternal, err.Error(), http.StatusInternalServerError)
			return
		}
		apiresponse.WriteSuccess(w, map[string]interface{}{"project": proj}, http.StatusOK)
	default:
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
	}
}

func projectItem(w http.ResponseWriter, r *http.Request) {
	id := strings.TrimPrefix(r.URL.Path, "/api/compose/projects/")
	id = strings.TrimSpace(id)
	if id == "" {
		http.NotFound(w, r)
		return
	}
	switch r.Method {
	case http.MethodDelete:
		deleteComposeProject(w, id)
	case http.MethodPatch:
		patchComposeProject(w, r, id)
	default:
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
	}
}

func deleteComposeProject(w http.ResponseWriter, id string) {
	items, err := loadProjects()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeInternal, err.Error(), http.StatusInternalServerError)
		return
	}
	var out []Project
	found := false
	for _, it := range items {
		if it.ID == id {
			found = true
			continue
		}
		out = append(out, it)
	}
	if !found {
		apiresponse.WriteError(w, apiresponse.CodeNotFound, "không tìm thấy project", http.StatusNotFound)
		return
	}
	if err := saveProjects(out); err != nil {
		apiresponse.WriteError(w, apiresponse.CodeInternal, err.Error(), http.StatusInternalServerError)
		return
	}
	apiresponse.WriteSuccess(w, struct{}{}, http.StatusOK)
}

func patchComposeProject(w http.ResponseWriter, r *http.Request, id string) {
	var body patchBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	var composeFiles []string
	for _, f := range body.ComposeFiles {
		f = strings.TrimSpace(f)
		if f != "" {
			composeFiles = append(composeFiles, f)
		}
	}
	items, err := loadProjects()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeInternal, err.Error(), http.StatusInternalServerError)
		return
	}
	for i := range items {
		if items[i].ID != id {
			continue
		}
		wslPath := items[i].WslPath
		if err := validateComposeProjectDirOrFiles(wslPath, composeFiles); err != nil {
			apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
			return
		}
		items[i].ComposeFiles = composeFiles
		if err := saveProjects(items); err != nil {
			apiresponse.WriteError(w, apiresponse.CodeInternal, err.Error(), http.StatusInternalServerError)
			return
		}
		apiresponse.WriteSuccess(w, map[string]interface{}{"project": items[i]}, http.StatusOK)
		return
	}
	apiresponse.WriteError(w, apiresponse.CodeNotFound, "không tìm thấy project", http.StatusNotFound)
}

func composeUp(w http.ResponseWriter, r *http.Request) {
	runComposeCommand(w, r, "up", "-d")
}

func composeDown(w http.ResponseWriter, r *http.Request) {
	runComposeCommand(w, r, "down")
}

func composePs(w http.ResponseWriter, r *http.Request) {
	runComposeCommand(w, r, "ps")
}

func runComposeCommand(w http.ResponseWriter, r *http.Request, rest ...string) {
	if r.Method != http.MethodPost {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	var body idBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	id := strings.TrimSpace(body.ID)
	if id == "" {
		apiresponse.WriteError(w, apiresponse.CodeValidation, "thiếu id", http.StatusBadRequest)
		return
	}
	items, err := loadProjects()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeInternal, err.Error(), http.StatusInternalServerError)
		return
	}
	var proj *Project
	for i := range items {
		if items[i].ID == id {
			proj = &items[i]
			break
		}
	}
	if proj == nil {
		apiresponse.WriteError(w, apiresponse.CodeNotFound, "không tìm thấy project", http.StatusNotFound)
		return
	}
	ctx := r.Context()
	fullArgs := dockerComposeArgs(*proj, rest...)
	cmd := exec.CommandContext(ctx, "docker", fullArgs...)
	cmd.Dir = proj.WslPath
	out, err := cmd.CombinedOutput()
	output := string(out)
	if err != nil {
		msg := strings.TrimSpace(output)
		if msg == "" {
			msg = err.Error()
		}
		apiresponse.WriteErrorWithDetails(w, apiresponse.CodeComposeCommand, msg, output, http.StatusInternalServerError)
		return
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"output": output}, http.StatusOK)
}
