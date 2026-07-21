using System;
using System.Globalization;
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
