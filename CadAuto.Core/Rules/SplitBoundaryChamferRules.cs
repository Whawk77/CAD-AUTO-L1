using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public static class SplitBoundaryChamferRules
{
	public static bool ContainsSegment(ChamferFeature2D chamfer, Segment2D segment, double tolerance)
	{
		if (chamfer == null || segment == null || !chamfer.SourceGeometryIds.Contains(segment.SourceKey)) { return false; }
		var full = new Segment2D(chamfer.StartPoint, chamfer.EndPoint);
		return OutlineGeometryQuery.IsPointOnSegment(segment.Start, full, tolerance)
			&& OutlineGeometryQuery.IsPointOnSegment(segment.End, full, tolerance);
	}

	public static IList<ChamferFeature2D> Find(OutlineFeature2D outline, DimensionRuleConfig config)
	{
		var result = new List<ChamferFeature2D>();
		if (outline == null || config == null) { return result; }
		double tol = config.GeometryTolerance;
		var graph = DimensionPlanner.InspectOutlineTopology(outline, tol);
		foreach (var run in BoundaryLineRuns.Find(graph, tol))
		{
			double dx = Math.Abs(run.End.X - run.Start.X), dy = Math.Abs(run.End.Y - run.Start.Y);
			double leg = Math.Max(dx, dy);
			if (dx <= tol || dy <= tol || Math.Abs(dx - dy) > Math.Max(tol, leg * 0.05)
				|| leg > Math.Max(outline.Width, outline.Height) * 0.25) { continue; }
			var before = new Segment2D(run.Previous.Start, run.Previous.End);
			var after = new Segment2D(run.Next.Start, run.Next.End);
			if (run.Previous.Kind != "Line" || run.Next.Kind != "Line"
				|| !((before.IsHorizontal(tol) && after.IsVertical(tol)) || (before.IsVertical(tol) && after.IsHorizontal(tol)))
				|| leg > Math.Max(before.Length, after.Length) * 0.5 + tol) { continue; }
			var sources = run.Edges.SelectMany(e => e.SourceKeys.Count > 0 ? e.SourceKeys : new List<string> { e.SourceKey })
				.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
			if (sources.Count == 0 || run.Edges.Any(e => string.IsNullOrEmpty(e.SourceKey))) { continue; }
			var full = new Segment2D(run.Start, run.End) { SourceKey = sources[0] };
			result.Add(new ChamferFeature2D { StartPoint = run.Start, EndPoint = run.End, Length = full.Length,
				DeltaX = dx, DeltaY = dy, Value = leg, Text = "C" + config.FormatNumber(leg),
				SourceSegment = full, SourceGeometryIds = sources, Confidence = 0.9 });
		}
		return result;
	}
}
