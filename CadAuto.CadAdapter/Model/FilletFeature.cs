using Autodesk.AutoCAD.Geometry;

namespace CadAuto.CadAdapter.Model;

public sealed class FilletFeature
{
	public Point2d Center { get; set; }

	public double Radius { get; set; }

	public Point2d StartPoint { get; set; }

	public Point2d EndPoint { get; set; }

	public string Text { get; set; }

	public OutlineArc SourceArc { get; set; }

	public double Confidence { get; set; }
}
