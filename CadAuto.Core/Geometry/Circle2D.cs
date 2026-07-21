namespace CadAuto.Core.Geometry;

public sealed class Circle2D
{
	public Point2D Center { get; set; }

	public double Radius { get; set; }

	public string SourceKey { get; set; }

	public double Diameter => Radius * 2.0;
}
