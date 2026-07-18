namespace CadAuto.Core.Planning;

public sealed class DimensionCandidateDiagnostic
{
	public int Id { get; set; }

	public string Kind { get; set; }

	public string SourceFeatureId { get; set; }

	public double Value { get; set; }

	public double FirstPointX { get; set; }

	public double FirstPointY { get; set; }

	public double SecondPointX { get; set; }

	public double SecondPointY { get; set; }

	public double MeasurementMinimum { get; set; }

	public double MeasurementMaximum { get; set; }

	public string PlacementSide { get; set; }

	public int Priority { get; set; }

	public string ReadingLevel { get; set; }

	public bool IsSuppressed { get; set; }

	public bool IsSelected { get; set; }

	public bool IsAttachmentValid { get; set; }

	public string DecisionStatus { get; set; }

	public string DecisionReason { get; set; }

	public string SuppressedReason { get; set; }

	public string Orientation { get; set; }

	public string DebugRole { get; set; }

	public string OverrideText { get; set; }
}
