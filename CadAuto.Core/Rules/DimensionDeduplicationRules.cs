using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public sealed class DimensionDeduplicationRules
{
	private readonly DimensionRuleConfig _config;

	public DimensionDeduplicationRules(DimensionRuleConfig config)
	{
		if (config == null)
		{
			throw new ArgumentNullException("config");
		}
		_config = config;
	}

	public bool CanSuppressMirroredNormalDimension(DimensionDeduplicationItem item)
	{
		return item.Kind == DimensionKind.Normal && !item.ForceOuterLevel;
	}

	public bool CanSuppressMirroredHoleRelatedDimension(DimensionDeduplicationItem first, DimensionDeduplicationItem second)
	{
		return !first.ForceOuterLevel && !second.ForceOuterLevel && (first.Kind == DimensionKind.HoleLocation || second.Kind == DimensionKind.HoleLocation);
	}

	public bool IsSameMeasuredDimension(DimensionDeduplicationItem first, DimensionDeduplicationItem second, bool horizontal)
	{
		Tuple<double, double> tuple = ComputeArrowInterval(first, horizontal);
		Tuple<double, double> tuple2 = ComputeArrowInterval(second, horizontal);
		if (Math.Abs(tuple.Item1 - tuple2.Item1) > _config.GeometryTolerance)
		{
			return false;
		}
		if (Math.Abs(tuple.Item2 - tuple2.Item2) > _config.GeometryTolerance)
		{
			return false;
		}
		return Math.Abs(first.Span - second.Span) <= _config.GeometryTolerance;
	}

	public bool IsDuplicate(DimensionDeduplicationItem first, DimensionDeduplicationItem second, bool horizontal)
	{
		Tuple<double, double> tuple = ComputeArrowInterval(first, horizontal);
		Tuple<double, double> tuple2 = ComputeArrowInterval(second, horizontal);
		if (Math.Abs(tuple.Item1 - tuple2.Item1) > _config.GeometryTolerance)
		{
			return false;
		}
		if (Math.Abs(tuple.Item2 - tuple2.Item2) > _config.GeometryTolerance)
		{
			return false;
		}
		string a = first.OverrideText ?? string.Empty;
		string b = second.OverrideText ?? string.Empty;
		return string.Equals(a, b, StringComparison.Ordinal);
	}

	public bool IsRightStructureHeightDuplicatingOverallHeight(DimensionDeduplicationItem right, DimensionDeduplicationItem overall)
	{
		return IsRightStructureHeight(right) && overall.Kind == DimensionKind.OverallHeight && IsSameVerticalInterval(right, overall);
	}

	public bool IsLeftStructureHeightCoveredByRight(DimensionDeduplicationItem left, DimensionDeduplicationItem right)
	{
		if (!IsLeftStructureHeight(left) || !IsRightStructureHeight(right))
		{
			return false;
		}
		double num = Math.Min(left.FirstPoint.Y, left.SecondPoint.Y);
		double num2 = Math.Max(left.FirstPoint.Y, left.SecondPoint.Y);
		double num3 = Math.Min(right.FirstPoint.Y, right.SecondPoint.Y);
		double num4 = Math.Max(right.FirstPoint.Y, right.SecondPoint.Y);
		if (!(Math.Abs(num - num3) <= _config.GeometryTolerance) && !(Math.Abs(num - num4) <= _config.GeometryTolerance) && !(Math.Abs(num2 - num3) <= _config.GeometryTolerance) && !(Math.Abs(num2 - num4) <= _config.GeometryTolerance))
		{
			return false;
		}
		return num >= num3 - _config.GeometryTolerance && num2 <= num4 + _config.GeometryTolerance && GetSpan(right, horizontal: false) >= GetSpan(left, horizontal: false) - _config.GeometryTolerance;
	}

	public bool IsLeftStructureHeight(DimensionDeduplicationItem item)
	{
		return item.Kind == DimensionKind.Normal && !item.ForceOuterLevel && string.Equals(item.DebugRole, "LeftStructHeight", StringComparison.Ordinal);
	}

	public bool IsRightStructureHeight(DimensionDeduplicationItem item)
	{
		return item.Kind == DimensionKind.Normal && !item.ForceOuterLevel && string.Equals(item.DebugRole, "RightStructHeight", StringComparison.Ordinal);
	}

	public bool IsSameVerticalInterval(DimensionDeduplicationItem first, DimensionDeduplicationItem second)
	{
		return Math.Abs(Math.Min(first.FirstPoint.Y, first.SecondPoint.Y) - Math.Min(second.FirstPoint.Y, second.SecondPoint.Y)) <= _config.GeometryTolerance && Math.Abs(Math.Max(first.FirstPoint.Y, first.SecondPoint.Y) - Math.Max(second.FirstPoint.Y, second.SecondPoint.Y)) <= _config.GeometryTolerance && Math.Abs(GetSpan(first, horizontal: false) - GetSpan(second, horizontal: false)) <= _config.GeometryTolerance;
	}

	public int CompareDuplicatePreference(DimensionDeduplicationItem first, DimensionDeduplicationItem second)
	{
		bool flag = IsOverall(first.Kind);
		bool flag2 = IsOverall(second.Kind);
		if (flag != flag2)
		{
			return flag ? 1 : (-1);
		}
		double? num = TryExtractTolerance(first.OverrideText);
		double? num2 = TryExtractTolerance(second.OverrideText);
		if (num.HasValue && !num2.HasValue)
		{
			return 1;
		}
		if (!num.HasValue && num2.HasValue)
		{
			return -1;
		}
		if (num.HasValue && num2.HasValue)
		{
			int num3 = num2.Value.CompareTo(num.Value);
			if (num3 != 0)
			{
				return num3;
			}
		}
		int num4 = GetDimensionPreferenceRank(second.Kind).CompareTo(GetDimensionPreferenceRank(first.Kind));
		if (num4 != 0)
		{
			return num4;
		}
		if (first.ForceOuterLevel != second.ForceOuterLevel)
		{
			return first.ForceOuterLevel ? 1 : (-1);
		}
		bool flag3 = !string.IsNullOrWhiteSpace(first.OverrideText);
		bool flag4 = !string.IsNullOrWhiteSpace(second.OverrideText);
		if (flag3 != flag4)
		{
			return flag3 ? 1 : (-1);
		}
		int num5 = GetDebugRolePreferenceRank(second.DebugRole).CompareTo(GetDebugRolePreferenceRank(first.DebugRole));
		if (num5 != 0)
		{
			return num5;
		}
		return 0;
	}

	private static bool IsOverall(DimensionKind kind)
	{
		return kind == DimensionKind.OverallWidth || kind == DimensionKind.OverallHeight;
	}

	private static int GetDebugRolePreferenceRank(string debugRole)
	{
		return (!string.Equals(debugRole, "OutlineSegment", StringComparison.Ordinal)) ? 1 : 0;
	}

	public static Tuple<double, double> ComputeArrowInterval(DimensionDeduplicationItem item, bool horizontal)
	{
		double val = (horizontal ? item.FirstPoint.X : item.FirstPoint.Y);
		double val2 = (horizontal ? item.SecondPoint.X : item.SecondPoint.Y);
		return Tuple.Create(Math.Min(val, val2), Math.Max(val, val2));
	}

	public static double GetSpan(DimensionDeduplicationItem item, bool horizontal)
	{
		return horizontal ? Math.Abs(item.SecondPoint.X - item.FirstPoint.X) : Math.Abs(item.SecondPoint.Y - item.FirstPoint.Y);
	}

	/// <summary>
	/// True when two dimensions form a real contiguous partition of the overall interval
	/// along the measurement axis (X if horizontal, Y if vertical).
	/// Requires endpoint intervals — not merely spanA + spanB ≈ overallSpan.
	/// Conditions: no interior overlap, no gap, abut end-to-end, union equals overall.
	/// </summary>
	public bool FormsCompleteOverallPartition(DimensionDeduplicationItem first, DimensionDeduplicationItem second, DimensionDeduplicationItem overall, bool horizontal)
	{
		if (first == null || second == null || overall == null)
		{
			return false;
		}
		Tuple<double, double> overallInterval = ComputeArrowInterval(overall, horizontal);
		return FormsCompleteOverallPartition(first, second, overallInterval.Item1, overallInterval.Item2, horizontal);
	}

	/// <summary>
	/// Same as <see cref="FormsCompleteOverallPartition(DimensionDeduplicationItem, DimensionDeduplicationItem, DimensionDeduplicationItem, bool)"/>
	/// but overall is given as an axis interval [overallMin, overallMax].
	/// </summary>
	public bool FormsCompleteOverallPartition(DimensionDeduplicationItem first, DimensionDeduplicationItem second, double overallMin, double overallMax, bool horizontal)
	{
		if (first == null || second == null)
		{
			return false;
		}
		double tol = _config.GeometryTolerance;
		double oMin = Math.Min(overallMin, overallMax);
		double oMax = Math.Max(overallMin, overallMax);
		if (oMax - oMin <= tol)
		{
			return false;
		}
		Tuple<double, double> a = ComputeArrowInterval(first, horizontal);
		Tuple<double, double> b = ComputeArrowInterval(second, horizontal);
		if (a.Item2 - a.Item1 <= tol || b.Item2 - b.Item1 <= tol)
		{
			return false;
		}
		// Order intervals by start (then by end for stable tie-break).
		Tuple<double, double> low;
		Tuple<double, double> high;
		if (a.Item1 < b.Item1 - tol || (Math.Abs(a.Item1 - b.Item1) <= tol && a.Item2 <= b.Item2 + tol))
		{
			low = a;
			high = b;
		}
		else
		{
			low = b;
			high = a;
		}
		// Merged range must match overall endpoints.
		if (Math.Abs(low.Item1 - oMin) > tol)
		{
			return false;
		}
		if (Math.Abs(high.Item2 - oMax) > tol)
		{
			return false;
		}
		// Contiguous abutment: no interior overlap and no gap (low.max ≈ high.min).
		if (Math.Abs(low.Item2 - high.Item1) > tol)
		{
			return false;
		}
		return true;
	}

	/// <summary>
	/// True when 2+ intervals form a contiguous abutment chain that exactly covers
	/// [overallMin, overallMax] (no gap, no interior overlap, ends match overall).
	/// Intervals may be unordered; they are sorted by start before validation.
	/// </summary>
	public bool FormsCompleteOverallPartitionChain(IList<Tuple<double, double>> intervals, double overallMin, double overallMax)
	{
		if (intervals == null || intervals.Count < 2)
		{
			return false;
		}
		double tol = _config.GeometryTolerance;
		double oMin = Math.Min(overallMin, overallMax);
		double oMax = Math.Max(overallMin, overallMax);
		if (oMax - oMin <= tol)
		{
			return false;
		}
		List<Tuple<double, double>> ordered = intervals
			.Where((Tuple<double, double> iv) => iv != null && iv.Item2 - iv.Item1 > tol)
			.OrderBy((Tuple<double, double> iv) => iv.Item1)
			.ThenBy((Tuple<double, double> iv) => iv.Item2)
			.ToList();
		if (ordered.Count < 2)
		{
			return false;
		}
		if (Math.Abs(ordered[0].Item1 - oMin) > tol)
		{
			return false;
		}
		if (Math.Abs(ordered[ordered.Count - 1].Item2 - oMax) > tol)
		{
			return false;
		}
		for (int i = 0; i < ordered.Count - 1; i++)
		{
			// Must abut: previous max ≈ next min (no gap, no interior overlap).
			if (Math.Abs(ordered[i].Item2 - ordered[i + 1].Item1) > tol)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>
	/// BuildOverallPartitionChain: select a subset of items (size ≥ 2) whose measurement
	/// intervals form a complete contiguous cover of overall via DFS/backtracking.
	/// Candidates at each step are ordered by priority (Structure before OutlineSegment,
	/// then longer reach, then stable input order) but every candidate is tried on failure
	/// of the preferred path — no single-path greedy dead-end.
	/// Each piece must lie within overall (tol); adjacent pieces must abut (no gap / interior overlap).
	/// Returns null when no such cover exists.
	/// </summary>
	public IList<DimensionDeduplicationItem> FindCompleteOverallPartitionChain(IList<DimensionDeduplicationItem> items, double overallMin, double overallMax, bool horizontal)
	{
		if (items == null || items.Count < 2)
		{
			return null;
		}
		double tol = _config.GeometryTolerance;
		double oMin = Math.Min(overallMin, overallMax);
		double oMax = Math.Max(overallMin, overallMax);
		if (oMax - oMin <= tol)
		{
			return null;
		}
		// Preserve stable input order for tie-break.
		List<PartitionCandidate> pool = new List<PartitionCandidate>();
		for (int i = 0; i < items.Count; i++)
		{
			DimensionDeduplicationItem item = items[i];
			if (item == null)
			{
				continue;
			}
			Tuple<double, double> iv = ComputeArrowInterval(item, horizontal);
			if (iv.Item2 - iv.Item1 <= tol)
			{
				continue;
			}
			if (iv.Item1 < oMin - tol || iv.Item2 > oMax + tol)
			{
				continue;
			}
			pool.Add(new PartitionCandidate
			{
				Item = item,
				Min = iv.Item1,
				Max = iv.Item2,
				InputIndex = i
			});
		}
		if (pool.Count < 2)
		{
			return null;
		}
		List<DimensionDeduplicationItem> chain = new List<DimensionDeduplicationItem>();
		HashSet<int> used = new HashSet<int>();
		if (!TryBuildOverallPartitionChain(pool, used, chain, oMin, oMax, tol))
		{
			return null;
		}
		if (chain.Count < 2)
		{
			return null;
		}
		return chain;
	}

	/// <summary>
	/// DFS from <paramref name="current"/> toward overall max. Backtracks when a branch
	/// cannot advance; tries all candidates whose min abuts current (priority order).
	/// </summary>
	private bool TryBuildOverallPartitionChain(
		List<PartitionCandidate> pool,
		HashSet<int> used,
		List<DimensionDeduplicationItem> chain,
		double current,
		double oMax,
		double tol)
	{
		if (Math.Abs(current - oMax) <= tol)
		{
			return chain.Count >= 2;
		}
		if (current >= oMax - tol)
		{
			// Overshot without exact abutment (should not happen if candidates stay within overall).
			return false;
		}
		List<PartitionCandidate> candidates = new List<PartitionCandidate>();
		foreach (PartitionCandidate candidate in pool)
		{
			if (used.Contains(candidate.InputIndex))
			{
				continue;
			}
			if (Math.Abs(candidate.Min - current) > tol)
			{
				continue;
			}
			// Must strictly advance toward overall max (prevent zero-progress loops under tol).
			if (candidate.Max <= current + tol)
			{
				continue;
			}
			candidates.Add(candidate);
		}
		if (candidates.Count == 0)
		{
			return false;
		}
		// Prefer Structure over OutlineSegment; then farther reach; then stable input index.
		candidates.Sort(ComparePartitionCandidates);
		foreach (PartitionCandidate candidate in candidates)
		{
			used.Add(candidate.InputIndex);
			chain.Add(candidate.Item);
			if (TryBuildOverallPartitionChain(pool, used, chain, candidate.Max, oMax, tol))
			{
				return true;
			}
			chain.RemoveAt(chain.Count - 1);
			used.Remove(candidate.InputIndex);
		}
		return false;
	}

	private static int ComparePartitionCandidates(PartitionCandidate a, PartitionCandidate b)
	{
		bool aStruct = IsStructureWidthOrHeightRole(a.Item.DebugRole);
		bool bStruct = IsStructureWidthOrHeightRole(b.Item.DebugRole);
		if (aStruct != bStruct)
		{
			return aStruct ? -1 : 1;
		}
		// Farther max first (preferred try order only — not exclusive).
		int maxCmp = b.Max.CompareTo(a.Max);
		if (maxCmp != 0)
		{
			return maxCmp;
		}
		return a.InputIndex.CompareTo(b.InputIndex);
	}

	private sealed class PartitionCandidate
	{
		public DimensionDeduplicationItem Item;

		public double Min;

		public double Max;

		public int InputIndex;
	}

	private static bool IsStructureWidthOrHeightRole(string debugRole)
	{
		if (string.IsNullOrEmpty(debugRole))
		{
			return false;
		}
		return debugRole == "TopStructWidth"
			|| debugRole == "BottomStructWidth"
			|| debugRole == "LeftStructHeight"
			|| debugRole == "RightStructHeight";
	}

	public static int GetDimensionPreferenceRank(DimensionKind kind)
	{
		switch (kind)
		{
		case DimensionKind.PinDistance:
			return 0;
		case DimensionKind.PinGroupDistance:
			return 1;
		case DimensionKind.DatumHoleLocationX:
		case DimensionKind.DatumHoleLocationY:
			return 2;
		case DimensionKind.OverallWidth:
		case DimensionKind.OverallHeight:
			return 3;
		case DimensionKind.Normal:
		case DimensionKind.HoleLocation:
			return 4;
		default:
			return 5;
		}
	}

	public static double? TryExtractTolerance(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		int num = text.IndexOf('±');
		if (num < 0)
		{
			num = text.IndexOf('卤');
		}
		if (num < 0 || num >= text.Length - 1)
		{
			return null;
		}
		int i;
		for (i = num + 1; i < text.Length && char.IsWhiteSpace(text[i]); i++)
		{
		}
		int j;
		for (j = i; j < text.Length && (char.IsDigit(text[j]) || text[j] == '.'); j++)
		{
		}
		if (j <= i)
		{
			return null;
		}
		if (double.TryParse(text.Substring(i, j - i), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		return null;
	}
}
