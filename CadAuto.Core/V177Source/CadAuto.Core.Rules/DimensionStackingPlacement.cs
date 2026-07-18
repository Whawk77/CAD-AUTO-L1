namespace CadAuto.Core.Rules;

public sealed class DimensionStackingPlacement
{
	public int Index { get; set; }

	public int Level { get; set; }

	public double Offset { get; set; }

	public double? DimLineCoordinateOverride { get; set; }

	public string AlignmentLaneKey { get; set; }

	public int AlignmentLaneMemberCount { get; set; }
}
