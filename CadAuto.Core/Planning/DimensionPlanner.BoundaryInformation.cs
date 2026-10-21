using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning;

public sealed partial class DimensionPlanner
{
	private const string BoundaryRawEdgeRole = "BoundaryRawEdge";
	private const string BoundaryObliqueRole = "BoundaryObliqueProjection";
	private const string BoundaryChamferRole = "BoundaryChamferSpan";
	private const string BoundaryCoverageRule = "BoundaryInformation.Covered";

	private bool SameBoundaryPair(Point2D a, Point2D b, Point2D c, Point2D d) =>
		(a.DistanceTo(c) <= _config.GeometryTolerance && b.DistanceTo(d) <= _config.GeometryTolerance)
		|| (a.DistanceTo(d) <= _config.GeometryTolerance && b.DistanceTo(c) <= _config.GeometryTolerance);

	private bool IsBoundaryChamfer(OutlineTopologyEdge edge, OutlineFeature2D outline) => edge.Kind == "Line"
		&& outline.Chamfers.Any(c => c.Value > _config.GeometryTolerance
			&& SameBoundaryPair(edge.Start, edge.End, c.StartPoint, c.EndPoint));

	private bool HasBodyObliqueBoundary(DimensionPlan plan, OutlineFeature2D outline)
	{
		var ownership = new StructureBoundaryEvidence(plan.Diagnostics.OutlineTopology, _config.GeometryTolerance);
		return _config.UseFeatureFirstStructurePipeline && ownership.IsAvailable
			&& plan.Diagnostics.OutlineTopology.Edges.Any(e => e.IsBoundary && e.Kind == "Line"
				&& Math.Abs(e.End.X - e.Start.X) > _config.GeometryTolerance
				&& Math.Abs(e.End.Y - e.Start.Y) > _config.GeometryTolerance && !IsBoundaryChamfer(e, outline));
	}

	private double BoundarySignedArea(OutlineTopologyDiagnostic graph)
	{
		var origin = graph.Nodes[0].Point;
		double area = 0;
		foreach (var step in graph.BoundaryPath)
		{
			var edge = graph.Edges.Single(e => e.Id == step.EdgeId);
			var a = step.Forward ? edge.Start : edge.End;
			var b = step.Forward ? edge.End : edge.Start;
			area += (a.X - origin.X) * (b.Y - origin.Y) - (b.X - origin.X) * (a.Y - origin.Y);
			if (edge.Kind == "Arc")
			{
				double sweep = 4 * Math.Atan(edge.Bulge) * (step.Forward ? 1 : -1);
				area += edge.Radius * edge.Radius * (sweep - Math.Sin(sweep));
			}
		}
		return area;
	}

	private DimensionSide BoundaryProjectionSide(OutlineTopologyEdge edge, OutlineTopologyDiagnostic graph, bool horizontal)
	{
		var step = graph.BoundaryPath.Single(s => s.EdgeId == edge.Id);
		var a = step.Forward ? edge.Start : edge.End;
		var b = step.Forward ? edge.End : edge.Start;
		double winding = BoundarySignedArea(graph) < 0 ? 1 : -1;
		// Clockwise exterior has material on the right: the empty-side normal is (-dy, dx).
		double normal = winding * (horizontal ? b.X - a.X : a.Y - b.Y);
		return horizontal ? (normal > 0 ? DimensionSide.Top : DimensionSide.Bottom)
			: (normal > 0 ? DimensionSide.Right : DimensionSide.Left);
	}

