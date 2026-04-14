// =============================================================================
//  WIRING GUIDE  —  Session / Login + RBAC
//  Drop all files in:  C:\Users\Admin\Documents\ATMML\Auth\
//  Add to project in VS: right-click Auth folder → Add → Existing Item
// =============================================================================


// ── 1.  App.xaml.cs  —  show login before main window ────────────────────────
//
//  In App.xaml: REMOVE  StartupUri="MainWindow.xaml"
//  Then override OnStartup:

/*
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    var login = new ATMML.Auth.LoginWindow();
    bool? result = login.ShowDialog();

    if (result != true)          // user closed window without logging in
    {
        Shutdown();
        return;
    }

    new MainWindow().Show();     // normal startup continues
}
*/


// ── 2.  MainWindow.xaml  —  bind toolbar label & user-management button ───────

/*
  Add to Window.Resources (or App.xaml Resources so all windows share it):

    <auth:AuthContext x:Key="Auth"
                      xmlns:auth="clr-namespace:ATMML.Auth"/>

  Toolbar display name:

    <TextBlock Text="{Binding DisplayName,
                              Source={StaticResource Auth}}"
               Foreground="#9CA3AF" FontSize="11"/>

  "Manage Users" button  (Admin only):

    <Button Content="Manage Users"
            Visibility="{Binding CanManageUsers,
                                 Source={StaticResource Auth},
                                 Converter={StaticResource BoolToVisibility}}"
            Click="BtnManageUsers_Click"/>
*/


// ── 3.  MainWindow.xaml.cs  —  open user management from toolbar button ───────

/*
private void BtnManageUsers_Click(object sender, RoutedEventArgs e)
{
    new ATMML.Auth.UserManagementWindow { Owner = this }.ShowDialog();
}
*/


// ── 4.  My Account / Change Password (self-service from toolbar) ──────────────

/*
private void BtnMyAccount_Click(object sender, RoutedEventArgs e)
{
    new ATMML.Auth.ChangePasswordDialog { Owner = this }.ShowDialog();
}
*/


// ── 5.  Protecting controls in Portfolio_Builder.xaml (examples) ─────────────

/*
  Setup tab:
    Visibility="{Binding CanViewSetup, Source={StaticResource Auth},
                         Converter={StaticResource BoolToVisibility}}"

  Approve rebalance button:
    IsEnabled="{Binding CanApproveTrades, Source={StaticResource Auth}}"

  DecisionLock toggle:
    Visibility="{Binding IsAdmin, Source={StaticResource Auth},
                         Converter={StaticResource BoolToVisibility}}"
*/


// ── 6.  BooleanToVisibilityConverter (add once to App.xaml if not present) ───

/*
  <Application.Resources>
    <BooleanToVisibilityConverter x:Key="BoolToVisibility"/>
    ...
  </Application.Resources>
*/


// ── 7.  Default credentials (first run) ───────────────────────────────────────
//
//  Username : admin
//  Password : Admin@CMR1
//  Flag     : MustChangePassword = true  →  user is forced to change on first login
//
//  File     : C:\ProgramData\ATMML\config\users.dat  (DPAPI encrypted)
//
// =============================================================================
