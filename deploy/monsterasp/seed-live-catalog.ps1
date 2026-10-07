# Live catalog seed. Set env ESELLER_ADMIN_EMAIL / ESELLER_ADMIN_PASSWORD before running.
# Usage: pwsh -File seed-live-catalog.ps1
# Does NOT print access tokens.

$ErrorActionPreference = "Stop"
$Api = "https://api.esellerglobal.com/api/v1"
$UploadsRoot = "F:\Eseller\EsellerBackend\Eseller.API\wwwroot\uploads\products"
$email = $env:ESELLER_ADMIN_EMAIL
$pass = $env:ESELLER_ADMIN_PASSWORD
if ([string]::IsNullOrWhiteSpace($email) -or [string]::IsNullOrWhiteSpace($pass)) {
  throw "Set ESELLER_ADMIN_EMAIL and ESELLER_ADMIN_PASSWORD env vars first."
}

function Invoke-ApiJson {
  param(
    [string]$Method,
    [string]$Url,
    [hashtable]$Headers = @{},
    [object]$Body = $null
  )
  $tmpIn = Join-Path $env:TEMP ("eseller-req-" + [guid]::NewGuid().ToString("n") + ".json")
  $tmpOut = Join-Path $env:TEMP ("eseller-resp-" + [guid]::NewGuid().ToString("n") + ".json")
  try {
    $curlArgs = [System.Collections.Generic.List[string]]::new()
    $curlArgs.AddRange([string[]]@("-sS", "-X", $Method, $Url, "-H", "Content-Type: application/json", "-o", $tmpOut, "-w", "%{http_code}"))
    foreach ($k in $Headers.Keys) {
      $curlArgs.Add("-H") | Out-Null
      $curlArgs.Add(("{0}: {1}" -f $k, $Headers[$k])) | Out-Null
    }
    if ($null -ne $Body) {
      $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 8 -Compress }
      [System.IO.File]::WriteAllText($tmpIn, $json)
      $curlArgs.Add("--data-binary") | Out-Null
      $curlArgs.Add("@$tmpIn") | Out-Null
    }
    $code = & curl.exe @($curlArgs.ToArray())
    $raw = if (Test-Path $tmpOut) { Get-Content $tmpOut -Raw -ErrorAction SilentlyContinue } else { "" }
    return @{ Code = [int]$code; Body = $raw }
  }
  finally {
    Remove-Item $tmpIn -Force -ErrorAction SilentlyContinue
    Remove-Item $tmpOut -Force -ErrorAction SilentlyContinue
  }
}

function Invoke-ApiUpload {
  param(
    [string]$Url,
    [hashtable]$Headers,
    [string]$FilePath
  )
  $tmpOut = Join-Path $env:TEMP ("eseller-up-" + [guid]::NewGuid().ToString("n") + ".json")
  try {
    $curlArgs = [System.Collections.Generic.List[string]]::new()
    $curlArgs.AddRange([string[]]@("-sS", "-X", "POST", $Url, "-F", "file=@$FilePath", "-o", $tmpOut, "-w", "%{http_code}"))
    foreach ($k in $Headers.Keys) {
      $curlArgs.Add("-H") | Out-Null
      $curlArgs.Add(("{0}: {1}" -f $k, $Headers[$k])) | Out-Null
    }
    $code = & curl.exe @($curlArgs.ToArray())
    $raw = if (Test-Path $tmpOut) { Get-Content $tmpOut -Raw -ErrorAction SilentlyContinue } else { "" }
    return @{ Code = [int]$code; Body = $raw }
  }
  finally {
    Remove-Item $tmpOut -Force -ErrorAction SilentlyContinue
  }
}

Write-Host "Logging in..."
$loginBody = @{
  usernameOrEmail = $email
  password        = $pass
  latitude        = 0
  longitude       = 0
  deviceType      = "Desktop"
}
$login = Invoke-ApiJson -Method POST -Url "$Api/auth/login" -Body $loginBody
if ($login.Code -ne 200) {
  throw "Login failed HTTP $($login.Code): $($login.Body.Substring(0, [Math]::Min(200, $login.Body.Length)))"
}
$loginObj = $login.Body | ConvertFrom-Json
$token = $loginObj.accessToken
if (-not $token) { $token = $loginObj.AccessToken }
if (-not $token) { throw "No accessToken in login response." }
$auth = @{ Authorization = "Bearer $token" }
Write-Host "Login OK."