	private bool TryChooseClearBoundaryEndpoint(OutlineTopologyDiagnostic graph, OutlineTopologyEdge sourceEdge,
		int sourceNodeId, int endpointIndex, Point2D endpoint, DimensionOrientation orientation, DimensionSide side,
		OutlineFeature2D outline, out Point2D replacement, out string evidence)
	{
		replacement = endpoint;
		evidence = null;
		if (outline == null || !ExtensionCrossesOutline(endpoint, orientation, side, outline)) { return false; }
		evidence = "ExtensionOverlapUnresolved=Endpoint" + endpointIndex + ";NoEquivalentAxisSupport";
		if (graph == null || graph.Status != "UniqueExteriorWalk" || !graph.IsClosedWalk || graph.HasRepeatedBoundaryEdges
			|| _config.FirstDimOffset < 0 || double.IsNaN(_config.FirstDimOffset) || double.IsInfinity(_config.FirstDimOffset))
		{
			return false;
		}

		double tolerance = _config.GeometryTolerance;
		bool horizontalExtension = orientation == DimensionOrientation.Vertical;
		bool horizontalMeasurement = orientation == DimensionOrientation.Horizontal;
		if ((orientation == DimensionOrientation.Vertical && side != DimensionSide.Left && side != DimensionSide.Right)
			|| (orientation == DimensionOrientation.Horizontal && side != DimensionSide.Top && side != DimensionSide.Bottom))
		{
			return false;
		}
		double measuredCoordinate = horizontalMeasurement ? endpoint.X : endpoint.Y;
		double dimLineCoordinate = side == DimensionSide.Left ? outline.MinX - _config.FirstDimOffset
			: side == DimensionSide.Right ? outline.MaxX + _config.FirstDimOffset
			: side == DimensionSide.Bottom ? outline.MinY - _config.FirstDimOffset
			: outline.MaxY + _config.FirstDimOffset;
		double currentDistance = Math.Abs((horizontalExtension ? endpoint.X : endpoint.Y) - dimLineCoordinate);
		var boundaryEdgeIds = new HashSet<int>(graph.BoundaryPath.Select(step => step.EdgeId));
		double bestDistance = currentDistance;
		OutlineTopologyNode bestNode = null;
		OutlineTopologyEdge bestSupport = null;
		foreach (var support in graph.Edges.Where(e => e.Id != sourceEdge.Id && e.IsBoundary
			&& boundaryEdgeIds.Contains(e.Id) && e.Kind == "Line"
			&& (e.StartNode == sourceNodeId || e.EndNode == sourceNodeId)
			&& (horizontalExtension ? Math.Abs(e.Start.Y - e.End.Y) <= tolerance : Math.Abs(e.Start.X - e.End.X) <= tolerance)
			&& !string.IsNullOrEmpty(e.SourceKey)).OrderBy(e => e.Id))
		{
			int candidateNodeId = support.StartNode == sourceNodeId ? support.EndNode : support.StartNode;
			var candidate = graph.Nodes.FirstOrDefault(n => n.Id == candidateNodeId && n.IsBoundary);
			if (candidate == null) { continue; }
			double candidateMeasuredCoordinate = horizontalMeasurement ? candidate.Point.X : candidate.Point.Y;
			if (Math.Abs(candidateMeasuredCoordinate - measuredCoordinate) > tolerance
				|| !MovesTowardDimensionLine(endpoint, candidate.Point, orientation, side, tolerance)
				|| ExtensionCrossesOutline(candidate.Point, orientation, side, outline)) { continue; }
			double candidateDistance = Math.Abs((horizontalExtension ? candidate.Point.X : candidate.Point.Y) - dimLineCoordinate);
			if (candidateDistance >= bestDistance - tolerance) { continue; }
			bestDistance = candidateDistance;
			bestNode = candidate;
			bestSupport = support;
		}
		if (bestNode == null) { return false; }

		replacement = bestNode.Point;
		evidence = string.Format(CultureInfo.InvariantCulture,
			"EquivalentAxisAttachment=Endpoint{0};OriginalNode={1};ReplacementNode={2};SupportEdge={3};MeasuredAxisDelta={4:0.#########}",
			endpointIndex, sourceNodeId, bestNode.Id, bestSupport.SourceKey,
			Math.Abs((horizontalMeasurement ? replacement.X : replacement.Y) - measuredCoordinate));
		return true;
	}

