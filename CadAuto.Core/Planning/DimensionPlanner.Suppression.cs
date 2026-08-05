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
	private const string BottomContourChainEvidence = "Bottom:RealContourChainMiddle";

	private static bool HasBottomContourChainEvidence(PlannedDimension dimension)
	{
		return dimension != null
			&& !string.IsNullOrEmpty(dimension.TopologyEvidence)
			&& dimension.TopologyEvidence.IndexOf(BottomContourChainEvidence, StringComparison.Ordinal) >= 0;
	}

	private static string PreserveBottomContourChainEvidence(PlannedDimension dimension, string fallback)
	{
		if (!HasBottomContourChainEvidence(dimension))
		{
			return fallback;
		}
		if (string.IsNullOrEmpty(fallback)
			|| fallback.IndexOf(BottomContourChainEvidence, StringComparison.Ordinal) >= 0)
		{
			return dimension.TopologyEvidence;
		}
		return dimension.TopologyEvidence + ";" + fallback;
	}

	// Suppression pipeline: 27 passes. THE ORDER IS PRODUCT BEHAVIOR -
	// each inline note below records a constraint that was learned the hard way; keep
	// the notes adjacent to the calls they explain. Stages:
	//   0. capture   - FindLocalGeometryOnOverallEnvelope snapshots candidates BEFORE
	//                  any pass mutates the plan; the matching suppress is pass 27.
	//   1. structure vs overall (passes 1-9)  - height dedup vs overall, bottom
	//                  protrusion remainders, structure partition chains (H then V),
	//                  datum-rooted complements, then real bottom/left outer-step partitions.
	//                  Must run before stage 2 so OS body equivalents still exist.
	//   2. OutlineSegment partition (passes 10-13, four sides) - scoped to OS only.
	//   3. mirror dedup (passes 14-15) - must precede envelope cleanup.
	//   4. secondary-side cleanup (passes 16-17).
	//   5. envelope collinear fragments (pass 18).
	//   6. complementary remainders (passes 19-20) - Top/Right only by design.
	//   7. closed overall length chains (passes 21-22).
	//   8. measured duplicates (passes 23-26, four sides).
	//   9. deferred local-geometry suppress (pass 27) using the stage-0 snapshot.
	// This method runs twice per full plan (once inside CreateOutlinePlan, once after
	// hole/slot dimensions are added) - outline candidates see it twice, hole/slot
	// candidates once.
	private void SuppressDuplicateDimensions(DimensionPlan plan, OutlineFeature2D outline)
	{
		List<PlannedDimension> localGeometryOnEnvelope = FindLocalGeometryOnOverallEnvelope(plan, outline);
		// Side heights are useful only when they describe a real outer-profile step (or
		// an explicit inner-groove chamfer). Remove projection-only heights before any
		// mirror/partition pass can promote them to the visible side.
		SuppressNonProfileBackedSideStructureHeights(plan, outline);
		SuppressRightStructureHeightsDuplicatingOverallHeight(plan);
		SuppressLeftStructureHeightsCoveredByRight(plan);
		SuppressBottomProtrusionInnerRemainders(plan, outline);
		// A real bottom contour chain may use the inner ledge as its middle piece. Resolve
		// that chain before later duplicate/partition passes remove its longest remainder
		// or let the cross-level structure projection win the same interval.
		SuppressBottomContourChainOverallRemainder(plan, outline);
		// BuildOverallPartitionChain for structure: multi-piece contiguous cover of overall
		// (structure roles + OutlineSegment partners). Suppress only structure members of the chain.
		// Run before OutlineSegment overall-partition removal so OS partners still exist.
		// Partners never include slot/hole Normals. Local structure steps that do not complete
		// overall are kept (e.g. TopStructWidth=50).
		SuppressStructureDimensionsThatPartitionOverall(plan, outline, horizontal: true);
		SuppressStructureDimensionsThatPartitionOverall(plan, outline, horizontal: false);
		SuppressDatumRootedOuterStepComplements(plan, outline, horizontal: true);
		SuppressDatumRootedOuterStepComplements(plan, outline, horizontal: false);
		// A real bottom outer step may be shorter or longer than its body remainder.
		// Keep Overall + the boundary-backed step; suppress every equivalent body interval
		// (projected BottomStructWidth and OutlineSegment) before either OS is removed.
		ArbitrateOuterContourStepOverallRemainders(plan, outline);
		// Keep the real outer-step arbitration ahead of this cleanup; its evidence and
		// suppression reason must remain authoritative for datum-rooted contour steps.
		SuppressProjectedStructureDimensionsThatPartitionOverall(plan, outline, horizontal: true);
		SuppressProjectedStructureDimensionsThatPartitionOverall(plan, outline, horizontal: false);
		// Drop raw OutlineSegment pairs that re-partition overall (before complementary remainder
		// removes only the larger partner and leaves the smaller fragment orphaned).
		// Scoped to OutlineSegment only — do not broaden complementary-remainder to Bottom/Left
		// or slot/hole Normal dims.
		SuppressOutlineSegmentsThatPartitionOverall(plan, outline, DimensionSide.Top, horizontal: true);
		SuppressOutlineSegmentsThatPartitionOverall(plan, outline, DimensionSide.Bottom, horizontal: true);
		SuppressOutlineSegmentsThatPartitionOverall(plan, outline, DimensionSide.Right, horizontal: false);
		SuppressOutlineSegmentsThatPartitionOverall(plan, outline, DimensionSide.Left, horizontal: false);
		// An open three-piece bottom contour keeps the middle real edge and the MinY edge;
		// the opposite projected interval is derivable from OverallWidth and is suppressed.
		SuppressOpenBottomContourDerivableOuterStructure(plan, outline);
		// Mirror before envelope: envelope may remove the primary-side outer tip first, which
		// would orphan a same-interval secondary OutlineSegment (e.g. Right OS@inner X that
		// matches Left envelope tip) and leave GEN|OutlineSegment*|R|L0 selected.
		SuppressMirroredDuplicates(plan, DimensionSide.Bottom, DimensionSide.Top, horizontal: true, outline: outline);
		SuppressMirroredDuplicates(plan, DimensionSide.Left, DimensionSide.Right, horizontal: false, outline: outline);
		// After mirror keeps structure heights over OS, drop secondary-side vertical OS that only
		// restate the primary structure stack or the overall residual (left/right step symmetry).
		SuppressSecondaryVerticalOutlineSegmentsRedundantWithPrimaryStructureStack(plan);
		// Structure>OS mirror can leave short outer tips that only restate overall residual noise.
		// Keep short dimensions backed by a real partial envelope edge: those are real steps.
		SuppressOrphanOuterVerticalStructureHeightTips(plan, outline);
		// Outer-envelope collinear OutlineSegment fragments (+ same-interval structure dups).
		SuppressOutlineSegmentsOnOverallEnvelope(plan, outline);
		// Existing complementary remainder for structure/normal remainders (Top/Right only).
		SuppressComplementaryOutlineRemainders(plan, DimensionSide.Top, horizontal: true, outline);
		SuppressComplementaryOutlineRemainders(plan, DimensionSide.Right, horizontal: false, outline);
		// Closed length chains (any side pairing): body 70 + step 20 = overall 90 → drop 70.
		// Includes OutlineSegment body lengths after interior edges resolve to Bottom/Top.
		// Still requires real 1D abutment cover (never span-sum alone).
		SuppressClosedOverallLengthRemainders(plan, horizontal: true);
		SuppressClosedOverallLengthRemainders(plan, horizontal: false);
		SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Bottom, horizontal: true);
		SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Top, horizontal: true);
		SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Left, horizontal: false);
		SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Right, horizontal: false);
		SuppressLocalGeometryOnOverallEnvelope(plan, localGeometryOnEnvelope);
	}

	private void SuppressNonProfileBackedSideStructureHeights(DimensionPlan plan, OutlineFeature2D outline)
	{
		if (plan == null || outline == null)
		{
			return;
		}
		foreach (PlannedDimension candidate in plan.Dimensions
			.Where((PlannedDimension dimension) =>
				(dimension.DebugRole == "LeftStructHeight" || dimension.DebugRole == "RightStructHeight")
				&& dimension.Orientation == DimensionOrientation.Vertical)
			.ToList())
		{
			if (HasOuterProfileStepEvidence(candidate, outline))
			{
				continue;
			}
			plan.MarkSuppressed(candidate, SuppressReason.NonProfileBackedSideStructureHeight);
			plan.Dimensions.Remove(candidate);
		}
	}

	private bool HasOuterProfileStepEvidence(PlannedDimension candidate, OutlineFeature2D outline)
	{
		if (candidate == null || outline == null)
		{
			return true;
		}
		DimensionSide side = candidate.Side;
		if (side != DimensionSide.Left && side != DimensionSide.Right)
		{
			return true;
		}
		if (HasSideInnerGrooveEvidence(candidate, outline, side))
		{
			return true;
		}
		double tol = _config.GeometryTolerance;
		// Cross-level projections are resolved by the existing overall-partition passes;
		// keep them in the plan until those passes can attach the authoritative reason.
		if (Math.Abs(candidate.FirstPoint.X - candidate.SecondPoint.X) > tol)
		{
			return true;
		}
		return HasSideProfileBoundaryAt(outline, side, candidate.FirstPoint.Y)
			|| HasSideProfileBoundaryAt(outline, side, candidate.SecondPoint.Y);
	}

	private bool HasSideProfileBoundaryAt(OutlineFeature2D outline, DimensionSide side, double y)
	{
		double tol = _config.GeometryTolerance;
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord || !segment.IsHorizontal(tol)
				|| Math.Abs(segment.MinY - y) > tol)
			{
				continue;
			}
			if (side == DimensionSide.Left
				&& Math.Abs(segment.MinX - outline.MinX) <= tol
				&& segment.MaxX < outline.MaxX - tol)
			{
				return true;
			}
			if (side == DimensionSide.Right
				&& Math.Abs(segment.MaxX - outline.MaxX) <= tol
				&& segment.MinX > outline.MinX + tol)
			{
				return true;
			}
		}
		return false;
	}

	private bool HasSideInnerGrooveEvidence(PlannedDimension candidate, OutlineFeature2D outline, DimensionSide side)
	{
		if (outline.Segments == null || outline.Segments.Count == 0)
		{
			return false;
		}
		double tol = _config.GeometryTolerance;
		return outline.Segments.Any((Segment2D segment) =>
			segment != null
			&& !segment.IsHorizontal(tol)
			&& !segment.IsVertical(tol)
			&& IsFortyFiveDegreeSegment(segment)
			&& IsSideInnerGrooveChamferSegment(segment, outline, side)
			&& (Math.Abs(segment.Start.Y - candidate.FirstPoint.Y) <= tol
				|| Math.Abs(segment.End.Y - candidate.FirstPoint.Y) <= tol
				|| Math.Abs(segment.Start.Y - candidate.SecondPoint.Y) <= tol
				|| Math.Abs(segment.End.Y - candidate.SecondPoint.Y) <= tol));
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
			bool onMinY = Math.Abs(dimension.FirstPoint.Y - outline.MinY) <= tol
				&& Math.Abs(dimension.SecondPoint.Y - outline.MinY) <= tol;
			bool onMaxY = Math.Abs(dimension.FirstPoint.Y - outline.MaxY) <= tol
				&& Math.Abs(dimension.SecondPoint.Y - outline.MaxY) <= tol;
			bool onMinX = Math.Abs(dimension.FirstPoint.X - outline.MinX) <= tol
				&& Math.Abs(dimension.SecondPoint.X - outline.MinX) <= tol;
			bool onMaxX = Math.Abs(dimension.FirstPoint.X - outline.MaxX) <= tol
				&& Math.Abs(dimension.SecondPoint.X - outline.MaxX) <= tol;
			bool onHorizontalEnvelope = dimension.Orientation == DimensionOrientation.Horizontal
				&& horizontalSide
				&& (onMinY || onMaxY);
			bool onVerticalEnvelope = dimension.Orientation == DimensionOrientation.Vertical
				&& verticalSide
				&& (onMinX || onMaxX);
			if (!onHorizontalEnvelope && !onVerticalEnvelope)
			{
				continue;
			}
			// Stepped outlines put real structure faces on the overall AABB (right body height on
			// MaxX, left-boss top width on MaxY). Only suppress when that envelope side is a
			// full-length outer edge; otherwise overall W/H cannot replace the step size.
			// No segment data => legacy suppress-all (unit tests that only set Min/Max).
			if (outline.Segments != null && outline.Segments.Count > 0)
			{
				double edgeCoordinate = onHorizontalEnvelope
					? (onMinY ? outline.MinY : outline.MaxY)
					: (onMinX ? outline.MinX : outline.MaxX);
				if (!IsFullLengthEnvelopeEdge(outline, horizontal: onHorizontalEnvelope, edgeCoordinate, tol))
				{
					continue;
				}
				// Collinear full outer edge can host a step length that closes overall with an
				// opposite-side body length (bottom 20 + top 70 = 90). Keep only then.
				if (ShouldKeepSteppedStructureForCrossSideClosedChain(dimension, plan, outline, tol))
				{
					continue;
				}
			}
			matches.Add(dimension);
		}
		return matches;
	}

	/// <summary>
	/// Keep a stepped structure dim only when an opposite-side structure partner completes a
	/// real overall partition with it (closed chain). Prevents short same-side tips that only
	/// pair with OutlineSegments from surviving.
	/// </summary>
	private bool ShouldKeepSteppedStructureForCrossSideClosedChain(
		PlannedDimension dimension,
		DimensionPlan plan,
		OutlineFeature2D outline,
		double tol)
	{
		if (dimension == null || plan == null || outline == null
			|| !IsSteppedEnvelopeStructureDimension(dimension, outline, tol))
		{
			return false;
		}
		bool horizontal = dimension.Orientation == DimensionOrientation.Horizontal;
		PlannedDimension overall = plan.Dimensions.FirstOrDefault((PlannedDimension d) =>
			horizontal ? (d.Kind == DimensionKind.OverallWidth) : (d.Kind == DimensionKind.OverallHeight));
		if (overall == null)
		{
			return false;
		}
		return plan.Dimensions.Any((PlannedDimension other) => other != null
			&& other != dimension
			&& other.Kind == DimensionKind.Normal
			&& IsClosedChainLengthRole(other.DebugRole, horizontal)
			&& IsClosedChainPairingAllowed(dimension, other)
			&& FormsCompleteOverallPartition(dimension, other, overall, horizontal));
	}

	/// <summary>
	/// True when a structure dim on the outer envelope ends at an interior shoulder: one end is
	/// an overall corner, the other is not, and a real orthogonal segment leaves the envelope
	/// there. Distinguishes step lengths from overall-duplicate edge fragments.
	/// </summary>
	private static bool IsSteppedEnvelopeStructureDimension(PlannedDimension dimension, OutlineFeature2D outline, double tol)
	{
		if (dimension == null || outline == null || outline.Segments == null)
		{
			return false;
		}
		bool horizontal = dimension.Orientation == DimensionOrientation.Horizontal;
		double span = horizontal
			? Math.Abs(dimension.SecondPoint.X - dimension.FirstPoint.X)
			: Math.Abs(dimension.SecondPoint.Y - dimension.FirstPoint.Y);
		double overall = horizontal ? outline.Width : outline.Height;
		if (span <= tol || span + tol >= overall)
		{
			return false;
		}
		Point2D a = dimension.FirstPoint;
		Point2D b = dimension.SecondPoint;
		bool aAtOverallCorner = horizontal
			? (Math.Abs(a.X - outline.MinX) <= tol || Math.Abs(a.X - outline.MaxX) <= tol)
			: (Math.Abs(a.Y - outline.MinY) <= tol || Math.Abs(a.Y - outline.MaxY) <= tol);
		bool bAtOverallCorner = horizontal
			? (Math.Abs(b.X - outline.MinX) <= tol || Math.Abs(b.X - outline.MaxX) <= tol)
			: (Math.Abs(b.Y - outline.MinY) <= tol || Math.Abs(b.Y - outline.MaxY) <= tol);
		if (aAtOverallCorner == bAtOverallCorner)
		{
			// Need exactly one end on the overall extremity (the free end of the step).
			return false;
		}
		Point2D interior = aAtOverallCorner ? b : a;
		return HasOrthogonalShoulderLeavingEnvelope(outline, interior, horizontal, tol);
	}

	private static bool HasOrthogonalShoulderLeavingEnvelope(OutlineFeature2D outline, Point2D point, bool envelopeIsHorizontal, double tol)
	{
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null)
			{
				continue;
			}
			bool touches = point.DistanceTo(segment.Start) <= tol || point.DistanceTo(segment.End) <= tol;
			if (!touches)
			{
				continue;
			}
			if (envelopeIsHorizontal)
			{
				// Envelope along Y=const: shoulder must be vertical and leave that Y.
				if (!segment.IsVertical(tol))
				{
					continue;
				}
				if (Math.Abs(segment.LengthY) > tol)
				{
					return true;
				}
			}
			else if (segment.IsHorizontal(tol) && Math.Abs(segment.LengthX) > tol)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// True when collinear outer segments on the envelope line cover the full overall width
	/// (horizontal edge) or height (vertical edge). Partial coverage means a step face.
	/// </summary>
	private static bool IsFullLengthEnvelopeEdge(OutlineFeature2D outline, bool horizontal, double edgeCoordinate, double tol)
	{
		double covered = 0.0;
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null)
			{
				continue;
			}
			if (horizontal)
			{
				if (!segment.IsHorizontal(tol))
				{
					continue;
				}
				if (Math.Abs(segment.Start.Y - edgeCoordinate) > tol || Math.Abs(segment.End.Y - edgeCoordinate) > tol)
				{
					continue;
				}
				covered += segment.LengthX;
			}
			else
			{
				if (!segment.IsVertical(tol))
				{
					continue;
				}
				if (Math.Abs(segment.Start.X - edgeCoordinate) > tol || Math.Abs(segment.End.X - edgeCoordinate) > tol)
				{
					continue;
				}
				covered += segment.LengthY;
			}
		}
		double overall = horizontal ? outline.Width : outline.Height;
		return overall > tol && covered + tol >= overall;
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
			// Keep an inner OutlineSegment when it is the real middle piece of a complete
			// bottom contour chain whose longest piece has a cross-level structure projection.
			// Without this narrow exemption the chain loses its real evidence before arbitration.
			if (isInnerOutline
				&& (HasBottomContourChainEvidence(candidate)
					|| IsBottomContourChainEvidence(candidate, plan, outline)))
			{
				if (!HasBottomContourChainEvidence(candidate))
				{
					plan.RecordRuleEvidence(candidate, null, null, BottomContourChainEvidence);
				}
				continue;
			}
			plan.MarkSuppressed(candidate, SuppressReason.BottomProtrusionInnerRemainder);
			plan.Dimensions.RemoveAt(num);
		}
	}

	private void SuppressBottomContourChainOverallRemainder(DimensionPlan plan, OutlineFeature2D outline)
	{
		if (!TryFindBottomContourChainRemainder(
			plan,
			outline,
			out List<PlannedDimension> chain,
			out PlannedDimension remainder,
			out List<PlannedDimension> projections))
		{
			return;
		}
		// A mirrored chain can expose a real short bottom structure on the opposite
		// outer tip. Keep that structure as the representative for its interval while
		// the middle real OutlineSegment remains the chain evidence.
		foreach (PlannedDimension segment in chain.Where((PlannedDimension dimension) => dimension != remainder))
		{
			PlannedDimension realStructure = plan.Dimensions.FirstOrDefault((PlannedDimension dimension) =>
				dimension.Kind == DimensionKind.Normal
				&& dimension.Side == DimensionSide.Bottom
				&& string.Equals(dimension.DebugRole, "BottomStructWidth", StringComparison.Ordinal)
				&& !IsCrossLevelStructureProjection(dimension, horizontal: true)
				&& HasRealStructurePartitionEdge(dimension, outline, horizontal: true)
				&& IsSameMeasurementInterval(dimension, segment, horizontal: true));
			if (realStructure != null)
			{
				plan.RecordRuleEvidence(realStructure, null, null, BottomContourChainEvidence);
			}
		}
		// Remove the cross-level representative first; the real contour interval is the
		// evidence used to choose the remainder, not a competing measured dimension.
		foreach (PlannedDimension projection in projections)
		{
			if (!plan.Dimensions.Contains(projection))
			{
				continue;
			}
			plan.MarkSuppressed(projection, SuppressReason.ProjectedStructureOverallPartition);
			plan.Dimensions.Remove(projection);
		}
		if (remainder != null && plan.Dimensions.Contains(remainder))
		{
			// Keep the two shorter real chain pieces; the longest piece is the overall
			// remainder and is redundant once OverallWidth is present.
			plan.MarkSuppressed(remainder, SuppressReason.OutlineSegmentOverallPartition);
			plan.Dimensions.Remove(remainder);
		}
	}

	private bool IsBottomContourChainEvidence(
		PlannedDimension candidate,
		DimensionPlan plan,
		OutlineFeature2D outline)
	{
		if (candidate == null
			|| !string.Equals(candidate.DebugRole, "OutlineSegment", StringComparison.Ordinal)
			|| candidate.Side != DimensionSide.Bottom
			|| candidate.Orientation != DimensionOrientation.Horizontal)
		{
			return false;
		}
		return TryFindBottomContourChainRemainder(
			plan,
			outline,
			out List<PlannedDimension> chain,
			out PlannedDimension remainder,
			out List<PlannedDimension> projections)
			&& chain.Contains(candidate)
			&& remainder != null
			&& projections.Count > 0;
	}

	private bool TryFindBottomContourChainRemainder(
		DimensionPlan plan,
		OutlineFeature2D outline,
		out List<PlannedDimension> chain,
		out PlannedDimension remainder,
		out List<PlannedDimension> projections)
	{
		chain = new List<PlannedDimension>();
		remainder = null;
		projections = new List<PlannedDimension>();
		if (plan == null || outline == null || outline.Segments == null || outline.Segments.Count == 0)
		{
			return false;
		}
		PlannedDimension overall = plan.Dimensions.FirstOrDefault((PlannedDimension dimension) =>
			dimension.Kind == DimensionKind.OverallWidth
			&& dimension.Orientation == DimensionOrientation.Horizontal);
		if (overall == null)
		{
			return false;
		}
		List<PlannedDimension> realOutlineSegments = plan.Dimensions
			.Where((PlannedDimension dimension) => dimension.Kind == DimensionKind.Normal
				&& dimension.Side == DimensionSide.Bottom
				&& dimension.Orientation == DimensionOrientation.Horizontal
				&& string.Equals(dimension.DebugRole, "OutlineSegment", StringComparison.Ordinal)
				&& IsRealOutlineSegmentDimension(dimension, outline))
			.ToList();
		if (realOutlineSegments.Count < 3)
		{
			return false;
		}
		Tuple<double, double> overallInterval = ComputeArrowInterval(overall, horizontal: true);
		List<DimensionDeduplicationItem> items = new List<DimensionDeduplicationItem>(realOutlineSegments.Count);
		Dictionary<DimensionDeduplicationItem, PlannedDimension> itemToDimension =
			new Dictionary<DimensionDeduplicationItem, PlannedDimension>();
		foreach (PlannedDimension segment in realOutlineSegments)
		{
			DimensionDeduplicationItem item = ToDeduplicationItem(segment);
			items.Add(item);
			itemToDimension[item] = segment;
		}
		IList<DimensionDeduplicationItem> chainItems = _dimensionDeduplicationRules.FindCompleteOverallPartitionChain(
			items,
			overallInterval.Item1,
			overallInterval.Item2,
			horizontal: true);
		if (chainItems == null || chainItems.Count < 3)
		{
			return false;
		}
		chain = chainItems
			.Where((DimensionDeduplicationItem item) => itemToDimension.ContainsKey(item))
			.Select((DimensionDeduplicationItem item) => itemToDimension[item])
			.ToList();
		if (chain.Count < 3)
		{
			return false;
		}
		List<PlannedDimension> orderedChain = chain
			.OrderBy((PlannedDimension dimension) => ComputeArrowInterval(dimension, horizontal: true).Item1)
			.ThenBy((PlannedDimension dimension) => ComputeArrowInterval(dimension, horizontal: true).Item2)
			.ToList();
		PlannedDimension longest = orderedChain
			.OrderByDescending((PlannedDimension dimension) => GetDimensionSpan(dimension, horizontal: true))
			.FirstOrDefault();
		if (longest == null)
		{
			return false;
		}
		projections = plan.Dimensions
			.Where((PlannedDimension dimension) => dimension.Kind == DimensionKind.Normal
				&& dimension.Side == DimensionSide.Bottom
				&& dimension.Orientation == DimensionOrientation.Horizontal
				&& string.Equals(dimension.DebugRole, "BottomStructWidth", StringComparison.Ordinal)
				&& IsCrossLevelStructureProjection(dimension, horizontal: true)
				&& IsSameMeasurementInterval(dimension, longest, horizontal: true))
			.ToList();
		if (projections.Count == 0)
		{
			return false;
		}
		remainder = longest;
		return true;
	}

	private bool IsRealOutlineSegmentDimension(PlannedDimension dimension, OutlineFeature2D outline)
	{
		if (dimension == null || outline == null || dimension.Orientation != DimensionOrientation.Horizontal)
		{
			return false;
		}
		double tol = _config.GeometryTolerance;
		return outline.Segments.Any((Segment2D segment) => segment != null
			&& !segment.IsArcChord
			&& segment.IsHorizontal(tol)
			&& HasSameSegmentEndpoints(segment, dimension.FirstPoint, dimension.SecondPoint));
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
		// Shaft/step length on MinY that closes overall with an opposite-side body length is not
		// a lower-arm residual (even when a parallel step-top shares the same X interval).
		if (isBottomStructure && ShouldKeepSteppedStructureForCrossSideClosedChain(dimension, plan, outline, tol))
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

	/// <summary>
	/// Collect bottom and left outer-contour-step decisions from one immutable candidate set,
	/// then apply the shared rule once after both directions have contributed their evidence.
	/// </summary>
	private void ArbitrateOuterContourStepOverallRemainders(DimensionPlan plan, OutlineFeature2D outline)
	{
		if (plan == null || outline == null)
		{
			return;
		}
		Dictionary<PlannedDimension, string> retainedEvidence = new Dictionary<PlannedDimension, string>();
		Dictionary<PlannedDimension, Tuple<PlannedDimension, string>> suppressionEvidence =
			new Dictionary<PlannedDimension, Tuple<PlannedDimension, string>>();
		CollectBottomOuterContourStepBodyRemainders(plan, outline, retainedEvidence, suppressionEvidence);
		CollectLeftOuterContourStepBodyRemainders(plan, outline, retainedEvidence, suppressionEvidence);

		foreach (KeyValuePair<PlannedDimension, string> retained in retainedEvidence)
		{
			plan.RecordRuleEvidence(
				retained.Key,
				SuppressReason.OuterContourStepOverallRemainder,
				CollectOuterContourStepSourceGeometryIds(retained.Key, null, outline),
				PreserveBottomContourChainEvidence(retained.Key, retained.Value));
		}
		for (int i = plan.Dimensions.Count - 1; i >= 0; i--)
		{
			PlannedDimension candidate = plan.Dimensions[i];
			if (!suppressionEvidence.TryGetValue(candidate, out Tuple<PlannedDimension, string> evidence))
			{
				continue;
			}
			plan.MarkSuppressed(
				candidate,
				SuppressReason.OuterContourStepOverallRemainder,
				SuppressReason.OuterContourStepOverallRemainder,
				CollectOuterContourStepSourceGeometryIds(candidate, evidence.Item1, outline),
				evidence.Item2);
			plan.Dimensions.RemoveAt(i);
		}
	}

	/// <summary>
	/// Preserve a genuine partial MinX outer face and collect its projected height residual.
	/// Example: real left face 20 + projected lower residual 8.262 = OverallHeight 28.262.
	/// </summary>
	private void CollectLeftOuterContourStepBodyRemainders(
		DimensionPlan plan,
		OutlineFeature2D outline,
		IDictionary<PlannedDimension, string> retainedEvidence,
		IDictionary<PlannedDimension, Tuple<PlannedDimension, string>> suppressionEvidence)
	{
		if (plan == null || outline == null)
		{
			return;
		}
		PlannedDimension overall = plan.Dimensions.FirstOrDefault((PlannedDimension d) =>
			d.Role == DimensionCandidateRole.Overall
			&& d.Kind == DimensionKind.OverallHeight);
		if (overall == null || overall.Orientation != DimensionOrientation.Vertical)
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		List<PlannedDimension> snapshot = plan.Dimensions.ToList();
		List<PlannedDimension> realLeftFaces = snapshot.Where((PlannedDimension d) =>
			d.Kind == DimensionKind.Normal
			&& d.Orientation == DimensionOrientation.Vertical
			&& d.Side == DimensionSide.Left
			&& d.Role == DimensionCandidateRole.Structure
			&& IsRealPartialEnvelopeStructureHeight(d, outline, tol))
			.ToList();
		foreach (PlannedDimension face in realLeftFaces)
		{
			retainedEvidence[face] = "Left:RetainedPartialEnvelopeFace";
			foreach (PlannedDimension candidate in snapshot)
			{
				if (candidate == face
					|| candidate.Kind != DimensionKind.Normal
					|| candidate.Orientation != DimensionOrientation.Vertical
					|| candidate.Side != DimensionSide.Left
					|| !(candidate.Role == DimensionCandidateRole.Structure
						|| candidate.Role == DimensionCandidateRole.OutlineSegment)
					|| IsRealPartialEnvelopeStructureHeight(candidate, outline, tol)
					|| !FormsCompleteOverallPartition(face, candidate, overall, horizontal: false))
				{
					continue;
				}
				if (!suppressionEvidence.ContainsKey(candidate))
				{
					suppressionEvidence[candidate] =
						Tuple.Create(face, "Left:SuppressedOverallRemainder");
				}
			}
		}
	}

	/// <summary>
	/// Preserve a genuine bottom outer-contour step and remove the complementary body interval.
	/// The step may be shorter (20 of 90) or longer (120 of 215) than the body; topology decides.
	/// Chamfered steps are supported by following a connected boundary path down to MinY.
	/// </summary>
	private void CollectBottomOuterContourStepBodyRemainders(
		DimensionPlan plan,
		OutlineFeature2D outline,
		IDictionary<PlannedDimension, string> retainedEvidence,
		IDictionary<PlannedDimension, Tuple<PlannedDimension, string>> suppressionEvidence)
	{
		if (plan == null || outline == null || outline.Segments == null || outline.Segments.Count == 0)
		{
			return;
		}
		PlannedDimension overall = plan.Dimensions.FirstOrDefault((PlannedDimension d) =>
			d.Role == DimensionCandidateRole.Overall
			&& d.Kind == DimensionKind.OverallWidth);
		if (overall == null || overall.Orientation != DimensionOrientation.Horizontal)
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		Tuple<double, double> overallInterval = ComputeArrowInterval(overall, horizontal: true);
		List<PlannedDimension> snapshot = plan.Dimensions.ToList();
		foreach (PlannedDimension step in snapshot.Where((PlannedDimension d) =>
			d.Kind == DimensionKind.Normal
			&& d.Orientation == DimensionOrientation.Horizontal
			&& d.Side == DimensionSide.Bottom
			&& d.Role == DimensionCandidateRole.Structure))
		{
			if (!TryResolveRealBottomOuterStepInterval(step, outline, overallInterval, tol, out Tuple<double, double> bodyInterval))
			{
				continue;
			}
			retainedEvidence[step] = "Bottom:RetainedBoundaryBackedStep";
			foreach (PlannedDimension candidate in snapshot)
			{
				if (candidate == step
					|| candidate.Kind != DimensionKind.Normal
					|| candidate.Orientation != DimensionOrientation.Horizontal
					|| candidate.Side != DimensionSide.Bottom
					|| !(candidate.Role == DimensionCandidateRole.Structure
						|| candidate.Role == DimensionCandidateRole.OutlineSegment))
				{
					continue;
				}
				Tuple<double, double> candidateInterval = ComputeArrowInterval(candidate, horizontal: true);
				if (Math.Abs(candidateInterval.Item1 - bodyInterval.Item1) > tol
					|| Math.Abs(candidateInterval.Item2 - bodyInterval.Item2) > tol
					|| !FormsCompleteOverallPartition(candidate, step, overall, horizontal: true))
				{
					continue;
				}
				if (!suppressionEvidence.ContainsKey(candidate))
				{
					suppressionEvidence[candidate] =
						Tuple.Create(step, "Bottom:SuppressedOverallRemainder");
				}
			}
		}
	}

	private static IEnumerable<string> CollectOuterContourStepSourceGeometryIds(
		PlannedDimension candidate,
		PlannedDimension witness,
		OutlineFeature2D outline)
	{
		HashSet<string> sourceIds = new HashSet<string>(StringComparer.Ordinal);
		AddSourceGeometryIds(sourceIds, candidate);
		AddSourceGeometryIds(sourceIds, witness);
		if (outline != null)
		{
			foreach (Segment2D segment in outline.Segments ?? Enumerable.Empty<Segment2D>())
			{
				AddSourceGeometryId(sourceIds, segment?.SourceKey);
			}
			foreach (Arc2D arc in outline.Arcs ?? Enumerable.Empty<Arc2D>())
			{
				AddSourceGeometryId(sourceIds, arc?.SourceKey);
			}
		}
		return sourceIds.OrderBy((string sourceId) => sourceId, StringComparer.Ordinal).ToList();
	}

	private static void AddSourceGeometryIds(ISet<string> target, PlannedDimension dimension)
	{
		if (dimension == null)
		{
			return;
		}
		AddSourceGeometryId(target, dimension.SourceKey);
		foreach (string sourceId in dimension.SourceGeometryIds ?? Enumerable.Empty<string>())
		{
			AddSourceGeometryId(target, sourceId);
		}
	}

	private static void AddSourceGeometryId(ISet<string> target, string sourceId)
	{
		if (!string.IsNullOrWhiteSpace(sourceId))
		{
			target.Add(sourceId);
		}
	}

	private bool TryResolveRealBottomOuterStepInterval(
		PlannedDimension step,
		OutlineFeature2D outline,
		Tuple<double, double> overallInterval,
		double tol,
		out Tuple<double, double> bodyInterval)
	{
		bodyInterval = null;
		if (step == null || Math.Abs(step.FirstPoint.Y - step.SecondPoint.Y) > tol)
		{
			return false;
		}
		Tuple<double, double> stepInterval = ComputeArrowInterval(step, horizontal: true);
		bool touchesLeft = Math.Abs(stepInterval.Item1 - overallInterval.Item1) <= tol;
		bool touchesRight = Math.Abs(stepInterval.Item2 - overallInterval.Item2) <= tol;
		if (touchesLeft == touchesRight)
		{
			return false;
		}
		double baseline = (step.FirstPoint.Y + step.SecondPoint.Y) * 0.5;
		Point2D inner = touchesRight
			? (step.FirstPoint.X <= step.SecondPoint.X ? step.FirstPoint : step.SecondPoint)
			: (step.FirstPoint.X >= step.SecondPoint.X ? step.FirstPoint : step.SecondPoint);
		Point2D outer = touchesRight
			? (step.FirstPoint.X > step.SecondPoint.X ? step.FirstPoint : step.SecondPoint)
			: (step.FirstPoint.X < step.SecondPoint.X ? step.FirstPoint : step.SecondPoint);
		if (!HasConnectedBottomBoundaryPath(outline, inner, outer, baseline, stepInterval, tol)
			|| !HasUpwardReturnAtStepShoulder(outline, inner, baseline, tol)
			|| HasSameLevelBodyContinuation(outline, inner, baseline, touchesRight, tol))
		{
			return false;
		}
		bodyInterval = touchesRight
			? Tuple.Create(overallInterval.Item1, stepInterval.Item1)
			: Tuple.Create(stepInterval.Item2, overallInterval.Item2);
		return bodyInterval.Item2 > bodyInterval.Item1 + tol;
	}

	private static bool HasConnectedBottomBoundaryPath(
		OutlineFeature2D outline,
		Point2D start,
		Point2D end,
		double baseline,
		Tuple<double, double> stepInterval,
		double tol)
	{
		List<Segment2D> eligible = outline.Segments.Where((Segment2D segment) => segment != null
			&& segment.MinX >= stepInterval.Item1 - tol
			&& segment.MaxX <= stepInterval.Item2 + tol
			&& segment.Start.Y <= baseline + tol
			&& segment.End.Y <= baseline + tol)
			.ToList();
		if (eligible.Count == 0 || !eligible.Any((Segment2D segment) => segment.MinY <= outline.MinY + tol))
		{
			return false;
		}
		Queue<Point2D> queue = new Queue<Point2D>();
		List<Point2D> visited = new List<Point2D>();
		queue.Enqueue(start);
		visited.Add(start);
		while (queue.Count > 0)
		{
			Point2D current = queue.Dequeue();
			if (current.DistanceTo(end) <= tol)
			{
				return true;
			}
			foreach (Segment2D segment in eligible)
			{
				Point2D next;
				if (current.DistanceTo(segment.Start) <= tol)
				{
					next = segment.End;
				}
				else if (current.DistanceTo(segment.End) <= tol)
				{
					next = segment.Start;
				}
				else
				{
					continue;
				}
				if (visited.Any((Point2D point) => point.DistanceTo(next) <= tol))
				{
					continue;
				}
				visited.Add(next);
				queue.Enqueue(next);
			}
		}
		return false;
	}

	private static bool HasUpwardReturnAtStepShoulder(OutlineFeature2D outline, Point2D shoulder, double baseline, double tol)
	{
		return outline.Segments.Any((Segment2D segment) =>
		{
			if (segment == null)
			{
				return false;
			}
			if (shoulder.DistanceTo(segment.Start) <= tol)
			{
				return segment.End.Y > baseline + tol;
			}
			if (shoulder.DistanceTo(segment.End) <= tol)
			{
				return segment.Start.Y > baseline + tol;
			}
			return false;
		});
	}

	private static bool HasSameLevelBodyContinuation(
		OutlineFeature2D outline,
		Point2D shoulder,
		double baseline,
		bool bodyExtendsLeft,
		double tol)
	{
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || !segment.IsHorizontal(tol))
			{
				continue;
			}
			Point2D other;
			if (shoulder.DistanceTo(segment.Start) <= tol)
			{
				other = segment.End;
			}
			else if (shoulder.DistanceTo(segment.End) <= tol)
			{
				other = segment.Start;
			}
			else
			{
				continue;
			}
			if (Math.Abs(other.Y - baseline) > tol)
			{
				continue;
			}
			if (bodyExtendsLeft ? other.X < shoulder.X - tol : other.X > shoulder.X + tol)
			{
				return true;
			}
		}
		return false;
	}

	private void SuppressComplementaryOutlineRemainders(DimensionPlan plan, DimensionSide side, bool horizontal, OutlineFeature2D outline)
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
				// A right-side top partial envelope is the real upper-rectangle height;
				// never discard it as the larger complementary remainder.
				if (side == DimensionSide.Right
					&& !horizontal
					&& IsRightTopPartialEnvelopeStructureHeight(candidate, outline, _config.GeometryTolerance))
				{
					continue;
				}
				// Once that real upper-rectangle height is retained, its lower partner is
				// the complementary remainder and must be the one removed.
				if (side == DimensionSide.Right
					&& !horizontal
					&& source.Any((PlannedDimension other) => other != candidate
						&& IsRightTopPartialEnvelopeStructureHeight(other, outline, _config.GeometryTolerance)
						&& FormsCompleteOverallPartition(candidate, other, overall, horizontal)))
				{
					plan.MarkSuppressed(candidate, SuppressReason.ComplementaryOutlineRemainder);
					plan.Dimensions.RemoveAt(num);
					continue;
				}
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
	/// Suppress the larger length piece when it pairs with a smaller length piece to form a
	/// complete overall partition (closed chain). Decision A: keep overall + short step, drop body.
	/// Partners may be any side (cross-side Top/Bottom or same-side after interior OS resolves to Bottom).
	/// Roles: structure widths/heights, chamfered steps, and axis-aligned OutlineSegments.
	/// </summary>
	internal void SuppressClosedOverallLengthRemainders(DimensionPlan plan, bool horizontal)
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
		List<PlannedDimension> lengthDims = plan.Dimensions
			.Where((PlannedDimension d) => d.Kind == DimensionKind.Normal
				&& d.Orientation == expected
				&& IsClosedChainLengthRole(d.DebugRole, horizontal))
			.ToList();
		if (lengthDims.Count < 2)
		{
			return;
		}
		for (int num = plan.Dimensions.Count - 1; num >= 0; num--)
		{
			PlannedDimension candidate = plan.Dimensions[num];
			if (candidate.Kind != DimensionKind.Normal
				|| candidate.Orientation != expected
				|| !IsClosedChainLengthRole(candidate.DebugRole, horizontal))
			{
				continue;
			}
			double span = GetDimensionSpan(candidate, horizontal);
			// Same-side structure/structure pairs stay with legacy structure partition rules.
			// This pass covers: cross-side structure pairs, and any pair involving OutlineSegment.
			if (!lengthDims.Any((PlannedDimension other) => other != candidate
				&& GetDimensionSpan(other, horizontal) < span - _config.GeometryTolerance
				&& FormsCompleteOverallPartition(candidate, other, overall, horizontal)
				&& IsClosedChainPairingAllowed(candidate, other)))
			{
				continue;
			}
			plan.MarkSuppressed(candidate, SuppressReason.ComplementaryOutlineRemainder);
			plan.Dimensions.RemoveAt(num);
		}
	}

	/// <summary>Backward-compatible alias used by older unit tests. </summary>
	internal void SuppressCrossSideComplementaryStructureRemainders(DimensionPlan plan, bool horizontal)
	{
		SuppressClosedOverallLengthRemainders(plan, horizontal);
	}

	private static bool IsClosedChainLengthRole(string debugRole, bool horizontal)
	{
		if (string.Equals(debugRole, "OutlineSegment", StringComparison.Ordinal))
		{
			return true;
		}
		return IsClosedChainStructureRole(debugRole, horizontal);
	}

	private static bool IsClosedChainPairingAllowed(PlannedDimension a, PlannedDimension b)
	{
		if (a == null || b == null)
		{
			return false;
		}
		bool aOs = string.Equals(a.DebugRole, "OutlineSegment", StringComparison.Ordinal);
		bool bOs = string.Equals(b.DebugRole, "OutlineSegment", StringComparison.Ordinal);
		if (aOs || bOs)
		{
			return true;
		}
		// Structure/structure: only when opposite sides (cross-side body + step).
		return a.Side != b.Side;
	}

	private static bool IsClosedChainStructureRole(string debugRole, bool horizontal)
	{
		if (horizontal)
		{
			return IsHorizontalStructureWidthRole(debugRole)
				|| string.Equals(debugRole, "TopChamferedStepWidth", StringComparison.Ordinal);
		}
		return string.Equals(debugRole, "LeftStructHeight", StringComparison.Ordinal)
			|| string.Equals(debugRole, "RightStructHeight", StringComparison.Ordinal)
			|| string.Equals(debugRole, "RightChamferedStepHeight", StringComparison.Ordinal);
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
		TryGetOpenBottomContourChain(
			outline,
			out Segment2D openBottomOuter,
			out Segment2D openBottomMiddle,
			out Segment2D openBottomMinY);
		foreach (DimensionDeduplicationItem coverItem in cover)
		{
			if (itemToDim.TryGetValue(coverItem, out PlannedDimension match))
			{
				if (horizontal
					&& side == DimensionSide.Bottom
					&& openBottomMiddle != null
					&& HasSameSegmentEndpoints(openBottomMiddle, match.FirstPoint, match.SecondPoint))
				{
					continue;
				}
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

	private void SuppressOpenBottomContourDerivableOuterStructure(DimensionPlan plan, OutlineFeature2D outline)
	{
		if (plan == null || outline == null
			|| !TryGetOpenBottomContourChain(
				outline,
				out Segment2D outer,
				out Segment2D middle,
				out Segment2D minY))
		{
			return;
		}
		if (!plan.Dimensions.Any((PlannedDimension dimension) =>
			dimension.Kind == DimensionKind.Normal
			&& string.Equals(dimension.DebugRole, "OutlineSegment", StringComparison.Ordinal)
			&& HasSameSegmentEndpoints(middle, dimension.FirstPoint, dimension.SecondPoint)))
		{
			return;
		}
		double tol = _config.GeometryTolerance;
		double outerMinX = Math.Min(outer.Start.X, outer.End.X);
		double outerMaxX = Math.Max(outer.Start.X, outer.End.X);
		foreach (PlannedDimension candidate in plan.Dimensions.ToList())
		{
			if (candidate.Kind != DimensionKind.Normal
				|| candidate.Orientation != DimensionOrientation.Horizontal
				|| candidate.Side != DimensionSide.Bottom
				|| !string.Equals(candidate.DebugRole, "BottomStructWidth", StringComparison.Ordinal)
				|| !IsCrossLevelStructureProjection(candidate, horizontal: true))
			{
				continue;
			}
			Tuple<double, double> interval = ComputeArrowInterval(candidate, horizontal: true);
			if (Math.Abs(interval.Item1 - outerMinX) > tol
				|| Math.Abs(interval.Item2 - outerMaxX) > tol)
			{
				continue;
			}
			plan.MarkSuppressed(
				candidate,
				SuppressReason.ProjectedStructureOverallPartition,
				SuppressReason.ProjectedStructureOverallPartition,
				new[] { outer.SourceKey, middle.SourceKey, minY.SourceKey },
				"Bottom:OpenContourDerivableOuterInterval");
			plan.Dimensions.Remove(candidate);
		}
	}

	private bool TryGetOpenBottomContourChain(
		OutlineFeature2D outline,
		out Segment2D outer,
		out Segment2D middle,
		out Segment2D minY)
	{
		outer = null;
		middle = null;
		minY = null;
		if (outline == null || outline.Segments == null)
		{
			return false;
		}
		double tol = _config.GeometryTolerance;
		List<Segment2D> chain = outline.Segments
			.Where((Segment2D segment) => segment != null
				&& !segment.IsArcChord
				&& segment.IsHorizontal(tol)
				&& !IsOverallBoundarySegment(segment, outline))
			.OrderBy((Segment2D segment) => segment.MinX)
			.ToList();
		if (chain.Count != 3
			|| Math.Abs(chain[0].MinX - outline.MinX) > tol
			|| Math.Abs(chain[2].MaxX - outline.MaxX) > tol
			|| Math.Abs(chain[0].MaxX - chain[1].MinX) > tol
			|| Math.Abs(chain[1].MaxX - chain[2].MinX) > tol)
		{
			return false;
		}
		bool rightMinY = Math.Abs(chain[2].MinY - outline.MinY) <= tol
			&& chain[0].MinY > chain[1].MinY + tol
			&& chain[1].MinY > chain[2].MinY + tol;
		bool leftMinY = Math.Abs(chain[0].MinY - outline.MinY) <= tol
			&& chain[0].MinY < chain[1].MinY - tol
			&& chain[1].MinY < chain[2].MinY - tol;
		if (!rightMinY && !leftMinY)
		{
			return false;
		}
		bool openAtBoundary = rightMinY
			? !HasContinuousStraightOutlineEdge(outline, horizontal: false, outline.MinX, outline.MinY, chain[0].MinY)
			: !HasContinuousStraightOutlineEdge(outline, horizontal: false, outline.MaxX, outline.MinY, chain[2].MinY);
		if (!openAtBoundary)
		{
			return false;
		}
		outer = rightMinY ? chain[0] : chain[2];
		middle = chain[1];
		minY = rightMinY ? chain[2] : chain[0];
		return true;
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
			if (HasBottomContourChainEvidence(structure))
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
			if (IsRealPartialEnvelopeStructureWidth(structure, outline, tol))
			{
				continue;
			}
			double structureSpan = GetDimensionSpan(structure, horizontal: true);
			// Short tip: less than half overall width. Step ledges are typically larger.
			if (structureSpan >= overallWidthSpan * 0.5 - tol)
			{
				continue;
			}
			// Real step length that closes overall with an opposite-side body length must stay;
			// the closed-chain partner is removed by the cross-side complementary pass.
			if (ShouldKeepSteppedStructureForCrossSideClosedChain(structure, plan, outline, tol))
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
	internal void SuppressOrphanOuterVerticalStructureHeightTips(DimensionPlan plan, OutlineFeature2D outline = null)
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
					if (IsRealPartialEnvelopeStructureHeight(d, outline, tol))
					{
						continue;
					}
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

	/// <summary>
	/// Protects a short left/right structure height when it is backed by a real straight
	/// segment on a partial (stepped) outer-envelope edge. Overall height cannot replace it.
	/// </summary>
	private bool IsRealPartialEnvelopeStructureHeight(PlannedDimension dimension, OutlineFeature2D outline, double tol)
	{
		if (dimension == null)
		{
			return false;
		}
		DimensionCandidateSemantics.ApplyLegacyMappings(dimension);
		if (outline == null || outline.Segments == null || outline.Segments.Count == 0
			|| dimension.Kind != DimensionKind.Normal
			|| dimension.Orientation != DimensionOrientation.Vertical
			|| dimension.Role != DimensionCandidateRole.Structure
			|| dimension.ForceOuterLevel
			|| (dimension.Side != DimensionSide.Left && dimension.Side != DimensionSide.Right))
		{
			return false;
		}
		bool onMinX = Math.Abs(dimension.FirstPoint.X - outline.MinX) <= tol
			&& Math.Abs(dimension.SecondPoint.X - outline.MinX) <= tol;
		bool onMaxX = Math.Abs(dimension.FirstPoint.X - outline.MaxX) <= tol
			&& Math.Abs(dimension.SecondPoint.X - outline.MaxX) <= tol;
		if (!onMinX && !onMaxX)
		{
			return false;
		}
		double edgeX = onMinX ? outline.MinX : outline.MaxX;
		if (IsFullLengthEnvelopeEdge(outline, horizontal: false, edgeX, tol))
		{
			return false;
		}
		List<Tuple<double, double>> realEdgeIntervals = outline.Segments
			.Where(segment => segment != null
				&& segment.IsVertical(tol)
				&& Math.Abs(segment.Start.X - edgeX) <= tol
				&& Math.Abs(segment.End.X - edgeX) <= tol)
			.Select(segment => Tuple.Create(segment.MinY, segment.MaxY))
			.ToList();
		if (realEdgeIntervals.Count == 0)
		{
			return false;
		}
		Tuple<double, double> dimensionInterval = ComputeArrowInterval(dimension, horizontal: false);
		return MergeVerticalIntervals(realEdgeIntervals, tol).Any(interval =>
			dimensionInterval.Item1 >= interval.Item1 - tol
			&& dimensionInterval.Item2 <= interval.Item2 + tol);
	}

	private bool IsRealPartialEnvelopeStructureWidth(PlannedDimension dimension, OutlineFeature2D outline, double tol)
	{
		if (dimension == null)
		{
			return false;
		}
		DimensionCandidateSemantics.ApplyLegacyMappings(dimension);
		if (outline == null || outline.Segments == null || outline.Segments.Count == 0
			|| dimension.Kind != DimensionKind.Normal
			|| dimension.Orientation != DimensionOrientation.Horizontal
			|| dimension.Role != DimensionCandidateRole.Structure
			|| dimension.ForceOuterLevel
			|| dimension.Side != DimensionSide.Top)
		{
			return false;
		}
		bool onMinY = Math.Abs(dimension.FirstPoint.Y - outline.MinY) <= tol
			&& Math.Abs(dimension.SecondPoint.Y - outline.MinY) <= tol;
		bool onMaxY = Math.Abs(dimension.FirstPoint.Y - outline.MaxY) <= tol
			&& Math.Abs(dimension.SecondPoint.Y - outline.MaxY) <= tol;
		if (!onMinY && !onMaxY)
		{
			return false;
		}
		double edgeY = onMinY ? outline.MinY : outline.MaxY;
		if (IsFullLengthEnvelopeEdge(outline, horizontal: true, edgeY, tol))
		{
			return false;
		}
		Tuple<double, double> interval = ComputeArrowInterval(dimension, horizontal: true);
		return outline.Segments.Any(segment => segment != null
			&& segment.IsHorizontal(tol)
			&& Math.Abs(segment.Start.Y - edgeY) <= tol
			&& Math.Abs(segment.End.Y - edgeY) <= tol
			&& interval.Item1 >= segment.MinX - tol
			&& interval.Item2 <= segment.MaxX + tol);
	}

	private bool IsRightTopPartialEnvelopeStructureHeight(PlannedDimension dimension, OutlineFeature2D outline, double tol)
	{
		if (dimension == null || outline == null || outline.Segments == null
			|| dimension.Kind != DimensionKind.Normal
			|| dimension.Orientation != DimensionOrientation.Vertical
			|| dimension.Side != DimensionSide.Right
			|| dimension.DebugRole != "RightStructHeight"
			|| dimension.ForceOuterLevel)
		{
			return false;
		}
		Tuple<double, double> interval = ComputeArrowInterval(dimension, horizontal: false);
		if (interval.Item2 < outline.MaxY - tol || interval.Item1 <= outline.MinY + tol)
		{
			return false;
		}
		return outline.Segments.Any(segment => segment != null
			&& segment.IsHorizontal(tol)
			&& Math.Abs(segment.MinY - interval.Item1) <= tol)
			&& outline.Segments.Any(segment => segment != null
				&& segment.IsHorizontal(tol)
				&& Math.Abs(segment.MinY - interval.Item2) <= tol);
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

	private void SuppressMirroredDuplicates(DimensionPlan plan, DimensionSide primarySide, DimensionSide secondarySide, bool horizontal, OutlineFeature2D outline)
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
				int geometryPreference = CompareGeometricMirrorPreference(item2, item, outline, horizontal);
				PlannedDimension plannedDimension = geometryPreference > 0
					? item
					: (geometryPreference < 0
						? item2
						: ((CompareDuplicatePreference(item, item2) > 0) ? item2 : item));
				plan.MarkSuppressed(plannedDimension, SuppressReason.MirroredDuplicate);
				plan.Dimensions.Remove(plannedDimension);
				break;
			}
		}
	}

	/// <summary>
	/// Mirror ownership follows the contour that actually supports the structure dimension.
	/// This prevents a cross-level projection from winning only because Bottom/Left is the
	/// historical primary side; ties keep the existing fallback for symmetric geometry.
	/// </summary>
	private int CompareGeometricMirrorPreference(PlannedDimension first, PlannedDimension second, OutlineFeature2D outline, bool horizontal)
	{
		if (first == null || second == null || outline == null
			|| !IsStructureWidthOrHeightRole(first.DebugRole)
			|| !IsStructureWidthOrHeightRole(second.DebugRole)
			|| (horizontal
				? (first.Side != DimensionSide.Bottom && first.Side != DimensionSide.Top)
				: (first.Side != DimensionSide.Left && first.Side != DimensionSide.Right))
			|| (horizontal
				? (second.Side != DimensionSide.Bottom && second.Side != DimensionSide.Top)
				: (second.Side != DimensionSide.Left && second.Side != DimensionSide.Right)))
		{
			return 0;
		}
		bool firstContourBacked = HasRealStructurePartitionEdge(first, outline, horizontal);
		bool secondContourBacked = HasRealStructurePartitionEdge(second, outline, horizontal);
		if (firstContourBacked != secondContourBacked)
		{
			return firstContourBacked ? 1 : -1;
		}
		return 0;
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
			if (focusOnChain
				&& !ShouldKeepSteppedStructureForCrossSideClosedChain(focus, plan, outline, tol)
				&& !IsSameSideProjectedStructureOverallChainMember(focus, plan, outline, overallInterval, horizontal))
			{
				// Keep step lengths only when they close overall with an opposite-side body
				// length; that partner is removed by the cross-side complementary pass.
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

	private void SuppressProjectedStructureDimensionsThatPartitionOverall(DimensionPlan plan, OutlineFeature2D outline, bool horizontal)
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
		Tuple<double, double> overallInterval = ComputeArrowInterval(overall, horizontal);
		DimensionSide[] sides = horizontal
			? new[] { DimensionSide.Top, DimensionSide.Bottom }
			: new[] { DimensionSide.Left, DimensionSide.Right };
		double tol = _config.GeometryTolerance;
		foreach (DimensionSide side in sides)
		{
			List<PlannedDimension> chainDimensions = FindSameSideStructureOverallChain(plan, outline, side, expected, overallInterval, horizontal);
			if (!chainDimensions.Any((PlannedDimension dimension) => HasRealStructurePartitionEdge(dimension, outline, horizontal)))
			{
				continue;
			}
			if (chainDimensions.Count == 2)
			{
				foreach (PlannedDimension candidate in chainDimensions)
				{
					if (!HasRealStructurePartitionEdge(candidate, outline, horizontal)
						&& IsCrossLevelStructureProjection(candidate, horizontal)
						&& !HasOppositeSideSameIntervalStructure(candidate, plan, horizontal))
					{
						SuppressProjectedStructureOverallPartition(plan, candidate);
					}
				}
				continue;
			}
			if (!HasProjectedStructureChainTopology(chainDimensions, horizontal, tol))
			{
				continue;
			}
			PlannedDimension overallRemainder = null;
			PlannedDimension minimumPiece = chainDimensions[0];
			PlannedDimension maximumPiece = chainDimensions[chainDimensions.Count - 1];
			if (HasRealStructurePartitionEdge(minimumPiece, outline, horizontal)
				&& !HasRealStructurePartitionEdge(maximumPiece, outline, horizontal))
			{
				overallRemainder = maximumPiece;
			}
			else if (!HasRealStructurePartitionEdge(minimumPiece, outline, horizontal)
				&& HasRealStructurePartitionEdge(maximumPiece, outline, horizontal))
			{
				overallRemainder = minimumPiece;
			}
			if (overallRemainder != null
				&& !HasRealStructurePartitionEdge(overallRemainder, outline, horizontal)
				&& IsCrossLevelStructureProjection(overallRemainder, horizontal)
				&& !HasOppositeSideSameIntervalStructure(overallRemainder, plan, horizontal))
			{
				SuppressProjectedStructureOverallPartition(plan, overallRemainder);
			}
		}
	}

	private bool IsSameSideProjectedStructureOverallChainMember(PlannedDimension focus, DimensionPlan plan, OutlineFeature2D outline, Tuple<double, double> overallInterval, bool horizontal)
	{
		List<PlannedDimension> chain = FindSameSideStructureOverallChain(plan, outline, focus.Side, focus.Orientation, overallInterval, horizontal);
		return chain.Contains(focus)
			&& chain.Any((PlannedDimension dimension) => IsCrossLevelStructureProjection(dimension, horizontal));
	}

	private List<PlannedDimension> FindSameSideStructureOverallChain(DimensionPlan plan, OutlineFeature2D outline, DimensionSide side, DimensionOrientation expected, Tuple<double, double> overallInterval, bool horizontal)
	{
		List<PlannedDimension> dimensions = plan.Dimensions
			.Where((PlannedDimension d) => d.Kind == DimensionKind.Normal
				&& d.Side == side
				&& d.Orientation == expected
				&& IsStructureWidthOrHeightRole(d.DebugRole))
			.OrderBy((PlannedDimension d) => ComputeArrowInterval(d, horizontal).Item1)
			.ThenBy((PlannedDimension d) => ComputeArrowInterval(d, horizontal).Item2)
			.ThenBy((PlannedDimension d) => HasRealStructurePartitionEdge(d, outline, horizontal) ? 0 : 1)
			.ThenBy((PlannedDimension d) => GetProjectedStructureCrossAxisMin(d, horizontal))
			.ThenBy((PlannedDimension d) => GetProjectedStructureCrossAxisMax(d, horizontal))
			.ToList();
		var itemToDimension = new Dictionary<DimensionDeduplicationItem, PlannedDimension>();
		var items = new List<DimensionDeduplicationItem>(dimensions.Count);
		foreach (PlannedDimension dimension in dimensions)
		{
			DimensionDeduplicationItem item = ToDeduplicationItem(dimension);
			items.Add(item);
			itemToDimension[item] = dimension;
		}
		IList<DimensionDeduplicationItem> chain = _dimensionDeduplicationRules.FindCompleteOverallPartitionChain(items, overallInterval.Item1, overallInterval.Item2, horizontal);
		return chain == null
			? new List<PlannedDimension>()
			: chain.Where((DimensionDeduplicationItem item) => itemToDimension.ContainsKey(item))
				.Select((DimensionDeduplicationItem item) => itemToDimension[item])
				.ToList();
	}

	private static double GetProjectedStructureCrossAxisMin(PlannedDimension dimension, bool horizontal)
	{
		return horizontal
			? Math.Min(dimension.FirstPoint.Y, dimension.SecondPoint.Y)
			: Math.Min(dimension.FirstPoint.X, dimension.SecondPoint.X);
	}

	private static double GetProjectedStructureCrossAxisMax(PlannedDimension dimension, bool horizontal)
	{
		return horizontal
			? Math.Max(dimension.FirstPoint.Y, dimension.SecondPoint.Y)
			: Math.Max(dimension.FirstPoint.X, dimension.SecondPoint.X);
	}

	private static Point2D GetProjectedStructureIntervalEndpoint(PlannedDimension dimension, bool horizontal, bool minimum)
	{
		double first = horizontal ? dimension.FirstPoint.X : dimension.FirstPoint.Y;
		double second = horizontal ? dimension.SecondPoint.X : dimension.SecondPoint.Y;
		bool firstIsMinimum = first <= second;
		return firstIsMinimum == minimum ? dimension.FirstPoint : dimension.SecondPoint;
	}

	private static bool HasProjectedStructureChainTopology(IList<PlannedDimension> chain, bool horizontal, double tolerance)
	{
		for (int i = 1; i < chain.Count; i++)
		{
			Point2D previousMaximum = GetProjectedStructureIntervalEndpoint(chain[i - 1], horizontal, minimum: false);
			Point2D currentMinimum = GetProjectedStructureIntervalEndpoint(chain[i], horizontal, minimum: true);
			if (Math.Abs(previousMaximum.X - currentMinimum.X) > tolerance
				|| Math.Abs(previousMaximum.Y - currentMinimum.Y) > tolerance)
			{
				return false;
			}
		}
		return true;
	}

	private void SuppressProjectedStructureOverallPartition(DimensionPlan plan, PlannedDimension candidate)
	{
		plan.MarkSuppressed(candidate, SuppressReason.ProjectedStructureOverallPartition);
		plan.Dimensions.Remove(candidate);
	}

	private bool HasOppositeSideSameIntervalStructure(PlannedDimension candidate, DimensionPlan plan, bool horizontal)
	{
		if (candidate == null || plan == null)
		{
			return false;
		}
		DimensionSide opposite = candidate.Side == DimensionSide.Top ? DimensionSide.Bottom
			: candidate.Side == DimensionSide.Bottom ? DimensionSide.Top
			: candidate.Side == DimensionSide.Left ? DimensionSide.Right
			: candidate.Side == DimensionSide.Right ? DimensionSide.Left
			: candidate.Side;
		return opposite != candidate.Side && plan.Dimensions.Any((PlannedDimension dimension) => dimension != candidate
			&& dimension.Kind == DimensionKind.Normal
			&& dimension.Side == opposite
			&& dimension.Orientation == candidate.Orientation
			&& IsStructureWidthOrHeightRole(dimension.DebugRole)
			&& IsSameMeasurementInterval(candidate, dimension, horizontal));
	}

	private bool IsCrossLevelStructureProjection(PlannedDimension dimension, bool horizontal)
	{
		if (dimension == null)
		{
			return false;
		}
		double tol = _config.GeometryTolerance;
		return horizontal
			? Math.Abs(dimension.FirstPoint.Y - dimension.SecondPoint.Y) > tol
			: Math.Abs(dimension.FirstPoint.X - dimension.SecondPoint.X) > tol;
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