$categories = (Invoke-ApiJson -Method GET -Url "$Api/categories").Body | ConvertFrom-Json
$brands = (Invoke-ApiJson -Method GET -Url "$Api/brands").Body | ConvertFrom-Json
Write-Host "Categories: $($categories.Count) | Brands: $($brands.Count)"

# Prefer category-matched Unsplash images (avoids sneakers under Mobiles etc.).
# Local uploads folder is only a fallback.
$CategoryImageUrls = @{
  "Electronics"            = @(
    "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1546435770-a3e426bf472b?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1593359677879-a4bb92f829d1?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1608043152269-423dbba4e7e1?auto=format&fit=crop&w=800&q=80"
  )
  "Mobiles & Tablets"      = @(
    "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1565849904461-04a58ad377e0?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1544244015-0df4b3ffc6b0?auto=format&fit=crop&w=800&q=80"
  )
  "Computers & Laptops"    = @(
    "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1525547719571-a2d4ac8945e2?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1587829741301-dc798b83add3?auto=format&fit=crop&w=800&q=80"
  )
  "Fashion"                = @(
    "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1542291026-7eec264c27ff?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1551028719-00167b16eac5?auto=format&fit=crop&w=800&q=80"
  )
  "Home & Living"          = @(
    "https://images.unsplash.com/photo-1555041469-a586c61ea9bc?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1586023492125-27b2c045efd7?auto=format&fit=crop&w=800&q=80"
  )
  "Beauty & Personal Care" = @(
    "https://images.unsplash.com/photo-1596462502278-27bfdc403348?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1522335789203-aabd1fc54bc0?auto=format&fit=crop&w=800&q=80"
  )
  "Sports & Outdoors"      = @(
    "https://images.unsplash.com/photo-1517836357463-d25dfeac3438?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1571019614242-c5c5dee9f50b?auto=format&fit=crop&w=800&q=80"
  )
  "Groceries"              = @(
    "https://images.unsplash.com/photo-1542838132-92c53300491e?auto=format&fit=crop&w=800&q=80"
  )
  "Toys & Kids"            = @(
    "https://images.unsplash.com/photo-1558060370-d644479cb6f7?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1596461404969-9ae70f2830c1?auto=format&fit=crop&w=800&q=80"
  )
  "Automotive"             = @(
    "https://images.unsplash.com/photo-1492144534655-ae79c964c9d7?auto=format&fit=crop&w=800&q=80",
    "https://images.unsplash.com/photo-1486262715619-67b85e0b08d3?auto=format&fit=crop&w=800&q=80"
  )
}

$imagePool = @(Get-ChildItem $UploadsRoot -Recurse -File -Include *.jpg,*.jpeg,*.png,*.webp -ErrorAction SilentlyContinue |
  Where-Object { $_.Length -gt 5000 -and $_.Length -lt 900000 } | Sort-Object Length -Descending)
Write-Host "Local image fallback pool: $($imagePool.Count)"

$BrandPools = @{
  "Electronics"            = @("Samsung", "Sony", "Apple", "Xiaomi", "Huawei")
  "Mobiles & Tablets"      = @("Samsung", "Apple", "Xiaomi", "Huawei")
  "Computers & Laptops"    = @("Apple", "Samsung", "Huawei", "Generic / Unbranded")
  "Fashion"                = @("Nike", "Adidas", "Generic / Unbranded")
  "Home & Living"          = @("Generic / Unbranded", "Xiaomi", "Samsung")
  "Beauty & Personal Care" = @("Generic / Unbranded")
  "Sports & Outdoors"      = @("Nike", "Adidas", "Generic / Unbranded")
  "Groceries"              = @("Generic / Unbranded")
  "Toys & Kids"            = @("Generic / Unbranded", "Nike", "Adidas")
  "Automotive"             = @("Generic / Unbranded", "Sony", "Xiaomi")
}

