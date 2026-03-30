// Package dockerengine cung cấp Docker Engine API client (tránh spawn docker CLI cho hầu hết thao tác).
package dockerengine

import (
	"sync"

	"github.com/docker/docker/client"
)

var (
	once   sync.Once
	cli    *client.Client
	cliErr error
)

// Client trả singleton kết nối socket mặc định (DOCKER_HOST / Unix trên WSL).
func Client() (*client.Client, error) {
	once.Do(func() {
		cli, cliErr = client.NewClientWithOpts(
			client.FromEnv,
			client.WithAPIVersionNegotiation(),
		)
	})
	return cli, cliErr
}
