using System.Collections.Generic;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;

namespace CadAuto.Core.Planning;

public sealed class HoleCalloutPlan
{
	public HoleCalloutKind Kind { get; set; }

	public Point2D AnchorPoint { get; set; }

	public string Text { get; set; }

	public string DebugOwner { get; set; }

	public List<HoleFeature2D> Holes { get; private set; }

	public HoleCalloutPlan()
	{
		Holes = new List<HoleFeature2D>();
	}
}
