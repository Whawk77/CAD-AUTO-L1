using System.Collections.Generic;
using CadAuto.Core.Model;

namespace CadAuto.Core.Planning;

public sealed class PinGroupPlan
{
	public int GroupIndex { get; set; }

	public HoleFeature2D BasePin { get; set; }

	public List<HoleFeature2D> Pins { get; private set; }

	public List<HoleFeature2D> MemberHoles { get; private set; }

	public DimensionSide HorizontalSide { get; set; }

	public DimensionSide VerticalSide { get; set; }

	public PinGroupPlan()
	{
		Pins = new List<HoleFeature2D>();
		MemberHoles = new List<HoleFeature2D>();
		HorizontalSide = DimensionSide.Bottom;
		VerticalSide = DimensionSide.Left;
	}
}
