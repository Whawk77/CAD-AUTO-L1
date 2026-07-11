using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadAuto.CadAdapter.Model;

public sealed class OutlineArc
{
	public Point2d Start { get; set; }

	public Point2d End { get; set; }

	public Point2d Center { get; set; }

	public double Radius { get; set; }

	public ObjectId SourceId { get; set; } = ObjectId.Null;

	public double Bulge { get; set; }
}
