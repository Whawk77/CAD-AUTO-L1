using System;
using System.Collections.Generic;
using System.Linq;

namespace CadAuto.Core.Planning;

public sealed class DimensionDiagnosticReport
{
	public FeatureDiagnosticCounts Features { get; private set; }

	public OutlineTopologyDiagnostic OutlineTopology { get; set; }

	public string OutlineTopologySvgPath { get; set; }

	public List<string> StructureDatumChecks { get; } = new List<string>();

	// Scoped to real axis edges, recognized body-slope projections and chamfer spans.
	public string OutlineInformationStatus { get; set; } = "NotEvaluated";

	public List<string> OutlineInformationDecisions { get; } = new List<string>();

	public List<DimensionCandidateDiagnostic> DimensionCandidates { get; private set; }

	public List<DimensionCandidateDiagnostic> FinalDimensions { get; private set; }

	/// <summary>Non-fatal problems recorded during planning or validation.</summary>
	public List<string> Warnings { get; private set; }

	/// <summary>Scoped dependency check, not a claim that all required dimensions are present.</summary>
	public string StructureCoverageStatus => Warnings.Any(w =>
		w.StartsWith("StructureCoverage.Incomplete|", StringComparison.Ordinal)
		|| w.StartsWith("StructureCoverage.PendingConfirmation|", StringComparison.Ordinal)
		|| w.StartsWith("StructureBoundary.PendingConfirmation|", StringComparison.Ordinal)
		|| w.StartsWith("StepDatum.PendingHumanConfirmation|", StringComparison.Ordinal)
		|| w.StartsWith("LProfile.StepCoverage.Topology|", StringComparison.Ordinal)
		|| w.Contains("LProfile.StepCoverage.NotApplied|"))
		? "PendingConfirmation" : "NoDetectedDependencyGap";

	public DimensionDiagnosticReport()
	{
		Features = new FeatureDiagnosticCounts();
		DimensionCandidates = new List<DimensionCandidateDiagnostic>();
		FinalDimensions = new List<DimensionCandidateDiagnostic>();
		Warnings = new List<string>();
	}

	public void RecordLayoutGeometry(int diagnosticId, string compactionDecision, string extensionLineDecision)
	{
		foreach (var diagnostic in DimensionCandidates.Concat(FinalDimensions).Where(d => d.Id == diagnosticId))
		{
			diagnostic.CompactionDecision = compactionDecision ?? "NotRequested";
			diagnostic.ExtensionLineDecision = extensionLineDecision ?? "Unknown";
		}
	}

	// This check covers family-side policy and nested-stack spacing, not full CAD collision correctness.
	public void ValidateDatumFamilyLayout(double minimumSpacing, double tolerance)
	{
		foreach (var family in FinalDimensions.Where(d => d.IsSelected && !string.IsNullOrEmpty(d.DatumFamilyKey))
			.GroupBy(d => new { d.DatumFamilyKey, d.Orientation }))
		{
			var members = family.ToList();
			bool complete = minimumSpacing > tolerance && !double.IsNaN(minimumSpacing) && !double.IsInfinity(minimumSpacing)
				&& members.All(d => d.HasFinalPlacement && d.ResolvedDimLineCoordinate.HasValue
					&& !double.IsNaN(d.ResolvedDimLineCoordinate.Value) && !double.IsInfinity(d.ResolvedDimLineCoordinate.Value));
			string sideDecision = !complete ? "PendingPlacement" : members.Select(d => d.PlacementSide).Distinct().Count() > 1
				? "SplitAcrossSides_GroupDatumPolicyViolation" : "SameSide_GroupDatumPolicy";
			bool? spacingValid = null;
			if (complete)
			{
				for (int i = 0; i < members.Count; i++)
				{
					for (int j = i + 1; j < members.Count; j++)
					{
						var a = members[i]; var b = members[j];
						if (a.PlacementSide != b.PlacementSide || Math.Min(a.MeasurementMaximum, b.MeasurementMaximum)
							- Math.Max(a.MeasurementMinimum, b.MeasurementMinimum) <= tolerance) { continue; }
						bool valid = Math.Abs(a.ResolvedDimLineCoordinate.Value - b.ResolvedDimLineCoordinate.Value) + tolerance >= minimumSpacing;
						spacingValid = (spacingValid ?? true) && valid;
					}
				}
			}
			var ids = new HashSet<int>(members.Select(d => d.Id));
			foreach (var diagnostic in DimensionCandidates.Concat(FinalDimensions).Where(d => ids.Contains(d.Id)))
			{
				diagnostic.DatumFamilySideDecision = sideDecision;
				diagnostic.FamilySpacingValid = spacingValid;
			}
		}
	}

