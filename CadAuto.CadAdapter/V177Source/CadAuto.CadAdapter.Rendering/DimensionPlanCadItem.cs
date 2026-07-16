using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;
using CadAuto.Core.Planning;

namespace CadAuto.CadAdapter.Rendering;

internal sealed class DimensionPlanCadItem
{
	public double Rotation { get; set; }

	public Point3d FirstPoint { get; set; }

	public Point3d SecondPoint { get; set; }

	public string OverrideText { get; set; }

	public double Span { get; set; }

	public DimensionType DimensionType { get; set; }

	public bool UseSegmentedExtensionLines { get; set; }

	public bool ForceOuterLevel { get; set; }

	public int ChainId { get; set; }

	public string AlignmentKey { get; set; }

	public int AlignmentPriority { get; set; }

	public bool PreferLocalBoundary { get; set; }

	public bool PreferFeatureLocalPlacement { get; set; }

	public bool PreservePreferredSide { get; set; }

	public string DebugOwner { get; set; }

	public string DebugRole { get; set; }

	public DimensionSide Side { get; set; }
}
