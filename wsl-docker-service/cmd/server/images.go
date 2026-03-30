package main

import (
	"bytes"
	"encoding/json"
	"net/http"
	"os/exec"
	"strings"
)

type dockerImageRow struct {
	ID         string `json:"ID"`
	Repository string `json:"Repository"`
	Tag        string `json:"Tag"`
	Size       string `json:"Size"`
	CreatedAt  string `json:"CreatedAt"`
}

type imageSummaryJSON struct {
	ID         string `json:"id"`
	Repository string `json:"repository"`
	Tag        string `json:"tag"`
	Size       string `json:"size"`
	CreatedAt  string `json:"createdAt,omitempty"`
}

func imagesRootHandler(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path != "/api/images" {
		http.NotFound(w, r)
		return
	}
	if r.Method != http.MethodGet {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	listImagesHandler(w, r)
}

func listImagesHandler(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	cmd := exec.CommandContext(ctx, "docker", "images", "--format", "{{json .}}")
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
	var items []imageSummaryJSON
	if text != "" {
		for _, line := range strings.Split(text, "\n") {
			line = strings.TrimSpace(line)
			if line == "" {
				continue
			}
			var row dockerImageRow
			if uerr := json.Unmarshal([]byte(line), &row); uerr != nil {
				continue
			}
			items = append(items, imageSummaryJSON{
				ID:         row.ID,
				Repository: row.Repository,
				Tag:        row.Tag,
				Size:       row.Size,
				CreatedAt:  row.CreatedAt,
			})
		}
	}
	_ = json.NewEncoder(w).Encode(map[string]interface{}{"items": items})
}

type imageRemoveBody struct {
	ID string `json:"id"`
}

func imagesRemoveHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost || r.URL.Path != "/api/images/remove" {
		http.NotFound(w, r)
		return
	}
	var body imageRemoveBody
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
	ctx := r.Context()
	cmd := exec.CommandContext(ctx, "docker", "rmi", id)
	var stderr bytes.Buffer
	cmd.Stderr = &stderr
	err := cmd.Run()
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	if err != nil {
		msg := strings.TrimSpace(stderr.String())
		if msg == "" {
			msg = err.Error()
		}
		w.WriteHeader(http.StatusBadRequest)
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": msg})
		return
	}
	_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": true})
}

type imagePruneBody struct {
	AllUnused bool `json:"allUnused"`
}

func imagesPruneHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost || r.URL.Path != "/api/images/prune" {
		http.NotFound(w, r)
		return
	}
	var body imagePruneBody
	_ = json.NewDecoder(r.Body).Decode(&body)
	args := []string{"image", "prune", "-f"}
	if body.AllUnused {
		args = append(args, "-a")
	}
	ctx := r.Context()
	cmd := exec.CommandContext(ctx, "docker", args...)
	out, err := cmd.CombinedOutput()
	output := string(out)
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
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
