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
	private static readonly HashSet<string> LegacyStepDebugRoles = new HashSet<string>(StringComparer.Ordinal)
	{
		"TopStructWidth", "BottomStructWidth", "LeftStructHeight", "RightStructHeight",
		"TopChamferedStepWidth", "RightChamferedStepHeight", "OutlineSegment", BoundaryRawEdgeRole, BoundaryChamferRole
	};

	private static readonly HashSet<string> LegacyStepRuleIds = new HashSet<string>(StringComparer.Ordinal)
	{
		"LProfile.VerticalArmWidth", "LProfile.HorizontalArmHeight", "LProfile.HorizontalArmTotalHeight",
		"LProfile.DerivedInnerEdge", "LProfile.DerivedOuterContourSegment", "LProfile.ChamferComplement",
		"LProfile.ChamferedArmResidual", "LProfile.SecondaryStepRiser", "LProfile.LocalAnchorRepair",
		"LProfile.CoveredStepWidth", "LProfile.CoveredStepHeight", "BoundaryInformation.ChamferSpan",
		SuppressReason.OuterContourStepOverallRemainder
	};

	// Every step face is measured once from the datum edge; face lengths, chamfer legs and fillet radii follow from those and the callouts.
	private bool ApplyDatumBaselineStructureDimensions(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum)
	{
		double tol = _config.GeometryTolerance;
		var graph = plan.Diagnostics.OutlineTopology;
		var ownership = new StructureBoundaryEvidence(graph, tol);
		if (!ownership.IsAvailable) { return false; }
		if (!DatumBaselineRules.Supports(graph, outline.Chamfers, outline.Fillets, tol, out string reason))
		{
			RecordStructureDatumCheck(plan, "Baseline|Decision=OutOfScope|Reason=" + reason);
			return false;
		}
		// L 型件（含内角处带一个局部小台阶的）走 LProfile 专属规则，不按基准边重新标注。判定与 L 型规则共用。
		LProfileShape shape = LProfileShapeRules.Classify(graph, tol, DimensionRuleConfig.LProfileLocalStepMaxDepthRatio);
		if (shape.IsLProfile)
		{
			RecordStructureDatumCheck(plan, "Baseline|Decision=OutOfScope|Reason=LProfile|" + shape.Evidence);
			return false;
		}
		var boundary = ownership.BoundaryOutline;
		bool minimumX = ChooseStructureDatumMinimum(plan, boundary, datum, true, () => true, out var markersX, out string reasonX);
		bool minimumY = ChooseStructureDatumMinimum(plan, boundary, datum, false, () => true, out var markersY, out string reasonY);
		if (!DatumBaselineRules.TryPlan(graph, outline.Chamfers, outline.Fillets, minimumX, minimumY, tol, out var layout, out reason))
		{
			RecordStructureDatumCheck(plan, "Baseline|Decision=OutOfScope|Reason=" + reason);
			return false;
		}
		var scope = ownership.CreateOuterGeometryScope(outline);
		var corners = scope.Chamfers.Select(c => Tuple.Create(c.StartPoint, c.EndPoint))
			.Concat(scope.Fillets.Select(f => Tuple.Create(f.StartPoint, f.EndPoint))).ToList();
		foreach (bool horizontal in new[] { true, false })
		{
			var axis = horizontal ? layout.AlongX : layout.AlongY;
			var markers = horizontal ? markersX : markersY;
			string name = horizontal ? "X" : "Y";
			Func<Point2D, double> coordinate = p => horizontal ? p.X : p.Y;
			var orientation = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
			var preferred = horizontal ? layout.AlongY.Minimum ? DimensionSide.Bottom : DimensionSide.Top
				: layout.AlongX.Minimum ? DimensionSide.Left : DimensionSide.Right;
			var opposite = preferred == DimensionSide.Bottom ? DimensionSide.Top : preferred == DimensionSide.Top ? DimensionSide.Bottom
				: preferred == DimensionSide.Left ? DimensionSide.Right : DimensionSide.Left;
			string datumText = axis.Datum.ToString("R", CultureInfo.InvariantCulture);
			string evidence = "DatumBaseline|Axis=" + name + "|Datum=" + (horizontal ? axis.Minimum ? "Left" : "Right" : axis.Minimum ? "Bottom" : "Top")
				+ "|Reason=" + (horizontal ? reasonX : reasonY) + "|BoundaryProof=UniqueExteriorWalk|Blocks="
				+ string.Join(",", markers.Select(m => m.BlockName + ":" + m.SourceGeometryId).Distinct().OrderBy(s => s, StringComparer.Ordinal));
			// Each known axis position lists the live dimensions that fix it relative to the datum.
			var known = new List<Tuple<double, List<PlannedDimension>>> { Tuple.Create(axis.Datum, new List<PlannedDimension>()) };
			var overall = plan.Dimensions.Where(d => d.Kind == (horizontal ? DimensionKind.OverallWidth : DimensionKind.OverallHeight)).ToList();
			if (overall.Count > 0) { known.Add(Tuple.Create(axis.Minimum ? axis.Datum + axis.Span : axis.Datum - axis.Span, overall)); }
			var added = new List<string>();
			foreach (var target in axis.Targets)
			{
				PlannedDimension dimension = null;
				foreach (var side in new[] { preferred, opposite })
				{
					var points = target.Points.Where(p => !ExtensionCrossesOutline(p, orientation, side, boundary))
						.OrderBy(p => DistanceFromDimensionSide(p, side)).ToList();
					var anchors = axis.DatumPoints.Where(p => !ExtensionCrossesOutline(p, orientation, side, boundary))
						.OrderBy(p => DistanceFromDimensionSide(p, side)).ToList();
					if (points.Count == 0 || anchors.Count == 0) { continue; }
					// Fixed precision keeps the source identity stable under rotated planning frames.
					string distance = target.Distance.ToString("0.######", CultureInfo.InvariantCulture);
					string group = "DatumBaseline:" + name + ":" + side + ":Datum=" + datumText;
					dimension = new PlannedDimension { Kind = DimensionKind.Normal, Role = DimensionCandidateRole.Structure,
						OwnerKind = DimensionCandidateOwnerKind.Outline, Orientation = orientation, Side = side,
						FirstPoint = anchors[0], SecondPoint = points[0],
						DebugRole = horizontal ? "DatumBaselineWidth" : "DatumBaselineHeight",
						RuleId = horizontal ? StructureDatumRules.HorizontalBaselineRule : StructureDatumRules.VerticalBaselineRule,
						SourceKey = "DatumBaseline:" + name + ":" + distance,
						SourceGeometryIds = target.SourceGeometryIds.Concat(axis.DatumSources).Concat(markers.Select(m => m.SourceGeometryId))
							.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList(),
						DatumFamilyKey = group, DebugOwner = group,
						TopologyEvidence = evidence + "|Side=" + side + "|Target=" + distance + "|TargetReason=" + target.Reason,
						AttachmentKind = "ExteriorBoundary", AttachmentValidity = true, RequiresOuterBoundarySupport = true,
						FirstPointMustLieOnOutline = true, PreservePreferredSide = true, ReadingLevel = DimensionReadingLevel.LocalSpacing };
					break;
				}
				if (dimension == null)
				{
					string warning = "StepDatum.PendingHumanConfirmation|Axis=" + name + "|Reason=NoClearBaselineAttachment|Target="
						+ target.Distance.ToString("R", CultureInfo.InvariantCulture);
					if (!plan.Diagnostics.Warnings.Contains(warning)) { plan.Diagnostics.Warnings.Add(warning); }
					continue;
				}
				plan.Add(dimension);
				plan.RegisterStepDatumRequirement(dimension, tol);
				known.Add(Tuple.Create(coordinate(dimension.SecondPoint), new List<PlannedDimension> { dimension }));
				added.Add(target.Distance.ToString("0.###", CultureInfo.InvariantCulture));
			}
			Func<double, List<PlannedDimension>> find = c => known.Where(k => Math.Abs(k.Item1 - c) <= tol).Select(k => k.Item2).FirstOrDefault();
			// A chamfer or fillet callout fixes its other end from whichever end is already known.
			for (bool changed = true; changed;)
			{
				changed = false;
				foreach (var corner in corners)
				{
					var start = find(coordinate(corner.Item1));
					var end = find(coordinate(corner.Item2));
					if ((start == null) == (end == null)) { continue; }
					known.Add(Tuple.Create(coordinate(start == null ? corner.Item1 : corner.Item2), start ?? end));
					changed = true;
				}
			}
			// Targets that could not be dimensioned from the datum (NoClearBaselineAttachment) remain unmeasured.
			// Record them so we can distinguish legitimate local dimensions from erroneous internal-line dimensions.
			// Collect the absolute axis coordinates of targets that could not be dimensioned from the datum.
			// Use absolute coordinates (from target.Points) rather than distances so comparisons with
			// candidate endpoint coordinates work directly.
			var unmeasuredTargets = new HashSet<double>();
			foreach (var target in axis.Targets)
			{
				if (target.Points.Any() && find(target.Distance) == null)
				{
					foreach (var p in target.Points) { unmeasuredTargets.Add(coordinate(p)); }
				}
			}
			RecordStructureDatumCheck(plan, "Baseline|Axis=" + name + "|Decision=Applied|Datum=" + datumText + "|Targets=" + string.Join(",", added)
				+ "|Derived=" + string.Join(",", axis.Derived.Select(d => d.ToString("0.###", CultureInfo.InvariantCulture))));
			foreach (var candidate in plan.Dimensions.Where(d => d.Orientation == orientation).ToList())
			{
				string replaceable = LegacyStepRuleIds.Contains(candidate.RuleId ?? string.Empty) ? candidate.RuleId : null;
				string ineligible = StructureCoveragePolicy.GetIneligibilityReason(candidate, replaceable, allowOutlineSegment: true);
				bool onBoundary = ownership.ValidateBoundaryAttachments(candidate.FirstPoint, candidate.SecondPoint) == true;
				if ((replaceable == null && !LegacyStepDebugRoles.Contains(candidate.DebugRole ?? string.Empty))
					|| (ineligible != null && ineligible != "MissingSourceGeometry"))
				{
					continue;
				}
				if (!onBoundary)
				{
					// One or both endpoints are off the exterior boundary (e.g. on an internal line).
					// Find if the on-boundary endpoint is already covered, and if so, use it to suppress this artifact.
					var onBdFirst = find(coordinate(candidate.FirstPoint));
					var onBdSecond = find(coordinate(candidate.SecondPoint));
					var coveringForOffBoundary = (onBdFirst ?? onBdSecond) != null ? (onBdFirst ?? onBdSecond) : null;
					if (coveringForOffBoundary != null && coveringForOffBoundary.Count > 0)
					{
						plan.TrySuppressCoveredStructure(candidate, "DatumBaseline.OffBoundaryArtifact",
							coveringForOffBoundary,
							coveringForOffBoundary.SelectMany(d => d.SourceGeometryIds ?? new List<string>()).Distinct().ToList(),
							evidence + "|Reason=EndpointNotOnBoundaryOutline", replaceable, allowOutlineSegment: true);
					}
					else
					{
						RecordStructureDatumCheck(plan, "Baseline|Axis=" + name + "|Decision=Kept|CandidateId=" + candidate.DiagnosticId
							+ "|Reason=OffBoundaryNoProvider");
					}
					continue;
				}
				var first = find(coordinate(candidate.FirstPoint));
				var second = find(coordinate(candidate.SecondPoint));
				var providers = first == null || second == null ? null : first.Concat(second).Distinct().ToList();
				var sources = (providers ?? new List<PlannedDimension>()).SelectMany(d => d.SourceGeometryIds ?? new List<string>())
					.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
				if (providers == null || providers.Count == 0 || (ineligible != null && sources.Count == 0))
				{
					// Allow local dimensions that connect a measured position to an unmeasured target.
					bool isLocalComplement = providers == null
						&& ((first != null && unmeasuredTargets.Any(t => Math.Abs(t - coordinate(candidate.SecondPoint)) <= tol))
							|| (second != null && unmeasuredTargets.Any(t => Math.Abs(t - coordinate(candidate.FirstPoint)) <= tol)));
					if (!isLocalComplement)
					{
						// Suppress dimensions where one end is a measured/known position and the other
						// is neither a target nor known — catches internal-line artifacts.
						// Also covers MissingSourceGeometry (no source labels on old legacy dimensions).
						bool suppressInternalLine = providers == null && replaceable == null
							&& (ineligible == null || ineligible == "MissingSourceGeometry")
							&& ((first == null && second != null && !unmeasuredTargets.Any(t => Math.Abs(t - coordinate(candidate.FirstPoint)) <= tol))
								|| (second == null && first != null && !unmeasuredTargets.Any(t => Math.Abs(t - coordinate(candidate.SecondPoint)) <= tol)));
						// Also suppress dimensions where both ends are unknown but one end is an unmeasured target
						// and the other is not even a target (e.g. 96.5 from collinear split point y=10 to unmeasured y=106.5).
						bool suppressOrphanedTarget = !suppressInternalLine && providers == null && replaceable == null
							&& (ineligible == null || ineligible == "MissingSourceGeometry")
							&& first == null && second == null
							&& ((unmeasuredTargets.Any(t => Math.Abs(t - coordinate(candidate.FirstPoint)) <= tol)
									&& !unmeasuredTargets.Any(t => Math.Abs(t - coordinate(candidate.SecondPoint)) <= tol))
								|| (unmeasuredTargets.Any(t => Math.Abs(t - coordinate(candidate.SecondPoint)) <= tol)
									&& !unmeasuredTargets.Any(t => Math.Abs(t - coordinate(candidate.FirstPoint)) <= tol)));
						if (suppressInternalLine || suppressOrphanedTarget)
						{
							// Directly remove internal-line artifacts. They have no real geometry source
							// (MissingSourceGeometry) so TrySuppressCoveredStructure cannot be used.
							plan.RemoveStructureDimension(candidate, suppressInternalLine
								? "DatumBaseline.InternalLineArtifact" : "DatumBaseline.OrphanedTargetArtifact");
						}
						else
						{
							RecordStructureDatumCheck(plan, "Baseline|Axis=" + name + "|Decision=Kept|CandidateId=" + candidate.DiagnosticId
								+ "|Reason=" + (providers == null ? "UnmeasuredEndpoint" : providers.Count == 0 ? "CalloutOnlyInterval" : ineligible));
						}
						continue;
					}
					// Record the local complement in 'known' so further candidates can be covered by it.
					// This dimension is kept as-is (it bridges a measured position to an unmeasured target).
					if (first != null) { known.Add(Tuple.Create(coordinate(candidate.SecondPoint), first)); }
					if (second != null) { known.Add(Tuple.Create(coordinate(candidate.FirstPoint), second)); }
					RecordStructureDatumCheck(plan, "Baseline|Axis=" + name + "|Decision=Kept|CandidateId=" + candidate.DiagnosticId
						+ "|Reason=LocalComplementToUnmeasuredTarget");
					continue;
				}
				// Legacy face dimensions carry no source labels; both real endpoints are already measured by the providers.
				if (ineligible != null) { candidate.SourceGeometryIds = DimensionCandidateSemantics.MergeSourceGeometryIds(candidate.SourceGeometryIds, sources); }
				// Merge the replaced candidate's source IDs into the covering baseline dimension when they share the same interval.
				// This preserves traceability to the original geometry edge (e.g. 685B81 in SplitBoundary).
				foreach (var provider in providers)
				{
					bool sameInterval = provider.DebugRole == "DatumBaselineWidth" || provider.DebugRole == "DatumBaselineHeight";
					if (sameInterval && candidate.SourceGeometryIds != null && candidate.SourceGeometryIds.Count > 0)
					{
						double pMin = horizontal ? Math.Min(provider.FirstPoint.X, provider.SecondPoint.X) : Math.Min(provider.FirstPoint.Y, provider.SecondPoint.Y);
						double pMax = horizontal ? Math.Max(provider.FirstPoint.X, provider.SecondPoint.X) : Math.Max(provider.FirstPoint.Y, provider.SecondPoint.Y);
						double cMin = horizontal ? Math.Min(candidate.FirstPoint.X, candidate.SecondPoint.X) : Math.Min(candidate.FirstPoint.Y, candidate.SecondPoint.Y);
						double cMax = horizontal ? Math.Max(candidate.FirstPoint.X, candidate.SecondPoint.X) : Math.Max(candidate.FirstPoint.Y, candidate.SecondPoint.Y);
						if (Math.Abs(pMin - cMin) <= tol && Math.Abs(pMax - cMax) <= tol)
						{
							provider.SourceGeometryIds = DimensionCandidateSemantics.MergeSourceGeometryIds(provider.SourceGeometryIds, candidate.SourceGeometryIds);
						}
					}
				}
				bool chamferDerived = IsChamferDerivedAxisPosition(graph, outline, candidate.FirstPoint, horizontal)
					!= IsChamferDerivedAxisPosition(graph, outline, candidate.SecondPoint, horizontal);
				plan.TrySuppressCoveredStructure(candidate, chamferDerived ? "Structure.ChamferDerivedPositionCovered" : "DatumBaseline.CoveredByDatumBaseline",
					providers, sources, evidence + "|Relation=DatumBaselineMeasuredInterval" + (chamferDerived ? "|Target=ChamferDerivedAxisPosition" : "")
					+ "|ReplacedRule=" + candidate.RuleId, replaceable, allowOutlineSegment: true);
			}
		}
		return true;
	}

	private static double DistanceFromDimensionSide(Point2D point, DimensionSide side)
		=> side == DimensionSide.Bottom ? point.Y : side == DimensionSide.Top ? -point.Y : side == DimensionSide.Left ? point.X : -point.X;
}
