// Package apiresponse chuẩn hóa JSON { success, data?, error? } cho REST DockLite.
package apiresponse

import (
	"encoding/json"
	"net/http"
)

// Mã lỗi domain (khớp phía client DockLite.Contracts.Api.DockLiteErrorCodes).
const (
	CodeValidation          = "VALIDATION"
	CodeNotFound            = "NOT_FOUND"
	CodeConflict            = "CONFLICT"
	CodeDockerCli           = "DOCKER_CLI"
	CodeDockerUnavailable   = "DOCKER_UNAVAILABLE"
	CodeInternal            = "INTERNAL"
	CodeBadGateway          = "BAD_GATEWAY"
	CodeComposeCommand      = "COMPOSE_COMMAND"
)

// ErrorBody là phần error khi success = false.
type ErrorBody struct {
	Code    string `json:"code"`
	Message string `json:"message"`
	Details string `json:"details,omitempty"`
}

type envelope struct {
	Success bool            `json:"success"`
	Data    json.RawMessage `json:"data,omitempty"`
	Error   *ErrorBody      `json:"error,omitempty"`
}

// WriteSuccess ghi HTTP status và JSON envelope thành công; data nil được coi là {}.
func WriteSuccess(w http.ResponseWriter, data interface{}, status int) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	var raw json.RawMessage
	var err error
	if data == nil {
		raw = json.RawMessage(`{}`)
	} else {
		raw, err = json.Marshal(data)
		if err != nil {
			WriteError(w, CodeInternal, err.Error(), http.StatusInternalServerError)
			return
		}
	}
	env := envelope{Success: true, Data: raw}
	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(env)
}

// WriteError ghi lỗi domain (không có chi tiết bổ sung).
func WriteError(w http.ResponseWriter, domainCode, message string, httpStatus int) {
	WriteErrorWithDetails(w, domainCode, message, "", httpStatus)
}

// WriteErrorWithDetails ghi lỗi; details dùng cho stdout/stderr dài (ví dụ compose prune).
func WriteErrorWithDetails(w http.ResponseWriter, domainCode, message, details string, httpStatus int) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	eb := &ErrorBody{Code: domainCode, Message: message}
	if details != "" {
		eb.Details = details
	}
	env := envelope{Success: false, Error: eb}
	w.WriteHeader(httpStatus)
	_ = json.NewEncoder(w).Encode(env)
}
