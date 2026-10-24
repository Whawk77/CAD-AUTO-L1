using System;
using CadAuto.Core.Geometry;

namespace CadAuto.Core.Rules;

public static class FullCircleArcRules
{
	public static bool AreComplementarySemicircles(Arc2D first, Arc2D second, double tolerance)
	{
		if (first == null || second == null || double.IsNaN(tolerance) || double.IsInfinity(tolerance) || tolerance <= 0)
		{ return false; }
		// Equal centres alone also describe slots, duplicate arcs and open holes; require a closed circle.
		return first.Radius > tolerance && second.Radius > tolerance
			&& !double.IsInfinity(first.Radius) && !double.IsInfinity(second.Radius)
			&& first.Center.DistanceTo(second.Center) <= tolerance && Math.Abs(first.Radius - second.Radius) <= tolerance
			&& first.Start.DistanceTo(second.End) <= tolerance && first.End.DistanceTo(second.Start) <= tolerance
			&& Math.Abs(first.Start.DistanceTo(first.End) - 2 * first.Radius) <= tolerance
			&& Math.Abs(second.Start.DistanceTo(second.End) - 2 * second.Radius) <= tolerance
			&& Math.Abs(first.Start.DistanceTo(first.Center) - first.Radius) <= tolerance
			&& Math.Abs(first.End.DistanceTo(first.Center) - first.Radius) <= tolerance
			&& Math.Abs(second.Start.DistanceTo(second.Center) - second.Radius) <= tolerance
			&& Math.Abs(second.End.DistanceTo(second.Center) - second.Radius) <= tolerance
			&& Math.Abs(Math.Abs(4 * Math.Atan(first.Bulge)) - Math.PI) * first.Radius <= tolerance
			&& Math.Abs(Math.Abs(4 * Math.Atan(second.Bulge)) - Math.PI) * second.Radius <= tolerance
			&& first.Bulge * second.Bulge > 0;
	}
}
