using CadAuto.Core.Geometry;

namespace CadAuto.Core.Planning;

public sealed class PlannedDimension
{
	public DimensionKind Kind { get; set; }

	public DimensionOrientation Orientation { get; set; }

	public DimensionSide Side { get; set; }

	public Point2D FirstPoint { get; set; }

	public Point2D SecondPoint { get; set; }

	public string OverrideText { get; set; }

	public string SourceKey { get; set; }

	public bool ForceOuterLevel { get; set; }

	public bool FirstPointMustLieOnOutline { get; set; }

	public double? RequiredOutlineReferenceCoordinate { get; set; }

	public bool UseSegmentedExtensionLines { get; set; }

	public bool PreferLocalBoundary { get; set; }

	public bool PreferFeatureLocalPlacement { get; set; }

	public int ChainId { get; set; }

	public string DebugOwner { get; set; }

	public string DebugRole { get; set; }
}
