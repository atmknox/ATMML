# ============================================================================
#  Patch-Alerts-ViewToggle.ps1
#  Wires the ALL | PRO view toggle into the Alerts view (reference view #1):
#    - Alerts.xaml      : names the row-1 nav StackPanel x:Name="MainNavRow"
#    - Alerts.xaml.cs   : Loaded/Unloaded subscribe to AuthContext.ViewChanged,
#                         ApplyNavView() collapses MainNavRow in PRO,
#                         and a "View > All / Pro" submenu in BtnSettings_Click.
#  ASCII-only. Per-file backups, idempotent, UTF-8 no-BOM. Rebuild in VS after.
# ============================================================================

$ErrorActionPreference = 'Stop'
$cs   = 'C:\Users\Admin\Documents\ATMML\Alerts.xaml.cs'
$xaml = 'C:\Users\Admin\Documents\ATMML\Alerts.xaml'
$mult = [System.Text.RegularExpressions.RegexOptions]::Multiline

function Reindent([string]$tmpl, [string]$base) {
    (($tmpl -split "`r?`n") | ForEach-Object { if ($_ -eq '') { '' } else { $base + $_ } }) -join "`r`n"
}

# ---- helper methods inserted before BtnSettings_Click -----------------------
$tmplMethods = @'
private void Alerts_NavViewLoaded(object sender, System.Windows.RoutedEventArgs e)
{
    ATMML.Auth.AuthContext.Current.ViewChanged -= OnNavViewChanged;
    ATMML.Auth.AuthContext.Current.ViewChanged += OnNavViewChanged;
    ApplyNavView();
}

private void Alerts_NavViewUnloaded(object sender, System.Windows.RoutedEventArgs e)
{
    ATMML.Auth.AuthContext.Current.ViewChanged -= OnNavViewChanged;
}

private void OnNavViewChanged(object sender, System.EventArgs e)
{
    ApplyNavView();
}

private void ApplyNavView()
{
    if (MainNavRow != null)
        MainNavRow.Visibility =
            (ATMML.Auth.AuthContext.Current.CurrentView == ATMML.Auth.AppView.Pro)
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
}
'@

# ---- View submenu inserted after menu.Items.Add(changePassword); ------------
$tmplSubmenu = @'
var viewMenu = new System.Windows.Controls.MenuItem { Header = "View" };
var viewAll = new System.Windows.Controls.MenuItem { Header = "All", IsCheckable = true, IsChecked = ATMML.Auth.AuthContext.Current.CurrentView == ATMML.Auth.AppView.All };
var viewPro = new System.Windows.Controls.MenuItem { Header = "Pro", IsCheckable = true, IsChecked = ATMML.Auth.AuthContext.Current.CurrentView == ATMML.Auth.AppView.Pro };
viewAll.Click += (_, _) => { ATMML.Auth.AuthContext.Current.CurrentView = ATMML.Auth.AppView.All; };
viewPro.Click += (_, _) => { ATMML.Auth.AuthContext.Current.CurrentView = ATMML.Auth.AppView.Pro; };
viewMenu.Items.Add(viewAll);
viewMenu.Items.Add(viewPro);
menu.Items.Add(viewMenu);
'@

# =====================  Alerts.xaml.cs  =====================
if (-not (Test-Path $cs)) { Write-Host "ABORT: not found: $cs" -ForegroundColor Red; exit 1 }
$csText = [System.IO.File]::ReadAllText($cs)

