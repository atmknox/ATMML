using System;

namespace ATMML.Auth
{
	// ─────────────────────────────────────────────────────────────────────────
	//  UserRole  —  ordinal defines access ceiling (Viewer=0 .. Admin=3)
	//  XAML visibility helpers in AuthContext compare via >= so that
	//  PortfolioManager automatically satisfies IsAnalyst checks, etc.
	// ─────────────────────────────────────────────────────────────────────────
	public enum UserRole
	{
		Viewer = 0,   // read-only: positions, P&L, charts
		Analyst = 1,   // + backtests, signals, risk reports
		PortfolioManager = 2,   // + approve trades, modify portfolios
		Admin = 3    // + user management, all setup controls
	}

	public class AppUser
	{
		// ── Identity ──────────────────────────────────────────────────────────
		public string UserId { get; set; } = GenerateId();
		public string Username { get; set; }
		public UserRole Role { get; set; } = UserRole.Viewer;

		// ── Credentials ───────────────────────────────────────────────────────
		public string PasswordHash { get; set; }   // PBKDF2-SHA256, base64
		public string Salt { get; set; }   // 16 random bytes, base64

		// ── State ─────────────────────────────────────────────────────────────
		public bool IsActive { get; set; } = true;
		public bool MustChangePassword { get; set; } = false;  // forced on first login

		// ── Audit metadata ────────────────────────────────────────────────────
		public DateTime LastLogin { get; set; }
		public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
		public string CreatedBy { get; set; } = "SYSTEM";

		// ── Helper ────────────────────────────────────────────────────────────
		private static string GenerateId()
			=> Guid.NewGuid().ToString("N")[..8].ToUpper();   // e.g. "A1B2C3D4"
	}
}
