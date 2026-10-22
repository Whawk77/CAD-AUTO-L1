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
	private void ApplyConfirmedStructureDatumDimensions(DimensionPlan plan, Datum2D datum)
	{
		double tol = _config.GeometryTolerance;
		var ownership = new StructureBoundaryEvidence(plan.Diagnostics.OutlineTopology, tol);
		if (!ownership.IsAvailable) { return; }
		var boundary = ownership.BoundaryOutline;
		foreach (bool horizontal in new[] { true, false })
		{
			bool minimum = ChooseStepDatumMinimum(plan, boundary, datum, horizontal, false,
				out var markers, out string reason, inspectionOnly: true);
			if (markers.Count == 0) { continue; }
			Func<Point2D, double> axis = p => horizontal ? p.X : p.Y;
			double low = horizontal ? boundary.MinX : boundary.MinY;
			double high = horizontal ? boundary.MaxX : boundary.MaxY;
			double reference = minimum ? low : high;
			var orientation = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
			string rule = horizontal ? StructureDatumRules.ConfirmedHorizontalDatumRule : StructureDatumRules.ConfirmedVerticalDatumRule;
			var supports = boundary.Segments.Where(e => !e.IsArcChord && (horizontal ? e.IsVertical(tol) : e.IsHorizontal(tol))
				&& Math.Abs(axis(e.Start) - reference) <= tol).ToList();
			if (supports.Count == 0) { continue; }
			var sources = supports.Select(e => e.SourceKey).Concat(markers.Select(m => m.SourceGeometryId)).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
			string group = "StructureDatum:" + (horizontal ? "X" : "Y") + ":" + reference.ToString("R", CultureInfo.InvariantCulture);
			string proof = "StructureDatum|Reason=" + reason + "|Reference=" + reference.ToString("R", CultureInfo.InvariantCulture)
				+ "|BoundaryProof=UniqueExteriorWalk|Blocks=" + string.Join(",", markers.Select(m => m.SourceGeometryId));
			foreach (var candidate in plan.Dimensions.Where(d => d.Orientation == orientation).ToList())
			{
				bool legacyArm = candidate.RuleId == "LProfile.HorizontalArmHeight" || candidate.RuleId == "LProfile.VerticalArmWidth";
				if (StructureCoveragePolicy.GetIneligibilityReason(candidate, legacyArm ? candidate.RuleId : null, allowOutlineSegment: true) != null
					|| (!legacyArm && plan.IsCoverageProvider(candidate))
					|| ownership.ValidateBoundaryAttachments(candidate.FirstPoint, candidate.SecondPoint) != true) { continue; }
				// Prefer live positions at the confirmed datum before creating another equivalent dimension.
				var coverage = FindBoundaryInformationProof(plan, boundary, candidate, includeOutlineSegments: true,
					providerFilter: d => d.Role == DimensionCandidateRole.Overall || (StructureDatumRules.IsCommonDatumRule(d.RuleId)
						&& Math.Abs(axis(d.FirstPoint) - reference) <= tol));
				if (coverage != null && coverage.Any(c => c.Provider != null && StructureDatumRules.IsCommonDatumRule(c.Provider.RuleId)))
				{
					plan.SetAttachmentValidity(candidate, true);
					plan.TrySuppressCoveredStructure(candidate, "StructureDatum.CoveredByConfirmedDatum",
						coverage.Where(c => c.Provider != null).Select(c => c.Provider), sources.Concat(coverage.SelectMany(c => c.Sources)),
						proof + "|Relation=ConnectedProjectedConstraintPath|ReplacedRule=" + candidate.RuleId, legacyArm ? candidate.RuleId : null,
						allowOutlineSegment: true);
					continue;
				}
				if (legacyArm) { continue; }
				var providers = new List<PlannedDimension>();
				var additions = new List<PlannedDimension>();
				bool complete = true;
				foreach (var target in new[] { candidate.FirstPoint, candidate.SecondPoint })
				{
					double coordinate = axis(target);
					if (Math.Abs(coordinate - reference) <= tol) { continue; }
					if (Math.Abs(coordinate - (minimum ? high : low)) <= tol)
					{
						var overall = plan.Dimensions.FirstOrDefault(d => d.Kind == (horizontal ? DimensionKind.OverallWidth : DimensionKind.OverallHeight));
						if (overall == null) { complete = false; break; }
						providers.Add(overall);
						continue;
					}
					// A matching number is insufficient: reuse only the same real target attachment and datum.
					var existing = plan.Dimensions.FirstOrDefault(d => d.RuleId == rule && d.DatumFamilyKey == group
						&& d.SecondPoint.DistanceTo(target) <= tol && d.AttachmentValidity == true);
					if (existing != null) { providers.Add(existing); continue; }
					PlannedDimension replacement = null;
					var opposite = horizontal ? candidate.Side == DimensionSide.Top ? DimensionSide.Bottom : DimensionSide.Top
						: candidate.Side == DimensionSide.Left ? DimensionSide.Right : DimensionSide.Left;
					foreach (var side in new[] { candidate.Side, opposite })
					{
						if (ExtensionCrossesOutline(target, orientation, side, boundary)) { continue; }
						var anchors = supports.SelectMany(e => new[] { e.Start, e.End })
							.Where(p => !ExtensionCrossesOutline(p, orientation, side, boundary))
							.OrderBy(p => horizontal ? side == DimensionSide.Top ? -p.Y : p.Y : side == DimensionSide.Right ? -p.X : p.X).ToList();
						if (anchors.Count == 0) { continue; }
						replacement = new PlannedDimension { Kind = DimensionKind.Normal, Role = DimensionCandidateRole.Structure,
							OwnerKind = DimensionCandidateOwnerKind.Outline, Orientation = orientation, Side = side,
							FirstPoint = anchors[0], SecondPoint = target, SourceKey = candidate.SourceKey,
							SourceGeometryIds = candidate.SourceGeometryIds.Concat(sources).Distinct().ToList(),
							RuleId = rule, DebugRole = horizontal ? "StructureDatumWidth" : "StructureDatumHeight",
							DatumFamilyKey = group, DebugOwner = group, TopologyEvidence = proof + "|Target=" + coordinate.ToString("R", CultureInfo.InvariantCulture),
							AttachmentKind = "ExteriorBoundary", AttachmentValidity = true, RequiresOuterBoundarySupport = true,
							FirstPointMustLieOnOutline = true, PreservePreferredSide = true, ReadingLevel = DimensionReadingLevel.LocalSpacing };
						break;
					}
					if (replacement == null) { complete = false; break; }
					additions.Add(replacement);
					providers.Add(replacement);
				}
				if (!complete)
				{
					RecordStructureDatumCheck(plan, "Axis=" + (horizontal ? "X" : "Y") + "|Placement=Fallback|CandidateId=" + candidate.DiagnosticId + "|Reason=NoClearRealAttachment");
					continue;
				}
				foreach (var addition in additions) { plan.Add(addition); plan.RegisterStepDatumRequirement(addition, tol); }
				plan.TrySuppressCoveredStructure(candidate, "StructureDatum.CoveredByConfirmedDatum", providers, sources,
					proof + "|Relation=CommonDatumMeasuredInterval", allowOutlineSegment: true);
			}
		}
	}
}
