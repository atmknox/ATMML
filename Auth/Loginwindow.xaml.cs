using System.IO;
using System.Windows;
using System.Windows.Input;

namespace ATMML.Auth
{
	public partial class LoginWindow : Window
	{
		private int _failedAttempts = 0;

		// Stored next to the users.dat file — same config folder
		private static readonly string LastUserFile =
			Path.Combine(
				System.Environment.GetFolderPath(
					System.Environment.SpecialFolder.CommonApplicationData),
				"ATMML", "config", "lastuser.txt");

		public LoginWindow()
		{
			InitializeComponent();
			Loaded += (_, _) => RestoreLastUsername();
		}

		private void RestoreLastUsername()
		{
			try
			{
				if (File.Exists(LastUserFile))
				{
					string saved = File.ReadAllText(LastUserFile).Trim();
					if (!string.IsNullOrEmpty(saved))
					{
						TxtUsername.Text = saved;
						TxtPassword.Focus();   // cursor lands on password — just type & Enter
						return;
					}
				}
			}
			catch { /* ignore — just fall through to normal focus */ }

			TxtUsername.Focus();
		}

		private void SaveLastUsername(string username)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(LastUserFile));
				File.WriteAllText(LastUserFile, username);
			}
			catch { /* non-critical */ }
		}

		// ── Drag support (WindowStyle=None) ──────────────────────────────────
		private void DragBar_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left)
				DragMove();
		}

		// ── Enter key on either field triggers login ──────────────────────────
		private void Input_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter) AttemptLogin();
		}

		private void BtnLogin_Click(object sender, RoutedEventArgs e)
			=> AttemptLogin();

		// ─────────────────────────────────────────────────────────────────────
		//  Core login logic
		// ─────────────────────────────────────────────────────────────────────
		private void AttemptLogin()
		{
			HideError();
			string username = TxtUsername.Text.Trim();
			// Read from whichever field is currently visible
			string password = TxtPasswordVisible.Visibility == Visibility.Visible
				? TxtPasswordVisible.Text
				: TxtPassword.Password;

			if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
			{
				ShowError("Please enter username and password.");
				return;
			}

			BtnLogin.IsEnabled = false;

			var (ok, mustChange, error) = AuthContext.Current.Login(username, password);

			if (!ok)
			{
				_failedAttempts++;
				BtnLogin.IsEnabled = true;
				ShowError(_failedAttempts >= 3
					? $"Invalid credentials. ({_failedAttempts} failed attempts)"
					: "Invalid username or password.");

				TxtPassword.Clear();
				TxtPassword.Focus();
				return;
			}

			// ── Force password change on first login ──────────────────────────
			if (mustChange)
			{
				var dlg = new ChangePasswordDialog(forcedChange: true)
				{
					Owner = this
				};
				bool? changed = dlg.ShowDialog();

				// If user cancels a forced change — logout and stay on login screen
				if (changed != true)
				{
					AuthContext.Current.Logout();
					BtnLogin.IsEnabled = true;
					ShowError("You must change your password before continuing.");
					return;
				}
			}

			SaveLastUsername(username);
			DialogResult = true;
			Close();
		}

		// ─────────────────────────────────────────────────────────────────────
		private void ShowError(string message)
		{
			TxtError.Text = message;
			TxtError.Visibility = Visibility.Visible;
		}

		private void HideError()
		{
			TxtError.Text = string.Empty;
			TxtError.Visibility = Visibility.Collapsed;
		}

		private void BtnShowPassword_Click(object sender, RoutedEventArgs e)
		{
			if (TxtPasswordVisible.Visibility == Visibility.Collapsed)
			{
				TxtPasswordVisible.Text = TxtPassword.Password;
				TxtPassword.Visibility = Visibility.Collapsed;
				TxtPasswordVisible.Visibility = Visibility.Visible;
				EyeIcon.Data = System.Windows.Media.Geometry.Parse(
					"M11.83,9L15,12.16C15,12.11 15,12.05 15,12A3,3 0 0,0 12,9C11.94,9 11.89,9 11.83,9M7.53,9.8L9.08,11.35C9.03,11.56 9,11.77 9,12A3,3 0 0,0 12,15C12.22,15 12.44,14.97 12.65,14.92L14.2,16.47C13.53,16.8 12.79,17 12,17A5,5 0 0,1 7,12C7,11.21 7.2,10.47 7.53,9.8M2,4.27L4.28,6.55L4.73,7C3.08,8.3 1.78,10 1,12C2.73,16.39 7,19.5 12,19.5C13.55,19.5 15.03,19.12 16.38,18.48L16.81,18.9L19.73,21.84L21,20.57L3.27,3M12,4.5C17,4.5 21.27,7.61 23,12C22.18,14.08 20.79,15.88 19,17.19L17.58,15.76C18.94,14.82 20.06,13.54 20.82,12C19.17,8.64 15.76,6.5 12,6.5C10.91,6.5 9.84,6.68 8.84,7L7.3,5.47C8.74,4.85 10.33,4.5 12,4.5Z");
				TxtPasswordVisible.Focus();
				TxtPasswordVisible.SelectionStart = TxtPasswordVisible.Text.Length;
			}
			else
			{
				TxtPassword.Password = TxtPasswordVisible.Text;
				TxtPasswordVisible.Visibility = Visibility.Collapsed;
				TxtPassword.Visibility = Visibility.Visible;
				EyeIcon.Data = System.Windows.Media.Geometry.Parse(
					"M12,4.5C7,4.5 2.73,7.61 1,12C2.73,16.39 7,19.5 12,19.5C17,19.5 21.27,16.39 23,12C21.27,7.61 17,4.5 12,4.5M12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9Z");
				TxtPassword.Focus();
			}
		}

		private void BtnClose_Click(object sender, RoutedEventArgs e)
		{
			var result = MessageBox.Show(
				"Are you sure you want to exit?",
				"Exit ATMML",
				MessageBoxButton.YesNo,
				MessageBoxImage.Question);

			if (result == MessageBoxResult.Yes)
			{
				DialogResult = false;
				Close();
			}
		}
	}
}