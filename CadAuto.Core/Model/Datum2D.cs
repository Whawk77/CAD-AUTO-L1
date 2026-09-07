using System.Collections.Generic;
using CadAuto.Core.Geometry;

namespace CadAuto.Core.Model;

public sealed class CenterlineEndpoint2D
{
	public Point2D Point { get; set; }

	public string SourceGeometryId { get; set; }
}

public sealed class Datum2D
{
	public double BaseX { get; set; }

	public double BaseY { get; set; }

	public HoleFeature2D DatumHole { get; set; }

	public double? DatumHoleLocationBaseX { get; set; }

	public double? DatumHoleLocationBaseY { get; set; }

	public bool DatumHoleLocationUseToleranceX { get; set; }

	public bool DatumHoleLocationUseToleranceY { get; set; }

	public List<CenterlineEndpoint2D> HoleCenterlineEndpoints { get; private set; } = new List<CenterlineEndpoint2D>();

	public static Datum2D FromOutline(OutlineFeature2D outline)
	{
		return new Datum2D
		{
			BaseX = outline.MinX,
			BaseY = outline.MinY
		};
	}
}
