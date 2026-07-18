using System.Collections.Generic;

namespace CadAuto.Core.Planning;

public sealed class DimensionDiagnosticReport
{
	public FeatureDiagnosticCounts Features { get; private set; }

	public List<DimensionCandidateDiagnostic> DimensionCandidates { get; private set; }

	public List<DimensionCandidateDiagnostic> FinalDimensions { get; private set; }

	public DimensionDiagnosticReport()
	{
		Features = new FeatureDiagnosticCounts();
		DimensionCandidates = new List<DimensionCandidateDiagnostic>();
		FinalDimensions = new List<DimensionCandidateDiagnostic>();
	}

	public void RecordFinalPlacement(int diagnosticId, int stackingLevel, double stackingOffset, double resolvedDimLineCoordinate, bool usesLocalBoundary, bool hasAlignmentCoordinateOverride, string alignmentLaneKey, int alignmentLaneMemberCount, string alignmentDecision, string layoutBlockId, string layoutBlockType, double effectiveSpan, int effectiveOrder, string orderingReason, string promotedByConflictWith, double physicalOutwardDistance, bool physicalOrderValidated)
	{
		RecordFinalPlacement(DimensionCandidates, diagnosticId, stackingLevel, stackingOffset, resolvedDimLineCoordinate, usesLocalBoundary, hasAlignmentCoordinateOverride, alignmentLaneKey, alignmentLaneMemberCount, alignmentDecision, layoutBlockId, layoutBlockType, effectiveSpan, effectiveOrder, orderingReason, promotedByConflictWith, physicalOutwardDistance, physicalOrderValidated);
		RecordFinalPlacement(FinalDimensions, diagnosticId, stackingLevel, stackingOffset, resolvedDimLineCoordinate, usesLocalBoundary, hasAlignmentCoordinateOverride, alignmentLaneKey, alignmentLaneMemberCount, alignmentDecision, layoutBlockId, layoutBlockType, effectiveSpan, effectiveOrder, orderingReason, promotedByConflictWith, physicalOutwardDistance, physicalOrderValidated);
	}

	private static void RecordFinalPlacement(IEnumerable<DimensionCandidateDiagnostic> diagnostics, int diagnosticId, int stackingLevel, double stackingOffset, double resolvedDimLineCoordinate, bool usesLocalBoundary, bool hasAlignmentCoordinateOverride, string alignmentLaneKey, int alignmentLaneMemberCount, string alignmentDecision, string layoutBlockId, string layoutBlockType, double effectiveSpan, int effectiveOrder, string orderingReason, string promotedByConflictWith, double physicalOutwardDistance, bool physicalOrderValidated)
	{
		foreach (DimensionCandidateDiagnostic diagnostic in diagnostics)
		{
			if (diagnostic.Id != diagnosticId)
			{
				continue;
			}
			diagnostic.HasFinalPlacement = true;
			diagnostic.StackingLevel = stackingLevel;
			diagnostic.StackingOffset = stackingOffset;
			diagnostic.ResolvedDimLineCoordinate = resolvedDimLineCoordinate;
			diagnostic.UsesLocalBoundary = usesLocalBoundary;
			diagnostic.HasAlignmentCoordinateOverride = hasAlignmentCoordinateOverride;
			diagnostic.AlignmentLaneKey = alignmentLaneKey ?? string.Empty;
			diagnostic.AlignmentLaneMemberCount = alignmentLaneMemberCount;
			diagnostic.AlignmentDecision = alignmentDecision ?? string.Empty;
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