if ($csText.Contains('ApplyNavView')) {
    Write-Host "Alerts.xaml.cs already wired (ApplyNavView present). Skipping code-behind." -ForegroundColor Yellow
} else {
    # Edit A: after InitializeComponent(); add Loaded/Unloaded subscription
    $rxA = New-Object System.Text.RegularExpressions.Regex('^([ \t]*)(InitializeComponent\(\);)', $mult)
    if ($rxA.Matches($csText).Count -ne 1) { Write-Host "ABORT: InitializeComponent anchor not unique." -ForegroundColor Red; exit 1 }
    $csText = $rxA.Replace($csText, {
        param($m)
        $i = $m.Groups[1].Value
        $m.Groups[1].Value + $m.Groups[2].Value + "`r`n" + $i + 'this.Loaded += Alerts_NavViewLoaded;' + "`r`n" + $i + 'this.Unloaded += Alerts_NavViewUnloaded;'
    }, 1)

    # Edit B: insert helper methods before BtnSettings_Click
    $rxB = New-Object System.Text.RegularExpressions.Regex('^([ \t]*)(private void BtnSettings_Click\(object sender, System\.Windows\.RoutedEventArgs e\))', $mult)
    if ($rxB.Matches($csText).Count -ne 1) { Write-Host "ABORT: BtnSettings_Click anchor not unique." -ForegroundColor Red; exit 1 }
    $csText = $rxB.Replace($csText, {
        param($m)
        (Reindent $tmplMethods $m.Groups[1].Value) + "`r`n`r`n" + $m.Groups[1].Value + $m.Groups[2].Value
    }, 1)

    # Edit C: insert View submenu after changePassword is added
    $rxC = New-Object System.Text.RegularExpressions.Regex('^([ \t]*)(menu\.Items\.Add\(changePassword\);)', $mult)
    if ($rxC.Matches($csText).Count -ne 1) { Write-Host "ABORT: changePassword anchor not unique." -ForegroundColor Red; exit 1 }
    $csText = $rxC.Replace($csText, {
        param($m)
        $m.Groups[1].Value + $m.Groups[2].Value + "`r`n`r`n" + (Reindent $tmplSubmenu $m.Groups[1].Value)
    }, 1)

    $bak = "$cs.$(Get-Date -Format yyyyMMdd_HHmmss).bak"
    Copy-Item $cs $bak -Force
    [System.IO.File]::WriteAllText($cs, $csText, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "Patched Alerts.xaml.cs. Backup: $bak" -ForegroundColor Green
}

# =====================  Alerts.xaml  =====================
if (-not (Test-Path $xaml)) { Write-Host "ABORT: not found: $xaml" -ForegroundColor Red; exit 1 }
$xText = [System.IO.File]::ReadAllText($xaml)

if ($xText.Contains('x:Name="MainNavRow"')) {
    Write-Host "Alerts.xaml already named MainNavRow. Skipping XAML." -ForegroundColor Yellow
} else {
    $rxD = New-Object System.Text.RegularExpressions.Regex('(<StackPanel )(Grid\.Row="1" Margin="145,0,0,10" Grid\.RowSpan="3" Grid\.ColumnSpan="11")')
    if ($rxD.Matches($xText).Count -ne 1) { Write-Host "ABORT: row-1 StackPanel anchor matched $($rxD.Matches($xText).Count) times (expected 1)." -ForegroundColor Red; exit 1 }
    $xText = $rxD.Replace($xText, {
        param($m)
        $m.Groups[1].Value + 'x:Name="MainNavRow" ' + $m.Groups[2].Value
    }, 1)
    $bakx = "$xaml.$(Get-Date -Format yyyyMMdd_HHmmss).bak"
    Copy-Item $xaml $bakx -Force
    [System.IO.File]::WriteAllText($xaml, $xText, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "Patched Alerts.xaml. Backup: $bakx" -ForegroundColor Green
}

Write-Host "`n----- verify (cs) -----" -ForegroundColor Cyan
Select-String -Path $cs -Pattern 'this.Loaded += Alerts_NavViewLoaded','private void ApplyNavView','Header = "View"','CurrentView = ATMML.Auth.AppView.Pro' |
    ForEach-Object { "{0}: {1}" -f $_.LineNumber, $_.Line.Trim() }
Write-Host "----- verify (xaml) -----" -ForegroundColor Cyan
Select-String -Path $xaml -Pattern 'x:Name="MainNavRow"' |
    ForEach-Object { "{0}: {1}" -f $_.LineNumber, $_.Line.Trim() }
Write-Host "`nNow REBUILD in Visual Studio, open Alerts, click the gear -> View -> Pro." -ForegroundColor Cyan
