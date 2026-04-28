using System;
using System.Drawing;
using System.Threading;
using WinForms = System.Windows.Forms;

namespace ATMML
{
	/// <summary>
	/// Login → role-view loading screen. STATIC display — full-screen black
	/// with centred "Loading ATMML..." text. No animation, no timers, no
	/// rendering pipeline dependencies.
	///
	/// Prior animated attempts (WPF Storyboard, WPF DispatcherTimer, WinForms
	/// per-frame GDI+, WinForms pre-rendered bitmap blits, deferred MainView
	/// construction) all exhibited the same failure on this codebase:
	/// the spinner thread would tick once or twice, then freeze for the
	/// duration of MainView/PortfolioBuilder construction in the main UI
	/// thread, then resume only after construction completed.
	///
	/// Root cause is some interaction between the main process's heavy XAML
	/// parsing and Windows' rendering pipeline that we cannot reliably defeat
	/// from another thread inside the same process. Process isolation would
	/// fix it but introduces an auxiliary executable that institutional DD
	/// reviews flag.
	///
	/// The pragmatic answer: drop the animation. A static text display has
	/// no animation to freeze. The user sees "Loading ATMML..." the moment
	/// login closes and continues seeing it until the role view paints.
	/// That's clear feedback that the app is working — same purpose a
	/// spinner serves — without any moving parts to break.
	///
	/// Show() and Hide() keep the same signature and contract as before:
	/// idempotent, thread-safe, hide-at-end-of-role-routing semantics.
	/// </summary>
	internal static class SpinnerHost
	{
		private static LoadingForm _form;
		private static Thread _thread;
		private static readonly object _lock = new object();

		public static void Show()
		{
			lock (_lock)
			{
				if (_thread != null) return;  // idempotent

				var ready = new ManualResetEventSlim(false);
				_thread = new Thread(() =>
				{
					_form = new LoadingForm();
					_form.Shown += (_, __) => ready.Set();
					WinForms.Application.Run(_form);
				});
				_thread.SetApartmentState(ApartmentState.STA);
				_thread.IsBackground = true;
				_thread.Start();
				ready.Wait();
			}
		}

		public static void Hide()
		{
			LoadingForm f;
			lock (_lock)
			{
				if (_thread == null) return;  // idempotent
				f = _form;
				_thread = null;
				_form = null;
			}

			try
			{
				if (f != null && !f.IsDisposed)
					f.BeginInvoke(new Action(() => { try { f.Close(); } catch { } }));
			}
			catch { /* form may have already closed */ }
		}
	}

	/// <summary>
	/// Full-screen black borderless form with centred "Loading ATMML..."
	/// text. Painted once, never updated. Cannot freeze because there's
	/// nothing to animate.
	/// </summary>
	internal sealed class LoadingForm : WinForms.Form
	{
		public LoadingForm()
		{
			FormBorderStyle = WinForms.FormBorderStyle.None;
			WindowState = WinForms.FormWindowState.Maximized;
			BackColor = Color.Black;
			TopMost = true;
			ShowInTaskbar = false;
			DoubleBuffered = true;

			var label = new WinForms.Label
			{
				Text = "Loading ATMML...",
				Font = new Font("Segoe UI Light", 28f, FontStyle.Regular),
				ForeColor = Color.FromArgb(0, 0xCC, 0xFF),  // CMR brand cyan
				BackColor = Color.Transparent,
				AutoSize = true,
				TextAlign = System.Drawing.ContentAlignment.MiddleCenter
			};

			Controls.Add(label);

			// Centre the label after the form is shown so ClientSize is final.
			Shown += (_, __) =>
			{
				label.Location = new Point(
					(ClientSize.Width  - label.Width)  / 2,
					(ClientSize.Height - label.Height) / 2);
			};
		}
	}
}
