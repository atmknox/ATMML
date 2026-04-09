using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ATMML
{
	public class AlertController
	{
		// ── State ─────────────────────────────────────────────────────────────────

		private readonly Dictionary<string, bool>    _acknowledged       = new();
		private readonly Dictionary<string, Button>  _buttons            = new();
		private readonly Dictionary<string, TextBox> _labels             = new();
		private readonly Dictionary<string, string>  _displayOverride    = new();
		private readonly Dictionary<string, Brush>   _colorOverride      = new();
		private readonly Dictionary<string, Brush>   _labelColorOverride = new();
		private readonly DispatcherTimer _timer;

		// ── Data-source lambdas ───────────────────────────────────────────────────

		public Func<bool> CheckFlexOne       { get; set; } = () => true;
		public Func<bool> CheckBloomberg     { get; set; } = () => true;
		public Func<bool> CheckMktNeutral    { get; set; } = () => true;
		public Func<bool> CheckVolNeutral    { get; set; } = () => true;
		public Func<bool> CheckMaxPosition   { get; set; } = () => true;
		public Func<bool> CheckGrossBook     { get; set; } = () => true;
		public Func<bool> CheckNetExposure   { get; set; } = () => true;
		public Func<bool> CheckSectorGross   { get; set; } = () => true;
		public Func<bool> CheckSectorNet     { get; set; } = () => true;
		public Func<bool> CheckIndustryGross { get; set; } = () => true;
		public Func<bool> CheckIndustryNet   { get; set; } = () => true;
		public Func<bool> CheckSubIndGross   { get; set; } = () => true;
		public Func<bool> CheckSubIndNet     { get; set; } = () => true;
		public Func<bool> CheckMaxPredVol    { get; set; } = () => true;
		public Func<bool> CheckMVaR95        { get; set; } = () => true;
		public Func<bool> CheckIdioRisk      { get; set; } = () => true;
		public Func<bool> CheckEqStress5     { get; set; } = () => true;
		public Func<bool> CheckEqStress10    { get; set; } = () => true;
		public Func<bool> CheckIntradayDD    { get; set; } = () => true;
		// Extended
		public Func<bool> CheckUtilization   { get; set; } = () => true;
		public Func<bool> CheckMaxVaR95      { get; set; } = () => true;
		public Func<bool> CheckCVaR95        { get; set; } = () => true;
		public Func<bool> CheckTop5Long      { get; set; } = () => true;
		public Func<bool> CheckTop5Short     { get; set; } = () => true;
		public Func<bool> CheckTop10Long     { get; set; } = () => true;
		public Func<bool> CheckTop10Short    { get; set; } = () => true;
		public Func<bool> CheckADV20         { get; set; } = () => true;
		public Func<bool> CheckADV50         { get; set; } = () => true;
		public Func<bool> CheckADV100        { get; set; } = () => true;
		public Func<bool> CheckLargeCapGross { get; set; } = () => true;
		public Func<bool> CheckLargeCapNet   { get; set; } = () => true;
		public Func<bool> CheckMidCapGross   { get; set; } = () => true;
		public Func<bool> CheckMidCapNet     { get; set; } = () => true;
		public Func<bool> CheckSmallCapGross { get; set; } = () => true;
		public Func<bool> CheckSmallCapNet   { get; set; } = () => true;

		// ── Constructor ───────────────────────────────────────────────────────────

		public AlertController(int pollSeconds = 30)
		{
			_timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(pollSeconds) };
			_timer.Tick += (_, _) => EvaluateAll();
		}

		// ── Registration ─────────────────────────────────────────────────────────

		/// <summary>
		/// Register a button and optional label with the controller.
		/// Called from UserControl_Loaded after the visual tree is built.
		/// Preserves acknowledged state on re-registration.
		/// </summary>
		public void Register(string key, Button button, TextBox label = null)
		{
			if (!_acknowledged.ContainsKey(key))
				_acknowledged[key] = false;
			if (button != null) _buttons[key] = button;
			if (label  != null) _labels[key]  = label;
		}

		public void RegisterFromHost(string key, FrameworkElement host, TextBox label = null)
		{
			if (!_acknowledged.ContainsKey(key))
				_acknowledged[key] = false;
			var btn = FindInVisualTree<Button>(host, "Btn" + key);
			if (btn != null) _buttons[key] = btn;
			var lbl = label ?? FindInVisualTree<TextBox>(host, "Lbl" + key);
			if (lbl != null) _labels[key] = lbl;
		}

		private static T FindInVisualTree<T>(DependencyObject parent, string name)
			where T : FrameworkElement
		{
			if (parent == null) return null;
			int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
			for (int i = 0; i < count; i++)
			{
				var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
				if (child is T fe && fe.Name == name) return fe;
				var result = FindInVisualTree<T>(child, name);
				if (result != null) return result;
			}
			return null;
		}

		// ── Lifecycle ─────────────────────────────────────────────────────────────

		public void Start()
		{
			EvaluateAll();
			_timer.Start();
		}

		public void Stop()         => _timer.Stop();
		public void ForceRefresh() => EvaluateAll();

		// ── Evaluation ────────────────────────────────────────────────────────────

		private void EvaluateAll()
		{
			SetCircle("FlexOne",       CheckFlexOne());
			SetCircle("Bloomberg",     CheckBloomberg());
			SetCircle("MktNeutral",    CheckMktNeutral());
			SetCircle("VolNeutral",    CheckVolNeutral());
			SetCircle("MaxPosition",   CheckMaxPosition());
			SetCircle("GrossBook",     CheckGrossBook());
			SetCircle("NetExposure",   CheckNetExposure());
			SetCircle("SectorGross",   CheckSectorGross());
			SetCircle("SectorNet",     CheckSectorNet());
			SetCircle("IndustryGross", CheckIndustryGross());
			SetCircle("IndustryNet",   CheckIndustryNet());
			SetCircle("SubIndGross",   CheckSubIndGross());
			SetCircle("SubIndNet",     CheckSubIndNet());
			SetCircle("MaxPredVol",    CheckMaxPredVol());
			SetCircle("MVaR95",        CheckMVaR95());
			SetCircle("IdioRisk",      CheckIdioRisk());
			SetCircle("EqStress5",     CheckEqStress5());
			SetCircle("EqStress10",    CheckEqStress10());
			SetCircle("IntradayDD",    CheckIntradayDD());
			// Extended
			SetCircle("Utilization",   CheckUtilization());
			SetCircle("MaxVaR95",      CheckMaxVaR95());
			SetCircle("CVaR95",        CheckCVaR95());
			SetCircle("Top5Long",      CheckTop5Long());
			SetCircle("Top5Short",     CheckTop5Short());
			SetCircle("Top10Long",     CheckTop10Long());
			SetCircle("Top10Short",    CheckTop10Short());
			SetCircle("ADV20",         CheckADV20());
			SetCircle("ADV50",         CheckADV50());
			SetCircle("ADV100",        CheckADV100());
			SetCircle("LargeCapGross", CheckLargeCapGross());
			SetCircle("LargeCapNet",   CheckLargeCapNet());
			SetCircle("MidCapGross",   CheckMidCapGross());
			SetCircle("MidCapNet",     CheckMidCapNet());
			SetCircle("SmallCapGross", CheckSmallCapGross());
			SetCircle("SmallCapNet",   CheckSmallCapNet());
			ApplyLabelOverrides();
		}

		private void SetCircle(string key, bool healthy)
		{
			if (!_buttons.TryGetValue(key, out var btn)) return;

			if (healthy) _acknowledged[key] = false;

			btn.Foreground = healthy
				? Brushes.Lime
				: (_acknowledged.TryGetValue(key, out bool acked) && acked
					? Brushes.Yellow
					: Brushes.Red);

			if (_colorOverride.TryGetValue(key, out var colorOverride))
				btn.Foreground = colorOverride;

			if (_displayOverride.TryGetValue(key, out var text))
			{
				btn.Content  = text;
				btn.FontSize = 9;
			}
		}

		private void ApplyLabelOverrides()
		{
			foreach (var kvp in _displayOverride)
				if (_labels.TryGetValue(kvp.Key, out var lbl))
					lbl.Text = kvp.Value;

			foreach (var kvp in _labelColorOverride)
				if (_labels.TryGetValue(kvp.Key, out var lbl))
					lbl.Foreground = kvp.Value;
		}

		// ── Public display methods ────────────────────────────────────────────────

		public void SetLabelDisplay(string key, string text, Brush color)
		{
			if (text == null)
			{
				_displayOverride.Remove(key);
				_labelColorOverride.Remove(key);
			}
			else
			{
				_displayOverride[key]    = text;
				_labelColorOverride[key] = color ?? Brushes.Lime;
			}
		}

		public void SetDisplay(string key, string labelText, Brush color = null)
		{
			if (labelText == null)
			{
				_displayOverride.Remove(key);
				_colorOverride.Remove(key);
			}
			else
			{
				_displayOverride[key] = labelText;
				if (color != null) _colorOverride[key] = color;
				else               _colorOverride.Remove(key);
			}
		}

		// ── Operator interaction ──────────────────────────────────────────────────

		public void HandleLabelClick(string key)
		{
			if (!_buttons.TryGetValue(key, out var btn)) return;
			if (btn.Foreground == Brushes.Lime) return;

			bool currentlyAcked = _acknowledged.TryGetValue(key, out bool a) && a;

			if (!currentlyAcked)
			{
				_acknowledged[key] = true;
				btn.Foreground     = Brushes.Yellow;
			}
			else
			{
				_acknowledged[key] = false;
				btn.Foreground     = Brushes.Red;
			}
		}
	}
}