# Category-specific product name templates (12 each â†’ 10+)
$catalog = @{
  "Electronics"            = @("LED Smart TV 43 inch","Bluetooth Soundbar","Wireless Earbuds Pro","Portable Power Bank 20000mAh","Smart WiFi Plug","USB-C Hub 7-in-1","Action Camera 4K","Wireless Charging Pad","Noise Cancelling Headphones","Streaming Stick HD","Car Dash Cam","Mini Bluetooth Speaker")
  "Mobiles & Tablets"      = @("Galaxy A Series Phone","iPhone-ready Case Pack","Android Tablet 10 inch","Stylus Pen for Tablets","Fast Charger 45W","Tempered Glass Screen Guard","Magnetic Car Phone Mount","Power Bank Slim 10000mAh","Wireless Earphones Sport","Tablet Keyboard Cover","USB-C Earphones","Phone Camera Lens Kit")
  "Computers & Laptops"    = @("Ultrabook 14 inch","Gaming Mouse RGB","Mechanical Keyboard","Laptop Cooling Pad","SSD External 1TB","USB Webcam Full HD","Laptop Backpack 15 inch","Wireless Mouse Silent","Monitor Stand Arm","HDMI Cable 2m","Laptop Sleeve Leather","Docking Station USB-C")
  "Fashion"                = @("Classic Cotton Tee","Slim Fit Chinos","Running Sneakers","Leather Belt","Hooded Sweatshirt","Denim Jacket","Casual Polo Shirt","Canvas Slip-ons","Wool Scarf","Sports Cap","Ankle Socks Pack","Crossbody Bag")
  "Home & Living"          = @("Ceramic Dinner Set","Memory Foam Pillow","LED Desk Lamp","Nonstick Frying Pan","Cotton Bedsheet Set","Wall Clock Modern","Storage Basket Set","Kitchen Knife Set","Bath Towel Pair","Indoor Plant Pot","Vacuum Flask 1L","Door Mat Anti-slip")
  "Beauty & Personal Care" = @("Vitamin C Serum","Matte Lipstick Set","Hair Dryer Ionic","Face Moisturizer SPF","Beard Trimmer Kit","Makeup Brush Pack","Body Lotion Shea","Shampoo + Conditioner","Nail Care Kit","Perfume EDT 50ml","Face Wash Gel","Hair Straightener")
  "Sports & Outdoors"      = @("Yoga Mat Non-slip","Dumbbell Pair 5kg","Football Size 5","Camping Tent 2-person","Hiking Backpack 40L","Jump Rope Speed","Sports Water Bottle","Resistance Band Set","Cycling Gloves","Badminton Racket","Trekking Poles","Gym Towel Pack")
  "Groceries"              = @("Basmati Rice 5kg","Olive Oil Extra Virgin","Green Tea Pack","Honey Pure 500g","Almonds Roasted 250g","Instant Oats 1kg","Spice Mix Assorted","Dates Medjool 400g","Peanut Butter Crunchy","Dark Chocolate Bar","Herbal Tea Sampler","Organic Honey Stick Pack")
  "Toys & Kids"            = @("Building Blocks 100pc","Remote Control Car","Soft Plush Bear","Educational Puzzle","Kids Scooter","Art Color Set","Board Game Family","Toddler Stack Rings","Kids Story Book Set","Bubble Blower Gun","Magic Drawing Board","Mini Drone Toy")
  "Automotive"             = @("Car Phone Holder","Tire Inflator Portable","Car Vacuum Mini","Jump Starter Pack","LED Headlight Bulbs","Seat Cover Set","Car Air Freshener Pack","Dash Cleaning Kit","Wiper Blade Pair","Engine Oil 4L","Car Organizer Trunk","OBD Scanner Basic")
}