	public void RecordRenderSuppressed(int diagnosticId, string reason)
	{
		foreach (DimensionCandidateDiagnostic diagnostic in DimensionCandidates)
		{
			if (diagnostic.Id == diagnosticId)
			{
				diagnostic.IsSelected = false;
				diagnostic.IsSuppressed = true;
				diagnostic.SuppressedReason = reason ?? string.Empty;
				diagnostic.DecisionStatus = "RenderSuppressed";
				diagnostic.DecisionReason = reason ?? string.Empty;
				diagnostic.Decision = DimensionCandidateDecision.Suppressed;
			}
		}
		FinalDimensions.RemoveAll(diagnostic => diagnostic.Id == diagnosticId);
	}

	public void RecordFinalPlacement(int diagnosticId, string placementSide, int stackingLevel, double stackingOffset, double resolvedDimLineCoordinate, bool usesLocalBoundary, bool hasAlignmentCoordinateOverride, string alignmentLaneKey, int alignmentLaneMemberCount, string alignmentDecision, string layoutBlockId, string layoutBlockType, double effectiveSpan, int effectiveOrder, string orderingReason, string promotedByConflictWith, double physicalOutwardDistance, bool physicalOrderValidated)
	{
		RecordFinalPlacement(DimensionCandidates, diagnosticId, placementSide, stackingLevel, stackingOffset, resolvedDimLineCoordinate, usesLocalBoundary, hasAlignmentCoordinateOverride, alignmentLaneKey, alignmentLaneMemberCount, alignmentDecision, layoutBlockId, layoutBlockType, effectiveSpan, effectiveOrder, orderingReason, promotedByConflictWith, physicalOutwardDistance, physicalOrderValidated);
		RecordFinalPlacement(FinalDimensions, diagnosticId, placementSide, stackingLevel, stackingOffset, resolvedDimLineCoordinate, usesLocalBoundary, hasAlignmentCoordinateOverride, alignmentLaneKey, alignmentLaneMemberCount, alignmentDecision, layoutBlockId, layoutBlockType, effectiveSpan, effectiveOrder, orderingReason, promotedByConflictWith, physicalOutwardDistance, physicalOrderValidated);
	}

	private static void RecordFinalPlacement(IEnumerable<DimensionCandidateDiagnostic> diagnostics, int diagnosticId, string placementSide, int stackingLevel, double stackingOffset, double resolvedDimLineCoordinate, bool usesLocalBoundary, bool hasAlignmentCoordinateOverride, string alignmentLaneKey, int alignmentLaneMemberCount, string alignmentDecision, string layoutBlockId, string layoutBlockType, double effectiveSpan, int effectiveOrder, string orderingReason, string promotedByConflictWith, double physicalOutwardDistance, bool physicalOrderValidated)
	{
		foreach (DimensionCandidateDiagnostic diagnostic in diagnostics)
		{
			if (diagnostic.Id != diagnosticId)
			{
				continue;
			}
			diagnostic.HasFinalPlacement = true;
			if (!string.IsNullOrEmpty(placementSide))
			{
				diagnostic.PlacementSide = placementSide;
			}
			diagnostic.StackingLevel = stackingLevel;
			diagnostic.StackingOffset = stackingOffset;
			diagnostic.ResolvedDimLineCoordinate = resolvedDimLineCoordinate;
			diagnostic.UsesLocalBoundary = usesLocalBoundary;
			diagnostic.HasAlignmentCoordinateOverride = hasAlignmentCoordinateOverride;
			diagnostic.AlignmentLaneKey = alignmentLaneKey ?? string.Empty;
			diagnostic.AlignmentLaneMemberCount = alignmentLaneMemberCount;
			diagnostic.AlignmentDecision = alignmentLaneMemberCount < 2 && !string.IsNullOrEmpty(alignmentDecision)
				&& alignmentDecision.StartsWith("Aligned", StringComparison.Ordinal)
				? "SingleMemberNoAlignmentProof" : alignmentDecision ?? string.Empty;
			diagnostic.LayoutBlockId = layoutBlockId ?? string.Empty;
			diagnostic.LayoutBlockType = layoutBlockType ?? string.Empty;
			diagnostic.EffectiveSpan = effectiveSpan;
			diagnostic.EffectiveOrder = effectiveOrder;
			diagnostic.OrderingReason = orderingReason ?? string.Empty;
			diagnostic.PromotedByConflictWith = promotedByConflictWith ?? string.Empty;
			diagnostic.PhysicalOutwardDistance = physicalOutwardDistance;
			diagnostic.PhysicalOrderValidated = physicalOrderValidated;
		}
	}
}
