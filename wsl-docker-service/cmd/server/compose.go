package main

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
)

const composeStoreDir = ".docklite"
const composeStoreFile = "compose_projects.json"

type composeProject struct {
	ID      string `json:"id"`
	WslPath string `json:"wslPath"`
	Name    string `json:"name"`
}

type composeProjectsPayload struct {
	Items []composeProject `json:"items"`
}

type composeAddBody struct {
	WindowsPath string `json:"windowsPath"`
	WslPath     string `json:"wslPath"`
}

type composeIdBody struct {
	ID string `json:"id"`
}

func composeStorePath() (string, error) {
	home, err := os.UserHomeDir()
	if err != nil {
		return "", err
	}
	dir := filepath.Join(home, composeStoreDir)
	if err := os.MkdirAll(dir, 0o755); err != nil {
		return "", err
	}
	return filepath.Join(dir, composeStoreFile), nil
}

func loadComposeProjects() ([]composeProject, error) {
	path, err := composeStorePath()
	if err != nil {
		return nil, err
	}
	data, err := os.ReadFile(path)
	if err != nil {
		if os.IsNotExist(err) {
			return []composeProject{}, nil
		}
		return nil, err
	}
	var p composeProjectsPayload
	if err := json.Unmarshal(data, &p); err != nil {
		return nil, err
	}
	return p.Items, nil
}

func saveComposeProjects(items []composeProject) error {
	path, err := composeStorePath()
	if err != nil {
		return err
	}
	p := composeProjectsPayload{Items: items}
	data, err := json.MarshalIndent(p, "", "  ")
	if err != nil {
		return err
	}
	return os.WriteFile(path, data, 0o644)
}

func toWslPath(p string) (string, error) {
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

func randomComposeID() string {
	b := make([]byte, 8)
	_, _ = rand.Read(b)
	return hex.EncodeToString(b)
}

func composeProjectsCollectionHandler(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path != "/api/compose/projects" {
		http.NotFound(w, r)
		return
	}
	switch r.Method {
	case http.MethodGet:
		items, err := loadComposeProjects()
		w.Header().Set("Content-Type", "application/json; charset=utf-8")
		if err != nil {
			_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "items": []composeProject{}})
			return
		}
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": true, "items": items})
	case http.MethodPost:
		var body composeAddBody
		if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
			http.Error(w, "bad json", http.StatusBadRequest)
			return
		}
		var wslPath string
		var err error
		if strings.TrimSpace(body.WslPath) != "" {
			wslPath = strings.TrimSpace(body.WslPath)
		} else {
			wslPath, err = toWslPath(body.WindowsPath)
			if err != nil {
				w.Header().Set("Content-Type", "application/json; charset=utf-8")
				w.WriteHeader(http.StatusBadRequest)
				_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": err.Error()})
				return
			}
		}
		name := filepath.Base(strings.TrimRight(wslPath, "/"))
		if name == "" || name == "." {
			name = "project"
		}
		proj := composeProject{
			ID:      randomComposeID(),
			WslPath: wslPath,
			Name:    name,
		}
		items, err := loadComposeProjects()
		if err != nil {
			w.Header().Set("Content-Type", "application/json; charset=utf-8")
			w.WriteHeader(http.StatusInternalServerError)
			_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": err.Error()})
			return
		}
		items = append(items, proj)
		if err := saveComposeProjects(items); err != nil {
			w.Header().Set("Content-Type", "application/json; charset=utf-8")
			w.WriteHeader(http.StatusInternalServerError)
			_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": err.Error()})
			return
		}
		w.Header().Set("Content-Type", "application/json; charset=utf-8")
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": true, "project": proj})
	default:
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
	}
}

func composeProjectItemHandler(w http.ResponseWriter, r *http.Request) {
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
	items, err := loadComposeProjects()
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	if err != nil {
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": err.Error()})
		return
	}
	var out []composeProject
	found := false
	for _, it := range items {
		if it.ID == id {
			found = true
			continue
		}
		out = append(out, it)
	}
	if !found {
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": "không tìm thấy project"})
		return
	}
	if err := saveComposeProjects(out); err != nil {
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": err.Error()})
		return
	}
	_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": true})
}

func composeUpHandler(w http.ResponseWriter, r *http.Request) {
	runComposeCommand(w, r, []string{"compose", "up", "-d"})
}

func composeDownHandler(w http.ResponseWriter, r *http.Request) {
	runComposeCommand(w, r, []string{"compose", "down"})
}

func composePsHandler(w http.ResponseWriter, r *http.Request) {
	runComposeCommand(w, r, []string{"compose", "ps"})
}

func runComposeCommand(w http.ResponseWriter, r *http.Request, dockerArgs []string) {
	if r.Method != http.MethodPost {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	var body composeIdBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	id := strings.TrimSpace(body.ID)
	if id == "" {
		w.Header().Set("Content-Type", "application/json; charset=utf-8")
		w.WriteHeader(http.StatusBadRequest)
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": "thiếu id"})
		return
	}
	items, err := loadComposeProjects()
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	if err != nil {
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": err.Error()})
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
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": "không tìm thấy project"})
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
		_ = json.NewEncoder(w).Encode(map[string]interface{}{
			"ok":     false,
			"error":  msg,
			"output": output,
		})
		return
	}
	_ = json.NewEncoder(w).Encode(map[string]interface{}{
		"ok":     true,
		"output": output,
	})
}