# Variant styles by category family
function Get-VariantsForCategory([string]$catName, [decimal]$basePrice, [string]$skuBase) {
  $list = @()
  if ($catName -match "Fashion|Sports") {
    foreach ($size in @("S","M","L")) {
      $list += @{
        sku = "$skuBase-$size"
        price = [math]::Round($basePrice + (Get-Random -Minimum 0 -Maximum 300), 2)
        stockQty = Get-Random -Minimum 15 -Maximum 80
        lowStockThreshold = 5
        attributes = @(
          @{ attributeName = "Size"; attributeValue = $size },
          @{ attributeName = "Color"; attributeValue = @("Black","Navy","White")[(Get-Random -Maximum 3)] }
        )
      }
    }
  }
  elseif ($catName -match "Mobile|Computer|Electronics") {
    foreach ($opt in @(@{n="Storage";v="64GB"},@{n="Storage";v="128GB"},@{n="Color";v="Black"})) {
      # create 2 variants: standard + premium
    }
    $list += @{
      sku = "$skuBase-STD"
      price = $basePrice
      stockQty = Get-Random -Minimum 20 -Maximum 100
      lowStockThreshold = 5
      attributes = @(
        @{ attributeName = "Color"; attributeValue = "Black" },
        @{ attributeName = "Edition"; attributeValue = "Standard" }
      )
    }
    $list += @{
      sku = "$skuBase-PRO"
      price = [math]::Round($basePrice * 1.18, 2)
      stockQty = Get-Random -Minimum 10 -Maximum 50
      lowStockThreshold = 5
      attributes = @(
        @{ attributeName = "Color"; attributeValue = "Silver" },
        @{ attributeName = "Edition"; attributeValue = "Pro" }
      )
    }
  }
  else {
    $list += @{
      sku = "$skuBase-DEF"
      price = $basePrice
      stockQty = Get-Random -Minimum 25 -Maximum 120
      lowStockThreshold = 5
      attributes = @(
        @{ attributeName = "Pack"; attributeValue = "Standard" }
      )
    }
    $list += @{
      sku = "$skuBase-XL"
      price = [math]::Round($basePrice * 1.25, 2)
      stockQty = Get-Random -Minimum 10 -Maximum 60
      lowStockThreshold = 5
      attributes = @(
        @{ attributeName = "Pack"; attributeValue = "Family / XL" }
      )
    }
  }
  return $list
}

$brandByName = @{}
foreach ($b in $brands) { $brandByName[$b.name] = $b }
$brandCounts = @{}
foreach ($b in $brands) { $brandCounts[$b.id] = 0 }

$created = 0
$variantsOk = 0
$imagesOk = 0
$errors = New-Object System.Collections.Generic.List[string]
$imgIndex = 0
$tmpImgDir = Join-Path $env:TEMP ("eseller-seed-img-" + [guid]::NewGuid().ToString("n"))
New-Item -ItemType Directory -Path $tmpImgDir | Out-Null

function Resolve-BrandForCategory([string]$catName, [int]$index) {
  $preferred = $BrandPools[$catName]
  if (-not $preferred) { $preferred = @("Generic / Unbranded") }
  $tryName = $preferred[$index % $preferred.Count]
  if ($brandByName.ContainsKey($tryName)) { return $brandByName[$tryName] }
  if ($brandByName.ContainsKey("Generic / Unbranded")) { return $brandByName["Generic / Unbranded"] }
  return ($brands | Select-Object -First 1)
}

function Get-ImageFileForCategory([string]$catName, [int]$index) {
  $urls = $CategoryImageUrls[$catName]
  if ($urls -and $urls.Count -gt 0) {
    $url = $urls[$index % $urls.Count]
    $local = Join-Path $tmpImgDir ("cat-$index.jpg")
    & curl.exe -sS -L $url -o $local
    if ((Test-Path $local) -and (Get-Item $local).Length -gt 2000) { return $local }
  }
  if ($imagePool.Count -gt 0) {
    return $imagePool[$index % $imagePool.Count].FullName
  }
  return $null
}

