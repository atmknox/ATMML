using ATMML.Compliance;   // AuditService lives here
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ATMML.Auth
{
	// ─────────────────────────────────────────────────────────────────────────
	//  AuthContext  —  singleton session state, XAML-bindable
	//
	//  Usage in XAML (bind to static resource):
	//    <Window.Resources>
	//      <local:AuthContext x:Key="Auth"/>  ← or use App.xaml resources
	//    </Window.Resources>
	//    Visibility="{Binding IsAdmin, Source={StaticResource Auth},
	//                         Converter={StaticResource BoolToVisibility}}"
	//
	//  Usage in code-behind:
	//    if (!AuthContext.Current.CanApproveTrades)  { ... }
	// ─────────────────────────────────────────────────────────────────────────
	public class AuthContext : INotifyPropertyChanged
	{
		// ── Singleton ─────────────────────────────────────────────────────────
		public static AuthContext Current { get; } = new AuthContext();
		private AuthContext() { }   // use Current only

		// ── Session state ─────────────────────────────────────────────────────
		private AppUser _user;
		public AppUser User
		{
			get => _user;
			private set
			{
				_user = value;
				NotifyAll();
			}
		}

		public bool IsAuthenticated => _user != null && _user.IsActive;

		// ─────────────────────────────────────────────────────────────────────
		//  Role convenience properties  (used directly in XAML bindings)
		// ─────────────────────────────────────────────────────────────────────
		public bool IsAdmin => IsAuthenticated && _user.Role == UserRole.Admin;
		public bool IsPortfolioManager => IsAuthenticated && _user.Role >= UserRole.PortfolioManager;
		public bool IsAnalyst => IsAuthenticated && _user.Role >= UserRole.Analyst;
		public bool IsViewer => IsAuthenticated;

		// Semantic aliases — use these in XAML for readable intent
		public bool CanViewSetup => IsAdmin;
		public bool CanManageUsers => IsAdmin;
		public bool CanApproveTrades => IsPortfolioManager;
		public bool CanModifyPortfolio => IsPortfolioManager;
		public bool CanRunBacktest => IsAnalyst;
		public bool IsReadOnly => IsAuthenticated && _user.Role == UserRole.Viewer;

		// For toolbar label  e.g. "rick  [Admin]"
		public string DisplayName =>
			IsAuthenticated ? $"{_user.Username}  [{_user.Role}]" : "Not logged in";

		public string Username => _user?.Username ?? "";
		public string UserId => _user?.UserId ?? "SYSTEM";

		// ─────────────────────────────────────────────────────────────────────
		//  Login  —  calls UserStore then wires AuditService
		// ─────────────────────────────────────────────────────────────────────
		/// <summary>
		/// Returns (success, mustChangePassword, errorMessage).
		/// On success, AuditService session is set and LOGIN is logged.
		/// </summary>
		public (bool ok, bool mustChange, string error) Login(
			string username, string password)
		{
			var user = UserStore.Authenticate(username, password);
			if (user == null)
				return (false, false, "Invalid username or password.");

			// ── Wire into compliance layer ────────────────────────────────────
			AuditService.SetSession(user.UserId, user.Username, user.Role.ToString());
			AuditService.LogLogin(user.Username);

			// ── Set session ───────────────────────────────────────────────────
			User = user;

			return (true, user.MustChangePassword, null);
		}

		// ─────────────────────────────────────────────────────────────────────
		//  Logout
		// ─────────────────────────────────────────────────────────────────────
		public void Logout()
		{
			AuditService.SetSession("SYSTEM", "SYSTEM", "SYSTEM");
			User = null;
		}

		// ─────────────────────────────────────────────────────────────────────
		//  Refresh  —  call after self-service username change so DisplayName
		//              updates in toolbar without a full re-login
		// ─────────────────────────────────────────────────────────────────────
		public void RefreshUser(string newUsername)
		{
			if (_user != null)
			{
				_user.Username = newUsername;
				AuditService.SetSession(_user.UserId, newUsername, _user.Role.ToString());
				NotifyAll();
			}
		}

		// ─────────────────────────────────────────────────────────────────────
		//  INotifyPropertyChanged
		// ─────────────────────────────────────────────────────────────────────
		public event PropertyChangedEventHandler PropertyChanged;

		private void Notify([CallerMemberName] string prop = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

		private void NotifyAll()
		{
			Notify(nameof(User));
			Notify(nameof(IsAuthenticated));
			Notify(nameof(IsAdmin));
			Notify(nameof(IsPortfolioManager));
			Notify(nameof(IsAnalyst));
			Notify(nameof(IsViewer));
			Notify(nameof(CanViewSetup));
			Notify(nameof(CanManageUsers));
			Notify(nameof(CanApproveTrades));
			Notify(nameof(CanModifyPortfolio));
			Notify(nameof(CanRunBacktest));
			Notify(nameof(IsReadOnly));
			Notify(nameof(DisplayName));
			Notify(nameof(Username));
			Notify(nameof(UserId));
		}
	}
}
