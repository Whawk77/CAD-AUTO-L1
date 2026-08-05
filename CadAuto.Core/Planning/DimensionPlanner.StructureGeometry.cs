using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning;

public sealed partial class DimensionPlanner
{
	private List<IgnoredPoint> GetBottomExtensionCrossingPoints(IList<PlannedDimension> candidates, OutlineFeature2D outline)
	{
		List<IgnoredPoint> list = new List<IgnoredPoint>();
		foreach (PlannedDimension candidate in candidates)
		{
			AddBottomCrossingPoint(list, candidate.FirstPoint, outline);
			AddBottomCrossingPoint(list, candidate.SecondPoint, outline);
			AddDirectionalInclinedIgnoredPoint(list, candidate.FirstPoint, outline, invertDirection: false);
			AddDirectionalInclinedIgnoredPoint(list, candidate.SecondPoint, outline, invertDirection: false);
		}
		return list;
	}

	private List<IgnoredPoint> GetLeftExtensionCrossingPoints(IList<PlannedDimension> candidates, OutlineFeature2D outline)
	{
		List<IgnoredPoint> list = new List<IgnoredPoint>();
		foreach (PlannedDimension candidate in candidates)
		{
			AddLeftCrossingPoint(list, candidate.FirstPoint, outline);
			AddLeftCrossingPoint(list, candidate.SecondPoint, outline);
			AddSideDirectionalInclinedEndpoint(list, candidate.FirstPoint, outline, DimensionSide.Left);
			AddSideDirectionalInclinedEndpoint(list, candidate.SecondPoint, outline, DimensionSide.Left);
		}
		return list;
	}

	private List<IgnoredPoint> GetRightExtensionCrossingPoints(IList<PlannedDimension> candidates, OutlineFeature2D outline)
	{
		List<IgnoredPoint> list = new List<IgnoredPoint>();
		foreach (PlannedDimension candidate in candidates)
		{
			AddRightCrossingPoint(list, candidate.FirstPoint, outline);
			AddRightCrossingPoint(list, candidate.SecondPoint, outline);
			AddSideDirectionalInclinedEndpoint(list, candidate.FirstPoint, outline, DimensionSide.Right);
			AddSideDirectionalInclinedEndpoint(list, candidate.SecondPoint, outline, DimensionSide.Right);
		}
		return list;
	}

	private void AddLeftCrossingPoint(IList<IgnoredPoint> points, Point2D featurePoint, OutlineFeature2D outline)
	{
		if (LeftExtensionCrossesOutline(featurePoint, outline) && !ContainsIgnoredPoint(points, featurePoint))
		{
			points.Add(new IgnoredPoint
			{
				Point = featurePoint,
				Reason = "LeftExtensionCrossesOutline"
			});
		}
	}

	private void AddRightCrossingPoint(IList<IgnoredPoint> points, Point2D featurePoint, OutlineFeature2D outline)
	{
		if (RightExtensionCrossesOutline(featurePoint, outline) && !ContainsIgnoredPoint(points, featurePoint))
		{
			points.Add(new IgnoredPoint
			{
				Point = featurePoint,
				Reason = "RightExtensionCrossesOutline"
			});
		}
	}

	private void AddBottomCrossingPoint(IList<IgnoredPoint> points, Point2D featurePoint, OutlineFeature2D outline)
	{
		if (BottomExtensionCrossesOutline(featurePoint, outline) && !ContainsIgnoredPoint(points, featurePoint))
		{
			points.Add(new IgnoredPoint
			{
				Point = featurePoint,
				Reason = "BottomExtensionCrossesOutline"
			});
		}
	}

	private void AddSideDirectionalInclinedEndpoint(IList<IgnoredPoint> points, Point2D point, OutlineFeature2D outline, DimensionSide side)
	{
		if (!ContainsIgnoredPoint(points, point) && !IsEnvelopeHorizontalSidePoint(point, outline) && ShouldIgnoreSideDirectionalInclinedEndpoint(point, outline, side))
		{
			points.Add(new IgnoredPoint
			{
				Point = point,
				Reason = side.ToString() + "DirectionalInclinedEndpoint"
			});
		}
	}

	private void RemoveLongestBottomExtensionCandidate(IList<PlannedDimension> candidates, OutlineFeature2D outline)
	{
		_structureEndpointRules.RemoveLongestExtensionCandidate(candidates, outline, DimensionSide.Bottom);
	}

	private void RemoveLongestLeftExtensionCandidate(IList<PlannedDimension> candidates, OutlineFeature2D outline)
	{
		_structureEndpointRules.RemoveLongestExtensionCandidate(candidates, outline, DimensionSide.Left);
	}