	private void ResolveStructureExtensionAttachments(DimensionPlan plan, OutlineFeature2D outline)
	{
		var graph = plan.Diagnostics.OutlineTopology;
		if (!new StructureBoundaryEvidence(graph, _config.GeometryTolerance).IsAvailable) { return; }
		foreach (var dimension in plan.Dimensions.Where(d => d.Kind == DimensionKind.Normal
			&& (d.Role == DimensionCandidateRole.Structure || d.Role == DimensionCandidateRole.OutlineSegment)))
		{
			for (int index = 1; index <= 2; index++)
			{
				Point2D point = index == 1 ? dimension.FirstPoint : dimension.SecondPoint;
				var node = graph.Nodes.FirstOrDefault(n => n.IsBoundary && n.Point.DistanceTo(point) <= _config.GeometryTolerance);
				if (node == null) { continue; }
				var source = graph.Edges.FirstOrDefault(e => e.IsBoundary && e.Kind == "Line"
					&& (e.StartNode == node.Id || e.EndNode == node.Id)
					&& (dimension.Orientation == DimensionOrientation.Horizontal
						? Math.Abs(e.Start.Y - e.End.Y) <= _config.GeometryTolerance
						: Math.Abs(e.Start.X - e.End.X) <= _config.GeometryTolerance));
				if (source == null) { continue; }
				Point2D replacement;
				string evidence;
				if (TryChooseClearBoundaryEndpoint(graph, source, node.Id, index, point, dimension.Orientation,
					dimension.Side, outline, out replacement, out evidence))
				{
					if (index == 1) { dimension.FirstPoint = replacement; } else { dimension.SecondPoint = replacement; }
				}
				if (evidence != null) { plan.RecordRuleEvidence(dimension, null, null, dimension.TopologyEvidence + ";" + evidence); }
			}
		}
	}

	private bool ExtensionCrossesOutline(Point2D point, DimensionOrientation orientation, DimensionSide side, OutlineFeature2D outline)
	{
		if (orientation == DimensionOrientation.Vertical)
		{
			return side == DimensionSide.Left
				? _structureSuppressionRules.LeftExtensionCrossesOutline(point, outline, _config.FirstDimOffset)
				: side == DimensionSide.Right && _structureSuppressionRules.RightExtensionCrossesOutline(point, outline, _config.FirstDimOffset);
		}
		return side == DimensionSide.Top
			? _structureSuppressionRules.TopExtensionCrossesOutline(point, outline, _config.FirstDimOffset)
			: side == DimensionSide.Bottom && _structureSuppressionRules.BottomExtensionCrossesOutline(point, outline, _config.FirstDimOffset);
	}

	private static bool MovesTowardDimensionLine(Point2D from, Point2D to, DimensionOrientation orientation,
		DimensionSide side, double tolerance)
	{
		if (orientation == DimensionOrientation.Vertical)
		{
			return side == DimensionSide.Left ? to.X < from.X - tolerance
				: side == DimensionSide.Right && to.X > from.X + tolerance;
		}
		return side == DimensionSide.Bottom ? to.Y < from.Y - tolerance
			: side == DimensionSide.Top && to.Y > from.Y + tolerance;
	}

	private PlannedDimension AddBoundaryInformationDimension(DimensionPlan plan, string key, string role,
		DimensionOrientation orientation, DimensionSide side, Point2D first, Point2D second, IEnumerable<string> sources, string proof)
	{
		var existing = plan.BoundaryInformationRequirements.FirstOrDefault(d => d.SourceKey == key && d.Orientation == orientation);
		if (existing != null) { return existing; }
		var dimension = new PlannedDimension { Kind = DimensionKind.Normal, Role = DimensionCandidateRole.Structure,
			OwnerKind = DimensionCandidateOwnerKind.Outline, Orientation = orientation, Side = side,
			FirstPoint = first, SecondPoint = second, SourceKey = key, SourceGeometryIds = sources.Distinct().ToList(),
			DebugRole = role, RuleId = role == BoundaryObliqueRole ? "BoundaryInformation.ObliqueProjection" : "BoundaryInformation.ChamferSpan",
			RequiresOuterBoundarySupport = true, AttachmentValidity = true, AttachmentKind = "ExteriorBoundary",
			PreservePreferredSide = true, ReadingLevel = DimensionReadingLevel.LocalSpacing,
			AlignmentKey = "Structure:" + (orientation == DimensionOrientation.Horizontal ? "H:" : "V:") + side,
			AlignmentPriority = 70, TopologyEvidence = proof + "|RealEndpoints=True|Side=ExteriorNormal" };
		plan.Add(dimension);
		plan.BoundaryInformationRequirements.Add(dimension);
		return dimension;
	}

