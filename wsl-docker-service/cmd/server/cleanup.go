package main

import (
	"encoding/json"
	"net/http"
	"os/exec"
	"strings"
)

type systemPruneBody struct {
	Kind        string `json:"kind"`
	WithVolumes bool   `json:"withVolumes"`
}

func systemPruneHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost || r.URL.Path != "/api/system/prune" {
		http.NotFound(w, r)
		return
	}
	var body systemPruneBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	kind := strings.TrimSpace(strings.ToLower(body.Kind))
	ctx := r.Context()
	var cmd *exec.Cmd
	switch kind {
	case "containers":
		cmd = exec.CommandContext(ctx, "docker", "container", "prune", "-f")
	case "images":
		cmd = exec.CommandContext(ctx, "docker", "image", "prune", "-f")
	case "volumes":
		cmd = exec.CommandContext(ctx, "docker", "volume", "prune", "-f")
	case "networks":
		cmd = exec.CommandContext(ctx, "docker", "network", "prune", "-f")
	case "system":
		args := []string{"system", "prune", "-f"}
		if body.WithVolumes {
			args = append(args, "--volumes")
		}
		cmd = exec.CommandContext(ctx, "docker", args...)
	default:
		w.Header().Set("Content-Type", "application/json; charset=utf-8")
		w.WriteHeader(http.StatusBadRequest)
		_ = json.NewEncoder(w).Encode(map[string]interface{}{
			"ok":    false,
			"error": "kind phải là: containers, images, volumes, networks, system",
		})
		return
	}
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
