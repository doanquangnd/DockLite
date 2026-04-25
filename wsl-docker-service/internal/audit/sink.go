package audit

import (
	"encoding/json"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"
)

// ghi tệp audit xoay 10MB, xóa tệp cũ hơn 14 ngày (một lần khi mở ghi), không cần phụ thuộc ngoài.
var (
	sinkOnce sync.Once
	sinkErr  error
	wrMu     sync.Mutex
	wr       *os.File
	wrPath   string
	wrSize   int64
)

const maxAuditFile = 10 << 20
const oldAuditPurge = 14 * 24 * time.Hour

// Init tạo ~/.docklite/logs/ và tệp audit.log; lỗi thư mục không chặn stdout audit.
func Init() error {
	sinkOnce.Do(func() {
		home, err := os.UserHomeDir()
		if err != nil {
			sinkErr = err
			return
		}
		logDir := filepath.Join(home, ".docklite", "logs")
		if err = os.MkdirAll(logDir, 0o750); err != nil {
			sr := err
			sinkErr = sr
			return
		}
		p := filepath.Join(logDir, "audit.log")
		f, err := os.OpenFile(p, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0o600)
		if err != nil {
			sr := err
			sinkErr = sr
			return
		}
		st, _ := f.Stat()
		sz := st.Size()
		if sz >= maxAuditFile {
			_ = f.Close()
			rot := p + "." + time.Now().UTC().Format("20060102-150405")
			_ = os.Rename(p, rot)
			f, err = os.OpenFile(p, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0o600)
			if err != nil {
				sinkErr = err
				return
			}
			sz = 0
		}
		wr, wrPath, wrSize = f, p, sz
		_ = purgeOldAuditFiles(logDir)
	})
	return sinkErr
}

func purgeOldAuditFiles(logDir string) error {
	entries, err := os.ReadDir(logDir)
	if err != nil {
		return err
	}
	now := time.Now()
	for _, e := range entries {
		if e.IsDir() {
			continue
		}
		n := e.Name()
		if !strings.HasPrefix(n, "audit") {
			continue
		}
		info, err := e.Info()
		if err != nil {
			continue
		}
		if now.Sub(info.ModTime()) > oldAuditPurge {
			_ = os.Remove(filepath.Join(logDir, n))
		}
	}
	return nil
}

// WriteJSON ghi stdout luôn; tệp nếu Init thành công (xoay 10MB).
func WriteJSON(r Record) error {
	r.Ts = time.Now().UTC().Format(time.RFC3339Nano)
	b, err := json.Marshal(r)
	if err != nil {
		return err
	}
	b = append(b, '\n')
	if _, werr := os.Stdout.Write(b); werr != nil {
		return werr
	}
	if wr == nil {
		return nil
	}
	wrMu.Lock()
	defer wrMu.Unlock()
	if int64(len(b))+wrSize > maxAuditFile {
		_ = wr.Close()
		rotated := wrPath + "." + time.Now().UTC().Format("150405")
		_ = os.Rename(wrPath, rotated)
		f, err := os.OpenFile(wrPath, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0o600)
		if err != nil {
			return err
		}
		wr, wrSize = f, 0
	}
	n, err := wr.Write(b)
	if err == nil {
		wrSize += int64(n)
	}
	return err
}
