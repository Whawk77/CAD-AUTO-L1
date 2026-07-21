using CadAuto.Core.Geometry;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public sealed class DimensionDeduplicationItem
{
	public Point2D FirstPoint { get; set; }

	public Point2D SecondPoint { get; set; }

	public string OverrideText { get; set; }

	public double Span { get; set; }

	public DimensionKind Kind { get; set; }

	public bool ForceOuterLevel { get; set; }

	public string DebugRole { get; set; }
}
