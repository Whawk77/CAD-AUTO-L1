using CadAuto.Core.Geometry;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public sealed class DimensionLayoutItem
{
	public Point2D FirstPoint { get; set; }

	public Point2D SecondPoint { get; set; }

	public string OverrideText { get; set; }

	public double Span { get; set; }

	public DimensionKind Kind { get; set; }

	public int LooseChainId { get; set; }

	public string AlignmentKey { get; set; }

	public int AlignmentPriority { get; set; }

	public bool PreserveAlignmentLevel { get; set; }

	public DimensionReadingLevel ReadingLevel { get; set; }

	public bool PreferLocalBoundary { get; set; }

	public bool ForceOuterLevel { get; set; }

	public bool PreferFeatureLocalPlacement { get; set; }

	public bool PreservePreferredSide { get; set; }
}
