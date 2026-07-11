using CadAuto.Core.Geometry;

namespace CadAuto.Core.Model;

public sealed class HoleFeature2D
{
	public Point2D Center { get; set; }

	public double Diameter { get; set; }

	public string SourceKey { get; set; }

	public string FitTolerance { get; set; }

	public string ThreadCallout { get; set; }

	public HoleKind2D Kind { get; set; }

	public bool IsPinHole => Kind == HoleKind2D.Pin;

	public bool IsThreadHole => Kind == HoleKind2D.Thread;

	public bool IsSlotPoint => Kind == HoleKind2D.Slot;
}
