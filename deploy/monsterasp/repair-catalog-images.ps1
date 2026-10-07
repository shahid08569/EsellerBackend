# Fix mismatched / broken catalog images on live API.
# Downloads category-matched stock photos and re-uploads per product.
#
# Usage:
#   $env:ESELLER_ADMIN_EMAIL = "superadmin@eseller.com"
#   $env:ESELLER_ADMIN_PASSWORD = "YOUR_PASSWORD"
#   pwsh -File deploy/monsterasp/repair-catalog-images.ps1
#
# Does NOT print access tokens.

$ErrorActionPreference = "Stop"
$Api = "https://api.esellerglobal.com/api/v1"
$email = $env:ESELLER_ADMIN_EMAIL
$pass = $env:ESELLER_ADMIN_PASSWORD
if ([string]::IsNullOrWhiteSpace($email) -or [string]::IsNullOrWhiteSpace($pass)) {
  throw "Set ESELLER_ADMIN_EMAIL and ESELLER_ADMIN_PASSWORD env vars first."
}

function Invoke-ApiJson {
  param([string]$Method, [string]$Url, [hashtable]$Headers = @{}, [object]$Body = $null)
  $tmpIn = Join-Path $env:TEMP ("eseller-req-" + [guid]::NewGuid().ToString("n") + ".json")
  $tmpOut = Join-Path $env:TEMP ("eseller-resp-" + [guid]::NewGuid().ToString("n") + ".json")
  try {
    $curlArgs = [System.Collections.Generic.List[string]]::new()
    [void]$curlArgs.AddRange([string[]]@("-sS", "-X", $Method, $Url, "-H", "Content-Type: application/json", "-o", $tmpOut, "-w", "%{http_code}"))
    foreach ($k in $Headers.Keys) {
      [void]$curlArgs.Add("-H")
      [void]$curlArgs.Add(("{0}: {1}" -f $k, $Headers[$k]))
    }
    if ($null -ne $Body) {
      $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 8 -Compress }
      [System.IO.File]::WriteAllText($tmpIn, $json)
      [void]$curlArgs.Add("--data-binary")
      [void]$curlArgs.Add("@$tmpIn")
    }
    $code = & curl.exe @($curlArgs.ToArray())
    $raw = if (Test-Path $tmpOut) { Get-Content $tmpOut -Raw -ErrorAction SilentlyContinue } else { "" }
    return @{ Code = [int]$code; Body = $raw }
  }
  finally {
    Remove-Item $tmpIn, $tmpOut -Force -ErrorAction SilentlyContinue
  }
}

function Invoke-ApiUpload {
  param([string]$Url, [hashtable]$Headers, [string]$FilePath)
  $tmpOut = Join-Path $env:TEMP ("eseller-up-" + [guid]::NewGuid().ToString("n") + ".json")
  try {
    $curlArgs = [System.Collections.Generic.List[string]]::new()
    [void]$curlArgs.AddRange([string[]]@("-sS", "-X", "POST", $Url, "-F", "file=@$FilePath", "-o", $tmpOut, "-w", "%{http_code}"))
    foreach ($k in $Headers.Keys) {
      [void]$curlArgs.Add("-H")
      [void]$curlArgs.Add(("{0}: {1}" -f $k, $Headers[$k]))
    }
    $code = & curl.exe @($curlArgs.ToArray())
    $raw = if (Test-Path $tmpOut) { Get-Content $tmpOut -Raw -ErrorAction SilentlyContinue } else { "" }
    return @{ Code = [int]$code; Body = $raw }
  }
  finally {
    Remove-Item $tmpOut -Force -ErrorAction SilentlyContinue
  }
}

# Curated Unsplash CDN URLs (stable, ~800px, category-correct).
$CategoryImages = @{
  "Electronics" = @(
    "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1546435770-a3e426bf472b?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1593359677879-a4bb92f829d1?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1526170375885-4d8ecf77b99f?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1583394838336-acd977736f90?auto=format&fit=crop&w=800&q=80"
  )
  "Mobiles & Tablets" = @(
    "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1565849904461-04a58ad377e0?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1585060544812-6b45742d762f?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1601784551446-20c9e07cdbdb?auto=format&fit=crop&w=800&q=80"
  )
  "Computers & Laptops" = @(
    "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1525547719571-a2d4ac8945e2?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1587829741301-dc798b83add3?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1527864550417-7fd91fc51a46?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1593640408182-31c70c8268f5?auto=format&fit=crop&w=800&q=80"
  )
  "Fashion" = @(
    "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1542291026-7eec264c27ff?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1551028719-00167b16eac5?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1434389677669-e08b4cac3105?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1549298916-b41d501d3772?auto=format&fit=crop&w=800&q=80"
  )
  "Home & Living" = @(
    "https://images.unsplash.com/photo-1555041469-a586c61ea9bc?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1586023492125-27b2c045efd7?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1507473885765-e6ed057f782c?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1556911220-bff31c812dba?auto=format&fit=crop&w=800&q=80"
  )
  "Beauty & Personal Care" = @(
    "https://images.unsplash.com/photo-1596462502278-27bfdc403348?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1522335789203-aabd1fc54bc0?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1571781926291-c477ebfd024b?auto=format&fit=crop&w=800&q=80"
  )
  "Sports & Outdoors" = @(
    "https://images.unsplash.com/photo-1517836357463-d25dfeac3438?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1571019614242-c5c5dee9f50b?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1461896836934-ffe607ba8211?auto=format&fit=crop&w=800&q=80"
  )
  "Groceries" = @(
    "https://images.unsplash.com/photo-1542838132-92c53300491e?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1610832958506-aa56368176cf?auto=format&fit=crop&w=800&q=80"
  )
  "Toys & Kids" = @(
    "https://images.unsplash.com/photo-1558060370-d644479cb6f7?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1596461404969-9ae70f2830c1?auto=format&fit=crop&w=800&q=80"
  )
  "Automotive" = @(
    "https://images.unsplash.com/photo-1492144534655-ae79c964c9d7?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1486262715619-67b85e0b08d3?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1503376780353-7e6692767b70?auto=format&fit=crop&w=800&q=80"
  )
}

