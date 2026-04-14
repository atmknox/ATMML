using System.Windows;
using System.Windows.Input;

namespace ATMML.Auth
{
	public partial class ChangePasswordDialog : Window
	{
		private readonly bool _forcedChange;

		/// <param name="forcedChange">
		/// True when the user must change password before proceeding.
		/// Hides the Cancel button and shows the forced-change banner.
		/// </param>
		public ChangePasswordDialog(bool forcedChange = false)
		{
			InitializeComponent();
			_forcedChange = forcedChange;

			if (forcedChange)
			{
				ForcedBanner.Visibility = Visibility.Visible;
				BtnCancel.Visibility = Visibility.Collapsed;
			}

			Loaded += (_, _) => TxtCurrent.Focus();
		}

		// ── Drag ─────────────────────────────────────────────────────────────
		private void DragBar_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left) DragMove();
		}

		// ── Buttons ───────────────────────────────────────────────────────────
		private void BtnCancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void BtnSave_Click(object sender, RoutedEventArgs e)
		{
			HideError();

			string current = TxtCurrent.Password;
			string newPwd = TxtNew.Password;
			string confirm = TxtConfirm.Password;

			if (string.IsNullOrEmpty(current))
			{ ShowError("Enter your current password."); return; }

			if (string.IsNullOrEmpty(newPwd) || newPwd.Length < 8)
			{ ShowError("New password must be at least 8 characters."); return; }

			if (newPwd != confirm)
			{ ShowError("Passwords do not match."); return; }

			var (ok, error) = UserStore.ChangePassword(
				AuthContext.Current.Username, current, newPwd);

			if (!ok) { ShowError(error); return; }

			DialogResult = true;
			Close();
		}

		// ─────────────────────────────────────────────────────────────────────
		private void ShowError(string msg)
		{
			TxtError.Text = msg;
			TxtError.Visibility = Visibility.Visible;
		}

		private void HideError()
		{
			TxtError.Text = string.Empty;
			TxtError.Visibility = Visibility.Collapsed;
		}
	}
}
