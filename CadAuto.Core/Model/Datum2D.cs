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
	public List<StructureDatumMarker2D> StructureDatumMarkers { get; } = new List<StructureDatumMarker2D>();
	// Per-run conflict choices: true = minimum (left/bottom), false = maximum (right/top).
	public bool? StructureDatumMinimumX { get; set; }
	public bool? StructureDatumMinimumY { get; set; }

	public double BaseX { get; set; }

	public double BaseY { get; set; }

	public HoleFeature2D DatumHole { get; set; }

	public PinGroupVerticalLayoutPreference PinGroupVerticalLayout { get; set; }

	public PinGroupHorizontalLayoutPreference PinGroupHorizontalLayout { get; set; }

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

public sealed class StructureDatumMarker2D
{
	public Point2D ContactPoint { get; set; }
	public string BlockName { get; set; }
	public string SourceGeometryId { get; set; }
	public string ContactKind { get; set; }
	public string ValidationFailure { get; set; }
}
