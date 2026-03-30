package main

import (
	"bytes"
	"encoding/json"
	"net/http"
	"os/exec"
	"strconv"
	"strings"
)

type dockerPsRow struct {
	ID         string `json:"ID"`
	Names      string `json:"Names"`
	Image      string `json:"Image"`
	Command    string `json:"Command"`
	CreatedAt  string `json:"CreatedAt"`
	RunningFor string `json:"RunningFor"`
	Status     string `json:"Status"`
	Ports      string `json:"Ports"`
}

func containersCollectionHandler(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path != "/api/containers" {
		http.NotFound(w, r)
		return
	}
	if r.Method == http.MethodGet {
		listContainersHandler(w, r)
		return
	}
	http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
}

func listContainersHandler(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	cmd := exec.CommandContext(ctx, "docker", "ps", "-a", "--no-trunc", "--format", "{{json .}}")
	var stdout, stderr bytes.Buffer
	cmd.Stdout = &stdout
	cmd.Stderr = &stderr
	err := cmd.Run()
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	if err != nil {
		msg := strings.TrimSpace(stderr.String())
		if msg == "" {
			msg = err.Error()
		}
		_ = json.NewEncoder(w).Encode(map[string]interface{}{
			"items": []interface{}{},
			"error": msg,
		})
		return
	}

	text := strings.TrimSpace(stdout.String())
	var items []map[string]interface{}
	if text != "" {
		for _, line := range strings.Split(text, "\n") {
			line = strings.TrimSpace(line)
			if line == "" {
				continue
			}
			var row dockerPsRow
			if uerr := json.Unmarshal([]byte(line), &row); uerr != nil {
				continue
			}
			fullID := strings.TrimPrefix(row.ID, "sha256:")
			shortID := fullID
			if len(shortID) > 12 {
				shortID = shortID[:12]
			}
			name := strings.TrimPrefix(row.Names, "/")
			if idx := strings.IndexByte(name, ','); idx >= 0 {
				name = name[:idx]
			}
			created := row.CreatedAt
			if created == "" {
				created = row.RunningFor
			}
			items = append(items, map[string]interface{}{
				"id":        row.ID,
				"shortId":   shortID,
				"name":      name,
				"image":     row.Image,
				"status":    row.Status,
				"ports":     row.Ports,
				"command":   row.Command,
				"createdAt": created,
			})
		}
	}
	_ = json.NewEncoder(w).Encode(map[string]interface{}{"items": items})
}

func containersItemHandler(w http.ResponseWriter, r *http.Request) {
	rest := strings.TrimPrefix(r.URL.Path, "/api/containers/")
	if rest == "" {
		http.NotFound(w, r)
		return
	}
	parts := strings.SplitN(rest, "/", 2)
	id := strings.TrimSpace(parts[0])
	if id == "" {
		http.NotFound(w, r)
		return
	}
	if len(parts) == 1 {
		if r.Method == http.MethodDelete {
			removeContainerHandler(w, r, id)
			return
		}
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	action := strings.TrimSpace(parts[1])
	switch action {
	case "start":
		if r.Method != http.MethodPost {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}
		runDockerAction(w, r, "start", id)
	case "stop":
		if r.Method != http.MethodPost {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}
		runDockerAction(w, r, "stop", id)
	case "restart":
		if r.Method != http.MethodPost {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}
		runDockerAction(w, r, "restart", id)
	case "logs":
		if r.Method != http.MethodGet {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}
		containerLogsHandler(w, r, id)
	default:
		http.NotFound(w, r)
	}
}

func removeContainerHandler(w http.ResponseWriter, r *http.Request, id string) {
	force := r.URL.Query().Get("force") == "true"
	args := []string{"rm"}
	if force {
		args = append(args, "-f")
	}
	args = append(args, id)
	ctx := r.Context()
	cmd := exec.CommandContext(ctx, "docker", args...)
	var stderr bytes.Buffer
	cmd.Stderr = &stderr
	err := cmd.Run()
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	if err != nil {
		msg := strings.TrimSpace(stderr.String())
		if msg == "" {
			msg = err.Error()
		}
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": msg})
		return
	}
	_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": true})
}

func runDockerAction(w http.ResponseWriter, r *http.Request, action, id string) {
	ctx := r.Context()
	cmd := exec.CommandContext(ctx, "docker", action, id)
	var stderr bytes.Buffer
	cmd.Stderr = &stderr
	err := cmd.Run()
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	if err != nil {
		msg := strings.TrimSpace(stderr.String())
		if msg == "" {
			msg = err.Error()
		}
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": msg})
		return
	}
	_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": true})
}

func containerLogsHandler(w http.ResponseWriter, r *http.Request, id string) {
	tail := 200
	if v := r.URL.Query().Get("tail"); v != "" {
		if n, err := strconv.Atoi(v); err == nil && n > 0 && n <= 10000 {
			tail = n
		}
	}
	ctx := r.Context()
	cmd := exec.CommandContext(ctx, "docker", "logs", "--tail", strconv.Itoa(tail), id)
	var stdout, stderr bytes.Buffer
	cmd.Stdout = &stdout
	cmd.Stderr = &stderr
	err := cmd.Run()
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	if err != nil {
		msg := strings.TrimSpace(stderr.String())
		if msg == "" {
			msg = err.Error()
		}
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"content": "", "error": msg})
		return
	}
	out := stdout.String()
	if se := stderr.String(); se != "" {
		out += se
	}
	_ = json.NewEncoder(w).Encode(map[string]interface{}{"content": out})
}
