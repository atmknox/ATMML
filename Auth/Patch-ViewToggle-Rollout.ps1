# ============================================================================
#  Patch-ViewToggle-Rollout.ps1
#  Rolls the ALL | PRO view toggle into Charts, MarketMonitor, and Timing
#  (matching the Alerts wiring). Per view:
#    XAML : name the row-1 nav StackPanel x:Name="MainNavRow"
#    code : _navWired-guarded WireNavView() after every InitializeComponent(),
#           NavView_Loaded/Unloaded subscribe to AuthContext.ViewChanged,
#           ApplyNavView() collapses MainNavRow in PRO,
#           and a "View > All / Pro" submenu in BtnSettings_Click.
#  ASCII-only. Per-file backups, idempotent, UTF-8 no-BOM. Rebuild in VS after.
# ============================================================================

$ErrorActionPreference = 'Stop'
$mult = [System.Text.RegularExpressions.RegexOptions]::Multiline

function Reindent([string]$tmpl, [string]$base) {
    (($tmpl -split "`r?`n") | ForEach-Object { if ($_ -eq '') { '' } else { $base + $_ } }) -join "`r`n"
}

$tmplMethods = @'
private bool _navWired;

private void WireNavView()
{
    if (_navWired) return;
    _navWired = true;
    this.Loaded += NavView_Loaded;
    this.Unloaded += NavView_Unloaded;
}

private void NavView_Loaded(object sender, System.Windows.RoutedEventArgs e)
{
    ATMML.Auth.AuthContext.Current.ViewChanged -= OnNavViewChanged;
    ATMML.Auth.AuthContext.Current.ViewChanged += OnNavViewChanged;
    ApplyNavView();
}

private void NavView_Unloaded(object sender, System.Windows.RoutedEventArgs e)
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

$root = 'C:\Users\Admin\Documents\ATMML'
$views = @(
  @{ name='Charts';        cb=(Join-Path $root 'Charts.xaml.cs');        xaml=(Join-Path $root 'Charts.xaml');        panel='Grid\.Row="1" Margin="145,0,0,10" Grid\.RowSpan="3" Grid\.ColumnSpan="11"' },
  @{ name='MarketMonitor'; cb=(Join-Path $root 'MarketMonitor.cs');      xaml=(Join-Path $root 'MarketMonitor.xaml'); panel='Grid\.Row="1" Margin="5,0,0,9" Grid\.RowSpan="3" Grid\.ColumnSpan="11"' },
  @{ name='Timing';        cb=(Join-Path $root 'Timing.xaml.cs');        xaml=(Join-Path $root 'Timing.xaml');        panel='Grid\.Row="1" Margin="145,0,0,10" Grid\.RowSpan="3" Grid\.ColumnSpan="11"' }
)

