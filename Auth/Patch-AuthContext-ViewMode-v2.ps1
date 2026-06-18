# ============================================================================
#  Patch-AuthContext-ViewMode-v2.ps1
#  Repairs the v1 corruption (PowerShell ate keywords like $L1public/$L2get):
#  restores the pristine AuthContext.cs from the v1 backup, then re-applies the
#  AppView enum + CurrentView/ViewChanged/CanUseView members CORRECTLY using
#  ${L1} delimited interpolation and a block-form getter (C# 6 safe). ASCII-only.
#  Idempotent, backs up the corrupted file, UTF-8 no-BOM. Rebuild in VS after.
# ============================================================================

$ErrorActionPreference = 'Stop'
$path = 'C:\Users\Admin\Documents\ATMML\Auth\AuthContext.cs'
$dir  = Split-Path $path

if (-not (Test-Path $path)) { Write-Host "ABORT: file not found: $path" -ForegroundColor Red; exit 1 }
$content = [System.IO.File]::ReadAllText($path)

if ($content.Contains('public bool CanUseView(AppView view) => true;')) {
    Write-Host "Already correctly patched. No changes made." -ForegroundColor Yellow
    exit 0
}

# Find a pristine pre-patch backup (no view-mode members) to restore from.
$baks = Get-ChildItem (Join-Path $dir 'AuthContext.cs.*.bak') -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending
$pristine = $null
foreach ($b in $baks) {
    $bc = [System.IO.File]::ReadAllText($b.FullName)
    if (($bc -notmatch 'enum AppView') -and ($bc -notmatch 'CurrentView')) { $pristine = $b; break }
}
if ($null -eq $pristine) {
    Write-Host "ABORT: no pristine AuthContext.cs backup found to restore from." -ForegroundColor Red
    Write-Host "Restore the original AuthContext.cs by hand, then re-run this." -ForegroundColor Red
    exit 1
}

$corrupt = "$path.corrupt.$(Get-Date -Format yyyyMMdd_HHmmss).bak"
Copy-Item $path $corrupt -Force
Copy-Item $pristine.FullName $path -Force
Write-Host ("Restored pristine from {0}; corrupted copy kept as {1}" -f $pristine.Name, (Split-Path $corrupt -Leaf)) -ForegroundColor Green
$content = [System.IO.File]::ReadAllText($path)

$mult = [System.Text.RegularExpressions.RegexOptions]::Multiline

# ---- Edit 1: enum before class (concatenation - no interpolation) ----------
$rx1 = New-Object System.Text.RegularExpressions.Regex('^([ \t]*)(public class AuthContext : INotifyPropertyChanged)', $mult)
if ($rx1.Matches($content).Count -ne 1) { Write-Host "ABORT: class anchor not unique." -ForegroundColor Red; exit 1 }
$content = $rx1.Replace($content, {
    param($m)
    $g1 = $m.Groups[1].Value
    $g1 + 'public enum AppView { All, Pro }' + "`r`n`r`n" + $g1 + $m.Groups[2].Value
}, 1)

# ---- Edit 2: view members after CanManageUsers (${...} delimited) ----------
$rx2 = New-Object System.Text.RegularExpressions.Regex('^([ \t]*)(public bool CanManageUsers => IsAdmin;)', $mult)
if ($rx2.Matches($content).Count -ne 1) { Write-Host "ABORT: CanManageUsers anchor not unique." -ForegroundColor Red; exit 1 }
$content = $rx2.Replace($content, {
    param($m)
    $L1 = $m.Groups[1].Value
    $unit = if ($L1 -match "`t") { "`t" } else { "    " }
    $L2 = $L1 + $unit
    $L3 = $L2 + $unit
    $blk = "`r`n`r`n" +
        "${L1}// View mode (All / Pro)`r`n" +
        "${L1}private AppView _currentView = AppView.All;`r`n" +
        "${L1}public AppView CurrentView`r`n" +
        "${L1}{`r`n" +
        "${L2}get { return _currentView; }`r`n" +
        "${L2}set`r`n" +
        "${L2}{`r`n" +
        "${L3}if (_currentView == value) return;`r`n" +
        "${L3}_currentView = value;`r`n" +
        "${L3}ViewChanged?.Invoke(this, System.EventArgs.Empty);`r`n" +
        "${L3}PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentView)));`r`n" +
        "${L2}}`r`n" +
        "${L1}}`r`n" +
        "`r`n" +
        "${L1}// Fired when CurrentView changes so each view re-applies its nav layout.`r`n" +
        "${L1}public event System.EventHandler ViewChanged;`r`n" +
        "`r`n" +
        "${L1}// Stub: both views allowed to everyone for now. Server entitlements drive this later.`r`n" +
        "${L1}public bool CanUseView(AppView view) => true;"
    $m.Groups[1].Value + $m.Groups[2].Value + $blk
}, 1)

[System.IO.File]::WriteAllText($path, $content, (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Re-applied view-mode members correctly." -ForegroundColor Green

Write-Host "`n----- verify -----" -ForegroundColor Cyan
Select-String -Path $path -Pattern 'enum AppView','public AppView CurrentView','get \{ return _currentView; \}','public event System.EventHandler ViewChanged','public bool CanUseView' |
    ForEach-Object { "{0}: {1}" -f $_.LineNumber, $_.Line.Trim() }

$c2 = [System.IO.File]::ReadAllText($path)
$o = ([regex]::Matches($c2,'\{')).Count
$cl = ([regex]::Matches($c2,'\}')).Count
Write-Host ("`nbrace check  open={0} close={1} (should match)" -f $o, $cl) -ForegroundColor Cyan
Write-Host "Now REBUILD in Visual Studio." -ForegroundColor Cyan
