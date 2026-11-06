using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public static class OutlineSelectionRules
{
	/// <summary>
	/// ASD 每次框选的都是单一零件（用户 2026-11-03 确认）：在拓扑验证闭合的线组里取外包络面积最大的一组作外轮廓，
	/// 其余线组（孔壁线、螺纹线、台阶线、内部闭合框）都是该零件的内部结构，不再要求用户点选。
	/// 没有任何闭合线组时返回 -1，由调用方走原有的兜底规则。decisions 按线组顺序记录判定依据。
	/// </summary>
	public static int SelectSinglePartExterior(IList<OutlineFeature2D> candidates, double tolerance, out List<string> decisions)
	{
		decisions = new List<string>();
		if (candidates == null) { return -1; }
		var boundaries = candidates.Select(candidate => GetVerifiedBoundary(candidate, tolerance)).ToList();
		int chosen = -1;
		double chosenArea = 0;
		for (int index = 0; index < boundaries.Count; index++)
		{
			if (boundaries[index] == null) { continue; }
			double area = EnvelopeArea(boundaries[index]);
			if (chosen < 0 || area > chosenArea + tolerance) { chosen = index; chosenArea = area; }
		}
		for (int index = 0; index < candidates.Count; index++)
		{
			bool closed = boundaries[index] != null;
			OutlineFeature2D extent = closed ? boundaries[index] : null;
			if (!closed && candidates[index] != null && OutlineGeometryQuery.TryGetEnvelope(candidates[index], tolerance, out OutlineEnvelope2D envelope))
			{
				extent = new OutlineFeature2D { MinX = envelope.MinX, MaxX = envelope.MaxX, MinY = envelope.MinY, MaxY = envelope.MaxY };
			}
			string decision = index == chosen ? "Exterior"
				: chosen < 0 ? "NoClosedComponent"
				: closed ? "InternalClosed" : "InternalOpen";
			decisions.Add("Component=" + index + "|Closed=" + closed
				+ "|EnvelopeArea=" + (extent == null ? "Unavailable" : EnvelopeArea(extent).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture))
				+ "|Decision=" + decision);
		}
		return chosen;
	}

	private static double EnvelopeArea(OutlineFeature2D outline) => Math.Max(0, outline.MaxX - outline.MinX) * Math.Max(0, outline.MaxY - outline.MinY);

	public static bool? ContainsPoint(OutlineFeature2D outline, Point2D point, double tolerance)
	{
		OutlineFeature2D boundary = GetVerifiedBoundary(outline, tolerance);
		if (boundary == null) { return null; }
		if (OutlineGeometryQuery.IsPointOnBoundary(point, boundary, tolerance)) { return false; }
		double epsilon = Math.Max(tolerance * 0.0001, 1E-9);
		foreach (double offset in new[] { 0.0, tolerance * 0.2, -tolerance * 0.2, tolerance * 0.4, -tolerance * 0.4 })
		{
			double y = point.Y + offset;
			if (IsRegularScanline(boundary, y, epsilon)) { return IsInsideAt(boundary, point.X, y, epsilon); }
		}
		return false;
	}

	private static OutlineFeature2D GetVerifiedBoundary(OutlineFeature2D candidate, double tolerance)
	{
		if (candidate == null || !(tolerance > 0)) { return null; }
		OutlineTopologyDiagnostic topology = DimensionPlanner.InspectOutlineTopology(candidate, tolerance);
		if (topology.Status != "UniqueExteriorWalk" || !topology.IsClosedWalk || topology.HasRepeatedBoundaryEdges) { return null; }
		var boundary = new OutlineFeature2D();
		foreach (OutlineTopologyEdge edge in topology.Edges.Where(edge => edge.IsBoundary))
		{
			if (edge.Kind == "Line")
			{
				boundary.Segments.Add(new Segment2D(edge.Start, edge.End) { SourceKey = edge.SourceKey });
			}
			else if (edge.Kind == "Arc")
			{
				boundary.Arcs.Add(new Arc2D { Start = edge.Start, End = edge.End, Center = edge.Center,
					Radius = edge.Radius, Bulge = edge.Bulge, SourceKey = edge.SourceKey });
			}
			else { return null; }
		}
		if (!OutlineGeometryQuery.TryGetEnvelope(boundary, tolerance, out OutlineEnvelope2D envelope)) { return null; }
		boundary.MinX = envelope.MinX; boundary.MaxX = envelope.MaxX;
		boundary.MinY = envelope.MinY; boundary.MaxY = envelope.MaxY;
		return boundary;
	}

	private static bool IsRegularScanline(OutlineFeature2D boundary, double y, double epsilon)
	{
		return !boundary.Segments.Any(s => Math.Abs(s.Start.Y - y) <= epsilon * 4 || Math.Abs(s.End.Y - y) <= epsilon * 4)
			&& !boundary.Arcs.Any(a => Math.Abs(a.Start.Y - y) <= epsilon * 4 || Math.Abs(a.End.Y - y) <= epsilon * 4
				|| Math.Abs(a.Center.Y + a.Radius - y) <= epsilon * 4 || Math.Abs(a.Center.Y - a.Radius - y) <= epsilon * 4);
	}

	private static bool IsInsideAt(OutlineFeature2D boundary, double x, double y, double epsilon)
	{
		return OutlineGeometryQuery.GetHorizontalBoundaryIntersections(boundary, y, epsilon)
			.Count(point => point.X > x + epsilon) % 2 == 1;
	}
}
