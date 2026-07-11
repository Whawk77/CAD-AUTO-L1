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
}
