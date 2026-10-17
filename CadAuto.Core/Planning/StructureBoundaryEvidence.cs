using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning;

/// <summary>Read-only ownership evidence from the same boundary graph used by the planner.</summary>
public sealed class StructureBoundaryEvidence
{
	private readonly OutlineTopologyDiagnostic _graph;
	private readonly double _tolerance;
	private readonly OutlineFeature2D _boundary = new OutlineFeature2D();

	public OutlineFeature2D BoundaryOutline => IsAvailable ? _boundary : null;

	public bool IsAvailable => _graph != null && _graph.Status == "UniqueExteriorWalk"
		&& _graph.IsClosedWalk && !_graph.HasRepeatedBoundaryEdges;

	public StructureBoundaryEvidence(OutlineTopologyDiagnostic graph, double tolerance)
	{
		_graph = graph;
		_tolerance = tolerance;
		if (!IsAvailable) { return; }
		_boundary.MinX = graph.MinX; _boundary.MinY = graph.MinY;
		_boundary.MaxX = graph.MaxX; _boundary.MaxY = graph.MaxY;
		foreach (var edge in graph.Edges.Where(e => e.IsBoundary))
		{
			if (edge.Kind == "Line") { _boundary.Segments.Add(new Segment2D(edge.Start, edge.End) { SourceKey = edge.SourceKey }); }
			else { _boundary.Arcs.Add(new Arc2D { Start = edge.Start, End = edge.End, Center = edge.Center,
				Radius = edge.Radius, Bulge = edge.Bulge, SourceKey = edge.SourceKey }); }
		}
	}

	public OutlineFeature2D CreateOuterGeometryScope(OutlineFeature2D source)
	{
		var scope = new OutlineFeature2D { MinX = source.MinX, MinY = source.MinY, MaxX = source.MaxX, MaxY = source.MaxY };
		if (!IsAvailable) { return scope; }
		scope.Segments.AddRange(_boundary.Segments);
		scope.Arcs.AddRange(_boundary.Arcs);
		foreach (var node in _graph.Nodes.Where(n => n.IsBoundary)) { scope.Vertices.Add(node.Point); }
		foreach (var chamfer in source.Chamfers.Where(c => _graph.Edges.Any(e => e.IsBoundary && e.Kind == "Line"
			&& PairMatches(c.StartPoint, c.EndPoint, e.Start, e.End)))) { scope.Chamfers.Add(chamfer); }
		foreach (var fillet in source.Fillets.Where(f => _graph.Edges.Any(e => e.IsBoundary && e.Kind == "Arc"
			&& PairMatches(f.StartPoint, f.EndPoint, e.Start, e.End)))) { scope.Fillets.Add(fillet); }
		return scope;
	}

	public string GetMembership(Segment2D segment)
	{
		if (!IsAvailable) { return "Unknown"; }
		var matches = _graph.Edges.Where(e => e.Kind == "Line" && PairMatches(segment.Start, segment.End, e.Start, e.End)).ToList();
		if (matches.Count == 0) { return "Unknown"; }
		return matches.All(e => e.IsBoundary) ? "Boundary" : matches.All(e => !e.IsBoundary) ? "NonBoundary" : "Unknown";
	}

	public bool? ValidateBoundaryAttachments(Point2D first, Point2D second)
	{
		if (!IsAvailable) { return null; }
		return OutlineGeometryQuery.IsPointOnBoundary(first, _boundary, _tolerance)
			&& OutlineGeometryQuery.IsPointOnBoundary(second, _boundary, _tolerance);
	}

