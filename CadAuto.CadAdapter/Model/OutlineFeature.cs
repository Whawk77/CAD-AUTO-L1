using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;

namespace CadAuto.CadAdapter.Model;

public sealed class OutlineFeature
{
	public double MinX { get; set; }

	public double MaxX { get; set; }

	public double MinY { get; set; }

	public double MaxY { get; set; }

	public List<Point2d> Vertices { get; } = new List<Point2d>();

	public List<OutlineSegment> Segments { get; } = new List<OutlineSegment>();

	public List<OutlineArc> Arcs { get; } = new List<OutlineArc>();

	public List<ChamferFeature> Chamfers { get; } = new List<ChamferFeature>();

	public List<FilletFeature> Fillets { get; } = new List<FilletFeature>();

	public double Width => MaxX - MinX;

	public double Height => MaxY - MinY;
}
