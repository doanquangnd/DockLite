package docker

import (
	"bytes"
	"fmt"
	"net/http"
	"strconv"
	"strings"
	"time"

	"github.com/docker/docker/api/types"
	"github.com/docker/docker/api/types/container"
	"github.com/docker/docker/pkg/stdcopy"

	"docklite-wsl/internal/apiresponse"
	"docklite-wsl/internal/dockerengine"
)

// ContainersCollection xử lý GET /api/containers.
func ContainersCollection(w http.ResponseWriter, r *http.Request) {
	if r.URL.Path != "/api/containers" {
		http.NotFound(w, r)
		return
	}
	if r.Method == http.MethodGet {
		listContainers(w, r)
		return
	}
	http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
}

func formatContainerPorts(ports []types.Port) string {
	if len(ports) == 0 {
		return ""
	}
	parts := make([]string, 0, len(ports))
	for _, p := range ports {
		if p.PublicPort == 0 {
			parts = append(parts, fmt.Sprintf("%d/%s", p.PrivatePort, p.Type))
			continue
		}
		ip := p.IP
		if ip == "" {
			ip = "0.0.0.0"
		}
		parts = append(parts, fmt.Sprintf("%s:%d->%d/%s", ip, p.PublicPort, p.PrivatePort, p.Type))
	}
	return strings.Join(parts, ", ")
}

func listContainers(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	list, err := dc.ContainerList(ctx, container.ListOptions{All: true})
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	items := make([]map[string]interface{}, 0, len(list))
	for _, c := range list {
		fullID := strings.TrimPrefix(c.ID, "sha256:")
		shortID := fullID
		if len(shortID) > 12 {
			shortID = shortID[:12]
		}
		name := ""
		if len(c.Names) > 0 {
			name = strings.TrimPrefix(c.Names[0], "/")
			if idx := strings.IndexByte(name, ','); idx >= 0 {
				name = name[:idx]
			}
		}
		created := time.Unix(c.Created, 0).UTC().Format(time.RFC3339)
		labels := map[string]string{}
		if c.Labels != nil {
			for k, v := range c.Labels {
				labels[k] = v
			}
		}
		items = append(items, map[string]interface{}{
			"id":        c.ID,
			"shortId":   shortID,
			"name":      name,
			"image":     c.Image,
			"status":    c.Status,
			"ports":     formatContainerPorts(c.Ports),
			"command":   c.Command,
			"createdAt": created,
			"labels":    labels,
		})
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"items": items}, http.StatusOK)
}

// ContainersItem xử lý /api/containers/{id} và /api/containers/{id}/...
func ContainersItem(w http.ResponseWriter, r *http.Request) {
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
			removeContainer(w, r, id)
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
		containerLogs(w, r, id)
	case "inspect":
		if r.Method != http.MethodGet {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}
		containerInspect(w, r, id)
	case "stats":
		if r.Method != http.MethodGet {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}
		containerStatsSnapshot(w, r, id)
	default:
		http.NotFound(w, r)
	}
}

func removeContainer(w http.ResponseWriter, r *http.Request, id string) {
	force := r.URL.Query().Get("force") == "true"
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	err = dc.ContainerRemove(ctx, id, container.RemoveOptions{Force: force})
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	apiresponse.WriteSuccess(w, struct{}{}, http.StatusOK)
}

func runDockerAction(w http.ResponseWriter, r *http.Request, action, id string) {
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	switch action {
	case "start":
		err = dc.ContainerStart(ctx, id, container.StartOptions{})
	case "stop":
		err = dc.ContainerStop(ctx, id, container.StopOptions{})
	case "restart":
		err = dc.ContainerRestart(ctx, id, container.StopOptions{})
	default:
		apiresponse.WriteError(w, apiresponse.CodeInternal, "action không hợp lệ", http.StatusInternalServerError)
		return
	}
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	apiresponse.WriteSuccess(w, struct{}{}, http.StatusOK)
}

func containerLogs(w http.ResponseWriter, r *http.Request, id string) {
	tail := 200
	if v := r.URL.Query().Get("tail"); v != "" {
		if n, err := strconv.Atoi(v); err == nil && n > 0 && n <= 10000 {
			tail = n
		}
	}
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	reader, err := dc.ContainerLogs(ctx, id, container.LogsOptions{
		ShowStdout: true,
		ShowStderr: true,
		Tail:       strconv.Itoa(tail),
	})
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	defer reader.Close()

	var buf bytes.Buffer
	_, err = stdcopy.StdCopy(&buf, &buf, reader)
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeInternal, err.Error(), http.StatusInternalServerError)
		return
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"content": buf.String()}, http.StatusOK)
}
