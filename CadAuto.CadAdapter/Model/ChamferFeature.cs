using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
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

	public List<ObjectId> SourceIds { get; set; } = new List<ObjectId>();

	public double Confidence { get; set; }
}
