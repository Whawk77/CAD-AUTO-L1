using Autodesk.AutoCAD.Geometry;

namespace CadAuto.CadAdapter.Model;

public sealed class ChamferFeature
{
	public Point2d StartPoint { get; set; }

	public Point2d EndPoint { get; set; }

	public double Length { get; set; }

	public double DeltaX { get; set; }

	public double DeltaY { get; set; }

	public double Value { get; set; }

	public string Text { get; set; }

	public OutlineSegment SourceSegment { get; set; }

	public double Confidence { get; set; }
}