	private void RemoveLongestRightExtensionCandidate(IList<PlannedDimension> candidates, OutlineFeature2D outline)
	{
		_structureEndpointRules.RemoveLongestExtensionCandidate(candidates, outline, DimensionSide.Right);
	}

	private bool IsBottomSideHorizontalStructureCandidate(PlannedDimension dim, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		return _structureEndpointRules.IsBottomSideHorizontalStructureCandidate(dim, outline, ignoredPoints.Select((IgnoredPoint p) => p.Point).ToList());
	}

	private bool IsRightSideVerticalStructureCandidate(PlannedDimension dim, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		return _structureEndpointRules.IsRightSideVerticalStructureCandidate(dim, outline, ignoredPoints.Select((IgnoredPoint p) => p.Point).ToList());
	}

	private bool IsLeftSideVerticalStructureCandidate(PlannedDimension dim, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		return _structureEndpointRules.IsLeftSideVerticalStructureCandidate(dim, outline, ignoredPoints.Select((IgnoredPoint p) => p.Point).ToList());
	}

	private void AddSideInclinedEndpointStructurePoints(IList<Point2D> points, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints, DimensionSide side)
	{
		foreach (Segment2D item in from s in (from s in outline.Segments
				where !s.IsHorizontal(_config.GeometryTolerance)
				where !s.IsVertical(_config.GeometryTolerance)
				select s).Where(IsFortyFiveDegreeSegment)
			where IsSideInnerGrooveChamferSegment(s, outline, side)
			select s)
		{
			AddSideInclinedEndpointStructurePoint(points, item.Start, outline, ignoredPoints);
			AddSideInclinedEndpointStructurePoint(points, item.End, outline, ignoredPoints);
		}
	}

	private void AddSideInclinedEndpointStructurePoint(IList<Point2D> points, Point2D point, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		if (!ContainsPoint(points, point) && !ContainsIgnoredPoint(ignoredPoints, point) && !IsEnvelopeHorizontalSidePoint(point, outline))
		{
			points.Add(point);
		}
	}

	private void AddBottomInclinedEndpointStructurePoints(IList<Point2D> points, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		foreach (Segment2D item in from s in outline.Segments
			where !s.IsHorizontal(_config.GeometryTolerance)
			where !s.IsVertical(_config.GeometryTolerance)
			where IsInnerGrooveChamferSegment(s, outline, isTopSide: false)
			select s)
		{
			AddBottomInclinedEndpointStructurePoint(points, item.Start, outline, ignoredPoints);
			AddBottomInclinedEndpointStructurePoint(points, item.End, outline, ignoredPoints);
		}
	}

	private void AddBottomInclinedEndpointStructurePoint(IList<Point2D> points, Point2D point, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		if (!ContainsPoint(points, point) && !ContainsIgnoredPoint(ignoredPoints, point) && !IsEnvelopeSidePoint(point, outline))
		{
			points.Add(point);
		}
	}

