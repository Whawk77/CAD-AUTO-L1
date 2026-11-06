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

	// A chamfer that consumes a whole step riser sits between two parallel faces; each face must run away
	// from the slope, otherwise the slope forms an acute dovetail tip rather than a chamfer.
	public static bool ParallelNeighborLeavesChamfer(Segment2D neighbor, Point2D joint, Point2D chamferOtherEnd, bool horizontal, double tolerance)
	{
		Point2D far = neighbor.Start.DistanceTo(joint) <= neighbor.End.DistanceTo(joint) ? neighbor.End : neighbor.Start;
		double outward = horizontal ? far.X - joint.X : far.Y - joint.Y;
		double inward = horizontal ? chamferOtherEnd.X - joint.X : chamferOtherEnd.Y - joint.Y;
		return Math.Abs(outward) > tolerance && outward * inward < 0;
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
			if (dx <= tol || dy <= tol || Math.Abs(dx - dy) > Math.Max(tol, DimensionRuleConfig.ChamferLegDifferenceTolerance)) { continue; }
			var before = new Segment2D(run.Previous.Start, run.Previous.End);
			var after = new Segment2D(run.Next.Start, run.Next.End);
			bool perpendicular = (before.IsHorizontal(tol) && after.IsVertical(tol)) || (before.IsVertical(tol) && after.IsHorizontal(tol));
			bool parallel = ((before.IsHorizontal(tol) && after.IsHorizontal(tol)) || (before.IsVertical(tol) && after.IsVertical(tol)))
				&& ParallelNeighborLeavesChamfer(before, run.Start, run.End, before.IsHorizontal(tol), tol)
				&& ParallelNeighborLeavesChamfer(after, run.End, run.Start, after.IsHorizontal(tol), tol);
			if (run.Previous.Kind != "Line" || run.Next.Kind != "Line" || !(perpendicular || parallel)) { continue; }
			var sources = BoundaryLineRuns.SourceKeys(run.Edges);
			if (sources.Count == 0 || run.Edges.Any(e => string.IsNullOrEmpty(e.SourceKey))) { continue; }
			var full = new Segment2D(run.Start, run.End) { SourceKey = sources[0] };
			result.Add(new ChamferFeature2D { StartPoint = run.Start, EndPoint = run.End, Length = full.Length,
				DeltaX = dx, DeltaY = dy, Value = leg, Text = "C" + config.FormatNumber(leg),
				SourceSegment = full, SourceGeometryIds = sources, Confidence = 0.9 });
		}
		return result;
	}
}
