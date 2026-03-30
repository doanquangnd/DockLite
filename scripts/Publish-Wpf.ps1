# Xuất bản ứng dụng WPF (win-x64) vào thư mục artifacts.
# Yêu cầu: .NET SDK 8.
param(
    [string] $OutputRelativePath = "artifacts\publish-win-x64",
    # Bật để dùng runtime máy (cần .NET 8 Desktop Runtime); mặc định là gói self-contained.
    [switch] $FrameworkDependent,
    # Bật để không gộp một file (dễ gỡ lỗi khi publish).
    [switch] $NoSingleFile
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$outDir = Join-Path $repoRoot $OutputRelativePath
$proj = Join-Path $repoRoot "src\DockLite.App\DockLite.App.csproj"

if (-not (Test-Path $proj)) {
    throw "Không tìm thấy DockLite.App.csproj: $proj"
}

$selfContained = -not $FrameworkDependent.IsPresent
$singleFile = -not $NoSingleFile.IsPresent

# Nếu exe đích đang bị khóa (đang chạy từ thư mục publish), GenerateBundle sẽ lỗi Access denied khi ghi đè.
$publishedExe = Join-Path $outDir "DockLite.App.exe"
if (Test-Path $publishedExe) {
    $fullExe = (Resolve-Path $publishedExe).Path
    Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and ($_.Path -ieq $fullExe)
    } | ForEach-Object {
        Write-Host "Đang đóng DockLite đang chạy từ thư mục publish (PID $($_.Id))."
        Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Milliseconds 500
}

Write-Host "Repo: $repoRoot"
Write-Host "Output: $outDir"
Write-Host "SelfContained: $selfContained  PublishSingleFile: $singleFile"

$dotnetArgs = @(
    "publish", $proj,
    "-c", "Release",
    "-r", "win-x64",
    "-o", $outDir,
    "--self-contained", $(if ($selfContained) { "true" } else { "false" }),
    "-p:PublishSingleFile=$singleFile",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true"
)

& dotnet @dotnetArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Hoan thanh. Chay: $outDir\DockLite.App.exe"
