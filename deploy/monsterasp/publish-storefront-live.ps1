# Publish warehouse catalog to customer storefront.
# Requires ESELLER_ADMIN_EMAIL / ESELLER_ADMIN_PASSWORD env vars.
$ErrorActionPreference = "Stop"
$Api = "https://api.esellerglobal.com/api/v1"
$email = $env:ESELLER_ADMIN_EMAIL
$pass = $env:ESELLER_ADMIN_PASSWORD
if ([string]::IsNullOrWhiteSpace($email) -or [string]::IsNullOrWhiteSpace($pass)) {
  throw "Set ESELLER_ADMIN_EMAIL and ESELLER_ADMIN_PASSWORD env vars first."
}

function Invoke-CurlJson {
  param([string]$Method,[string]$Url,[hashtable]$Headers=$null,[object]$Body=$null)
  $tmpIn = Join-Path $env:TEMP ("pub-in-" + [guid]::NewGuid().ToString("n") + ".json")
  $tmpOut = Join-Path $env:TEMP ("pub-out-" + [guid]::NewGuid().ToString("n") + ".json")
  try {
    $curlArgs = [System.Collections.Generic.List[string]]::new()
    $curlArgs.AddRange([string[]]@("-sS","-X",$Method,$Url,"-H","Content-Type: application/json","-o",$tmpOut,"-w","%{http_code}"))
    if ($Headers) {
      foreach ($k in $Headers.Keys) {
        $curlArgs.Add("-H") | Out-Null
        $curlArgs.Add(("{0}: {1}" -f $k, $Headers[$k])) | Out-Null
      }
    }
    if ($null -ne $Body) {
      [System.IO.File]::WriteAllText($tmpIn, ($Body | ConvertTo-Json -Depth 6 -Compress))
      $curlArgs.Add("--data-binary") | Out-Null
      $curlArgs.Add("@$tmpIn") | Out-Null
    }
    $code = & curl.exe @($curlArgs.ToArray())
    $raw = if (Test-Path $tmpOut) { Get-Content $tmpOut -Raw } else { "" }
    @{ Code=[int]$code; Body=$raw }
  } finally {
    Remove-Item $tmpIn,$tmpOut -Force -ErrorAction SilentlyContinue
  }
}

Write-Host "Logging in..."
$login = Invoke-CurlJson POST "$Api/auth/login" -Body @{
  usernameOrEmail=$email; password=$pass
  latitude=0; longitude=0; deviceType="Desktop"
}
if ($login.Code -ne 200) { throw "Login failed $($login.Code)" }
$token = ($login.Body | ConvertFrom-Json).accessToken
$auth = @{ Authorization = "Bearer $token" }
Write-Host "Login OK"

$ids = New-Object System.Collections.Generic.List[string]
$page = 1
do {
  $res = Invoke-CurlJson GET "$Api/products/catalog?pageNumber=$page&pageSize=50" -Headers $auth
  $obj = $res.Body | ConvertFrom-Json
  foreach ($it in $obj.items) {
    if (-not $it.isStorefrontLive) { $ids.Add([string]$it.id) | Out-Null }
  }
  $totalPages = [int]$obj.totalPages
  if (-not $totalPages) { $totalPages = 1 }
  Write-Host "Catalog page $page/$totalPages — queued offline: $($ids.Count)"
  $page++
} while ($page -le $totalPages)

Write-Host "Setting storefront-live on $($ids.Count) products..."
$ok = 0
$fail = 0
$i = 0
foreach ($id in $ids) {
  $i++
  $r = Invoke-CurlJson PUT "$Api/admin/products/$id/storefront-live" -Headers $auth -Body @{ isStorefrontLive = $true }
  if ($r.Code -in 200,201) { $ok++ } else { $fail++; Write-Host "FAIL $id $($r.Code)" }
  if ($i % 20 -eq 0) { Write-Host "  $i/$($ids.Count) ok=$ok fail=$fail" }
}

# Feature first ~24 newest for homepage pins
$featRes = Invoke-CurlJson GET "$Api/products/catalog?pageNumber=1&pageSize=24" -Headers $auth
$featIds = @(($featRes.Body | ConvertFrom-Json).items | ForEach-Object { $_.id })
if ($featIds.Count -gt 0) {
  $fr = Invoke-CurlJson PUT "$Api/admin/products/featured-bulk" -Headers $auth -Body @{
    productIds = $featIds
    isFeatured = $true
  }
  Write-Host "Featured bulk: HTTP $($fr.Code) $($fr.Body.Substring(0,[Math]::Min(120,$fr.Body.Length)))"
}

$probe = Invoke-CurlJson GET "$Api/products?pageNumber=1&pageSize=1"
$probeObj = $probe.Body | ConvertFrom-Json
Write-Host "DONE live=$ok fail=$fail publicTotal=$($probeObj.totalCount)"
