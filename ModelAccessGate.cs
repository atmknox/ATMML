using System;
using System.Collections.Generic;
using System.IO;

namespace ATMML
{
	/// <summary>
	/// Centralised RBAC check for LIVE vs TEST model classification.
	/// Reads the lightweight name→IsLiveMode index at models\Models\_meta
	/// (maintained by Portfolio_Builder.saveModelMeta / loadModelMeta).
	///
	/// The meta file is a simple TSV: each line is "modelName<TAB>1" for LIVE
	/// or "modelName<TAB>0" for TEST. Used by all views that need to filter
	/// ML portfolios: Timing, Charts, Alerts, ScanDialog.
	///
	/// Cached for 5 seconds to avoid re-reading the file on every filter call
	/// (nav rebuilds happen frequently). The file is rewritten whenever a model
	/// is flipped LIVE↔TEST in Portfolio Management, so a short cache is safe.
	/// </summary>
	internal static class ModelAccessGate
	{
		private static Dictionary<string, bool> _cache = new Dictionary<string, bool>(StringComparer.Ordinal);
		private static DateTime _cacheAsOf = DateTime.MinValue;
		private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);
		private static readonly object _lock = new object();

		/// <summary>
		/// Returns true iff the named model should be visible to a non-admin user.
		/// LIVE models are visible. TEST models and unknown names are hidden.
		/// Default-deny: if the meta file can't be read, or the name isn't in it,
		/// treat as TEST (hidden from non-admin). Admins always see everything;
		/// callers should bypass this check entirely when the user is Admin.
		/// </summary>
		public static bool IsLive(string modelName)
		{
			if (string.IsNullOrWhiteSpace(modelName)) return false;
			var snapshot = GetMeta();
			// If not in meta, default-deny for non-admin (caller already gated IsAdmin).
			return snapshot.TryGetValue(modelName, out bool live) && live;
		}

		/// <summary>
		/// Returns the cached copy of the name→IsLiveMode map. Rereads the meta
		/// file from disk if the cache is older than CacheTtl.
		/// </summary>
		private static Dictionary<string, bool> GetMeta()
		{
			lock (_lock)
			{
				if (DateTime.UtcNow - _cacheAsOf < CacheTtl) return _cache;

				var fresh = new Dictionary<string, bool>(StringComparer.Ordinal);
				try
				{
					string data = MainView.LoadUserData(@"models\Models\_meta");
					if (!string.IsNullOrEmpty(data))
					{
						foreach (var rawLine in data.Split('\n'))
						{
							var line = rawLine.Trim();
							if (line.Length == 0) continue;
							var parts = line.Split('\t');
							if (parts.Length == 2)
								fresh[parts[0]] = (parts[1] == "1");
						}
					}
				}
				catch { /* keep last-known cache on error */ }

				_cache = fresh;
				_cacheAsOf = DateTime.UtcNow;
				return _cache;
			}
		}

		/// <summary>
		/// Force the next IsLive() call to reread from disk.
		/// Optional — used by Portfolio Management after saving a model flip.
		/// </summary>
		public static void InvalidateCache()
		{
			lock (_lock)
			{
				_cacheAsOf = DateTime.MinValue;
			}
		}
	}
}
