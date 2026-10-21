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
	private bool ChooseStepDatumMinimum(DimensionPlan plan, OutlineFeature2D boundary, Datum2D datum,
		bool horizontal, bool geometricMinimum, out List<StructureDatumMarker2D> selected, out string reason)
	{
		double tol = _config.GeometryTolerance;
		string axis = horizontal ? "X" : "Y";
		string kind = horizontal ? "BlockInsertionVertical" : "BlockInsertion";
		var markers = datum.StructureDatumMarkers.Where(m => m != null && StructureDatumRules.IsDatumBlock(m.BlockName)
			&& m.ContactKind == kind).ToList();
		var valid = new List<StructureDatumMarker2D>();
		foreach (var marker in markers)
		{
			if (!string.IsNullOrEmpty(marker.ValidationFailure)
				|| !OutlineGeometryQuery.IsPointOnBoundary(marker.ContactPoint, boundary, tol))
			{
				plan.Diagnostics.Warnings.Add("StepDatum.PendingHumanConfirmation|Axis=" + axis
					+ "|Reason=UnverifiedBlockContact|Block=" + marker.BlockName + "|Source=" + marker.SourceGeometryId
					+ "|Detail=" + (marker.ValidationFailure ?? "MissingInsertionEvidence") + "|Fallback=Geometry");
				continue;
			}
			valid.Add(marker);
		}
		double min = horizontal ? boundary.MinX : boundary.MinY;
		double max = horizontal ? boundary.MaxX : boundary.MaxY;
		var minimum = valid.Where(m => Math.Abs((horizontal ? m.ContactPoint.X : m.ContactPoint.Y) - min) <= tol).ToList();
		var maximum = valid.Where(m => Math.Abs((horizontal ? m.ContactPoint.X : m.ContactPoint.Y) - max) <= tol).ToList();
		selected = new List<StructureDatumMarker2D>();
		bool? userMinimum = horizontal ? datum.StructureDatumMinimumX : datum.StructureDatumMinimumY;
		if (minimum.Count > 0 && maximum.Count > 0 && userMinimum.HasValue)
		{
			selected = userMinimum.Value ? minimum : maximum;
			reason = "UserConfirmedBoundaryBlock";
			return userMinimum.Value;
		}
		if ((minimum.Count > 0) != (maximum.Count > 0))
		{
			selected = minimum.Count > 0 ? minimum : maximum;
			reason = "ConfirmedBoundaryBlock";
			return minimum.Count > 0;
		}
		string failure = minimum.Count > 0 ? (horizontal ? "ConflictingLeftRightBlocks" : "ConflictingTopBottomBlocks") : "NoBlockEvidence";
		plan.Diagnostics.Warnings.Add("StepDatum.PendingHumanConfirmation|Axis=" + axis + "|Reason=" + failure + "|Fallback=Geometry");
		reason = "GeometryClearExtensionThenShorterSpan";
		return geometricMinimum;
	}

	private bool PreferMinimumStepDatum(OutlineFeature2D boundary, IEnumerable<double> targets,
		bool horizontal, DimensionSide side)
	{
		double tol = _config.GeometryTolerance;
		double min = horizontal ? boundary.MinX : boundary.MinY;
		double max = horizontal ? boundary.MaxX : boundary.MaxY;
		bool bestMinimum = false;
		double bestDepth = double.PositiveInfinity;
		double bestSpan = double.PositiveInfinity;
		foreach (bool minimum in new[] { false, true })
		{
			double coordinate = minimum ? min : max;
			var supports = boundary.Segments.Where(e => horizontal
				? e.IsVertical(tol) && Math.Abs(e.MinX - coordinate) <= tol
				: e.IsHorizontal(tol) && Math.Abs(e.MinY - coordinate) <= tol);
			var points = supports.SelectMany(e => new[] { e.Start, e.End }).Where(p => !ExtensionCrossesOutline(p,
				horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical, side, boundary)).ToList();
			if (points.Count == 0) { continue; }
			double depth = points.Min(p => side == DimensionSide.Top ? boundary.MaxY - p.Y
				: side == DimensionSide.Bottom ? p.Y - boundary.MinY
				: side == DimensionSide.Left ? p.X - boundary.MinX : boundary.MaxX - p.X);
			double span = targets.Sum(value => Math.Abs(value - coordinate));
			if (depth < bestDepth - tol || (Math.Abs(depth - bestDepth) <= tol && span < bestSpan - tol))
			{
				bestMinimum = minimum;
				bestDepth = depth;
				bestSpan = span;
			}
		}
		return bestMinimum;
	}

	private void ApplyHorizontalStepDatumDimensions(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum)
	{
		double tol = _config.GeometryTolerance;
		var ownership = new StructureBoundaryEvidence(plan.Diagnostics.OutlineTopology, tol);
		if (!ownership.IsAvailable) { return; }
		var boundary = ownership.BoundaryOutline;
		if (boundary.Segments.Any(e => !e.IsHorizontal(tol) && !e.IsVertical(tol))) { return; }
		var ledges = boundary.Segments.Where(e => e.IsHorizontal(tol) && e.MinY > outline.MinY + tol && e.MinY < outline.MaxY - tol).ToList();
		bool left = ledges.Any(e => Math.Abs(e.MinX - outline.MinX) <= tol);
		bool right = ledges.Any(e => Math.Abs(e.MaxX - outline.MaxX) <= tol);
		if (left == right || !TryGetStepDatumBoundaryChain(plan.Diagnostics.OutlineTopology, outline,
			left ? DimensionSide.Left : DimensionSide.Right, out var chain)) { return; }
		var groups = new List<Tuple<DimensionSide, List<OutlineTopologyEdge>>>();
		foreach (var placement in new[] { DimensionSide.Bottom, DimensionSide.Top })
		{
			bool bottom = placement == DimensionSide.Bottom;
			var targets = chain.Where(e => Math.Abs(e.Start.X - e.End.X) <= tol
				&& e.Start.X > outline.MinX + tol && e.Start.X < outline.MaxX - tol
				&& (bottom ? Math.Max(e.Start.Y, e.End.Y) < outline.MaxY - tol : Math.Min(e.Start.Y, e.End.Y) > outline.MinY + tol)
				&& new[] { e.Start, e.End }.Any(p => !ExtensionCrossesOutline(p, DimensionOrientation.Horizontal, placement, boundary)))
				.OrderBy(e => e.Start.X).ThenBy(e => e.SourceKey, StringComparer.Ordinal).ToList();
			if (targets.Count > 0 && targets.Last().Start.X - targets.First().Start.X > tol) { groups.Add(Tuple.Create(placement, targets)); }
		}
		if (groups.Count == 0) { return; }
		if (groups.Count > 1)
		{
			plan.Diagnostics.Warnings.Add("StepDatum.PendingHumanConfirmation|Axis=X|Reason=AmbiguousStructureSide");
			return;
		}
		var group = groups[0];
		var edges = group.Item2;
		bool minimum = ChooseStepDatumMinimum(plan, boundary, datum, horizontal: true,
			geometricMinimum: PreferMinimumStepDatum(boundary, edges.Select(e => e.Start.X), horizontal: true, side: group.Item1),
			out var markers, out string reason);
		double baseX = minimum ? outline.MinX : outline.MaxX;
		var supports = boundary.Segments.Where(e => e.IsVertical(tol) && Math.Abs(e.MinX - baseX) <= tol).ToList();
		var basePoints = supports.SelectMany(e => new[] { e.Start, e.End })
			.Where(p => !ExtensionCrossesOutline(p, DimensionOrientation.Horizontal, group.Item1, boundary));
		if (!basePoints.Any())
		{
			plan.Diagnostics.Warnings.Add("StepDatum.PendingHumanConfirmation|Axis=X|Reason=ExtensionCrossesOutline");
			return;
		}
		Point2D basePoint = group.Item1 == DimensionSide.Bottom ? basePoints.OrderBy(p => p.Y).First() : basePoints.OrderByDescending(p => p.Y).First();
		string groupKey = "StepDatum:X:" + group.Item1 + ":Datum=" + baseX.ToString("R", CultureInfo.InvariantCulture)
			+ ":" + string.Join(",", edges.Select(e => e.SourceKey).OrderBy(s => s, StringComparer.Ordinal));
		string evidence = "StepDatum|Axis=X|Datum=" + (minimum ? "Left" : "Right") + "|Side=" + group.Item1
			+ "|Reason=" + reason + "|GroupProof=ConnectedMonotoneBoundaryWalk|Blocks="
			+ string.Join(",", markers.Select(m => m.BlockName + ":" + m.SourceGeometryId).Distinct().OrderBy(s => s, StringComparer.Ordinal));
		var replacements = new List<PlannedDimension>();
		foreach (var edge in edges)
		{
			if (replacements.Any(d => Math.Abs(d.SecondPoint.X - edge.Start.X) <= tol)) { continue; }
			var points = new[] { edge.Start, edge.End }.Where(p => !ExtensionCrossesOutline(p, DimensionOrientation.Horizontal, group.Item1, boundary));
			Point2D target = group.Item1 == DimensionSide.Bottom ? points.OrderBy(p => p.Y).First() : points.OrderByDescending(p => p.Y).First();
			var sources = supports.Select(e => e.SourceKey).Concat(new[] { edge.SourceKey })
				.Concat(markers.Select(m => m.SourceGeometryId)).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
			var dimension = new PlannedDimension {
				Kind = DimensionKind.Normal, Role = DimensionCandidateRole.Structure, OwnerKind = DimensionCandidateOwnerKind.Outline,
				Orientation = DimensionOrientation.Horizontal, Side = group.Item1, FirstPoint = basePoint, SecondPoint = target,
				DebugRole = "StepDatumWidth", RuleId = StructureDatumRules.HorizontalCommonDatumRule, SourceKey = edge.SourceKey,
				SourceGeometryIds = sources, DatumFamilyKey = groupKey, DebugOwner = groupKey,
				TopologyEvidence = evidence + "|TargetX=" + target.X.ToString("R", CultureInfo.InvariantCulture),
				AttachmentKind = "ExteriorBoundary", AttachmentValidity = true, RequiresOuterBoundarySupport = true,
				PreservePreferredSide = true, ReadingLevel = DimensionReadingLevel.LocalSpacing };
			plan.Add(dimension);
			plan.RegisterStepDatumRequirement(dimension, tol);
			replacements.Add(dimension);
		}
		var groupSources = new HashSet<string>(chain.Select(e => e.SourceKey));
		var positions = replacements.Select(d => d.SecondPoint.X).Concat(new[] { outline.MinX, outline.MaxX }).ToList();
		foreach (var candidate in plan.Dimensions.ToList())
		{
			if (candidate.Orientation != DimensionOrientation.Horizontal || candidate.Side != group.Item1
				|| (candidate.DebugRole != "OutlineSegment" && candidate.DebugRole != "BottomStructWidth" && candidate.DebugRole != "TopStructWidth")
				|| !groupSources.Contains(candidate.SourceKey)
				|| !positions.Any(x => Math.Abs(x - candidate.FirstPoint.X) <= tol)
				|| !positions.Any(x => Math.Abs(x - candidate.SecondPoint.X) <= tol)
				|| !OutlineGeometryQuery.IsPointOnBoundary(candidate.FirstPoint, boundary, tol)
				|| !OutlineGeometryQuery.IsPointOnBoundary(candidate.SecondPoint, boundary, tol)) { continue; }
			var providers = replacements.Where(d => Math.Abs(d.SecondPoint.X - candidate.FirstPoint.X) <= tol
				|| Math.Abs(d.SecondPoint.X - candidate.SecondPoint.X) <= tol).ToList();
			if (Math.Abs(candidate.FirstPoint.X - (minimum ? outline.MaxX : outline.MinX)) <= tol
				|| Math.Abs(candidate.SecondPoint.X - (minimum ? outline.MaxX : outline.MinX)) <= tol)
			{ providers.AddRange(plan.Dimensions.Where(d => d.Kind == DimensionKind.OverallWidth)); }
			plan.TrySuppressCoveredStructure(candidate, "StepDatum.CoveredByCommonDatum", providers, null, evidence, allowOutlineSegment: true);
		}
		SuppressStepDatumDuplicateStructureDimensions(plan, replacements, evidence);
	}

	private void ApplyVerticalStepDatumDimensions(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum)
	{
		double tol = _config.GeometryTolerance;
		var ownership = new StructureBoundaryEvidence(plan.Diagnostics.OutlineTopology, tol);
		if (!ownership.IsAvailable) { return; }
		var boundary = ownership.BoundaryOutline;
		// Sloping bodies keep their existing feature rules; this pass owns orthogonal step levels only.
		if (boundary.Segments.Any(e => !e.IsHorizontal(tol) && !e.IsVertical(tol))) { return; }
		var ledges = boundary.Segments.Where(e => e.IsHorizontal(tol)
			&& e.MinY > outline.MinY + tol && e.MinY < outline.MaxY - tol).ToList();
		var levels = new List<double>();
		foreach (var edge in ledges.OrderBy(e => e.MinY))
		{
			if (!levels.Any(y => Math.Abs(y - edge.MinY) <= tol)) { levels.Add(edge.MinY); }
		}
		if (levels.Count < 2) { return; }
		bool left = ledges.Any(e => Math.Abs(e.MinX - outline.MinX) <= tol);
		bool right = ledges.Any(e => Math.Abs(e.MaxX - outline.MaxX) <= tol);
		if (left == right)
		{
			plan.Diagnostics.Warnings.Add("StepDatum.PendingHumanConfirmation|Reason=AmbiguousStructureSide");
			return;
		}
		DimensionSide side = left ? DimensionSide.Left : DimensionSide.Right;
		if (!TryGetStepDatumBoundaryChain(plan.Diagnostics.OutlineTopology, outline, side, out var chain))
		{
			plan.Diagnostics.Warnings.Add("StepDatum.PendingHumanConfirmation|Reason=NoUniqueConnectedStepGroup");
			return;
		}
		ledges = chain.Where(e => Math.Abs(e.Start.Y - e.End.Y) <= tol
			&& e.Start.Y > outline.MinY + tol && e.Start.Y < outline.MaxY - tol)
			.Select(e => new Segment2D(e.Start, e.End) { SourceKey = e.SourceKey }).ToList();
		levels.Clear();
		foreach (var edge in ledges.OrderBy(e => e.MinY))
		{
			if (!levels.Any(y => Math.Abs(y - edge.MinY) <= tol)) { levels.Add(edge.MinY); }
		}
		if (levels.Count < 2) { return; }
		bool bottom = ChooseStepDatumMinimum(plan, boundary, datum, horizontal: false,
			geometricMinimum: PreferMinimumStepDatum(boundary, levels, horizontal: false, side: side),
			out var selectedMarkers, out string datumReason);
		double baseY = bottom ? outline.MinY : outline.MaxY;
		var supports = boundary.Segments.Where(e => e.IsHorizontal(tol) && Math.Abs(e.MinY - baseY) <= tol).ToList();
		if (supports.Count == 0) { return; }
		var basePoints = supports.SelectMany(e => new[] { e.Start, e.End })
			.Where(p => !ExtensionCrossesOutline(p, DimensionOrientation.Vertical, side, boundary)).ToList();
		if (basePoints.Count == 0) { return; }
		Point2D basePoint = left ? basePoints.OrderBy(p => p.X).First() : basePoints.OrderByDescending(p => p.X).First();
		string evidence = "StepDatum|Axis=Y|Datum=" + (bottom ? "Bottom" : "Top")
			+ "|Side=" + side + "|Reason=" + datumReason + "|GroupProof=ConnectedMonotoneBoundaryWalk";
		evidence += "|Blocks=" + string.Join(",", selectedMarkers.Select(m => m.BlockName + ":" + m.SourceGeometryId).Distinct());
		var groupSources = new HashSet<string>(chain.Select(e => e.SourceKey).Where(s => !string.IsNullOrEmpty(s)));
		var boundarySources = new HashSet<string>(plan.Diagnostics.OutlineTopology.Edges.Where(e => e.IsBoundary).Select(e => e.SourceKey));
		var groupBoundary = new OutlineFeature2D();
		foreach (var edge in chain) { groupBoundary.Segments.Add(new Segment2D(edge.Start, edge.End) { SourceKey = edge.SourceKey }); }
		var groupKey = "StepDatum:" + side + ":" + string.Join(",", groupSources.OrderBy(s => s, StringComparer.Ordinal));
		var replacements = new List<PlannedDimension>();
		foreach (double y in levels)
		{
			var candidates = ledges.Where(e => Math.Abs(e.MinY - y) <= tol)
				.SelectMany(e => new[] { e.Start, e.End });
			Point2D target = left ? candidates.OrderBy(p => p.X).First() : candidates.OrderByDescending(p => p.X).First();
			var targetEdge = ledges.Where(e => Math.Abs(e.MinY - y) <= tol
				&& (e.Start.DistanceTo(target) <= tol || e.End.DistanceTo(target) <= tol)).OrderBy(e => e.SourceKey, StringComparer.Ordinal).First();
			if (ExtensionCrossesOutline(basePoint, DimensionOrientation.Vertical, side, boundary)
				|| ExtensionCrossesOutline(target, DimensionOrientation.Vertical, side, boundary))
			{
				plan.Diagnostics.Warnings.Add("StepDatum.PendingHumanConfirmation|Reason=ExtensionCrossesOutline|Y=" + y.ToString("R", CultureInfo.InvariantCulture));
				return;
			}
			var sources = ledges.Where(e => Math.Abs(e.MinY - y) <= tol).Select(e => e.SourceKey)
				.Concat(supports.Select(e => e.SourceKey)).Concat(selectedMarkers.Select(m => m.SourceGeometryId)).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
			replacements.Add(new PlannedDimension {
				Kind = DimensionKind.Normal, Role = DimensionCandidateRole.Structure, OwnerKind = DimensionCandidateOwnerKind.Outline,
				Orientation = DimensionOrientation.Vertical, Side = side, FirstPoint = basePoint, SecondPoint = target,
				DebugRole = "StepDatumHeight", RuleId = StructureDatumRules.VerticalCommonDatumRule, SourceGeometryIds = sources,
				SourceKey = targetEdge.SourceKey, DatumFamilyKey = groupKey, DebugOwner = groupKey,
				TopologyEvidence = evidence + "|TargetY=" + y.ToString("R", CultureInfo.InvariantCulture),
				AttachmentKind = "ExteriorBoundary", AttachmentValidity = true, RequiresOuterBoundarySupport = true,
				PreservePreferredSide = true, ReadingLevel = DimensionReadingLevel.LocalSpacing });
		}
		// Replace only un-toleranced spans between these proven levels, after every provider is valid.
		var coveredLevels = levels.Concat(new[] { outline.MinY, outline.MaxY }).ToList();
		foreach (var replacement in replacements)
		{
			plan.Add(replacement);
			plan.RegisterStepDatumRequirement(replacement, tol);
		}
		foreach (var candidate in plan.Dimensions.ToList())
		{
			if (candidate.Kind != DimensionKind.Normal || candidate.Orientation != DimensionOrientation.Vertical
				|| !string.IsNullOrEmpty(candidate.OverrideText) || candidate.ForceOuterLevel
				|| (candidate.Role != DimensionCandidateRole.Structure && candidate.Role != DimensionCandidateRole.OutlineSegment)
				|| (candidate.DebugRole != "LeftStructHeight" && candidate.DebugRole != "RightStructHeight" && candidate.DebugRole != "OutlineSegment")
				|| !coveredLevels.Any(y => Math.Abs(y - candidate.FirstPoint.Y) <= tol)
				|| !coveredLevels.Any(y => Math.Abs(y - candidate.SecondPoint.Y) <= tol)
				|| !groupSources.Any(source => string.Equals(candidate.SourceKey, source, StringComparison.Ordinal)
					|| (candidate.SourceKey ?? string.Empty).EndsWith(":" + source, StringComparison.Ordinal))) { continue; }
			// A borrowed support ID alone does not make a dimension part of this walk.
			bool onWalk = OutlineGeometryQuery.IsPointOnBoundary(candidate.FirstPoint, groupBoundary, tol)
				&& OutlineGeometryQuery.IsPointOnBoundary(candidate.SecondPoint, groupBoundary, tol);
			bool ledgeResidual = ((candidate.SourceKey ?? string.Empty).StartsWith("LedgeResidualUp:", StringComparison.Ordinal)
				|| (candidate.SourceKey ?? string.Empty).StartsWith("LedgeResidualDown:", StringComparison.Ordinal))
				&& (Math.Abs(candidate.FirstPoint.Y - baseY) <= tol || Math.Abs(candidate.SecondPoint.Y - baseY) <= tol)
				&& ledges.Any(e => (candidate.SourceKey ?? string.Empty).EndsWith(":" + e.SourceKey, StringComparison.Ordinal)
					&& (Math.Abs(candidate.FirstPoint.Y - e.MinY) <= tol || Math.Abs(candidate.SecondPoint.Y - e.MinY) <= tol));
			if (!onWalk && !ledgeResidual) { continue; }
			if ((candidate.SourceGeometryIds ?? new List<string>()).Any(s => boundarySources.Contains(s) && !groupSources.Contains(s))) { continue; }
			var providers = replacements.Where(d => Math.Abs(d.SecondPoint.Y - candidate.FirstPoint.Y) <= tol
				|| Math.Abs(d.SecondPoint.Y - candidate.SecondPoint.Y) <= tol).ToList();
			if (Math.Abs(candidate.FirstPoint.Y - (bottom ? outline.MaxY : outline.MinY)) <= tol
				|| Math.Abs(candidate.SecondPoint.Y - (bottom ? outline.MaxY : outline.MinY)) <= tol)
			{
				providers.AddRange(plan.Dimensions.Where(d => d.Kind == DimensionKind.OverallHeight));
			}
			plan.TrySuppressCoveredStructure(candidate, "StepDatum.CoveredByCommonDatum", providers, null, evidence, allowOutlineSegment: true);
		}
		SuppressStepDatumDuplicateStructureDimensions(plan, replacements, evidence);
	}

	private void SuppressStepDatumDuplicateStructureDimensions(DimensionPlan plan,
		List<PlannedDimension> providers, string evidence)
	{
		double tol = _config.GeometryTolerance;
		foreach (var candidate in plan.Dimensions.ToList())
		{
			if (candidate.Kind != DimensionKind.Normal || candidate.OwnerKind != DimensionCandidateOwnerKind.Outline
				|| (candidate.Role != DimensionCandidateRole.Structure && candidate.Role != DimensionCandidateRole.OutlineSegment)
				|| StructureDatumRules.IsCommonDatumRule(candidate.RuleId) || candidate.AttachmentValidity == false
				|| !string.IsNullOrWhiteSpace(candidate.OverrideText) || candidate.ForceOuterLevel) { continue; }
			var provider = providers.FirstOrDefault(d => plan.Dimensions.Contains(d) && d.AttachmentValidity == true
				&& candidate.Orientation == d.Orientation && candidate.Side == d.Side
				&& ((candidate.FirstPoint.DistanceTo(d.FirstPoint) <= tol && candidate.SecondPoint.DistanceTo(d.SecondPoint) <= tol)
					|| (candidate.FirstPoint.DistanceTo(d.SecondPoint) <= tol && candidate.SecondPoint.DistanceTo(d.FirstPoint) <= tol)));
			if (provider == null) { continue; }
			// The same real anchors prove replacement even when legacy source labels differ or are absent.
			candidate.SourceGeometryIds = DimensionCandidateSemantics.MergeSourceGeometryIds(candidate.SourceGeometryIds, provider.SourceGeometryIds);
			plan.TrySuppressCoveredStructure(candidate, "StepDatum.CoveredByCommonDatum", new[] { provider },
				provider.SourceGeometryIds, evidence + "|Relation=SameRealEndpointPair",
				replaceableRuleId: candidate.RuleId, allowOutlineSegment: true);
		}
	}

	private bool TryGetStepDatumBoundaryChain(OutlineTopologyDiagnostic graph, OutlineFeature2D outline,
		DimensionSide side, out List<OutlineTopologyEdge> chain)
	{
		chain = null;
		double tol = _config.GeometryTolerance;
		var edges = graph.Edges.Where(e => e.IsBoundary).ToList();
		var horizontal = edges.Where(e => e.Kind == "Line" && Math.Abs(e.Start.Y - e.End.Y) <= tol).ToList();
		var top = horizontal.Where(e => Math.Abs(e.Start.Y - outline.MaxY) <= tol).SelectMany(e => new[] { e.Start, e.End });
		var bottom = horizontal.Where(e => Math.Abs(e.Start.Y - outline.MinY) <= tol).SelectMany(e => new[] { e.Start, e.End });
		if (!top.Any() || !bottom.Any()) { return false; }
		Point2D first = side == DimensionSide.Left ? top.OrderBy(p => p.X).First() : top.OrderByDescending(p => p.X).First();
		Point2D last = side == DimensionSide.Left ? bottom.OrderBy(p => p.X).First() : bottom.OrderByDescending(p => p.X).First();
		var firstNode = graph.Nodes.FirstOrDefault(n => n.IsBoundary && n.Point.DistanceTo(first) <= tol);
		var lastNode = graph.Nodes.FirstOrDefault(n => n.IsBoundary && n.Point.DistanceTo(last) <= tol);
		if (firstNode == null || lastNode == null) { return false; }
		var matches = new List<List<OutlineTopologyEdge>>();
		foreach (var start in edges.Where(e => e.StartNode == firstNode.Id || e.EndNode == firstNode.Id))
		{
			var walk = new List<OutlineTopologyEdge>();
			var visited = new HashSet<int>();
			int node = firstNode.Id;
			var edge = start;
			while (visited.Add(edge.Id))
			{
				int next = edge.StartNode == node ? edge.EndNode : edge.StartNode;
				var from = graph.Nodes.First(n => n.Id == node).Point;
				var to = graph.Nodes.First(n => n.Id == next).Point;
				bool vertical = Math.Abs(to.X - from.X) <= tol;
				if (edge.Kind != "Line" || to.Y > from.Y + tol
					|| (!vertical && Math.Abs(to.Y - from.Y) > tol)
					|| (vertical && BoundaryProjectionSide(edge, graph, horizontal: false) != side)) { break; }
				walk.Add(edge);
				if (next == lastNode.Id) { matches.Add(walk); break; }
				var successors = edges.Where(e => e.Id != edge.Id && (e.StartNode == next || e.EndNode == next)).ToList();
				if (successors.Count != 1) { break; }
				node = next;
				edge = successors[0];
			}
		}
		if (matches.Count != 1) { return false; }
		chain = matches[0];
		return true;
	}
}
