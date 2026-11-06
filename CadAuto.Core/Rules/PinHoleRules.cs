using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;

namespace CadAuto.Core.Rules;

public static class PinHoleRules
{
	public static Circle2D ResolveMarkedCircle(IList<Circle2D> circles, Circle2D marker, double tolerance)
	{
		if (marker == null || !(tolerance > 0) || double.IsInfinity(tolerance)
			|| !(marker.Radius >= 0) || double.IsInfinity(marker.Radius)
			|| double.IsNaN(marker.Center.X) || double.IsInfinity(marker.Center.X)
			|| double.IsNaN(marker.Center.Y) || double.IsInfinity(marker.Center.Y)) { return null; }
		int match = FindMatchingCircle(circles, marker, tolerance);
		// A measured pin marker proves the hole even when its circle is split or absent.
		return match >= 0 ? circles[match] : marker.Radius > tolerance ? marker : null;
	}

	public static int FindMatchingCircle(IList<Circle2D> circles, Circle2D marker, double tolerance)
	{
		if (circles == null || marker == null || !(tolerance > 0) || double.IsInfinity(tolerance)
			|| !(marker.Radius >= 0) || double.IsInfinity(marker.Radius)) { return -1; }
		bool hasRadius = marker.Radius > tolerance;
		var matches = circles.Select((circle, index) => new { Circle = circle, Index = index,
			Distance = circle == null ? double.PositiveInfinity : circle.Center.DistanceTo(marker.Center) })
			.Where(m => m.Circle != null && m.Circle.Radius > tolerance && !double.IsInfinity(m.Circle.Radius)
				&& m.Distance <= (hasRadius ? tolerance : Math.Max(m.Circle.Radius + tolerance, 1.0))
				&& (!hasRadius || Math.Abs(m.Circle.Radius - marker.Radius) <= tolerance)).ToList();
		if (matches.Count == 0) { return -1; }
		double nearestDistance = matches.Min(m => m.Distance);
		var nearest = matches.Where(m => m.Distance <= nearestDistance + tolerance).ToList();
		// A marker without size evidence cannot distinguish concentric circles or duplicate sources.
		return nearest.Count == 1 ? nearest[0].Index : -1;
	}
}
