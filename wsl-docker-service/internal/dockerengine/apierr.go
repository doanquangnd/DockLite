package dockerengine

import (
	"net/http"

	"docklite-wsl/internal/apiresponse"
	"github.com/docker/docker/errdefs"
)

// WriteError ánh xạ lỗi Docker Engine API sang HTTP + mã domain (code `DOCKER_CLI` giữ tương thích client C#).
func WriteError(w http.ResponseWriter, err error) {
	switch {
	case errdefs.IsNotFound(err):
		apiresponse.WriteError(w, apiresponse.CodeNotFound, err.Error(), http.StatusNotFound)
	case errdefs.IsConflict(err):
		apiresponse.WriteError(w, apiresponse.CodeConflict, err.Error(), http.StatusConflict)
	default:
		apiresponse.WriteError(w, apiresponse.CodeDockerCli, err.Error(), http.StatusInternalServerError)
	}
}
