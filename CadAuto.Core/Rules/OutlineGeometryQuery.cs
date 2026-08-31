using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;

namespace CadAuto.Core.Rules;

public sealed class OutlineEnvelope2D
{
	public double MinX { get; set; }

	public double MaxX { get; set; }

	public double MinY { get; set; }

	public double MaxY { get; set; }

	public Point2D LeftGrip { get; set; }

	public Point2D RightGrip { get; set; }

	public Point2D BottomGrip { get; set; }

	public Point2D TopGrip { get; set; }
}

public static class OutlineGeometryQuery
{
	private const double NumericEpsilon = 1E-12;

	private static readonly double[] CardinalAngles = new double[4]
	{
		0.0,
		Math.PI / 2.0,
		Math.PI,
		Math.PI * 1.5
	};

	public static bool TryGetEnvelope(OutlineFeature2D outline, double tolerance, out OutlineEnvelope2D envelope)
	{
		envelope = null;
		if (outline == null)
		{
			return false;
		}
		tolerance = NormalizeTolerance(tolerance);
		List<Point2D> list = GetEnvelopeCandidates(outline, tolerance);
		if (list.Count == 0)
		{
			return false;
		}
		double minX = list.Min((Point2D point) => point.X);
		double maxX = list.Max((Point2D point) => point.X);
		double minY = list.Min((Point2D point) => point.Y);
		double maxY = list.Max((Point2D point) => point.Y);
		envelope = new OutlineEnvelope2D
		{
			MinX = minX,
			MaxX = maxX,
			MinY = minY,
			MaxY = maxY,
			LeftGrip = list.Where((Point2D point) => Math.Abs(point.X - minX) <= tolerance).OrderBy((Point2D point) => point.Y).ThenBy((Point2D point) => point.X).First(),
			RightGrip = list.Where((Point2D point) => Math.Abs(point.X - maxX) <= tolerance).OrderBy((Point2D point) => point.Y).ThenBy((Point2D point) => point.X).First(),
			BottomGrip = list.Where((Point2D point) => Math.Abs(point.Y - minY) <= tolerance).OrderBy((Point2D point) => point.X).ThenBy((Point2D point) => point.Y).First(),
			TopGrip = list.Where((Point2D point) => Math.Abs(point.Y - maxY) <= tolerance).OrderBy((Point2D point) => point.X).ThenBy((Point2D point) => point.Y).First()
		};
		return true;
	}