foreach ($cat in $categories) {
  $names = $catalog[$cat.name]
  if (-not $names) {
    $names = 1..12 | ForEach-Object { "$($cat.name) Product $_" }
  }
  Write-Host "`n=== $($cat.name) ($($names.Count) products) ==="

  $i = 0
  foreach ($name in $names) {
    $i++
    $brand = Resolve-BrandForCategory -catName $cat.name -index ($i - 1)
    $brandCounts[$brand.id] = [int]$brandCounts[$brand.id] + 1

    # USD-ish demo prices (not inflated PKR-style numbers)
    $price = switch -Regex ($cat.name) {
      "Mobile|Computer" { Get-Random -Minimum 49 -Maximum 899 }
      "Electronics" { Get-Random -Minimum 19 -Maximum 499 }
      "Fashion|Sports" { Get-Random -Minimum 12 -Maximum 149 }
      "Automotive" { Get-Random -Minimum 15 -Maximum 199 }
      "Groceries|Beauty" { Get-Random -Minimum 5 -Maximum 59 }
      default { Get-Random -Minimum 9 -Maximum 129 }
    }

    $desc = "Premium $($cat.name) item - $name by $($brand.name). Ready for marketplace sellers to add to store."
    $body = @{
      shopId = $null
      categoryId = $cat.id
      brandId = $brand.id
      name = "$($brand.name) $name"
      description = $desc
      basePrice = [decimal]$price
      seoTitle = "$($brand.name) $name | Eseller"
      seoDescription = $desc
    }

    $res = Invoke-ApiJson -Method POST -Url "$Api/products" -Headers $auth -Body $body
    if ($res.Code -notin 200,201) {
      $errors.Add("CREATE $($cat.name)/$name -> $($res.Code) $($res.Body.Substring(0,[Math]::Min(120,$res.Body.Length)))") | Out-Null
      continue
    }
    $productId = ($res.Body | ConvertFrom-Json).productId
    if (-not $productId) {
      $errors.Add("CREATE no productId for $name") | Out-Null
      continue
    }
    $created++

    # Category-matched image (Unsplash), not a random local dump
    $imgPath = Get-ImageFileForCategory -catName $cat.name -index $imgIndex
    $imgIndex++
    if ($imgPath) {
      $up = Invoke-ApiUpload -Url "$Api/products/$productId/images" -Headers $auth -FilePath $imgPath
      if ($up.Code -in 200,201) { $imagesOk++ }
      else { $errors.Add("IMG $productId -> $($up.Code)") | Out-Null }
    }
    else { $errors.Add("IMG $productId -> no image source") | Out-Null }

    # Variants
    $skuBase = ("ES-" + $cat.slug.Substring(0,[Math]::Min(4,$cat.slug.Length)) + "-" + $created).ToUpper()
    $vars = Get-VariantsForCategory -catName $cat.name -basePrice ([decimal]$price) -skuBase $skuBase
    foreach ($v in $vars) {
      $vres = Invoke-ApiJson -Method POST -Url "$Api/products/$productId/variants" -Headers $auth -Body $v
      if ($vres.Code -in 200,201) { $variantsOk++ }
      else { $errors.Add("VAR $productId $($v.sku) -> $($vres.Code)") | Out-Null }
    }

    if ($i % 4 -eq 0) { Write-Host "  ... $i/$($names.Count) (total created=$created)" }
  }
}

Write-Host "`n========== SUMMARY =========="
Write-Host "Products created: $created"
Write-Host "Variants created: $variantsOk"
Write-Host "Images uploaded:  $imagesOk"
Write-Host "Brand counts:"
foreach ($b in $brands) { Write-Host ("  {0,-20} {1}" -f $b.name, $brandCounts[$b.id]) }
if ($errors.Count -gt 0) {
  Write-Host "`nErrors ($($errors.Count)):"
  $errors | Select-Object -First 25 | ForEach-Object { Write-Host "  $_" }
}

$probe = Invoke-ApiJson -Method GET -Url "$Api/products?pageNumber=1&pageSize=1"
$probeObj = $probe.Body | ConvertFrom-Json
Write-Host "`nLive totalCount: $($probeObj.totalCount)"
Remove-Item $tmpImgDir -Recurse -Force -ErrorAction SilentlyContinue

