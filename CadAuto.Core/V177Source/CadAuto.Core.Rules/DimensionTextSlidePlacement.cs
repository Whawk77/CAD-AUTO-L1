using CadAuto.Core.Geometry;

namespace CadAuto.Core.Rules;

public sealed class DimensionTextSlidePlacement
{
	public int Index { get; set; }

	public Point2D TextPosition { get; set; }

	public TextBounds2D TextBounds { get; set; }
}
