using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public static class OutlineSelectionRules
{
	public static IList<int> FindExteriorCandidates(IList<OutlineFeature2D> candidates, double tolerance)
	{
		if (candidates == null) { return new List<int>(); }
		var boundaries = candidates.Select(candidate => GetVerifiedBoundary(candidate, tolerance)).ToList();
		var exterior = Enumerable.Range(0, boundaries.Count)
			.Where(index => boundaries[index] != null && !Enumerable.Range(0, boundaries.Count)
				.Any(other => other != index && boundaries[other] != null
					&& Encloses(boundaries[other], boundaries[index], tolerance)))
			.ToList();
		if (exterior.Count == 0) { return exterior; }
		for (int index = 0; index < candidates.Count; index++)
		{
			if (boundaries[index] != null) { continue; }
			if (candidates[index] == null || !OutlineGeometryQuery.TryGetEnvelope(candidates[index], tolerance, out OutlineEnvelope2D envelope))
			{
				exterior.Add(index);
				continue;
			}
			var extent = new OutlineFeature2D { MinX = envelope.MinX, MaxX = envelope.MaxX, MinY = envelope.MinY, MaxY = envelope.MaxY };
			if (!exterior.Any(other => boundaries[other] != null && Encloses(boundaries[other], extent, tolerance))) { exterior.Add(index); }
		}
		return exterior;
	}

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

	private static bool Encloses(OutlineFeature2D outer, OutlineFeature2D inner, double tolerance)
	{
		if (inner.MinX <= outer.MinX + tolerance || inner.MaxX >= outer.MaxX - tolerance
			|| inner.MinY <= outer.MinY + tolerance || inner.MaxY >= outer.MaxY - tolerance) { return false; }
		foreach (double y in new[] { inner.MinY, inner.MaxY })
		{
			if (OutlineGeometryQuery.GetHorizontalBoundaryIntersections(outer, y, tolerance)
				.Any(p => p.X >= inner.MinX - tolerance && p.X <= inner.MaxX + tolerance)
				|| outer.Segments.Any(s => Math.Abs(s.Start.Y - s.End.Y) <= tolerance
					&& Math.Abs(s.Start.Y - y) <= tolerance && s.MaxX >= inner.MinX - tolerance && s.MinX <= inner.MaxX + tolerance)) { return false; }
		}
		foreach (double x in new[] { inner.MinX, inner.MaxX })
		{
			if (OutlineGeometryQuery.GetVerticalBoundaryIntersections(outer, x, tolerance)
				.Any(p => p.Y >= inner.MinY - tolerance && p.Y <= inner.MaxY + tolerance)
				|| outer.Segments.Any(s => Math.Abs(s.Start.X - s.End.X) <= tolerance
					&& Math.Abs(s.Start.X - x) <= tolerance && s.MaxY >= inner.MinY - tolerance && s.MinY <= inner.MaxY + tolerance)) { return false; }
		}
		double epsilon = Math.Max(tolerance * 0.0001, 1E-9);
		foreach (double fraction in new[] { 0.5, 0.38196601125, 0.61803398875, 0.25, 0.75 })
		{
			double y = inner.MinY + (inner.MaxY - inner.MinY) * fraction;
			if (IsRegularScanline(outer, y, epsilon)) { return IsInsideAt(outer, (inner.MinX + inner.MaxX) * 0.5, y, epsilon); }
		}
		return false;
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
