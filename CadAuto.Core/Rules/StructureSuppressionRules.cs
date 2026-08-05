using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public sealed class StructureSuppressionRules
{
	private readonly DimensionRuleConfig _config;

	public StructureSuppressionRules(DimensionRuleConfig config)
	{
		if (config == null)
		{
			throw new ArgumentNullException("config");
		}
		_config = config;
	}

	public bool LeftExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline, double firstDimensionOffset)
	{
		double geometryTolerance = _config.GeometryTolerance;
		double num = outline.MinX - firstDimensionOffset;
		double num2 = featurePoint.X - geometryTolerance;
		if (num2 <= num + geometryTolerance)
		{
			return false;
		}
		foreach (Segment2D segment in outline.Segments)
		{
			if (TryGetHorizontalIntersectionX(segment, featurePoint.Y, out var x) && x >= num - geometryTolerance && x <= num2)
			{
				return true;
			}
			if (HorizontalExtensionOverlapsOutlineSegment(segment, featurePoint, num, num2))
			{
				return true;
			}
		}
		return false;
	}

	public bool RightExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline, double firstDimensionOffset)
	{
		double geometryTolerance = _config.GeometryTolerance;
		double num = featurePoint.X + geometryTolerance;
		double num2 = outline.MaxX + firstDimensionOffset;
		if (num2 <= num + geometryTolerance)
		{
			return false;
		}
		foreach (Segment2D segment in outline.Segments)
		{
			if (TryGetHorizontalIntersectionX(segment, featurePoint.Y, out var x) && x >= num && x <= num2 + geometryTolerance)
			{
				return true;
			}
			if (HorizontalExtensionOverlapsOutlineSegment(segment, featurePoint, num, num2))
			{
				return true;
			}
		}
		return false;
	}

	public bool TopExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline, double firstDimensionOffset)
	{
		double geometryTolerance = _config.GeometryTolerance;
		double num = featurePoint.Y + geometryTolerance;
		double num2 = outline.MaxY + firstDimensionOffset;
		if (num2 <= num + geometryTolerance)
		{
			return false;
		}
		foreach (Segment2D segment in outline.Segments)
		{
			if (TryGetVerticalIntersectionY(segment, featurePoint.X, out var y) && y >= num && y <= num2 + geometryTolerance)
			{
				return true;
			}
			if (VerticalExtensionOverlapsOutlineSegment(segment, featurePoint, num, num2))
			{
				return true;
			}
		}
		return false;
	}

	public bool BottomExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline, double firstDimensionOffset)
	{
		double geometryTolerance = _config.GeometryTolerance;
		double num = outline.MinY - firstDimensionOffset;
		double num2 = featurePoint.Y - geometryTolerance;
		if (num2 <= num + geometryTolerance)
		{
			return false;
		}
		foreach (Segment2D segment in outline.Segments)
		{
			if (TryGetVerticalIntersectionY(segment, featurePoint.X, out var y) && y >= num - geometryTolerance && y <= num2)
			{
				return true;
			}
			if (VerticalExtensionOverlapsOutlineSegment(segment, featurePoint, num, num2))
			{
				return true;
			}
		}
		return false;
	}

	public bool IsPointInsideOutlineByRayCast(double x, double y, OutlineFeature2D outline)
	{
		bool flag = false;
		double geometryTolerance = _config.GeometryTolerance;
		foreach (Segment2D segment in outline.Segments)
		{
			double y2 = segment.Start.Y;
			double y3 = segment.End.Y;
			if (!(Math.Abs(y2 - y3) <= geometryTolerance) && y2 > y != y3 > y)
			{
				double num = segment.Start.X + (y - y2) * (segment.End.X - segment.Start.X) / (y3 - y2);
				if (num > x + geometryTolerance)
				{
					flag = !flag;
				}
			}
		}
		return flag;
	}

	public bool IsOuterHorizontalSegment(Segment2D segment, OutlineFeature2D outline)
	{
		double geometryTolerance = _config.GeometryTolerance;
		if (!segment.IsHorizontal(geometryTolerance))
		{
			return false;
		}
		return Math.Abs(segment.MinY - outline.MinY) <= geometryTolerance || Math.Abs(segment.MaxY - outline.MaxY) <= geometryTolerance;
	}

	public bool IsOuterVerticalSegment(Segment2D segment, OutlineFeature2D outline)
	{
		double geometryTolerance = _config.GeometryTolerance;
		if (!segment.IsVertical(geometryTolerance))
		{
			return false;
		}
		return Math.Abs(segment.MinX - outline.MinX) <= geometryTolerance || Math.Abs(segment.MaxX - outline.MaxX) <= geometryTolerance;
	}

	public bool IsEnvelopeSidePoint(Point2D point, OutlineFeature2D outline)
	{
		if (outline == null)
		{
			return false;
		}
		return point.X <= outline.MinX + _config.GeometryTolerance || point.X >= outline.MaxX - _config.GeometryTolerance;
	}

	public bool IsEnvelopeHorizontalSidePoint(Point2D point, OutlineFeature2D outline)
	{
		if (outline == null)
		{
			return false;
		}
		return point.Y <= outline.MinY + _config.GeometryTolerance || point.Y >= outline.MaxY - _config.GeometryTolerance;
	}

	public bool IsTopChamferEndpoint(Point2D point, OutlineFeature2D outline, double topBandDepth)
	{
		if (outline == null || outline.Chamfers.Count == 0)
		{
			return false;
		}
		if (point.Y < outline.MaxY - topBandDepth - _config.GeometryTolerance)
		{
			return false;
		}
		if (point.X <= outline.MinX + _config.GeometryTolerance || point.X >= outline.MaxX - _config.GeometryTolerance)
		{
			return false;
		}
		return outline.Chamfers.Any((ChamferFeature2D chamfer) => (PointsEqual(chamfer.StartPoint, point) || PointsEqual(chamfer.EndPoint, point)) && IsFortyFiveDegreeChamfer(chamfer));
	}

	public bool IsFortyFiveDegreeChamfer(ChamferFeature2D chamfer)
	{
		double num = Math.Abs(chamfer.StartPoint.X - chamfer.EndPoint.X);
		double num2 = Math.Abs(chamfer.StartPoint.Y - chamfer.EndPoint.Y);
		double num3 = Math.Max(_config.GeometryTolerance, Math.Max(num, num2) * 0.08);
		return num > _config.GeometryTolerance && num2 > _config.GeometryTolerance && Math.Abs(num - num2) <= num3;
	}

	public bool IsFortyFiveDegreeSegment(Segment2D segment)
	{
		double num = Math.Abs(segment.Start.X - segment.End.X);
		double num2 = Math.Abs(segment.Start.Y - segment.End.Y);
		double num3 = Math.Max(_config.GeometryTolerance, Math.Max(num, num2) * 0.08);
		return num > _config.GeometryTolerance && num2 > _config.GeometryTolerance && Math.Abs(num - num2) <= num3;
	}

	public bool IsIgnorableFortyFiveDegreeSegment(Segment2D segment)
	{
		double num = Math.Abs(segment.Start.X - segment.End.X);
		double num2 = Math.Abs(segment.Start.Y - segment.End.Y);
		double num3 = Math.Max(_config.GeometryTolerance, Math.Max(num, num2) * 0.02);
		return num > _config.GeometryTolerance && num2 > _config.GeometryTolerance && Math.Abs(num - num2) <= num3;
	}

	public bool IsInnerGrooveChamferSegment(Segment2D chamfer, OutlineFeature2D outline, bool isTopSide)
	{
		if (outline == null || !IsFortyFiveDegreeSegment(chamfer))
		{
			return false;
		}
		return GetInnerGrooveHorizontalSharedPoint(chamfer, outline, isTopSide).HasValue;
	}

	public Point2D? GetInnerGrooveHorizontalSharedPoint(Segment2D chamfer, OutlineFeature2D outline, bool isTopSide)
	{
		if (outline == null || chamfer == null)
		{
			return null;
		}
		double tolerance = _config.GeometryTolerance;
		double num = Math.Max(chamfer.Start.Y, chamfer.End.Y);
		double num2 = Math.Min(chamfer.Start.Y, chamfer.End.Y);
		foreach (Segment2D item in outline.Segments.Where((Segment2D s) => s.IsHorizontal(tolerance) && !s.IsArcChord))
		{
			if (!(Math.Abs(item.MinY - outline.MaxY) <= tolerance) && !(Math.Abs(item.MinY - outline.MinY) <= tolerance) && (!isTopSide || !(item.MinY >= num - tolerance)) && (isTopSide || !(item.MinY <= num2 + tolerance)))
			{
				if (SegmentTouchesPoint(item, chamfer.Start) && HorizontalOtherEndConnectsInnerGroove(item, chamfer.Start, chamfer, outline))
				{
					return chamfer.Start;
				}
				if (SegmentTouchesPoint(item, chamfer.End) && HorizontalOtherEndConnectsInnerGroove(item, chamfer.End, chamfer, outline))
				{
					return chamfer.End;
				}
			}
		}
		return null;
	}

	public bool IsInnerGrooveIgnoredEndpoint(Point2D point, Segment2D chamfer, OutlineFeature2D outline, bool isTopSide)
	{
		Point2D? innerGrooveHorizontalSharedPoint = GetInnerGrooveHorizontalSharedPoint(chamfer, outline, isTopSide);
		return innerGrooveHorizontalSharedPoint.HasValue && PointsEqual(point, innerGrooveHorizontalSharedPoint.Value) && IsDirectionalIgnoredEndpoint(point, chamfer, isTopSide);
	}

	public bool IsSideInnerGrooveChamferSegment(Segment2D chamfer, OutlineFeature2D outline, DimensionSide side)
	{
		if (outline == null || !IsFortyFiveDegreeSegment(chamfer))
		{
			return false;
		}
		return GetSideInnerGrooveVerticalSharedPoint(chamfer, outline, side).HasValue;
	}

	public Point2D? GetSideInnerGrooveVerticalSharedPoint(Segment2D chamfer, OutlineFeature2D outline, DimensionSide side)
	{
		if (outline == null || chamfer == null)
		{
			return null;
		}
		double tolerance = _config.GeometryTolerance;
		double num = Math.Min(chamfer.Start.X, chamfer.End.X);
		double num2 = Math.Max(chamfer.Start.X, chamfer.End.X);
		foreach (Segment2D item in outline.Segments.Where((Segment2D s) => s.IsVertical(tolerance) && !s.IsArcChord))
		{
			if (!(Math.Abs(item.MinX - outline.MinX) <= tolerance) && !(Math.Abs(item.MinX - outline.MaxX) <= tolerance) && (side != DimensionSide.Left || !(item.MinX <= num + tolerance)) && (side != DimensionSide.Right || !(item.MinX >= num2 - tolerance)))
			{
				if (SegmentTouchesPoint(item, chamfer.Start) && IsInternalSideGrooveVertical(item, chamfer, side, outline) && VerticalOtherEndConnectsChamfer(item, chamfer.Start, chamfer, outline))
				{
					return chamfer.Start;
				}
				if (SegmentTouchesPoint(item, chamfer.End) && IsInternalSideGrooveVertical(item, chamfer, side, outline) && VerticalOtherEndConnectsChamfer(item, chamfer.End, chamfer, outline))
				{
					return chamfer.End;
				}
			}
		}
		return null;
	}

	public bool IsSideInnerGrooveIgnoredEndpoint(Point2D point, Segment2D chamfer, OutlineFeature2D outline, DimensionSide side)
	{
		Point2D? sideInnerGrooveVerticalSharedPoint = GetSideInnerGrooveVerticalSharedPoint(chamfer, outline, side);
		return sideInnerGrooveVerticalSharedPoint.HasValue && PointsEqual(point, sideInnerGrooveVerticalSharedPoint.Value);
	}

	public bool IsInternalSideGrooveVertical(Segment2D vertical, Segment2D chamfer, DimensionSide side, OutlineFeature2D outline)
	{
		double geometryTolerance = _config.GeometryTolerance;
		if (outline == null || vertical == null || chamfer == null || !vertical.IsVertical(geometryTolerance) || Math.Abs(vertical.MinX - outline.MinX) <= geometryTolerance || Math.Abs(vertical.MinX - outline.MaxX) <= geometryTolerance)
		{
			return false;
		}
		double num = Math.Min(chamfer.Start.X, chamfer.End.X);
		double num2 = Math.Max(chamfer.Start.X, chamfer.End.X);
		return side switch
		{
			DimensionSide.Left => vertical.MinX > num + geometryTolerance,
			DimensionSide.Right => vertical.MinX < num2 - geometryTolerance,
			_ => false,
		};
	}

	public string GetDirectionalInclinedIgnoreReason(Point2D point, OutlineFeature2D outline, bool invertDirection)
	{
		if (outline == null)
		{
			return string.Empty;
		}
		IEnumerable<Segment2D> enumerable = (from s in outline.Segments
			where !s.IsHorizontal(_config.GeometryTolerance)
			where !s.IsVertical(_config.GeometryTolerance)
			select s).Where(IsIgnorableFortyFiveDegreeSegment);
		if (invertDirection)
		{
			foreach (Segment2D item in enumerable)
			{
				if (IsInnerGrooveChamferSegment(item, outline, isTopSide: true) && IsInnerGrooveIgnoredEndpoint(point, item, outline, isTopSide: true))
				{
					return "TopInnerGrooveSharedEndpoint";
				}
				if (IsDirectionalIgnoredEndpoint(point, item, invertDirection: true))
				{
					return "TopDirectional45Endpoint";
				}
			}
			return string.Empty;
		}
		foreach (Segment2D item2 in enumerable)
		{
			if (IsInnerGrooveChamferSegment(item2, outline, isTopSide: false) && IsInnerGrooveIgnoredEndpoint(point, item2, outline, isTopSide: false))
			{
				return "BottomInnerGrooveSharedEndpoint";
			}
			if (IsDirectionalIgnoredEndpoint(point, item2, invertDirection: false))
			{
				return "BottomDirectional45Endpoint";
			}
		}
		return string.Empty;
	}

	public bool ShouldIgnoreSideDirectionalInclinedEndpoint(Point2D point, OutlineFeature2D outline, DimensionSide side)
	{
		if (outline == null)
		{
			return false;
		}
		return (from s in outline.Segments
			where !s.IsHorizontal(_config.GeometryTolerance)
			where !s.IsVertical(_config.GeometryTolerance)
			select s).Where(IsIgnorableFortyFiveDegreeSegment).Any((Segment2D s) => IsSideInnerGrooveChamferSegment(s, outline, side) ? IsSideInnerGrooveIgnoredEndpoint(point, s, outline, side) : IsSideDirectionalIgnoredEndpoint(point, s, side));
	}

	public bool ShouldIgnoreSideInnerGrooveEndpoint(Point2D point, OutlineFeature2D outline, DimensionSide side)
	{
		if (outline == null)
		{
			return false;
		}
		return (from s in (from s in outline.Segments
				where !s.IsHorizontal(_config.GeometryTolerance)
				where !s.IsVertical(_config.GeometryTolerance)
				select s).Where(IsIgnorableFortyFiveDegreeSegment)
			where IsSideInnerGrooveChamferSegment(s, outline, side)
			select s).Any((Segment2D s) => IsSideInnerGrooveIgnoredEndpoint(point, s, outline, side));
	}

	public bool IsSideDirectionalIgnoredEndpoint(Point2D point, Segment2D segment, DimensionSide side)
	{
		if (!PointsEqual(point, segment.Start) && !PointsEqual(point, segment.End))
		{
			return false;
		}
		double num = segment.End.X - segment.Start.X;
		double num2 = segment.End.Y - segment.Start.Y;
		if (Math.Abs(num) <= _config.GeometryTolerance || Math.Abs(num2) <= _config.GeometryTolerance)
		{
			return false;
		}
		if (num * num2 > 0.0)
		{
			Point2D b = ((side != DimensionSide.Left) ? ((segment.Start.Y >= segment.End.Y) ? segment.Start : segment.End) : ((segment.Start.Y <= segment.End.Y) ? segment.Start : segment.End));
			return PointsEqual(point, b);
		}
		Point2D b2 = ((side != DimensionSide.Left) ? ((segment.Start.Y <= segment.End.Y) ? segment.Start : segment.End) : ((segment.Start.Y >= segment.End.Y) ? segment.Start : segment.End));
		return PointsEqual(point, b2);
	}

	public bool IsPointXWithinSegmentXRange(Point2D point, Segment2D segment)
	{
		double geometryTolerance = _config.GeometryTolerance;
		double num = Math.Min(segment.Start.X, segment.End.X);
		double num2 = Math.Max(segment.Start.X, segment.End.X);
		return point.X >= num - geometryTolerance && point.X <= num2 + geometryTolerance;
	}

	public bool IsPointOnSegment(Point2D point, Segment2D segment)
	{
		double num = Math.Max(_config.GeometryTolerance, 0.2);
		double num2 = Math.Min(segment.Start.X, segment.End.X) - num;
		double num3 = Math.Max(segment.Start.X, segment.End.X) + num;
		double num4 = Math.Min(segment.Start.Y, segment.End.Y) - num;
		double num5 = Math.Max(segment.Start.Y, segment.End.Y) + num;
		if (point.X < num2 || point.X > num3 || point.Y < num4 || point.Y > num5)
		{
			return false;
		}
		double num6 = segment.End.X - segment.Start.X;
		double num7 = segment.End.Y - segment.Start.Y;
		double num8 = Math.Abs((point.X - segment.Start.X) * num7 - (point.Y - segment.Start.Y) * num6);
		double num9 = Math.Sqrt(num6 * num6 + num7 * num7);
		return num9 <= num || num8 / num9 <= num;
	}

	public bool IsPointOnVerticalOutlineSegment(Point2D point, OutlineFeature2D outline)
	{
		double tolerance = _config.GeometryTolerance;
		return outline.Segments.Where((Segment2D s) => s.IsVertical(tolerance)).Any((Segment2D s) => Math.Abs(s.MinX - point.X) <= tolerance && point.Y >= s.MinY - tolerance && point.Y <= s.MaxY + tolerance);
	}

	public bool IsPointOnHorizontalOutlineSegment(Point2D point, OutlineFeature2D outline)
	{
		double tolerance = _config.GeometryTolerance;
		return outline.Segments.Where((Segment2D s) => s.IsHorizontal(tolerance)).Any((Segment2D s) => Math.Abs(s.MinY - point.Y) <= tolerance && point.X >= s.MinX - tolerance && point.X <= s.MaxX + tolerance);
	}

	private bool TryGetHorizontalIntersectionX(Segment2D segment, double y, out double x)
	{
		double geometryTolerance = _config.GeometryTolerance;
		x = 0.0;
		if (Math.Abs(segment.Start.Y - segment.End.Y) <= geometryTolerance)
		{
			return false;
		}
		double minY = segment.MinY;
		double maxY = segment.MaxY;
		if (y < minY - geometryTolerance || y > maxY + geometryTolerance)
		{
			return false;
		}
		x = segment.Start.X + (y - segment.Start.Y) * (segment.End.X - segment.Start.X) / (segment.End.Y - segment.Start.Y);
		return true;
	}

	private bool TryGetVerticalIntersectionY(Segment2D segment, double x, out double y)
	{
		double geometryTolerance = _config.GeometryTolerance;
		y = 0.0;
		if (Math.Abs(segment.Start.X - segment.End.X) <= geometryTolerance)
		{
			return false;
		}
		double minX = segment.MinX;
		double maxX = segment.MaxX;
		if (x < minX - geometryTolerance || x > maxX + geometryTolerance)
		{
			return false;
		}
		y = segment.Start.Y + (x - segment.Start.X) * (segment.End.Y - segment.Start.Y) / (segment.End.X - segment.Start.X);
		return true;
	}

	private bool HorizontalExtensionOverlapsOutlineSegment(Segment2D segment, Point2D featurePoint, double minX, double maxX)
	{
		double geometryTolerance = _config.GeometryTolerance;
		if (!segment.IsHorizontal(geometryTolerance))
		{
			return false;
		}
		if (Math.Abs(segment.MinY - featurePoint.Y) > geometryTolerance)
		{
			return false;
		}
		double num = Math.Max(segment.MinX, minX);
		double num2 = Math.Min(segment.MaxX, maxX);
		if (num2 <= num + geometryTolerance)
		{
			return false;
		}
		return !PointLiesOnSegmentHorizontalExtent(segment, featurePoint.X);
	}

	private bool PointLiesOnSegmentHorizontalExtent(Segment2D segment, double x)
	{
		double geometryTolerance = _config.GeometryTolerance;
		return x >= segment.MinX - geometryTolerance && x <= segment.MaxX + geometryTolerance;
	}

	private bool VerticalExtensionOverlapsOutlineSegment(Segment2D segment, Point2D featurePoint, double minY, double maxY)
	{
		double geometryTolerance = _config.GeometryTolerance;
		if (!segment.IsVertical(geometryTolerance))
		{
			return false;
		}
		if (Math.Abs(segment.MinX - featurePoint.X) > geometryTolerance)
		{
			return false;
		}
		double num = Math.Max(segment.MinY, minY);
		double num2 = Math.Min(segment.MaxY, maxY);
		if (num2 <= num + geometryTolerance)
		{
			return false;
		}
		return !PointLiesOnSegmentVerticalExtent(segment, featurePoint.Y);
	}

	private bool PointLiesOnSegmentVerticalExtent(Segment2D segment, double y)
	{
		double geometryTolerance = _config.GeometryTolerance;
		return y >= segment.MinY - geometryTolerance && y <= segment.MaxY + geometryTolerance;
	}

	private bool HorizontalOtherEndConnectsInnerGroove(Segment2D horizontal, Point2D sharedPoint, Segment2D currentChamfer, OutlineFeature2D outline)
	{
		Point2D otherEnd = (PointsEqual(horizontal.Start, sharedPoint) ? horizontal.End : horizontal.Start);
		return outline.Segments.Any((Segment2D segment) => segment != currentChamfer && !segment.IsHorizontal(_config.GeometryTolerance) && !segment.IsVertical(_config.GeometryTolerance) && IsFortyFiveDegreeSegment(segment) && (PointsEqual(segment.Start, otherEnd) || PointsEqual(segment.End, otherEnd))) || outline.Chamfers.Any((ChamferFeature2D chamfer) => IsFortyFiveDegreeChamfer(chamfer) && (PointsEqual(chamfer.StartPoint, otherEnd) || PointsEqual(chamfer.EndPoint, otherEnd))) || outline.Fillets.Any((FilletFeature2D fillet) => PointsEqual(fillet.StartPoint, otherEnd) || PointsEqual(fillet.EndPoint, otherEnd));
	}

	private bool VerticalOtherEndConnectsChamfer(Segment2D vertical, Point2D sharedPoint, Segment2D currentChamfer, OutlineFeature2D outline)
	{
		Point2D otherEnd = (PointsEqual(vertical.Start, sharedPoint) ? vertical.End : vertical.Start);
		return outline.Segments.Any((Segment2D segment) => segment != currentChamfer && !segment.IsHorizontal(_config.GeometryTolerance) && !segment.IsVertical(_config.GeometryTolerance) && IsFortyFiveDegreeSegment(segment) && (PointsEqual(segment.Start, otherEnd) || PointsEqual(segment.End, otherEnd))) || outline.Chamfers.Any((ChamferFeature2D chamfer) => IsFortyFiveDegreeChamfer(chamfer) && (PointsEqual(chamfer.StartPoint, otherEnd) || PointsEqual(chamfer.EndPoint, otherEnd))) || outline.Fillets.Any((FilletFeature2D fillet) => PointsEqual(fillet.StartPoint, otherEnd) || PointsEqual(fillet.EndPoint, otherEnd));
	}

	public bool IsDirectionalIgnoredEndpoint(Point2D point, Segment2D segment, bool invertDirection)
	{
		if (!PointsEqual(point, segment.Start) && !PointsEqual(point, segment.End))
		{
			return false;
		}
		double num = segment.End.X - segment.Start.X;
		double num2 = segment.End.Y - segment.Start.Y;
		if (Math.Abs(num) <= _config.GeometryTolerance || Math.Abs(num2) <= _config.GeometryTolerance)
		{
			return false;
		}
		if (num * num2 > 0.0)
		{
			Point2D b = ((!invertDirection) ? ((segment.Start.X <= segment.End.X) ? segment.Start : segment.End) : ((segment.Start.X >= segment.End.X) ? segment.Start : segment.End));
			return PointsEqual(point, b);
		}
		Point2D b2 = ((!invertDirection) ? ((segment.Start.X >= segment.End.X) ? segment.Start : segment.End) : ((segment.Start.X <= segment.End.X) ? segment.Start : segment.End));
		return PointsEqual(point, b2);
	}

	private bool SegmentTouchesPoint(Segment2D segment, Point2D point)
	{
		return PointsEqual(segment.Start, point) || PointsEqual(segment.End, point);
	}

	private bool PointsEqual(Point2D a, Point2D b)
	{
		return a.DistanceTo(b) <= Math.Max(_config.GeometryTolerance, 0.2);
	}
}
