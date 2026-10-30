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

	public IDictionary<Segment2D, BoundaryLineRun> MapAxisSegments(OutlineFeature2D outline)
		=> BoundaryLineRuns.MapAxisSegments(_graph, outline, _tolerance);

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
			&& IsChamferEdge(e, c)) && ValidateBoundaryAttachments(c.StartPoint, c.EndPoint) == true)) { scope.Chamfers.Add(chamfer); }
		foreach (var fillet in source.Fillets.Where(f => _graph.Edges.Any(e => e.IsBoundary && e.Kind == "Arc"
			&& PairMatches(f.StartPoint, f.EndPoint, e.Start, e.End)))) { scope.Fillets.Add(fillet); }
		return scope;
	}

	public string GetMembership(Segment2D segment)
	{
		if (!IsAvailable) { return "Unknown"; }
		double length = segment.Length;
		if (length <= _tolerance) { return "Unknown"; }
		double dx = (segment.End.X - segment.Start.X) / length, dy = (segment.End.Y - segment.Start.Y) / length;
		Func<Point2D, double> axis = p => (p.X - segment.Start.X) * dx + (p.Y - segment.Start.Y) * dy;
		Func<Point2D, bool> onLine = p => Math.Abs((p.X - segment.Start.X) * dy - (p.Y - segment.Start.Y) * dx) <= _tolerance;
		var matches = _graph.Edges.Where(e => e.Kind == "Line" && onLine(e.Start) && onLine(e.End)
			&& Math.Max(axis(e.Start), axis(e.End)) > _tolerance
			&& Math.Min(axis(e.Start), axis(e.End)) < length - _tolerance)
			.OrderBy(e => Math.Min(axis(e.Start), axis(e.End))).ToList();
		double covered = 0;
		foreach (var edge in matches)
		{
			if (Math.Min(axis(edge.Start), axis(edge.End)) > covered + _tolerance) { return "Unknown"; }
			covered = Math.Max(covered, Math.Max(axis(edge.Start), axis(edge.End)));
		}
		if (covered < length - _tolerance) { return "Unknown"; }
		// A raw edge crossing an internal seam is not wholly supported by the exterior contour.
		return matches.All(e => e.IsBoundary) ? "Boundary" : "NonBoundary";
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

	public void AttachRecoveredGroove(StructureFeature feature, double localBand)
	{
		AttachRecoveredBoundarySpan(feature, localBand, "RecoveredGroove");
	}

	public void AttachRecoveredBoundarySpan(StructureFeature feature, double localBand, string relation)
	{
		feature.RequiresOuterBoundarySupport = true;
		feature.BoundaryMembership = "Unknown";
		feature.BoundaryAttachmentValidity = null;
		feature.BoundaryEvidence = "StructureBoundary|Relation=" + relation + "|Membership=Unknown|Reason=ConnectedBoundaryProofMissing";
		feature.SupportSourceKeys.Clear();
		if (!IsAvailable) { return; }
		bool horizontal = feature.Axis == StructureFeatureAxis.Horizontal;
		Func<Point2D, double> axis = p => horizontal ? p.X : p.Y;
		Func<Point2D, double> cross = p => horizontal ? p.Y : p.X;
		double minimum = Math.Min(feature.T0, feature.T1), maximum = Math.Max(feature.T0, feature.T1);
		var paths = new List<(Point2D First, Point2D Second, List<OutlineTopologyEdge> Edges, double Length)>();
		foreach (var start in _graph.Nodes.Where(n => n.IsBoundary && Math.Abs(axis(n.Point) - minimum) <= _tolerance
			&& Math.Abs(cross(n.Point) - feature.CrossPosition) <= localBand + _tolerance))
		foreach (int direction in new[] { 1, -1 })
		{
			var path = _graph.BoundaryPath;
			int index = path.FindIndex(s => direction > 0 ? s.FromNode == start.Id : s.ToNode == start.Id);
			if (index < 0) { continue; }
			var visited = new List<OutlineTopologyEdge>();
			double previous = axis(start.Point);
			bool hasFloor = false;
			for (int count = 0; count < path.Count; count++)
			{
				var step = path[index];
				var edge = _graph.Edges.Single(e => e.Id == step.EdgeId);
				var end = (direction > 0 ? step.Forward : !step.Forward) ? edge.End : edge.Start;
				if (!edge.IsBoundary || string.IsNullOrEmpty(edge.SourceKey) || axis(end) < previous - _tolerance
					|| axis(end) > maximum + _tolerance || Math.Abs(cross(end) - feature.CrossPosition) > localBand + _tolerance) { break; }
				if (edge.Kind == "Arc")
				{
					var scope = new OutlineFeature2D();
					scope.Arcs.Add(new Arc2D { Start = edge.Start, End = edge.End, Center = edge.Center,
						Radius = edge.Radius, Bulge = edge.Bulge });
					if (!OutlineGeometryQuery.TryGetEnvelope(scope, _tolerance, out var envelope)
						|| (horizontal ? envelope.MinX : envelope.MinY) < Math.Min(axis(edge.Start), axis(edge.End)) - _tolerance
						|| (horizontal ? envelope.MaxX : envelope.MaxY) > Math.Max(axis(edge.Start), axis(edge.End)) + _tolerance
						|| (horizontal ? envelope.MinY : envelope.MinX) < feature.CrossPosition - localBand - _tolerance
						|| (horizontal ? envelope.MaxY : envelope.MaxX) > feature.CrossPosition + localBand + _tolerance) { break; }
				}
				else if (edge.Kind != "Line") { break; }
				hasFloor |= edge.Kind == "Line" && Math.Abs(cross(edge.Start) - feature.CrossPosition) <= _tolerance
					&& Math.Abs(cross(edge.End) - feature.CrossPosition) <= _tolerance
					&& Math.Abs(axis(edge.End) - axis(edge.Start)) > _tolerance;
				visited.Add(edge);
				if (Math.Abs(axis(end) - maximum) <= _tolerance)
				{
					if (hasFloor) { paths.Add((start.Point, end, visited,
						visited.Sum(e => e.Kind == "Arc" ? Math.Abs(4 * Math.Atan(e.Bulge)) * e.Radius : e.Start.DistanceTo(e.End)))); }
					break;
				}
				previous = axis(end);
				index = (index + direction + path.Count) % path.Count;
			}
		}
		// The shortest proven local walk excludes unrelated wall tails at the same station.
		var ordered = paths.OrderBy(p => p.Length).ToList();
		if (ordered.Count == 0) { return; }
		var best = ordered[0];
		if (ordered.Skip(1).Any(p => Math.Abs(p.Length - best.Length) <= _tolerance
			&& !PairMatches(best.First, best.Second, p.First, p.Second))) { return; }
		feature.FirstPoint = best.First; feature.SecondPoint = best.Second;
		feature.BoundaryMembership = "Boundary";
		feature.BoundaryEvidence = "StructureBoundary|Relation=" + relation + "|Membership=Boundary|EdgeIds=" + string.Join(",", best.Edges.Select(e => e.Id));
		feature.SupportSourceKeys.AddRange(best.Edges.SelectMany(e => e.SourceKeys.Concat(new[] { e.SourceKey })).Where(k => !string.IsNullOrEmpty(k)).Distinct());
	}

	public void RecoverInnerBossGroove(StructureFeature feature, OutlineFeature2D outline,
		double stem, double wall, double baseCross, double topCross)
	{
		if (!IsAvailable) { return; }
		bool horizontal = feature.Axis == StructureFeatureAxis.Horizontal;
		Func<Point2D, double> axis = p => horizontal ? p.X : p.Y;
		Func<Point2D, double> cross = p => horizontal ? p.Y : p.X;
		double minimum = Math.Min(stem, wall), maximum = Math.Max(stem, wall);
		double low = Math.Min(baseCross, topCross), high = Math.Max(baseCross, topCross);
		var matches = new List<(Point2D First, Point2D Second, List<OutlineTopologyEdge> Edges)>();
		foreach (var tread in _graph.Edges.Where(e => !e.IsBoundary && e.Kind == "Line"
			&& Math.Abs(cross(e.Start) - cross(e.End)) <= _tolerance))
		{
			var outer = Math.Abs(axis(tread.Start) - wall) <= _tolerance ? tread.Start : tread.End;
			var inner = outer.DistanceTo(tread.Start) <= _tolerance ? tread.End : tread.Start;
			int outerId = outer.DistanceTo(tread.Start) <= _tolerance ? tread.StartNode : tread.EndNode;
			int innerId = outerId == tread.StartNode ? tread.EndNode : tread.StartNode;
			if (Math.Abs(axis(outer) - wall) > _tolerance || !_graph.Nodes.Any(n => n.Id == outerId && n.IsBoundary)
				|| cross(outer) <= low + _tolerance || cross(outer) >= high - _tolerance
				|| !_graph.Edges.Any(e => e.IsBoundary && e.Kind == "Line" && (e.StartNode == outerId || e.EndNode == outerId)
					&& Math.Abs(axis(e.Start) - wall) <= _tolerance && Math.Abs(axis(e.End) - wall) <= _tolerance
					&& feature.CrossPosition >= Math.Min(cross(e.Start), cross(e.End)) - _tolerance
					&& feature.CrossPosition <= Math.Max(cross(e.Start), cross(e.End)) + _tolerance)) { continue; }
			foreach (var arc in _graph.Edges.Where(e => !e.IsBoundary && e.Kind == "Arc" && e.Radius > _tolerance
				&& (e.StartNode == innerId || e.EndNode == innerId)
				&& outline.Fillets.Any(f => PairMatches(f.StartPoint, f.EndPoint, e.Start, e.End))))
			{
				int joinId = arc.StartNode == innerId ? arc.EndNode : arc.StartNode;
				var join = arc.StartNode == innerId ? arc.End : arc.Start;
				if (Math.Abs(axis(inner) - axis(arc.Center)) > _tolerance
					|| Math.Abs(cross(join) - cross(arc.Center)) > _tolerance
					|| Math.Abs(Math.Abs(arc.Bulge) - Math.Tan(Math.PI / 8)) > _tolerance / arc.Radius
					|| (axis(join) - axis(inner)) * Math.Sign(stem - wall) <= _tolerance
					|| Math.Abs(inner.DistanceTo(arc.Center) - arc.Radius) > _tolerance
					|| Math.Abs(join.DistanceTo(arc.Center) - arc.Radius) > _tolerance) { continue; }
				foreach (var side in _graph.Edges.Where(e => !e.IsBoundary && e.Kind == "Line"
					&& (e.StartNode == joinId || e.EndNode == joinId) && Math.Abs(axis(e.Start) - axis(e.End)) <= _tolerance))
				{
					int endId = side.StartNode == joinId ? side.EndNode : side.StartNode;
					var end = side.StartNode == joinId ? side.End : side.Start;
					var edges = new List<OutlineTopologyEdge> { tread, arc, side };
					if (!_graph.Nodes.Any(n => n.Id == endId && n.IsBoundary) || axis(end) <= minimum + _tolerance
						|| axis(end) >= maximum - _tolerance || (cross(end) - cross(join)) * Math.Sign(cross(join) - cross(inner)) < -_tolerance
						|| edges.Any(e => string.IsNullOrEmpty(e.SourceKey)
							|| new[] { e.Start, e.End }.Any(p => axis(p) < minimum - _tolerance || axis(p) > maximum + _tolerance
								|| cross(p) < low - _tolerance || cross(p) > high + _tolerance))) { continue; }
					matches.Add((end, outer, edges));
				}
			}
		}
		if (matches.Count != 1) { return; }
		var match = matches[0];
		feature.T0 = Math.Min(axis(match.First), axis(match.Second)); feature.T1 = Math.Max(axis(match.First), axis(match.Second));
		feature.FirstPoint = axis(match.First) < axis(match.Second) ? match.First : match.Second;
		feature.SecondPoint = axis(match.First) < axis(match.Second) ? match.Second : match.First;
		// Only this complete, tangent internal path is admitted; raw internal edges remain excluded.
		feature.RequiresOuterBoundarySupport = false; feature.BoundaryMembership = "NonBoundary";
		feature.BoundaryAttachmentValidity = ValidateBoundaryAttachments(feature.FirstPoint, feature.SecondPoint);
		feature.SupportSourceKeys.Clear(); feature.SupportSourceKeys.AddRange(match.Edges.Select(e => e.SourceKey));
		feature.SourceKey = "FootLedge:InnerGroove:" + string.Join(",", feature.SupportSourceKeys);
		feature.BoundaryEvidence = "StructureBoundary|Relation=RecoveredInnerBossGroove|Membership=ConnectedInternalPath|TangentFillet=True|RealBoundaryEndpoints=True|EdgeIds="
			+ string.Join(",", match.Edges.Select(e => e.Id));
	}

	public void ApplyAttachmentEvidence(IEnumerable<StructureFeature> features, OutlineFeature2D outline = null)
	{
		foreach (var feature in features.Where(f => f.RequiresOuterBoundarySupport && f.Kind != StructureFeatureKind.Overall))
		{
			feature.BoundaryAttachmentValidity = ValidateBoundaryAttachments(feature.FirstPoint, feature.SecondPoint);
			if (feature.BoundaryAttachmentValidity == false && outline != null && feature.BoundaryMembership == "Boundary"
				&& feature.SourceKey != null && feature.SourceKey.EndsWith("+Expanded", StringComparison.Ordinal))
			{
				RecoverExpandedEndpoints(feature, outline);
				feature.BoundaryAttachmentValidity = ValidateBoundaryAttachments(feature.FirstPoint, feature.SecondPoint);
			}
		}
	}

	private void RecoverExpandedEndpoints(StructureFeature feature, OutlineFeature2D outline)
	{
		// shortcut: keep existing slope-coverage ownership when body slopes coexist; upgrade with a shared coverage proof.
		if (_graph.Edges.Any(e => e.IsBoundary && e.Kind == "Line"
			&& Math.Abs(e.End.X - e.Start.X) > _tolerance && Math.Abs(e.End.Y - e.Start.Y) > _tolerance
			&& !outline.Chamfers.Any(c => IsChamferEdge(e, c)))) { return; }
		var sources = feature.SupportSourceKeys.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
		if (!IsAvailable || sources.Count == 0) { return; }
		bool horizontal = feature.Axis == StructureFeatureAxis.Horizontal;
		Func<Point2D, double> axis = p => horizontal ? p.X : p.Y;
		var seeds = _graph.Edges.Where(e => e.IsBoundary && e.Kind == "Line" && e.SourceKeys.Any(sources.Contains)
			&& (horizontal ? Math.Abs(e.Start.Y - e.End.Y) <= _tolerance : Math.Abs(e.Start.X - e.End.X) <= _tolerance)
			&& Math.Abs((horizontal ? e.Start.Y : e.Start.X) - feature.CrossPosition) <= _tolerance).ToList();
		if (seeds.Count == 0 || sources.Any(s => !seeds.Any(e => e.SourceKeys.Contains(s)))) { return; }
		if (seeds.Count > 1 && BoundaryLineRuns.Find(_graph, _tolerance).Count(r => r.Edges.Count == seeds.Count
			&& seeds.All(r.Edges.Contains)) != 1) { return; }
		Func<Point2D, OutlineTopologyEdge> nearest = p => seeds.OrderBy(e => Math.Min(Math.Abs(axis(e.Start) - axis(p)), Math.Abs(axis(e.End) - axis(p)))).First();
		var additions = new List<string>();
		Point2D first = feature.FirstPoint, second = feature.SecondPoint;
		bool firstReal = ValidateBoundaryAttachments(first, first) == true, secondReal = ValidateBoundaryAttachments(second, second) == true;
		Func<Point2D, bool> onSeed = p => seeds.Any(e => OutlineGeometryQuery.IsPointOnSegment(p, new Segment2D(e.Start, e.End), _tolerance));
		if ((firstReal && !onSeed(first)) || (secondReal && !onSeed(second))) { return; }
		if (!firstReal && !TryRecoverExpandedEndpoint(feature, first, nearest(first), outline, out first, additions)) { return; }
		if (!secondReal && !TryRecoverExpandedEndpoint(feature, second, nearest(second), outline, out second, additions)) { return; }
		if (ValidateBoundaryAttachments(first, second) != true) { return; }
		feature.FirstPoint = first; feature.SecondPoint = second;
		foreach (string source in additions.Where(s => !feature.SupportSourceKeys.Contains(s))) { feature.SupportSourceKeys.Add(source); }
		feature.BoundaryEvidence += "|Relation=ExpandedAcrossRecognizedCorner|RealBoundaryEndpoints=True|Sources=" + string.Join(",", additions);
	}

	private bool TryRecoverExpandedEndpoint(StructureFeature feature, Point2D expected, OutlineTopologyEdge seed,
		OutlineFeature2D outline, out Point2D recovered, List<string> additions)
	{
		recovered = expected;
		bool horizontal = feature.Axis == StructureFeatureAxis.Horizontal;
		Func<Point2D, double> axis = p => horizontal ? p.X : p.Y;
		Func<Point2D, double> cross = p => horizontal ? p.Y : p.X;
		if (horizontal ? Math.Abs(seed.Start.Y - seed.End.Y) > _tolerance : Math.Abs(seed.Start.X - seed.End.X) > _tolerance) { return false; }
		double target = axis(expected), low = Math.Min(axis(seed.Start), axis(seed.End)), high = Math.Max(axis(seed.Start), axis(seed.End));
		if (target >= low - _tolerance && target <= high + _tolerance) { return false; }
		bool towardHigh = target > high;
		bool startIsEnd = towardHigh ? axis(seed.End) > axis(seed.Start) : axis(seed.End) < axis(seed.Start);
		int node = startIsEnd ? seed.EndNode : seed.StartNode;
		Point2D current = startIsEnd ? seed.End : seed.Start;
		var path = _graph.BoundaryPath;
		int forwardIndex = path.FindIndex(s => s.FromNode == node && s.EdgeId != seed.Id);
		int backwardIndex = path.FindIndex(s => s.ToNode == node && s.EdgeId != seed.Id);
		if ((forwardIndex >= 0) == (backwardIndex >= 0)) { return false; }
		int direction = forwardIndex >= 0 ? 1 : -1, index = forwardIndex >= 0 ? forwardIndex : backwardIndex;
		object corner = null;
		var walked = new List<string>();
		for (int count = 0; count < path.Count; count++)
		{
			if (!_graph.Nodes.Any(n => n.Id == node && n.IsBoundary && n.Degree == 2)) { return false; }
			var step = path[index]; var edge = _graph.Edges.Single(e => e.Id == step.EdgeId);
			bool forward = direction > 0 ? step.Forward : !step.Forward;
			Point2D next = forward ? edge.End : edge.Start;
			double advance = (axis(next) - axis(current)) * (towardHigh ? 1 : -1);
			if (advance < -_tolerance || (axis(next) - target) * (towardHigh ? 1 : -1) > _tolerance
				|| string.IsNullOrEmpty(edge.SourceKey)) { return false; }
			object edgeCorner = null;
			if (edge.Kind == "Arc")
			{
				edgeCorner = outline.Fillets.FirstOrDefault(f => Math.Abs(f.Radius - edge.Radius) <= _tolerance
					&& f.Center.DistanceTo(edge.Center) <= _tolerance && PairMatches(f.StartPoint, f.EndPoint, edge.Start, edge.End));
				if (edgeCorner == null || edge.Radius <= _tolerance
					|| Math.Abs(Math.Abs(edge.Bulge) - Math.Tan(Math.PI / 8)) > _tolerance / edge.Radius
					|| !((Math.Abs(axis(edge.Start) - axis(edge.Center)) <= _tolerance && Math.Abs(cross(edge.End) - cross(edge.Center)) <= _tolerance)
						|| (Math.Abs(axis(edge.End) - axis(edge.Center)) <= _tolerance && Math.Abs(cross(edge.Start) - cross(edge.Center)) <= _tolerance))) { return false; }
			}
			else if (edge.Kind == "Line" && Math.Abs(cross(edge.Start) - cross(edge.End)) <= _tolerance)
			{
				if (corner != null || Math.Abs(cross(edge.Start) - feature.CrossPosition) > _tolerance) { return false; }
			}
			else if (edge.Kind == "Line")
			{
				edgeCorner = outline.Chamfers.FirstOrDefault(c => IsChamferEdge(edge, c));
				if (edgeCorner == null) { return false; }
			}
			else { return false; }
			if (edgeCorner != null && corner != null && !ReferenceEquals(edgeCorner, corner)) { return false; }
			if (edgeCorner != null) { corner = edgeCorner; }
			walked.AddRange(edge.SourceKeys.Count > 0 ? edge.SourceKeys : new List<string> { edge.SourceKey });
			if (Math.Abs(axis(next) - target) <= _tolerance && corner != null)
			{
				recovered = next; additions.AddRange(walked.Where(s => !string.IsNullOrEmpty(s)).Distinct()); return true;
			}
			current = next; node = direction > 0 ? step.ToNode : step.FromNode;
			index = (index + direction + path.Count) % path.Count;
		}
		return false;
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

	private bool IsChamferEdge(OutlineTopologyEdge edge, ChamferFeature2D chamfer)
	{
		if (chamfer.Value <= _tolerance) { return false; }
		if (PairMatches(chamfer.StartPoint, chamfer.EndPoint, edge.Start, edge.End)) { return true; }
		return edge.SourceKeys.Any(key => SplitBoundaryChamferRules.ContainsSegment(chamfer,
			new Segment2D(edge.Start, edge.End) { SourceKey = key }, _tolerance));
	}
}
