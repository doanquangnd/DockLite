package httpserver

import (
	"math"
	"net/http"
	"strconv"
	"strings"
	"sync"

	"golang.org/x/time/rate"
)

// Rate: REST 30 yêu cầu/giây, burst 60. WebSocket upgrade: 2/s, burst 4 (mỗi IP). Từ chối: 429, Retry-After (giây).
const (
	restRatePerSec  = 30.0
	restBurst       = 60
	wsUpgradePerSec = 2.0
	wsUpgradeBurst  = 4
)

type dualLimiter struct {
	rest *rate.Limiter
	ws   *rate.Limiter
}

var rateMap sync.Map

func isWebSocketUpgrade(r *http.Request) bool {
	return strings.EqualFold(r.Header.Get("Upgrade"), "websocket")
}

func getDualLimiter(ip string) *dualLimiter {
	if v, ok := rateMap.Load(ip); ok {
		return v.(*dualLimiter)
	}
	d := &dualLimiter{
		rest: rate.NewLimiter(restRatePerSec, restBurst),
		ws:   rate.NewLimiter(wsUpgradePerSec, wsUpgradeBurst),
	}
	if actual, loaded := rateMap.LoadOrStore(ip, d); loaded {
		return actual.(*dualLimiter)
	}
	return d
}

// PerIPRateLimit áp dụng giới hạn theo IP: REST tách với tăng cấp WebSocket.
func PerIPRateLimit(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		ip := clientIP(r)
		lim := getDualLimiter(ip)
		which := lim.rest
		if isWebSocketUpgrade(r) {
			which = lim.ws
		}
		if !which.Allow() {
			sec := 1.0
			if f := which.Limit(); f > 0 {
				sec = math.Ceil(1.0 / float64(f))
			}
			if sec < 1 {
				sec = 1
			}
			if sec > 60 {
				sec = 60
			}
			w.Header().Set("Retry-After", strconv.FormatInt(int64(sec), 10))
			http.Error(w, "quá nhiều yêu cầu", http.StatusTooManyRequests)
			return
		}
		next.ServeHTTP(w, r)
	})
}
