package compose

import (
	"encoding/json"
	"fmt"
	"net/http"
	"os/exec"
	"strconv"
	"strings"

	"docklite-wsl/internal/apiresponse"
)

type serviceBody struct {
	ID       string   `json:"id"`
	Service  string   `json:"service"`
	Profiles []string `json:"profiles,omitempty"`
}

type serviceLogsBody struct {
	ID       string   `json:"id"`
	Service  string   `json:"service"`
	Tail     int      `json:"tail"`
	Profiles []string `json:"profiles,omitempty"`
}

type serviceExecBody struct {
	ID       string   `json:"id"`
	Service  string   `json:"service"`
	Command  string   `json:"command"`
	Profiles []string `json:"profiles,omitempty"`
}

func validateComposeServiceName(s string) error {
	s = strings.TrimSpace(s)
	if s == "" {
		return fmt.Errorf("thiếu tên service")
	}
	if strings.Contains(s, "..") {
		return fmt.Errorf("tên service không hợp lệ")
	}
	if strings.ContainsAny(s, ";&|`$\n\r") {
		return fmt.Errorf("tên service không hợp lệ")
	}
	return nil
}

func parseServiceLines(output string) []string {
	var out []string
	for _, line := range strings.Split(output, "\n") {
		line = strings.TrimSpace(line)
		if line != "" {
			out = append(out, line)
		}
	}
	return out
}

func resolveComposeProject(w http.ResponseWriter, projectID string) (Project, bool) {
	var zero Project
	id := strings.TrimSpace(projectID)
	if id == "" {
		apiresponse.WriteError(w, apiresponse.CodeValidation, "thiếu id", http.StatusBadRequest)
		return zero, false
	}
	items, err := loadProjects()
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeInternal, err.Error(), http.StatusInternalServerError)
		return zero, false
	}
	for _, it := range items {
		if it.ID == id {
			return it, true
		}
	}
	apiresponse.WriteError(w, apiresponse.CodeNotFound, "không tìm thấy project", http.StatusNotFound)
	return zero, false
}

// composeConfigServices xử lý POST /api/compose/config/services — `docker compose config --services`.
func composeConfigServices(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	var body composeIDBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	profiles, err := normalizeComposeProfiles(body.Profiles)
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
		return
	}
	proj, ok := resolveComposeProject(w, body.ID)
	if !ok {
		return
	}
	ctx := r.Context()
	args := dockerComposeArgs(proj, profiles, "config", "--services")
	cmd := exec.CommandContext(ctx, "docker", args...)
	cmd.Dir = proj.WslPath
	out, err := cmd.CombinedOutput()
	output := string(out)
	if err != nil {
		msg := strings.TrimSpace(output)
		if msg == "" {
			msg = err.Error()
		}
		apiresponse.WriteErrorWithDetails(w, apiresponse.CodeComposeCommand, msg, output, http.StatusInternalServerError)
		return
	}
	items := parseServiceLines(output)
	apiresponse.WriteSuccess(w, map[string]interface{}{
		"items":  items,
		"output": strings.TrimSpace(output),
	}, http.StatusOK)
}

// composeConfigValidate xử lý POST /api/compose/config/validate — `docker compose config -q` (chỉ kiểm tra, không in YAML gộp).
func composeConfigValidate(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	var body composeIDBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	profiles, err := normalizeComposeProfiles(body.Profiles)
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
		return
	}
	proj, ok := resolveComposeProject(w, body.ID)
	if !ok {
		return
	}
	ctx := r.Context()
	args := dockerComposeArgs(proj, profiles, "config", "-q")
	cmd := exec.CommandContext(ctx, "docker", args...)
	cmd.Dir = proj.WslPath
	out, err := cmd.CombinedOutput()
	output := string(out)
	if err != nil {
		msg := strings.TrimSpace(output)
		if msg == "" {
			msg = err.Error()
		}
		apiresponse.WriteErrorWithDetails(w, apiresponse.CodeComposeCommand, msg, output, http.StatusInternalServerError)
		return
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"output": strings.TrimSpace(output)}, http.StatusOK)
}

