# Additive upload of Eseller.API/wwwroot/uploads → MonsterASP
# Never deletes remote files. Creates missing directories via curl --ftp-create-dirs.
#
# Required env:
#   FTP_SERVER, FTP_USERNAME, FTP_PASSWORD
# Optional:
#   FTP_SERVER_DIR=/wwwroot/
#   FTP_PROTOCOL=ftps|ftp
#   LOCAL_UPLOADS=publish/wwwroot/uploads  (or Eseller.API/wwwroot/uploads)

$ErrorActionPreference = "Continue"

$server = $env:FTP_SERVER
$user = $env:FTP_USERNAME
$pass = $env:FTP_PASSWORD
$baseDir = ($env:FTP_SERVER_DIR)
if ([string]::IsNullOrWhiteSpace($baseDir)) { $baseDir = "/wwwroot/" }
$baseDir = $baseDir.TrimEnd('/')
$protocol = if ($env:FTP_PROTOCOL) { $env:FTP_PROTOCOL.ToLowerInvariant() } else { "ftps" }

$localRoot = $env:LOCAL_UPLOADS
if ([string]::IsNullOrWhiteSpace($localRoot)) {
  if (Test-Path "publish/wwwroot/uploads") { $localRoot = "publish/wwwroot/uploads" }
  elseif (Test-Path "Eseller.API/wwwroot/uploads") { $localRoot = "Eseller.API/wwwroot/uploads" }
  else { throw "No local uploads folder found." }
}
$localRoot = (Resolve-Path $localRoot).Path

$files = @(Get-ChildItem $localRoot -Recurse -File | Where-Object {
  $_.Name -ne ".keep" -and $_.Length -gt 0
})
Write-Host "Local upload files (non-empty): $($files.Count) from $localRoot"
if ($files.Count -eq 0) {
  Write-Warning "Nothing to upload."
  exit 0
}

# Physical path on MonsterASP is ContentRoot/wwwroot/uploads → FTP /wwwroot/wwwroot/uploads
$remoteRoot = "$baseDir/wwwroot/uploads"
$scheme = "ftp"
$curlSsl = @()
if ($protocol -ne "ftp") {
  # Explicit FTPS (AUTH TLS). --ssl-reqd fails hard if TLS unavailable.
  $curlSsl = @("--ssl-reqd", "--ftp-ssl")
}

function Upload-One([string]$localPath, [string]$remoteRel, [int]$attempt) {
  $remoteUrl = "${scheme}://${server}${remoteRoot}/$remoteRel"
  $args = @(
    "-sS", "--fail", "--ftp-create-dirs",
    "-u", "${user}:${pass}",
    "-T", $localPath,
    "--retry", "2",
    "--connect-timeout", "30",
    "--max-time", "120"
  ) + $curlSsl + @($remoteUrl)

  & curl.exe @args
  if ($LASTEXITCODE -eq 0) { return $true }

  # Fallback: plain FTP if FTPS failed
  if ($protocol -ne "ftp" -and $attempt -eq 1) {
    $plainUrl = "ftp://${server}${remoteRoot}/$remoteRel"
    $plainArgs = @(
      "-sS", "--fail", "--ftp-create-dirs",
      "-u", "${user}:${pass}",
      "-T", $localPath,
      "--retry", "2",
      "--connect-timeout", "30",
      "--max-time", "120",
      $plainUrl
    )
    & curl.exe @plainArgs
    return ($LASTEXITCODE -eq 0)
  }
  return $false
}

$ok = 0
$fail = 0
$failed = New-Object System.Collections.Generic.List[string]
$i = 0
foreach ($f in $files) {
  $i++
  $rel = $f.FullName.Substring($localRoot.Length).TrimStart('\', '/').Replace('\', '/')
  $success = $false
  for ($try = 1; $try -le 3 -and -not $success; $try++) {
    $success = Upload-One $f.FullName $rel $try
    if (-not $success) { Start-Sleep -Seconds (2 * $try) }
  }
  if ($success) {
    $ok++
  } else {
    $fail++
    $failed.Add($rel) | Out-Null
    Write-Warning "Failed: $rel"
  }
  if ($i % 25 -eq 0) {
    Write-Host "[$i/$($files.Count)] ok=$ok fail=$fail"
  }
}

Write-Host "Uploaded $ok / $($files.Count) (failed=$fail) → $remoteRoot"
if ($failed.Count -gt 0) {
  Write-Host "First failures:"
  $failed | Select-Object -First 15 | ForEach-Object { Write-Host "  $_" }
}

# Hard fail if most images did not land — prevents silent broken storefront.
$minOk = [Math]::Max(1, [int]([Math]::Floor($files.Count * 0.85)))
if ($ok -lt $minOk) {
  throw "Image restore incomplete: $ok/$($files.Count) (need >= $minOk). Check FTP path/permissions."
}
