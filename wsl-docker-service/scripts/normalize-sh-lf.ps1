# Chuyen CRLF -> LF cho file .sh (goi tu thu muc wsl-docker-service hoac truyen duong dan).
param(
    [Parameter(Mandatory = $false)]
    [string] $Path = ""
)
if ([string]::IsNullOrEmpty($Path)) {
    $root = Split-Path -Parent $MyInvocation.MyCommand.Path
    $Path = Join-Path $root "run-server.sh"
}
$utf8 = New-Object System.Text.UTF8Encoding $false
$bytes = [System.IO.File]::ReadAllBytes($Path)
$text = [System.Text.Encoding]::UTF8.GetString($bytes)
$text = $text -replace "`r`n", "`n" -replace "`r", "`n"
[System.IO.File]::WriteAllText($Path, $text, $utf8)
$after = [System.IO.File]::ReadAllBytes($Path)
$cr = ($after | Where-Object { $_ -eq 13 }).Count
Write-Host "Da ghi: $Path (so byte CR con lai: $cr)"