	private void ApplyBoundaryInformationPlanning(DimensionPlan plan, OutlineFeature2D outline)
	{
		if (_config.DisabledExperimentRule == DimensionExperimentRule.BoundaryInformationCompletion) { return; }
		if (!HasBodyObliqueBoundary(plan, outline)) { return; }
		var graph = plan.Diagnostics.OutlineTopology;
		double tol = _config.GeometryTolerance;
		foreach (var raw in plan.Dimensions.Where(d => d.DebugRole == BoundaryRawEdgeRole))
		{
			if (!plan.BoundaryInformationRequirements.Contains(raw)) { plan.BoundaryInformationRequirements.Add(raw); }
		}
		foreach (var edge in graph.Edges.Where(e => e.IsBoundary && e.Kind == "Line"
			&& Math.Abs(e.End.X - e.Start.X) > tol && Math.Abs(e.End.Y - e.Start.Y) > tol && !IsBoundaryChamfer(e, outline)))
		{
			foreach (bool horizontal in new[] { true, false })
			{
				var orientation = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
				var side = BoundaryProjectionSide(edge, graph, horizontal);
				var first = edge.Start;
				var second = edge.End;
				var attachmentEvidence = new List<string>();
				if (TryChooseClearBoundaryEndpoint(graph, edge, edge.StartNode, 1, first, orientation, side, outline,
					out var replacement, out var evidence))
				{
					first = replacement;
				}
				if (!string.IsNullOrEmpty(evidence)) { attachmentEvidence.Add(evidence); }
				if (TryChooseClearBoundaryEndpoint(graph, edge, edge.EndNode, 2, second, orientation, side, outline,
					out replacement, out evidence))
				{
					second = replacement;
				}
				if (!string.IsNullOrEmpty(evidence)) { attachmentEvidence.Add(evidence); }
				string proof = "BoundaryInformation|Relation=BodyObliqueProjection|BoundaryEdge=" + edge.Id
					+ (attachmentEvidence.Count == 0 ? string.Empty : "|" + string.Join("|", attachmentEvidence));
				AddBoundaryInformationDimension(plan, "BoundaryInformation:Oblique:" + edge.SourceKey, BoundaryObliqueRole,
					orientation, side, first, second, new[] { edge.SourceKey }, proof);
			}
		}
		// A chamfer / straight face / chamfer route defines its full face width using
		// the two REAL shoulders. Never project those shoulders to a removed sharp corner.
		for (int i = 0; i < graph.BoundaryPath.Count; i++)
		{
			var steps = Enumerable.Range(0, 3).Select(n => graph.BoundaryPath[(i + n) % graph.BoundaryPath.Count]).ToList();
			var edges = steps.Select(s => graph.Edges.Single(e => e.Id == s.EdgeId)).ToList();
			if (!IsBoundaryChamfer(edges[0], outline) || !IsBoundaryChamfer(edges[2], outline) || edges[1].Kind != "Line") { continue; }
			bool horizontal = Math.Abs(edges[1].End.Y - edges[1].Start.Y) <= tol;
			bool vertical = Math.Abs(edges[1].End.X - edges[1].Start.X) <= tol;
			if (horizontal == vertical) { continue; }
			var a = steps[0].Forward ? edges[0].Start : edges[0].End;
			var b = steps[2].Forward ? edges[2].End : edges[2].Start;
			Func<Point2D, double> axis = p => horizontal ? p.X : p.Y;
			Func<Point2D, double> cross = p => horizontal ? p.Y : p.X;
			if (Math.Abs(cross(a) - cross(b)) > tol || Math.Abs(axis(b) - axis(a)) <= tol) { continue; }
			double sign = Math.Sign(axis(b) - axis(a));
			if (steps.Select((s, n) => new { s, e = edges[n] }).Any(x =>
				(axis(x.s.Forward ? x.e.End : x.e.Start) - axis(x.s.Forward ? x.e.Start : x.e.End)) * sign < -tol)) { continue; }
			AddBoundaryInformationDimension(plan, "BoundaryInformation:ChamferSpan:" + edges[1].SourceKey, BoundaryChamferRole,
				horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical,
				BoundaryProjectionSide(edges[1], graph, horizontal), a, b, edges.Select(e => e.SourceKey),
				"BoundaryInformation|Relation=RecognizedChamferFaceChamfer|BoundaryEdges=" + string.Join(",", edges.Select(e => e.Id)));
		}

		// Restore a valid original/requirement if an earlier pass removed it without a
		// currently live information replacement. Reviewed coverage may then remove it again.
		foreach (var required in plan.BoundaryInformationRequirements.Where(d => !plan.Dimensions.Contains(d)).ToList())
		{
			if (FindBoundaryInformationProof(plan, outline, required) != null) { continue; }
			if (new StructureBoundaryEvidence(graph, tol).ValidateBoundaryAttachments(required.FirstPoint, required.SecondPoint) != true) { continue; }
			plan.RestoreInformationDimension(required);
		}
		// Prefer meaningful slope projections/face widths over their raw remainder edges.
		var targets = plan.BoundaryInformationRequirements.OrderBy(d => d.DebugRole == BoundaryRawEdgeRole ? 0 : 1).ToList();
		foreach (var candidate in targets)
		{
			if (!plan.Dimensions.Contains(candidate) || plan.IsCoverageProvider(candidate)
				|| StructureCoveragePolicy.GetIneligibilityReason(candidate, candidate.RuleId) != null) { continue; }
			var proof = FindBoundaryInformationProof(plan, outline, candidate);
			if (proof == null) { continue; }
			// Keep useful envelope anchors (e.g. the top run) rather than swapping them
			// for a slope projection merely because the two form an algebraic cycle.
			if (candidate.DebugRole == BoundaryRawEdgeRole && IsBoundaryRawAnchor(candidate, outline)
				&& (proof.Count != 1 || proof[0].Provider == null || proof[0].Provider.DebugRole == BoundaryObliqueRole)) { continue; }
			var providers = proof.Where(c => c.Provider != null).Select(c => c.Provider).Distinct().ToList();
			plan.TrySuppressCoveredStructure(candidate, BoundaryCoverageRule, providers,
				proof.SelectMany(c => c.Sources).Distinct(),
				"BoundaryInformation|Relation=ConnectedProjectedConstraintPath|StationIdentity=PerpendicularExteriorWalkOnly"
				+ "|ConstraintSources=" + string.Join(",", proof.SelectMany(c => c.Sources).Distinct())
				+ "|RecognizedChamferLegs=" + proof.Count(c => c.Provider == null), candidate.RuleId);
		}
		AuditBoundaryInformationPlan(plan, outline);
	}

