# ============================================================================
#  Массовая скачка текстур для «Двора» с ambientCG (лицензия CC0)
# ============================================================================
#
#  Лежит в Assets\_Dvor\ — Unity такие файлы игнорирует, проекту не мешает.
#
#  КАК ЗАПУСТИТЬ
#    1. Правый клик на этом файле → "Выполнить с помощью PowerShell"
#       Если Windows ругается на политику запуска, открой PowerShell и введи:
#         powershell -ExecutionPolicy Bypass -File "Скачать текстуры.ps1"
#    2. Подожди 2-5 минут. Скрипт скачает ~15 материалов по 2K.
#    3. Вернись в Unity, дождись импорта, нажми Play.
#
#  Скрипт ходит в открытый API ambientCG, качает ZIP с картами 2K-JPG,
#  распаковывает, берёт Color и NormalGL и переименовывает под имена,
#  которые ждёт игра. Ничего платного и никакого Patreon не нужно.
# ============================================================================

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Папка назначения — рядом со скриптом (скрипт лежит в Assets\_Dvor)
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dest = Join-Path $root "Resources\Textures"
$tmp  = Join-Path $env:TEMP "dvor_tex"

New-Item -ItemType Directory -Force -Path $dest | Out-Null
New-Item -ItemType Directory -Force -Path $tmp  | Out-Null

# имя в игре  ->  что искать на ambientCG
$wanted = [ordered]@{
    "panel_a"    = "concrete plates"
    "panel_b"    = "concrete wall"
    "panel_end"  = "concrete rough"
    "concrete"   = "concrete smooth"
    "concrete_d" = "concrete damaged"
    "asphalt"    = "asphalt"
    "grass"      = "grass"
    "dirt"       = "ground dirt"
    "brick"      = "bricks"
    "garage"     = "corrugated metal"
    "garage_2"   = "metal painted"
    "rust"       = "metal rust"
    "wood"       = "wood planks"
    "wood_blue"  = "wood painted"
    "foliage"    = "leaves"
}

function Get-Asset([string]$query) {
    # Открытый API: ищем материалы по ключевым словам, берём первый с 2K-JPG
    $url = "https://ambientcg.com/api/v2/full_json?type=Material&q=$([uri]::EscapeDataString($query))" +
           "&limit=8&include=downloadData&sort=popular"
    $json = Invoke-RestMethod -Uri $url -TimeoutSec 60

    foreach ($asset in $json.foundAssets) {
        $zip = $asset.downloadFolders.default.downloadFiletypeCategories.zip.downloads |
               Where-Object { $_.attribute -eq "2K-JPG" } | Select-Object -First 1
        if ($zip) {
            return [pscustomobject]@{ Id = $asset.assetId; Url = $zip.downloadLink }
        }
    }
    return $null
}

$ok = 0; $fail = @()

foreach ($name in $wanted.Keys) {
    $query = $wanted[$name]
    Write-Host ""
    Write-Host "[$name] ищу «$query»..." -ForegroundColor Cyan

    try {
        $asset = Get-Asset $query
        if (-not $asset) { throw "ничего не нашлось с картами 2K-JPG" }

        Write-Host "   нашёл: $($asset.Id)" -ForegroundColor DarkGray

        $zipPath = Join-Path $tmp "$name.zip"
        $outDir  = Join-Path $tmp $name
        Invoke-WebRequest -Uri $asset.Url -OutFile $zipPath -TimeoutSec 300

        if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
        Expand-Archive -Path $zipPath -DestinationPath $outDir -Force

        # Color обязателен, NormalGL — если есть
        $color = Get-ChildItem $outDir -Recurse -Include *_Color.jpg, *_Color.png | Select-Object -First 1
        if (-not $color) { throw "в архиве нет карты Color" }
        Copy-Item $color.FullName (Join-Path $dest "$name`_color$($color.Extension)") -Force

        $normal = Get-ChildItem $outDir -Recurse -Include *_NormalGL.jpg, *_NormalGL.png | Select-Object -First 1
        if ($normal) {
            Copy-Item $normal.FullName (Join-Path $dest "$name`_normal$($normal.Extension)") -Force
            Write-Host "   готово: цвет + нормали" -ForegroundColor Green
        } else {
            Write-Host "   готово: только цвет" -ForegroundColor Yellow
        }
        $ok++
    }
    catch {
        Write-Host "   не вышло: $($_.Exception.Message)" -ForegroundColor Red
        $fail += $name
    }
}

Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Скачано материалов: $ok из $($wanted.Count)"
if ($fail.Count -gt 0) {
    Write-Host "Не получилось: $($fail -join ', ')" -ForegroundColor Yellow
    Write-Host "Их можно докачать руками с ambientcg.com и положить в:"
    Write-Host "  $dest"
    Write-Host "с именами вида  имя_color.jpg  и  имя_normal.jpg"
}
Write-Host ""
Write-Host "Теперь вернись в Unity, дождись импорта и нажми Play."
Write-Host "В консоли должны появиться строки: [Двор] Материал «...» взят из файла"
Write-Host "============================================" -ForegroundColor Cyan
Read-Host "Нажми Enter чтобы закрыть"
