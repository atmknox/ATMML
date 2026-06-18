# ============================================================================
#  Patch-AODSetupButtonHover.ps1
#  Gives the AOD "ADD | MODIFY PAGES" setup buttons (Add New Page / Close /
#  Delete) a custom ControlTemplate so mouse-over shows a dull blue instead
#  of WPF's default light-blue Aero highlight.
#
#  Single point of change: makeSetupButton() in MarketMonitor.AODPages.cs
#  Idempotent, timestamped .bak, UTF-8 no-BOM, aborts cleanly if anchors move.
#  Requires a Visual Studio REBUILD afterwards (.cs change).
# ============================================================================

$ErrorActionPreference = 'Stop'

$path    = 'C:\Users\Admin\Documents\ATMML\MarketMonitor.AODPages.cs'
$HoverHex = '#34557A'    # <-- dull blue. Use '#243A4F' for the darker option.

if (-not (Test-Path $path)) { Write-Host "ABORT: file not found: $path" -ForegroundColor Red; exit 1 }

$content = [System.IO.File]::ReadAllText($path)

# ---- Idempotency guard ----------------------------------------------------
if ($content.Contains('GetSetupButtonTemplate')) {
    Write-Host "Already patched (GetSetupButtonTemplate present). No changes made." -ForegroundColor Yellow
    exit 0
}

# ---- Anchor A: add template assignment inside makeSetupButton --------------
$rxA = [regex]'(b\.Cursor = Cursors\.Hand;)(\r?\n)([ \t]*)(return b;)'
$mA  = $rxA.Matches($content)
if ($mA.Count -ne 1) {
    Write-Host "ABORT: makeSetupButton anchor matched $($mA.Count) times (expected 1). No changes made." -ForegroundColor Red
    exit 1
}

# ---- Anchor B: insert helper method just before drawPageSetup -------------
$rxB = [regex]'([ \t]*)(private void drawPageSetup\(\))'
$mB  = $rxB.Matches($content)
if ($mB.Count -lt 1) {
    Write-Host "ABORT: drawPageSetup anchor not found. No changes made." -ForegroundColor Red
    exit 1
}

# ---- Helper method text (placeholder __HOVER__ swapped for the chosen hex) -
$helper = @'
        private System.Windows.Controls.ControlTemplate _setupBtnTemplate;
        private System.Windows.Controls.ControlTemplate GetSetupButtonTemplate()
        {
            if (_setupBtnTemplate == null)
            {
                const string xaml =
                    "<ControlTemplate TargetType='Button' " +
                    "xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
                    "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" +
                      "<Border x:Name='bd' Background='{TemplateBinding Background}' " +
                              "BorderBrush='{TemplateBinding BorderBrush}' " +
                              "BorderThickness='{TemplateBinding BorderThickness}'>" +
                        "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>" +
                      "</Border>" +
                      "<ControlTemplate.Triggers>" +
                        "<Trigger Property='IsMouseOver' Value='True'>" +
                          "<Setter TargetName='bd' Property='Background' Value='__HOVER__'/>" +
                        "</Trigger>" +
                      "</ControlTemplate.Triggers>" +
                    "</ControlTemplate>";
                _setupBtnTemplate = (System.Windows.Controls.ControlTemplate)
                    System.Windows.Markup.XamlReader.Parse(xaml);
            }
            return _setupBtnTemplate;
        }
'@
$helper = ($helper -replace "`r`n","`n") -replace "`n","`r`n"
$helper = $helper.Replace('__HOVER__', $HoverHex)
$helper = $helper.TrimEnd() + "`r`n`r`n"

# ---- Apply both edits (MatchEvaluators avoid $-substitution pitfalls) ------
$content = $rxA.Replace($content, {
    param($m)
    $m.Groups[1].Value + $m.Groups[2].Value + $m.Groups[3].Value +
    'b.Template = GetSetupButtonTemplate();' +
    $m.Groups[2].Value + $m.Groups[3].Value + $m.Groups[4].Value
}, 1)

$content = $rxB.Replace($content, {
    param($m)
    $helper + $m.Groups[1].Value + $m.Groups[2].Value
}, 1)

# ---- Backup then write (UTF-8 no-BOM) -------------------------------------
$bak = "$path.$(Get-Date -Format yyyyMMdd_HHmmss).bak"
Copy-Item $path $bak -Force
[System.IO.File]::WriteAllText($path, $content, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "Patched. Backup: $bak" -ForegroundColor Green

# ---- Verify ---------------------------------------------------------------
Write-Host "`n----- verify -----" -ForegroundColor Cyan
Select-String -Path $path -Pattern 'b\.Template = GetSetupButtonTemplate\(\);','GetSetupButtonTemplate\(\)','IsMouseOver',[regex]::Escape($HoverHex) |
    ForEach-Object { "{0}: {1}" -f $_.LineNumber, $_.Line.Trim() }
Write-Host "`nNow REBUILD in Visual Studio, then reopen the ADD | MODIFY PAGES panel." -ForegroundColor Cyan
