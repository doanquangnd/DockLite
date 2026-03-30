# Build binary Go trong WSL (không chạy server). Cần WSL và Go trong distro.
param(
    [string] $WslDistribution = "",
    [string] $WslUnixPath = ""
)

$ErrorActionPreference = "Stop"

$wslPath = ""
if (-not [string]::IsNullOrWhiteSpace($WslUnixPath)) {
    $wslPath = $WslUnixPath.Trim()
} else {
    $repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
    $svc = Join-Path $repoRoot "wsl-docker-service"
    if (-not (Test-Path $svc)) {
        throw "Không tìm thấy wsl-docker-service: $svc. Dùng -WslUnixPath '/home/.../wsl-docker-service' nếu mã chỉ có trong WSL."
    }
    $wslPath = (wsl wslpath -a (Resolve-Path $svc).Path).Trim()
    if ([string]::IsNullOrWhiteSpace($wslPath)) {
        throw "Không chuyển được đường dẫn sang WSL (wslpath)."
    }
}

if ([string]::IsNullOrWhiteSpace($WslDistribution)) {
    wsl.exe bash -c "cd '$wslPath' && mkdir -p bin && go mod tidy && go build -o bin/docklite-wsl ./cmd/server && echo Build OK: bin/docklite-wsl"
} else {
    wsl.exe -d $WslDistribution bash -c "cd '$wslPath' && mkdir -p bin && go mod tidy && go build -o bin/docklite-wsl ./cmd/server && echo Build OK: bin/docklite-wsl"
}

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
