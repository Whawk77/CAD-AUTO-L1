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

	public void RecordFinalPlacement(int diagnosticId, int stackingLevel, double stackingOffset, double resolvedDimLineCoordinate, bool usesLocalBoundary, bool hasAlignmentCoordinateOverride, string alignmentLaneKey, int alignmentLaneMemberCount, string alignmentDecision)
	{
		RecordFinalPlacement(DimensionCandidates, diagnosticId, stackingLevel, stackingOffset, resolvedDimLineCoordinate, usesLocalBoundary, hasAlignmentCoordinateOverride, alignmentLaneKey, alignmentLaneMemberCount, alignmentDecision);
		RecordFinalPlacement(FinalDimensions, diagnosticId, stackingLevel, stackingOffset, resolvedDimLineCoordinate, usesLocalBoundary, hasAlignmentCoordinateOverride, alignmentLaneKey, alignmentLaneMemberCount, alignmentDecision);
	}

	private static void RecordFinalPlacement(IEnumerable<DimensionCandidateDiagnostic> diagnostics, int diagnosticId, int stackingLevel, double stackingOffset, double resolvedDimLineCoordinate, bool usesLocalBoundary, bool hasAlignmentCoordinateOverride, string alignmentLaneKey, int alignmentLaneMemberCount, string alignmentDecision)
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
		}
	}
}
