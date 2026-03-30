using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ATMML.Monitoring
{
	/// <summary>
	/// Represents a single monitored process/risk limit with a visual circle indicator.
	/// Lime = healthy, Red = breached, Yellow = breached but acknowledged by operator.
	/// </summary>
	public class AlertItem : INotifyPropertyChanged
	{
		// ── State ────────────────────────────────────────────────────────────────

		private string _name;
		private bool _isHealthy = true;
		private bool _isAcknowledged = false;
		private string _tooltipDetail = string.Empty;
		private DateTime _lastChecked = DateTime.MinValue;
		private DateTime _lastBreachTime = DateTime.MinValue;

		// ── Identity ─────────────────────────────────────────────────────────────

		/// <summary>Stable key used internally (not editable by operator).</summary>
		public string Key { get; init; }

		/// <summary>Display name shown in the TextBox — operator can relabel.</summary>
		public string Name
		{
			get => _name;
			set { _name = value; OnPropertyChanged(); }
		}

		/// <summary>Friendly description shown in the ToolTip header.</summary>
		public string Description { get; init; }

		// ── Health ───────────────────────────────────────────────────────────────

		/// <summary>True when the condition check passes (no breach).</summary>
		public bool IsHealthy
		{
			get => _isHealthy;
			set
			{
				if (_isHealthy == value) return;
				_isHealthy = value;
				if (!value) _lastBreachTime = DateTime.Now;
				// Clearing a breach clears acknowledgement automatically.
				if (value) _isAcknowledged = false;
				OnPropertyChanged();
				OnPropertyChanged(nameof(CircleBrush));
				OnPropertyChanged(nameof(TooltipText));
			}
		}

		/// <summary>
		/// Set true when the operator clicks the name TextBox on a red alert.
		/// Turns the circle Yellow — "I see it, leave me alone for now."
		/// Clicking again on Yellow restores full red monitoring state.
		/// </summary>
		public bool IsAcknowledged
		{
			get => _isAcknowledged;
			set
			{
				_isAcknowledged = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(CircleBrush));
				OnPropertyChanged(nameof(TooltipText));
			}
		}

		/// <summary>Human-readable detail set by the condition check on each evaluation.</summary>
		public string TooltipDetail
		{
			get => _tooltipDetail;
			set { _tooltipDetail = value; OnPropertyChanged(); OnPropertyChanged(nameof(TooltipText)); }
		}

		public DateTime LastChecked
		{
			get => _lastChecked;
			set { _lastChecked = value; OnPropertyChanged(); OnPropertyChanged(nameof(TooltipText)); }
		}

		// ── Derived UI ───────────────────────────────────────────────────────────

		/// <summary>
		/// Lime  → healthy
		/// Yellow → breached but acknowledged by operator
		/// Red    → breached and active alarm
		/// </summary>
		public Brush CircleBrush
		{
			get
			{
				if (IsHealthy) return Brushes.Lime;
				if (IsAcknowledged) return Brushes.Yellow;
				return Brushes.Red;
			}
		}

		public string TooltipText
		{
			get
			{
				string status = IsHealthy ? "OK" : (IsAcknowledged ? "ACKNOWLEDGED" : "BREACH");
				string breachLine = !IsHealthy && _lastBreachTime != DateTime.MinValue
					? $"\nFirst breach: {_lastBreachTime:HH:mm:ss}"
					: string.Empty;
				string checkedLine = LastChecked != DateTime.MinValue
					? $"\nLast checked: {LastChecked:HH:mm:ss}"
					: string.Empty;
				string detail = !string.IsNullOrWhiteSpace(TooltipDetail)
					? $"\n{TooltipDetail}"
					: string.Empty;

				return $"[{status}] {Description}{breachLine}{checkedLine}{detail}";
			}
		}

		// ── Operator Interaction ─────────────────────────────────────────────────

		/// <summary>
		/// Called when operator clicks the name TextBox.
		///   • Lime  → no-op (already healthy).
		///   • Red   → Acknowledge (Yellow). Subsequent checks still run.
		///   • Yellow → Restore full monitoring (back to Red if still breached).
		/// </summary>
		public void HandleNameClick()
		{
			if (IsHealthy) return;

			if (!IsAcknowledged)
				IsAcknowledged = true;   // Red → Yellow
			else
				IsAcknowledged = false;  // Yellow → Red (restore full alarm)
		}

		// ── INotifyPropertyChanged ───────────────────────────────────────────────

		public event PropertyChangedEventHandler PropertyChanged;
		protected void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}