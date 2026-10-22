using System;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning;

public sealed partial class DimensionPlanner
{
	private void EnsureNotchOpeningLocations(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum)
	{
		var ownership = new StructureBoundaryEvidence(plan.Diagnostics.OutlineTopology, _config.GeometryTolerance);
		if (!ownership.IsAvailable || plan.AnnotationCaseSnapshot == null) { return; }
		var boundary = ownership.BoundaryOutline;
		double tol = _config.GeometryTolerance;
		foreach (var notch in plan.AnnotationCaseSnapshot.Features.Where(f => (f.Kind == StructureFeatureKind.NotchOpening
			|| (f.BoundaryEvidence ?? "").Contains("|Relation=RecoveredGroove|"))
			&& f.Keep && f.RequiresOuterBoundarySupport && f.BoundaryMembership == "Boundary" && f.BoundaryAttachmentValidity == true))
		{
			bool horizontal = notch.Axis == StructureFeatureAxis.Horizontal;
			var orientation = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
			Func<Point2D, double> axis = p => horizontal ? p.X : p.Y;
			var width = plan.Dimensions.FirstOrDefault(d => d.Kind == DimensionKind.Normal && d.Role == DimensionCandidateRole.Structure
				&& d.Orientation == orientation && d.SourceGeometryIds.Intersect(notch.SupportSourceKeys).Any()
				&& Math.Abs(Math.Min(axis(d.FirstPoint), axis(d.SecondPoint)) - Math.Min(notch.T0, notch.T1)) <= tol
				&& Math.Abs(Math.Max(axis(d.FirstPoint), axis(d.SecondPoint)) - Math.Max(notch.T0, notch.T1)) <= tol);
			if (ownership.ValidateBoundaryAttachments(notch.FirstPoint, notch.SecondPoint) != true) { continue; }
			if (width == null)
			{
				width = new StructurePlacementAdapter(_config).CreateStructureCandidate(notch);
				if (FindBoundaryInformationProof(plan, outline, width, includeOutlineSegments: true) == null) { continue; }
			}
			bool minimum = ChooseStepDatumMinimum(plan, boundary, datum, horizontal,
				PreferMinimumStepDatum(boundary, new[] { notch.T0, notch.T1 }, horizontal, width.Side), out var markers, out string reason);
			double reference = horizontal ? minimum ? boundary.MinX : boundary.MaxX : minimum ? boundary.MinY : boundary.MaxY;
			var supports = boundary.Segments.Where(e => !e.IsArcChord && (horizontal ? e.IsVertical(tol) : e.IsHorizontal(tol))
				&& Math.Abs(axis(e.Start) - reference) <= tol).ToList();
			var target = new[] { width.FirstPoint, width.SecondPoint }.OrderBy(p => Math.Abs(axis(p) - reference)).First();
			var node = plan.Diagnostics.OutlineTopology.Nodes.FirstOrDefault(n => n.IsBoundary && n.Point.DistanceTo(target) <= tol);
			if (TryChooseClearBoundaryEndpoint(plan.Diagnostics.OutlineTopology, node?.Id ?? -1, 2, target, orientation, width.Side,
				boundary, out var clearTarget, out var attachmentEvidence)) { target = clearTarget; }
			var anchors = supports.SelectMany(e => new[] { e.Start, e.End })
				.Where(p => !ExtensionCrossesOutline(p, orientation, width.Side, boundary))
				.OrderBy(p => horizontal ? width.Side == DimensionSide.Top ? -p.Y : p.Y : width.Side == DimensionSide.Right ? -p.X : p.X).ToList();
			if (anchors.Count == 0 || Math.Abs(axis(target) - reference) <= tol || ExtensionCrossesOutline(target, orientation, width.Side, boundary))
			{
				RecordStructureDatumCheck(plan, "Axis=" + (horizontal ? "X" : "Y") + "|Placement=Pending|Source=" + notch.SourceKey
					+ "|Reason=NoClearRealNotchAttachment");
				continue;
			}
			string group = "StructureDatum:" + (horizontal ? "X:" : "Y:") + reference.ToString("R", CultureInfo.InvariantCulture);
			var targetSources = plan.Diagnostics.OutlineTopology.Edges.Where(e => e.IsBoundary
				&& (e.Start.DistanceTo(target) <= tol || e.End.DistanceTo(target) <= tol)).Select(e => e.SourceKey);
			var location = new PlannedDimension { Kind = DimensionKind.Normal, Role = DimensionCandidateRole.Structure,
				OwnerKind = DimensionCandidateOwnerKind.Outline, Orientation = orientation, Side = width.Side,
				FirstPoint = anchors[0], SecondPoint = target, SourceKey = "NotchLocation:" + notch.SourceKey,
				SourceGeometryIds = notch.SupportSourceKeys.Concat(targetSources).Concat(supports.Select(e => e.SourceKey)).Concat(markers.Select(m => m.SourceGeometryId))
					.Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList(),
				RuleId = StructureDatumRules.NotchLocationRule, DebugRole = horizontal ? "StructureDatumWidth" : "StructureDatumHeight",
				DatumFamilyKey = group, DebugOwner = group, TopologyEvidence = "StructureDatum|Relation=NotchLocation|Reason=" + reason
					+ "|BoundaryProof=UniqueExteriorWalk|RealEndpoints=True|WidthCandidateId=" + width.DiagnosticId
					+ (attachmentEvidence != null ? "|" + attachmentEvidence : ""),
				AttachmentKind = "ExteriorBoundary", AttachmentValidity = true, RequiresOuterBoundarySupport = true,
				FirstPointMustLieOnOutline = true, PreservePreferredSide = true, ReadingLevel = DimensionReadingLevel.LocalSpacing };
			if (!plan.BoundaryInformationRequirements.Any(d => d.SourceKey == location.SourceKey && d.DatumFamilyKey == group && d.Orientation == orientation))
			{
				plan.BoundaryInformationRequirements.Add(location);
			}
			// Width fixes the opening size; only a real connected constraint path can also fix its location.
			if (FindBoundaryInformationProof(plan, outline, location, includeOutlineSegments: true) != null) { continue; }
			plan.Add(location);
			plan.RegisterStepDatumRequirement(location, tol);
		}
	}
}
