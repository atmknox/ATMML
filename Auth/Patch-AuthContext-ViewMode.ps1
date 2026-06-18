# ============================================================================
#  Patch-AuthContext-ViewMode.ps1
#  Adds view-mode state to AuthContext: AppView { All, Pro } enum, a CurrentView
#  property whose setter raises a ViewChanged event (and PropertyChanged), and a
#  CanUseView stub (always true for now; server entitlements drive it later).
#
#  Foundation for the gear ALL | PRO toggle. Idempotent, timestamped .bak,
#  UTF-8 no-BOM. Requires a Visual Studio REBUILD afterwards.
# ============================================================================

$ErrorActionPreference = 'Stop'
$path = 'C:\Users\Admin\Documents\ATMML\Auth\AuthContext.cs'

if (-not (Test-Path $path)) { Write-Host "ABORT: file not found: $path" -ForegroundColor Red; exit 1 }
$content = [System.IO.File]::ReadAllText($path)

if ($content -match 'enum AppView' -or $content -match 'CurrentView') {
    Write-Host "Already patched (AppView/CurrentView present). No changes made." -ForegroundColor Yellow
    exit 0
}

$mult = [System.Text.RegularExpressions.RegexOptions]::Multiline

# ---- Edit 1: insert enum AppView just before the class declaration ----------
$rx1 = New-Object System.Text.RegularExpressions.Regex('^([ \t]*)(public class AuthContext : INotifyPropertyChanged)', $mult)
if ($rx1.Matches($content).Count -ne 1) {
    Write-Host "ABORT: class-declaration anchor matched $($rx1.Matches($content).Count) times (expected 1). No changes made." -ForegroundColor Red
    exit 1
}
$content = $rx1.Replace($content, {
    param($m)
    $g1 = $m.Groups[1].Value
    $g1 + "public enum AppView { All, Pro }`r`n`r`n" + $g1 + $m.Groups[2].Value
}, 1)

# ---- Edit 2: insert view members after CanManageUsers ----------------------
$rx2 = New-Object System.Text.RegularExpressions.Regex('^([ \t]*)(public bool CanManageUsers => IsAdmin;)', $mult)
if ($rx2.Matches($content).Count -ne 1) {
    Write-Host "ABORT: CanManageUsers anchor matched $($rx2.Matches($content).Count) times (expected 1). No changes made." -ForegroundColor Red
    exit 1
}
$content = $rx2.Replace($content, {
    param($m)
    $L1 = $m.Groups[1].Value
    $unit = if ($L1 -match "`t") { "`t" } else { "    " }
    $L2 = $L1 + $unit
    $L3 = $L2 + $unit
    $blk = "`r`n`r`n" +
        "$L1// View mode (All / Pro)`r`n" +
        "$L1private AppView _currentView = AppView.All;`r`n" +
        "$L1public AppView CurrentView`r`n" +
        "$L1{`r`n" +
        "$L2get => _currentView;`r`n" +
        "$L2set`r`n" +
        "$L2{`r`n" +
        "$L3if (_currentView == value) return;`r`n" +
        "$L3_currentView = value;`r`n" +
        "$L3ViewChanged?.Invoke(this, System.EventArgs.Empty);`r`n" +
        "$L3PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentView)));`r`n" +
        "$L2}`r`n" +
        "$L1}`r`n" +
        "`r`n" +
        "$L1// Fired when CurrentView changes so each view re-applies its nav layout.`r`n" +
        "$L1public event System.EventHandler ViewChanged;`r`n" +
        "`r`n" +
        "$L1// Stub: both views allowed to everyone for now. Server entitlements drive this later.`r`n" +
        "$L1public bool CanUseView(AppView view) => true;"
    $m.Groups[1].Value + $m.Groups[2].Value + $blk
}, 1)

$bak = "$path.$(Get-Date -Format yyyyMMdd_HHmmss).bak"
Copy-Item $path $bak -Force
[System.IO.File]::WriteAllText($path, $content, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Patched AuthContext.cs. Backup: $bak" -ForegroundColor Green

Write-Host "`n----- verify -----" -ForegroundColor Cyan
Select-String -Path $path -Pattern 'enum AppView','public AppView CurrentView','event System.EventHandler ViewChanged','CanUseView' |
    ForEach-Object { "{0}: {1}" -f $_.LineNumber, $_.Line.Trim() }
Write-Host "`nNow REBUILD in Visual Studio. (No visible change yet - this is the state foundation.)" -ForegroundColor Cyan
