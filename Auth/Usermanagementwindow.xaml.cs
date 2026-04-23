using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ATMML.Auth
{
	public partial class UserManagementWindow : Window
	{
		private List<AppUser> _users;
		private AppUser _selectedUser;
		private bool _isAddMode = false;

		public UserManagementWindow()
		{
			InitializeComponent();

			// Populate role dropdown
			CbRole.ItemsSource = Enum.GetNames(typeof(UserRole));
			CbRole.SelectedIndex = 0;

			LoadUsers();
		}

		// ── Drag / close ─────────────────────────────────────────────────────
		private void DragBar_MouseDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Left) DragMove();
		}

		private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

		// ─────────────────────────────────────────────────────────────────────
		//  Load / refresh
		// ─────────────────────────────────────────────────────────────────────
		private void LoadUsers(string reselectUsername = null)
		{
			_users = UserStore.LoadUsers()
							  .OrderBy(u => u.Username)
							  .ToList();
			UserGrid.ItemsSource = null;
			UserGrid.ItemsSource = _users;

			// Restore selection (and therefore edit-panel + status message) after a refresh
			if (!string.IsNullOrEmpty(reselectUsername))
			{
				var row = _users.FirstOrDefault(u =>
					string.Equals(u.Username, reselectUsername, StringComparison.OrdinalIgnoreCase));
				if (row != null) UserGrid.SelectedItem = row;
			}
		}

		// ─────────────────────────────────────────────────────────────────────
		//  Grid selection  →  populate edit panel
		// ─────────────────────────────────────────────────────────────────────
		private void UserGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			// If we're in the middle of adding a user, an UnselectAll() fires this event.
			// Do not clobber _isAddMode or reset the edit panel in that case.
			if (_isAddMode) return;

			_selectedUser = UserGrid.SelectedItem as AppUser;

			if (_selectedUser == null)
			{
				HideEditPanel();
				return;
			}

			ShowEditPanel(editMode: true);
			PopulateFields(_selectedUser);
		}

		// ─────────────────────────────────────────────────────────────────────
		//  Add user button
		// ─────────────────────────────────────────────────────────────────────
		private void BtnAddUser_Click(object sender, RoutedEventArgs e)
		{
			_isAddMode = true;
			_selectedUser = null;
			UserGrid.UnselectAll();

			ShowEditPanel(editMode: false);
			TxtEditUsername.Text = "";
			CbRole.SelectedIndex = 0;
			ChkActive.IsChecked = true;
			TxtNewPassword.Clear();
			LblPasswordHint.Visibility = Visibility.Collapsed;  // password required for new user
			LblPassword.Text = "TEMP PASSWORD *";
		}

		// ─────────────────────────────────────────────────────────────────────
		//  Save
		// ─────────────────────────────────────────────────────────────────────
		private void BtnSaveUser_Click(object sender, RoutedEventArgs e)
		{
			HideMessages();
			string username = TxtEditUsername.Text.Trim();
			var role = (UserRole)Enum.Parse(typeof(UserRole), CbRole.SelectedItem.ToString());
			bool isActive = ChkActive.IsChecked == true;
			string newPwd = TxtNewPassword.Password;
			string adminName = AuthContext.Current.Username;

			if (_isAddMode)
			{
				// ── Create new user ───────────────────────────────────────────
				if (string.IsNullOrEmpty(newPwd))
				{ ShowError("A temporary password is required for new users."); return; }

				var (ok, error) = UserStore.CreateUser(username, newPwd, role, adminName);
				if (!ok) { ShowError(error); return; }

				_isAddMode = false;
				LoadUsers(username);
				ShowStatus($"User '{username}' created. They must change their password on first login.");
			}
			else
			{
				// ── Update existing user ──────────────────────────────────────
				if (_selectedUser == null) { ShowError("No user selected. Click Add User or select a row."); return; }

				// Role change
				if (_selectedUser.Role != role)
				{
					var (ok, error) = UserStore.SetRole(_selectedUser.Username, role, adminName);
					if (!ok) { ShowError(error); return; }
				}

				// Active state change
				if (_selectedUser.IsActive != isActive)
				{
					var (ok, error) = UserStore.SetActive(_selectedUser.Username, isActive, adminName);
					if (!ok) { ShowError(error); return; }
				}

				// Password reset (optional)
				if (!string.IsNullOrEmpty(newPwd))
				{
					var (ok, error) = UserStore.AdminResetPassword(_selectedUser.Username, newPwd, adminName);
					if (!ok) { ShowError(error); return; }
				}

				string usernameToReselect = _selectedUser.Username;
				LoadUsers(usernameToReselect);
				ShowStatus("Changes saved.");
			}
		}

		// ─────────────────────────────────────────────────────────────────────
		//  Toggle active / deactivate
		// ─────────────────────────────────────────────────────────────────────
		private void BtnToggleActive_Click(object sender, RoutedEventArgs e)
		{
			if (_selectedUser == null) return;
			HideMessages();

			string targetUsername = _selectedUser.Username;
			bool wasActive = _selectedUser.IsActive;
			bool newState = !wasActive;
			string adminName = AuthContext.Current.Username;

			var (ok, error) = UserStore.SetActive(targetUsername, newState, adminName);
			if (!ok) { ShowError(error); return; }

			// Reload first so _selectedUser is refreshed, then show status so it survives
			LoadUsers(targetUsername);
			ShowStatus(wasActive
				? $"User '{targetUsername}' deactivated."
				: $"User '{targetUsername}' reactivated.");
		}

		// ─────────────────────────────────────────────────────────────────────
		//  Helpers
		// ─────────────────────────────────────────────────────────────────────
		private void PopulateFields(AppUser u)
		{
			TxtEditUsername.Text = u.Username;
			CbRole.SelectedItem = u.Role.ToString();
			ChkActive.IsChecked = u.IsActive;
			TxtNewPassword.Clear();
			LblPassword.Text = "RESET PASSWORD";
			LblPasswordHint.Visibility = Visibility.Visible;

			// Disable deactivate button for own account
			bool isSelf = string.Equals(u.Username, AuthContext.Current.Username,
										StringComparison.OrdinalIgnoreCase);
			BtnToggleActive.IsEnabled = !isSelf;
			BtnToggleActive.Content = u.IsActive ? "Deactivate" : "Reactivate";
		}

		private void ShowEditPanel(bool editMode)
		{
			EditFields.Visibility = Visibility.Visible;
			PanelTitle.Text = editMode ? "EDIT USER" : "ADD USER";
			HideMessages();
		}

		private void HideEditPanel()
		{
			EditFields.Visibility = Visibility.Collapsed;
			PanelTitle.Text = "SELECT A USER";
		}

		private void ShowStatus(string msg)
		{
			TxtEditStatus.Text = msg;
			TxtEditStatus.Visibility = Visibility.Visible;
			TxtEditError.Visibility = Visibility.Collapsed;
		}

		private void ShowError(string msg)
		{
			TxtEditError.Text = msg;
			TxtEditError.Visibility = Visibility.Visible;
			TxtEditStatus.Visibility = Visibility.Collapsed;
		}

		private void HideMessages()
		{
			TxtEditStatus.Visibility = Visibility.Collapsed;
			TxtEditError.Visibility = Visibility.Collapsed;
		}
	}
}
