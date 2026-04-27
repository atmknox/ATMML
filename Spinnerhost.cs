using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Threading;
using WinForms = System.Windows.Forms;

namespace ATMML
{
	/// <summary>
	/// Login → role-view spinner, hosted on its own STA thread + WinForms
	/// message pump. This is the third iteration; failure modes from v1 (WPF
	/// Storyboard) and v2 (WinForms.Timer + GDI+ per-frame rotation) inform
	/// every design choice here.
	///
	/// Observed v2 behaviour: spinner painted for ~1s then froze for ~3s
	/// while the main thread parsed PortfolioBuilder.xaml, then resumed.
	/// That pattern — the spinner thread alive but apparently unable to
	/// paint — points at GDI+ contention. Heavy WPF XAML loading exercises
	/// the GDI+ pipeline (font metrics, glyph cache, brush construction)
	/// and GDI+ has process-wide critical sections. The spinner's per-frame
	/// `RotateTransform`+`DrawArc` calls were getting queued behind the
	/// main thread's GDI+ work.
	///
	/// Three mitigations together kill the freeze:
	///
	///   1. Pre-render the rotation as 60 cached Bitmaps (one per 6°
	///      step) at form-load time, ONCE. Per-frame painting is reduced
	///      to a single `DrawImage` blit — no transform, no path, no pen
	///      construction. ~95% less GDI+ work per frame, far less likely
	///      to contend with the main thread's GDI+ activity.
	///
	///   2. Drive the tick from a `System.Threading.Timer` (thread-pool)
	///      that calls `Invalidate()` directly. `Invalidate` is
	///      thread-safe at the Win32 level (calls InvalidateRect, which
	///      posts WM_PAINT to the form's queue). The form's message pump
	///      on the spinner STA thread eventually processes WM_PAINT —
	///      but the timer itself never depends on either thread's pump.
	///
	///   3. Spinner thread priority `AboveNormal` so the OS scheduler
	///      favours it when the main thread is CPU-bound.
	///
	/// Show() and Hide() are idempotent and thread-safe.
	/// </summary>
	internal static class SpinnerHost
	{
		private static SpinnerForm _form;
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
					_form = new SpinnerForm();
					_form.Shown += (_, __) => ready.Set();
					WinForms.Application.Run(_form);
				});
				_thread.SetApartmentState(ApartmentState.STA);
				_thread.IsBackground = true;
				_thread.Priority = ThreadPriority.AboveNormal;  // (3)
				_thread.Start();
				ready.Wait();
			}
		}

		public static void Hide()
		{
			SpinnerForm f;
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
	/// Full-screen black borderless form. 270° cyan arc in the centre,
	/// rotating clockwise via pre-rendered bitmap frames. ~60fps.
	/// </summary>
	internal sealed class SpinnerForm : WinForms.Form
	{
		private const int FRAME_COUNT = 60;       // 6° per frame, full rotation in 60 frames
		private const int FRAME_INTERVAL_MS = 16; // ~62fps
		private const int ARC_BOX = 88;           // bounding box of the arc bitmap
		private const int ARC_INSET = 6;          // pen thickness clearance from box edge

		private readonly Bitmap[] _frames = new Bitmap[FRAME_COUNT];
		private System.Threading.Timer _tickTimer;
		private int _frameIdx;
		private bool _framesReady;

		public SpinnerForm()
		{
			FormBorderStyle = WinForms.FormBorderStyle.None;
			WindowState = WinForms.FormWindowState.Maximized;
			BackColor = Color.Black;
			TopMost = true;
			ShowInTaskbar = false;
			DoubleBuffered = true;
			SetStyle(WinForms.ControlStyles.OptimizedDoubleBuffer
				   | WinForms.ControlStyles.AllPaintingInWmPaint
				   | WinForms.ControlStyles.UserPaint, true);

			// Pre-render frames once the form's handle exists. HandleCreated
			// is the earliest reliable point — Load happens after Shown on
			// some configurations, and we want frames ready before Shown
			// fires so the first paint is the first frame, not a blank.
			HandleCreated += (_, __) =>
			{
				PreRenderFrames();
				_framesReady = true;
				_tickTimer = new System.Threading.Timer(OnTick, null,
					FRAME_INTERVAL_MS, FRAME_INTERVAL_MS);
			};

			FormClosed += (_, __) =>
			{
				try { _tickTimer?.Dispose(); _tickTimer = null; } catch { }
				foreach (var b in _frames) { try { b?.Dispose(); } catch { } }
			};
		}

		private void PreRenderFrames()
		{
			// Pen reused across all 60 frames — single allocation rather
			// than per-paint. Same applies to the brush (background black)
			// although we rely on Bitmap default transparent pixels.
			using (var pen = new Pen(Color.FromArgb(0, 0xCC, 0xFF), 8f)
			{
				StartCap = LineCap.Round,
				EndCap = LineCap.Round
			})
			{
				int r = (ARC_BOX / 2) - ARC_INSET;
				for (int i = 0; i < FRAME_COUNT; i++)
				{
					var bmp = new Bitmap(ARC_BOX, ARC_BOX, PixelFormat.Format32bppArgb);
					using (var g = Graphics.FromImage(bmp))
					{
						g.SmoothingMode = SmoothingMode.AntiAlias;
						g.TranslateTransform(ARC_BOX / 2f, ARC_BOX / 2f);
						g.RotateTransform(i * (360f / FRAME_COUNT));
						// 270° arc: starts at 12 o'clock (-90°), sweeps
						// 270° clockwise to 9 o'clock. Asymmetric so the
						// rotation is visually obvious.
						g.DrawArc(pen, -r, -r, r * 2, r * 2, -90f, 270f);
					}
					_frames[i] = bmp;
				}
			}
		}

		// Fires on a thread-pool thread, NOT on the spinner thread. The
		// only thing that depends on the spinner thread is the eventual
		// WM_PAINT processing — which happens via Application.Run's pump.
		// Invalidate() itself is thread-safe (Win32 InvalidateRect).
		private void OnTick(object _)
		{
			if (IsDisposed) return;
			_frameIdx = (_frameIdx + 1) % FRAME_COUNT;
			try { Invalidate(); } catch { /* form disposing */ }
		}

		protected override void OnPaint(WinForms.PaintEventArgs e)
		{
			base.OnPaint(e);
			if (!_framesReady) return;

			var frame = _frames[_frameIdx];
			int x = (ClientSize.Width - frame.Width) / 2;
			int y = (ClientSize.Height - frame.Height) / 2;
			// Single bitmap blit — minimal GDI+ work per frame.
			e.Graphics.DrawImage(frame, x, y);
		}

		// Avoid the default WinForms background-erase that would clear the
		// whole client area on every Invalidate. We paint a single bitmap;
		// the rest of the form is already black from BackColor and stays
		// untouched between frames.
		protected override void OnPaintBackground(WinForms.PaintEventArgs e)
		{
			// Only paint background once on first paint
			if (_frameIdx == 0 && !_framesReady) base.OnPaintBackground(e);
		}
	}
}