	private bool BottomExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline)
	{
		return _structureSuppressionRules.BottomExtensionCrossesOutline(featurePoint, outline, _config.FirstDimOffset);
	}

	private bool LeftExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline)
	{
		return _structureSuppressionRules.LeftExtensionCrossesOutline(featurePoint, outline, _config.FirstDimOffset);
	}

	private bool RightExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline)
	{
		return _structureSuppressionRules.RightExtensionCrossesOutline(featurePoint, outline, _config.FirstDimOffset);
	}

	private bool ContainsPoint(IEnumerable<Point2D> points, Point2D point)
	{
		return points.Any((Point2D p) => PointsEqual(p, point));
	}

	private bool IsFortyFiveDegreeSegment(Segment2D segment)
	{
		return _structureSuppressionRules.IsFortyFiveDegreeSegment(segment);
	}

	private bool IsIgnorableFortyFiveDegreeSegment(Segment2D segment)
	{
		return _structureSuppressionRules.IsIgnorableFortyFiveDegreeSegment(segment);
	}

	private bool IsInnerGrooveChamferSegment(Segment2D chamfer, OutlineFeature2D outline, bool isTopSide)
	{
		return _structureSuppressionRules.IsInnerGrooveChamferSegment(chamfer, outline, isTopSide);
	}

	private bool IsSideInnerGrooveChamferSegment(Segment2D chamfer, OutlineFeature2D outline, DimensionSide side)
	{
		return _structureSuppressionRules.IsSideInnerGrooveChamferSegment(chamfer, outline, side);
	}

	private bool IsFortyFiveDegreeChamfer(ChamferFeature2D chamfer)
	{
		return _structureSuppressionRules.IsFortyFiveDegreeChamfer(chamfer);
	}

	private List<IgnoredPoint> GetTopExtensionIgnoredPoints(IList<PlannedDimension> candidates, OutlineFeature2D outline)
	{
		List<IgnoredPoint> list = new List<IgnoredPoint>();
		foreach (PlannedDimension candidate in candidates)
		{
			AddTopCrossingIgnoredPoint(list, candidate.FirstPoint, outline);
			AddTopCrossingIgnoredPoint(list, candidate.SecondPoint, outline);
			AddDirectionalInclinedIgnoredPoint(list, candidate.FirstPoint, outline, invertDirection: true);
			AddDirectionalInclinedIgnoredPoint(list, candidate.SecondPoint, outline, invertDirection: true);
		}
		return list;
	}

	private void AddTopCrossingIgnoredPoint(IList<IgnoredPoint> points, Point2D point, OutlineFeature2D outline)
	{
		if (TopExtensionCrossesOutline(point, outline) && !ContainsIgnoredPoint(points, point))
		{
			points.Add(new IgnoredPoint
			{
				Point = point,
				Reason = "TopExtensionCrossesOutline"
			});
		}
	}

	private void AddDirectionalInclinedIgnoredPoint(IList<IgnoredPoint> points, Point2D point, OutlineFeature2D outline, bool invertDirection)
	{
		string directionalInclinedIgnoreReason = GetDirectionalInclinedIgnoreReason(point, outline, invertDirection);
		if (!ContainsIgnoredPoint(points, point) && !IsEnvelopeSidePoint(point, outline) && !string.IsNullOrEmpty(directionalInclinedIgnoreReason))
		{
			points.Add(new IgnoredPoint
			{
				Point = point,
				Reason = directionalInclinedIgnoreReason
			});
		}
	}

	private bool AddIgnoredPointMetadata(IList<IgnoredPoint> ignoredPoints, IEnumerable<IgnoredPoint> crossingPoints)
	{
		bool result = false;
		foreach (IgnoredPoint crossingPoint in crossingPoints)
		{
			if (!ContainsIgnoredPoint(ignoredPoints, crossingPoint.Point))
			{
				ignoredPoints.Add(crossingPoint);
				result = true;
			}
		}
		return result;
	}

	private void RemoveLongestTopExtensionCandidate(IList<PlannedDimension> candidates, OutlineFeature2D outline)
	{
		_structureEndpointRules.RemoveLongestExtensionCandidate(candidates, outline, DimensionSide.Top);
	}

	private bool IsTopSideHorizontalStructureCandidate(PlannedDimension dim, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		return _structureEndpointRules.IsTopSideHorizontalStructureCandidate(dim, outline, ignoredPoints.Select((IgnoredPoint p) => p.Point).ToList());
	}

	private void AddTopInclinedEndpointStructurePoints(IList<StructurePoint> points, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		foreach (Segment2D item in from s in (from s in outline.Segments
				where !s.IsHorizontal(_config.GeometryTolerance)
				where !s.IsVertical(_config.GeometryTolerance)
				select s).Where(IsFortyFiveDegreeSegment)
			where IsInnerGrooveChamferSegment(s, outline, isTopSide: true)
			select s)
		{
			AddTopInclinedEndpointStructurePoint(points, item.Start, outline, ignoredPoints);
			AddTopInclinedEndpointStructurePoint(points, item.End, outline, ignoredPoints);
		}
	}

	private void AddTopInclinedEndpointStructurePoint(IList<StructurePoint> points, Point2D point, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		if (!ContainsStructurePoint(points, point) && !ContainsIgnoredPoint(ignoredPoints, point) && !IsEnvelopeSidePoint(point, outline))
		{
			points.Add(new StructurePoint
			{
				Point = point,
				Source = "TopInclinedEndpoint"
			});
		}
	}

	private void AddTopSlopeEndpointStructurePoints(IList<StructurePoint> points, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		foreach (Segment2D item in from s in outline.Segments
			where !s.IsHorizontal(_config.GeometryTolerance)
			where !s.IsVertical(_config.GeometryTolerance)
			where !s.IsArcChord
			select s)
		{
			Point2D point2D = ((item.Start.Y <= item.End.Y) ? item.Start : item.End);
			Point2D high = (PointsEqual(point2D, item.Start) ? item.End : item.Start);
			if (IsTopSlopeEndpointStructurePoint(point2D, high, item, outline, ignoredPoints))
			{
				AddTopSlopeEndpointStructurePoint(points, point2D, outline, ignoredPoints);
			}
		}
	}

	private bool IsTopSlopeEndpointStructurePoint(Point2D low, Point2D high, Segment2D slope, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		if (ContainsIgnoredPoint(ignoredPoints, low) || IsEnvelopeSidePoint(low, outline) || high.Y <= low.Y + _config.GeometryTolerance)
		{
			return false;
		}
		return EndpointConnectsHorizontalSegment(low, slope, outline) && EndpointConnectsHigherHorizontalSegment(high, low.Y, slope, outline);
	}

	private bool IsTopSlopeEndpointStructurePoint(Point2D point, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		if (ContainsIgnoredPoint(ignoredPoints, point) || IsEnvelopeSidePoint(point, outline))
		{
			return false;
		}
		foreach (Segment2D item in from s in outline.Segments
			where !s.IsHorizontal(_config.GeometryTolerance)
			where !s.IsVertical(_config.GeometryTolerance)
			where !s.IsArcChord
			select s)
		{
			if (PointsEqual(point, item.Start) || PointsEqual(point, item.End))
			{
				Point2D point2D = ((item.Start.Y <= item.End.Y) ? item.Start : item.End);
				Point2D high = (PointsEqual(point2D, item.Start) ? item.End : item.Start);
				if (PointsEqual(point, point2D) && IsTopSlopeEndpointStructurePoint(point2D, high, item, outline, ignoredPoints))
				{
					return true;
				}
			}
		}
		return false;
	}

	private void AddTopSlopeEndpointStructurePoint(IList<StructurePoint> points, Point2D point, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		if (!ContainsStructurePoint(points, point) && !ContainsIgnoredPoint(ignoredPoints, point) && !IsEnvelopeSidePoint(point, outline))
		{
			points.Add(new StructurePoint
			{
				Point = point,
				Source = "TopSlopeEndpoint"
			});
		}
	}

	private bool EndpointConnectsHorizontalSegment(Point2D point, Segment2D source, OutlineFeature2D outline)
	{
		return outline.Segments.Any((Segment2D segment) => segment != source && segment.IsHorizontal(_config.GeometryTolerance) && !segment.IsArcChord && IsPointOnHorizontalSegment(point, segment));
	}

	private bool EndpointConnectsHigherHorizontalSegment(Point2D point, double referenceY, Segment2D source, OutlineFeature2D outline)
	{
		return outline.Segments.Any((Segment2D segment) => segment != source && segment.IsHorizontal(_config.GeometryTolerance) && !segment.IsArcChord && segment.MinY > referenceY + _config.GeometryTolerance && IsPointOnHorizontalSegment(point, segment));
	}

	private bool IsPointOnHorizontalSegment(Point2D point, Segment2D segment)
	{
		return segment.IsHorizontal(_config.GeometryTolerance) && Math.Abs(segment.MinY - point.Y) <= _config.GeometryTolerance && point.X >= segment.MinX - _config.GeometryTolerance && point.X <= segment.MaxX + _config.GeometryTolerance;
	}

	private bool TopExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline)
	{
		return _structureSuppressionRules.TopExtensionCrossesOutline(featurePoint, outline, _config.FirstDimOffset);
	}

	private string GetDirectionalInclinedIgnoreReason(Point2D point, OutlineFeature2D outline, bool invertDirection)
	{
		return _structureSuppressionRules.GetDirectionalInclinedIgnoreReason(point, outline, invertDirection);
	}

	private bool ShouldIgnoreSideDirectionalInclinedEndpoint(Point2D point, OutlineFeature2D outline, DimensionSide side)
	{
		return _structureSuppressionRules.ShouldIgnoreSideDirectionalInclinedEndpoint(point, outline, side);
	}

	private bool IsSideDirectionalIgnoredEndpoint(Point2D point, Segment2D segment, DimensionSide side)
	{
		return _structureSuppressionRules.IsSideDirectionalIgnoredEndpoint(point, segment, side);
	}

	private bool IsEnvelopeSidePoint(Point2D point, OutlineFeature2D outline)
	{
		return _structureSuppressionRules.IsEnvelopeSidePoint(point, outline);
	}

	private bool IsEnvelopeHorizontalSidePoint(Point2D point, OutlineFeature2D outline)
	{
		return _structureSuppressionRules.IsEnvelopeHorizontalSidePoint(point, outline);
	}
}
