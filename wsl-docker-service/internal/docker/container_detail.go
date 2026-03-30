package docker

import (
	"encoding/json"
	"io"
	"net/http"
	"time"

	"github.com/docker/docker/api/types"

	"docklite-wsl/internal/apiresponse"
	"docklite-wsl/internal/dockerengine"
)

// containerInspect trả JSON inspect thô từ Engine (GET /api/containers/{id}/inspect).
func containerInspect(w http.ResponseWriter, r *http.Request, id string) {
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	_, raw, err := dc.ContainerInspectWithRaw(ctx, id, false)
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	var payload struct {
		Inspect json.RawMessage `json:"inspect"`
	}
	payload.Inspect = raw
	apiresponse.WriteSuccess(w, payload, http.StatusOK)
}

// statsWire khớp JSON một lần chụp từ GET /containers/{id}/stats (không stream).
type statsWire struct {
	Read        time.Time         `json:"read"`
	CPUStats    types.CPUStats    `json:"cpu_stats"`
	PreCPUStats types.CPUStats    `json:"precpu_stats"`
	MemoryStats types.MemoryStats `json:"memory_stats"`
	BlkioStats  types.BlkioStats  `json:"blkio_stats,omitempty"`
	Networks    map[string]struct {
		RxBytes uint64 `json:"rx_bytes"`
		TxBytes uint64 `json:"tx_bytes"`
	} `json:"networks,omitempty"`
}

type statsSnapshot struct {
	ReadAt           string  `json:"readAt"`
	CPUUsagePercent  float64 `json:"cpuUsagePercent"`
	MemoryUsageBytes uint64  `json:"memoryUsageBytes"`
	MemoryLimitBytes uint64  `json:"memoryLimitBytes"`
	NetworkRxBytes   uint64  `json:"networkRxBytes"`
	NetworkTxBytes   uint64  `json:"networkTxBytes"`
	BlockReadBytes   uint64  `json:"blockReadBytes"`
	BlockWriteBytes  uint64  `json:"blockWriteBytes"`
}

func computeCPUPercent(w statsWire) float64 {
	sysDelta := int64(w.CPUStats.SystemUsage) - int64(w.PreCPUStats.SystemUsage)
	cpuDelta := int64(w.CPUStats.CPUUsage.TotalUsage) - int64(w.PreCPUStats.CPUUsage.TotalUsage)
	if sysDelta <= 0 || cpuDelta < 0 {
		return 0
	}
	n := w.CPUStats.OnlineCPUs
	if n == 0 {
		n = uint32(len(w.CPUStats.CPUUsage.PercpuUsage))
	}
	if n == 0 {
		n = 1
	}
	return float64(cpuDelta) / float64(sysDelta) * float64(n) * 100.0
}

func sumNetworks(w statsWire) (rx, tx uint64) {
	for _, v := range w.Networks {
		rx += v.RxBytes
		tx += v.TxBytes
	}
	return rx, tx
}

func sumBlkioReadWrite(b types.BlkioStats) (read, write uint64) {
	for _, e := range b.IoServiceBytesRecursive {
		switch e.Op {
		case "Read":
			read += e.Value
		case "Write":
			write += e.Value
		}
	}
	return read, write
}

// containerStatsSnapshot trả một snapshot CPU/RAM/mạng (GET /api/containers/{id}/stats).
func containerStatsSnapshot(w http.ResponseWriter, r *http.Request, id string) {
	ctx := r.Context()
	dc, err := dockerengine.Client()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeDockerUnavailable, err.Error(), http.StatusServiceUnavailable)
		return
	}
	st, err := dc.ContainerStatsOneShot(ctx, id)
	if err != nil {
		dockerengine.WriteError(w, err)
		return
	}
	defer st.Body.Close()
	body, err := io.ReadAll(st.Body)
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeInternal, err.Error(), http.StatusInternalServerError)
		return
	}
	var wire statsWire
	if err := json.Unmarshal(body, &wire); err != nil {
		apiresponse.WriteError(w, apiresponse.CodeInternal, "json stats: "+err.Error(), http.StatusInternalServerError)
		return
	}
	rx, tx := sumNetworks(wire)
	br, bw := sumBlkioReadWrite(wire.BlkioStats)
	out := statsSnapshot{
		ReadAt:           wire.Read.UTC().Format(time.RFC3339),
		CPUUsagePercent:  computeCPUPercent(wire),
		MemoryUsageBytes: wire.MemoryStats.Usage,
		MemoryLimitBytes: wire.MemoryStats.Limit,
		NetworkRxBytes:   rx,
		NetworkTxBytes:   tx,
		BlockReadBytes:   br,
		BlockWriteBytes:  bw,
	}
	apiresponse.WriteSuccess(w, out, http.StatusOK)
}
