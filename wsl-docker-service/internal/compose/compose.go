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

func validateComposeProjectDir(wslPath string) error {
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
	ID      string `json:"id"`
	WslPath string `json:"wslPath"`
	Name    string `json:"name"`
}

type projectsPayload struct {
	Items []Project `json:"items"`
}

type addBody struct {
	WindowsPath string `json:"windowsPath"`
	WslPath     string `json:"wslPath"`
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

func loadProjects() ([]Project, error) {
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
	return os.WriteFile(path, data, 0o644)
}

// ToWslPath chuyển đường dẫn Windows hoặc WSL sang dạng WSL.
func ToWslPath(p string) (string, error) {
	p = strings.TrimSpace(p)
	if p == "" {
		return "", fmt.Errorf("đường dẫn trống")
	}
	p = strings.ReplaceAll(p, "\\", "/")
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
		if err := validateComposeProjectDir(wslPath); err != nil {
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
			ID:      randomID(),
			WslPath: wslPath,
			Name:    name,
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
	if r.Method != http.MethodDelete {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	id := strings.TrimPrefix(r.URL.Path, "/api/compose/projects/")
	id = strings.TrimSpace(id)
	if id == "" {
		http.NotFound(w, r)
		return
	}
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

func composeUp(w http.ResponseWriter, r *http.Request) {
	runComposeCommand(w, r, []string{"compose", "up", "-d"})
}

func composeDown(w http.ResponseWriter, r *http.Request) {
	runComposeCommand(w, r, []string{"compose", "down"})
}

func composePs(w http.ResponseWriter, r *http.Request) {
	runComposeCommand(w, r, []string{"compose", "ps"})
}

func runComposeCommand(w http.ResponseWriter, r *http.Request, dockerArgs []string) {
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
	var dir string
	for _, it := range items {
		if it.ID == id {
			dir = it.WslPath
			break
		}
	}
	if dir == "" {
		apiresponse.WriteError(w, apiresponse.CodeNotFound, "không tìm thấy project", http.StatusNotFound)
		return
	}
	ctx := r.Context()
	cmd := exec.CommandContext(ctx, "docker", dockerArgs...)
	cmd.Dir = dir
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
