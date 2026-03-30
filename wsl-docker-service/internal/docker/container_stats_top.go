package docker

import (
	"encoding/json"
	"io"
	"net/http"
	"sort"
	"strconv"
	"strings"
	"sync"

	"github.com/docker/docker/api/types"
	"github.com/docker/docker/api/types/container"

	"docklite-wsl/internal/apiresponse"
	"docklite-wsl/internal/dockerengine"
)

type topStatsWire struct {
	c    types.Container
	mem  uint64
	cpu  float64
	ok   bool
}

type topSortMode int

const (
	topSortByMemory topSortMode = iota
	topSortByCPU
)

// TopContainersByMemory trả GET /api/containers/top-by-memory?limit=5 — container đang chạy, sắp theo RAM dùng (snapshot).
func TopContainersByMemory(w http.ResponseWriter, r *http.Request) {
	topContainersWithSort(w, r, topSortByMemory)
}

// TopContainersByCPU trả GET /api/containers/top-by-cpu?limit=5 — container đang chạy, sắp theo CPU % (snapshot).
func TopContainersByCPU(w http.ResponseWriter, r *http.Request) {
	topContainersWithSort(w, r, topSortByCPU)
}

func topContainersWithSort(w http.ResponseWriter, r *http.Request, mode topSortMode) {
	if r.Method != http.MethodGet {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	limit := 5
	if q := r.URL.Query().Get("limit"); q != "" {
		if n, err := strconv.Atoi(q); err == nil && n > 0 && n <= 20 {
			limit = n
		}
	}
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
	var running []types.Container
	for _, c := range list {
		if c.State == "running" {
			running = append(running, c)
		}
	}
	if len(running) == 0 {
		apiresponse.WriteSuccess(w, map[string]interface{}{"items": []interface{}{}}, http.StatusOK)
		return
	}
	results := make([]topStatsWire, len(running))
	var wg sync.WaitGroup
	for i := range running {
		wg.Add(1)
		go func(idx int) {
			defer wg.Done()
			st, err := dc.ContainerStatsOneShot(ctx, running[idx].ID)
			if err != nil {
				return
			}
			defer st.Body.Close()
			body, err := io.ReadAll(st.Body)
			if err != nil {
				return
			}
			var wire statsWire
			if err := json.Unmarshal(body, &wire); err != nil {
				return
			}
			results[idx].c = running[idx]
			results[idx].mem = wire.MemoryStats.Usage
			results[idx].cpu = computeCPUPercent(wire)
			results[idx].ok = true
		}(i)
	}
	wg.Wait()

	type row struct {
		ID               string  `json:"id"`
		ShortID          string  `json:"shortId"`
		Name             string  `json:"name"`
		Image            string  `json:"image"`
		MemoryUsageBytes uint64  `json:"memoryUsageBytes"`
		CPUUsagePercent  float64 `json:"cpuUsagePercent"`
	}
	rows := make([]row, 0, len(results))
	for _, rw := range results {
		if !rw.ok {
			continue
		}
		c := rw.c
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
		rows = append(rows, row{
			ID:               c.ID,
			ShortID:          shortID,
			Name:             name,
			Image:            c.Image,
			MemoryUsageBytes: rw.mem,
			CPUUsagePercent:  rw.cpu,
		})
	}
	sort.Slice(rows, func(i, j int) bool {
		if mode == topSortByMemory {
			return rows[i].MemoryUsageBytes > rows[j].MemoryUsageBytes
		}
		return rows[i].CPUUsagePercent > rows[j].CPUUsagePercent
	})
	if len(rows) > limit {
		rows = rows[:limit]
	}
	out := make([]map[string]interface{}, len(rows))
	for i := range rows {
		r := rows[i]
		out[i] = map[string]interface{}{
			"id":               r.ID,
			"shortId":          r.ShortID,
			"name":             r.Name,
			"image":            r.Image,
			"memoryUsageBytes": r.MemoryUsageBytes,
			"cpuUsagePercent":  r.CPUUsagePercent,
		}
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"items": out}, http.StatusOK)
}
