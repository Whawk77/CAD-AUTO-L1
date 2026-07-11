using CadAuto.Core.Geometry;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public sealed class DimensionTextPlacementItem
{
	public DimensionLayoutItem Dimension { get; set; }

	public DimensionSide Side { get; set; }

	public Point2D DimLinePoint { get; set; }

	public TextBounds2D TextBounds { get; set; }
}
