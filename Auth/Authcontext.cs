using System.ComponentModel;
using System.Runtime.CompilerServices;
using ATMML.Compliance;   // AuditService lives here

namespace ATMML.Auth
{
	public class AuthContext : INotifyPropertyChanged
	{
		// ── Singleton ─────────────────────────────────────────────────────────
		public static AuthContext Current { get; } = new AuthContext();
		private AuthContext() { }

		// ── Session state ─────────────────────────────────────────────────────
		private AppUser _user;
		public AppUser User
		{
			get => _user;
			private set { _user = value; NotifyAll(); }
		}

		public bool IsAuthenticated => _user != null && _user.IsActive;

		// ── Role identity checks ──────────────────────────────────────────────
		public bool IsAdmin => IsAuthenticated && _user.Role == UserRole.Admin;
		public bool IsPortfolioManager => IsAuthenticated && _user.Role >= UserRole.PortfolioManager;
		public bool IsTrader => IsAuthenticated && _user.Role >= UserRole.Trader;
		public bool IsCompliance => IsAuthenticated && _user.Role >= UserRole.Compliance;
		public bool IsViewer => IsAuthenticated;
		public bool IsReadOnly => IsAuthenticated && _user.Role == UserRole.Viewer;

		// ── Landing page buttons ──────────────────────────────────────────────
		// Admin, PortfolioManager, Trader
		public bool CanAccessPortfolioManagement => IsAuthenticated;
		// All roles
		public bool CanAccessSurveillance => IsAuthenticated && _user.Role > UserRole.Viewer;
		// Admin, PortfolioManager, Compliance
		public bool CanAccessResearch => IsAuthenticated
													 && (_user.Role >= UserRole.PortfolioManager
														 || _user.Role == UserRole.Compliance);

		// ── Portfolio Management sub-buttons ──────────────────────────────────
		// Admin only
		public bool CanAccessPortfolioSetup => IsAdmin;
		// Admin, PortfolioManager, Trader
		public bool CanAccessOrderManagement => IsAuthenticated && _user.Role >= UserRole.Trader;
		// Admin, PortfolioManager, Trader, Compliance
		public bool CanAccessPortfolioPerformance => IsAuthenticated;

		// ── Order Management actions ──────────────────────────────────────────
		// Admin, PortfolioManager — runs the optimizer
		public bool CanRunPortfolio => IsAuthenticated && _user.Role >= UserRole.PortfolioManager;
		// Admin, Trader — submits live orders to FlexOne
		public bool CanSendOrdersToFlexOne => IsAuthenticated && (_user.Role >= UserRole.PortfolioManager || _user.Role == UserRole.Trader);

		// ── Compliance and audit ──────────────────────────────────────────────
		// Admin, Compliance only — can see TEST portfolios in addition to LIVE
		public bool CanAccessTestPortfolios => IsAdmin || (IsAuthenticated && _user.Role == UserRole.Compliance);

		// Admin, PortfolioManager, Compliance
		public bool CanViewCompliance => IsAuthenticated
													 && (_user.Role >= UserRole.PortfolioManager
														 || _user.Role == UserRole.Compliance);
		// Admin only
		public bool CanViewAuditLog => IsAdmin;
		// Admin, PortfolioManager, Compliance
		public bool CanExportReports => IsAuthenticated
													 && (_user.Role >= UserRole.PortfolioManager
														 || _user.Role == UserRole.Compliance);

		// ── General (kept for existing XAML bindings) ─────────────────────────
		public bool CanViewSetup => IsAdmin;
		public bool CanManageUsers => IsAdmin;
		public bool CanApproveTrades => IsPortfolioManager;
		public bool CanModifyPortfolio => IsPortfolioManager;

		// ── Toolbar display ───────────────────────────────────────────────────
		public string DisplayName =>
			IsAuthenticated ? $"{_user.Username}  [{_user.Role}]" : "Not logged in";

		public string Username => _user?.Username ?? "";
		public string UserId => _user?.UserId ?? "SYSTEM";

		// ── Login ─────────────────────────────────────────────────────────────
		public (bool ok, bool mustChange, string error) Login(
			string username, string password)
		{
			var user = UserStore.Authenticate(username, password);
			if (user == null)
				return (false, false, "Invalid username or password.");

			AuditService.SetSession(user.UserId, user.Username, user.Role.ToString());
			AuditService.LogLogin(user.Username);
			User = user;

			return (true, user.MustChangePassword, null);
		}

		// ── Logout ────────────────────────────────────────────────────────────
		public void Logout()
		{
			AuditService.SetSession("SYSTEM", "SYSTEM", "SYSTEM");
			User = null;
		}

		// ── Refresh after username change ─────────────────────────────────────
		public void RefreshUser(string newUsername)
		{
			if (_user != null)
			{
				_user.Username = newUsername;
				AuditService.SetSession(_user.UserId, newUsername, _user.Role.ToString());
				NotifyAll();
			}
		}

		// ── INotifyPropertyChanged ────────────────────────────────────────────
		public event PropertyChangedEventHandler PropertyChanged;

		private void Notify([CallerMemberName] string prop = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

		private void NotifyAll()
		{
			Notify(nameof(User));
			Notify(nameof(IsAuthenticated));
			Notify(nameof(IsAdmin));
			Notify(nameof(IsPortfolioManager));
			Notify(nameof(IsTrader));
			Notify(nameof(IsCompliance));
			Notify(nameof(IsViewer));
			Notify(nameof(IsReadOnly));
			Notify(nameof(CanAccessPortfolioManagement));
			Notify(nameof(CanAccessSurveillance));
			Notify(nameof(CanAccessResearch));
			Notify(nameof(CanAccessPortfolioSetup));
			Notify(nameof(CanAccessOrderManagement));
			Notify(nameof(CanAccessPortfolioPerformance));
			Notify(nameof(CanRunPortfolio));
			Notify(nameof(CanSendOrdersToFlexOne));
			Notify(nameof(CanAccessTestPortfolios));
			Notify(nameof(CanViewCompliance));
			Notify(nameof(CanViewAuditLog));
			Notify(nameof(CanExportReports));
			Notify(nameof(CanViewSetup));
			Notify(nameof(CanManageUsers));
			Notify(nameof(CanApproveTrades));
			Notify(nameof(CanModifyPortfolio));
			Notify(nameof(DisplayName));
			Notify(nameof(Username));
			Notify(nameof(UserId));
		}
	}
}