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

				// Show the loading overlay BEFORE MainView construction begins.
				// SpinnerHost.Show() runs the WinForms LoadingForm on its own STA
				// thread and only returns once the form's Shown event has fired —
				// but "Shown" only means Windows has dispatched WM_SHOWWINDOW, not
				// that the form has actually painted to screen. Without a brief
				// pump, the WPF main thread immediately enters new MainView()
				// (heavy XAML parse for PortfolioBuilder) and the spinner thread
				// can't get a paint cycle in until construction is done — which
				// is exactly the freeze the SpinnerHost.cs comments warn about.
				//
				// Dispatcher.Invoke at ApplicationIdle priority lets pending
				// foreground render messages (including the spinner thread's
				// initial paint) drain before we block the main thread on
				// MainView's synchronous construction.
				SpinnerHost.Show();
				Dispatcher.Invoke(new Action(() => { }),
					System.Windows.Threading.DispatcherPriority.ApplicationIdle);

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