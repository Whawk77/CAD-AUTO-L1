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
	// Suppression pipeline: 23 passes over 13 rules. THE ORDER IS PRODUCT BEHAVIOR -
	// each inline note below records a constraint that was learned the hard way; keep
	// the notes adjacent to the calls they explain. Stages:
	//   0. capture   - FindLocalGeometryOnOverallEnvelope snapshots candidates BEFORE
	//                  any pass mutates the plan; the matching suppress is pass 23.
	//   1. structure vs overall (passes 1-7)  - height dedup vs overall, bottom
	//                  protrusion remainders, structure partition chains (H then V),
	//                  datum-rooted outer-step complements. Must run before stage 2 so
	//                  OutlineSegment partners still exist when chains are built.
	//   2. OutlineSegment partition (passes 8-11, four sides) - scoped to
	//                  OutlineSegment only.
	//   3. mirror dedup (passes 12-13) - must precede stage 5; envelope could remove
	//                  the primary-side outer tip first and orphan a same-interval
	//                  secondary OutlineSegment.
	//   4. secondary-side cleanup (passes 14-15) - after mirror keeps structure
	//                  heights over OS.
	//   5. envelope collinear fragments (pass 16).
	//   6. complementary remainders (passes 17-18) - Top/Right only by design.
	//   7. measured duplicates (passes 19-22, four sides).
	//   8. deferred local-geometry suppress (pass 23) using the stage-0 snapshot.
	// This method runs twice per full plan (once inside CreateOutlinePlan, once after
	// hole/slot dimensions are added) - outline candidates see it twice, hole/slot
	// candidates once.
	private void SuppressDuplicateDimensions(DimensionPlan plan, OutlineFeature2D outline)
	{
		List<PlannedDimension> localGeometryOnEnvelope = FindLocalGeometryOnOverallEnvelope(plan, outline);
		SuppressRightStructureHeightsDuplicatingOverallHeight(plan);
		SuppressLeftStructureHeightsCoveredByRight(plan);
		SuppressBottomProtrusionInnerRemainders(plan, outline);
		// BuildOverallPartitionChain for structure: multi-piece contiguous cover of overall
		// (structure roles + OutlineSegment partners). Suppress only structure members of the chain.
		// Run before OutlineSegment overall-partition removal so OS partners still exist.
		// Partners never include slot/hole Normals. Local structure steps that do not complete
		// overall are kept (e.g. TopStructWidth=50).
		SuppressStructureDimensionsThatPartitionOverall(plan, outline, horizontal: true);
		SuppressStructureDimensionsThatPartitionOverall(plan, outline, horizontal: false);
		SuppressDatumRootedOuterStepComplements(plan, outline, horizontal: true);
		SuppressDatumRootedOuterStepComplements(plan, outline, horizontal: false);
		// Drop raw OutlineSegment pairs that re-partition overall (before complementary remainder
		// removes only the larger partner and leaves the smaller fragment orphaned).
		// Scoped to OutlineSegment only — do not broaden complementary-remainder to Bottom/Left
		// or slot/hole Normal dims.
		SuppressOutlineSegmentsThatPartitionOverall(plan, outline, DimensionSide.Top, horizontal: true);
		SuppressOutlineSegmentsThatPartitionOverall(plan, outline, DimensionSide.Bottom, horizontal: true);
		SuppressOutlineSegmentsThatPartitionOverall(plan, outline, DimensionSide.Right, horizontal: false);
		SuppressOutlineSegmentsThatPartitionOverall(plan, outline, DimensionSide.Left, horizontal: false);
		// Mirror before envelope: envelope may remove the primary-side outer tip first, which
		// would orphan a same-interval secondary OutlineSegment (e.g. Right OS@inner X that
		// matches Left envelope tip) and leave GEN|OutlineSegment*|R|L0 selected.
		SuppressMirroredDuplicates(plan, DimensionSide.Bottom, DimensionSide.Top, horizontal: true);
		SuppressMirroredDuplicates(plan, DimensionSide.Left, DimensionSide.Right, horizontal: false);
		// After mirror keeps structure heights over OS, drop secondary-side vertical OS that only
		// restate the primary structure stack or the overall residual (left/right step symmetry).
		SuppressSecondaryVerticalOutlineSegmentsRedundantWithPrimaryStructureStack(plan);
		// Structure>OS mirror can leave short outer tips that only restate overall residual noise.
		SuppressOrphanOuterVerticalStructureHeightTips(plan);
		// Outer-envelope collinear OutlineSegment fragments (+ same-interval structure dups).
		SuppressOutlineSegmentsOnOverallEnvelope(plan, outline);
		// Existing complementary remainder for structure/normal remainders (Top/Right only).
		SuppressComplementaryOutlineRemainders(plan, DimensionSide.Top, horizontal: true);
		SuppressComplementaryOutlineRemainders(plan, DimensionSide.Right, horizontal: false);
		SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Bottom, horizontal: true);
		SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Top, horizontal: true);
		SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Left, horizontal: false);
		SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Right, horizontal: false);
		SuppressLocalGeometryOnOverallEnvelope(plan, localGeometryOnEnvelope);
	}

	internal void SuppressLocalGeometryOnOverallEnvelope(DimensionPlan plan, OutlineFeature2D outline)
	{
		SuppressLocalGeometryOnOverallEnvelope(plan, FindLocalGeometryOnOverallEnvelope(plan, outline));
	}

	private List<PlannedDimension> FindLocalGeometryOnOverallEnvelope(DimensionPlan plan, OutlineFeature2D outline)
	{
		List<PlannedDimension> matches = new List<PlannedDimension>();
		if (plan == null || outline == null)
		{
			return matches;
		}
		double tol = _config.GeometryTolerance;
		foreach (PlannedDimension dimension in plan.Dimensions)
		{
			if (!IsEnvelopeLocalGeometryRole(dimension.DebugRole))
			{
				continue;
			}
			bool horizontalSide = dimension.Side == DimensionSide.Top || dimension.Side == DimensionSide.Bottom;
			bool verticalSide = dimension.Side == DimensionSide.Left || dimension.Side == DimensionSide.Right;
			bool onHorizontalEnvelope = dimension.Orientation == DimensionOrientation.Horizontal
				&& horizontalSide
				&& (Math.Abs(dimension.FirstPoint.Y - outline.MinY) <= tol
					&& Math.Abs(dimension.SecondPoint.Y - outline.MinY) <= tol
					|| Math.Abs(dimension.FirstPoint.Y - outline.MaxY) <= tol
					&& Math.Abs(dimension.SecondPoint.Y - outline.MaxY) <= tol);
			bool onVerticalEnvelope = dimension.Orientation == DimensionOrientation.Vertical
				&& verticalSide
				&& (Math.Abs(dimension.FirstPoint.X - outline.MinX) <= tol
					&& Math.Abs(dimension.SecondPoint.X - outline.MinX) <= tol
					|| Math.Abs(dimension.FirstPoint.X - outline.MaxX) <= tol
					&& Math.Abs(dimension.SecondPoint.X - outline.MaxX) <= tol);
			if (!onHorizontalEnvelope && !onVerticalEnvelope)
			{
				continue;
			}
			matches.Add(dimension);
		}
		return matches;
	}

	private static void SuppressLocalGeometryOnOverallEnvelope(
		DimensionPlan plan,
		IEnumerable<PlannedDimension> dimensions)
	{
		if (plan == null || dimensions == null)
		{
			return;
		}
		foreach (PlannedDimension dimension in dimensions)
		{
			plan.MarkSuppressed(dimension, SuppressReason.LocalGeometryOnOverallEnvelope);
			plan.Dimensions.Remove(dimension);
		}
	}

	private static bool IsEnvelopeLocalGeometryRole(string debugRole)
	{
		return IsStructureWidthOrHeightRole(debugRole)
			|| string.Equals(debugRole, "OutlineSegment", StringComparison.Ordinal)
			|| string.Equals(debugRole, "TopChamferedStepWidth", StringComparison.Ordinal)
			|| string.Equals(debugRole, "RightChamferedStepHeight", StringComparison.Ordinal);
	}

	private void SuppressBottomProtrusionInnerRemainders(DimensionPlan plan, OutlineFeature2D outline)
	{
		if (plan == null || outline == null)
		{
			return;
		}
		List<Segment2D> innerLedges = new List<Segment2D>();
		foreach (Segment2D segment in outline.Segments.Where((Segment2D s) => s != null
			&& s.IsHorizontal(_config.GeometryTolerance)
			&& Math.Abs(s.MinY - outline.MinY) <= _config.GeometryTolerance))
		{
			if (TryGetBottomProtrusionInnerLedge(outline, segment, out Segment2D innerLedge))
			{
				innerLedges.Add(innerLedge);
			}
		}
		for (int num = plan.Dimensions.Count - 1; num >= 0; num--)
		{
			PlannedDimension candidate = plan.Dimensions[num];
			if (candidate.Kind != DimensionKind.Normal || candidate.Orientation != DimensionOrientation.Horizontal)
			{
				continue;
			}
			bool isInnerOutline = string.Equals(candidate.DebugRole, "OutlineSegment", StringComparison.Ordinal)
				&& innerLedges.Any((Segment2D ledge) => HasSameSegmentEndpoints(ledge, candidate.FirstPoint, candidate.SecondPoint));
			bool isInnerBottomStructure = string.Equals(candidate.DebugRole, "BottomStructWidth", StringComparison.Ordinal)
				&& innerLedges.Any((Segment2D ledge) => HasSameHorizontalInterval(candidate, ledge));
			if (!isInnerOutline && !isInnerBottomStructure)
			{
				continue;
			}
			plan.MarkSuppressed(candidate, SuppressReason.BottomProtrusionInnerRemainder);
			plan.Dimensions.RemoveAt(num);
		}
	}

	private bool IsBottomProtrusionWidth(PlannedDimension dimension, OutlineFeature2D outline)
	{
		if (dimension == null || outline == null
			|| !string.Equals(dimension.DebugRole, "BottomStructWidth", StringComparison.Ordinal)
			|| dimension.Orientation != DimensionOrientation.Horizontal)
		{
			return false;
		}
		Segment2D bottomSegment = outline.Segments.FirstOrDefault((Segment2D segment) => segment != null
			&& segment.IsHorizontal(_config.GeometryTolerance)
			&& Math.Abs(segment.MinY - outline.MinY) <= _config.GeometryTolerance
			&& HasSameSegmentEndpoints(segment, dimension.FirstPoint, dimension.SecondPoint));
		if (bottomSegment == null || !TryGetBottomProtrusionRiserTop(outline, bottomSegment, out Point2D riserTop))
		{
			return false;
		}
		return GetBottomProtrusionInnerLedge(outline, riserTop) != null
			|| outline.Fillets.Any((FilletFeature2D fillet) => PointsEqual(fillet.StartPoint, riserTop) || PointsEqual(fillet.EndPoint, riserTop));
	}

	private bool IsBottomOuterArmResidual(PlannedDimension dimension, OutlineFeature2D outline, DimensionPlan plan)
	{
		if (dimension == null || outline == null
			|| dimension.Orientation != DimensionOrientation.Horizontal)
		{
			return false;
		}
		double tol = _config.GeometryTolerance;
		bool isBottomStructure = string.Equals(dimension.DebugRole, "BottomStructWidth", StringComparison.Ordinal);
		bool isInnerOutline = string.Equals(dimension.DebugRole, "OutlineSegment", StringComparison.Ordinal);
		if (!isBottomStructure && !isInnerOutline)
		{
			return false;
		}
		if (Math.Abs(dimension.FirstPoint.Y - dimension.SecondPoint.Y) > tol
			|| Math.Abs(Math.Max(dimension.FirstPoint.X, dimension.SecondPoint.X) - outline.MaxX) > tol
			|| IsBottomProtrusionWidth(dimension, outline))
		{
			return false;
		}
		if (isInnerOutline)
		{
			double y = dimension.FirstPoint.Y;
			double minX = Math.Min(dimension.FirstPoint.X, dimension.SecondPoint.X);
			if (y <= outline.MinY + tol || plan == null)
			{
				return false;
			}
			Point2D inner = dimension.FirstPoint.X <= dimension.SecondPoint.X
				? dimension.FirstPoint
				: dimension.SecondPoint;
			foreach (PlannedDimension root in plan.Dimensions.Where((PlannedDimension candidate) =>
				candidate.Orientation == DimensionOrientation.Horizontal
				&& candidate.Side == DimensionSide.Bottom
				&& IsDatumRootedOuterStepDimension(candidate, outline)))
			{
				double rootMaxX = Math.Max(root.FirstPoint.X, root.SecondPoint.X);
				if (rootMaxX >= minX - tol)
				{
					continue;
				}
				foreach (Arc2D transition in outline.Arcs.Where((Arc2D arc) => arc != null
					&& (PointsEqual(arc.Start, inner) || PointsEqual(arc.End, inner))))
				{
					Point2D other = PointsEqual(transition.Start, inner) ? transition.End : transition.Start;
					if (Math.Abs(other.X - rootMaxX) <= tol
						&& other.Y > outline.MinY + tol
						&& IsTooSmallStructureSpan(Math.Abs(inner.X - other.X))
						&& IsTooSmallStructureSpan(Math.Abs(inner.Y - other.Y))
						&& HasContinuousStraightOutlineEdge(outline, false, rootMaxX, outline.MinY, other.Y))
					{
						return true;
					}
				}
				foreach (Segment2D transition in outline.Segments.Where((Segment2D segment) => segment != null
					&& !segment.IsArcChord
					&& !segment.IsHorizontal(tol)
					&& !segment.IsVertical(tol)
					&& (PointsEqual(segment.Start, inner) || PointsEqual(segment.End, inner))))
				{
					Point2D other = PointsEqual(transition.Start, inner) ? transition.End : transition.Start;
					if (Math.Abs(other.X - rootMaxX) <= tol
						&& other.Y > y + tol
						&& IsTooSmallStructureSpan(Math.Abs(inner.X - other.X))
						&& HasContinuousStraightOutlineEdge(outline, false, rootMaxX, outline.MinY, other.Y))
					{
						return true;
					}
				}
			}
			return false;
		}
		if (Math.Abs(dimension.FirstPoint.Y - outline.MinY) > tol)
		{
			return false;
		}
		return outline.Segments.Any((Segment2D segment) => segment != null
			&& !segment.IsArcChord
			&& segment.IsHorizontal(tol)
			&& segment.MinY > dimension.FirstPoint.Y + tol
			&& HasSameHorizontalInterval(dimension, segment));
	}

	private bool TryGetBottomProtrusionInnerLedge(OutlineFeature2D outline, Segment2D bottomSegment, out Segment2D innerLedge)
	{
		innerLedge = null;
		if (!TryGetBottomProtrusionRiserTop(outline, bottomSegment, out Point2D riserTop))
		{
			return false;
		}
		innerLedge = GetBottomProtrusionInnerLedge(outline, riserTop);
		return innerLedge != null;
	}

	private bool TryGetBottomProtrusionRiserTop(OutlineFeature2D outline, Segment2D bottomSegment, out Point2D riserTop)
	{
		riserTop = default(Point2D);
		if (outline == null || bottomSegment == null)
		{
			return false;
		}
		// ponytail: left-bottom protrusion only; add mirrored right-bottom matching when a repro needs it.
		StructureEndpointSpan span = _structureEndpointRules.ResolveHorizontalStepBoundarySpan(outline, bottomSegment, DimensionSide.Bottom);
		if (!span.WasResolved || !HasSameSegmentEndpoints(bottomSegment, span.FirstPoint, span.SecondPoint))
		{
			return false;
		}
		Segment2D riser = outline.Segments.FirstOrDefault((Segment2D segment) => segment != null
			&& segment.IsVertical(_config.GeometryTolerance)
			&& (PointsEqual(segment.Start, span.SecondPoint) || PointsEqual(segment.End, span.SecondPoint)));
		if (riser == null)
		{
			return false;
		}
		riserTop = PointsEqual(riser.Start, span.SecondPoint) ? riser.End : riser.Start;
		if (riserTop.Y <= outline.MinY + _config.GeometryTolerance || riserTop.Y >= outline.MaxY - _config.GeometryTolerance)
		{
			return false;
		}
		return true;
	}

	private Segment2D GetBottomProtrusionInnerLedge(OutlineFeature2D outline, Point2D riserTop)
	{
		return outline.Segments.FirstOrDefault((Segment2D segment) => segment != null
			&& segment.IsHorizontal(_config.GeometryTolerance)
			&& (PointsEqual(segment.Start, riserTop) && segment.End.X > riserTop.X + _config.GeometryTolerance
				|| PointsEqual(segment.End, riserTop) && segment.Start.X > riserTop.X + _config.GeometryTolerance));
	}

	private bool HasSameSegmentEndpoints(Segment2D segment, Point2D firstPoint, Point2D secondPoint)
	{
		return (PointsEqual(segment.Start, firstPoint) && PointsEqual(segment.End, secondPoint))
			|| (PointsEqual(segment.Start, secondPoint) && PointsEqual(segment.End, firstPoint));
	}

	private bool HasSameHorizontalInterval(PlannedDimension dimension, Segment2D segment)
	{
		return Math.Abs(Math.Min(dimension.FirstPoint.X, dimension.SecondPoint.X) - segment.MinX) <= _config.GeometryTolerance
			&& Math.Abs(Math.Max(dimension.FirstPoint.X, dimension.SecondPoint.X) - segment.MaxX) <= _config.GeometryTolerance;
	}

	private bool HasContinuousStraightOutlineEdge(OutlineFeature2D outline, bool horizontal, double fixedCoordinate, double minimum, double maximum)
	{
		double tol = _config.GeometryTolerance;
		double covered = minimum;
		foreach (Tuple<double, double> interval in outline.Segments
			.Where((Segment2D segment) => segment != null
				&& !segment.IsArcChord
				&& (horizontal ? segment.IsHorizontal(tol) : segment.IsVertical(tol))
				&& Math.Abs((horizontal ? segment.MinY : segment.MinX) - fixedCoordinate) <= tol)
			.Select((Segment2D segment) => horizontal
				? Tuple.Create(segment.MinX, segment.MaxX)
				: Tuple.Create(segment.MinY, segment.MaxY))
			.Where((Tuple<double, double> interval) => interval.Item2 >= minimum - tol && interval.Item1 <= maximum + tol)
			.OrderBy((Tuple<double, double> interval) => interval.Item1))
		{
			if (interval.Item2 < covered - tol)
			{
				continue;
			}
			if (interval.Item1 > covered + tol)
			{
				return false;
			}
			covered = Math.Max(covered, interval.Item2);
			if (covered >= maximum - tol)
			{
				return true;
			}
		}
		return false;
	}

	private bool IsDatumRootedOuterStepDimension(PlannedDimension dimension, OutlineFeature2D outline)
	{
		if (dimension == null || outline == null || dimension.Kind != DimensionKind.Normal)
		{
			return false;
		}
		// ponytail: datum convention is Bottom/Left; add Top/Right only when a concrete repro requires it.
		double tol = _config.GeometryTolerance;
		if (dimension.Orientation == DimensionOrientation.Horizontal && dimension.Side == DimensionSide.Bottom)
		{
			double minX = Math.Min(dimension.FirstPoint.X, dimension.SecondPoint.X);
			double maxX = Math.Max(dimension.FirstPoint.X, dimension.SecondPoint.X);
			if (Math.Abs(dimension.FirstPoint.Y - outline.MinY) > tol
				|| Math.Abs(dimension.SecondPoint.Y - outline.MinY) > tol
				|| Math.Abs(minX - outline.MinX) > tol
				|| maxX >= outline.MaxX - tol)
			{
				return false;
			}
			Point2D inner = new Point2D(maxX, outline.MinY);
			return HasContinuousStraightOutlineEdge(outline, true, outline.MinY, minX, maxX)
				&& outline.Segments.Any((Segment2D segment) => segment != null
					&& !segment.IsArcChord
					&& segment.IsVertical(tol)
					&& (PointsEqual(segment.Start, inner) && segment.End.Y > inner.Y + tol
						|| PointsEqual(segment.End, inner) && segment.Start.Y > inner.Y + tol));
		}
		if (dimension.Orientation != DimensionOrientation.Vertical || dimension.Side != DimensionSide.Left)
		{
			return false;
		}
		double minY = Math.Min(dimension.FirstPoint.Y, dimension.SecondPoint.Y);
		double maxY = Math.Max(dimension.FirstPoint.Y, dimension.SecondPoint.Y);
		if (Math.Abs(dimension.FirstPoint.X - outline.MinX) > tol
			|| Math.Abs(dimension.SecondPoint.X - outline.MinX) > tol
			|| Math.Abs(minY - outline.MinY) > tol
			|| maxY >= outline.MaxY - tol)
		{
			return false;
		}
		Point2D upper = new Point2D(outline.MinX, maxY);
		return HasContinuousStraightOutlineEdge(outline, false, outline.MinX, minY, maxY)
			&& outline.Segments.Any((Segment2D segment) => segment != null
				&& !segment.IsArcChord
				&& segment.IsHorizontal(tol)
				&& segment.LengthX < outline.Width - tol
				&& (PointsEqual(segment.Start, upper) && segment.End.X > upper.X + tol
					|| PointsEqual(segment.End, upper) && segment.Start.X > upper.X + tol));
	}

	private void SuppressDatumRootedOuterStepComplements(DimensionPlan plan, OutlineFeature2D outline, bool horizontal)
	{
		PlannedDimension overall = plan.Dimensions.FirstOrDefault((PlannedDimension d) =>
			horizontal ? d.Kind == DimensionKind.OverallWidth : d.Kind == DimensionKind.OverallHeight);
		if (overall == null)
		{
			return;
		}
		DimensionOrientation orientation = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
		List<PlannedDimension> roots = plan.Dimensions
			.Where((PlannedDimension d) => d.Orientation == orientation && IsDatumRootedOuterStepDimension(d, outline))
			.ToList();
		if (roots.Count == 0)
		{
			return;
		}
		HashSet<PlannedDimension> toSuppress = new HashSet<PlannedDimension>();
		foreach (PlannedDimension candidate in plan.Dimensions.Where((PlannedDimension d) =>
			d.Kind == DimensionKind.Normal
			&& d.Orientation == orientation
			&& string.Equals(d.DebugRole, "OutlineSegment", StringComparison.Ordinal)
			&& !roots.Contains(d)))
		{
			PlannedDimension root = roots.FirstOrDefault((PlannedDimension item) =>
				FormsCompleteOverallPartition(item, candidate, overall, horizontal));
			if (root != null)
			{
				toSuppress.Add(candidate);
				if (horizontal
					&& AreCollinearStructurePartners(root, candidate, horizontal, _config.GeometryTolerance))
				{
					toSuppress.Add(root);
				}
			}
		}
		for (int i = plan.Dimensions.Count - 1; i >= 0; i--)
		{
			PlannedDimension candidate = plan.Dimensions[i];
			if (!toSuppress.Contains(candidate))
			{
				continue;
			}
			plan.MarkSuppressed(candidate, SuppressReason.ComplementaryOutlineRemainder);
			plan.Dimensions.RemoveAt(i);
		}
	}

	private void SuppressComplementaryOutlineRemainders(DimensionPlan plan, DimensionSide side, bool horizontal)
	{
		PlannedDimension overall = plan.Dimensions.FirstOrDefault((PlannedDimension d) => horizontal ? (d.Kind == DimensionKind.OverallWidth) : (d.Kind == DimensionKind.OverallHeight));
		if (overall == null)
		{
			return;
		}
		List<PlannedDimension> source = plan.Dimensions.Where((PlannedDimension d) => d.Kind == DimensionKind.Normal && d.Side == side).ToList();
		for (int num = plan.Dimensions.Count - 1; num >= 0; num--)
		{
			PlannedDimension candidate = plan.Dimensions[num];
			if (candidate.Kind == DimensionKind.Normal && candidate.Side == side)
			{
				double span = GetDimensionSpan(candidate, horizontal);
				if (source.Any((PlannedDimension other) => other != candidate
					&& GetDimensionSpan(other, horizontal) < span - _config.GeometryTolerance
					&& FormsCompleteOverallPartition(candidate, other, overall, horizontal)))
				{
					plan.MarkSuppressed(candidate, SuppressReason.ComplementaryOutlineRemainder);
					plan.Dimensions.RemoveAt(num);
				}
			}
		}
	}

	/// <summary>
	/// Suppress OutlineSegment dims that re-partition the overall envelope on the same side.
	/// Covers both 2-piece pairs (e.g. [0,25]+[25,257]) and multi-piece chains
	/// (e.g. [0,9]+[9,20]+[20,91] = overall 91). Requires real contiguous cover of overall
	/// (abut, no gap/overlap) — not mere span sums. Datum-rooted Bottom/Left steps are excluded;
	/// all other pieces in the covering chain are dropped.
	/// </summary>
	private void SuppressOutlineSegmentsThatPartitionOverall(DimensionPlan plan, OutlineFeature2D outline, DimensionSide side, bool horizontal)
	{
		PlannedDimension overall = plan.Dimensions.FirstOrDefault((PlannedDimension d) => horizontal ? (d.Kind == DimensionKind.OverallWidth) : (d.Kind == DimensionKind.OverallHeight));
		if (overall == null)
		{
			return;
		}
		DimensionOrientation expected = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
		if (overall.Orientation != expected)
		{
			return;
		}
		List<PlannedDimension> outlineSegments = plan.Dimensions
			.Where((PlannedDimension d) => d.Kind == DimensionKind.Normal
				&& d.Side == side
				&& d.Orientation == expected
				&& string.Equals(d.DebugRole, "OutlineSegment", StringComparison.Ordinal))
			.ToList();
		if (outlineSegments.Count < 2)
		{
			return;
		}
		Tuple<double, double> overallInterval = ComputeArrowInterval(overall, horizontal);
		List<DimensionDeduplicationItem> items = new List<DimensionDeduplicationItem>(outlineSegments.Count);
		Dictionary<DimensionDeduplicationItem, PlannedDimension> itemToDim = new Dictionary<DimensionDeduplicationItem, PlannedDimension>();
		foreach (PlannedDimension segment in outlineSegments)
		{
			DimensionDeduplicationItem item = ToDeduplicationItem(segment);
			items.Add(item);
			itemToDim[item] = segment;
		}
		IList<DimensionDeduplicationItem> cover = _dimensionDeduplicationRules.FindCompleteOverallPartitionChain(
			items,
			overallInterval.Item1,
			overallInterval.Item2,
			horizontal);
		if (cover == null || cover.Count < 2)
		{
			return;
		}
		HashSet<PlannedDimension> toSuppress = new HashSet<PlannedDimension>();
		foreach (DimensionDeduplicationItem coverItem in cover)
		{
			if (itemToDim.TryGetValue(coverItem, out PlannedDimension match))
			{
				toSuppress.Add(match);
			}
		}
		if (toSuppress.Count < 2)
		{
			return;
		}
		for (int num = plan.Dimensions.Count - 1; num >= 0; num--)
		{
			PlannedDimension candidate = plan.Dimensions[num];
			if (!toSuppress.Contains(candidate))
			{
				continue;
			}
			plan.MarkSuppressed(candidate, SuppressReason.OutlineSegmentOverallPartition);
			plan.Dimensions.RemoveAt(num);
		}
	}

	/// <summary>
	/// Option-1 outer envelope rule: suppress OutlineSegment fragments that lie on the
	/// overall envelope (top/bottom Y or left/right X) when Overall already exists.
	/// Also suppress Structure dims that are true duplicates of those envelope OS tips:
	/// same measurement interval, same Side, and collinear (same Y for horizontal) —
	/// e.g. BottomStructWidth 10 == bottom OutlineSegment 10 on the outer tip.
	/// Does not co-suppress opposite-side / non-collinear structures that only share a 1D
	/// interval (e.g. top or internal structure [65,75] vs bottom tip OS [65,75]).
	/// Left/RightStructHeight never co-suppressed here.
	/// </summary>
	internal void SuppressOutlineSegmentsOnOverallEnvelope(DimensionPlan plan)
	{
		SuppressOutlineSegmentsOnOverallEnvelope(plan, null);
	}

	private void SuppressOutlineSegmentsOnOverallEnvelope(DimensionPlan plan, OutlineFeature2D outline)
	{
		PlannedDimension overallWidth = plan.Dimensions.FirstOrDefault((PlannedDimension d) => d.Kind == DimensionKind.OverallWidth);
		PlannedDimension overallHeight = plan.Dimensions.FirstOrDefault((PlannedDimension d) => d.Kind == DimensionKind.OverallHeight);
		if (overallWidth == null && overallHeight == null)
		{
			return;
		}
		if (!TryGetOverallEnvelopeBounds(overallWidth, overallHeight, out double minX, out double maxX, out double minY, out double maxY))
		{
			return;
		}
		List<PlannedDimension> envelopeOutlineSegments = plan.Dimensions
			.Where((PlannedDimension d) => d.Kind == DimensionKind.Normal
				&& string.Equals(d.DebugRole, "OutlineSegment", StringComparison.Ordinal)
				&& !IsDatumRootedOuterStepDimension(d, outline)
				&& IsDimensionOnOverallEnvelope(d, minX, maxX, minY, maxY))
			.ToList();
		List<PlannedDimension> bottomOuterArmResiduals = plan.Dimensions
			.Where((PlannedDimension d) => IsBottomOuterArmResidual(d, outline, plan))
			.ToList();
		if (envelopeOutlineSegments.Count == 0 && bottomOuterArmResiduals.Count == 0)
		{
			return;
		}
		HashSet<PlannedDimension> toSuppress = new HashSet<PlannedDimension>(envelopeOutlineSegments);
		foreach (PlannedDimension residual in bottomOuterArmResiduals)
		{
			toSuppress.Add(residual);
		}
		// Co-suppress same-edge duplicates. A lower rectangular arm remainder is redundant
		// even when long; real bottom protrusions and substantial step faces stay.
		// Left/RightStructHeight never co-suppressed here (short arm height on MaxX, etc.).
		double overallWidthSpan = maxX - minX;
		double tol = _config.GeometryTolerance;
		foreach (PlannedDimension structure in plan.Dimensions
			.Where((PlannedDimension d) => d.Kind == DimensionKind.Normal
				&& IsHorizontalStructureWidthRole(d.DebugRole)))
		{
			bool matchesEnvelopeOutline = envelopeOutlineSegments.Any((PlannedDimension os) =>
				os.Orientation == DimensionOrientation.Horizontal
				&& structure.Side == os.Side
				&& AreCollinearStructurePartners(structure, os, horizontal: true, tol)
				&& IsSameMeasurementInterval(structure, os, horizontal: true));
			if (!matchesEnvelopeOutline)
			{
				continue;
			}
			if (IsBottomOuterArmResidual(structure, outline, plan))
			{
				toSuppress.Add(structure);
				continue;
			}
			if (IsBottomProtrusionWidth(structure, outline))
			{
				continue;
			}
			double structureSpan = GetDimensionSpan(structure, horizontal: true);
			// Short tip: less than half overall width. Step ledges are typically larger.
			if (structureSpan >= overallWidthSpan * 0.5 - tol)
			{
				continue;
			}
			toSuppress.Add(structure);
		}
		// Same measurement interval as an envelope OS fragment, even if not on the envelope
		// itself (e.g. inner vertical at X=mid labeled Side=Right that matches outer tip OS@MaxX).
		foreach (PlannedDimension otherOs in plan.Dimensions
			.Where((PlannedDimension d) => d.Kind == DimensionKind.Normal
				&& string.Equals(d.DebugRole, "OutlineSegment", StringComparison.Ordinal)
				&& !toSuppress.Contains(d)))
		{
			bool horizontal = otherOs.Orientation == DimensionOrientation.Horizontal;
			if (envelopeOutlineSegments.Any((PlannedDimension envOs) =>
				envOs.Orientation == otherOs.Orientation
				&& IsSameMeasurementInterval(otherOs, envOs, horizontal)))
			{
				toSuppress.Add(otherOs);
			}
		}
		for (int num = plan.Dimensions.Count - 1; num >= 0; num--)
		{
			PlannedDimension candidate = plan.Dimensions[num];
			if (!toSuppress.Contains(candidate))
			{
				continue;
			}
			string reason;
			if (IsBottomOuterArmResidual(candidate, outline, plan))
			{
				reason = SuppressReason.StructureDuplicateOfEnvelopeOutlineSegment;
			}
			else if (string.Equals(candidate.DebugRole, "OutlineSegment", StringComparison.Ordinal))
			{
				reason = envelopeOutlineSegments.Contains(candidate)
					? SuppressReason.OutlineSegmentOnOverallEnvelope
					: SuppressReason.OutlineSegmentSameIntervalAsEnvelopeFragment;
			}
			else
			{
				reason = SuppressReason.StructureDuplicateOfEnvelopeOutlineSegment;
			}
			plan.MarkSuppressed(candidate, reason);
			plan.Dimensions.RemoveAt(num);
		}
	}

	private bool IsSameMeasurementInterval(PlannedDimension a, PlannedDimension b, bool horizontal)
	{
		if (a == null || b == null)
		{
			return false;
		}
		Tuple<double, double> ia = ComputeArrowInterval(a, horizontal);
		Tuple<double, double> ib = ComputeArrowInterval(b, horizontal);
		double tol = _config.GeometryTolerance;
		return Math.Abs(ia.Item1 - ib.Item1) <= tol && Math.Abs(ia.Item2 - ib.Item2) <= tol;
	}

	private bool TryGetOverallEnvelopeBounds(
		PlannedDimension overallWidth,
		PlannedDimension overallHeight,
		out double minX,
		out double maxX,
		out double minY,
		out double maxY)
	{
		minX = maxX = minY = maxY = 0.0;
		bool hasX = false;
		bool hasY = false;
		// Prefer OverallWidth for X span and OverallHeight for Y span (attachment sides differ).
		if (overallWidth != null)
		{
			minX = Math.Min(overallWidth.FirstPoint.X, overallWidth.SecondPoint.X);
			maxX = Math.Max(overallWidth.FirstPoint.X, overallWidth.SecondPoint.X);
			hasX = maxX - minX > _config.GeometryTolerance;
		}
		if (overallHeight != null)
		{
			minY = Math.Min(overallHeight.FirstPoint.Y, overallHeight.SecondPoint.Y);
			maxY = Math.Max(overallHeight.FirstPoint.Y, overallHeight.SecondPoint.Y);
			hasY = maxY - minY > _config.GeometryTolerance;
			if (!hasX)
			{
				minX = Math.Min(overallHeight.FirstPoint.X, overallHeight.SecondPoint.X);
				maxX = Math.Max(overallHeight.FirstPoint.X, overallHeight.SecondPoint.X);
				hasX = maxX - minX > _config.GeometryTolerance;
			}
		}
		if (overallWidth != null && !hasY)
		{
			// Only width present: both grips share one envelope Y — still enough for that edge.
			minY = Math.Min(overallWidth.FirstPoint.Y, overallWidth.SecondPoint.Y);
			maxY = Math.Max(overallWidth.FirstPoint.Y, overallWidth.SecondPoint.Y);
			hasY = true;
		}
		return hasX && hasY;
	}

	private bool IsDimensionOnOverallEnvelope(
		PlannedDimension dim,
		double minX,
		double maxX,
		double minY,
		double maxY)
	{
		if (dim == null)
		{
			return false;
		}
		double tol = _config.GeometryTolerance;
		if (dim.Orientation == DimensionOrientation.Horizontal)
		{
			double x1 = Math.Min(dim.FirstPoint.X, dim.SecondPoint.X);
			double x2 = Math.Max(dim.FirstPoint.X, dim.SecondPoint.X);
			double span = x2 - x1;
			// Full-span outer edge is Overall itself; only suppress proper fragments.
			if (span >= maxX - minX - tol)
			{
				return false;
			}
			if (x1 < minX - tol || x2 > maxX + tol)
			{
				return false;
			}
			bool onBottom = Math.Abs(dim.FirstPoint.Y - minY) <= tol && Math.Abs(dim.SecondPoint.Y - minY) <= tol;
			bool onTop = Math.Abs(dim.FirstPoint.Y - maxY) <= tol && Math.Abs(dim.SecondPoint.Y - maxY) <= tol;
			return onBottom || onTop;
		}
		if (dim.Orientation == DimensionOrientation.Vertical)
		{
			double y1 = Math.Min(dim.FirstPoint.Y, dim.SecondPoint.Y);
			double y2 = Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y);
			double span = y2 - y1;
			if (span >= maxY - minY - tol)
			{
				return false;
			}
			if (y1 < minY - tol || y2 > maxY + tol)
			{
				return false;
			}
			bool onLeft = Math.Abs(dim.FirstPoint.X - minX) <= tol && Math.Abs(dim.SecondPoint.X - minX) <= tol;
			bool onRight = Math.Abs(dim.FirstPoint.X - maxX) <= tol && Math.Abs(dim.SecondPoint.X - maxX) <= tol;
			return onLeft || onRight;
		}
		return false;
	}

	/// <summary>
	/// Drop Left/Right structure heights that restate OverallHeight (was Right-only).
	/// Kept entry name for call-site compatibility.
	/// </summary>
	private void SuppressRightStructureHeightsDuplicatingOverallHeight(DimensionPlan plan)
	{
		SuppressStructureHeightsDuplicatingOverallHeight(plan);
	}

	private void SuppressStructureHeightsDuplicatingOverallHeight(DimensionPlan plan)
	{
		List<PlannedDimension> list = plan.Dimensions.Where((PlannedDimension dim) => dim.Kind == DimensionKind.OverallHeight).ToList();
		if (list.Count == 0)
		{
			return;
		}
		for (int num = plan.Dimensions.Count - 1; num >= 0; num--)
		{
			PlannedDimension cand = plan.Dimensions[num];
			bool isRight = IsRightStructureHeight(cand);
			bool isLeft = IsLeftStructureHeight(cand);
			if (!isRight && !isLeft)
			{
				continue;
			}
			if (!list.Any((PlannedDimension overall) => IsSameVerticalInterval(cand, overall)))
			{
				continue;
			}
			string reason = isRight
				? SuppressReason.RightStructureHeightDuplicatesOverallHeight
				: SuppressReason.LeftStructureHeightDuplicatesOverallHeight;
			plan.MarkSuppressed(cand, reason);
			plan.Dimensions.RemoveAt(num);
		}
	}

	private void SuppressLeftStructureHeightsCoveredByRight(DimensionPlan plan)
	{
		List<PlannedDimension> list = plan.Dimensions.Where(IsRightStructureHeight).ToList();
		if (list.Count == 0)
		{
			return;
		}
		for (int num = plan.Dimensions.Count - 1; num >= 0; num--)
		{
			PlannedDimension left = plan.Dimensions[num];
			if (IsLeftStructureHeight(left) && list.Any((PlannedDimension right) =>
				_dimensionDeduplicationRules.IsLeftStructureHeightCoveredByRight(
					ToDeduplicationItem(left), ToDeduplicationItem(right))))
			{
				plan.MarkSuppressed(left, SuppressReason.LeftStructureHeightCoveredByRight);
				plan.Dimensions.RemoveAt(num);
			}
		}
	}

	/// <summary>
	/// Drop secondary-side vertical OutlineSegments that restate the primary structure-height
	/// stack or only cover the residual of OverallHeight minus that stack (step symmetry).
	/// Primary side = larger total Left/Right structure-height span (tie -> Right).
	/// </summary>

	/// <summary>
	/// After structure wins mirror vs OutlineSegment, short outer-edge structure heights can
	/// remain as overall residual tips (e.g. two RightStructHeight 5 on a 45-high part).
	/// Suppress same-side abutting structure-height chains that are only tip noise:
	/// no piece spans &gt;= 30% of OverallHeight and the chain covers &lt; 50% of overall.
	/// Keeps real steps (L-arm ~40% of height, left-step 50+30 chain).
	/// </summary>
	internal void SuppressOrphanOuterVerticalStructureHeightTips(DimensionPlan plan)
	{
		PlannedDimension overall = plan.Dimensions.FirstOrDefault((PlannedDimension d) => d.Kind == DimensionKind.OverallHeight);
		if (overall == null)
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		double overallSpan = GetDimensionSpan(overall, horizontal: false);
		if (overallSpan <= tol)
		{
			return;
		}
		double minPieceToKeep = overallSpan * 0.3;
		double minChainToKeep = overallSpan * 0.5;
		foreach (DimensionSide side in new DimensionSide[] { DimensionSide.Left, DimensionSide.Right })
		{
			List<PlannedDimension> heights = plan.Dimensions
				.Where((PlannedDimension d) => d.Kind == DimensionKind.Normal
					&& d.Orientation == DimensionOrientation.Vertical
					&& d.Side == side
					&& (IsLeftStructureHeight(d) || IsRightStructureHeight(d)))
				.ToList();
			if (heights.Count == 0)
			{
				continue;
			}
			List<List<PlannedDimension>> chains = BuildAbuttingVerticalStructureChains(heights, tol);
			HashSet<PlannedDimension> toSuppress = new HashSet<PlannedDimension>();
			foreach (List<PlannedDimension> chain in chains)
			{
				double maxPiece = chain.Max((PlannedDimension d) => GetDimensionSpan(d, horizontal: false));
				List<Tuple<double, double>> ivs = chain
					.Select((PlannedDimension d) => ComputeArrowInterval(d, horizontal: false))
					.ToList();
				List<Tuple<double, double>> merged = MergeVerticalIntervals(ivs, tol);
				double chainSpan = merged.Sum((Tuple<double, double> m) => m.Item2 - m.Item1);
				if (maxPiece + tol >= minPieceToKeep || chainSpan + tol >= minChainToKeep)
				{
					continue;
				}
				foreach (PlannedDimension d in chain)
				{
					toSuppress.Add(d);
				}
			}
			if (toSuppress.Count == 0)
			{
				continue;
			}
			for (int num = plan.Dimensions.Count - 1; num >= 0; num--)
			{
				PlannedDimension cand = plan.Dimensions[num];
				if (!toSuppress.Contains(cand))
				{
					continue;
				}
				plan.MarkSuppressed(cand, SuppressReason.OrphanOuterVerticalStructureHeightTip);
				plan.Dimensions.RemoveAt(num);
			}
		}
	}

	internal static List<List<PlannedDimension>> BuildAbuttingVerticalStructureChains(IList<PlannedDimension> heights, double tol)
	{
		List<PlannedDimension> ordered = heights
			.OrderBy((PlannedDimension d) => ComputeArrowInterval(d, horizontal: false).Item1)
			.ToList();
		List<List<PlannedDimension>> chains = new List<List<PlannedDimension>>();
		List<PlannedDimension> current = new List<PlannedDimension>();
		double currentMax = double.NegativeInfinity;
		foreach (PlannedDimension d in ordered)
		{
			Tuple<double, double> iv = ComputeArrowInterval(d, horizontal: false);
			if (current.Count == 0)
			{
				current.Add(d);
				currentMax = iv.Item2;
				continue;
			}
			if (iv.Item1 <= currentMax + tol)
			{
				current.Add(d);
				currentMax = Math.Max(currentMax, iv.Item2);
			}
			else
			{
				chains.Add(current);
				current = new List<PlannedDimension> { d };
				currentMax = iv.Item2;
			}
		}
		if (current.Count > 0)
		{
			chains.Add(current);
		}
		return chains;
	}

	internal void SuppressSecondaryVerticalOutlineSegmentsRedundantWithPrimaryStructureStack(DimensionPlan plan)
	{
		PlannedDimension overall = plan.Dimensions.FirstOrDefault((PlannedDimension d) => d.Kind == DimensionKind.OverallHeight);
		if (overall == null)
		{
			return;
		}
		List<PlannedDimension> leftHeights = plan.Dimensions.Where(IsLeftStructureHeight).ToList();
		List<PlannedDimension> rightHeights = plan.Dimensions.Where(IsRightStructureHeight).ToList();
		if (leftHeights.Count == 0 && rightHeights.Count == 0)
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		double leftSpan = leftHeights.Sum((PlannedDimension d) => GetDimensionSpan(d, horizontal: false));
		double rightSpan = rightHeights.Sum((PlannedDimension d) => GetDimensionSpan(d, horizontal: false));
		bool primaryIsLeft;
		if (leftSpan > rightSpan + tol)
		{
			primaryIsLeft = true;
		}
		else if (rightSpan > leftSpan + tol)
		{
			primaryIsLeft = false;
		}
		else if (leftHeights.Count != rightHeights.Count)
		{
			primaryIsLeft = leftHeights.Count > rightHeights.Count;
		}
		else
		{
			// Historical tie-break: prefer Right as primary structure side.
			primaryIsLeft = false;
		}
		List<PlannedDimension> primary = primaryIsLeft ? leftHeights : rightHeights;
		if (primary.Count == 0)
		{
			return;
		}
		DimensionSide secondarySide = primaryIsLeft ? DimensionSide.Right : DimensionSide.Left;
		Tuple<double, double> overallIv = ComputeArrowInterval(overall, horizontal: false);
		List<Tuple<double, double>> primaryIvs = primary
			.Select((PlannedDimension d) => ComputeArrowInterval(d, horizontal: false))
			.ToList();
		List<Tuple<double, double>> merged = MergeVerticalIntervals(primaryIvs, tol);
		List<Tuple<double, double>> residuals = ComputeVerticalResiduals(overallIv, merged, tol);

		// V4b prep: complete overall chain of primary structure + secondary vertical OS.
		List<PlannedDimension> secondaryOsAll = plan.Dimensions
			.Where((PlannedDimension d) => d.Kind == DimensionKind.Normal
				&& d.Orientation == DimensionOrientation.Vertical
				&& d.Side == secondarySide
				&& string.Equals(d.DebugRole, "OutlineSegment", StringComparison.Ordinal))
			.ToList();
		HashSet<PlannedDimension> osOnCompleteChain = new HashSet<PlannedDimension>();
		if (secondaryOsAll.Count > 0)
		{
			List<DimensionDeduplicationItem> poolItems = new List<DimensionDeduplicationItem>();
			Dictionary<DimensionDeduplicationItem, PlannedDimension> map = new Dictionary<DimensionDeduplicationItem, PlannedDimension>();
			foreach (PlannedDimension p in primary)
			{
				DimensionDeduplicationItem it = ToDeduplicationItem(p);
				poolItems.Add(it);
				map[it] = p;
			}
			foreach (PlannedDimension os in secondaryOsAll)
			{
				DimensionDeduplicationItem it = ToDeduplicationItem(os);
				poolItems.Add(it);
				map[it] = os;
			}
			IList<DimensionDeduplicationItem> chain = _dimensionDeduplicationRules.FindCompleteOverallPartitionChain(
				poolItems,
				overallIv.Item1,
				overallIv.Item2,
				horizontal: false);
			if (chain != null)
			{
				foreach (DimensionDeduplicationItem ci in chain)
				{
					if (map.TryGetValue(ci, out PlannedDimension dim)
						&& string.Equals(dim.DebugRole, "OutlineSegment", StringComparison.Ordinal))
					{
						osOnCompleteChain.Add(dim);
					}
				}
			}
		}

		for (int num = plan.Dimensions.Count - 1; num >= 0; num--)
		{
			PlannedDimension cand = plan.Dimensions[num];
			if (cand.Kind != DimensionKind.Normal
				|| cand.Orientation != DimensionOrientation.Vertical
				|| cand.Side != secondarySide
				|| !string.Equals(cand.DebugRole, "OutlineSegment", StringComparison.Ordinal))
			{
				continue;
			}
			Tuple<double, double> osIv = ComputeArrowInterval(cand, horizontal: false);
			bool sameAsPrimary = primaryIvs.Any((Tuple<double, double> piv) =>
				Math.Abs(piv.Item1 - osIv.Item1) <= tol && Math.Abs(piv.Item2 - osIv.Item2) <= tol);
			bool inResidual = residuals.Any((Tuple<double, double> r) =>
				osIv.Item1 >= r.Item1 - tol && osIv.Item2 <= r.Item2 + tol);
			bool onPartitionChain = osOnCompleteChain.Contains(cand);
			if (!sameAsPrimary && !inResidual && !onPartitionChain)
			{
				continue;
			}
			string why;
			if (sameAsPrimary)
			{
				why = SuppressReason.SecondaryOutlineSegmentDuplicatesPrimaryStructureHeight;
			}
			else if (inResidual)
			{
				why = SuppressReason.SecondaryOutlineSegmentOverallResidual;
			}
			else
			{
				why = SuppressReason.SecondaryOutlineSegmentOverallPartitionWithPrimaryStructure;
			}
			plan.MarkSuppressed(cand, why);
			plan.Dimensions.RemoveAt(num);
		}
	}

	internal static List<Tuple<double, double>> MergeVerticalIntervals(IList<Tuple<double, double>> intervals, double tol)
	{
		List<Tuple<double, double>> sorted = intervals.OrderBy((Tuple<double, double> i) => i.Item1).ToList();
		List<Tuple<double, double>> merged = new List<Tuple<double, double>>();
		foreach (Tuple<double, double> iv in sorted)
		{
			if (merged.Count == 0)
			{
				merged.Add(iv);
				continue;
			}
			Tuple<double, double> last = merged[merged.Count - 1];
			if (iv.Item1 <= last.Item2 + tol)
			{
				merged[merged.Count - 1] = Tuple.Create(last.Item1, Math.Max(last.Item2, iv.Item2));
			}
			else
			{
				merged.Add(iv);
			}
		}
		return merged;
	}

	internal static List<Tuple<double, double>> ComputeVerticalResiduals(
		Tuple<double, double> overall,
		IList<Tuple<double, double>> mergedPrimary,
		double tol)
	{
		List<Tuple<double, double>> residuals = new List<Tuple<double, double>>();
		double cursor = overall.Item1;
		foreach (Tuple<double, double> block in mergedPrimary.OrderBy((Tuple<double, double> i) => i.Item1))
		{
			if (block.Item1 > cursor + tol)
			{
				residuals.Add(Tuple.Create(cursor, block.Item1));
			}
			cursor = Math.Max(cursor, block.Item2);
		}
		if (overall.Item2 > cursor + tol)
		{
			residuals.Add(Tuple.Create(cursor, overall.Item2));
		}
		return residuals;
	}

	private bool IsLeftStructureHeight(PlannedDimension dim)
	{
		return _dimensionDeduplicationRules.IsLeftStructureHeight(ToDeduplicationItem(dim));
	}

	private bool IsRightStructureHeight(PlannedDimension dim)
	{
		return _dimensionDeduplicationRules.IsRightStructureHeight(ToDeduplicationItem(dim));
	}

	private bool IsSameVerticalInterval(PlannedDimension a, PlannedDimension b)
	{
		return _dimensionDeduplicationRules.IsSameVerticalInterval(ToDeduplicationItem(a), ToDeduplicationItem(b));
	}

	private void SuppressMirroredDuplicates(DimensionPlan plan, DimensionSide primarySide, DimensionSide secondarySide, bool horizontal)
	{
		List<PlannedDimension> source = plan.Dimensions.Where((PlannedDimension d) => d.Side == primarySide).ToList();
		List<PlannedDimension> source2 = plan.Dimensions.Where((PlannedDimension d) => d.Side == secondarySide).ToList();
		foreach (PlannedDimension item in source2.ToList())
		{
			foreach (PlannedDimension item2 in source.ToList())
			{
				if (!CanSuppressMirroredDimension(item2, item) || !IsSameMeasuredDimension(item2, item, horizontal))
				{
					continue;
				}
				PlannedDimension plannedDimension = ((CompareDuplicatePreference(item, item2) > 0) ? item2 : item);
				plan.MarkSuppressed(plannedDimension, SuppressReason.MirroredDuplicate);
				plan.Dimensions.Remove(plannedDimension);
				break;
			}
		}
	}

	private static bool CanSuppressMirroredDimension(PlannedDimension a, PlannedDimension b)
	{
		if (a.ForceOuterLevel || b.ForceOuterLevel)
		{
			return false;
		}
		if (a.Kind == DimensionKind.Normal && b.Kind == DimensionKind.Normal)
		{
			return true;
		}
		return a.Kind == DimensionKind.HoleLocation || b.Kind == DimensionKind.HoleLocation;
	}

	private void SuppressDuplicateMeasuredDimensions(DimensionPlan plan, DimensionSide side, bool horizontal)
	{
		List<PlannedDimension> list = plan.Dimensions.Where((PlannedDimension d) => d.Side == side).ToList();
		for (int num = 0; num < list.Count; num++)
		{
			int num2 = num;
			for (int num3 = num + 1; num3 < list.Count; num3++)
			{
				if (IsSameMeasuredDimension(list[num], list[num3], horizontal) && CompareDuplicatePreference(list[num3], list[num2]) > 0)
				{
					num2 = num3;
				}
			}
			if (num2 != num)
			{
				PlannedDimension value = list[num2];
				list[num2] = list[num];
				list[num] = value;
			}
			for (int num4 = list.Count - 1; num4 > num; num4--)
			{
				if (IsSameMeasuredDimension(list[num], list[num4], horizontal))
				{
					plan.MarkSuppressed(list[num4], SuppressReason.DuplicateMeasuredDimension);
					plan.Dimensions.Remove(list[num4]);
					list.RemoveAt(num4);
				}
			}
		}
	}

	private bool IsSameMeasuredDimension(PlannedDimension a, PlannedDimension b, bool horizontal)
	{
		if (a.Orientation != b.Orientation)
		{
			return false;
		}
		return _dimensionDeduplicationRules.IsSameMeasuredDimension(ToDeduplicationItem(a), ToDeduplicationItem(b), horizontal);
	}

	private static Tuple<double, double> ComputeArrowInterval(PlannedDimension dim, bool horizontal)
	{
		return DimensionDeduplicationRules.ComputeArrowInterval(ToDeduplicationItem(dim), horizontal);
	}

	private static double GetDimensionSpan(PlannedDimension dim, bool horizontal)
	{
		return horizontal ? Math.Abs(dim.SecondPoint.X - dim.FirstPoint.X) : Math.Abs(dim.SecondPoint.Y - dim.FirstPoint.Y);
	}

	private static DimensionDeduplicationItem ToDeduplicationItem(PlannedDimension dim)
	{
		return new DimensionDeduplicationItem
		{
			FirstPoint = dim.FirstPoint,
			SecondPoint = dim.SecondPoint,
			OverrideText = dim.OverrideText,
			Span = GetDimensionSpan(dim, dim.Orientation == DimensionOrientation.Horizontal),
			Kind = dim.Kind,
			ForceOuterLevel = dim.ForceOuterLevel,
			DebugRole = dim.DebugRole
		};
	}

	private int CompareDuplicatePreference(PlannedDimension a, PlannedDimension b)
	{
		return _dimensionDeduplicationRules.CompareDuplicatePreference(ToDeduplicationItem(a), ToDeduplicationItem(b));
	}

	private static int GetDimensionPreferenceRank(PlannedDimension dim)
	{
		return DimensionDeduplicationRules.GetDimensionPreferenceRank(dim.Kind);
	}

	private static double? TryExtractTolerance(string text)
	{
		return DimensionDeduplicationRules.TryExtractTolerance(text);
	}

	/// <summary>
	/// BuildOverallPartitionChain for structure dims:
	/// 1) Collect same-axis Structure roles + OutlineSegment partners.
	/// 2) Project to real 1D intervals.
	/// 3) Find contiguous cover Overall.Min→Max (abut, no gap/interior overlap, within overall, tol).
	/// 4) If chain length ≥ 2: suppress Structure members of the chain; also suppress structure
	///    dims whose interval matches a chain piece (when OS won a same-interval slot).
	///    Additionally try a structure-only chain so finer structure pieces are not missed when
	///    coarser OutlineSegments form an alternate cover.
	/// OutlineSegment members stay for <see cref="SuppressOutlineSegmentsThatPartitionOverall"/>.
	/// Numeric span sums alone are never sufficient.
	/// </summary>
	private void SuppressStructureDimensionsThatPartitionOverall(DimensionPlan plan, OutlineFeature2D outline, bool horizontal)
	{
		if (plan == null || outline == null)
		{
			return;
		}
		PlannedDimension overall = plan.Dimensions.FirstOrDefault((PlannedDimension d) => horizontal ? (d.Kind == DimensionKind.OverallWidth) : (d.Kind == DimensionKind.OverallHeight));
		if (overall == null)
		{
			return;
		}
		DimensionOrientation expected = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
		if (overall.Orientation != expected)
		{
			return;
		}
		List<PlannedDimension> structureDims = plan.Dimensions
			.Where((PlannedDimension d) => d.Kind == DimensionKind.Normal
				&& IsStructureWidthOrHeightRole(d.DebugRole)
				&& d.Orientation == expected
				&& (horizontal || !IsDatumRootedOuterStepDimension(d, outline))
				&& HasRealStructurePartitionEdge(d, outline, horizontal))
			.ToList();
		if (structureDims.Count == 0)
		{
			return;
		}
		List<PlannedDimension> outlineSegments = plan.Dimensions
			.Where((PlannedDimension d) => d.Kind == DimensionKind.Normal
				&& string.Equals(d.DebugRole, "OutlineSegment", StringComparison.Ordinal)
				&& d.Orientation == expected)
			.ToList();
		Tuple<double, double> overallInterval = ComputeArrowInterval(overall, horizontal);
		HashSet<PlannedDimension> toSuppress = new HashSet<PlannedDimension>();
		double tol = _config.GeometryTolerance;
		// Per-structure search: all members must be real, collinear contour edges on one side.
		// X/Y projection alone must not join a top step to a bottom ledge.
		foreach (PlannedDimension focus in structureDims)
		{
			List<PlannedDimension> partnerPool = new List<PlannedDimension> { focus };
			foreach (PlannedDimension other in structureDims)
			{
				if (other != focus
					&& other.Side == focus.Side
					&& AreCollinearStructurePartners(focus, other, horizontal, tol))
				{
					partnerPool.Add(other);
				}
			}
			foreach (PlannedDimension os in outlineSegments)
			{
				if (os.Side == focus.Side
					&& HasRealStructurePartitionEdge(os, outline, horizontal)
					&& AreCollinearStructurePartners(focus, os, horizontal, tol))
				{
					partnerPool.Add(os);
				}
			}
			if (partnerPool.Count < 2)
			{
				continue;
			}
			List<DimensionDeduplicationItem> items = new List<DimensionDeduplicationItem>(partnerPool.Count);
			Dictionary<DimensionDeduplicationItem, PlannedDimension> itemToDim = new Dictionary<DimensionDeduplicationItem, PlannedDimension>();
			foreach (PlannedDimension partner in partnerPool)
			{
				DimensionDeduplicationItem item = ToDeduplicationItem(partner);
				items.Add(item);
				itemToDim[item] = partner;
			}
			IList<DimensionDeduplicationItem> chain = _dimensionDeduplicationRules.FindCompleteOverallPartitionChain(
				items,
				overallInterval.Item1,
				overallInterval.Item2,
				horizontal);
			if (chain == null || chain.Count < 2)
			{
				continue;
			}
			bool focusOnChain = false;
			Tuple<double, double> focusInterval = ComputeArrowInterval(focus, horizontal);
			foreach (DimensionDeduplicationItem chainItem in chain)
			{
				if (!itemToDim.TryGetValue(chainItem, out PlannedDimension dim))
				{
					continue;
				}
				if (dim == focus)
				{
					focusOnChain = true;
					break;
				}
				if (IsStructureWidthOrHeightRole(dim.DebugRole)
					|| string.Equals(dim.DebugRole, "OutlineSegment", StringComparison.Ordinal))
				{
					Tuple<double, double> iv = ComputeArrowInterval(dim, horizontal);
					if (Math.Abs(iv.Item1 - focusInterval.Item1) <= tol
						&& Math.Abs(iv.Item2 - focusInterval.Item2) <= tol)
					{
						focusOnChain = true;
						break;
					}
				}
			}
			if (focusOnChain)
			{
				toSuppress.Add(focus);
			}
		}
		if (toSuppress.Count == 0)
		{
			return;
		}
		for (int num = plan.Dimensions.Count - 1; num >= 0; num--)
		{
			PlannedDimension candidate = plan.Dimensions[num];
			if (!toSuppress.Contains(candidate))
			{
				continue;
			}
			plan.MarkSuppressed(candidate, SuppressReason.StructureOverallPartition);
			plan.Dimensions.RemoveAt(num);
		}
	}

	private bool HasRealStructurePartitionEdge(PlannedDimension dimension, OutlineFeature2D outline, bool horizontal)
	{
		if (dimension == null || outline == null)
		{
			return false;
		}
		double tol = _config.GeometryTolerance;
		if (horizontal)
		{
			if (Math.Abs(dimension.FirstPoint.Y - dimension.SecondPoint.Y) > tol)
			{
				return false;
			}
			double minX = Math.Min(dimension.FirstPoint.X, dimension.SecondPoint.X);
			double maxX = Math.Max(dimension.FirstPoint.X, dimension.SecondPoint.X);
			return outline.Segments.Any((Segment2D segment) => segment != null
				&& !segment.IsArcChord
				&& segment.IsHorizontal(tol)
				&& Math.Abs(segment.MinY - dimension.FirstPoint.Y) <= tol
				&& segment.MinX <= minX + tol
				&& segment.MaxX >= maxX - tol);
		}
		if (Math.Abs(dimension.FirstPoint.X - dimension.SecondPoint.X) > tol)
		{
			return false;
		}
		double minY = Math.Min(dimension.FirstPoint.Y, dimension.SecondPoint.Y);
		double maxY = Math.Max(dimension.FirstPoint.Y, dimension.SecondPoint.Y);
		return outline.Segments.Any((Segment2D segment) => segment != null
			&& !segment.IsArcChord
			&& segment.IsVertical(tol)
			&& Math.Abs(segment.MinX - dimension.FirstPoint.X) <= tol
			&& segment.MinY <= minY + tol
			&& segment.MaxY >= maxY - tol);
	}

	/// <summary>
	/// Structure + OutlineSegment overall-partition partners must be collinear on the
	/// measurement edge: same Y for horizontal dims, same X for vertical dims.
	/// Same placement side alone is insufficient (tower top vs arm top both Side=Top).
	/// </summary>
	internal static bool AreCollinearStructurePartners(PlannedDimension a, PlannedDimension b, bool horizontal, double tol)
	{
		if (a == null || b == null)
		{
			return false;
		}
		if (horizontal)
		{
			double aY1 = a.FirstPoint.Y;
			double aY2 = a.SecondPoint.Y;
			double bY1 = b.FirstPoint.Y;
			double bY2 = b.SecondPoint.Y;
			// Both segments nearly horizontal on the same Y.
			if (Math.Abs(aY1 - aY2) > tol || Math.Abs(bY1 - bY2) > tol)
			{
				return false;
			}
			return Math.Abs(aY1 - bY1) <= tol;
		}
		double aX1 = a.FirstPoint.X;
		double aX2 = a.SecondPoint.X;
		double bX1 = b.FirstPoint.X;
		double bX2 = b.SecondPoint.X;
		if (Math.Abs(aX1 - aX2) > tol || Math.Abs(bX1 - bX2) > tol)
		{
			return false;
		}
		return Math.Abs(aX1 - bX1) <= tol;
	}

	/// <summary>
	/// Shared geometric partition check for overall-suppression rules.
	/// Requires matching measurement orientation and real endpoint coverage of overall.
	/// </summary>
	private bool FormsCompleteOverallPartition(PlannedDimension first, PlannedDimension second, PlannedDimension overall, bool horizontal)
	{
		if (first == null || second == null || overall == null)
		{
			return false;
		}
		DimensionOrientation expected = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
		if (first.Orientation != expected || second.Orientation != expected || overall.Orientation != expected)
		{
			return false;
		}
		return _dimensionDeduplicationRules.FormsCompleteOverallPartition(
			ToDeduplicationItem(first),
			ToDeduplicationItem(second),
			ToDeduplicationItem(overall),
			horizontal);
	}

	private bool FormsCompleteOverallPartition(PlannedDimension first, PlannedDimension second, double overallMin, double overallMax, bool horizontal)
	{
		if (first == null || second == null)
		{
			return false;
		}
		DimensionOrientation expected = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
		if (first.Orientation != expected || second.Orientation != expected)
		{
			return false;
		}
		return _dimensionDeduplicationRules.FormsCompleteOverallPartition(
			ToDeduplicationItem(first),
			ToDeduplicationItem(second),
			overallMin,
			overallMax,
			horizontal);
	}

	private static bool IsStructureWidthOrHeightRole(string debugRole)
	{
		if (string.IsNullOrEmpty(debugRole))
		{
			return false;
		}
		return debugRole == "TopStructWidth"
			|| debugRole == "BottomStructWidth"
			|| debugRole == "LeftStructHeight"
			|| debugRole == "RightStructHeight";
	}

	private static bool IsHorizontalStructureWidthRole(string debugRole)
	{
		return string.Equals(debugRole, "TopStructWidth", StringComparison.Ordinal)
			|| string.Equals(debugRole, "BottomStructWidth", StringComparison.Ordinal);
	}
}