func composeServiceStart(w http.ResponseWriter, r *http.Request) {
	runComposeServiceAction(w, r, "start")
}

func composeServiceStop(w http.ResponseWriter, r *http.Request) {
	runComposeServiceAction(w, r, "stop")
}

func composeServiceExec(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	var body serviceExecBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	if err := validateComposeServiceName(body.Service); err != nil {
		apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
		return
	}
	parts, err := parseExecCommandParts(body.Command)
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
		return
	}
	profiles, err := normalizeComposeProfiles(body.Profiles)
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
		return
	}
	proj, ok := resolveComposeProject(w, body.ID)
	if !ok {
		return
	}
	svc := strings.TrimSpace(body.Service)
	args := dockerComposeArgs(proj, profiles, append([]string{"exec", "-T", svc}, parts...)...)
	execComposeInDir(w, r, proj, args)
}

func parseExecCommandParts(cmd string) ([]string, error) {
	trim := strings.TrimSpace(cmd)
	if trim == "" {
		return nil, fmt.Errorf("thiếu lệnh (ví dụ: uname -a)")
	}
	parts := strings.Fields(trim)
	if len(parts) == 0 {
		return nil, fmt.Errorf("thiếu lệnh")
	}
	if len(parts) > 48 {
		return nil, fmt.Errorf("quá nhiều đối số")
	}
	forbidden := []string{";", "|", "&", "`", "$", "(", ")", "\n", "\r", ">", "<", "'", "\""}
	for _, p := range parts {
		if len(p) > 512 {
			return nil, fmt.Errorf("đối số quá dài")
		}
		for _, ch := range forbidden {
			if strings.Contains(p, ch) {
				return nil, fmt.Errorf("ký tự không được phép trong đối số")
			}
		}
	}
	return parts, nil
}

func composeServiceLogs(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodPost {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	var body serviceLogsBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	if err := validateComposeServiceName(body.Service); err != nil {
		apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
		return
	}
	profiles, err := normalizeComposeProfiles(body.Profiles)
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
		return
	}
	proj, ok := resolveComposeProject(w, body.ID)
	if !ok {
		return
	}
	tail := body.Tail
	if tail <= 0 || tail > 10000 {
		tail = 200
	}
	args := dockerComposeArgs(proj, profiles, "logs", "--tail", strconv.Itoa(tail), strings.TrimSpace(body.Service))
	execComposeInDir(w, r, proj, args)
}

func runComposeServiceAction(w http.ResponseWriter, r *http.Request, action string) {
	if r.Method != http.MethodPost {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	var body serviceBody
	if err := json.NewDecoder(r.Body).Decode(&body); err != nil {
		http.Error(w, "bad json", http.StatusBadRequest)
		return
	}
	if err := validateComposeServiceName(body.Service); err != nil {
		apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
		return
	}
	profiles, err := normalizeComposeProfiles(body.Profiles)
	if err != nil {
		apiresponse.WriteError(w, apiresponse.CodeValidation, err.Error(), http.StatusBadRequest)
		return
	}
	proj, ok := resolveComposeProject(w, body.ID)
	if !ok {
		return
	}
	svc := strings.TrimSpace(body.Service)
	args := dockerComposeArgs(proj, profiles, action, svc)
	execComposeInDir(w, r, proj, args)
}

func execComposeInDir(w http.ResponseWriter, r *http.Request, p Project, dockerArgs []string) {
	ctx := r.Context()
	cmd := exec.CommandContext(ctx, "docker", dockerArgs...)
	cmd.Dir = p.WslPath
	out, err := cmd.CombinedOutput()
	output := string(out)
	if err != nil {
		msg := strings.TrimSpace(output)
		if msg == "" {
			msg = err.Error()
		}
		apiresponse.WriteErrorWithDetails(w, apiresponse.CodeComposeCommand, msg, output, http.StatusInternalServerError)
		return
	}
	apiresponse.WriteSuccess(w, map[string]interface{}{"output": strings.TrimSpace(output)}, http.StatusOK)
}
