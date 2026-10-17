using System;
using System.Collections.Generic;
using CadAuto.Core.Geometry;

namespace CadAuto.Core.Planning;

public static class CenterlineGripQuery
{
	public static bool TryPickCloserEndpoint(
		IEnumerable<Segment2D> centerlines,
		Point2D holeCenter,
		Point2D datumPoint,
		bool horizontalCenterline,
		double tolerance,
		out Point2D endpoint)
	{
		return TryPickCloserEndpoint(centerlines, holeCenter, datumPoint, horizontalCenterline, tolerance, 0.0, out endpoint);
	}

	public static bool TryPickCloserEndpoint(
		IEnumerable<Segment2D> centerlines,
		Point2D holeCenter,
		Point2D datumPoint,
		bool horizontalCenterline,
		double tolerance,
		double holeDiameter,
		out Point2D endpoint)
	{
		endpoint = default(Point2D);
		if (centerlines == null)
		{
			return false;
		}
		tolerance = Math.Max(tolerance, 1E-9);
		double hitTolerance = Math.Max(tolerance, Math.Max(0.05, holeDiameter * 0.15));
		Segment2D best = null;
		double bestLength = double.MaxValue;
		double bestDistance = double.MaxValue;
		foreach (Segment2D segment in centerlines)
		{
			if (segment == null || segment.IsArcChord)
			{
				continue;
			}
			if (horizontalCenterline)
			{
				if (!segment.IsHorizontal(hitTolerance))
				{
					continue;
				}
			}
			else if (!segment.IsVertical(hitTolerance))
			{
				continue;
			}
			double distance = DistanceToSegment(holeCenter, segment);
			if (distance > hitTolerance)
			{
				continue;
			}
			double length = segment.Length;
			if (best == null
				|| length < bestLength - tolerance
				|| (Math.Abs(length - bestLength) <= tolerance && distance < bestDistance))
			{
				best = segment;
				bestLength = length;
				bestDistance = distance;
			}
		}
		if (best == null)
		{
			return false;
		}
		endpoint = datumPoint.DistanceTo(best.Start) <= datumPoint.DistanceTo(best.End) ? best.Start : best.End;
		return true;
	}

	private static bool IsPointOnSegment(Point2D point, Segment2D segment, double tolerance)
	{
		double dx = segment.End.X - segment.Start.X;
		double dy = segment.End.Y - segment.Start.Y;
		double lengthSquared = dx * dx + dy * dy;
		if (lengthSquared <= 1E-24)
		{
			return point.DistanceTo(segment.Start) <= tolerance;
		}
		double ratio = ((point.X - segment.Start.X) * dx + (point.Y - segment.Start.Y) * dy) / lengthSquared;
		if (ratio < 0.0)
		{
			ratio = 0.0;
		}
		else if (ratio > 1.0)
		{
			ratio = 1.0;
		}
		Point2D projection = new Point2D(segment.Start.X + ratio * dx, segment.Start.Y + ratio * dy);
		return point.DistanceTo(projection) <= tolerance;
	}

	private static double DistanceToSegment(Point2D point, Segment2D segment)
	{
		double dx = segment.End.X - segment.Start.X;
		double dy = segment.End.Y - segment.Start.Y;
		double lengthSquared = dx * dx + dy * dy;
		if (lengthSquared <= 1E-24)
		{
			return point.DistanceTo(segment.Start);
		}
		double ratio = ((point.X - segment.Start.X) * dx + (point.Y - segment.Start.Y) * dy) / lengthSquared;
		if (ratio < 0.0)
		{
			ratio = 0.0;
		}
		else if (ratio > 1.0)
		{
			ratio = 1.0;
		}
		Point2D projection = new Point2D(segment.Start.X + ratio * dx, segment.Start.Y + ratio * dy);
		return point.DistanceTo(projection);
	}
}
