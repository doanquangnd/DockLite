// Package hostresources đọc snapshot tài nguyên Linux phía tiến trình service (WSL distro).
package hostresources

import (
	"bufio"
	"fmt"
	"math"
	"net/http"
	"os"
	"runtime"
	"strconv"
	"strings"
	"syscall"
	"time"

	"docklite-wsl/internal/apiresponse"
)

// Snapshot là dữ liệu trả về GET /api/wsl/host-resources (trong envelope data).
type Snapshot struct {
	MemoryTotalKb          uint64  `json:"memoryTotalKb"`
	MemoryAvailableKb      uint64  `json:"memoryAvailableKb"`
	MemoryUsedPercent      float64 `json:"memoryUsedPercent"`
	LoadAvg1               float64 `json:"loadAvg1"`
	LoadAvg5               float64 `json:"loadAvg5"`
	LoadAvg15              float64 `json:"loadAvg15"`
	RootMountPath          string  `json:"rootMountPath"`
	DiskRootTotalBytes     uint64  `json:"diskRootTotalBytes"`
	DiskRootAvailableBytes uint64  `json:"diskRootAvailableBytes"`
	DiskRootUsedPercent    float64 `json:"diskRootUsedPercent"`
	Hostname               string  `json:"hostname,omitempty"`
	CpuCoresOnline         int     `json:"cpuCoresOnline"`
	CollectedAtUtcIso      string  `json:"collectedAtUtcIso"`
}

// HTTPHandler GET /api/wsl/host-resources — không gọi Docker; chỉ đọc kernel và filesystem cục bộ.
func HTTPHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	snap, err := collect()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeInternal, err.Error(), http.StatusInternalServerError)
		return
	}
	apiresponse.WriteSuccess(w, snap, http.StatusOK)
}

func collect() (*Snapshot, error) {
	totalKb, availKb, err := readMeminfoKB()
	if err != nil {
		return nil, err
	}
	if availKb > totalKb {
		availKb = totalKb
	}
	var memUsedPct float64
	if totalKb > 0 {
		memUsedPct = 100.0 * float64(totalKb-availKb) / float64(totalKb)
		memUsedPct = math.Min(100, math.Max(0, memUsedPct))
	}

	l1, l5, l15, err := readLoadAvg()
	if err != nil {
		return nil, err
	}

	root := "/"
	totalB, availB, usedPct, err := statDisk(root)
	if err != nil {
		return nil, err
	}

	host, _ := os.Hostname()
	host = strings.TrimSpace(host)

	return &Snapshot{
		MemoryTotalKb:          totalKb,
		MemoryAvailableKb:      availKb,
		MemoryUsedPercent:      memUsedPct,
		LoadAvg1:               l1,
		LoadAvg5:               l5,
		LoadAvg15:              l15,
		RootMountPath:          root,
		DiskRootTotalBytes:     totalB,
		DiskRootAvailableBytes: availB,
		DiskRootUsedPercent:    usedPct,
		Hostname:               host,
		CpuCoresOnline:         runtime.NumCPU(),
		CollectedAtUtcIso:      time.Now().UTC().Format(time.RFC3339Nano),
	}, nil
}

func readMeminfoKB() (totalKb, availKb uint64, err error) {
	f, err := os.Open("/proc/meminfo")
	if err != nil {
		return 0, 0, err
	}
	defer f.Close()

	var memFree uint64
	sc := bufio.NewScanner(f)
	for sc.Scan() {
		line := sc.Text()
		switch {
		case strings.HasPrefix(line, "MemTotal:"):
			totalKb, err = parseMeminfoValueKB(line)
			if err != nil {
				return 0, 0, fmt.Errorf("MemTotal: %w", err)
			}
		case strings.HasPrefix(line, "MemAvailable:"):
			availKb, err = parseMeminfoValueKB(line)
			if err != nil {
				return 0, 0, fmt.Errorf("MemAvailable: %w", err)
			}
		case strings.HasPrefix(line, "MemFree:"):
			memFree, _ = parseMeminfoValueKB(line)
		}
	}
	if err := sc.Err(); err != nil {
		return 0, 0, err
	}
	if totalKb == 0 {
		return 0, 0, fmt.Errorf("không đọc được MemTotal từ /proc/meminfo")
	}
	if availKb == 0 && memFree > 0 {
		availKb = memFree
	}
	return totalKb, availKb, nil
}

func parseMeminfoValueKB(line string) (uint64, error) {
	i := strings.IndexByte(line, ':')
	if i < 0 {
		return 0, fmt.Errorf("dòng meminfo không hợp lệ")
	}
	rest := strings.TrimSpace(line[i+1:])
	fields := strings.Fields(rest)
	if len(fields) < 1 {
		return 0, fmt.Errorf("thiếu giá trị kB")
	}
	return strconv.ParseUint(fields[0], 10, 64)
}

func readLoadAvg() (l1, l5, l15 float64, err error) {
	b, err := os.ReadFile("/proc/loadavg")
	if err != nil {
		return 0, 0, 0, err
	}
	fields := strings.Fields(string(b))
	if len(fields) < 3 {
		return 0, 0, 0, fmt.Errorf("/proc/loadavg không đủ trường")
	}
	l1, err = strconv.ParseFloat(fields[0], 64)
	if err != nil {
		return 0, 0, 0, err
	}
	l5, err = strconv.ParseFloat(fields[1], 64)
	if err != nil {
		return 0, 0, 0, err
	}
	l15, err = strconv.ParseFloat(fields[2], 64)
	if err != nil {
		return 0, 0, 0, err
	}
	return l1, l5, l15, nil
}

func statDisk(mountPath string) (totalBytes, availBytes uint64, usedPercent float64, err error) {
	var st syscall.Statfs_t
	if err := syscall.Statfs(mountPath, &st); err != nil {
		return 0, 0, 0, err
	}
	bs := uint64(st.Bsize)
	totalBytes = uint64(st.Blocks) * bs
	availBytes = uint64(st.Bavail) * bs
	if totalBytes > 0 && availBytes <= totalBytes {
		used := totalBytes - availBytes
		usedPercent = 100.0 * float64(used) / float64(totalBytes)
		usedPercent = math.Min(100, math.Max(0, usedPercent))
	}
	return totalBytes, availBytes, usedPercent, nil
}
