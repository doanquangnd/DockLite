package httpserver

import "context"

type ctxKeyRequestID struct{}

// RequestIDFromContext trả về req_id do middleware LogRequests gắn vào context, hoặc rỗng.
func RequestIDFromContext(ctx context.Context) string {
	s, _ := ctx.Value(ctxKeyRequestID{}).(string)
	return s
}