	/// <summary>
	/// A complement must follow a unique local, axis-monotone boundary walk through a recognized
	/// chamfer and its seed. No projection to a theoretical corner or nearest-point guessing.
	/// Endpoints must already be real graph nodes; unsupported cases remain unproven.
	/// </summary>
	public bool TryProveChamferComplement(StructureFeature seed, Point2D first, Point2D second,
		OutlineFeature2D outline, double localBand, out string evidence, out List<string> sourceKeys)
	{
		evidence = null;
		sourceKeys = new List<string>();
		if (!IsAvailable || seed.BoundaryMembership != "Boundary" || ValidateBoundaryAttachments(first, second) != true) { return false; }
		var starts = _graph.Nodes.Where(n => n.Point.DistanceTo(first) <= _tolerance).ToList();
		var ends = _graph.Nodes.Where(n => n.Point.DistanceTo(second) <= _tolerance).ToList();
		if (starts.Count != 1 || ends.Count != 1 || starts[0].Id == ends[0].Id) { return false; }
		bool horizontal = seed.Axis == StructureFeatureAxis.Horizontal;
		Func<Point2D, double> axis = p => horizontal ? p.X : p.Y;
		Func<Point2D, double> cross = p => horizontal ? p.Y : p.X;
		double minimum = Math.Min(axis(first), axis(second)), maximum = Math.Max(axis(first), axis(second));
		var validPaths = new List<List<OutlineTopologyEdge>>();
		foreach (int direction in new[] { 1, -1 })
		{
			var path = _graph.BoundaryPath;
			int index = path.FindIndex(s => direction > 0 ? s.FromNode == starts[0].Id : s.ToNode == starts[0].Id);
			if (index < 0) { continue; }
			var visited = new List<OutlineTopologyEdge>();
			bool reached = false, monotone = true;
			double previous = axis(first), sign = Math.Sign(axis(second) - axis(first));
			for (int count = 0; count < path.Count; count++)
			{
				var step = path[index];
				var edge = _graph.Edges.Single(e => e.Id == step.EdgeId);
				bool forward = direction > 0 ? step.Forward : !step.Forward;
				Point2D end = forward ? edge.End : edge.Start;
				// Only a local real line/chamfer route is proved here. Curved or complex routes need a separate proof.
				if (edge.Kind != "Line" || axis(end) < minimum - _tolerance || axis(end) > maximum + _tolerance
					|| (axis(end) - previous) * sign < -_tolerance
					|| Math.Abs(cross(end) - seed.CrossPosition) > localBand + _tolerance) { monotone = false; break; }
				visited.Add(edge);
				previous = axis(end);
				if ((direction > 0 ? step.ToNode : step.FromNode) == ends[0].Id) { reached = true; break; }
				index = (index + direction + path.Count) % path.Count;
			}
			if (!reached || !monotone) { continue; }
			bool includesSeed = visited.Any(e => e.Kind == "Line"
				&& PairMatches(seed.FirstPoint, seed.SecondPoint, e.Start, e.End));
			var inclined = visited.Where(e => Math.Abs(e.Start.X - e.End.X) > _tolerance
				&& Math.Abs(e.Start.Y - e.End.Y) > _tolerance).ToList();
			bool includesChamfer = inclined.Count == 1 && outline.Chamfers.Any(c => c.Value > _tolerance
				&& PairMatches(c.StartPoint, c.EndPoint, inclined[0].Start, inclined[0].End));
			if (includesSeed && includesChamfer) { validPaths.Add(visited); }
		}
		if (validPaths.Count != 1) { return false; }
		sourceKeys = validPaths[0].Select(e => e.SourceKey).Where(k => !string.IsNullOrEmpty(k)).Distinct().ToList();
		evidence = "StructureBoundary|Membership=Boundary|Relation=ConnectedChamferComplement|EdgeIds="
			+ string.Join(",", validPaths[0].Select(e => e.Id));
		return true;
	}

	public void ApplyAttachmentEvidence(IEnumerable<StructureFeature> features)
	{
		foreach (var feature in features.Where(f => f.RequiresOuterBoundarySupport && f.Kind != StructureFeatureKind.Overall))
		{
			feature.BoundaryAttachmentValidity = ValidateBoundaryAttachments(feature.FirstPoint, feature.SecondPoint);
		}
	}

	public static void EnforceSelection(IEnumerable<StructureFeature> features)
	{
		foreach (var feature in features.Where(f => f.RequiresOuterBoundarySupport && f.Kind != StructureFeatureKind.Overall))
		{
			if (feature.BoundaryMembership == "Boundary" && feature.BoundaryAttachmentValidity == true) { continue; }
			feature.Keep = false;
			feature.SuppressReason = feature.BoundaryMembership == "NonBoundary"
				? "StructureSourceNotOnExteriorBoundary" : feature.BoundaryAttachmentValidity == false
					? "StructureAttachmentNotOnExteriorBoundary" : "StructureBoundaryEvidenceInsufficient";
		}
	}

	private bool PairMatches(Point2D a, Point2D b, Point2D c, Point2D d)
	{
		return (a.DistanceTo(c) <= _tolerance && b.DistanceTo(d) <= _tolerance)
			|| (a.DistanceTo(d) <= _tolerance && b.DistanceTo(c) <= _tolerance);
	}
}
