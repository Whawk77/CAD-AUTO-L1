using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public sealed class ExtensionLineLayoutItem
{
	public int DiagnosticId { get; set; }
	public string DatumFamilyKey { get; set; }
	public DimensionSide Side { get; set; }
	public Point2D FirstPoint { get; set; }
	public Point2D SecondPoint { get; set; }
	public double DimLineCoordinate { get; set; }
	public TextBounds2D TextBounds { get; set; }
	public bool Enabled { get; set; }
	public bool NativeFirstSuppressed { get; set; }
	public bool NativeSecondSuppressed { get; set; }
}

public sealed class ExtensionLineRenderPlan
{
	public bool SuppressFirst { get; set; }
	public bool SuppressSecond { get; set; }
	public List<Segment2D> FirstSegments { get; } = new List<Segment2D>();
	public List<Segment2D> SecondSegments { get; } = new List<Segment2D>();
	public string Decision { get; set; } = "NativeUnchanged_PendingCadVerification";
}

/// <summary>Pure geometry plan; it never changes dimension definition points or creates centerlines.</summary>
public static class DimensionExtensionLineRules
{
	public static bool ExceedsLengthLimit(Point2D first, Point2D second, Point2D dimLinePoint,
		double rotation, out double maximumLength)
	{
		double normalX = -Math.Sin(rotation), normalY = Math.Cos(rotation);
		double firstLength = Math.Abs((first.X - dimLinePoint.X) * normalX + (first.Y - dimLinePoint.Y) * normalY);
		double secondLength = Math.Abs((second.X - dimLinePoint.X) * normalX + (second.Y - dimLinePoint.Y) * normalY);
		maximumLength = Math.Max(firstLength, secondLength);
		// Allow floating-point roundoff at the exact model-space limit, independent of annotation scale.
		return !Finite(maximumLength) || maximumLength > DimensionRuleConfig.MaximumDimensionExtensionLineLength + 1E-9;
	}

	private sealed class Candidate
	{
		public int Index;
		public bool First;
		public bool Horizontal;
		public Point2D Feature;
		public double Fixed, Min, Max;
		public bool Deduplicated;
	}

	// Production policy: no automatic breaks, no shared-line suppression, no replacement entities.
	// Native style gaps/suppression remain the responsibility of the original AutoCAD dimension.
	public static List<ExtensionLineRenderPlan> CreateContinuous(IList<ExtensionLineLayoutItem> items)
	{
		return items.Select(_ => new ExtensionLineRenderPlan
		{
			Decision = "NativeContinuous_NoAutomaticBreaks|PendingCadVerification"
		}).ToList();
	}

