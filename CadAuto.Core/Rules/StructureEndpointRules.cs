using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public sealed class StructureEndpointRules
{
	private readonly DimensionRuleConfig _config;

	private readonly StructureSuppressionRules _structureRules;

	public StructureEndpointRules(DimensionRuleConfig config)
	{
		if (config == null)
		{
			throw new ArgumentNullException("config");
		}
		_config = config;
		_structureRules = new StructureSuppressionRules(config);
	}

	public StructureEndpointSpan ResolveHorizontalStepBoundarySpan(OutlineFeature2D outline, Segment2D segment, DimensionSide side)
	{
		Point2D leftPoint = ((segment.Start.X <= segment.End.X) ? segment.Start : segment.End);
		Point2D rightPoint = ((segment.Start.X > segment.End.X) ? segment.Start : segment.End);
		bool wasResolved = false;
		if (side == DimensionSide.Bottom)
		{
			wasResolved = TryResolveBottomProtrusionWidthSpan(outline, segment, ref leftPoint, ref rightPoint);
		}
		return new StructureEndpointSpan
		{
			FirstPoint = leftPoint,
			SecondPoint = rightPoint,
			WasResolved = wasResolved
		};
	}

	public StructureEndpointSpan ResolveVerticalStepBoundarySpan(OutlineFeature2D outline, Segment2D segment, DimensionSide side)
	{
		Point2D lowerPoint = ((segment.Start.Y <= segment.End.Y) ? segment.Start : segment.End);
		Point2D upperPoint = ((segment.Start.Y > segment.End.Y) ? segment.Start : segment.End);
		bool flag = false;
		if (TryResolveAdjacentVerticalStepEndpoint(outline, segment, lowerPoint, lowerPoint.Y, searchLower: true, out var resolved))
		{
			lowerPoint = resolved;
			flag = true;
		}
		if (TryResolveAdjacentVerticalStepEndpoint(outline, segment, upperPoint, upperPoint.Y, searchLower: false, out resolved))
		{
			upperPoint = resolved;
			flag = true;
		}
		switch (side)
		{
		case DimensionSide.Left:
			flag = TryResolveLeftProtrusionHeightSpan(outline, segment, ref lowerPoint, ref upperPoint) || flag;
			break;
		case DimensionSide.Right:
			flag = TryResolveRightProtrusionHeightSpan(outline, segment, ref lowerPoint, ref upperPoint) || flag;
			break;
		}
		return new StructureEndpointSpan
		{
			FirstPoint = lowerPoint,
			SecondPoint = upperPoint,
			WasResolved = flag
		};
	}

	public bool IsHorizontalProtrusionStep(Segment2D segment, OutlineFeature2D outline, DimensionSide side)
	{
		if (GetNearestHorizontalDimSide(segment, outline) != side)
		{
			return false;
		}
		double num = Math.Max(outline.Width * 0.35, _config.ArrowSize * 10.0);
		if (segment.LengthX > num + _config.GeometryTolerance)
		{
			return false;
		}
		return TouchesHorizontalOuterBoundary(segment, outline) && HasHorizontalStepReturn(segment, outline);
	}

	public int ScoreHorizontalProtrusionStep(Segment2D segment, OutlineFeature2D outline, DimensionSide side)
	{
		int num = 0;
		if (TouchesHorizontalOuterBoundary(segment, outline))
		{
			num += 4;
		}
		if (HasHorizontalStepReturn(segment, outline))
		{
			num += 4;
		}
		if (side == DimensionSide.Bottom)
		{
			return num + ((Math.Abs(segment.MinY - outline.MinY) <= outline.Height * 0.25) ? 2 : 0);
		}
		return num + ((Math.Abs(segment.MaxY - outline.MaxY) <= outline.Height * 0.25) ? 2 : 0);
	}

	public bool IsVerticalProtrusionStep(Segment2D segment, OutlineFeature2D outline)
	{
		if (_structureRules.IsOuterVerticalSegment(segment, outline))
		{
			return false;
		}
		double num = Math.Max(outline.Height * 0.8, _config.ArrowSize * 12.0);
		if (segment.LengthY > num + _config.GeometryTolerance)
		{
			return false;
		}
		return TouchesVerticalOuterBoundary(segment, outline) && HasVerticalStepReturn(segment, outline);
	}

	public int ScoreVerticalProtrusionStep(Segment2D segment, OutlineFeature2D outline)
	{
		int num = 0;
		if (TouchesVerticalOuterBoundary(segment, outline))
		{
			num += 4;
		}
		if (HasVerticalStepReturn(segment, outline))
		{
			num += 4;
		}
		if (GetNearestVerticalDimSide(segment, outline) == DimensionSide.Left)
		{
			return num + ((Math.Abs(segment.MinX - outline.MinX) <= outline.Width * 0.25) ? 2 : 0);
		}
		return num + ((Math.Abs(segment.MaxX - outline.MaxX) <= outline.Width * 0.25) ? 2 : 0);
	}

	public int FindLongestExtensionCandidateIndex(IList<PlannedDimension> candidates, OutlineFeature2D outline, DimensionSide side)
	{
		if (candidates == null || candidates.Count <= 1 || outline == null)
		{
			return -1;
		}
		var ranked = candidates.Select((PlannedDimension dim, int index) => new
			{
				Index = index,
				Dim = dim,
				ExtensionLength = GetExtensionLength(dim, outline, side),
				Span = GetCandidateSpan(dim, side)
			})
			.OrderByDescending(x => x.ExtensionLength)
			.ThenByDescending(x => x.Span)
			.ToList();
		double tol = _config.GeometryTolerance;
		double maxExtension = ranked[0].ExtensionLength;
		double secondExtension = ranked.Count > 1 ? ranked[1].ExtensionLength : maxExtension;
		bool hasOutlierExtension = maxExtension > secondExtension + tol;
		// All-collinear envelope partitions are body/step lengths, not extension noise.
		// In particular, do not remove shaft body 70 before 70 + 20 = overall 90 is evaluated.
		if (!hasOutlierExtension && candidates.All((PlannedDimension dim) => IsOnPlacementEnvelope(dim, outline, side)))
		{
			return -1;
		}
		// Preserve a real body length anchored at the datum corner and shoulder. Do not fall
		// through to the next ranked item: that item may be the valid outer step length.
		var longest = ranked[0];
		return IsProtectedBodyLengthCandidate(longest.Dim, outline, side)
			? -1
			: longest.Index;
	}

	public bool RemoveLongestExtensionCandidate(IList<PlannedDimension> candidates, OutlineFeature2D outline, DimensionSide side)
	{
		int num = FindLongestExtensionCandidateIndex(candidates, outline, side);
		if (num < 0)
		{
			return false;
		}
		candidates.RemoveAt(num);
		return true;
	}

	/// <summary>
	/// Left-anchored body length (touches overall MinX/MinY corner + interior shoulder) must not
	/// be discarded when a true long-extension outlier is selected for removal.
	/// </summary>
	private bool IsOnPlacementEnvelope(PlannedDimension dim, OutlineFeature2D outline, DimensionSide side)
	{
		if (dim == null || outline == null)
		{
			return false;
		}
		double tol = _config.GeometryTolerance;
		return side switch
		{
			DimensionSide.Top => Math.Abs(dim.FirstPoint.Y - outline.MaxY) <= tol
				&& Math.Abs(dim.SecondPoint.Y - outline.MaxY) <= tol,
			DimensionSide.Left => Math.Abs(dim.FirstPoint.X - outline.MinX) <= tol
				&& Math.Abs(dim.SecondPoint.X - outline.MinX) <= tol,
			DimensionSide.Right => Math.Abs(dim.FirstPoint.X - outline.MaxX) <= tol
				&& Math.Abs(dim.SecondPoint.X - outline.MaxX) <= tol,
			_ => Math.Abs(dim.FirstPoint.Y - outline.MinY) <= tol
				&& Math.Abs(dim.SecondPoint.Y - outline.MinY) <= tol,
		};
	}

	public bool IsProtectedBodyLengthCandidate(PlannedDimension dim, OutlineFeature2D outline, DimensionSide side)
	{
		if (dim == null || outline == null || outline.Segments == null)
		{
			return false;
		}
		double tol = _config.GeometryTolerance;
		bool horizontal = side == DimensionSide.Top || side == DimensionSide.Bottom;
		double span = horizontal
			? Math.Abs(dim.SecondPoint.X - dim.FirstPoint.X)
			: Math.Abs(dim.SecondPoint.Y - dim.FirstPoint.Y);
		double overall = horizontal ? outline.Width : outline.Height;
		if (span <= tol || span + tol >= overall)
		{
			return false;
		}
		Point2D a = dim.FirstPoint;
		Point2D b = dim.SecondPoint;
		// Body lengths are anchored at the datum-side overall corner (MinX / MinY).
		bool touchesStartCorner = horizontal
			? (Math.Abs(a.X - outline.MinX) <= tol || Math.Abs(b.X - outline.MinX) <= tol)
			: (Math.Abs(a.Y - outline.MinY) <= tol || Math.Abs(b.Y - outline.MinY) <= tol);
		if (!touchesStartCorner)
		{
			return false;
		}
		bool aStart = horizontal ? Math.Abs(a.X - outline.MinX) <= tol : Math.Abs(a.Y - outline.MinY) <= tol;
		Point2D interior = aStart ? b : a;
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null)
			{
				continue;
			}
			bool touches = interior.DistanceTo(segment.Start) <= tol || interior.DistanceTo(segment.End) <= tol;
			if (!touches)
			{
				continue;
			}
			if (horizontal && segment.IsVertical(tol) && segment.LengthY > tol)
			{
				return true;
			}
			if (!horizontal && segment.IsHorizontal(tol) && segment.LengthX > tol)
			{
				return true;
			}
		}
		return false;
	}

	public bool IsTooSmallStructureSpan(double span)
	{
		return span <= Math.Max(_config.GeometryTolerance, _config.TextHeight * 2.0);
	}

	public bool IsCrossAxisStructureSpanTooLarge(Point2D first, Point2D second, bool horizontal)
	{
		double num = (horizontal ? Math.Abs(second.X - first.X) : Math.Abs(second.Y - first.Y));
		double num2 = (horizontal ? Math.Abs(second.Y - first.Y) : Math.Abs(second.X - first.X));
		return num2 > num + _config.GeometryTolerance;
	}

	public Point2D GetBoundaryPoint(OutlineFeature2D outline, DimensionSide side)
	{
		if (outline == null)
		{
			throw new ArgumentNullException("outline");
		}
		if (!OutlineGeometryQuery.TryGetEnvelope(outline, _config.GeometryTolerance, out var envelope))
		{
			throw new InvalidOperationException("Unable to resolve a real outline envelope point.");
		}
		return side switch
		{
			DimensionSide.Left => envelope.LeftGrip,
			DimensionSide.Right => envelope.RightGrip,
			DimensionSide.Top => envelope.TopGrip,
			_ => envelope.BottomGrip,
		};
	}

	public bool IsBottomSideHorizontalStructureCandidate(PlannedDimension dim, OutlineFeature2D outline, IEnumerable<Point2D> ignoredPoints)
	{
		return !IsCrossAxisStructureSpanTooLarge(dim.FirstPoint, dim.SecondPoint, horizontal: true)
			&& IsCurrentBottomSideStructurePoint(dim.FirstPoint, outline, ignoredPoints)
			&& IsCurrentBottomSideStructurePoint(dim.SecondPoint, outline, ignoredPoints);
	}

	public bool IsTopSideHorizontalStructureCandidate(PlannedDimension dim, OutlineFeature2D outline, IEnumerable<Point2D> ignoredPoints)
	{
		return IsCurrentTopSideStructurePoint(dim.FirstPoint, outline, ignoredPoints) && IsCurrentTopSideStructurePoint(dim.SecondPoint, outline, ignoredPoints);
	}

	public bool IsRightSideVerticalStructureCandidate(PlannedDimension dim, OutlineFeature2D outline, IEnumerable<Point2D> ignoredPoints)
	{
		return IsCurrentRightSideStructurePoint(dim.FirstPoint, outline, ignoredPoints) && IsCurrentRightSideStructurePoint(dim.SecondPoint, outline, ignoredPoints);
	}

	public bool IsLeftSideVerticalStructureCandidate(PlannedDimension dim, OutlineFeature2D outline, IEnumerable<Point2D> ignoredPoints)
	{
		return IsCurrentLeftSideStructurePoint(dim.FirstPoint, outline, ignoredPoints) && IsCurrentLeftSideStructurePoint(dim.SecondPoint, outline, ignoredPoints);
	}

	public bool IsCurrentBottomSideStructurePoint(Point2D point, OutlineFeature2D outline, IEnumerable<Point2D> ignoredPoints)
	{
		if (IsBottomInclinedEndpointStructurePoint(point, outline, ignoredPoints))
		{
			return true;
		}
		List<Segment2D> list = (from s in outline.Segments
			where s.IsVertical(_config.GeometryTolerance)
			where !s.IsArcChord
			where s.LengthY > _config.GeometryTolerance
			where Math.Abs(s.MinX - point.X) <= _config.GeometryTolerance
			select s).ToList();
		if (list.Count == 0)
		{
			return false;
		}
		Point2D? bottomMostPoint = GetBottomMostPoint(list, ignoredPoints);
		return bottomMostPoint.HasValue && PointsEqual(bottomMostPoint.Value, point);
	}

	public bool IsCurrentTopSideStructurePoint(Point2D point, OutlineFeature2D outline, IEnumerable<Point2D> ignoredPoints)
	{
		if (IsTopInclinedEndpointStructurePoint(point, outline, ignoredPoints) || IsTopSlopeEndpointStructurePoint(point, outline, ignoredPoints))
		{
			return true;
		}
		List<Segment2D> list = (from s in outline.Segments
			where s.IsVertical(_config.GeometryTolerance)
			where !s.IsArcChord
			where s.LengthY > _config.GeometryTolerance
			where Math.Abs(s.MinX - point.X) <= _config.GeometryTolerance
			select s).ToList();
		if (list.Count == 0)
		{
			return false;
		}
		Point2D? topMostPoint = GetTopMostPoint(list, ignoredPoints);
		return topMostPoint.HasValue && PointsEqual(topMostPoint.Value, point);
	}

	public bool IsCurrentRightSideStructurePoint(Point2D point, OutlineFeature2D outline, IEnumerable<Point2D> ignoredPoints)
	{
		if (IsSideInclinedEndpointStructurePoint(point, outline, ignoredPoints, DimensionSide.Right))
		{
			return true;
		}
		List<Segment2D> list = (from s in outline.Segments
			where s.IsHorizontal(_config.GeometryTolerance)
			where !s.IsArcChord
			where s.LengthX > _config.GeometryTolerance
			where Math.Abs(s.MinY - point.Y) <= _config.GeometryTolerance
			select s).ToList();
		if (list.Count == 0)
		{
			return false;
		}
		Point2D? rightMostPoint = GetRightMostPoint(list, ignoredPoints);
		return rightMostPoint.HasValue && PointsEqual(rightMostPoint.Value, point);
	}

	public bool IsCurrentLeftSideStructurePoint(Point2D point, OutlineFeature2D outline, IEnumerable<Point2D> ignoredPoints)
	{
		if (IsSideInclinedEndpointStructurePoint(point, outline, ignoredPoints, DimensionSide.Left))
		{
			return true;
		}
		List<Segment2D> list = (from s in outline.Segments
			where s.IsHorizontal(_config.GeometryTolerance)
			where !s.IsArcChord
			where s.LengthX > _config.GeometryTolerance
			where Math.Abs(s.MinY - point.Y) <= _config.GeometryTolerance
			select s).ToList();
		if (list.Count == 0)
		{
			return false;
		}
		Point2D? leftMostPoint = GetLeftMostPoint(list, ignoredPoints);
		return leftMostPoint.HasValue && PointsEqual(leftMostPoint.Value, point);
	}

	private bool TryResolveBottomProtrusionWidthSpan(OutlineFeature2D outline, Segment2D segment, ref Point2D leftPoint, ref Point2D rightPoint)
	{
		double tolerance = _config.GeometryTolerance;
		double lowerLimit = outline.MinY + outline.Height * 0.45;
		double localWidthLimit = Math.Max(outline.Width * 0.35, _config.ArrowSize * 10.0);
		double num = Math.Abs(rightPoint.X - leftPoint.X);
		double minY = segment.MinY;
		List<Segment2D> list = (from s in outline.Segments
			where s.IsVertical(tolerance)
			where s.MinY <= lowerLimit + tolerance
			where s.MinX >= outline.MinX - tolerance
			where s.MinX <= outline.MinX + localWidthLimit + tolerance
			orderby s.MinX
			select s).ToList();
		if (list.Count < 2)
		{
			return false;
		}
		Segment2D left = list.First();
		Segment2D segment2D = (from s in list
			where s.MinX > left.MinX + tolerance
			orderby s.MinX
			select s).FirstOrDefault();
		if (segment2D == null)
		{
			return false;
		}
		double num2 = Math.Abs(segment2D.MinX - left.MinX);
		if (num2 <= tolerance || num2 > num + Math.Max(tolerance, 2.0))
		{
			return false;
		}
		leftPoint = new Point2D(left.MinX, minY);
		rightPoint = new Point2D(segment2D.MinX, minY);
		return true;
	}

	private static double GetExtensionLength(PlannedDimension dim, OutlineFeature2D outline, DimensionSide side)
	{
		return side switch
		{
			DimensionSide.Top => outline.MaxY - Math.Min(dim.FirstPoint.Y, dim.SecondPoint.Y),
			DimensionSide.Left => Math.Max(dim.FirstPoint.X, dim.SecondPoint.X) - outline.MinX,
			DimensionSide.Right => outline.MaxX - Math.Min(dim.FirstPoint.X, dim.SecondPoint.X),
			_ => Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y) - outline.MinY,
		};
	}

	private static double GetCandidateSpan(PlannedDimension dim, DimensionSide side)
	{
		return (side == DimensionSide.Bottom || side == DimensionSide.Top) ? Math.Abs(dim.SecondPoint.X - dim.FirstPoint.X) : Math.Abs(dim.SecondPoint.Y - dim.FirstPoint.Y);
	}

	private bool IsBottomInclinedEndpointStructurePoint(Point2D point, OutlineFeature2D outline, IEnumerable<Point2D> ignoredPoints)
	{
		if (ContainsPoint(ignoredPoints, point) || _structureRules.IsEnvelopeSidePoint(point, outline))
		{
			return false;
		}
		return (from s in (from s in outline.Segments
				where !s.IsHorizontal(_config.GeometryTolerance)
				where !s.IsVertical(_config.GeometryTolerance)
				select s).Where(_structureRules.IsFortyFiveDegreeSegment)
			where _structureRules.IsInnerGrooveChamferSegment(s, outline, isTopSide: false)
			select s).Any((Segment2D s) => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
	}

	private bool IsTopInclinedEndpointStructurePoint(Point2D point, OutlineFeature2D outline, IEnumerable<Point2D> ignoredPoints)
	{
		if (ContainsPoint(ignoredPoints, point) || _structureRules.IsEnvelopeSidePoint(point, outline))
		{
			return false;
		}
		return (from s in (from s in outline.Segments
				where !s.IsHorizontal(_config.GeometryTolerance)
				where !s.IsVertical(_config.GeometryTolerance)
				select s).Where(_structureRules.IsFortyFiveDegreeSegment)
			where _structureRules.IsInnerGrooveChamferSegment(s, outline, isTopSide: true)
			select s).Any((Segment2D s) => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
	}

	private bool IsSideInclinedEndpointStructurePoint(Point2D point, OutlineFeature2D outline, IEnumerable<Point2D> ignoredPoints, DimensionSide side)
	{
		if (ContainsPoint(ignoredPoints, point) || _structureRules.IsEnvelopeHorizontalSidePoint(point, outline))
		{
			return false;
		}
		return (from s in (from s in outline.Segments
				where !s.IsHorizontal(_config.GeometryTolerance)
				where !s.IsVertical(_config.GeometryTolerance)
				select s).Where(_structureRules.IsFortyFiveDegreeSegment)
			where _structureRules.IsSideInnerGrooveChamferSegment(s, outline, side)
			select s).Any((Segment2D s) => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
	}

	private bool IsTopSlopeEndpointStructurePoint(Point2D point, OutlineFeature2D outline, IEnumerable<Point2D> ignoredPoints)
	{
		if (ContainsPoint(ignoredPoints, point) || _structureRules.IsEnvelopeSidePoint(point, outline))
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

	private bool IsTopSlopeEndpointStructurePoint(Point2D low, Point2D high, Segment2D slope, OutlineFeature2D outline, IEnumerable<Point2D> ignoredPoints)
	{
		if (ContainsPoint(ignoredPoints, low) || _structureRules.IsEnvelopeSidePoint(low, outline) || high.Y <= low.Y + _config.GeometryTolerance)
		{
			return false;
		}
		return EndpointConnectsHorizontalSegment(low, slope, outline) && EndpointConnectsHigherHorizontalSegment(high, low.Y, slope, outline);
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

	private Point2D? GetBottomMostPoint(IEnumerable<Segment2D> segments, IEnumerable<Point2D> ignoredPoints)
	{
		return ((IEnumerable<Point2D>)(from p in segments.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			where !ContainsPoint(ignoredPoints, p)
			orderby p.Y, p.X
			select p)).Select((Func<Point2D, Point2D?>)((Point2D p) => p)).FirstOrDefault();
	}

	private Point2D? GetTopMostPoint(IEnumerable<Segment2D> segments, IEnumerable<Point2D> ignoredPoints)
	{
		return ((IEnumerable<Point2D>)(from p in segments.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			where !ContainsPoint(ignoredPoints, p)
			orderby p.Y descending, p.X
			select p)).Select((Func<Point2D, Point2D?>)((Point2D p) => p)).FirstOrDefault();
	}

	private Point2D? GetRightMostPoint(IEnumerable<Segment2D> segments, IEnumerable<Point2D> ignoredPoints)
	{
		return ((IEnumerable<Point2D>)(from p in segments.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			where !ContainsPoint(ignoredPoints, p)
			orderby p.X descending, p.Y
			select p)).Select((Func<Point2D, Point2D?>)((Point2D p) => p)).FirstOrDefault();
	}

	private Point2D? GetLeftMostPoint(IEnumerable<Segment2D> segments, IEnumerable<Point2D> ignoredPoints)
	{
		return ((IEnumerable<Point2D>)(from p in segments.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			where !ContainsPoint(ignoredPoints, p)
			orderby p.X, p.Y
			select p)).Select((Func<Point2D, Point2D?>)((Point2D p) => p)).FirstOrDefault();
	}

	private bool TryResolveLeftProtrusionHeightSpan(OutlineFeature2D outline, Segment2D segment, ref Point2D lowerPoint, ref Point2D upperPoint)
	{
		double tolerance = _config.GeometryTolerance;
		double leftLimit = outline.MinX + outline.Width * 0.45;
		double topLimit = outline.MinY + outline.Height * 0.7;
		double num = Math.Abs(upperPoint.Y - lowerPoint.Y);
		double lowerY = lowerPoint.Y;
		Segment2D segment2D = (from s in outline.Segments
			where s.IsHorizontal(tolerance)
			where s.LengthX <= Math.Max(outline.Width * 0.35, _config.ArrowSize * 10.0) + tolerance
			where s.MinY <= lowerY + Math.Max(tolerance, 0.2)
			where s.MinX <= leftLimit
			orderby s.MinY, Math.Abs(s.MinX - outline.MinX)
			select s).FirstOrDefault();
		Segment2D segment2D2 = (from s in outline.Segments
			where s.IsHorizontal(tolerance)
			where s.MinX <= leftLimit
			where s.MinY > lowerY + tolerance
			where s.MinY <= topLimit + tolerance
			orderby s.MinY descending, Math.Abs(s.MinX - segment.MinX)
			select s).FirstOrDefault();
		if (segment2D == null || segment2D2 == null)
		{
			return false;
		}
		Point2D point2D = ((Math.Abs(segment2D.Start.X - segment.MinX) <= Math.Abs(segment2D.End.X - segment.MinX)) ? segment2D.Start : segment2D.End);
		Point2D point2D2 = ((Math.Abs(segment2D2.Start.X - segment.MinX) <= Math.Abs(segment2D2.End.X - segment.MinX)) ? segment2D2.Start : segment2D2.End);
		double num2 = Math.Abs(point2D2.Y - point2D.Y);
		if (num2 <= num + Math.Max(tolerance, 0.2))
		{
			return false;
		}
		lowerPoint = point2D;
		upperPoint = point2D2;
		return true;
	}

	private bool TryResolveRightProtrusionHeightSpan(OutlineFeature2D outline, Segment2D segment, ref Point2D lowerPoint, ref Point2D upperPoint)
	{
		double tolerance = _config.GeometryTolerance;
		double rightLimit = outline.MaxX - outline.Width * 0.45;
		double num = Math.Abs(upperPoint.Y - lowerPoint.Y);
		List<Point2D> list = new List<Point2D> { segment.Start, segment.End };
		double minX = segment.MinX;
		double num2 = Math.Max(tolerance, _config.ArrowSize * 2.0);
		foreach (Segment2D segment2 in outline.Segments)
		{
			if (segment2 != segment && !(segment2.MinX < rightLimit - tolerance) && (Math.Abs(segment2.MinX - minX) <= num2 || Math.Abs(segment2.MaxX - minX) <= num2 || SegmentTouchesPoint(segment2, segment.Start) || SegmentTouchesPoint(segment2, segment.End)))
			{
				list.Add(segment2.Start);
				list.Add(segment2.End);
			}
		}
		foreach (ChamferFeature2D chamfer in outline.Chamfers)
		{
			if ((!(chamfer.StartPoint.X < rightLimit - tolerance) || !(chamfer.EndPoint.X < rightLimit - tolerance)) && (Math.Abs(chamfer.StartPoint.X - minX) <= num2 || Math.Abs(chamfer.EndPoint.X - minX) <= num2 || PointsEqual(chamfer.StartPoint, segment.Start) || PointsEqual(chamfer.EndPoint, segment.Start) || PointsEqual(chamfer.StartPoint, segment.End) || PointsEqual(chamfer.EndPoint, segment.End)))
			{
				list.Add(chamfer.StartPoint);
				list.Add(chamfer.EndPoint);
			}
		}
		double num3 = list.Min((Point2D p) => p.Y);
		double maxY = list.Max((Point2D p) => p.Y);
		Segment2D segment2D = (from s in outline.Segments
			where s.IsHorizontal(tolerance)
			where s.MaxX >= rightLimit - tolerance
			where s.MinY >= maxY - tolerance
			orderby s.MinY
			select s).FirstOrDefault();
		if (segment2D != null)
		{
			maxY = Math.Max(maxY, segment2D.MinY);
		}
		double num4 = maxY - num3;
		if (num4 <= num + Math.Max(tolerance, 0.2))
		{
			return false;
		}
		double minX2 = segment.MinX;
		lowerPoint = new Point2D(minX2, num3);
		upperPoint = new Point2D(minX2, maxY);
		return true;
	}

	private bool TryResolveAdjacentVerticalStepEndpoint(OutlineFeature2D outline, Segment2D segment, Point2D endpoint, double originalY, bool searchLower, out Point2D resolved)
	{
		resolved = endpoint;
		List<Point2D> list = new List<Point2D>();
		foreach (Segment2D segment2 in outline.Segments)
		{
			if (segment2 != segment)
			{
				if (PointsEqual(segment2.Start, endpoint))
				{
					list.Add(segment2.End);
				}
				else if (PointsEqual(segment2.End, endpoint))
				{
					list.Add(segment2.Start);
				}
			}
		}
		foreach (Arc2D arc in outline.Arcs)
		{
			if (PointsEqual(arc.Start, endpoint))
			{
				list.Add(arc.End);
			}
			else if (PointsEqual(arc.End, endpoint))
			{
				list.Add(arc.Start);
			}
		}
		foreach (FilletFeature2D fillet in outline.Fillets)
		{
			if (PointsEqual(fillet.StartPoint, endpoint))
			{
				list.Add(fillet.EndPoint);
			}
			else if (PointsEqual(fillet.EndPoint, endpoint))
			{
				list.Add(fillet.StartPoint);
			}
		}
		double maxLocalExtension = Math.Max(segment.LengthY * 0.25, _config.ArrowSize * 8.0);
		var anon = (from p in list
			where searchLower ? (p.Y < originalY - _config.GeometryTolerance) : (p.Y > originalY + _config.GeometryTolerance)
			select new
			{
				Point = p,
				Delta = Math.Abs(p.Y - originalY)
			} into x
			where x.Delta <= maxLocalExtension + _config.GeometryTolerance
			orderby x.Delta descending
			select x).FirstOrDefault();
		if (anon == null)
		{
			return false;
		}
		resolved = anon.Point;
		return true;
	}

	private bool TouchesHorizontalOuterBoundary(Segment2D segment, OutlineFeature2D outline)
	{
		double num = Math.Max(_config.GeometryTolerance, 0.2);
		return Math.Abs(segment.MinX - outline.MinX) <= num || Math.Abs(segment.MaxX - outline.MaxX) <= num || EndpointConnectsToBoundary(segment.Start, outline, horizontalBoundary: true) || EndpointConnectsToBoundary(segment.End, outline, horizontalBoundary: true);
	}

	private bool HasHorizontalStepReturn(Segment2D segment, OutlineFeature2D outline)
	{
		return EndpointHasStructuralReturn(segment.Start, segment, outline) || EndpointHasStructuralReturn(segment.End, segment, outline);
	}

	private bool TouchesVerticalOuterBoundary(Segment2D segment, OutlineFeature2D outline)
	{
		double num = Math.Max(_config.GeometryTolerance, 0.2);
		return Math.Abs(segment.MinY - outline.MinY) <= num || Math.Abs(segment.MaxY - outline.MaxY) <= num || EndpointConnectsToBoundary(segment.Start, outline, horizontalBoundary: false) || EndpointConnectsToBoundary(segment.End, outline, horizontalBoundary: false);
	}

	private bool HasVerticalStepReturn(Segment2D segment, OutlineFeature2D outline)
	{
		return EndpointHasStructuralReturn(segment.Start, segment, outline) || EndpointHasStructuralReturn(segment.End, segment, outline);
	}

	private bool EndpointHasStructuralReturn(Point2D endpoint, Segment2D source, OutlineFeature2D outline)
	{
		if (outline.Segments.Any((Segment2D s) => s != source && SegmentTouchesPoint(s, endpoint) && !s.IsHorizontal(_config.GeometryTolerance)))
		{
			return true;
		}
		if (outline.Chamfers.Any((ChamferFeature2D chamfer) => PointsEqual(chamfer.StartPoint, endpoint) || PointsEqual(chamfer.EndPoint, endpoint)))
		{
			return true;
		}
		return outline.Fillets.Any((FilletFeature2D fillet) => PointsEqual(fillet.StartPoint, endpoint) || PointsEqual(fillet.EndPoint, endpoint));
	}

	private bool EndpointConnectsToBoundary(Point2D endpoint, OutlineFeature2D outline, bool horizontalBoundary)
	{
		foreach (Segment2D segment in outline.Segments)
		{
			if (!SegmentTouchesPoint(segment, endpoint))
			{
				continue;
			}
			if (horizontalBoundary)
			{
				if (Math.Abs(segment.MinX - outline.MinX) <= _config.GeometryTolerance || Math.Abs(segment.MaxX - outline.MaxX) <= _config.GeometryTolerance)
				{
					return true;
				}
			}
			else if (Math.Abs(segment.MinY - outline.MinY) <= _config.GeometryTolerance || Math.Abs(segment.MaxY - outline.MaxY) <= _config.GeometryTolerance)
			{
				return true;
			}
		}
		return false;
	}

	private DimensionSide GetNearestHorizontalDimSide(Segment2D segment, OutlineFeature2D outline)
	{
		double num = Math.Abs(segment.MinY - outline.MinY);
		double num2 = Math.Abs(outline.MaxY - segment.MaxY);
		return (!(num <= num2)) ? DimensionSide.Top : DimensionSide.Bottom;
	}

	private DimensionSide GetNearestVerticalDimSide(Segment2D segment, OutlineFeature2D outline)
	{
		double num = Math.Abs(segment.MinX - outline.MinX);
		double num2 = Math.Abs(outline.MaxX - segment.MaxX);
		return (num <= num2) ? DimensionSide.Left : DimensionSide.Right;
	}

	private bool SegmentTouchesPoint(Segment2D segment, Point2D point)
	{
		return PointsEqual(segment.Start, point) || PointsEqual(segment.End, point);
	}

	private bool ContainsPoint(IEnumerable<Point2D> points, Point2D point)
	{
		return points?.Any((Point2D p) => PointsEqual(p, point)) ?? false;
	}

	private bool PointsEqual(Point2D first, Point2D second)
	{
		return first.DistanceTo(second) <= Math.Max(_config.GeometryTolerance, 0.2);
	}
}
