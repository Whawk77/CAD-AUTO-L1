using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadAuto.CadAdapter.Model;

public sealed class HoleFeature
{
	public Point3d Center { get; set; }

	public double Diameter { get; set; }

	public ObjectId SourceId { get; set; }

	public ObjectId CircleId { get; set; }

	public string FitTolerance { get; set; }

	public string ThreadCallout { get; set; }

	public HoleKind HoleKind { get; set; }

	public bool IsPinHole
	{
		get
		{
			return HoleKind == HoleKind.Pin;
		}
		set
		{
			HoleKind = (value ? HoleKind.Pin : HoleKind.Normal);
		}
	}

	public bool IsThreadHole => HoleKind == HoleKind.Thread;

	public bool IsSlotPoint => HoleKind == HoleKind.Slot;
}