	public static List<ExtensionLineRenderPlan> Create(IList<ExtensionLineLayoutItem> items,
		IEnumerable<Segment2D> boundary, double offset, double extension, double clearance, double tolerance)
	{
		var result = items.Select(_ => new ExtensionLineRenderPlan()).ToList();
		if (!Finite(offset) || !Finite(extension) || !Finite(clearance) || !Finite(tolerance)
			|| offset < 0 || extension < 0 || tolerance <= 0 || clearance <= tolerance) { return result; }
		var candidates = new List<Candidate>();
		for (int i = 0; i < items.Count; i++)
		{
			var item = items[i];
			if (!item.Enabled || !Finite(item.DimLineCoordinate) || !Finite(item.FirstPoint.X) || !Finite(item.FirstPoint.Y)
				|| !Finite(item.SecondPoint.X) || !Finite(item.SecondPoint.Y)) { continue; }
			foreach (bool first in new[] { true, false })
			{
				if (first ? item.NativeFirstSuppressed : item.NativeSecondSuppressed) { continue; }
				bool horizontal = item.Side == DimensionSide.Left || item.Side == DimensionSide.Right;
				var point = first ? item.FirstPoint : item.SecondPoint;
				double variable = horizontal ? point.X : point.Y;
				double sign = item.Side == DimensionSide.Left || item.Side == DimensionSide.Bottom ? -1 : 1;
				if ((item.DimLineCoordinate - variable) * sign <= offset + tolerance) { continue; }
				double start = variable + sign * offset, end = item.DimLineCoordinate + sign * extension;
				candidates.Add(new Candidate { Index = i, First = first, Horizontal = horizontal, Feature = point,
					Fixed = horizontal ? point.Y : point.X, Min = Math.Min(start, end), Max = Math.Max(start, end) });
			}
		}
		// A common coordinate alone is not proof of a common baseline: require the same real feature point AND family.
		foreach (var line in candidates.OrderByDescending(c => c.Max - c.Min).ThenBy(c => c.Index).ThenBy(c => c.First))
		{
			if (line.Deduplicated || string.IsNullOrEmpty(items[line.Index].DatumFamilyKey)) { continue; }
			foreach (var duplicate in candidates.Where(c => c != line && !c.Deduplicated && c.Horizontal == line.Horizontal
				&& (c.Index > line.Index || c.Max - c.Min < line.Max - line.Min - tolerance)
				&& items[c.Index].Side == items[line.Index].Side
				&& items[c.Index].DatumFamilyKey == items[line.Index].DatumFamilyKey
				&& c.Feature.DistanceTo(line.Feature) <= tolerance && c.Min >= line.Min - tolerance && c.Max <= line.Max + tolerance))
			{
				duplicate.Deduplicated = true;
				Suppress(result[duplicate.Index], duplicate.First);
				RecordDecision(result[duplicate.Index], "SharedExtensionOwnedBy:" + items[line.Index].DiagnosticId);
			}
		}
		var edges = (boundary ?? Enumerable.Empty<Segment2D>()).ToList();
		foreach (var line in candidates.Where(c => !c.Deduplicated))
		{
			var cuts = new List<Tuple<double, double>>();
			for (int j = 0; j < items.Count; j++)
			{
				if (j == line.Index) { continue; }
				var other = items[j];
				var box = other.TextBounds;
				if (box != null && (line.Horizontal
					? line.Fixed >= box.MinY - clearance && line.Fixed <= box.MaxY + clearance
					: line.Fixed >= box.MinX - clearance && line.Fixed <= box.MaxX + clearance))
				{
					cuts.Add(line.Horizontal ? Tuple.Create(box.MinX - clearance, box.MaxX + clearance)
						: Tuple.Create(box.MinY - clearance, box.MaxY + clearance));
				}
				bool sharedFeature = !string.IsNullOrEmpty(items[line.Index].DatumFamilyKey)
					&& items[line.Index].DatumFamilyKey == other.DatumFamilyKey && items[line.Index].Side == other.Side
					&& (line.Feature.DistanceTo(other.FirstPoint) <= tolerance || line.Feature.DistanceTo(other.SecondPoint) <= tolerance);
				if (sharedFeature || other.Side != items[line.Index].Side) { continue; }
				double a = line.Horizontal ? other.FirstPoint.Y : other.FirstPoint.X;
				double b = line.Horizontal ? other.SecondPoint.Y : other.SecondPoint.X;
				if (line.Fixed >= Math.Min(a, b) - clearance && line.Fixed <= Math.Max(a, b) + clearance)
				{
					cuts.Add(Tuple.Create(other.DimLineCoordinate - clearance, other.DimLineCoordinate + clearance));
				}
			}
			bool collinear = false;
			foreach (var edge in edges.Where(e => e != null && !e.IsArcChord))
			{
				double a = line.Horizontal ? edge.Start.Y : edge.Start.X;
				double b = line.Horizontal ? edge.End.Y : edge.End.X;
				double u = line.Horizontal ? edge.Start.X : edge.Start.Y;
				double v = line.Horizontal ? edge.End.X : edge.End.Y;
				if (Math.Abs(a - b) <= tolerance)
				{
					if (Math.Abs(a - line.Fixed) <= tolerance && Math.Max(u, v) > line.Min + tolerance && Math.Min(u, v) < line.Max - tolerance) { collinear = true; }
					continue;
				}
				double t = (line.Fixed - a) / (b - a);
				if (t < -tolerance || t > 1 + tolerance) { continue; }
				double intersection = u + t * (v - u);
				cuts.Add(Tuple.Create(intersection - clearance, intersection + clearance));
			}
			cuts = cuts.Where(c => c.Item2 > line.Min + tolerance && c.Item1 < line.Max - tolerance).ToList();
			double ownArrow = items[line.Index].DimLineCoordinate;
			var protectedArrows = items.Where(other => other.Side == items[line.Index].Side
				&& !string.IsNullOrEmpty(other.DatumFamilyKey) && other.DatumFamilyKey == items[line.Index].DatumFamilyKey
				&& (line.Feature.DistanceTo(other.FirstPoint) <= tolerance || line.Feature.DistanceTo(other.SecondPoint) <= tolerance))
				.Select(other => other.DimLineCoordinate).Concat(new[] { ownArrow });
			if (collinear || cuts.Any(c => protectedArrows.Any(arrow => c.Item1 <= arrow + tolerance && c.Item2 >= arrow - tolerance)))
			{
				RecordDecision(result[line.Index], "UnresolvedExtensionConflict_NativeRetained");
				continue;
			}
			if (cuts.Count == 0) { continue; }
			Suppress(result[line.Index], line.First);
			var segments = line.First ? result[line.Index].FirstSegments : result[line.Index].SecondSegments;
			double cursor = line.Min;
			foreach (var cut in cuts.OrderBy(c => c.Item1))
			{
				double end = Math.Min(line.Max, cut.Item1);
				AddSegment(segments, line, cursor, end, tolerance);
				cursor = Math.Max(cursor, Math.Min(line.Max, cut.Item2));
			}
			AddSegment(segments, line, cursor, line.Max, tolerance);
			RecordDecision(result[line.Index], "SegmentedExtensionGeometry");
		}
		return result;
	}

	private static void RecordDecision(ExtensionLineRenderPlan plan, string decision)
	{
		plan.Decision = plan.Decision.StartsWith("NativeUnchanged", StringComparison.Ordinal)
			? decision + "|PendingCadVerification" : plan.Decision + ";" + decision;
	}

	private static void Suppress(ExtensionLineRenderPlan plan, bool first)
	{
		if (first) { plan.SuppressFirst = true; } else { plan.SuppressSecond = true; }
	}

	private static void AddSegment(List<Segment2D> output, Candidate line, double a, double b, double tolerance)
	{
		if (b - a <= tolerance) { return; }
		output.Add(line.Horizontal ? new Segment2D(new Point2D(a, line.Fixed), new Point2D(b, line.Fixed))
			: new Segment2D(new Point2D(line.Fixed, a), new Point2D(line.Fixed, b)));
	}

	private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