function Get-CategoryImageUrl([string]$categoryName, [int]$index) {
  $list = $CategoryImages[$categoryName]
  if (-not $list -or $list.Count -eq 0) {
    $list = $CategoryImages["Electronics"]
  }
  return $list[$index % $list.Count]
}

function Get-ProductImages {
  param([string]$ProductId, [hashtable]$Headers)
  $res = Invoke-ApiJson -Method GET -Url "$Api/products/$ProductId/images" -Headers $Headers
  if ($res.Code -ne 200) { return @() }
  $obj = $res.Body | ConvertFrom-Json
  if ($obj -is [array]) { return @($obj) }
  if ($obj.items) { return @($obj.items) }
  return @()
}

Write-Host "Logging in..."
$login = Invoke-ApiJson -Method POST -Url "$Api/auth/login" -Body @{
  usernameOrEmail = $email
  password        = $pass
  latitude        = 0
  longitude       = 0
  deviceType      = "Desktop"
}
if ($login.Code -ne 200) {
  throw "Login failed HTTP $($login.Code)"
}
$token = ($login.Body | ConvertFrom-Json).accessToken
if (-not $token) { $token = ($login.Body | ConvertFrom-Json).AccessToken }
if (-not $token) { throw "No accessToken." }
$auth = @{ Authorization = "Bearer $token" }
Write-Host "Login OK."

$products = New-Object System.Collections.Generic.List[object]
$page = 1
do {
  $res = Invoke-ApiJson -Method GET -Url "$Api/products/catalog?pageNumber=$page&pageSize=50" -Headers $auth
  if ($res.Code -ne 200) { throw "Catalog list failed $($res.Code)" }
  $obj = $res.Body | ConvertFrom-Json
  foreach ($it in @($obj.items)) { [void]$products.Add($it) }
  $totalPages = [int]$obj.totalPages
  if ($totalPages -lt 1) { $totalPages = 1 }
  Write-Host "Catalog page $page/$totalPages (loaded $($products.Count))"
  $page++
} while ($page -le $totalPages)

$tmpDir = Join-Path $env:TEMP ("eseller-img-fix-" + [guid]::NewGuid().ToString("n"))
New-Item -ItemType Directory -Path $tmpDir | Out-Null
$ok = 0
$fail = 0
$i = 0

try {
  foreach ($p in $products) {
    $i++
    $id = [string]$p.id
    $cat = [string]($p.categoryName ?? $p.CategoryName ?? "Electronics")
    $name = [string]($p.name ?? $p.Name ?? $id)
    $imgUrl = Get-CategoryImageUrl -categoryName $cat -index ($i - 1)
    $local = Join-Path $tmpDir ("p-$i.jpg")

    try {
      & curl.exe -sS -L $imgUrl -o $local
      if (-not (Test-Path $local) -or (Get-Item $local).Length -lt 2000) {
        throw "Download too small for $name"
      }

      # Remove existing images so the new one becomes cover
      $existing = Get-ProductImages -ProductId $id -Headers $auth
      foreach ($img in $existing) {
        $imgId = [string]($img.id ?? $img.Id)
        if ($imgId) {
          $null = Invoke-ApiJson -Method DELETE -Url "$Api/products/$id/images/$imgId" -Headers $auth
        }
      }

      $up = Invoke-ApiUpload -Url "$Api/products/$id/images" -Headers $auth -FilePath $local
      if ($up.Code -in 200, 201) {
        $ok++
        Write-Host "[$i/$($products.Count)] OK  $cat :: $name"
      }
      else {
        $fail++
        Write-Host "[$i/$($products.Count)] FAIL upload HTTP $($up.Code) :: $name"
      }
    }
    catch {
      $fail++
      Write-Host "[$i/$($products.Count)] FAIL $($_.Exception.Message) :: $name"
    }

    Start-Sleep -Milliseconds 200
  }
}
finally {
  Remove-Item $tmpDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "`n========== REPAIR SUMMARY =========="
Write-Host "Products: $($products.Count)"
Write-Host "Images fixed: $ok"
Write-Host "Failed: $fail"
Write-Host "Hard-refresh storefront after ~60s (homepage cache)."
