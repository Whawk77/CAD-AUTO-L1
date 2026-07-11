namespace CadAuto.Core.Planning;

public sealed class DimensionCandidateDiagnostic
{
	public int Id { get; set; }

	public string Kind { get; set; }

	public string SourceFeatureId { get; set; }

	public double Value { get; set; }

	public string PlacementSide { get; set; }

	public int Priority { get; set; }

	public bool IsSuppressed { get; set; }

	public string SuppressedReason { get; set; }

	public string Orientation { get; set; }

	public string DebugRole { get; set; }

	public string OverrideText { get; set; }
}