foreach ($v in $views) {
    Write-Host "===== $($v.name) =====" -ForegroundColor Cyan

    # ---------- code-behind ----------
    if (-not (Test-Path $v.cb)) { Write-Host "  SKIP cb (not found): $($v.cb)" -ForegroundColor Red; }
    else {
        $cs = [System.IO.File]::ReadAllText($v.cb)
        if ($cs.Contains('WireNavView')) {
            Write-Host "  cb already wired (WireNavView present). Skipping." -ForegroundColor Yellow
        } else {
            $ok = $true

            # Edit A: WireNavView() after EVERY InitializeComponent()
            $rxA = New-Object System.Text.RegularExpressions.Regex('^([ \t]*)(InitializeComponent\(\);)', $mult)
            if ($rxA.Matches($cs).Count -lt 1) { Write-Host "  ABORT cb: no InitializeComponent()." -ForegroundColor Red; $ok=$false }
            if ($ok) {
                $cs = $rxA.Replace($cs, { param($m) $m.Groups[1].Value + $m.Groups[2].Value + "`r`n" + $m.Groups[1].Value + 'WireNavView();' })
            }

            # Edit B: methods before BtnSettings_Click
            $rxB = New-Object System.Text.RegularExpressions.Regex('^([ \t]*)(private void BtnSettings_Click\(object sender, System\.Windows\.RoutedEventArgs e\))', $mult)
            if ($ok -and $rxB.Matches($cs).Count -ne 1) { Write-Host "  ABORT cb: BtnSettings_Click anchor not unique." -ForegroundColor Red; $ok=$false }
            if ($ok) {
                $cs = $rxB.Replace($cs, { param($m) (Reindent $tmplMethods $m.Groups[1].Value) + "`r`n`r`n" + $m.Groups[1].Value + $m.Groups[2].Value }, 1)
            }

            # Edit C: submenu after changePassword
            $rxC = New-Object System.Text.RegularExpressions.Regex('^([ \t]*)(menu\.Items\.Add\(changePassword\);)', $mult)
            if ($ok -and $rxC.Matches($cs).Count -ne 1) { Write-Host "  ABORT cb: changePassword anchor not unique." -ForegroundColor Red; $ok=$false }
            if ($ok) {
                $cs = $rxC.Replace($cs, { param($m) $m.Groups[1].Value + $m.Groups[2].Value + "`r`n`r`n" + (Reindent $tmplSubmenu $m.Groups[1].Value) }, 1)
            }

            if ($ok) {
                $bak = "$($v.cb).$(Get-Date -Format yyyyMMdd_HHmmss).bak"
                Copy-Item $v.cb $bak -Force
                [System.IO.File]::WriteAllText($v.cb, $cs, (New-Object System.Text.UTF8Encoding($false)))
                Write-Host "  Patched $(Split-Path $v.cb -Leaf). Backup: $(Split-Path $bak -Leaf)" -ForegroundColor Green
            } else {
                Write-Host "  cb NOT written (anchor problem above)." -ForegroundColor Red
            }
        }
    }

    # ---------- xaml ----------
    if (-not (Test-Path $v.xaml)) { Write-Host "  SKIP xaml (not found): $($v.xaml)" -ForegroundColor Red; }
    else {
        $xt = [System.IO.File]::ReadAllText($v.xaml)
        if ($xt.Contains('x:Name="MainNavRow"')) {
            Write-Host "  xaml already named MainNavRow. Skipping." -ForegroundColor Yellow
        } else {
            $rxD = New-Object System.Text.RegularExpressions.Regex('(<StackPanel )(' + $v.panel + ')')
            $cnt = $rxD.Matches($xt).Count
            if ($cnt -ne 1) {
                Write-Host "  ABORT xaml: row-1 panel anchor matched $cnt times (expected 1). Not written." -ForegroundColor Red
            } else {
                $xt = $rxD.Replace($xt, { param($m) $m.Groups[1].Value + 'x:Name="MainNavRow" ' + $m.Groups[2].Value }, 1)
                $bakx = "$($v.xaml).$(Get-Date -Format yyyyMMdd_HHmmss).bak"
                Copy-Item $v.xaml $bakx -Force
                [System.IO.File]::WriteAllText($v.xaml, $xt, (New-Object System.Text.UTF8Encoding($false)))
                Write-Host "  Patched $(Split-Path $v.xaml -Leaf). Backup: $(Split-Path $bakx -Leaf)" -ForegroundColor Green
            }
        }
    }
}

Write-Host "`n----- verify -----" -ForegroundColor Cyan
foreach ($v in $views) {
    $hits = @()
    if (Test-Path $v.cb)   { $hits += (Select-String -Path $v.cb   -Pattern 'WireNavView','private void ApplyNavView','Header = "View"').Count }
    if (Test-Path $v.xaml) { $hits += (Select-String -Path $v.xaml -Pattern 'x:Name="MainNavRow"').Count }
    Write-Host ("  {0}: cb-hits+xaml-hits = {1}" -f $v.name, ($hits -join '+'))
}
Write-Host "`nNow REBUILD in Visual Studio, then check each view's gear -> View -> Pro." -ForegroundColor Cyan
