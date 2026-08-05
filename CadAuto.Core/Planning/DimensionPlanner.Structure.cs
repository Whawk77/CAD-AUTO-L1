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
		List<PlannedDimension> kept = list2.Where((PlannedDimension dim) => IsLeftSideVerticalStructureCandidate(dim, outline, ignoredPoints)).ToList();
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
		List<PlannedDimension> kept = list2.Where((PlannedDimension dim) =>
			IsRightSideVerticalStructureCandidate(dim, outline, ignoredPoints)
			|| IsRightTopPartialEnvelopeStructureHeight(dim, outline, _config.GeometryTolerance)).ToList();
		RecordDiscardedCandidates(plan, list2, kept, "NotRightSideStructureCandidate");
		return kept;
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
		// ponytail: Bottom intentionally skips the complementary-remainder snap/removal that
		// Top/Left/Right run here (Bottom is the datum side); add it only when a concrete
		// repro shows a bottom remainder duplicate.
		List<PlannedDimension> kept = list2.Where((PlannedDimension dim) => IsBottomSideHorizontalStructureCandidate(dim, outline, ignoredPoints)).ToList();
		RecordDiscardedCandidates(plan, list2, kept, "NotBottomSideStructureCandidate");
		return kept;
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
