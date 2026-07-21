namespace CadAuto.Core.Rules;

public sealed class DimensionStackingPlacement
{
	public int Index { get; set; }

	public int Level { get; set; }

	public double Offset { get; set; }

	public double? DimLineCoordinateOverride { get; set; }

	public string AlignmentLaneKey { get; set; }

	public int AlignmentLaneMemberCount { get; set; }

	public string LayoutBlockId { get; set; }

	public string LayoutBlockType { get; set; }

	public double EffectiveSpan { get; set; }

	public int EffectiveOrder { get; set; }

	public string OrderingReason { get; set; }

	public string PromotedByConflictWith { get; set; }

	public double PhysicalOutwardDistance { get; set; }

	public bool PhysicalOrderValidated { get; set; }
}
