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
	private void AddOverallWidth(DimensionPlan plan, OutlineFeature2D outline)
	{
		if (!(outline.Width <= _config.GeometryTolerance))
		{
			plan.Add(new PlannedDimension
			{
				Kind = DimensionKind.OverallWidth,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = GetLeftBoundaryPoint(outline),
				SecondPoint = GetRightBoundaryPoint(outline),
				ForceOuterLevel = true,
				ReadingLevel = DimensionReadingLevel.Overall,
				DebugRole = "OverallWidth"
			});
		}
	}

	private void AddOverallHeight(DimensionPlan plan, OutlineFeature2D outline)
	{
		if (!(outline.Height <= _config.GeometryTolerance))
		{
			plan.Add(new PlannedDimension
			{
				Kind = DimensionKind.OverallHeight,
				Orientation = DimensionOrientation.Vertical,
				Side = DimensionSide.Left,
				FirstPoint = GetBottomBoundaryPoint(outline),
				SecondPoint = GetTopBoundaryPoint(outline),
				ForceOuterLevel = true,
				ReadingLevel = DimensionReadingLevel.Overall,
				DebugRole = "OverallHeight"
			});
		}
	}

	private void AddLinearSegmentDimensions(DimensionPlan plan, OutlineFeature2D outline)
	{
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.Length <= _config.GeometryTolerance || segment.IsArcChord || IsOverallBoundarySegment(segment, outline))
			{
				continue;
			}
			if (segment.IsHorizontal(_config.GeometryTolerance))
			{
				PlannedDimension dimension = new PlannedDimension
				{
					Kind = DimensionKind.Normal,
					Orientation = DimensionOrientation.Horizontal,
					Side = ResolveHorizontalOutlineSegmentSide(segment, outline),
					FirstPoint = segment.Start,
					SecondPoint = segment.End,
					SourceKey = segment.SourceKey,
					DebugRole = "OutlineSegment"
				};
				if (IsSuppressedByCornerFeature(segment, outline))
				{
					AddSuppressedDimension(plan, dimension, "SuppressedByCornerFeature");
				}
				else
				{
					plan.Add(dimension);
				}
			}
			else if (segment.IsVertical(_config.GeometryTolerance))
			{
				PlannedDimension dimension2 = new PlannedDimension
				{
					Kind = DimensionKind.Normal,
					Orientation = DimensionOrientation.Vertical,
					Side = ResolveVerticalOutlineSegmentSide(segment, outline),
					FirstPoint = segment.Start,
					SecondPoint = segment.End,
					SourceKey = segment.SourceKey,
					DebugRole = "OutlineSegment"
				};
				if (IsSuppressedByCornerFeature(segment, outline))
				{
					AddSuppressedDimension(plan, dimension2, "SuppressedByCornerFeature");
				}
				else
				{
					plan.Add(dimension2);
				}
			}
		}
	}

	/// <summary>
	/// Horizontal OS side: only MaxY outer edges use Top; MinY uses Bottom; interior edges
	/// pick the free (outside) normal so dimension lines do not cross the solid.
	/// </summary>
	internal DimensionSide ResolveHorizontalOutlineSegmentSide(Segment2D segment, OutlineFeature2D outline)
	{
		double tol = _config.GeometryTolerance;
		double y = (segment.Start.Y + segment.End.Y) * 0.5;
		if (Math.Abs(y - outline.MinY) <= tol)
		{
			return DimensionSide.Bottom;
		}
		if (Math.Abs(y - outline.MaxY) <= tol)
		{
			return DimensionSide.Top;
		}
		double midX = (segment.MinX + segment.MaxX) * 0.5;
		double probe = Math.Max(tol * 4.0, Math.Min(outline.Height * 0.05, 1.0));
		bool aboveInside = _structureSuppressionRules.IsPointInsideOutlineByRayCast(midX, y + probe, outline);
		bool belowInside = _structureSuppressionRules.IsPointInsideOutlineByRayCast(midX, y - probe, outline);
		if (aboveInside && !belowInside)
		{
			return DimensionSide.Bottom;
		}
		if (belowInside && !aboveInside)
		{
			return DimensionSide.Top;
		}
		// Both outside/inside: prefer the nearer outer envelope.
		return (Math.Abs(y - outline.MinY) <= Math.Abs(outline.MaxY - y)) ? DimensionSide.Bottom : DimensionSide.Top;
	}

	/// <summary>
	/// Vertical OS side: MinX/MaxX outer edges keep Left/Right; interior edges use free normal.
	/// </summary>
	internal DimensionSide ResolveVerticalOutlineSegmentSide(Segment2D segment, OutlineFeature2D outline)
	{
		double tol = _config.GeometryTolerance;
		double x = (segment.Start.X + segment.End.X) * 0.5;
		if (Math.Abs(x - outline.MinX) <= tol)
		{
			return DimensionSide.Left;
		}
		if (Math.Abs(x - outline.MaxX) <= tol)
		{
			return DimensionSide.Right;
		}
		double midY = (segment.MinY + segment.MaxY) * 0.5;
		double probe = Math.Max(tol * 4.0, Math.Min(outline.Width * 0.05, 1.0));
		bool rightInside = _structureSuppressionRules.IsPointInsideOutlineByRayCast(x + probe, midY, outline);
		bool leftInside = _structureSuppressionRules.IsPointInsideOutlineByRayCast(x - probe, midY, outline);
		if (rightInside && !leftInside)
		{
			return DimensionSide.Left;
		}
		if (leftInside && !rightInside)
		{
			return DimensionSide.Right;
		}
		return (Math.Abs(x - outline.MinX) <= Math.Abs(outline.MaxX - x)) ? DimensionSide.Left : DimensionSide.Right;
	}

	private void AddStepOutlineDimensions(DimensionPlan plan, OutlineFeature2D outline)
	{
		AddHorizontalStructureDimensions(plan, outline, BuildTopStructureWidthDimensions(outline, plan));
		AddHorizontalStructureDimensions(plan, outline, BuildBottomStructureWidthDimensions(outline, plan));
		AddVerticalStructureDimensions(plan, outline, BuildLeftStructureHeightDimensions(outline, plan));
		AddDatumRootedLeftOuterStepDimension(plan, outline);
		AddVerticalStructureDimensions(plan, outline, BuildRightStructureHeightDimensions(outline, plan));
		AddChamferedTopStepDimensions(plan, outline);
	}

	private void AddDatumRootedLeftOuterStepDimension(DimensionPlan plan, OutlineFeature2D outline)
	{
		double tol = _config.GeometryTolerance;
		Segment2D ledge = outline.Segments
			.Where((Segment2D segment) => segment != null
				&& !segment.IsArcChord
				&& segment.IsHorizontal(tol)
				&& Math.Abs(segment.MinX - outline.MinX) <= tol
				&& segment.MinY > outline.MinY + tol
				&& segment.MinY < outline.MaxY - tol
				&& segment.MaxX < outline.MaxX - tol
				&& HasContinuousStraightOutlineEdge(outline, false, outline.MinX, outline.MinY, segment.MinY)
				&& outline.Segments.Any((Segment2D orthogonal) => orthogonal != null
					&& !orthogonal.IsArcChord
					&& orthogonal.IsVertical(tol)
					&& (PointsEqual(orthogonal.Start, new Point2D(segment.MaxX, segment.MinY))
						|| PointsEqual(orthogonal.End, new Point2D(segment.MaxX, segment.MinY)))))
			.OrderBy((Segment2D segment) => segment.MinY)
			.FirstOrDefault();
		if (ledge == null)
		{
			return;
		}
		Point2D first = new Point2D(outline.MinX, outline.MinY);
		Point2D second = new Point2D(outline.MinX, ledge.MinY);
		if (plan.Dimensions.Any((PlannedDimension dimension) =>
			dimension.Orientation == DimensionOrientation.Vertical
			&& dimension.Side == DimensionSide.Left
			&& Math.Abs(Math.Min(dimension.FirstPoint.Y, dimension.SecondPoint.Y) - first.Y) <= tol
			&& Math.Abs(Math.Max(dimension.FirstPoint.Y, dimension.SecondPoint.Y) - second.Y) <= tol))
		{
			return;
		}
		AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Vertical, DimensionSide.Left,
			first, second, string.Empty, "LeftStructHeight",
			alignmentKey: GetStructureAlignmentKey(DimensionSide.Left, horizontal: false),
			alignmentPriority: 70, readingLevel: DimensionReadingLevel.LocalSpacing);
		PlannedDimension outerStep = plan.Dimensions.Last();
		for (int i = plan.Dimensions.Count - 2; i >= 0; i--)
		{
			PlannedDimension candidate = plan.Dimensions[i];
			if (!IsLeftStructureHeight(candidate)
				|| !_dimensionDeduplicationRules.IsVerticalStructureIntervalCoveredByOther(
					ToDeduplicationItem(candidate), ToDeduplicationItem(outerStep)))
			{
				continue;
			}
			plan.MarkSuppressed(candidate, SuppressReason.LeftStructureHeightCoveredByDatumRootedOuterStep);
			plan.Dimensions.RemoveAt(i);
		}
	}

	private void AddChamferedTopStepDimensions(DimensionPlan plan, OutlineFeature2D outline)
	{
		if (plan == null || outline == null || outline.Chamfers.Count < 2)
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		List<Segment2D> horizontalSegments = outline.Segments
			.Where((Segment2D segment) => segment != null && segment.IsHorizontal(tol) && segment.LengthX > tol)
			.ToList();
		foreach (Segment2D lowerEdge in horizontalSegments)
		{
			Point2D lowerLeft = lowerEdge.Start.X <= lowerEdge.End.X ? lowerEdge.Start : lowerEdge.End;
			Point2D lowerRight = lowerEdge.Start.X <= lowerEdge.End.X ? lowerEdge.End : lowerEdge.Start;
			Segment2D upperEdge = horizontalSegments.FirstOrDefault((Segment2D segment) => segment != lowerEdge
				&& Math.Abs(segment.MinX - lowerEdge.MinX) <= tol
				&& Math.Abs(segment.MaxX - lowerEdge.MaxX) <= tol
				&& Math.Abs(segment.MinY - outline.MaxY) <= tol
				&& segment.MinY > lowerEdge.MinY + tol);
			if (upperEdge == null || !HasVerticalSegmentTouching(outline, lowerEdge, lowerLeft)
				|| !HasVerticalSegmentTouching(outline, lowerEdge, lowerRight))
			{
				continue;
			}
			Point2D upperLeft = upperEdge.Start.X <= upperEdge.End.X ? upperEdge.Start : upperEdge.End;
			Point2D upperRight = upperEdge.Start.X <= upperEdge.End.X ? upperEdge.End : upperEdge.Start;
			if (!TouchesChamferEndpoint(outline, upperLeft) || !TouchesChamferEndpoint(outline, upperRight))
			{
				continue;
			}
			AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Top,
				lowerLeft, lowerRight, string.Empty, "TopChamferedStepWidth");
			AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Vertical, DimensionSide.Right,
				lowerRight, upperRight, string.Empty, "RightChamferedStepHeight");
		}
	}

	private bool HasVerticalSegmentTouching(OutlineFeature2D outline, Segment2D edge, Point2D point)
	{
		return outline.Segments.Any((Segment2D segment) => segment != edge
			&& segment.IsVertical(_config.GeometryTolerance)
			&& Touches(segment, point));
	}

	private bool TouchesChamferEndpoint(OutlineFeature2D outline, Point2D point)
	{
		return outline.Chamfers.Any((ChamferFeature2D chamfer) => PointsEqual(chamfer.StartPoint, point) || PointsEqual(chamfer.EndPoint, point));
	}

	private void AddHorizontalStructureDimensions(DimensionPlan plan, OutlineFeature2D outline, IList<PlannedDimension> dimensions)
	{
		foreach (PlannedDimension dimension in dimensions)
		{
			plan.Add(dimension);
		}
	}

	private void AddHorizontalStructureDimensions(DimensionPlan plan, OutlineFeature2D outline, IList<Point2D> points, DimensionSide side, string debugRole)
	{
		if (points.Count < 2)
		{
			return;
		}
		string alignmentKey = GetStructureAlignmentKey(side, horizontal: true);
		for (int i = 1; i < points.Count; i++)
		{
			Point2D point2D = points[i - 1];
			Point2D point2D2 = points[i];
			double num = Math.Abs(point2D2.X - point2D.X);
			if (!IsTooSmallStructureSpan(num) && !(Math.Abs(num - outline.Width) <= _config.GeometryTolerance) && !IsCrossAxisStructureSpanTooLarge(point2D, point2D2, horizontal: true))
			{
				AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Horizontal, side, point2D, point2D2, string.Empty, debugRole, alignmentKey: alignmentKey, alignmentPriority: 70, readingLevel: DimensionReadingLevel.LocalSpacing);
			}
		}
	}

	internal IList<PlannedDimension> BuildTopStructureWidthDimensions(OutlineFeature2D outline, DimensionPlan plan = null)
	{
		List<IgnoredPoint> ignoredPoints = new List<IgnoredPoint>();
		List<PlannedDimension> list2;
		while (true)
		{
			List<StructurePoint> source = BuildTopSideStructurePoints(outline, ignoredPoints);
			List<Point2D> list = source.Select((StructurePoint p) => p.Point).ToList();
			if (list.Count < 2)
			{
				return new List<PlannedDimension>();
			}
			list2 = BuildHorizontalWidthCandidates(list, DimensionSide.Top, "TopStructWidth");
			List<IgnoredPoint> topExtensionIgnoredPoints = GetTopExtensionIgnoredPoints(list2, outline);
			if (topExtensionIgnoredPoints.Count == 0)
			{
				break;
			}
			if (!AddIgnoredPointMetadata(ignoredPoints, topExtensionIgnoredPoints))
			{
				return new List<PlannedDimension>();
			}
		}
		List<PlannedDimension> snapshot = new List<PlannedDimension>(list2);
		RemoveLongestTopExtensionCandidate(list2, outline);
		RecordDiscardedCandidates(plan, snapshot, list2, "LongestExtensionCandidate");
		SnapComplementaryHorizontalRemainderEndpoints(list2, outline);
		snapshot = new List<PlannedDimension>(list2);
		RemoveComplementaryOverallRemainderCandidates(list2, outline.MinX, outline.MaxX, horizontal: true);
		RecordDiscardedCandidates(plan, snapshot, list2, "ComplementaryOverallRemainder");
		List<PlannedDimension> kept = list2.Where((PlannedDimension dim) => IsTopSideHorizontalStructureCandidate(dim, outline, ignoredPoints)).ToList();
		RecordDiscardedCandidates(plan, list2, kept, "NotTopSideStructureCandidate");
		return kept;
	}

	private List<PlannedDimension> BuildHorizontalWidthCandidates(IList<Point2D> points, DimensionSide side, string debugRole)
	{
		List<PlannedDimension> list = new List<PlannedDimension>();
		string alignmentKey = GetStructureAlignmentKey(side, horizontal: true);
		for (int i = 1; i < points.Count; i++)
		{
			Point2D firstPoint = points[i - 1];
			Point2D secondPoint = points[i];
			double num = Math.Abs(secondPoint.X - firstPoint.X);
			if (!(num <= _config.GeometryTolerance))
			{
				list.Add(new PlannedDimension
				{
					Kind = DimensionKind.Normal,
					Orientation = DimensionOrientation.Horizontal,
					Side = side,
					FirstPoint = firstPoint,
					SecondPoint = secondPoint,
					OverrideText = string.Empty,
					DebugRole = (debugRole ?? string.Empty),
					AlignmentKey = alignmentKey,
					AlignmentPriority = 70,
					ReadingLevel = DimensionReadingLevel.LocalSpacing
				});
			}
		}
		return list;
	}

	private void AddVerticalStructureDimensions(DimensionPlan plan, OutlineFeature2D outline, IList<PlannedDimension> dimensions)
	{
		foreach (PlannedDimension dimension in dimensions)
		{
			plan.Add(dimension);
		}
	}

	private void AddVerticalStructureDimensions(DimensionPlan plan, OutlineFeature2D outline, IList<Point2D> points, DimensionSide side, string debugRole)
	{
		if (points.Count < 2)
		{
			return;
		}
		string alignmentKey = GetStructureAlignmentKey(side, horizontal: false);
		for (int i = 1; i < points.Count; i++)
		{
			Point2D point2D = points[i - 1];
			Point2D point2D2 = points[i];
			double num = Math.Abs(point2D2.Y - point2D.Y);
			if (!IsTooSmallStructureSpan(num) && !(Math.Abs(num - outline.Height) <= _config.GeometryTolerance) && !IsCrossAxisStructureSpanTooLarge(point2D, point2D2, horizontal: false))
			{
				AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Vertical, side, point2D, point2D2, string.Empty, debugRole, alignmentKey: alignmentKey, alignmentPriority: 70, readingLevel: DimensionReadingLevel.LocalSpacing);
			}
		}
	}

	internal IList<PlannedDimension> BuildLeftStructureHeightDimensions(OutlineFeature2D outline, DimensionPlan plan = null)
	{
		List<IgnoredPoint> ignoredPoints = new List<IgnoredPoint>();
		List<PlannedDimension> list2;
		while (true)
		{
			List<Point2D> list = BuildLeftSideVerticalStructurePoints(outline, ignoredPoints);
			if (list.Count < 2)
			{
				return new List<PlannedDimension>();
			}
			list2 = BuildVerticalHeightCandidates(list, DimensionSide.Left, "LeftStructHeight");
			List<IgnoredPoint> leftExtensionCrossingPoints = GetLeftExtensionCrossingPoints(list2, outline);
			if (leftExtensionCrossingPoints.Count == 0)
			{
				break;
			}
			if (!AddIgnoredPointMetadata(ignoredPoints, leftExtensionCrossingPoints))
			{
				return new List<PlannedDimension>();
			}
		}
		List<PlannedDimension> snapshot = new List<PlannedDimension>(list2);
		RemoveLongestLeftExtensionCandidate(list2, outline);
		RecordDiscardedCandidates(plan, snapshot, list2, "LongestExtensionCandidate");
		// Preserve a real partial MinX edge (production LeftStructHeight 20). Its projected
		// complementary residual is removed later with full outline topology available.
		SnapComplementaryVerticalRemainderEndpoints(list2, outline);
		snapshot = new List<PlannedDimension>(list2);
		RemoveComplementaryOverallRemainderCandidates(
			list2,
			outline.MinY,
			outline.MaxY,
			horizontal: false,
			preserveCandidate: (PlannedDimension candidate) => IsRealPartialEnvelopeStructureHeight(candidate, outline, _config.GeometryTolerance));
		RecordDiscardedCandidates(plan, snapshot, list2, "ComplementaryOverallRemainder");
		// LEGACY-SIDE-PATCH (v19–v23): WCS left notch recovery — do not extend (Phase B feature-first).
		HashSet<PlannedDimension> keptByContour = new HashSet<PlannedDimension>();
		SnapCrossLevelVerticalCandidatesOntoSideFacingEdges(list2, outline, DimensionSide.Left, keptByContour);
		AddSideFacingVerticalContourEdgeHeights(list2, outline, DimensionSide.Left, "LeftStructHeight", keptByContour);
		PreferContourBackedVerticalStructureOverOverlappingProjection(list2, outline, DimensionSide.Left);
		AbsorbMicroVerticalStructureSpans(list2, outline, keptByContour);
		BridgeSmallChamferGapsInVerticalStructure(list2, outline, keptByContour);
		DeduplicateSameIntervalVerticalStructure(list2, keptByContour);
		List<PlannedDimension> kept = list2
			.Where((PlannedDimension dim) => IsLeftSideVerticalStructureCandidate(dim, outline, ignoredPoints)
				|| keptByContour.Contains(dim)
				|| IsInsetContourBackedVerticalStructureEdge(dim, outline, DimensionSide.Left))
			.ToList();
		RecordDiscardedCandidates(plan, list2, kept, "NotLeftSideStructureCandidate");
		return kept;
	}

	internal IList<PlannedDimension> BuildRightStructureHeightDimensions(OutlineFeature2D outline, DimensionPlan plan = null)
	{
		List<IgnoredPoint> ignoredPoints = new List<IgnoredPoint>();
		List<PlannedDimension> list2;
		while (true)
		{
			List<Point2D> list = BuildRightSideVerticalStructurePoints(outline, ignoredPoints);
			if (list.Count < 2)
			{
				return new List<PlannedDimension>();
			}
			list2 = BuildVerticalHeightCandidates(list, DimensionSide.Right, "RightStructHeight");
			List<IgnoredPoint> rightExtensionCrossingPoints = GetRightExtensionCrossingPoints(list2, outline);
			if (rightExtensionCrossingPoints.Count == 0)
			{
				break;
			}
			if (!AddIgnoredPointMetadata(ignoredPoints, rightExtensionCrossingPoints))
			{
				return new List<PlannedDimension>();
			}
		}
		List<PlannedDimension> snapshot = new List<PlannedDimension>(list2);
		RemoveLongestRightExtensionCandidate(list2, outline);
		RecordDiscardedCandidates(plan, snapshot, list2, "LongestExtensionCandidate");
		SnapComplementaryVerticalRemainderEndpoints(list2, outline);
		snapshot = new List<PlannedDimension>(list2);
		// Preserve a real partial MaxX edge (the upper rectangle). Its projected
		// complementary residual is removed, not the real right-side structure height.
		RemoveComplementaryOverallRemainderCandidates(
			list2,
			outline.MinY,
			outline.MaxY,
			horizontal: false,
			preserveCandidate: (PlannedDimension candidate) => IsRealPartialEnvelopeStructureHeight(candidate, outline, _config.GeometryTolerance)
				|| IsRightTopPartialEnvelopeStructureHeight(candidate, outline, _config.GeometryTolerance));
		RecordDiscardedCandidates(plan, snapshot, list2, "ComplementaryOverallRemainder");
		// LEGACY-SIDE-PATCH (v19–v23): WCS right dual of left notch recovery — do not extend.
		HashSet<PlannedDimension> keptByContour = new HashSet<PlannedDimension>();
		SnapCrossLevelVerticalCandidatesOntoSideFacingEdges(list2, outline, DimensionSide.Right, keptByContour);
		AddSideFacingVerticalContourEdgeHeights(list2, outline, DimensionSide.Right, "RightStructHeight", keptByContour);
		PreferContourBackedVerticalStructureOverOverlappingProjection(list2, outline, DimensionSide.Right);
		AbsorbMicroVerticalStructureSpans(list2, outline, keptByContour);
		BridgeSmallChamferGapsInVerticalStructure(list2, outline, keptByContour);
		DeduplicateSameIntervalVerticalStructure(list2, keptByContour);
		List<PlannedDimension> kept = list2.Where((PlannedDimension dim) =>
			IsRightSideVerticalStructureCandidate(dim, outline, ignoredPoints)
			|| IsRightTopPartialEnvelopeStructureHeight(dim, outline, _config.GeometryTolerance)
			|| keptByContour.Contains(dim)
			|| IsInsetContourBackedVerticalStructureEdge(dim, outline, DimensionSide.Right)).ToList();
		RecordDiscardedCandidates(plan, list2, kept, "NotRightSideStructureCandidate");
		return kept;
	}

	/// <summary>
	/// When an inset contour edge (notch 50) is present, drop outer Y-column pieces that
	/// heavily overlap it (e.g. outer 56 overlapping inset 50) so the feature chain stays clean.
	/// </summary>
	private void PreferContourBackedVerticalStructureOverOverlappingProjection(
		IList<PlannedDimension> candidates,
		OutlineFeature2D outline,
		DimensionSide side)
	{
		if (candidates == null || candidates.Count < 2 || outline == null)
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		List<PlannedDimension> insetContour = candidates
			.Where((PlannedDimension d) => IsInsetContourBackedVerticalStructureEdge(d, outline, side))
			.ToList();
		if (insetContour.Count == 0)
		{
			return;
		}
		for (int i = candidates.Count - 1; i >= 0; i--)
		{
			PlannedDimension dim = candidates[i];
			if (dim == null || IsInsetContourBackedVerticalStructureEdge(dim, outline, side))
			{
				continue;
			}
			double a0 = Math.Min(dim.FirstPoint.Y, dim.SecondPoint.Y);
			double a1 = Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y);
			double aSpan = a1 - a0;
			if (aSpan <= tol)
			{
				continue;
			}
			bool overlapsInset = insetContour.Any((PlannedDimension c) =>
			{
				double c0 = Math.Min(c.FirstPoint.Y, c.SecondPoint.Y);
				double c1 = Math.Max(c.FirstPoint.Y, c.SecondPoint.Y);
				double overlap = Math.Min(a1, c1) - Math.Max(a0, c0);
				return overlap > tol && overlap + tol >= Math.Min(aSpan, c1 - c0) * 0.5;
			});
			if (overlapsInset)
			{
				candidates.RemoveAt(i);
			}
		}
	}

	private bool IsInsetContourBackedVerticalStructureEdge(
		PlannedDimension dim,
		OutlineFeature2D outline,
		DimensionSide side)
	{
		if (!IsContourBackedVerticalStructureEdge(dim, outline, side))
		{
			return false;
		}
		double tol = _config.GeometryTolerance;
		double x = (dim.FirstPoint.X + dim.SecondPoint.X) * 0.5;
		bool onOuter = (side == DimensionSide.Left && Math.Abs(x - outline.MinX) <= tol)
			|| (side == DimensionSide.Right && Math.Abs(x - outline.MaxX) <= tol);
		return !onOuter;
	}

	/// <summary>
	/// Inject each left/right-side vertical contour edge as an atomic structure height.
	/// Y-column projection alone misses inset notch walls (last-run 90° OS 50 at x≈MinX+3).
	/// </summary>
	private void AddSideFacingVerticalContourEdgeHeights(
		IList<PlannedDimension> candidates,
		OutlineFeature2D outline,
		DimensionSide side,
		string debugRole,
		ISet<PlannedDimension> keptByContour)
	{
		if (candidates == null || outline?.Segments == null)
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		double overall = outline.Height;
		double sideBand = Math.Max(tol * 8.0, outline.Width * 0.35);
		string alignmentKey = GetStructureAlignmentKey(side, horizontal: false);
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord || !segment.IsVertical(tol))
			{
				continue;
			}
			double span = segment.LengthY;
			if (IsTooSmallStructureSpan(span) || Math.Abs(span - overall) <= tol)
			{
				continue;
			}
			double x = (segment.Start.X + segment.End.X) * 0.5;
			// Only inset step walls (not outer envelope). Outer spans come from Y-column
			// projection; injecting outer edges re-adds dropped closed-chain body (e.g. 80).
			bool onOuterEnvelope = (side == DimensionSide.Left && Math.Abs(x - outline.MinX) <= tol)
				|| (side == DimensionSide.Right && Math.Abs(x - outline.MaxX) <= tol);
			if (onOuterEnvelope)
			{
				continue;
			}
			if (side == DimensionSide.Left)
			{
				if (x > outline.MinX + sideBand + tol)
				{
					continue;
				}
			}
			else if (x < outline.MaxX - sideBand - tol)
			{
				continue;
			}
			double y0 = Math.Min(segment.Start.Y, segment.End.Y);
			double y1 = Math.Max(segment.Start.Y, segment.End.Y);
			bool sameInterval = candidates.Any((PlannedDimension other) =>
				other != null
				&& Math.Abs(Math.Min(other.FirstPoint.Y, other.SecondPoint.Y) - y0) <= tol
				&& Math.Abs(Math.Max(other.FirstPoint.Y, other.SecondPoint.Y) - y1) <= tol
				&& Math.Abs(Math.Abs(other.SecondPoint.Y - other.FirstPoint.Y) - span) <= tol);
			if (sameInterval)
			{
				continue;
			}
			PlannedDimension dim = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = DimensionOrientation.Vertical,
				Side = side,
				FirstPoint = new Point2D(x, y0),
				SecondPoint = new Point2D(x, y1),
				OverrideText = string.Empty,
				DebugRole = debugRole ?? string.Empty,
				SourceKey = segment.SourceKey,
				AlignmentKey = alignmentKey,
				AlignmentPriority = 70,
				ReadingLevel = DimensionReadingLevel.LocalSpacing
			};
			candidates.Add(dim);
			if (keptByContour != null)
			{
				keptByContour.Add(dim);
			}
		}
	}

	/// <summary>
	/// Remove micro vertical structure spans (chamfer risers) and extend the larger abutting
	/// neighbor across them so 72 | 3 | 50 becomes 72 | 50 with a 3-unit gap for bridging.
	/// </summary>
	private void AbsorbMicroVerticalStructureSpans(
		IList<PlannedDimension> candidates,
		OutlineFeature2D outline,
		ISet<PlannedDimension> keptByContour = null)
	{
		if (candidates == null || candidates.Count < 2 || outline == null)
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		double maxMicro = Math.Max(5.0, Math.Max(outline.Width, outline.Height) * 0.025);
		bool changed;
		do
		{
			changed = false;
			List<PlannedDimension> ordered = candidates
				.Where((PlannedDimension d) => d != null)
				.OrderBy((PlannedDimension d) => Math.Min(d.FirstPoint.Y, d.SecondPoint.Y))
				.ToList();
			for (int i = 0; i < ordered.Count; i++)
			{
				PlannedDimension micro = ordered[i];
				double m0 = Math.Min(micro.FirstPoint.Y, micro.SecondPoint.Y);
				double m1 = Math.Max(micro.FirstPoint.Y, micro.SecondPoint.Y);
				double mSpan = m1 - m0;
				if (mSpan <= tol || mSpan > maxMicro + tol)
				{
					continue;
				}
				// Prefer absorbing into the larger abutting neighbor (not another micro).
				PlannedDimension lower = i > 0 ? ordered[i - 1] : null;
				PlannedDimension upper = i + 1 < ordered.Count ? ordered[i + 1] : null;
				double lower1 = lower == null ? double.NaN : Math.Max(lower.FirstPoint.Y, lower.SecondPoint.Y);
				double upper0 = upper == null ? double.NaN : Math.Min(upper.FirstPoint.Y, upper.SecondPoint.Y);
				bool abutsLower = lower != null && Math.Abs(lower1 - m0) <= tol;
				bool abutsUpper = upper != null && Math.Abs(upper0 - m1) <= tol;
				if (!abutsLower && !abutsUpper)
				{
					continue;
				}
				PlannedDimension absorbInto = null;
				if (abutsLower && abutsUpper)
				{
					double ls = Math.Abs(lower.SecondPoint.Y - lower.FirstPoint.Y);
					double us = Math.Abs(upper.SecondPoint.Y - upper.FirstPoint.Y);
					absorbInto = ls >= us ? lower : upper;
				}
				else if (abutsLower)
				{
					absorbInto = lower;
				}
				else
				{
					absorbInto = upper;
				}
				double a0 = Math.Min(absorbInto.FirstPoint.Y, absorbInto.SecondPoint.Y);
				double a1 = Math.Max(absorbInto.FirstPoint.Y, absorbInto.SecondPoint.Y);
				double new0 = Math.Min(a0, m0);
				double new1 = Math.Max(a1, m1);
				double x = (absorbInto.FirstPoint.X + absorbInto.SecondPoint.X) * 0.5;
				absorbInto.FirstPoint = new Point2D(x, new0);
				absorbInto.SecondPoint = new Point2D(x, new1);
				if (keptByContour != null)
				{
					keptByContour.Add(absorbInto);
				}
				candidates.Remove(micro);
				if (keptByContour != null)
				{
					keptByContour.Remove(micro);
				}
				changed = true;
				break;
			}
		}
		while (changed);
	}

	/// <summary>
	/// Close small chamfer gaps between same-side vertical structure pieces so a 72 edge that
	/// sits 3 above a 50 notch wall becomes the full 75 location span (0° top dual).
	/// </summary>
	private void BridgeSmallChamferGapsInVerticalStructure(
		IList<PlannedDimension> candidates,
		OutlineFeature2D outline,
		ISet<PlannedDimension> keptByContour = null)
	{
		if (candidates == null || candidates.Count < 2 || outline == null)
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		double maxGap = Math.Max(5.0, Math.Max(outline.Width, outline.Height) * 0.025);
		List<PlannedDimension> ordered = candidates
			.Where((PlannedDimension d) => d != null)
			.OrderBy((PlannedDimension d) => Math.Min(d.FirstPoint.Y, d.SecondPoint.Y))
			.ToList();
		for (int i = 0; i < ordered.Count - 1; i++)
		{
			PlannedDimension lower = ordered[i];
			PlannedDimension upper = ordered[i + 1];
			double lower0 = Math.Min(lower.FirstPoint.Y, lower.SecondPoint.Y);
			double lower1 = Math.Max(lower.FirstPoint.Y, lower.SecondPoint.Y);
			double upper0 = Math.Min(upper.FirstPoint.Y, upper.SecondPoint.Y);
			double upper1 = Math.Max(upper.FirstPoint.Y, upper.SecondPoint.Y);
			double gap = upper0 - lower1;
			if (gap <= tol || gap > maxGap + tol)
			{
				continue;
			}
			// Bridge when either end sits on an overall Y envelope (75 can be MaxY or MinY
			// depending on 90° vs 270°). Only extend the envelope-touching piece toward the other.
			bool upperOnEnvelope = Math.Abs(upper1 - outline.MaxY) <= tol;
			bool lowerOnEnvelope = Math.Abs(lower0 - outline.MinY) <= tol;
			if (!upperOnEnvelope && !lowerOnEnvelope)
			{
				continue;
			}
			if (upperOnEnvelope && (!lowerOnEnvelope || GetDimensionSpan(upper, horizontal: false) >= GetDimensionSpan(lower, horizontal: false) - tol))
			{
				double x = (upper.FirstPoint.X + upper.SecondPoint.X) * 0.5;
				upper.FirstPoint = new Point2D(x, lower1);
				upper.SecondPoint = new Point2D(x, upper1);
				if (keptByContour != null)
				{
					keptByContour.Add(upper);
				}
			}
			else if (lowerOnEnvelope)
			{
				double x = (lower.FirstPoint.X + lower.SecondPoint.X) * 0.5;
				lower.FirstPoint = new Point2D(x, lower0);
				lower.SecondPoint = new Point2D(x, upper0);
				if (keptByContour != null)
				{
					keptByContour.Add(lower);
				}
			}
		}
	}

	private bool IsContourBackedVerticalStructureEdge(
		PlannedDimension dim,
		OutlineFeature2D outline,
		DimensionSide side)
	{
		if (dim == null || outline?.Segments == null)
		{
			return false;
		}
		double tol = _config.GeometryTolerance;
		double y0 = Math.Min(dim.FirstPoint.Y, dim.SecondPoint.Y);
		double y1 = Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y);
		double span = y1 - y0;
		if (span <= tol)
		{
			return false;
		}
		double x = (dim.FirstPoint.X + dim.SecondPoint.X) * 0.5;
		double sideBand = Math.Max(tol * 8.0, outline.Width * 0.35);
		if (side == DimensionSide.Left && x > outline.MinX + sideBand + tol)
		{
			return false;
		}
		if (side == DimensionSide.Right && x < outline.MaxX - sideBand - tol)
		{
			return false;
		}
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord || !segment.IsVertical(tol))
			{
				continue;
			}
			double edgeX = (segment.Start.X + segment.End.X) * 0.5;
			if (Math.Abs(edgeX - x) > Math.Max(tol * 4.0, outline.Width * 0.05))
			{
				continue;
			}
			double overlap = Math.Min(segment.MaxY, y1) - Math.Max(segment.MinY, y0);
			if (overlap + tol >= span * 0.85)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Vertical dual of bottom ledge snap: cross-axis height chords (ΔX &gt; ΔY) that land on a
	/// side-facing vertical contour edge are projected onto that edge so rotated top steps
	/// (50+75) survive as Left/RightStructHeight.
	/// </summary>
	private void SnapCrossLevelVerticalCandidatesOntoSideFacingEdges(
		IList<PlannedDimension> candidates,
		OutlineFeature2D outline,
		DimensionSide side,
		ISet<PlannedDimension> snapped)
	{
		if (candidates == null || outline == null)
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		foreach (PlannedDimension dim in candidates)
		{
			if (dim == null || !IsCrossAxisStructureSpanTooLarge(dim.FirstPoint, dim.SecondPoint, horizontal: false))
			{
				continue;
			}
			Segment2D edge = FindStepSideVerticalEdgeCoveringSpan(dim, outline, side);
			if (edge == null)
			{
				continue;
			}
			double x = (edge.Start.X + edge.End.X) * 0.5;
			// Keep candidate Y span (feature height); only snap onto edge X.
			double y0 = Math.Min(dim.FirstPoint.Y, dim.SecondPoint.Y);
			double y1 = Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y);
			if (y1 - y0 <= tol)
			{
				continue;
			}
			double candidateSpan = y1 - y0;
			bool sameSpanClassicExists = candidates.Any((PlannedDimension other) =>
				other != null
				&& other != dim
				&& !IsCrossAxisStructureSpanTooLarge(other.FirstPoint, other.SecondPoint, horizontal: false)
				&& Math.Abs(Math.Abs(other.SecondPoint.Y - other.FirstPoint.Y) - candidateSpan) <= tol);
			if (sameSpanClassicExists)
			{
				continue;
			}
			dim.FirstPoint = new Point2D(x, y0);
			dim.SecondPoint = new Point2D(x, y1);
			if (snapped != null)
			{
				snapped.Add(dim);
			}
		}
	}

	private Segment2D FindStepSideVerticalEdgeCoveringSpan(PlannedDimension dim, OutlineFeature2D outline, DimensionSide side)
	{
		if (dim == null || outline == null || outline.Segments == null)
		{
			return null;
		}
		double tol = _config.GeometryTolerance;
		double y0 = Math.Min(dim.FirstPoint.Y, dim.SecondPoint.Y);
		double y1 = Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y);
		double span = y1 - y0;
		if (span <= tol)
		{
			return null;
		}
		// Prefer the more extreme X toward the placement side (left: smaller X, right: larger X).
		double sideX = side == DimensionSide.Left
			? Math.Min(dim.FirstPoint.X, dim.SecondPoint.X)
			: Math.Max(dim.FirstPoint.X, dim.SecondPoint.X);
		Segment2D best = null;
		double bestScore = double.NegativeInfinity;
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord || !segment.IsVertical(tol))
			{
				continue;
			}
			double edgeX = (segment.Start.X + segment.End.X) * 0.5;
			double overlap = Math.Min(segment.MaxY, y1) - Math.Max(segment.MinY, y0);
			if (overlap <= tol)
			{
				continue;
			}
			double lengthRatio = overlap / Math.Max(span, segment.LengthY);
			if (lengthRatio + tol < 0.9)
			{
				continue;
			}
			bool nearSideX = Math.Abs(edgeX - sideX) <= Math.Max(tol * 4.0, Math.Abs(dim.FirstPoint.X - dim.SecondPoint.X) * 0.15 + tol);
			if (!nearSideX)
			{
				continue;
			}
			// Prefer edges not on the continuous outer envelope wall.
			bool onOuterEnvelope = (side == DimensionSide.Left && Math.Abs(edgeX - outline.MinX) <= tol)
				|| (side == DimensionSide.Right && Math.Abs(edgeX - outline.MaxX) <= tol);
			double score = (nearSideX ? 1000.0 : 0.0) + (onOuterEnvelope ? 0.0 : 200.0) + overlap;
			if (score > bestScore + tol)
			{
				bestScore = score;
				best = segment;
			}
		}
		return best;
	}

	private void DeduplicateSameIntervalVerticalStructure(
		IList<PlannedDimension> candidates,
		ISet<PlannedDimension> preferDropIfDuplicate)
	{
		if (candidates == null || candidates.Count < 2)
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		for (int i = candidates.Count - 1; i >= 0; i--)
		{
			PlannedDimension a = candidates[i];
			if (a == null)
			{
				continue;
			}
			double a0 = Math.Min(a.FirstPoint.Y, a.SecondPoint.Y);
			double a1 = Math.Max(a.FirstPoint.Y, a.SecondPoint.Y);
			double ax = (a.FirstPoint.X + a.SecondPoint.X) * 0.5;
			for (int j = 0; j < i; j++)
			{
				PlannedDimension b = candidates[j];
				if (b == null)
				{
					continue;
				}
				double b0 = Math.Min(b.FirstPoint.Y, b.SecondPoint.Y);
				double b1 = Math.Max(b.FirstPoint.Y, b.SecondPoint.Y);
				double bx = (b.FirstPoint.X + b.SecondPoint.X) * 0.5;
				if (Math.Abs(a0 - b0) > tol || Math.Abs(a1 - b1) > tol || Math.Abs(ax - bx) > tol)
				{
					continue;
				}
				bool aSnapped = preferDropIfDuplicate != null && preferDropIfDuplicate.Contains(a);
				if (aSnapped)
				{
					preferDropIfDuplicate.Remove(a);
					candidates.RemoveAt(i);
					break;
				}
				bool bSnapped = preferDropIfDuplicate != null && preferDropIfDuplicate.Contains(b);
				if (bSnapped)
				{
					preferDropIfDuplicate.Remove(b);
					candidates.RemoveAt(j);
					i = candidates.Count;
					break;
				}
				candidates.RemoveAt(i);
				break;
			}
		}
	}

	private static void RecordDiscardedCandidates(DimensionPlan plan, IList<PlannedDimension> before, IList<PlannedDimension> after, string reason)
	{
		if (plan == null)
		{
			return;
		}
		foreach (PlannedDimension candidate in before)
		{
			if (!after.Contains(candidate))
			{
				plan.AddDiscardedCandidate(candidate, reason);
			}
		}
	}

	private void RemoveComplementaryOverallRemainderCandidates(
		IList<PlannedDimension> candidates,
		double overallMin,
		double overallMax,
		bool horizontal,
		Func<PlannedDimension, bool> preserveCandidate = null)
	{
		if (candidates == null || candidates.Count < 2)
		{
			return;
		}
		for (int i = candidates.Count - 1; i >= 0; i--)
		{
			if (preserveCandidate != null && preserveCandidate(candidates[i]))
			{
				continue;
			}
			double span = GetDimensionSpan(candidates[i], horizontal);
			if (candidates.Any((PlannedDimension other) => other != candidates[i]
				&& GetDimensionSpan(other, horizontal) < span - _config.GeometryTolerance
				&& FormsCompleteOverallPartition(candidates[i], other, overallMin, overallMax, horizontal)))
			{
				candidates.RemoveAt(i);
			}
		}
	}

	private void SnapComplementaryHorizontalRemainderEndpoints(IList<PlannedDimension> candidates, OutlineFeature2D outline)
	{
		if (candidates == null || outline == null)
		{
			return;
		}
		foreach (PlannedDimension dimension in candidates)
		{
			if (dimension == null)
			{
				continue;
			}
			// Only snap when a smaller partner forms a *real* overall partition — not mere span sum.
			if (!candidates.Any((PlannedDimension other) => other != null && other != dimension
				&& CanAttemptComplementaryPartitionSnap(dimension, other, outline, horizontal: true)))
			{
				continue;
			}
			dimension.FirstPoint = SnapVerticalPointToLowerConnectedHorizontal(dimension.FirstPoint, outline);
			dimension.SecondPoint = SnapVerticalPointToLowerConnectedHorizontal(dimension.SecondPoint, outline);
		}
	}

	private void SnapComplementaryVerticalRemainderEndpoints(IList<PlannedDimension> candidates, OutlineFeature2D outline)
	{
		if (candidates == null || outline == null)
		{
			return;
		}
		foreach (PlannedDimension dimension in candidates)
		{
			if (dimension == null)
			{
				continue;
			}
			if (!candidates.Any((PlannedDimension other) => other != null && other != dimension
				&& CanAttemptComplementaryPartitionSnap(dimension, other, outline, horizontal: false)))
			{
				continue;
			}
			if (dimension.Side == DimensionSide.Right
				&& IsRightTopPartialEnvelopeStructureHeight(dimension, outline, _config.GeometryTolerance))
			{
				dimension.FirstPoint = SnapHorizontalPointToRightConnectedVertical(dimension.FirstPoint, outline);
				dimension.SecondPoint = SnapHorizontalPointToRightConnectedVertical(dimension.SecondPoint, outline);
			}
			else
			{
				dimension.FirstPoint = SnapHorizontalPointToLeftConnectedVertical(dimension.FirstPoint, outline);
				dimension.SecondPoint = SnapHorizontalPointToLeftConnectedVertical(dimension.SecondPoint, outline);
			}
		}
	}

	/// <summary>
	/// Narrow gate for complementary-remainder endpoint snap.
	/// Span-sum alone is never enough: requires structure semantics, correct orientation,
	/// intervals inside overall, and FormsCompleteOverallPartition (no gap / interior overlap /
	/// out-of-bounds; ends match overall). Snap only adjusts attachment; suppress stays on
	/// FormsCompleteOverallPartition / multi-piece chain rules.
	/// </summary>
	internal bool CanAttemptComplementaryPartitionSnap(
		PlannedDimension larger,
		PlannedDimension smaller,
		OutlineFeature2D outline,
		bool horizontal)
	{
		if (larger == null || smaller == null || outline == null)
		{
			return false;
		}
		// Only structure-width/height candidates participate in this path.
		if (!IsStructureWidthOrHeightRole(larger.DebugRole) || !IsStructureWidthOrHeightRole(smaller.DebugRole))
		{
			return false;
		}
		DimensionOrientation expected = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
		if (larger.Orientation != expected || smaller.Orientation != expected)
		{
			return false;
		}
		double largerSpan = GetDimensionSpan(larger, horizontal);
		double smallerSpan = GetDimensionSpan(smaller, horizontal);
		// Complementary remainder snaps the larger partner only.
		if (smallerSpan >= largerSpan - _config.GeometryTolerance)
		{
			return false;
		}
		double overallMin = horizontal ? outline.MinX : outline.MinY;
		double overallMax = horizontal ? outline.MaxX : outline.MaxY;
		// Real geometric partition (covers ends, no gap/overlap/out of bounds).
		if (!FormsCompleteOverallPartition(larger, smaller, overallMin, overallMax, horizontal))
		{
			return false;
		}
		return true;
	}

	/// <summary>
	/// Snap a vertical-edge point down onto a connected horizontal segment endpoint.
	/// Uses nullable FirstOrDefault so a real hit at (0,0) is not treated as "not found".
	/// When no candidate exists, returns the original <paramref name="point"/>.
	/// </summary>
	internal Point2D SnapVerticalPointToLowerConnectedHorizontal(Point2D point, OutlineFeature2D outline)
	{
		if (outline == null)
		{
			return point;
		}
		Point2D? found = outline.Segments
			.Where((Segment2D s) => s != null && s.IsHorizontal(_config.GeometryTolerance))
			.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			.Where((Point2D p) => Math.Abs(p.X - point.X) <= _config.GeometryTolerance && p.Y < point.Y - _config.GeometryTolerance)
			.OrderByDescending((Point2D p) => p.Y)
			.Select((Point2D p) => (Point2D?)p)
			.FirstOrDefault();
		return found ?? point;
	}

	/// <summary>
	/// Snap a horizontal-edge point left onto a connected vertical segment endpoint.
	/// Uses nullable FirstOrDefault so a real hit at (0,0) is not treated as "not found".
	/// When no candidate exists, returns the original <paramref name="point"/>.
	/// </summary>
	internal Point2D SnapHorizontalPointToLeftConnectedVertical(Point2D point, OutlineFeature2D outline)
	{
		if (outline == null)
		{
			return point;
		}
		Point2D? found = outline.Segments
			.Where((Segment2D s) => s != null && s.IsVertical(_config.GeometryTolerance))
			.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			.Where((Point2D p) => Math.Abs(p.Y - point.Y) <= _config.GeometryTolerance && p.X < point.X - _config.GeometryTolerance)
			.OrderByDescending((Point2D p) => p.X)
			.Select((Point2D p) => (Point2D?)p)
			.FirstOrDefault();
		return found ?? point;
	}

	/// <summary>
	/// Snap a horizontal-edge point right onto a connected vertical segment endpoint.
	/// Uses the nearest endpoint to preserve the right-side attachment.
	/// </summary>
	internal Point2D SnapHorizontalPointToRightConnectedVertical(Point2D point, OutlineFeature2D outline)
	{
		if (outline == null)
		{
			return point;
		}
		Point2D? found = outline.Segments
			.Where((Segment2D s) => s != null && s.IsVertical(_config.GeometryTolerance))
			.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			.Where((Point2D p) => Math.Abs(p.Y - point.Y) <= _config.GeometryTolerance && p.X > point.X + _config.GeometryTolerance)
			.OrderBy((Point2D p) => p.X)
			.Select((Point2D p) => (Point2D?)p)
			.FirstOrDefault();
		return found ?? point;
	}

	private List<PlannedDimension> BuildVerticalHeightCandidates(IList<Point2D> points, DimensionSide side, string debugRole)
	{
		List<PlannedDimension> list = new List<PlannedDimension>();
		string alignmentKey = GetStructureAlignmentKey(side, horizontal: false);
		for (int i = 1; i < points.Count; i++)
		{
			Point2D secondPoint = points[i - 1];
			Point2D firstPoint = points[i];
			double num = Math.Abs(secondPoint.Y - firstPoint.Y);
			if (!(num <= _config.GeometryTolerance))
			{
				list.Add(new PlannedDimension
				{
					Kind = DimensionKind.Normal,
					Orientation = DimensionOrientation.Vertical,
					Side = side,
					FirstPoint = firstPoint,
					SecondPoint = secondPoint,
					OverrideText = string.Empty,
					DebugRole = (debugRole ?? string.Empty),
					AlignmentKey = alignmentKey,
					AlignmentPriority = 70,
					ReadingLevel = DimensionReadingLevel.LocalSpacing
				});
			}
		}
		return list;
	}

	private List<StructurePoint> BuildTopSideStructurePoints(OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		List<StructurePoint> list = (from g in GroupVerticalSegmentsByX(outline)
			select GetTopMostStructurePoint(g, ignoredPoints) into p
			where p != null
			select p).ToList();
		AddTopInclinedEndpointStructurePoints(list, outline, ignoredPoints);
		AddTopSlopeEndpointStructurePoints(list, outline, ignoredPoints);
		return (from p in list
			orderby p.Point.X
			select new StructurePoint
			{
				Point = p.Point,
				Source = p.Source
			}).ToList();
	}

	private IList<Point2D> BuildBottomSideHorizontalStructurePoints(OutlineFeature2D outline)
	{
		return (from p in GroupVerticalSegmentsByX(outline).Select(GetBottomMostPoint)
			where p.HasValue
			select p.Value into p
			orderby p.X, p.Y
			select p).ToList();
	}

	internal IList<PlannedDimension> BuildBottomStructureWidthDimensions(OutlineFeature2D outline, DimensionPlan plan = null)
	{
		List<IgnoredPoint> ignoredPoints = new List<IgnoredPoint>();
		List<PlannedDimension> list2;
		while (true)
		{
			List<Point2D> list = BuildBottomSideHorizontalStructurePoints(outline, ignoredPoints);
			if (list.Count < 2)
			{
				return new List<PlannedDimension>();
			}
			list2 = BuildHorizontalWidthCandidates(list, DimensionSide.Bottom, "BottomStructWidth");
			List<IgnoredPoint> bottomExtensionCrossingPoints = GetBottomExtensionCrossingPoints(list2, outline);
			if (bottomExtensionCrossingPoints.Count == 0)
			{
				break;
			}
			if (!AddIgnoredPointMetadata(ignoredPoints, bottomExtensionCrossingPoints))
			{
				return new List<PlannedDimension>();
			}
		}
		List<PlannedDimension> snapshot = new List<PlannedDimension>(list2);
		RemoveLongestBottomExtensionCandidate(list2, outline);
		RecordDiscardedCandidates(plan, snapshot, list2, "LongestExtensionCandidate");
		// Multi-level bottom steps: consecutive column bottoms can form a cross-level span that
		// is still covered by a real bottom-facing horizontal ledge (e.g. 180° of former top
		// 73+87.55). Snap those spans onto the ledge and keep only via the snapped set — do not
		// broadly relax NotBottomSideStructureCandidate (preserves test2 / residual-arm baselines).
		HashSet<PlannedDimension> snappedToBottomFacingEdge = new HashSet<PlannedDimension>();
		SnapCrossLevelBottomCandidatesOntoBottomFacingEdges(list2, outline, snappedToBottomFacingEdge);
		DeduplicateSameIntervalHorizontalStructure(list2, snappedToBottomFacingEdge);
		// ponytail: Bottom intentionally skips the complementary-remainder snap/removal that
		// Top/Left/Right run here (Bottom is the datum side); add it only when a concrete
		// repro shows a bottom remainder duplicate.
		List<PlannedDimension> kept = list2
			.Where((PlannedDimension dim) => IsBottomSideHorizontalStructureCandidate(dim, outline, ignoredPoints)
				|| snappedToBottomFacingEdge.Contains(dim))
			.ToList();
		RecordDiscardedCandidates(plan, list2, kept, "NotBottomSideStructureCandidate");
		return kept;
	}

	/// <summary>
	/// Remove later horizontal structure dims that share the same X interval and Y (tol).
	/// Prefer keeping non-snapped (classic bottom-most) members.
	/// </summary>
	private void DeduplicateSameIntervalHorizontalStructure(
		IList<PlannedDimension> candidates,
		ISet<PlannedDimension> preferDropIfDuplicate)
	{
		if (candidates == null || candidates.Count < 2)
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		// Drop snapped duplicates first when they collide with a classic member.
		for (int i = candidates.Count - 1; i >= 0; i--)
		{
			PlannedDimension a = candidates[i];
			if (a == null)
			{
				continue;
			}
			double a0 = Math.Min(a.FirstPoint.X, a.SecondPoint.X);
			double a1 = Math.Max(a.FirstPoint.X, a.SecondPoint.X);
			double ay = (a.FirstPoint.Y + a.SecondPoint.Y) * 0.5;
			for (int j = 0; j < i; j++)
			{
				PlannedDimension b = candidates[j];
				if (b == null)
				{
					continue;
				}
				double b0 = Math.Min(b.FirstPoint.X, b.SecondPoint.X);
				double b1 = Math.Max(b.FirstPoint.X, b.SecondPoint.X);
				double by = (b.FirstPoint.Y + b.SecondPoint.Y) * 0.5;
				if (Math.Abs(a0 - b0) > tol || Math.Abs(a1 - b1) > tol || Math.Abs(ay - by) > tol)
				{
					continue;
				}
				// Same interval: drop the snapped one if either is snapped; else drop later.
				bool aSnapped = preferDropIfDuplicate != null && preferDropIfDuplicate.Contains(a);
				bool bSnapped = preferDropIfDuplicate != null && preferDropIfDuplicate.Contains(b);
				if (aSnapped)
				{
					preferDropIfDuplicate.Remove(a);
					candidates.RemoveAt(i);
					break;
				}
				if (bSnapped)
				{
					preferDropIfDuplicate.Remove(b);
					candidates.RemoveAt(j);
					// indices shifted; restart outer from i
					i = candidates.Count;
					break;
				}
				candidates.RemoveAt(i);
				break;
			}
		}
	}

	/// <summary>
	/// When consecutive bottom-most column points produce a cross-level span, snap onto a real
	/// horizontal contour edge at the higher column Y (the step ledge). Ray-cast "bottom-facing"
	/// alone is insufficient: after 180° the ledge OS may resolve Top while still being the
	/// real step face between the two bottom-most column points (last-run 87.55 case).
	/// </summary>
	private void SnapCrossLevelBottomCandidatesOntoBottomFacingEdges(
		IList<PlannedDimension> candidates,
		OutlineFeature2D outline,
		ISet<PlannedDimension> snapped)
	{
		if (candidates == null || outline == null)
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		foreach (PlannedDimension dim in candidates)
		{
			if (dim == null || !IsCrossAxisStructureSpanTooLarge(dim.FirstPoint, dim.SecondPoint, horizontal: true))
			{
				continue;
			}
			Segment2D edge = FindStepLedgeHorizontalEdgeCoveringSpan(dim, outline);
			if (edge == null)
			{
				continue;
			}
			double y = (edge.Start.Y + edge.End.Y) * 0.5;
			// Keep the column-bottom X span (e.g. 177.45→265 = 87.55), only project onto ledge Y.
			// Using edge.Min/Max alone shortens the step (last-run: 82.55) and leaves a gap so
			// 177+82.55+73 never completes overall and MultiPiece cannot drop 177.
			double x0 = Math.Min(dim.FirstPoint.X, dim.SecondPoint.X);
			double x1 = Math.Max(dim.FirstPoint.X, dim.SecondPoint.X);
			if (x1 - x0 <= tol)
			{
				continue;
			}
			// Only recover multi-level ledges above MinY (not the outer bottom envelope).
			if (Math.Abs(y - outline.MinY) <= tol)
			{
				continue;
			}
			double candidateSpan = x1 - x0;
			// Block snap when a classic collinear BottomStructWidth already has the same span —
			// open-contour fixtures intentionally keep that length once as structure and once as OS
			// (test2 134.79 pair). Mid-level recovery still works when the span is new (87.55).
			bool sameSpanClassicExists = candidates.Any((PlannedDimension other) =>
				other != null
				&& other != dim
				&& !IsCrossAxisStructureSpanTooLarge(other.FirstPoint, other.SecondPoint, horizontal: true)
				&& Math.Abs(Math.Abs(other.SecondPoint.X - other.FirstPoint.X) - candidateSpan) <= tol);
			if (sameSpanClassicExists)
			{
				continue;
			}
			// If any collinear classic structure already covers ≥90% of this X span at ledge Y, skip.
			bool edgeAlreadyOwned = candidates.Any((PlannedDimension other) =>
			{
				if (other == null || other == dim
					|| IsCrossAxisStructureSpanTooLarge(other.FirstPoint, other.SecondPoint, horizontal: true))
				{
					return false;
				}
				double oy = (other.FirstPoint.Y + other.SecondPoint.Y) * 0.5;
				if (Math.Abs(oy - y) > tol)
				{
					return false;
				}
				double o0 = Math.Min(other.FirstPoint.X, other.SecondPoint.X);
				double o1 = Math.Max(other.FirstPoint.X, other.SecondPoint.X);
				double overlap = Math.Min(o1, x1) - Math.Max(o0, x0);
				return overlap >= candidateSpan * 0.9 - tol;
			});
			if (edgeAlreadyOwned)
			{
				continue;
			}
			dim.FirstPoint = new Point2D(x0, y);
			dim.SecondPoint = new Point2D(x1, y);
			if (snapped != null)
			{
				snapped.Add(dim);
			}
		}
	}

	/// <summary>
	/// Find a real horizontal outline edge covering the cross-level chord's X span, preferring
	/// edges near the higher bottom-most column Y (step ledge). Falls back to ray-cast
	/// bottom-facing edges when no ledge-at-higher-Y match exists.
	/// </summary>
	private Segment2D FindStepLedgeHorizontalEdgeCoveringSpan(PlannedDimension dim, OutlineFeature2D outline)
	{
		if (dim == null || outline == null || outline.Segments == null)
		{
			return null;
		}
		double tol = _config.GeometryTolerance;
		double x0 = Math.Min(dim.FirstPoint.X, dim.SecondPoint.X);
		double x1 = Math.Max(dim.FirstPoint.X, dim.SecondPoint.X);
		double span = x1 - x0;
		if (span <= tol)
		{
			return null;
		}
		// Higher of the two column bottoms sits on the step ledge after rotation.
		double ledgeY = Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y);
		Segment2D best = null;
		double bestScore = double.NegativeInfinity;
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord || !segment.IsHorizontal(tol))
			{
				continue;
			}
			double edgeY = (segment.Start.Y + segment.End.Y) * 0.5;
			// Never snap onto outer MinY envelope (test2 open-contour).
			if (Math.Abs(edgeY - outline.MinY) <= tol)
			{
				continue;
			}
			double overlap = Math.Min(segment.MaxX, x1) - Math.Max(segment.MinX, x0);
			if (overlap <= tol)
			{
				continue;
			}
			double lengthRatio = overlap / Math.Max(span, segment.LengthX);
			if (lengthRatio + tol < 0.9)
			{
				continue;
			}
			bool nearLedgeY = Math.Abs(edgeY - ledgeY) <= Math.Max(tol * 4.0, Math.Abs(dim.FirstPoint.Y - dim.SecondPoint.Y) * 0.15);
			bool bottomFacing = _structureEndpointRules.IsBottomFacingHorizontalSegment(segment, outline);
			// Prefer ledge-Y match (works when OS side resolves Top); allow bottom-facing fallback.
			if (!nearLedgeY && !bottomFacing)
			{
				continue;
			}
			// Score: prefer near ledge Y, then larger overlap.
			double score = (nearLedgeY ? 1000.0 : 0.0) + (bottomFacing ? 100.0 : 0.0) + overlap;
			if (score > bestScore + tol)
			{
				bestScore = score;
				best = segment;
			}
		}
		return best;
	}

	private List<Point2D> BuildBottomSideHorizontalStructurePoints(OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		List<Point2D> list = (from g in GroupVerticalSegmentsByX(outline)
			select GetBottomMostPoint(g, ignoredPoints) into p
			where p.HasValue
			select p.Value).ToList();
		AddBottomInclinedEndpointStructurePoints(list, outline, ignoredPoints);
		return list.OrderBy((Point2D p) => p.X).ToList();
	}

	private IList<Point2D> BuildLeftSideVerticalStructurePoints(OutlineFeature2D outline)
	{
		return (from p in GroupHorizontalSegmentsByY(outline).Select(GetLeftMostPoint)
			where p.HasValue
			select p.Value into p
			orderby p.Y, p.X
			select p).ToList();
	}

	private List<Point2D> BuildLeftSideVerticalStructurePoints(OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		List<Point2D> list = (from g in GroupHorizontalSegmentsByY(outline)
			select GetLeftMostPoint(g, ignoredPoints) into p
			where p.HasValue
			select p.Value).ToList();
		AddSideInclinedEndpointStructurePoints(list, outline, ignoredPoints, DimensionSide.Left);
		return list.OrderByDescending((Point2D p) => p.Y).ToList();
	}

	private IList<Point2D> BuildRightSideVerticalStructurePoints(OutlineFeature2D outline)
	{
		return (from p in GroupHorizontalSegmentsByY(outline).Select(GetRightMostPoint)
			where p.HasValue
			select p.Value into p
			orderby p.Y, p.X
			select p).ToList();
	}

	private List<Point2D> BuildRightSideVerticalStructurePoints(OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		List<Point2D> list = (from g in GroupHorizontalSegmentsByY(outline)
			select GetRightMostPoint(g, ignoredPoints) into p
			where p.HasValue
			select p.Value).ToList();
		AddSideInclinedEndpointStructurePoints(list, outline, ignoredPoints, DimensionSide.Right);
		return list.OrderByDescending((Point2D p) => p.Y).ToList();
	}

	private IList<IList<Segment2D>> GroupVerticalSegmentsByX(OutlineFeature2D outline)
	{
		List<IList<Segment2D>> list = new List<IList<Segment2D>>();
		foreach (Segment2D segment in from s in outline.Segments
			where !s.IsArcChord
			where s.IsVertical(_config.GeometryTolerance)
			where s.LengthY > _config.GeometryTolerance
			select s)
		{
			IList<Segment2D> list2 = list.FirstOrDefault((IList<Segment2D> g) => Math.Abs(g[0].MinX - segment.MinX) <= _config.GeometryTolerance);
			if (list2 == null)
			{
				list2 = new List<Segment2D>();
				list.Add(list2);
			}
			list2.Add(segment);
		}
		return list;
	}

	private IList<IList<Segment2D>> GroupHorizontalSegmentsByY(OutlineFeature2D outline)
	{
		List<IList<Segment2D>> list = new List<IList<Segment2D>>();
		foreach (Segment2D segment in from s in outline.Segments
			where !s.IsArcChord
			where s.IsHorizontal(_config.GeometryTolerance)
			where s.LengthX > _config.GeometryTolerance
			select s)
		{
			IList<Segment2D> list2 = list.FirstOrDefault((IList<Segment2D> g) => Math.Abs(g[0].MinY - segment.MinY) <= _config.GeometryTolerance);
			if (list2 == null)
			{
				list2 = new List<Segment2D>();
				list.Add(list2);
			}
			list2.Add(segment);
		}
		return list;
	}

	private StructurePoint GetTopMostStructurePoint(IEnumerable<Segment2D> segments, IList<IgnoredPoint> ignoredPoints)
	{
		List<Segment2D> source = segments.ToList();
		Point2D? point2D = ((IEnumerable<Point2D>)(from p in source.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			where !ContainsIgnoredPoint(ignoredPoints, p)
			orderby p.Y descending, p.X
			select p)).Select((Func<Point2D, Point2D?>)((Point2D p) => p)).FirstOrDefault();
		if (!point2D.HasValue)
		{
			return null;
		}
		return new StructurePoint
		{
			Point = point2D.Value,
			Source = "VerticalTopMost"
		};
	}

	private Point2D? GetBottomMostPoint(IEnumerable<Segment2D> segments)
	{
		List<Point2D> list = segments.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End }).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		return (from p in list
			orderby p.Y, p.X
			select p).First();
	}

	private Point2D? GetBottomMostPoint(IEnumerable<Segment2D> segments, IList<IgnoredPoint> ignoredPoints)
	{
		List<Segment2D> source = segments.ToList();
		return ((IEnumerable<Point2D>)(from p in source.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			where !ContainsIgnoredPoint(ignoredPoints, p)
			orderby p.Y, p.X
			select p)).Select((Func<Point2D, Point2D?>)((Point2D p) => p)).FirstOrDefault();
	}

	private Point2D? GetLeftMostPoint(IEnumerable<Segment2D> segments)
	{
		List<Point2D> list = segments.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End }).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		return (from p in list
			orderby p.X, p.Y
			select p).First();
	}

	private Point2D? GetLeftMostPoint(IEnumerable<Segment2D> segments, IList<IgnoredPoint> ignoredPoints)
	{
		return ((IEnumerable<Point2D>)(from p in segments.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			where !ContainsIgnoredPoint(ignoredPoints, p)
			orderby p.X, p.Y
			select p)).Select((Func<Point2D, Point2D?>)((Point2D p) => p)).FirstOrDefault();
	}

	private Point2D? GetRightMostPoint(IEnumerable<Segment2D> segments)
	{
		List<Point2D> list = segments.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End }).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		return (from p in list
			orderby p.X descending, p.Y
			select p).First();
	}

	private Point2D? GetRightMostPoint(IEnumerable<Segment2D> segments, IList<IgnoredPoint> ignoredPoints)
	{
		return ((IEnumerable<Point2D>)(from p in segments.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			where !ContainsIgnoredPoint(ignoredPoints, p)
			orderby p.X descending, p.Y
			select p)).Select((Func<Point2D, Point2D?>)((Point2D p) => p)).FirstOrDefault();
	}

	private bool IsOverallBoundarySegment(Segment2D segment, OutlineFeature2D outline)
	{
		if (segment.IsHorizontal(_config.GeometryTolerance) && Math.Abs(segment.LengthX - outline.Width) <= _config.GeometryTolerance)
		{
			return true;
		}
		return segment.IsVertical(_config.GeometryTolerance) && Math.Abs(segment.LengthY - outline.Height) <= _config.GeometryTolerance;
	}

	private bool IsSuppressedByCornerFeature(Segment2D segment, OutlineFeature2D outline)
	{
		foreach (ChamferFeature2D chamfer in outline.Chamfers)
		{
			if (Touches(segment, chamfer.StartPoint) || Touches(segment, chamfer.EndPoint))
			{
				return true;
			}
		}
		foreach (FilletFeature2D fillet in outline.Fillets)
		{
			if (Touches(segment, fillet.StartPoint) || Touches(segment, fillet.EndPoint))
			{
				return true;
			}
		}
		return false;
	}

	private bool Touches(Segment2D segment, Point2D point)
	{
		return PointsEqual(segment.Start, point) || PointsEqual(segment.End, point);
	}

	private bool PointsEqual(Point2D a, Point2D b)
	{
		return Math.Abs(a.X - b.X) <= _config.GeometryTolerance && Math.Abs(a.Y - b.Y) <= _config.GeometryTolerance;
	}
}
