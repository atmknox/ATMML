using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows;
using ATMML.Auth;

namespace ATMML
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);

			// Prevent WPF auto-shutdown when LoginWindow closes
			ShutdownMode = ShutdownMode.OnExplicitShutdown;

			DispatcherUnhandledException += (s, ex) =>
			{
				MessageBox.Show(
					ex.Exception.Message + "\n\n" + ex.Exception.StackTrace,
					"Unhandled Error",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				ex.Handled = true;
			};

			try
			{
				var login = new LoginWindow();
				bool? result = login.ShowDialog();

				if (result != true)
				{
					Shutdown();
					return;
				}

				// Restore normal shutdown behaviour before showing main window
				ShutdownMode = ShutdownMode.OnLastWindowClose;
				new MainView().Show();
			}
			catch (Exception ex)
			{
				MessageBox.Show(
					ex.Message + "\n\n" + ex.StackTrace,
					"Startup Error",
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				Shutdown();
			}
		}
	}
}