using CadAuto.Core.Geometry;

namespace CadAuto.Core.Rules;

public sealed class StructureEndpointSpan
{
	public Point2D FirstPoint { get; set; }

	public Point2D SecondPoint { get; set; }

	public bool WasResolved { get; set; }
}