	public static bool IsPointOnBoundary(Point2D point, OutlineFeature2D outline, double tolerance)
	{
		if (outline == null || !IsFinite(point))
		{
			return false;
		}
		tolerance = NormalizeTolerance(tolerance);
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment != null && !segment.IsArcChord && IsPointOnSegment(point, segment, tolerance))
			{
				return true;
			}
		}
		foreach (Arc2D arc in outline.Arcs)
		{
			if (IsPointOnArc(point, arc, tolerance))
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsAxisSegmentBetweenTwoChamfers(OutlineFeature2D outline, Segment2D candidate, double tolerance)
	{
		if (outline == null || candidate == null || candidate.IsArcChord || !IsFinite(candidate.Start) || !IsFinite(candidate.End))
		{
			return false;
		}
		tolerance = Math.Max(NormalizeTolerance(tolerance), NumericEpsilon);
		if (candidate.Length <= tolerance || (!candidate.IsHorizontal(tolerance) && !candidate.IsVertical(tolerance)))
		{
			return false;
		}
		Segment2D segment = outline.Segments.FirstOrDefault((Segment2D item) => item != null
			&& !item.IsArcChord
			&& (item.IsHorizontal(tolerance) || item.IsVertical(tolerance))
			&& SegmentsEqual(item, candidate, tolerance));
		if (segment == null)
		{
			return false;
		}
		return outline.Chamfers.Any((ChamferFeature2D first) => ChamferTouches(first, segment.Start, tolerance)
			&& outline.Chamfers.Any((ChamferFeature2D second) => !ReferenceEquals(first, second)
				&& ChamferTouches(second, segment.End, tolerance)));
	}

	public static bool TryFindHorizontalBoundaryPoint(OutlineFeature2D outline, double y, double preferredX, double tolerance, out Point2D point)
	{
		point = default(Point2D);
		if (outline == null || !IsFinite(y) || !IsFinite(preferredX))
		{
			return false;
		}
		tolerance = NormalizeTolerance(tolerance);
		List<Point2D> list = GetHorizontalBoundaryIntersections(outline, y, tolerance).ToList();
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord || !IsFinite(segment.Start) || !IsFinite(segment.End))
			{
				continue;
			}
			if (Math.Abs(segment.End.Y - segment.Start.Y) <= Math.Max(tolerance, NumericEpsilon) && Math.Abs(y - segment.Start.Y) <= tolerance)
			{
				double x = Clamp(preferredX, segment.MinX, segment.MaxX);
				AddUnique(list, new Point2D(x, segment.Start.Y), tolerance);
			}
		}
		if (list.Count == 0)
		{
			return false;
		}
		point = list.OrderBy((Point2D candidate) => Math.Abs(candidate.X - preferredX)).ThenBy((Point2D candidate) => candidate.X).ThenBy((Point2D candidate) => candidate.Y).First();
		return IsPointOnBoundary(point, outline, tolerance);
	}

	public static bool TryFindVerticalBoundaryPoint(OutlineFeature2D outline, double x, double preferredY, double tolerance, out Point2D point)
	{
		point = default(Point2D);
		if (outline == null || !IsFinite(x) || !IsFinite(preferredY))
		{
			return false;
		}
		tolerance = NormalizeTolerance(tolerance);
		List<Point2D> list = GetVerticalBoundaryIntersections(outline, x, tolerance).ToList();
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord || !IsFinite(segment.Start) || !IsFinite(segment.End))
			{
				continue;
			}
			if (Math.Abs(segment.End.X - segment.Start.X) <= Math.Max(tolerance, NumericEpsilon) && Math.Abs(x - segment.Start.X) <= tolerance)
			{
				double y = Clamp(preferredY, segment.MinY, segment.MaxY);
				AddUnique(list, new Point2D(segment.Start.X, y), tolerance);
			}
		}
		if (list.Count == 0)
		{
			return false;
		}
		point = list.OrderBy((Point2D candidate) => Math.Abs(candidate.Y - preferredY)).ThenBy((Point2D candidate) => candidate.Y).ThenBy((Point2D candidate) => candidate.X).First();
		return IsPointOnBoundary(point, outline, tolerance);
	}

	public static IList<Point2D> GetHorizontalBoundaryIntersections(OutlineFeature2D outline, double y, double tolerance)
	{
		List<Point2D> list = new List<Point2D>();
		if (outline == null || !IsFinite(y))
		{
			return list;
		}
		tolerance = NormalizeTolerance(tolerance);
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord || !IsFinite(segment.Start) || !IsFinite(segment.End) || y < segment.MinY - tolerance || y > segment.MaxY + tolerance)
			{
				continue;
			}
			double dy = segment.End.Y - segment.Start.Y;
			if (Math.Abs(dy) <= Math.Max(tolerance, NumericEpsilon))
			{
				if (Math.Abs(y - segment.Start.Y) <= tolerance)
				{
					AddUnique(list, segment.Start, tolerance);
					AddUnique(list, segment.End, tolerance);
				}
				continue;
			}
			double ratio = (y - segment.Start.Y) / dy;
			if (ratio >= -NumericEpsilon && ratio <= 1.0 + NumericEpsilon)
			{
				ratio = Clamp(ratio, 0.0, 1.0);
				AddUnique(list, new Point2D(segment.Start.X + ratio * (segment.End.X - segment.Start.X), segment.Start.Y + ratio * dy), tolerance);
			}
		}
		foreach (Arc2D arc in outline.Arcs)
		{
			if (!IsValidArc(arc) || Math.Abs(y - arc.Center.Y) > arc.Radius + tolerance)
			{
				continue;
			}
			double dy = y - arc.Center.Y;
			double squared = arc.Radius * arc.Radius - dy * dy;
			if (squared < 0.0)
			{
				squared = 0.0;
			}
			double dx = Math.Sqrt(squared);
			AddArcPointIfPresent(list, arc, new Point2D(arc.Center.X - dx, y), tolerance);
			AddArcPointIfPresent(list, arc, new Point2D(arc.Center.X + dx, y), tolerance);
		}
		return list.OrderBy((Point2D candidate) => candidate.X).ThenBy((Point2D candidate) => candidate.Y).ToList();
	}

	public static IList<Point2D> GetVerticalBoundaryIntersections(OutlineFeature2D outline, double x, double tolerance)
	{
		List<Point2D> list = new List<Point2D>();
		if (outline == null || !IsFinite(x))
		{
			return list;
		}
		tolerance = NormalizeTolerance(tolerance);
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord || !IsFinite(segment.Start) || !IsFinite(segment.End) || x < segment.MinX - tolerance || x > segment.MaxX + tolerance)
			{
				continue;
			}
			double dx = segment.End.X - segment.Start.X;
			if (Math.Abs(dx) <= Math.Max(tolerance, NumericEpsilon))
			{
				if (Math.Abs(x - segment.Start.X) <= tolerance)
				{
					AddUnique(list, segment.Start, tolerance);
					AddUnique(list, segment.End, tolerance);
				}
				continue;
			}
			double ratio = (x - segment.Start.X) / dx;
			if (ratio >= -NumericEpsilon && ratio <= 1.0 + NumericEpsilon)
			{
				ratio = Clamp(ratio, 0.0, 1.0);
				AddUnique(list, new Point2D(segment.Start.X + ratio * dx, segment.Start.Y + ratio * (segment.End.Y - segment.Start.Y)), tolerance);
			}
		}
		foreach (Arc2D arc in outline.Arcs)
		{
			if (!IsValidArc(arc) || Math.Abs(x - arc.Center.X) > arc.Radius + tolerance)
			{
				continue;
			}
			double dx = x - arc.Center.X;
			double squared = arc.Radius * arc.Radius - dx * dx;
			if (squared < 0.0)
			{
				squared = 0.0;
			}
			double dy = Math.Sqrt(squared);
			AddArcPointIfPresent(list, arc, new Point2D(x, arc.Center.Y - dy), tolerance);
			AddArcPointIfPresent(list, arc, new Point2D(x, arc.Center.Y + dy), tolerance);
		}
		return list.OrderBy((Point2D candidate) => candidate.Y).ThenBy((Point2D candidate) => candidate.X).ToList();
	}

	private static List<Point2D> GetEnvelopeCandidates(OutlineFeature2D outline, double tolerance)
	{
		List<Point2D> list = new List<Point2D>();
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord || !IsFinite(segment.Start) || !IsFinite(segment.End) || segment.Start.DistanceTo(segment.End) <= NumericEpsilon)
			{
				continue;
			}
			AddUnique(list, segment.Start, tolerance);
			AddUnique(list, segment.End, tolerance);
		}
		foreach (Arc2D arc in outline.Arcs)
		{
			if (!IsValidArc(arc))
			{
				continue;
			}
			AddUnique(list, arc.Start, tolerance);
			AddUnique(list, arc.End, tolerance);
			foreach (double angle in CardinalAngles)
			{
				if (IsAngleOnArc(angle, arc, tolerance))
				{
					AddUnique(list, PointAt(arc, angle), tolerance);
				}
			}
		}
		return list;
	}

	private static bool IsPointOnSegment(Point2D point, Segment2D segment, double tolerance)
	{
		double dx = segment.End.X - segment.Start.X;
		double dy = segment.End.Y - segment.Start.Y;
		double lengthSquared = dx * dx + dy * dy;
		if (lengthSquared <= NumericEpsilon * NumericEpsilon)
		{
			return point.DistanceTo(segment.Start) <= tolerance;
		}
		double ratio = ((point.X - segment.Start.X) * dx + (point.Y - segment.Start.Y) * dy) / lengthSquared;
		ratio = Clamp(ratio, 0.0, 1.0);
		Point2D projection = new Point2D(segment.Start.X + ratio * dx, segment.Start.Y + ratio * dy);
		return point.DistanceTo(projection) <= tolerance;
	}

	private static bool SegmentsEqual(Segment2D first, Segment2D second, double tolerance)
	{
		return first.Start.DistanceTo(second.Start) <= tolerance && first.End.DistanceTo(second.End) <= tolerance
			|| first.Start.DistanceTo(second.End) <= tolerance && first.End.DistanceTo(second.Start) <= tolerance;
	}

	private static bool ChamferTouches(ChamferFeature2D chamfer, Point2D point, double tolerance)
	{
		return chamfer != null && IsFinite(chamfer.StartPoint) && IsFinite(chamfer.EndPoint)
			&& (chamfer.StartPoint.DistanceTo(point) <= tolerance || chamfer.EndPoint.DistanceTo(point) <= tolerance);
	}

	private static bool IsPointOnArc(Point2D point, Arc2D arc, double tolerance)
	{
		if (!IsValidArc(arc))
		{
			return false;
		}
		if (point.DistanceTo(arc.Start) <= tolerance || point.DistanceTo(arc.End) <= tolerance)
		{
			return true;
		}
		if (Math.Abs(point.DistanceTo(arc.Center) - arc.Radius) > tolerance || Math.Abs(arc.Bulge) <= NumericEpsilon)
		{
			return false;
		}
		double angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X);
		return IsAngleOnArc(angle, arc, tolerance);
	}

	private static bool IsAngleOnArc(double angle, Arc2D arc, double tolerance)
	{
		if (!IsValidArc(arc) || Math.Abs(arc.Bulge) <= NumericEpsilon)
		{
			return false;
		}
		double startAngle = Math.Atan2(arc.Start.Y - arc.Center.Y, arc.Start.X - arc.Center.X);
		double sweep = 4.0 * Math.Atan(arc.Bulge);
		double angleTolerance = Math.Max(NumericEpsilon, tolerance / arc.Radius);
		if (sweep > 0.0)
		{
			return NormalizePositive(angle - startAngle) <= sweep + angleTolerance;
		}
		return NormalizePositive(startAngle - angle) <= 0.0 - sweep + angleTolerance;
	}

	private static bool IsValidArc(Arc2D arc)
	{
		return arc != null && IsFinite(arc.Start) && IsFinite(arc.End) && IsFinite(arc.Center) && IsFinite(arc.Radius) && arc.Radius > NumericEpsilon;
	}

	private static Point2D PointAt(Arc2D arc, double angle)
	{
		return new Point2D(arc.Center.X + Math.Cos(angle) * arc.Radius, arc.Center.Y + Math.Sin(angle) * arc.Radius);
	}

	private static void AddArcPointIfPresent(IList<Point2D> points, Arc2D arc, Point2D point, double tolerance)
	{
		if (IsPointOnArc(point, arc, tolerance))
		{
			AddUnique(points, point, tolerance);
		}
	}

	private static void AddUnique(IList<Point2D> points, Point2D point, double tolerance)
	{
		if (IsFinite(point) && !points.Any((Point2D existing) => existing.DistanceTo(point) <= tolerance))
		{
			points.Add(point);
		}
	}

	private static double NormalizePositive(double angle)
	{
		double fullTurn = Math.PI * 2.0;
		angle %= fullTurn;
		return (angle < 0.0) ? (angle + fullTurn) : angle;
	}

	private static double Clamp(double value, double minimum, double maximum)
	{
		return Math.Max(minimum, Math.Min(maximum, value));
	}

	private static double NormalizeTolerance(double tolerance)
	{
		return (!IsFinite(tolerance) || tolerance < 0.0) ? 0.0 : tolerance;
	}

	private static bool IsFinite(Point2D point)
	{
		return IsFinite(point.X) && IsFinite(point.Y);
	}

	private static bool IsFinite(double value)
	{
		return !double.IsNaN(value) && !double.IsInfinity(value);
	}
}