	private sealed class BoundaryInformationConstraint
	{
		public int From;
		public int To;
		public PlannedDimension Provider;
		public IEnumerable<string> Sources;
	}

	private List<BoundaryInformationConstraint> FindBoundaryInformationProof(DimensionPlan plan, OutlineFeature2D outline, PlannedDimension target)
	{
		var graph = plan.Diagnostics.OutlineTopology;
		var ownership = new StructureBoundaryEvidence(graph, _config.GeometryTolerance);
		if (!ownership.IsAvailable) { return null; }
		double tol = _config.GeometryTolerance;
		bool horizontal = target.Orientation == DimensionOrientation.Horizontal;
		Func<Point2D, double> axis = p => horizontal ? p.X : p.Y;
		var parent = graph.Nodes.ToDictionary(n => n.Id, n => n.Id);
		Func<int, int> root = null;
		root = id => parent[id] == id ? id : parent[id] = root(parent[id]);
		foreach (var edge in graph.Edges.Where(e => e.IsBoundary && e.Kind == "Line" && Math.Abs(axis(e.Start) - axis(e.End)) <= tol))
		{
			parent[root(edge.StartNode)] = root(edge.EndNode);
		}
		Func<Point2D, int?> station = p =>
		{
			var nodes = graph.Nodes.Where(n => n.IsBoundary && n.Point.DistanceTo(p) <= tol).ToList();
			return nodes.Count == 1 ? (int?)root(nodes[0].Id) : null;
		};
		int? start = station(target.FirstPoint), end = station(target.SecondPoint);
		if (!start.HasValue || !end.HasValue || start == end) { return null; }
		var constraints = new List<BoundaryInformationConstraint>();
		foreach (var dimension in plan.Dimensions.Where(d => d != target && d.Orientation == target.Orientation
			&& string.IsNullOrWhiteSpace(d.OverrideText) && (d.Role == DimensionCandidateRole.Overall
				|| (d.Role == DimensionCandidateRole.Structure && d.RequiresOuterBoundarySupport))))
		{
			if (dimension.DebugRole == BoundaryRawEdgeRole && !IsBoundaryRawAnchor(dimension, outline)) { continue; }
			if (ownership.ValidateBoundaryAttachments(dimension.FirstPoint, dimension.SecondPoint) != true) { continue; }
			int? a = station(dimension.FirstPoint), b = station(dimension.SecondPoint);
			if (!a.HasValue || !b.HasValue || a == b) { continue; }
			constraints.Add(new BoundaryInformationConstraint { From = a.Value, To = b.Value, Provider = dimension,
				Sources = (dimension.SourceGeometryIds ?? new List<string>())
					.Where(key => graph.Edges.Any(e => e.IsBoundary && e.SourceKey == key)).ToList() });
		}
		foreach (var edge in graph.Edges.Where(e => e.IsBoundary && IsBoundaryChamfer(e, outline)))
		{
			int a = root(edge.StartNode), b = root(edge.EndNode);
			if (a != b) { constraints.Add(new BoundaryInformationConstraint { From = a, To = b, Sources = new[] { edge.SourceKey } }); }
		}
		constraints = constraints.OrderBy(c => c.Provider == null ? 5 : c.Provider.Role == DimensionCandidateRole.Overall ? 0
			: c.Provider.DebugRole == BoundaryChamferRole ? 2 : c.Provider.DebugRole == BoundaryObliqueRole ? 3
			: c.Provider.DebugRole == BoundaryRawEdgeRole ? 4 : 1).ThenBy(c => c.Provider?.DiagnosticId ?? 0).ToList();
		var visited = new HashSet<int> { start.Value };
		var routes = new Dictionary<int, List<BoundaryInformationConstraint>> { [start.Value] = new List<BoundaryInformationConstraint>() };
		var queue = new Queue<int>(); queue.Enqueue(start.Value);
		while (queue.Count > 0)
		{
			int at = queue.Dequeue();
			foreach (var c in constraints.Where(c => c.From == at || c.To == at))
			{
				int next = c.From == at ? c.To : c.From;
				if (!visited.Add(next)) { continue; }
				var route = routes[at].Concat(new[] { c }).ToList();
				if (next == end.Value) { return route.Any(item => item.Provider != null) ? route : null; }
				routes[next] = route; queue.Enqueue(next);
			}
		}
		return null;
	}

