using CadAuto.Core.Geometry;

namespace CadAuto.Core.Model;

public sealed class ChamferFeature2D
{
	public Point2D StartPoint { get; set; }

	public Point2D EndPoint { get; set; }

	public double Length { get; set; }

	public double DeltaX { get; set; }

	public double DeltaY { get; set; }

	public double Value { get; set; }

	public string Text { get; set; }

	public Segment2D SourceSegment { get; set; }

	public double Confidence { get; set; }
}
