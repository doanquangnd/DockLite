# Chạy service DockLite trong WSL (build + exec). Giữ cửa sổ terminal mở khi chạy.
param(
    # Tên distro (khi có nhiều bản WSL), ví dụ Ubuntu-22.04
    [string] $WslDistribution = "",
    # Đường dẫn POSIX trong WSL tới thư mục wsl-docker-service (khi mã chỉ nằm trong WSL, không có bản Windows cạnh repo DockLite).
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

Write-Host "WSL path: $wslPath"
Write-Host "Lắng nghe mặc định 127.0.0.1:17890 (DOCKLITE_ADDR để đổi)."

# bash -lc: login shell co PATH go (bash -c khong nap .bashrc tren nhieu distro).
if ([string]::IsNullOrWhiteSpace($WslDistribution)) {
    wsl.exe bash -lc "cd '$wslPath' && exec bash scripts/run-server.sh"
} else {
    wsl.exe -d $WslDistribution bash -lc "cd '$wslPath' && exec bash scripts/run-server.sh"
}
