package httpserver

import (
	"crypto/rand"
	"encoding/hex"
	"encoding/json"
	"net/http"
	"strings"
	"sync"
	"unicode/utf8"

	"docklite-wsl/internal/apiresponse"
	"golang.org/x/time/rate"
)

const (
	authRotateMaxBodyBytes  = 4096
	authRotateTokenMaxRunes   = 8192
	newTokenRandomBytes       = 32
	authRotatePerMinute       = 5
	authRotateLimiterBurst    = 5
)

var authRotateIPLimiters sync.Map

type authRotateRequest struct {
	CurrentToken string `json:"current_token"`
}

type authRotateData struct {
	NewToken string `json:"new_token"`
}

func authRotateLimiter(ip string) *rate.Limiter {
	if v, ok := authRotateIPLimiters.Load(ip); ok {
		return v.(*rate.Limiter)
	}
	lim := rate.NewLimiter(rate.Limit(float64(authRotatePerMinute)/60.0), authRotateLimiterBurst)
	if actual, loaded := authRotateIPLimiters.LoadOrStore(ip, lim); loaded {
		return actual.(*rate.Limiter)
	}
	return lim
}

// HandleAuthRotate xử lý POST /api/auth/rotate: giữ Bearer hiện tại, tạo mật khẩu mới và cập nhật trong bộ nhớ.
func HandleAuthRotate(state *MutableToken) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
			return
		}

		if state == nil || state.IsEmpty() {
			apiresponse.WriteError(w, apiresponse.CodeAuth, "xác thực API chưa bật trên service", http.StatusNotFound)
			return
		}

		lim := authRotateLimiter(clientIP(r))
		if !lim.Allow() {
			apiresponse.WriteError(w, apiresponse.CodeRateLimit, "quá nhiều yêu cầu xoay token, thử lại sau", http.StatusTooManyRequests)
			return
		}

		r = limitAuthRotateBody(r)

		var req authRotateRequest
		dec := json.NewDecoder(r.Body)
		if err := dec.Decode(&req); err != nil {
			apiresponse.WriteError(w, apiresponse.CodeValidation, "JSON không hợp lệ", http.StatusBadRequest)
			return
		}
		ct := strings.TrimSpace(req.CurrentToken)
		if len(ct) == 0 {
			apiresponse.WriteError(w, apiresponse.CodeValidation, "current_token bắt buộc", http.StatusBadRequest)
			return
		}
		if utf8.RuneCountInString(ct) > authRotateTokenMaxRunes {
			apiresponse.WriteError(w, apiresponse.CodeValidation, "current_token quá dài", http.StatusBadRequest)
			return
		}
		if !state.CompareBytes([]byte(ct)) {
			w.Header().Set("WWW-Authenticate", `Bearer realm="docklite"`)
			apiresponse.WriteError(w, apiresponse.CodeAuth, "mật khẩu hiện tại không khớp", http.StatusUnauthorized)
			return
		}

		buf := make([]byte, newTokenRandomBytes)
		if _, err := rand.Read(buf); err != nil {
			apiresponse.WriteError(w, apiresponse.CodeInternal, "không tạo được mã ngẫu nhiên", http.StatusInternalServerError)
			return
		}
		newToken := hex.EncodeToString(buf)
		state.Update(newToken)
		apiresponse.WriteSuccess(w, authRotateData{NewToken: newToken}, http.StatusOK)
	}
}

func limitAuthRotateBody(r *http.Request) *http.Request {
	if r.ContentLength > authRotateMaxBodyBytes {
		r2 := *r
		r2.Body = http.MaxBytesReader(nil, r.Body, authRotateMaxBodyBytes)
		return &r2
	}
	r2 := *r
	r2.Body = http.MaxBytesReader(nil, r.Body, authRotateMaxBodyBytes)
	return &r2
}
