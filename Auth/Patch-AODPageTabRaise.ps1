# ============================================================================
#  Patch-AODPageTabRaise.ps1
#  Raises the AOD page tabs (Page 1 / Page 2 ...) drawn by drawAodPageList so
#  they line up vertically with the "ADD | MODIFY PAGES" label in the toolbar.
#  Applies a negative top margin to the per-tab Label in MarketMonitor.AODPages.cs.
#
#  Tunable, idempotent, re-runnable (regex matches any current top value),
#  timestamped .bak, UTF-8 no-BOM. Requires a Visual Studio REBUILD afterwards.
# ============================================================================

$ErrorActionPreference = 'Stop'

$path    = 'C:\Users\Admin\Documents\ATMML\MarketMonitor.AODPages.cs'
$RaisePx = 5     # <-- raise the tabs this many pixels. 0 restores original.

if (-not (Test-Path $path)) { Write-Host "ABORT: file not found: $path" -ForegroundColor Red; exit 1 }

$content = [System.IO.File]::ReadAllText($path)

# Match the page-tab label margin: new Thickness(0, <top>, 5, 0)  (any current top)
$rx = [regex]'(l1\.Margin = new Thickness\(0, )(-?\d+)(, 5, 0\);)'
$m  = $rx.Matches($content)
if ($m.Count -ne 1) {
    Write-Host "ABORT: page-tab margin anchor matched $($m.Count) times (expected 1). No changes made." -ForegroundColor Red
    exit 1
}

$current = $m[0].Groups[2].Value
$newTop  = ([int](-1 * $RaisePx)).ToString()

if ($current -eq $newTop) {
    Write-Host "Already set to top=$newTop. No changes made." -ForegroundColor Yellow
    exit 0
}

$content = $rx.Replace($content, {
    param($mm)
    $mm.Groups[1].Value + $newTop + $mm.Groups[3].Value
}, 1)

$bak = "$path.$(Get-Date -Format yyyyMMdd_HHmmss).bak"
Copy-Item $path $bak -Force
[System.IO.File]::WriteAllText($path, $content, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "Patched: page-tab top margin $current -> $newTop. Backup: $bak" -ForegroundColor Green

Write-Host "`n----- verify -----" -ForegroundColor Cyan
Select-String -Path $path -Pattern 'l1\.Margin = new Thickness' |
    ForEach-Object { "{0}: {1}" -f $_.LineNumber, $_.Line.Trim() }
Write-Host "`nNow REBUILD in Visual Studio, then reopen the AOD page to check tab alignment." -ForegroundColor Cyan
Write-Host "If 5px isn't perfect, change `$RaisePx and just re-run (no need to restore the .bak)." -ForegroundColor DarkGray
