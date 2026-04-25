package docker

import (
	"encoding/json"
	"fmt"
	"net/http"
	"os/exec"
	"strings"

	"docklite-wsl/internal/apiresponse"
)

type trivyScanBody struct {
	ImageRef   string `json:"imageRef"`
	Format     string `json:"format,omitempty"`
	PolicyPath string `json:"policyPath,omitempty"`
}

func normalizeTrivyFormat(s string) string {
	s = strings.TrimSpace(strings.ToLower(s))
	if s == "json" {
		return "json"
	}
	return "table"
}

func validateTrivyPolicyPath(p string) error {
	p = strings.TrimSpace(p)
	if p == "" {
		return nil
	}
	if len(p) > 2048 {
		return fmt.Errorf("policyPath quá dài")
	}
	if strings.Contains(p, "..") {
		return fmt.Errorf("policyPath không được chứa ..")
	}
	if strings.ContainsAny(p, ";|&`$\n\r") {
		return fmt.Errorf("policyPath chứa ký tự không hợp lệ")
	}
	if !strings.HasPrefix(p, "/") {
		return fmt.Errorf("policyPath phải là đường dẫn tuyệt đối trong WSL (bắt đầu bằng /)")
	}
	return nil
}

func validateTrivyImageRef(ref string) error {
	ref = strings.TrimSpace(ref)
	if ref == "" {
		return fmt.Errorf("thiếu imageRef")
	}
	if strings.HasPrefix(ref, "-") {
		return fmt.Errorf("imageRef không được bắt đầu bằng '-'")
	}
	return nil
}

// ImageTrivyScan xử lý POST /api/images/trivy-scan — gọi `trivy image` nếu có trong PATH (tùy chọn, công cụ ngoài).
func ImageTrivyScan(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost || r.URL.Path != "/api/images/trivy-scan" {
		http.NotFound(w, r)
		return
	}
	var body trivyScanBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	ref := strings.TrimSpace(body.ImageRef)
	if err := validateTrivyImageRef(ref); err != nil {
		apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
		return
	}
	if len(ref) > 512 {
		apiresponse.WriteError(w, apiresponse.CodeValidation, "imageRef quá dài", http.StatusBadRequest)
		return
	}
	if err := validateTrivyPolicyPath(body.PolicyPath); err != nil {
		apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
		return
	}
	path, err := exec.LookPath("trivy")
	if err != nil || path == "" {
		apiresponse.WriteError(w, apiresponse.CodeValidation, "không tìm thấy trivy trong PATH trên WSL (cài đặt trivy để dùng quét CVE)", http.StatusServiceUnavailable)
		return
	}
	format := normalizeTrivyFormat(body.Format)
	ctx := r.Context()
	args := []string{"image", "--quiet", "--format", format}
	if pp := strings.TrimSpace(body.PolicyPath); pp != "" {
		args = append(args, "--policy", pp)
	}
	args = append(args, "--", ref)
	cmd := exec.CommandContext(ctx, "trivy", args...)
	out, err := cmd.CombinedOutput()
	output := string(out)
	if err != nil {
		msg := strings.TrimSpace(output)
		if msg == "" {
			msg = err.Error()
		}
		apiresponse.WriteErrorWithDetails(w, apiresponse.CodeDockerUnavailable, msg, output, http.StatusInternalServerError)
		return
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{
		"output": strings.TrimSpace(output),
		"format": format,
	}, http.StatusOK)
}
