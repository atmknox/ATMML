using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ATMML.Auth
{
	// ─────────────────────────────────────────────────────────────────────────
	//  UserStore
	//
	//  Storage  : C:\Users\Public\Documents\ATMML\config\users.dat
	//  Encoding : JSON → UTF-8 bytes → DPAPI (LocalMachine scope)
	//  Passwords: PBKDF2-SHA256, 120 000 iterations, 32-byte derived key
	//
	//  On first run (no file) a default Admin is bootstrapped:
	//    username: admin   password: Admin@CMR1   MustChangePassword: true
	// ─────────────────────────────────────────────────────────────────────────
	public static class UserStore
	{
		// ── File path ─────────────────────────────────────────────────────────
		private static readonly string DataDir =
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
						 "ATMML", "config");

		private static string FilePath => Path.Combine(DataDir, "users.dat");

		// ── PBKDF2 parameters ─────────────────────────────────────────────────
		private const int Iterations = 120_000;
		private const int KeyBytes = 32;
		private const int SaltBytes = 16;

		// ─────────────────────────────────────────────────────────────────────
		//  Load / Save (DPAPI)
		// ─────────────────────────────────────────────────────────────────────
		public static List<AppUser> LoadUsers()
		{
			if (!File.Exists(FilePath))
				return Bootstrap();

			try
			{
				byte[] encrypted = File.ReadAllBytes(FilePath);
				byte[] plain = ProtectedData.Unprotect(encrypted, null,
									   DataProtectionScope.LocalMachine);
				string json = Encoding.UTF8.GetString(plain);
				return JsonSerializer.Deserialize<List<AppUser>>(json)
					   ?? new List<AppUser>();
			}
			catch
			{
				// Corrupt / wrong machine — re-bootstrap rather than crash
				return Bootstrap();
			}
		}

		public static void SaveUsers(List<AppUser> users)
		{
			Directory.CreateDirectory(DataDir);
			string json = JsonSerializer.Serialize(users,
								   new JsonSerializerOptions { WriteIndented = true });
			byte[] plain = Encoding.UTF8.GetBytes(json);
			byte[] encrypted = ProtectedData.Protect(plain, null,
								   DataProtectionScope.LocalMachine);
			File.WriteAllBytes(FilePath, encrypted);
		}

		// ─────────────────────────────────────────────────────────────────────
		//  Authentication
		// ─────────────────────────────────────────────────────────────────────
		/// <summary>Returns the matching active user, or null on failure.</summary>
		public static AppUser Authenticate(string username, string password)
		{
			if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
				return null;

			var users = LoadUsers();
			var user = users.FirstOrDefault(u =>
				string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)
				&& u.IsActive);

			if (user == null) return null;
			if (!VerifyPassword(password, user.PasswordHash, user.Salt)) return null;

			// Stamp LastLogin and persist
			user.LastLogin = DateTime.UtcNow;
			SaveUsers(users);
			return user;
		}

		// ─────────────────────────────────────────────────────────────────────
		//  Admin operations
		// ─────────────────────────────────────────────────────────────────────
		public static (bool ok, string error) CreateUser(
			string username, string tempPassword, UserRole role, string createdBy)
		{
			if (string.IsNullOrWhiteSpace(username))
				return (false, "Username is required.");
			if (string.IsNullOrWhiteSpace(tempPassword) || tempPassword.Length < 6)
				return (false, "Password must be at least 6 characters.");

			var users = LoadUsers();
			if (users.Any(u => string.Equals(u.Username, username,
											  StringComparison.OrdinalIgnoreCase)))
				return (false, $"Username '{username}' already exists.");

			string salt = GenerateSalt();
			users.Add(new AppUser
			{
				Username = username.Trim(),
				Role = role,
				Salt = salt,
				PasswordHash = HashPassword(tempPassword, salt),
				IsActive = true,
				MustChangePassword = true,
				CreatedBy = createdBy,
				CreatedDate = DateTime.UtcNow
			});

			SaveUsers(users);
			return (true, null);
		}

		public static (bool ok, string error) SetRole(
			string targetUsername, UserRole newRole, string changedBy)
		{
			var users = LoadUsers();
			var user = users.FirstOrDefault(u =>
				string.Equals(u.Username, targetUsername,
							  StringComparison.OrdinalIgnoreCase));
			if (user == null) return (false, "User not found.");

			// Prevent demoting the last Admin
			if (user.Role == UserRole.Admin && newRole != UserRole.Admin)
			{
				int adminCount = users.Count(u => u.Role == UserRole.Admin && u.IsActive);
				if (adminCount <= 1)
					return (false, "Cannot demote the last active Admin.");
			}

			user.Role = newRole;
			SaveUsers(users);
			return (true, null);
		}

		public static (bool ok, string error) AdminResetPassword(
			string targetUsername, string newPassword, string resetBy)
		{
			if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
				return (false, "Password must be at least 6 characters.");

			var users = LoadUsers();
			var user = users.FirstOrDefault(u =>
				string.Equals(u.Username, targetUsername,
							  StringComparison.OrdinalIgnoreCase));
			if (user == null) return (false, "User not found.");

			user.Salt = GenerateSalt();
			user.PasswordHash = HashPassword(newPassword, user.Salt);
			user.MustChangePassword = true;
			SaveUsers(users);
			return (true, null);
		}

		public static (bool ok, string error) SetActive(
			string targetUsername, bool active, string changedBy)
		{
			var users = LoadUsers();
			var user = users.FirstOrDefault(u =>
				string.Equals(u.Username, targetUsername,
							  StringComparison.OrdinalIgnoreCase));
			if (user == null) return (false, "User not found.");

			if (!active && user.Role == UserRole.Admin)
			{
				int adminCount = users.Count(u => u.Role == UserRole.Admin && u.IsActive);
				if (adminCount <= 1)
					return (false, "Cannot deactivate the last active Admin.");
			}

			user.IsActive = active;
			SaveUsers(users);
			return (true, null);
		}

		// ─────────────────────────────────────────────────────────────────────
		//  Self-service
		// ─────────────────────────────────────────────────────────────────────
		public static (bool ok, string error) ChangePassword(
			string username, string currentPassword, string newPassword)
		{
			if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
				return (false, "New password must be at least 8 characters.");

			var users = LoadUsers();
			var user = users.FirstOrDefault(u =>
				string.Equals(u.Username, username,
							  StringComparison.OrdinalIgnoreCase));
			if (user == null) return (false, "User not found.");
			if (!VerifyPassword(currentPassword, user.PasswordHash, user.Salt))
				return (false, "Current password is incorrect.");

			user.Salt = GenerateSalt();
			user.PasswordHash = HashPassword(newPassword, user.Salt);
			user.MustChangePassword = false;
			SaveUsers(users);
			return (true, null);
		}

		public static (bool ok, string error) ChangeUsername(
			string currentUsername, string newUsername, string password)
		{
			if (string.IsNullOrWhiteSpace(newUsername))
				return (false, "New username is required.");

			var users = LoadUsers();
			if (users.Any(u => string.Equals(u.Username, newUsername,
											  StringComparison.OrdinalIgnoreCase)
							   && !string.Equals(u.Username, currentUsername,
												 StringComparison.OrdinalIgnoreCase)))
				return (false, $"Username '{newUsername}' is already taken.");

			var user = users.FirstOrDefault(u =>
				string.Equals(u.Username, currentUsername,
							  StringComparison.OrdinalIgnoreCase));
			if (user == null) return (false, "User not found.");
			if (!VerifyPassword(password, user.PasswordHash, user.Salt))
				return (false, "Password is incorrect.");

			user.Username = newUsername.Trim();
			SaveUsers(users);
			return (true, null);
		}

		// ─────────────────────────────────────────────────────────────────────
		//  Crypto helpers
		// ─────────────────────────────────────────────────────────────────────
		private static string GenerateSalt()
		{
			byte[] salt = new byte[SaltBytes];
			using var rng = RandomNumberGenerator.Create();
			rng.GetBytes(salt);
			return Convert.ToBase64String(salt);
		}

		private static string HashPassword(string password, string saltBase64)
		{
			byte[] salt = Convert.FromBase64String(saltBase64);
			using var pbkdf2 = new Rfc2898DeriveBytes(
				password, salt, Iterations, HashAlgorithmName.SHA256);
			return Convert.ToBase64String(pbkdf2.GetBytes(KeyBytes));
		}

		private static bool VerifyPassword(string password, string hashBase64, string saltBase64)
		{
			string candidate = HashPassword(password, saltBase64);
			// Constant-time compare
			return CryptographicOperations.FixedTimeEquals(
				Convert.FromBase64String(candidate),
				Convert.FromBase64String(hashBase64));
		}

		// ─────────────────────────────────────────────────────────────────────
		//  Bootstrap — creates default admin on first run
		// ─────────────────────────────────────────────────────────────────────
		private static List<AppUser> Bootstrap()
		{
			const string defaultPassword = "Admin@CMR1";
			string salt = GenerateSalt();
			var users = new List<AppUser>
			{
				new AppUser
				{
					Username           = "admin",
					Role               = UserRole.Admin,
					Salt               = salt,
					PasswordHash       = HashPassword(defaultPassword, salt),
					IsActive           = true,
					MustChangePassword = true,
					CreatedBy          = "SYSTEM",
					CreatedDate        = DateTime.UtcNow
				}
			};
			SaveUsers(users);
			return users;
		}
	}
}