	private bool IsBoundaryRawAnchor(PlannedDimension dimension, OutlineFeature2D outline)
	{
		bool horizontal = dimension.Orientation == DimensionOrientation.Horizontal;
		double cross = horizontal ? dimension.FirstPoint.Y : dimension.FirstPoint.X;
		if (Math.Abs(cross - (horizontal ? outline.MinY : outline.MinX)) > _config.GeometryTolerance
			&& Math.Abs(cross - (horizontal ? outline.MaxY : outline.MaxX)) > _config.GeometryTolerance) { return false; }
		return !outline.Chamfers.Any(c => PointsEqual(dimension.FirstPoint, c.StartPoint) || PointsEqual(dimension.FirstPoint, c.EndPoint)
			|| PointsEqual(dimension.SecondPoint, c.StartPoint) || PointsEqual(dimension.SecondPoint, c.EndPoint));
	}

	private void AuditBoundaryInformationPlan(DimensionPlan plan, OutlineFeature2D outline)
	{
		if (plan.BoundaryInformationRequirements.Count == 0) { return; }
		var report = plan.Diagnostics;
		report.OutlineInformationDecisions.Clear();
		bool pending = false;
		foreach (var required in plan.BoundaryInformationRequirements)
		{
			bool direct = plan.Dimensions.Contains(required) && required.AttachmentValidity == true;
			var proof = direct ? null : FindBoundaryInformationProof(plan, outline, required);
			string decision = direct ? "Direct" : proof != null ? "CoveredByLiveConstraints" : "PendingConfirmation";
			pending |= !direct && proof == null;
			report.OutlineInformationDecisions.Add("Source=" + required.SourceKey + "|Axis=" + required.Orientation + "|Decision=" + decision
				+ "|Providers=" + (direct ? required.DiagnosticId.ToString() : proof == null ? "" : string.Join(",", proof.Where(c => c.Provider != null).Select(c => c.Provider.DiagnosticId).Distinct())));
		}
		report.OutlineInformationStatus = pending ? "PendingConfirmation" : "CompleteForRecognizedBoundaryRequirements";
	}
}
