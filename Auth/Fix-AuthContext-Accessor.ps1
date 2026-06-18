# ============================================================================
#  Fix-AuthContext-Accessor.ps1
#  The CurrentView getter was written as an expression-bodied accessor
#  (get => _currentView;), which is C# 7. This project compiles at C# 6, so
#  convert it to block form: get { return _currentView; }. ASCII-only.
#  Idempotent, timestamped .bak, UTF-8 no-BOM. Rebuild in VS afterwards.
# ============================================================================

$ErrorActionPreference = 'Stop'
$path = 'C:\Users\Admin\Documents\ATMML\Auth\AuthContext.cs'

if (-not (Test-Path $path)) { Write-Host "ABORT: file not found: $path" -ForegroundColor Red; exit 1 }
$content = [System.IO.File]::ReadAllText($path)

if ($content.Contains('get { return _currentView; }')) {
    Write-Host "Already fixed (block accessor present). No changes made." -ForegroundColor Yellow
    exit 0
}

$old = 'get => _currentView;'
$new = 'get { return _currentView; }'

$count = ([regex]::Matches($content, [regex]::Escape($old))).Count
if ($count -ne 1) {
    Write-Host "ABORT: anchor 'get => _currentView;' matched $count times (expected 1). No changes made." -ForegroundColor Red
    exit 1
}

$content = $content.Replace($old, $new)

$bak = "$path.$(Get-Date -Format yyyyMMdd_HHmmss).bak"
Copy-Item $path $bak -Force
[System.IO.File]::WriteAllText($path, $content, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Fixed CurrentView getter. Backup: $bak" -ForegroundColor Green

Write-Host "`n----- verify -----" -ForegroundColor Cyan
Select-String -Path $path -Pattern 'get \{ return _currentView; \}','public AppView CurrentView' |
    ForEach-Object { "{0}: {1}" -f $_.LineNumber, $_.Line.Trim() }
Write-Host "`nNow REBUILD in Visual Studio." -ForegroundColor Cyan
