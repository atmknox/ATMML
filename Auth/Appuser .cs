using System;

namespace ATMML.Auth
{
	// ─────────────────────────────────────────────────────────────────────────
	//  UserRole  —  ordinal defines access ceiling (Viewer=0 .. Admin=4)
	//  AuthContext compares via >= so higher roles satisfy lower role checks.
	//
	//  Viewer           = 0   Passive observers — surveillance, charts, P&L
	//  Compliance       = 1   Compliance/risk — reports, audit log
	//  Trader           = 2   Executes trades, follows fills, reports problems
	//  PortfolioManager = 3   PM — full portfolio access, run optimizer
	//  Admin            = 4   Full access including setup and user management
	// ─────────────────────────────────────────────────────────────────────────
	public enum UserRole
	{
		Viewer = 0,
		Compliance = 1,
		Trader = 2,
		PortfolioManager = 3,
		Admin = 4
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