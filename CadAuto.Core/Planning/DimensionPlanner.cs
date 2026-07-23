using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning;

public sealed class DimensionPlanner
{
	private sealed class StructurePoint
	{
		public Point2D Point { get; set; }

		public string Source { get; set; }
	}

	private sealed class IgnoredPoint
	{
		public Point2D Point { get; set; }

		public string Reason { get; set; }
	}

	private sealed class FunctionalHoleGroupPlan
	{
		public PinGroupPlan PinGroup { get; set; }

		public List<HoleFeature2D> Holes { get; private set; }

		public FunctionalHoleGroupPlan()
		{
			Holes = new List<HoleFeature2D>();
		}
	}

	private sealed class LooseHoleLineGroup
	{
		public bool Horizontal { get; set; }

		public string SpecKey { get; set; }

		public List<HoleFeature2D> Holes { get; private set; }

		public LooseHoleLineGroup()
		{
			Holes = new List<HoleFeature2D>();
		}
	}

	private sealed class LooseHoleMacroGroup
	{
		public List<LooseHoleLineGroup> LineGroups { get; private set; }

		public List<HoleFeature2D> Holes { get; private set; }

		public LooseHoleMacroGroup()
		{
			LineGroups = new List<LooseHoleLineGroup>();
			Holes = new List<HoleFeature2D>();
		}
	}

	private sealed class LooseHoleLocationPlan
	{
		public LooseHoleMacroGroup MacroGroup { get; set; }

		public PinGroupPlan ReferencePinGroup { get; set; }

		public HoleFeature2D AnchorHole { get; set; }
	}

	private readonly DimensionRuleConfig _config;

	private readonly StructureSuppressionRules _structureSuppressionRules;

	private readonly StructureEndpointRules _structureEndpointRules;

	private readonly DimensionDeduplicationRules _dimensionDeduplicationRules;

	private readonly DimensionPlanPostValidator _postValidator;

	private int _nextLooseChainId = 1;

	public DimensionPlanner(DimensionRuleConfig config)
	{
		_config = config;
		_structureSuppressionRules = new StructureSuppressionRules(config);
		_structureEndpointRules = new StructureEndpointRules(config);
		_dimensionDeduplicationRules = new DimensionDeduplicationRules(config);
		_postValidator = new DimensionPlanPostValidator(config);
	}

	public DimensionPlan CreateOutlinePlan(OutlineFeature2D outline)
	{
		if (outline == null)
		{
			throw new ArgumentNullException("outline");
		}
		_nextLooseChainId = 1;
		DimensionPlan dimensionPlan = new DimensionPlan();
		AddOverallWidth(dimensionPlan, outline);
		AddOverallHeight(dimensionPlan, outline);
		AddStepOutlineDimensions(dimensionPlan, outline);
		AddLinearSegmentDimensions(dimensionPlan, outline);
		SuppressDuplicateDimensions(dimensionPlan);
		_postValidator.Validate(dimensionPlan, outline);
		dimensionPlan.CaptureFinalDimensions();
		return dimensionPlan;
	}

	public DimensionPlan CreateDimensionPlan(OutlineFeature2D outline, Datum2D datum, IEnumerable<HoleFeature2D> holes)
	{
		return CreateDimensionPlan(outline, datum, holes, new SlotFeature2D[0]);
	}

	public DimensionPlan CreateDimensionPlan(OutlineFeature2D outline, Datum2D datum, IEnumerable<HoleFeature2D> holes, IEnumerable<SlotFeature2D> slots)
	{
		DimensionPlan dimensionPlan = CreateOutlinePlan(outline);
		Datum2D datum2 = datum ?? Datum2D.FromOutline(outline);
		List<HoleFeature2D> holes2 = (holes ?? new HoleFeature2D[0]).Where((HoleFeature2D h) => h != null).ToList();
		AddHolePositionDimensions(dimensionPlan, outline, datum2, holes2);
		AddSlotDimensions(dimensionPlan, outline, datum2, holes2, slots ?? new SlotFeature2D[0]);
		SuppressDuplicateDimensions(dimensionPlan);
		_postValidator.Validate(dimensionPlan, outline);
		dimensionPlan.CaptureFinalDimensions();
		return dimensionPlan;
	}

	public IList<IList<HoleFeature2D>> GroupHolesByHorizontalRow(IEnumerable<HoleFeature2D> holes)
	{
		List<HoleFeature2D> list = (from h in holes ?? new HoleFeature2D[0]
			where h != null
			orderby h.Center.Y, h.Center.X
			select h).ToList();
		List<IList<HoleFeature2D>> list2 = new List<IList<HoleFeature2D>>();
		foreach (HoleFeature2D hole in list)
		{
			IList<HoleFeature2D> list3 = list2.FirstOrDefault((IList<HoleFeature2D> r) => Math.Abs(GetAverageY(r) - hole.Center.Y) <= _config.GeometryTolerance);
			if (list3 == null)
			{
				list2.Add(new List<HoleFeature2D> { hole });
			}
			else
			{
				list3.Add(hole);
			}
		}
		foreach (IList<HoleFeature2D> item in list2)
		{
			List<HoleFeature2D> list4 = item.OrderBy((HoleFeature2D h) => h.Center.X).ToList();
			item.Clear();
			foreach (HoleFeature2D item2 in list4)
			{
				item.Add(item2);
			}
		}
		return list2.OrderBy((IList<HoleFeature2D> r) => GetAverageY(r)).ToList();
	}

	public IList<PinGroupPlan> BuildPinGroupPlan(IEnumerable<HoleFeature2D> holes, HoleFeature2D datumPin)
	{
		List<HoleFeature2D> list = (holes ?? new HoleFeature2D[0]).Where((HoleFeature2D h) => h != null && !h.IsSlotPoint).ToList();
		List<HoleFeature2D> list2 = (from h in list
			where h.IsPinHole
			orderby h.Center.X, h.Center.Y
			select h).ToList();
		List<PinGroupPlan> list3 = new List<PinGroupPlan>();
		if (list2.Count == 0)
		{
			return list3;
		}
		HoleFeature2D holeFeature2D = ((datumPin != null && datumPin.IsPinHole) ? (list2.FirstOrDefault((HoleFeature2D h) => IsSameHole(h, datumPin)) ?? datumPin) : (from h in list2
			orderby h.Center.X, h.Center.Y
			select h).First());
		PinGroupPlan pinGroupPlan = CreatePinPairGroup(holeFeature2D, list2, holeFeature2D, null);
		list3.Add(pinGroupPlan);
		RemoveGroupPins(list2, pinGroupPlan);
		while (list2.Count > 0)
		{
			HoleFeature2D previousBase = list3[list3.Count - 1].BasePin;
			holeFeature2D = (from h in list2
				orderby DistanceSquared(h.Center, previousBase.Center), h.Center.X, h.Center.Y
				select h).First();
			PinGroupPlan pinGroupPlan2 = CreatePinPairGroup(holeFeature2D, list2, null, previousBase);
			list3.Add(pinGroupPlan2);
			RemoveGroupPins(list2, pinGroupPlan2);
		}
		for (int num = 0; num < list3.Count; num++)
		{
			list3[num].GroupIndex = num + 1;
		}
		AssignNonPinHolesToNearestPinPair(list, list3);
		return list3;
	}

	private void AddHolePositionDimensions(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, IEnumerable<HoleFeature2D> holes)
	{
		List<HoleFeature2D> list = (holes ?? new HoleFeature2D[0]).Where((HoleFeature2D h) => h != null && !h.IsSlotPoint).ToList();
		if (list.Count == 0)
		{
			return;
		}
		IList<PinGroupPlan> list2 = BuildPinGroupPlan(list, datum.DatumHole);
		foreach (PinGroupPlan item in list2)
		{
			item.HorizontalSide = ChooseHorizontalHoleSide(outline, datum, item.BasePin.Center);
			item.VerticalSide = ChooseVerticalHoleSide(outline, datum, item.BasePin.Center);
			plan.PinGroups.Add(item);
		}
		if (list2.Count == 0)
		{
			AddNonPinHolesFromOutlineEdge(plan, outline, datum, list);
			return;
		}
		AddFirstPinGroupBaseLocation(plan, outline, datum, list2[0]);
		AddPinGroupBaseTransfers(plan, list2);
		AddSameGroupPinDistances(plan, outline, list2);
		AddNonPinHoleLocationsFromPinGroups(plan, outline, list, list2);
	}

	private void AddSlotDimensions(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, IEnumerable<HoleFeature2D> holes, IEnumerable<SlotFeature2D> slots)
	{
		List<SlotFeature2D> list = (slots ?? new SlotFeature2D[0]).Where((SlotFeature2D s) => s != null).ToList();
		if (list.Count == 0)
		{
			return;
		}
		IList<IList<HoleFeature2D>> list2 = GroupHolesByHorizontalRow(holes ?? new HoleFeature2D[0]);
		bool flag = list2.SelectMany((IList<HoleFeature2D> r) => r).Any((HoleFeature2D h) => h.IsPinHole);
		foreach (SlotFeature2D item in list)
		{
			AddSlotCenterDistanceDimension(plan, item);
		}
		if (!flag)
		{
			AddSlotContinuousLocationsFromDatum(plan, outline, datum, list);
		}
		else
		{
			AddSlotAnchorLocations(plan, outline, datum, list2, list);
		}
	}

	private void AddSlotContinuousLocationsFromDatum(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, IList<SlotFeature2D> slots)
	{
		if (datum != null && slots != null && slots.Count != 0)
		{
			List<SlotFeature2D> slots2 = slots.Where(IsVerticalSlot).ToList();
			List<SlotFeature2D> slots3 = slots.Where((SlotFeature2D s) => !IsVerticalSlot(s)).ToList();
			AddVerticalSlotContinuousDimensions(plan, outline, datum, slots2);
			AddHorizontalSlotContinuousDimensions(plan, outline, datum, slots3);
		}
	}

	private void AddVerticalSlotContinuousDimensions(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, IList<SlotFeature2D> slots)
	{
		foreach (List<Point2D> item in GroupSlotAnchorsByCoordinate(slots, datum, (Point2D p) => p.Y))
		{
			List<Point2D> list = UniquePointsByCoordinate(item.OrderBy((Point2D p) => p.X), (Point2D p) => p.X);
			AddHorizontalChainFromOutlineDatum(plan, outline, datum.BaseX, list, "SlotChainH");
			if (list.Count > 0)
			{
				Point2D target = PickNearestPointByX(list, datum.BaseX);
				AddVerticalOutlineReferenceDimension(plan, outline, datum.BaseY, target, DimensionKind.Normal, DimensionSide.Left, string.Empty, "SlotDatumV");
			}
		}
	}

	private void AddHorizontalSlotContinuousDimensions(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, IList<SlotFeature2D> slots)
	{
		foreach (List<Point2D> item in GroupSlotAnchorsByCoordinate(slots, datum, (Point2D p) => p.X))
		{
			List<Point2D> list = UniquePointsByCoordinate(item.OrderBy((Point2D p) => p.Y), (Point2D p) => p.Y);
			AddVerticalChainFromOutlineDatum(plan, outline, datum.BaseY, list, "SlotChainV");
			if (list.Count != 0)
			{
				Point2D point2D = PickNearestPointByY(list, datum.BaseY);
				SlotFeature2D slotFeature2D = FindSlotByAnchor(slots, point2D);
				if (slotFeature2D != null && slotFeature2D.IsSingleArcSlot && outline != null)
				{
					AddSingleArcSlotHorizontalDatumDimension(plan, outline, datum, slotFeature2D, point2D);
				}
				else
				{
					AddHorizontalOutlineReferenceDimension(plan, outline, datum.BaseX, point2D, DimensionKind.Normal, DimensionSide.Bottom, string.Empty, "SlotDatumH");
				}
			}
		}
	}

	private void AddSlotCenterDistanceDimension(DimensionPlan plan, SlotFeature2D slot)
	{
		if (slot == null || slot.CenterDistance <= _config.GeometryTolerance)
		{
			return;
		}
		Point2D firstCenter = slot.FirstCenter;
		Point2D secondCenter = slot.SecondCenter;
		double num = Math.Abs(firstCenter.X - secondCenter.X);
		double num2 = Math.Abs(firstCenter.Y - secondCenter.Y);
		if (!(num <= _config.GeometryTolerance) || !(num2 <= _config.GeometryTolerance))
		{
			if (num >= num2)
			{
				AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Bottom, firstCenter, secondCenter, string.Empty, "SlotCenter", slot.GroupId);
			}
			else
			{
				AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Vertical, DimensionSide.Left, firstCenter, secondCenter, string.Empty, "SlotCenter", slot.GroupId);
			}
		}
	}

	private void AddSlotAnchorLocations(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, IList<IList<HoleFeature2D>> rows, IList<SlotFeature2D> slots)
	{
		if (datum == null || rows == null || slots == null || slots.Count == 0)
		{
			return;
		}
		List<HoleFeature2D> holes = (from h in rows.SelectMany((IList<HoleFeature2D> r) => r)
			where h != null && !h.IsSlotPoint
			select h).ToList();
		IList<PinGroupPlan> list = BuildPinGroupPlan(holes, datum.DatumHole);
		if (list.Count == 0)
		{
			foreach (SlotFeature2D slot in slots)
			{
				Point2D target = PickSlotAnchorPoint(slot, datum);
				AddHorizontalOutlineReferenceDimension(plan, outline, datum.BaseX, target, DimensionKind.Normal, DimensionSide.Bottom, string.Empty, "SlotDatumH", slot.GroupId);
				AddVerticalOutlineReferenceDimension(plan, outline, datum.BaseY, target, DimensionKind.Normal, DimensionSide.Left, string.Empty, "SlotDatumV", slot.GroupId);
			}
			return;
		}
		var source = list.SelectMany((PinGroupPlan g) => g.Pins.Select((HoleFeature2D p) => new
		{
			Pin = p,
			IsGroupBase = IsSameHole(p, g.BasePin)
		})).ToList();
		foreach (SlotFeature2D slot2 in slots)
		{
			Point2D anchor = PickSlotAnchorPoint(slot2, datum);
			var anon = (from r in source
				orderby DistanceSquared(r.Pin.Center, anchor), r.IsGroupBase ? 1 : 0 descending
				select r).FirstOrDefault();
			if (anon != null)
			{
				AddHorizontalDim(plan, anon.Pin.Center, anchor, "SlotPinRef");
				AddVerticalDim(plan, anon.Pin.Center, anchor, "SlotPinRef");
			}
		}
	}

	private void SuppressDuplicateDimensions(DimensionPlan plan)
	{
		SuppressRightStructureHeightsDuplicatingOverallHeight(plan);
		SuppressLeftStructureHeightsCoveredByRight(plan);
		// BuildOverallPartitionChain for structure: multi-piece contiguous cover of overall
		// (structure roles + OutlineSegment partners). Suppress only structure members of the chain.
		// Run before OutlineSegment overall-partition removal so OS partners still exist.
		// Partners never include slot/hole Normals. Local structure steps that do not complete
		// overall are kept (e.g. TopStructWidth=50).
		SuppressStructureDimensionsThatPartitionOverall(plan, horizontal: true);
		SuppressStructureDimensionsThatPartitionOverall(plan, horizontal: false);
		// Drop raw OutlineSegment pairs that re-partition overall (before complementary remainder
		// removes only the larger partner and leaves the smaller fragment orphaned).
		// Scoped to OutlineSegment only — do not broaden complementary-remainder to Bottom/Left
		// or slot/hole Normal dims.
		SuppressOutlineSegmentsThatPartitionOverall(plan, DimensionSide.Top, horizontal: true);
		SuppressOutlineSegmentsThatPartitionOverall(plan, DimensionSide.Bottom, horizontal: true);
		SuppressOutlineSegmentsThatPartitionOverall(plan, DimensionSide.Right, horizontal: false);
		SuppressOutlineSegmentsThatPartitionOverall(plan, DimensionSide.Left, horizontal: false);
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
		SuppressOutlineSegmentsOnOverallEnvelope(plan);
		// Existing complementary remainder for structure/normal remainders (Top/Right only).
		SuppressComplementaryOutlineRemainders(plan, DimensionSide.Top, horizontal: true);
		SuppressComplementaryOutlineRemainders(plan, DimensionSide.Right, horizontal: false);
		SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Bottom, horizontal: true);
		SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Top, horizontal: true);
		SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Left, horizontal: false);
		SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Right, horizontal: false);
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
					plan.MarkSuppressed(candidate, "ComplementaryOutlineRemainder");
					plan.Dimensions.RemoveAt(num);
				}
			}
		}
	}

	/// <summary>
	/// Suppress OutlineSegment dims that re-partition the overall envelope on the same side.
	/// Covers both 2-piece pairs (e.g. [0,25]+[25,257]) and multi-piece chains
	/// (e.g. [0,9]+[9,20]+[20,91] = overall 91). Requires real contiguous cover of overall
	/// (abut, no gap/overlap) — not mere span sums. All pieces in the covering chain are dropped.
	/// </summary>
	private void SuppressOutlineSegmentsThatPartitionOverall(DimensionPlan plan, DimensionSide side, bool horizontal)
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
			plan.MarkSuppressed(candidate, "OutlineSegmentOverallPartition");
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
				&& IsDimensionOnOverallEnvelope(d, minX, maxX, minY, maxY))
			.ToList();
		if (envelopeOutlineSegments.Count == 0)
		{
			return;
		}
		HashSet<PlannedDimension> toSuppress = new HashSet<PlannedDimension>(envelopeOutlineSegments);
		// Short overall-edge tip co-suppress: same interval + same Side + collinear.
		// Do NOT co-suppress substantial step faces (span >= half overall) or cross-edge
		// same-interval structures (different Side / not collinear).
		// Left/RightStructHeight never co-suppressed here (short arm height on MaxX, etc.).
		double overallWidthSpan = maxX - minX;
		double tol = _config.GeometryTolerance;
		foreach (PlannedDimension structure in plan.Dimensions
			.Where((PlannedDimension d) => d.Kind == DimensionKind.Normal
				&& IsHorizontalStructureWidthRole(d.DebugRole)))
		{
			double structureSpan = GetDimensionSpan(structure, horizontal: true);
			// Short tip: less than half overall width. Step ledges are typically larger.
			if (structureSpan >= overallWidthSpan * 0.5 - tol)
			{
				continue;
			}
			if (envelopeOutlineSegments.Any((PlannedDimension os) =>
				os.Orientation == DimensionOrientation.Horizontal
				&& structure.Side == os.Side
				&& AreCollinearStructurePartners(structure, os, horizontal: true, tol)
				&& IsSameMeasurementInterval(structure, os, horizontal: true)))
			{
				toSuppress.Add(structure);
			}
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
			if (string.Equals(candidate.DebugRole, "OutlineSegment", StringComparison.Ordinal))
			{
				reason = envelopeOutlineSegments.Contains(candidate)
					? "OutlineSegmentOnOverallEnvelope"
					: "OutlineSegmentSameIntervalAsEnvelopeFragment";
			}
			else
			{
				reason = "StructureDuplicateOfEnvelopeOutlineSegment";
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
				? "RightStructureHeightDuplicatesOverallHeight"
				: "LeftStructureHeightDuplicatesOverallHeight";
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
				plan.MarkSuppressed(left, "LeftStructureHeightCoveredByRight");
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
				plan.MarkSuppressed(cand, "OrphanOuterVerticalStructureHeightTip");
				plan.Dimensions.RemoveAt(num);
			}
		}
	}

	private static List<List<PlannedDimension>> BuildAbuttingVerticalStructureChains(IList<PlannedDimension> heights, double tol)
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
				why = "SecondaryOutlineSegmentDuplicatesPrimaryStructureHeight";
			}
			else if (inResidual)
			{
				why = "SecondaryOutlineSegmentOverallResidual";
			}
			else
			{
				why = "SecondaryOutlineSegmentOverallPartitionWithPrimaryStructure";
			}
			plan.MarkSuppressed(cand, why);
			plan.Dimensions.RemoveAt(num);
		}
	}

	private static List<Tuple<double, double>> MergeVerticalIntervals(IList<Tuple<double, double>> intervals, double tol)
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

	private static List<Tuple<double, double>> ComputeVerticalResiduals(
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
				plan.MarkSuppressed(plannedDimension, "MirroredDuplicate");
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
					plan.MarkSuppressed(list[num4], "DuplicateMeasuredDimension");
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

	private void AddNonPinHolesFromOutlineEdge(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, IList<HoleFeature2D> holes)
	{
		List<HoleFeature2D> list = (holes ?? new List<HoleFeature2D>()).Where((HoleFeature2D h) => h != null && !h.IsPinHole && !h.IsSlotPoint).ToList();
		HoleFeature2D baseHole = ChooseNonPinBaseHole(list, datum);
		if (baseHole != null)
		{
			AddNonPinHoleDatumLocation(plan, outline, datum, baseHole);
			AddNonPinHoleChainsFromBase(plan, outline, baseHole, list.Where((HoleFeature2D h) => !h.IsThreadHole && !IsSameHole(h, baseHole)).ToList(), "HoleChain");
			AddNonPinHoleChainsFromBase(plan, outline, baseHole, list.Where((HoleFeature2D h) => h.IsThreadHole && !IsSameHole(h, baseHole)).ToList(), "ThreadHoleChain");
		}
	}

	private HoleFeature2D ChooseNonPinBaseHole(IList<HoleFeature2D> holes, Datum2D datum)
	{
		List<HoleFeature2D> list = holes.Where((HoleFeature2D h) => !h.IsThreadHole).ToList();
		IList<HoleFeature2D> list2;
		if (list.Count <= 0)
		{
			list2 = holes;
		}
		else
		{
			IList<HoleFeature2D> list3 = list;
			list2 = list3;
		}
		IList<HoleFeature2D> source = list2;
		return (from h in source
			orderby DistanceSquared(h.Center, new Point2D(datum.BaseX, datum.BaseY)), h.Center.X, h.Center.Y
			select h).FirstOrDefault();
	}

	private void AddNonPinHoleChainsFromBase(DimensionPlan plan, OutlineFeature2D outline, HoleFeature2D baseHole, IList<HoleFeature2D> targets, string debugRole)
	{
		if (targets != null && targets.Count != 0)
		{
			List<HoleFeature2D> list = new List<HoleFeature2D> { baseHole };
			list.AddRange(targets);
			AddNonPinHoleAxisChain(plan, outline, list, horizontalAxis: true, debugRole + "H");
			List<HoleFeature2D> list2 = new List<HoleFeature2D> { baseHole };
			list2.AddRange(targets);
			AddNonPinHoleAxisChain(plan, outline, list2, horizontalAxis: false, debugRole + "V");
		}
	}

	private void AddNonPinHoleAxisChain(DimensionPlan plan, OutlineFeature2D outline, IList<HoleFeature2D> holes, bool horizontalAxis, string debugRole)
	{
		List<HoleFeature2D> list = (from h in holes
			orderby horizontalAxis ? h.Center.X : h.Center.Y, horizontalAxis ? h.Center.Y : h.Center.X
			select h).ToList();
		DimensionSide side = ChooseLooseDimensionSide(outline, list, horizontalAxis);
		for (int num = 1; num < list.Count; num++)
		{
			AddDimension(plan, DimensionKind.HoleLocation, (!horizontalAxis) ? DimensionOrientation.Vertical : DimensionOrientation.Horizontal, side, list[num - 1].Center, list[num].Center, string.Empty, debugRole);
		}
	}

	private IEnumerable<List<HoleFeature2D>> GroupNonPinHolesByCoordinate(IList<HoleFeature2D> holes, bool horizontal)
	{
		double tolerance = GetFunctionalHoleAlignmentTolerance();
		List<List<HoleFeature2D>> list = new List<List<HoleFeature2D>>();
		foreach (HoleFeature2D item in holes.OrderBy((HoleFeature2D h) => horizontal ? h.Center.Y : h.Center.X))
		{
			double coordinate = (horizontal ? item.Center.Y : item.Center.X);
			List<HoleFeature2D> list2 = list.FirstOrDefault((List<HoleFeature2D> g) => Math.Abs(g.Average((HoleFeature2D h) => horizontal ? h.Center.Y : h.Center.X) - coordinate) <= tolerance);
			if (list2 == null)
			{
				list2 = new List<HoleFeature2D>();
				list.Add(list2);
			}
			list2.Add(item);
		}
		return from g in list
			where g.Count >= 2
			orderby g.Average((HoleFeature2D h) => horizontal ? h.Center.Y : h.Center.X)
			select (from h in g
				orderby horizontal ? h.Center.X : h.Center.Y, horizontal ? h.Center.Y : h.Center.X
				select h).ToList();
	}

	private void AddNonPinHoleHorizontalChain(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, IList<HoleFeature2D> row)
	{
		List<HoleFeature2D> list = (from h in row
			orderby h.Center.X, h.Center.Y
			select h).ToList();
		if (list.Count != 0)
		{
			DimensionSide side = ChooseLooseDimensionSide(outline, list, horizontal: true);
			AddHorizontalOutlineReferenceDimension(plan, outline, datum.BaseX, list[0].Center, DimensionKind.HoleLocation, side, string.Empty, "HoleChainH", null, preferFeatureLocalPlacement: true);
			for (int num = 1; num < list.Count; num++)
			{
				AddDimension(plan, DimensionKind.HoleLocation, DimensionOrientation.Horizontal, side, list[num - 1].Center, list[num].Center, string.Empty, "HoleChainH", null, preferFeatureLocalPlacement: true);
			}
		}
	}

	private void AddNonPinHoleVerticalChain(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, IList<HoleFeature2D> column)
	{
		List<HoleFeature2D> list = (from h in column
			orderby h.Center.Y, h.Center.X
			select h).ToList();
		if (list.Count != 0)
		{
			DimensionSide side = ChooseLooseDimensionSide(outline, list, horizontal: false);
			AddVerticalOutlineReferenceDimension(plan, outline, datum.BaseY, list[0].Center, DimensionKind.HoleLocation, side, string.Empty, "HoleChainV", null, preferFeatureLocalPlacement: true);
			for (int num = 1; num < list.Count; num++)
			{
				AddDimension(plan, DimensionKind.HoleLocation, DimensionOrientation.Vertical, side, list[num - 1].Center, list[num].Center, string.Empty, "HoleChainV", null, preferFeatureLocalPlacement: true);
			}
		}
	}

	private void AddNonPinHoleRowLocation(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, IList<HoleFeature2D> row)
	{
		HoleFeature2D holeFeature2D = (from h in row
			orderby Math.Abs(h.Center.X - datum.BaseX), h.Center.X
			select h).FirstOrDefault();
		if (holeFeature2D != null)
		{
			AddVerticalOutlineReferenceDimension(plan, outline, datum.BaseY, holeFeature2D.Center, DimensionKind.HoleLocation, ChooseVerticalHoleSide(outline, holeFeature2D.Center), string.Empty, "HoleRowY");
		}
	}

	private void AddNonPinHoleColumnLocation(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, IList<HoleFeature2D> column)
	{
		HoleFeature2D holeFeature2D = (from h in column
			orderby Math.Abs(h.Center.Y - datum.BaseY), h.Center.Y
			select h).FirstOrDefault();
		if (holeFeature2D != null)
		{
			AddHorizontalOutlineReferenceDimension(plan, outline, datum.BaseX, holeFeature2D.Center, DimensionKind.HoleLocation, ChooseHorizontalHoleSide(outline, holeFeature2D.Center), string.Empty, "HoleColumnX");
		}
	}

	private void AddNonPinHoleDatumLocation(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, HoleFeature2D hole)
	{
		AddHorizontalOutlineReferenceDimension(plan, outline, datum.BaseX, hole.Center, DimensionKind.HoleLocation, ChooseHorizontalHoleSide(outline, hole.Center), string.Empty, "HoleDatumX");
		AddVerticalOutlineReferenceDimension(plan, outline, datum.BaseY, hole.Center, DimensionKind.HoleLocation, ChooseVerticalHoleSide(outline, hole.Center), string.Empty, "HoleDatumY");
	}

	private PinGroupPlan CreatePinPairGroup(HoleFeature2D seed, IList<HoleFeature2D> candidates, HoleFeature2D forcedBasePin, HoleFeature2D referenceBasePin)
	{
		List<HoleFeature2D> list = new List<HoleFeature2D> { seed };
		HoleFeature2D holeFeature2D = (from h in candidates
			where !IsSameHole(h, seed) && IsSamePinDiameter(h, seed)
			orderby DistanceSquared(h.Center, seed.Center), h.Center.X, h.Center.Y
			select h).FirstOrDefault();
		if (holeFeature2D != null)
		{
			list.Add(holeFeature2D);
		}
		HoleFeature2D basePin = ChoosePinGroupBasePin(list, forcedBasePin, referenceBasePin, seed);
		PinGroupPlan pinGroupPlan = new PinGroupPlan
		{
			BasePin = basePin
		};
		foreach (HoleFeature2D item in list.OrderBy((HoleFeature2D h) => DistanceSquared(h.Center, basePin.Center)))
		{
			pinGroupPlan.Pins.Add(item);
			pinGroupPlan.MemberHoles.Add(item);
		}
		return pinGroupPlan;
	}

	private HoleFeature2D ChoosePinGroupBasePin(IList<HoleFeature2D> groupPins, HoleFeature2D forcedBasePin, HoleFeature2D referenceBasePin, HoleFeature2D fallbackPin)
	{
		if (forcedBasePin != null)
		{
			HoleFeature2D holeFeature2D = groupPins.FirstOrDefault((HoleFeature2D h) => IsSameHole(h, forcedBasePin));
			return holeFeature2D ?? forcedBasePin;
		}
		if (referenceBasePin != null && groupPins.Count > 0)
		{
			return (from h in groupPins
				orderby DistanceSquared(h.Center, referenceBasePin.Center), Math.Abs(h.Center.X - referenceBasePin.Center.X), Math.Abs(h.Center.Y - referenceBasePin.Center.Y), h.Center.X, h.Center.Y
				select h).First();
		}
		return (from h in groupPins
			orderby h.Center.X, h.Center.Y
			select h).FirstOrDefault() ?? fallbackPin;
	}

	private void AssignNonPinHolesToNearestPinPair(IList<HoleFeature2D> holes, IList<PinGroupPlan> groups)
	{
		if (groups.Count == 0)
		{
			return;
		}
		foreach (HoleFeature2D hole in holes.Where((HoleFeature2D h) => !h.IsPinHole && !h.IsSlotPoint))
		{
			(from g in groups
				orderby DistanceToPinPair(hole, g), DistanceSquared(hole.Center, g.BasePin.Center)
				select g).FirstOrDefault()?.MemberHoles.Add(hole);
		}
	}

	private void AddFirstPinGroupBaseLocation(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, PinGroupPlan group)
	{
		HoleFeature2D basePin = group.BasePin;
		double x = datum.DatumHoleLocationBaseX ?? datum.BaseX;
		double y = datum.DatumHoleLocationBaseY ?? datum.BaseY;
		string overrideText = (ShouldUseDatumHoleLocationTolerance(datum, isXDirection: true) ? (_config.DatumHoleLocationToleranceText ?? string.Empty) : string.Empty);
		string overrideText2 = (ShouldUseDatumHoleLocationTolerance(datum, isXDirection: false) ? (_config.DatumHoleLocationToleranceText ?? string.Empty) : string.Empty);
		AddHorizontalOutlineReferenceDimension(plan, outline, x, basePin.Center, DimensionKind.DatumHoleLocationX, group.HorizontalSide, overrideText, "DatumX", GetPinGroupDebugOwner(group), alignmentKey: GetPinDatumAlignmentKey(group, horizontal: true), alignmentPriority: 120, readingLevel: DimensionReadingLevel.DatumTransfer);
		AddVerticalOutlineReferenceDimension(plan, outline, y, basePin.Center, DimensionKind.DatumHoleLocationY, group.VerticalSide, overrideText2, "DatumY", GetPinGroupDebugOwner(group), alignmentKey: GetPinDatumAlignmentKey(group, horizontal: false), alignmentPriority: 120, readingLevel: DimensionReadingLevel.DatumTransfer);
	}

	private void AddPinGroupBaseTransfers(DimensionPlan plan, IList<PinGroupPlan> groups)
	{
		if (groups.Count < 2)
		{
			return;
		}
		HoleFeature2D basePin = groups[0].BasePin;
		for (int i = 1; i < groups.Count; i++)
		{
			HoleFeature2D basePin2 = groups[i].BasePin;
			string horizontalAlignmentKey = GetPinDatumAlignmentKey(groups[0], horizontal: true);
			string verticalAlignmentKey = GetPinDatumAlignmentKey(groups[0], horizontal: false);
			double num = Math.Abs(basePin2.Center.X - basePin.Center.X);
			double num2 = Math.Abs(basePin2.Center.Y - basePin.Center.Y);
			if (num > _config.GeometryTolerance)
			{
				AddDimension(plan, DimensionKind.PinGroupDistance, DimensionOrientation.Horizontal, groups[i].HorizontalSide, basePin.Center, basePin2.Center, _config.FormatPinGroupDistanceOverride(num), "PinGroupDistance", GetPinGroupDebugOwner(groups[i]), alignmentKey: horizontalAlignmentKey, alignmentPriority: 110, readingLevel: DimensionReadingLevel.DatumTransfer);
			}
			if (num2 > _config.GeometryTolerance)
			{
				AddDimension(plan, DimensionKind.PinGroupDistance, DimensionOrientation.Vertical, groups[i].VerticalSide, basePin.Center, basePin2.Center, _config.FormatPinGroupDistanceOverride(num2), "PinGroupDistance", GetPinGroupDebugOwner(groups[i]), alignmentKey: verticalAlignmentKey, alignmentPriority: 110, readingLevel: DimensionReadingLevel.DatumTransfer);
			}
		}
	}

	private void AddSameGroupPinDistances(DimensionPlan plan, OutlineFeature2D outline, IList<PinGroupPlan> groups)
	{
		for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
		{
			PinGroupPlan group = groups[groupIndex];
			string horizontalAlignmentKey = GetPinDatumAlignmentKey(groups[0], horizontal: true);
			string verticalAlignmentKey = GetPinDatumAlignmentKey(groups[0], horizontal: false);
			foreach (HoleFeature2D pin in group.Pins)
			{
				if (!IsSameHole(pin, group.BasePin))
				{
					double num = Math.Abs(pin.Center.X - group.BasePin.Center.X);
					double num2 = Math.Abs(pin.Center.Y - group.BasePin.Center.Y);
					if (num > _config.GeometryTolerance)
					{
						AddDimension(plan, DimensionKind.PinDistance, DimensionOrientation.Horizontal, group.HorizontalSide, group.BasePin.Center, pin.Center, _config.FormatPinCenterDistanceOverride(num), "PinDistance", GetPinGroupDebugOwner(group), alignmentKey: horizontalAlignmentKey, alignmentPriority: 100, readingLevel: DimensionReadingLevel.IntraGroup);
					}
					if (num2 > _config.GeometryTolerance)
					{
						AddDimension(plan, DimensionKind.PinDistance, DimensionOrientation.Vertical, group.VerticalSide, group.BasePin.Center, pin.Center, _config.FormatPinCenterDistanceOverride(num2), "PinDistance", GetPinGroupDebugOwner(group), alignmentKey: verticalAlignmentKey, alignmentPriority: 100, readingLevel: DimensionReadingLevel.IntraGroup);
					}
				}
			}
		}
	}

	private void AddNonPinHoleLocationsFromPinGroups(DimensionPlan plan, OutlineFeature2D outline, IList<HoleFeature2D> holes, IList<PinGroupPlan> pinGroups)
	{
		List<FunctionalHoleGroupPlan> list = BuildFunctionalHoleGroups(pinGroups);
		List<HoleFeature2D> groupedHoles = list.SelectMany((FunctionalHoleGroupPlan g) => g.Holes).ToList();
		AddFunctionalHoleGroupLocations(plan, list);
		AddLooseNonPinHoleLocations(plan, outline, holes, pinGroups, groupedHoles);
	}

	private List<FunctionalHoleGroupPlan> BuildFunctionalHoleGroups(IList<PinGroupPlan> pinGroups)
	{
		List<FunctionalHoleGroupPlan> list = new List<FunctionalHoleGroupPlan>();
		foreach (PinGroupPlan pinGroup in pinGroups.Where((PinGroupPlan g) => g.Pins.Count == 2 && g.BasePin != null))
		{
			List<HoleFeature2D> candidates = (from h in pinGroup.MemberHoles
				where h != null && !h.IsPinHole && !h.IsSlotPoint
				orderby DistanceToPinPair(h, pinGroup), h.Center.X, h.Center.Y
				select h).ToList();
			List<HoleFeature2D> used = new List<HoleFeature2D>();
			foreach (FunctionalHoleGroupPlan item in FindFunctionalHoleGroupsForPinPair(pinGroup, candidates, used))
			{
				list.Add(item);
			}
			// Holes that sit between the two pin centers belong to this pin group even when
			// they are not part of a 2/4-hole functional grid (common: one hole between a pin pair).
			List<HoleFeature2D> betweenPins = candidates
				.Where((HoleFeature2D h) => !ContainsHole(used, h) && IsHoleBetweenPinPair(h, pinGroup))
				.OrderBy((HoleFeature2D h) => DistanceSquared(h.Center, pinGroup.BasePin.Center))
				.ThenBy((HoleFeature2D h) => h.Center.X)
				.ThenBy((HoleFeature2D h) => h.Center.Y)
				.ToList();
			if (betweenPins.Count > 0)
			{
				FunctionalHoleGroupPlan betweenGroup = new FunctionalHoleGroupPlan
				{
					PinGroup = pinGroup
				};
				foreach (HoleFeature2D hole in betweenPins)
				{
					betweenGroup.Holes.Add(hole);
					used.Add(hole);
				}
				list.Add(betweenGroup);
			}
		}
		return list;
	}

	private IEnumerable<FunctionalHoleGroupPlan> FindFunctionalHoleGroupsForPinPair(PinGroupPlan pinGroup, IList<HoleFeature2D> candidates, IList<HoleFeature2D> used)
	{
		int[] array = new int[2] { 4, 2 };
		foreach (int attachedCount in array)
		{
			List<HoleFeature2D> available = candidates.Where((HoleFeature2D h) => !ContainsHole(used, h)).Take(10).ToList();
			if (available.Count < attachedCount)
			{
				continue;
			}
			FunctionalHoleGroupPlan bestGroup = null;
			double bestScore = double.MaxValue;
			foreach (List<HoleFeature2D> subset in EnumerateHoleCombinations(available, attachedCount))
			{
				if (!IsValidFunctionalHoleAttachmentSet(subset))
				{
					continue;
				}
				List<HoleFeature2D> allHoles = pinGroup.Pins.Concat(subset).ToList();
				if (!FitsFunctionalHoleGrid(allHoles))
				{
					continue;
				}
				double score = allHoles.Sum((HoleFeature2D h) => DistanceToPinPair(h, pinGroup));
				if (score >= bestScore)
				{
					continue;
				}
				bestScore = score;
				bestGroup = new FunctionalHoleGroupPlan
				{
					PinGroup = pinGroup
				};
				foreach (HoleFeature2D hole in subset)
				{
					bestGroup.Holes.Add(hole);
				}
			}
			if (bestGroup == null)
			{
				continue;
			}
			foreach (HoleFeature2D hole2 in bestGroup.Holes)
			{
				used.Add(hole2);
			}
			yield return bestGroup;
		}
	}

	private IEnumerable<List<HoleFeature2D>> EnumerateHoleCombinations(IList<HoleFeature2D> holes, int count)
	{
		List<HoleFeature2D> selected = new List<HoleFeature2D>();
		foreach (List<HoleFeature2D> item in EnumerateHoleCombinations(holes, count, 0, selected))
		{
			yield return item;
		}
	}

	private IEnumerable<List<HoleFeature2D>> EnumerateHoleCombinations(IList<HoleFeature2D> holes, int count, int start, List<HoleFeature2D> selected)
	{
		if (selected.Count == count)
		{
			yield return selected.ToList();
			yield break;
		}
		for (int i = start; i <= holes.Count - (count - selected.Count); i++)
		{
			selected.Add(holes[i]);
			foreach (List<HoleFeature2D> item in EnumerateHoleCombinations(holes, count, i + 1, selected))
			{
				yield return item;
			}
			selected.RemoveAt(selected.Count - 1);
		}
	}

	private bool IsValidFunctionalHoleAttachmentSet(IList<HoleFeature2D> holes)
	{
		if (holes.Count != 2 && holes.Count != 4)
		{
			return false;
		}
		return holes.GroupBy(GetFunctionalHoleSpecKey).All((IGrouping<string, HoleFeature2D> g) => g.Count() == 2 || g.Count() == 4);
	}

	private string GetFunctionalHoleSpecKey(HoleFeature2D hole)
	{
		if (hole == null)
		{
			return string.Empty;
		}
		if (hole.IsThreadHole)
		{
			return "Thread:" + (hole.ThreadCallout ?? string.Empty) + ":" + _config.FormatNumber(hole.Diameter);
		}
		return hole.Kind.ToString() + ":" + _config.FormatNumber(hole.Diameter);
	}

	private bool FitsFunctionalHoleGrid(IList<HoleFeature2D> holes)
	{
		return (holes.Count switch
		{
			6 => new Tuple<int, int>[4]
			{
				Tuple.Create(1, 6),
				Tuple.Create(6, 1),
				Tuple.Create(2, 3),
				Tuple.Create(3, 2)
			},
			4 => new Tuple<int, int>[3]
			{
				Tuple.Create(1, 4),
				Tuple.Create(4, 1),
				Tuple.Create(2, 2)
			},
			_ => new Tuple<int, int>[0],
		}).Any((Tuple<int, int> shape) => FitsGridShape(holes, shape.Item1, shape.Item2));
	}

	private bool FitsGridShape(IList<HoleFeature2D> holes, int rowCount, int columnCount)
	{
		List<double> list = ClusterCoordinates(holes.Select((HoleFeature2D h) => h.Center.Y));
		List<double> list2 = ClusterCoordinates(holes.Select((HoleFeature2D h) => h.Center.X));
		if (list.Count != rowCount || list2.Count != columnCount)
		{
			return false;
		}
		HashSet<string> hashSet = new HashSet<string>();
		foreach (HoleFeature2D hole in holes)
		{
			int num = FindClusterIndex(list, hole.Center.Y);
			int num2 = FindClusterIndex(list2, hole.Center.X);
			if (num < 0 || num2 < 0)
			{
				return false;
			}
			if (!hashSet.Add(num.ToString(CultureInfo.InvariantCulture) + ":" + num2.ToString(CultureInfo.InvariantCulture)))
			{
				return false;
			}
		}
		return hashSet.Count == rowCount * columnCount && HasContinuousGridSpacing(list) && HasContinuousGridSpacing(list2);
	}

	private List<double> ClusterCoordinates(IEnumerable<double> coordinates)
	{
		double tolerance = GetFunctionalHoleAlignmentTolerance();
		List<List<double>> list = new List<List<double>>();
		foreach (double coordinate in coordinates.OrderBy((double v) => v))
		{
			List<double> list2 = list.FirstOrDefault((List<double> c) => Math.Abs(c.Average() - coordinate) <= tolerance);
			if (list2 == null)
			{
				list2 = new List<double>();
				list.Add(list2);
			}
			list2.Add(coordinate);
		}
		return (from c in list
			select c.Average() into v
			orderby v
			select v).ToList();
	}

	private int FindClusterIndex(IList<double> clusters, double coordinate)
	{
		double functionalHoleAlignmentTolerance = GetFunctionalHoleAlignmentTolerance();
		for (int i = 0; i < clusters.Count; i++)
		{
			if (Math.Abs(clusters[i] - coordinate) <= functionalHoleAlignmentTolerance)
			{
				return i;
			}
		}
		return -1;
	}

	private double GetFunctionalHoleAlignmentTolerance()
	{
		return Math.Max(_config.GeometryTolerance * 10.0, 0.05);
	}

	private bool HasContinuousGridSpacing(IList<double> clusters)
	{
		if (clusters.Count <= 2)
		{
			return true;
		}
		List<double> list = new List<double>();
		for (int i = 1; i < clusters.Count; i++)
		{
			double num = clusters[i] - clusters[i - 1];
			if (num <= _config.GeometryTolerance)
			{
				return false;
			}
			list.Add(num);
		}
		return list.Max() <= list.Min() * 2.5;
	}

	private void AddFunctionalHoleGroupLocations(DimensionPlan plan, IList<FunctionalHoleGroupPlan> functionalGroups)
	{
		foreach (FunctionalHoleGroupPlan functionalGroup in functionalGroups)
		{
			PinGroupPlan pinGroup = functionalGroup.PinGroup;
			HoleFeature2D holeFeature2D = pinGroup?.BasePin;
			if (holeFeature2D == null)
			{
				continue;
			}
			foreach (HoleFeature2D hole in functionalGroup.Holes)
			{
				AddDimension(plan, DimensionKind.HoleLocation, DimensionOrientation.Horizontal, pinGroup.HorizontalSide, holeFeature2D.Center, hole.Center, string.Empty, "FunctionalHole", GetPinGroupDebugOwner(pinGroup), preservePreferredSide: true, preferLocalBoundary: true, alignmentKey: GetPinFunctionalHoleAlignmentKey(pinGroup, horizontal: true), alignmentPriority: 90, readingLevel: DimensionReadingLevel.LocalSpacing, preserveAlignmentLevel: true);
				AddDimension(plan, DimensionKind.HoleLocation, DimensionOrientation.Vertical, pinGroup.VerticalSide, holeFeature2D.Center, hole.Center, string.Empty, "FunctionalHole", GetPinGroupDebugOwner(pinGroup), preservePreferredSide: true, preferLocalBoundary: true, alignmentKey: GetPinFunctionalHoleAlignmentKey(pinGroup, horizontal: false), alignmentPriority: 90, readingLevel: DimensionReadingLevel.LocalSpacing, preserveAlignmentLevel: true);
			}
		}
	}

	private void AddLooseNonPinHoleLocations(DimensionPlan plan, OutlineFeature2D outline, IList<HoleFeature2D> holes, IList<PinGroupPlan> pinGroups, IList<HoleFeature2D> groupedHoles)
	{
		List<HoleFeature2D> list = (from h in holes
			where h != null && !h.IsPinHole && !h.IsSlotPoint
			where !ContainsHole(groupedHoles, h)
			select h).ToList();
		if (list.Count == 0 || pinGroups.Count == 0)
		{
			return;
		}
		List<LooseHoleLineGroup> lineGroups = BuildLooseHoleLineGroups(list);
		List<LooseHoleMacroGroup> source = BuildLooseHoleMacroGroups(list, lineGroups);
		List<LooseHoleLocationPlan> list2 = (from macro in source
			select BuildLooseHoleLocationPlan(macro, pinGroups) into p
			where p.ReferencePinGroup != null && p.ReferencePinGroup.BasePin != null && p.AnchorHole != null
			orderby DistanceSquared(p.ReferencePinGroup.BasePin.Center, p.AnchorHole.Center), p.AnchorHole.Center.X, p.AnchorHole.Center.Y
			select p).ToList();
		foreach (LooseHoleLocationPlan item in list2)
		{
			AddLooseHoleMacroGroup(plan, outline, item);
		}
	}

	private List<LooseHoleLineGroup> BuildLooseHoleLineGroups(IList<HoleFeature2D> holes)
	{
		List<LooseHoleLineGroup> list = new List<LooseHoleLineGroup>();
		foreach (List<HoleFeature2D> item in GroupLooseHolesBySpec(holes))
		{
			bool[] array = new bool[2] { true, false };
			foreach (bool horizontal in array)
			{
				foreach (List<HoleFeature2D> item2 in GroupLooseHolesByCoordinate(item, horizontal))
				{
					foreach (List<HoleFeature2D> item3 in SplitLooseCoordinateGroupIntoChains(item2, horizontal))
					{
						if (item3.Count < 2)
						{
							continue;
						}
						LooseHoleLineGroup looseHoleLineGroup = new LooseHoleLineGroup
						{
							Horizontal = horizontal,
							SpecKey = GetLooseHoleSpecKey(item3[0])
						};
						foreach (HoleFeature2D item4 in item3)
						{
							looseHoleLineGroup.Holes.Add(item4);
						}
						list.Add(looseHoleLineGroup);
					}
				}
			}
		}
		return list;
	}

	private IEnumerable<List<HoleFeature2D>> GroupLooseHolesBySpec(IList<HoleFeature2D> holes)
	{
		List<List<HoleFeature2D>> list = new List<List<HoleFeature2D>>();
		foreach (HoleFeature2D hole in holes.OrderBy(GetLooseHoleSpecKey).ThenBy((HoleFeature2D h) => h.Center.X).ThenBy((HoleFeature2D h) => h.Center.Y))
		{
			List<HoleFeature2D> list2 = list.FirstOrDefault((List<HoleFeature2D> g) => AreSameLooseHoleSpec(g[0], hole));
			if (list2 == null)
			{
				list2 = new List<HoleFeature2D>();
				list.Add(list2);
			}
			list2.Add(hole);
		}
		return list;
	}

	private string GetLooseHoleSpecKey(HoleFeature2D hole)
	{
		if (hole == null)
		{
			return string.Empty;
		}
		if (hole.IsThreadHole)
		{
			return "Thread:" + (hole.ThreadCallout ?? string.Empty) + ":" + _config.FormatNumber(hole.Diameter);
		}
		return hole.Kind.ToString() + ":" + _config.FormatNumber(hole.Diameter);
	}

	private bool AreSameLooseHoleSpec(HoleFeature2D a, HoleFeature2D b)
	{
		if (a == null || b == null || a.Kind != b.Kind)
		{
			return false;
		}
		if (a.IsThreadHole && !string.Equals(a.ThreadCallout ?? string.Empty, b.ThreadCallout ?? string.Empty, StringComparison.Ordinal))
		{
			return false;
		}
		return Math.Abs(a.Diameter - b.Diameter) <= _config.GeometryTolerance;
	}

	private IEnumerable<List<HoleFeature2D>> GroupLooseHolesByCoordinate(IList<HoleFeature2D> holes, bool horizontal)
	{
		double tolerance = GetFunctionalHoleAlignmentTolerance();
		List<List<HoleFeature2D>> list = new List<List<HoleFeature2D>>();
		foreach (HoleFeature2D item in holes.OrderBy((HoleFeature2D h) => horizontal ? h.Center.Y : h.Center.X))
		{
			double coordinate = (horizontal ? item.Center.Y : item.Center.X);
			List<HoleFeature2D> list2 = list.FirstOrDefault((List<HoleFeature2D> g) => Math.Abs(g.Average((HoleFeature2D h) => horizontal ? h.Center.Y : h.Center.X) - coordinate) <= tolerance);
			if (list2 == null)
			{
				list2 = new List<HoleFeature2D>();
				list.Add(list2);
			}
			list2.Add(item);
		}
		return from g in list
			where g.Count >= 2
			orderby g.Average((HoleFeature2D h) => horizontal ? h.Center.Y : h.Center.X)
			select (from h in g
				orderby horizontal ? h.Center.X : h.Center.Y, horizontal ? h.Center.Y : h.Center.X
				select h).ToList();
	}

	private IEnumerable<List<HoleFeature2D>> SplitLooseCoordinateGroupIntoChains(IList<HoleFeature2D> holes, bool horizontal)
	{
		List<HoleFeature2D> ordered = (from h in holes
			orderby horizontal ? h.Center.X : h.Center.Y, horizontal ? h.Center.Y : h.Center.X
			select h).ToList();
		if (ordered.Count <= 2 || HasContinuousHoleSpacing(ordered, horizontal))
		{
			yield return ordered;
			yield break;
		}
		List<double> gaps = GetLooseHoleAxisGaps(ordered, horizontal);
		if (gaps.Count == 0)
		{
			yield return ordered;
			yield break;
		}
		double breakGap = gaps.Min() * 2.5;
		List<HoleFeature2D> current = new List<HoleFeature2D> { ordered[0] };
		for (int i = 1; i < ordered.Count; i++)
		{
			double gap = GetAxisDistance(ordered[i - 1].Center, ordered[i].Center, horizontal);
			if (gap > breakGap)
			{
				yield return current;
				current = new List<HoleFeature2D>();
			}
			current.Add(ordered[i]);
		}
		yield return current;
	}

	private bool HasContinuousHoleSpacing(IList<HoleFeature2D> holes, bool horizontal)
	{
		if (holes.Count <= 2)
		{
			return true;
		}
		List<HoleFeature2D> list = holes.OrderBy((HoleFeature2D h) => horizontal ? h.Center.X : h.Center.Y).ToList();
		List<double> list2 = new List<double>();
		for (int num = 1; num < list.Count; num++)
		{
			double num2 = Math.Abs((horizontal ? list[num].Center.X : list[num].Center.Y) - (horizontal ? list[num - 1].Center.X : list[num - 1].Center.Y));
			if (num2 <= _config.GeometryTolerance)
			{
				return false;
			}
			list2.Add(num2);
		}
		return list2.Max() <= list2.Min() * 2.5;
	}

	private List<double> GetLooseHoleAxisGaps(IList<HoleFeature2D> ordered, bool horizontal)
	{
		List<double> list = new List<double>();
		for (int i = 1; i < ordered.Count; i++)
		{
			double axisDistance = GetAxisDistance(ordered[i - 1].Center, ordered[i].Center, horizontal);
			if (axisDistance > _config.GeometryTolerance)
			{
				list.Add(axisDistance);
			}
		}
		return list;
	}

	private double GetAxisDistance(Point2D a, Point2D b, bool horizontal)
	{
		return Math.Abs((horizontal ? b.X : b.Y) - (horizontal ? a.X : a.Y));
	}

	private List<LooseHoleMacroGroup> BuildLooseHoleMacroGroups(IList<HoleFeature2D> holes, IList<LooseHoleLineGroup> lineGroups)
	{
		List<LooseHoleMacroGroup> list = new List<LooseHoleMacroGroup>();
		foreach (LooseHoleLineGroup lineGroup in lineGroups)
		{
			LooseHoleMacroGroup looseHoleMacroGroup = new LooseHoleMacroGroup();
			looseHoleMacroGroup.LineGroups.Add(lineGroup);
			AddUniqueHoles(looseHoleMacroGroup.Holes, lineGroup.Holes);
			list.Add(looseHoleMacroGroup);
		}
		foreach (HoleFeature2D hole in holes)
		{
			if (!list.Any((LooseHoleMacroGroup existingSeed) => ContainsHole(existingSeed.Holes, hole)))
			{
				LooseHoleMacroGroup looseHoleMacroGroup2 = new LooseHoleMacroGroup();
				looseHoleMacroGroup2.Holes.Add(hole);
				list.Add(looseHoleMacroGroup2);
			}
		}
		double looseMacroGroupDistanceThreshold = GetLooseMacroGroupDistanceThreshold(holes);
		bool flag = true;
		while (flag)
		{
			flag = false;
			for (int num = 0; num < list.Count; num++)
			{
				if (flag)
				{
					break;
				}
				for (int num2 = num + 1; num2 < list.Count; num2++)
				{
					if (GetMacroGroupDistance(list[num], list[num2]) > looseMacroGroupDistanceThreshold)
					{
						continue;
					}
					AddUniqueHoles(list[num].Holes, list[num2].Holes);
					foreach (LooseHoleLineGroup lineGroup2 in list[num2].LineGroups)
					{
						if (!list[num].LineGroups.Contains(lineGroup2))
						{
							list[num].LineGroups.Add(lineGroup2);
						}
					}
					list.RemoveAt(num2);
					flag = true;
					break;
				}
			}
		}
		return (from seed in list
			where seed.Holes.Count > 0
			orderby seed.Holes.Average((HoleFeature2D h) => h.Center.X), seed.Holes.Average((HoleFeature2D h) => h.Center.Y)
			select seed).ToList();
	}

	private double GetLooseMacroGroupDistanceThreshold(IList<HoleFeature2D> holes)
	{
		List<double> list = new List<double>();
		bool[] array = new bool[2] { true, false };
		foreach (bool horizontal in array)
		{
			foreach (List<HoleFeature2D> item in GroupLooseHolesByCoordinate(holes, horizontal))
			{
				List<HoleFeature2D> ordered = item.OrderBy((HoleFeature2D h) => horizontal ? h.Center.X : h.Center.Y).ToList();
				list.AddRange(GetLooseHoleAxisGaps(ordered, horizontal));
			}
		}
		double num = ((list.Count == 0) ? 0.0 : list.OrderBy((double v) => v).ElementAt(list.Count / 2));
		return Math.Max(num * 2.5, _config.TextHeight * 6.0);
	}

	private double GetMacroGroupDistance(LooseHoleMacroGroup a, LooseHoleMacroGroup b)
	{
		if (a == null || b == null || a.Holes.Count == 0 || b.Holes.Count == 0)
		{
			return double.MaxValue;
		}
		double num = a.Holes.Min((HoleFeature2D h) => h.Center.X);
		double num2 = a.Holes.Max((HoleFeature2D h) => h.Center.X);
		double num3 = a.Holes.Min((HoleFeature2D h) => h.Center.Y);
		double num4 = a.Holes.Max((HoleFeature2D h) => h.Center.Y);
		double num5 = b.Holes.Min((HoleFeature2D h) => h.Center.X);
		double num6 = b.Holes.Max((HoleFeature2D h) => h.Center.X);
		double num7 = b.Holes.Min((HoleFeature2D h) => h.Center.Y);
		double num8 = b.Holes.Max((HoleFeature2D h) => h.Center.Y);
		double num9 = Math.Max(0.0, Math.Max(num5 - num2, num - num6));
		double num10 = Math.Max(0.0, Math.Max(num7 - num4, num3 - num8));
		return Math.Sqrt(num9 * num9 + num10 * num10);
	}

	private LooseHoleLocationPlan BuildLooseHoleLocationPlan(LooseHoleMacroGroup macroGroup, IList<PinGroupPlan> pinGroups)
	{
		LooseHoleLocationPlan looseHoleLocationPlan = new LooseHoleLocationPlan
		{
			MacroGroup = macroGroup
		};
		double num = double.MaxValue;
		foreach (PinGroupPlan pinGroup in pinGroups.Where((PinGroupPlan g) => g != null && g.BasePin != null))
		{
			HoleFeature2D nearestHole = (from h in macroGroup.Holes
				orderby DistanceSquared(h.Center, pinGroup.BasePin.Center), h.Center.X, h.Center.Y
				select h).FirstOrDefault();
			if (nearestHole == null)
			{
				continue;
			}
			PinGroupPlan pinGroupPlan = (from g in pinGroups
				where g != null && g.BasePin != null
				orderby DistanceSquared(nearestHole.Center, g.BasePin.Center), g.BasePin.Center.X, g.BasePin.Center.Y
				select g).FirstOrDefault();
			if (pinGroupPlan == pinGroup)
			{
				double num2 = DistanceSquared(nearestHole.Center, pinGroup.BasePin.Center);
				if (!(num2 >= num))
				{
					num = num2;
					looseHoleLocationPlan.ReferencePinGroup = pinGroup;
					looseHoleLocationPlan.AnchorHole = nearestHole;
				}
			}
		}
		if (looseHoleLocationPlan.ReferencePinGroup != null)
		{
			return looseHoleLocationPlan;
		}
		var anon = (from x in pinGroups.Where((PinGroupPlan g) => g != null && g.BasePin != null).SelectMany((PinGroupPlan g) => macroGroup.Holes.Select((HoleFeature2D h) => new
			{
				PinGroup = g,
				Hole = h,
				Distance = DistanceSquared(g.BasePin.Center, h.Center)
			}))
			orderby x.Distance, x.Hole.Center.X, x.Hole.Center.Y
			select x).FirstOrDefault();
		if (anon != null)
		{
			looseHoleLocationPlan.ReferencePinGroup = anon.PinGroup;
			looseHoleLocationPlan.AnchorHole = anon.Hole;
		}
		return looseHoleLocationPlan;
	}

	private void AddLooseHoleMacroGroup(DimensionPlan plan, OutlineFeature2D outline, LooseHoleLocationPlan loosePlan)
	{
		List<Tuple<HoleFeature2D, HoleFeature2D>> edges = AddLooseHoleCenterDistances(plan, outline, loosePlan.MacroGroup.LineGroups, loosePlan.ReferencePinGroup);
		List<HoleFeature2D> located = new List<HoleFeature2D> { loosePlan.AnchorHole };
		ExpandLocatedLooseHolesByCenterEdges(located, edges);
		AddLooseHoleLocationPair(plan, outline, loosePlan.ReferencePinGroup, loosePlan.ReferencePinGroup.BasePin.Center, loosePlan.AnchorHole, located, forcePinReference: true);
		while (located.Count < loosePlan.MacroGroup.Holes.Count)
		{
			HoleFeature2D holeFeature2D = (from h in loosePlan.MacroGroup.Holes
				where !ContainsHole(located, h)
				orderby GetNearestLooseLocationDistance(h, located, loosePlan.ReferencePinGroup.BasePin.Center), h.Center.X, h.Center.Y
				select h).FirstOrDefault();
			if (holeFeature2D == null)
			{
				break;
			}
			AddLooseHoleLocationPair(plan, outline, loosePlan.ReferencePinGroup, loosePlan.ReferencePinGroup.BasePin.Center, holeFeature2D, located, forcePinReference: false);
			located.Add(holeFeature2D);
			ExpandLocatedLooseHolesByCenterEdges(located, edges);
		}
	}

	private List<Tuple<HoleFeature2D, HoleFeature2D>> AddLooseHoleCenterDistances(DimensionPlan plan, OutlineFeature2D outline, IList<LooseHoleLineGroup> lineGroups, PinGroupPlan referencePinGroup)
	{
		List<Tuple<HoleFeature2D, HoleFeature2D>> list = new List<Tuple<HoleFeature2D, HoleFeature2D>>();
		HashSet<string> hashSet = new HashSet<string>();
		foreach (LooseHoleLineGroup lineGroup in lineGroups)
		{
			List<HoleFeature2D> list2 = (from h in lineGroup.Holes
				orderby lineGroup.Horizontal ? h.Center.X : h.Center.Y, lineGroup.Horizontal ? h.Center.Y : h.Center.X
				select h).ToList();
			int chainId = _nextLooseChainId++;
			DimensionSide side = ChoosePinGroupAnchoredDimensionSide(referencePinGroup, lineGroup.Horizontal) ?? ChooseLooseDimensionSide(outline, list2, lineGroup.Horizontal);
			for (int num = 1; num < list2.Count; num++)
			{
				string looseDimKey = GetLooseDimKey(list2[num - 1], list2[num], lineGroup.Horizontal);
				if (hashSet.Add(looseDimKey))
				{
					AddLooseHoleLocationDimension(plan, list2[num - 1].Center, list2[num].Center, lineGroup.Horizontal, side, chainId);
					list.Add(Tuple.Create(list2[num - 1], list2[num]));
				}
			}
		}
		return list;
	}

	private void AddLooseHoleLocationPair(DimensionPlan plan, OutlineFeature2D outline, PinGroupPlan referencePinGroup, Point2D pinReference, HoleFeature2D target, IList<HoleFeature2D> located, bool forcePinReference)
	{
		Point2D point2D = (forcePinReference ? pinReference : ChooseLooseLocationReference(pinReference, target, located, horizontal: true));
		if (Math.Abs(point2D.X - target.Center.X) > _config.GeometryTolerance)
		{
			int chainId = _nextLooseChainId++;
			AddLooseHoleLocationDimension(plan, point2D, target.Center, horizontal: true, ChoosePinGroupAnchoredDimensionSide(referencePinGroup, horizontal: true) ?? ChooseLooseDimensionSide(outline, new HoleFeature2D[1] { target }, horizontal: true), chainId);
		}
		Point2D point2D2 = (forcePinReference ? pinReference : ChooseLooseLocationReference(pinReference, target, located, horizontal: false));
		if (Math.Abs(point2D2.Y - target.Center.Y) > _config.GeometryTolerance)
		{
			int chainId2 = _nextLooseChainId++;
			AddLooseHoleLocationDimension(plan, point2D2, target.Center, horizontal: false, ChoosePinGroupAnchoredDimensionSide(referencePinGroup, horizontal: false) ?? ChooseLooseDimensionSide(outline, new HoleFeature2D[1] { target }, horizontal: false), chainId2);
		}
	}

	private void AddLooseHoleLocationDimension(DimensionPlan plan, Point2D from, Point2D to, bool horizontal, DimensionSide side, int chainId)
	{
		AddDimension(plan, DimensionKind.HoleLocation, (!horizontal) ? DimensionOrientation.Vertical : DimensionOrientation.Horizontal, side, from, to, string.Empty, "LooseHole", (chainId == 0) ? string.Empty : ("L" + chainId.ToString(CultureInfo.InvariantCulture)), preferLocalBoundary: true, chainId: chainId);
	}

	private Point2D ChooseLooseLocationReference(Point2D pinReference, HoleFeature2D target, IList<HoleFeature2D> located, bool horizontal)
	{
		List<Point2D> list = new List<Point2D> { pinReference };
		list.AddRange(from h in located
			where !IsSameHole(h, target)
			select h.Center);
		List<Point2D> list2 = list.Where((Point2D p) => horizontal ? (Math.Abs(p.Y - target.Center.Y) <= GetFunctionalHoleAlignmentTolerance()) : (Math.Abs(p.X - target.Center.X) <= GetFunctionalHoleAlignmentTolerance())).ToList();
		List<Point2D> source = ((list2.Count > 0) ? list2 : list);
		List<Point2D> list3 = source.Where((Point2D p) => LooseLocationTextFits(p, target.Center, horizontal)).ToList();
		if (list3.Count > 0)
		{
			source = list3;
		}
		return (from p in source
			orderby Math.Abs((horizontal ? p.X : p.Y) - (horizontal ? target.Center.X : target.Center.Y)), DistanceSquared(p, target.Center)
			select p).First();
	}

	private bool LooseLocationTextFits(Point2D from, Point2D to, bool horizontal)
	{
		double num = (horizontal ? Math.Abs(to.X - from.X) : Math.Abs(to.Y - from.Y));
		if (num <= _config.GeometryTolerance)
		{
			return false;
		}
		string text = _config.FormatNumber(num);
		double num2 = (double)Math.Max(text.Length, 2) * _config.TextHeight * 0.7;
		return num2 <= num - Math.Max(_config.GeometryTolerance, _config.TextHeight * 0.5);
	}

	private double GetNearestLooseLocationDistance(HoleFeature2D target, IList<HoleFeature2D> located, Point2D pinReference)
	{
		double num = DistanceSquared(target.Center, pinReference);
		foreach (HoleFeature2D item in located)
		{
			num = Math.Min(num, DistanceSquared(target.Center, item.Center));
		}
		return num;
	}

	private void ExpandLocatedLooseHolesByCenterEdges(IList<HoleFeature2D> located, IList<Tuple<HoleFeature2D, HoleFeature2D>> edges)
	{
		bool flag = true;
		while (flag)
		{
			flag = false;
			foreach (Tuple<HoleFeature2D, HoleFeature2D> edge in edges)
			{
				bool flag2 = ContainsHole(located, edge.Item1);
				bool flag3 = ContainsHole(located, edge.Item2);
				if (flag2 && !flag3)
				{
					located.Add(edge.Item2);
					flag = true;
				}
				else if (flag3 && !flag2)
				{
					located.Add(edge.Item1);
					flag = true;
				}
			}
		}
	}

	private DimensionSide ChooseLooseDimensionSide(OutlineFeature2D outline, IEnumerable<HoleFeature2D> holes, bool horizontal)
	{
		List<Point2D> list = holes.Select((HoleFeature2D h) => h.Center).ToList();
		if (list.Count == 0)
		{
			return (!horizontal) ? DimensionSide.Left : DimensionSide.Bottom;
		}
		if (horizontal)
		{
			double num = list.Average((Point2D p) => p.Y);
			return (!(Math.Abs(num - outline.MinY) <= Math.Abs(outline.MaxY - num))) ? DimensionSide.Top : DimensionSide.Bottom;
		}
		double num2 = list.Average((Point2D p) => p.X);
		return (Math.Abs(num2 - outline.MinX) <= Math.Abs(outline.MaxX - num2)) ? DimensionSide.Left : DimensionSide.Right;
	}

	private DimensionSide? ChoosePinGroupAnchoredDimensionSide(PinGroupPlan referencePinGroup, bool horizontal)
	{
		if (referencePinGroup == null)
		{
			return null;
		}
		return horizontal ? referencePinGroup.HorizontalSide : referencePinGroup.VerticalSide;
	}

	private string GetLooseDimKey(HoleFeature2D a, HoleFeature2D b, bool horizontal)
	{
		string text = GetHolePointKey(a);
		string text2 = GetHolePointKey(b);
		if (string.CompareOrdinal(text, text2) > 0)
		{
			string text3 = text;
			text = text2;
			text2 = text3;
		}
		return (horizontal ? "H:" : "V:") + text + ":" + text2;
	}

	private string GetHolePointKey(HoleFeature2D hole)
	{
		return _config.FormatNumber(hole.Center.X) + "," + _config.FormatNumber(hole.Center.Y);
	}

	private void AddUniqueHoles(IList<HoleFeature2D> target, IEnumerable<HoleFeature2D> source)
	{
		foreach (HoleFeature2D item in source)
		{
			if (!ContainsHole(target, item))
			{
				target.Add(item);
			}
		}
	}

	private bool ContainsHole(IEnumerable<HoleFeature2D> holes, HoleFeature2D target)
	{
		return holes?.Any((HoleFeature2D h) => IsSameHole(h, target)) ?? false;
	}

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
					Side = ((!(segment.MinY <= outline.MinY + _config.GeometryTolerance)) ? DimensionSide.Top : DimensionSide.Bottom),
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
					Side = ((segment.MinX <= outline.MinX + _config.GeometryTolerance) ? DimensionSide.Left : DimensionSide.Right),
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

	private void AddStepOutlineDimensions(DimensionPlan plan, OutlineFeature2D outline)
	{
		AddHorizontalStructureDimensions(plan, outline, BuildTopStructureWidthDimensions(outline));
		AddHorizontalStructureDimensions(plan, outline, BuildBottomStructureWidthDimensions(outline));
		AddVerticalStructureDimensions(plan, outline, BuildLeftStructureHeightDimensions(outline));
		AddVerticalStructureDimensions(plan, outline, BuildRightStructureHeightDimensions(outline));
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

	private IList<PlannedDimension> BuildTopStructureWidthDimensions(OutlineFeature2D outline)
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
		RemoveLongestTopExtensionCandidate(list2, outline);
		SnapComplementaryHorizontalRemainderEndpoints(list2, outline);
		RemoveComplementaryOverallRemainderCandidates(list2, outline.MinX, outline.MaxX, horizontal: true);
		return list2.Where((PlannedDimension dim) => IsTopSideHorizontalStructureCandidate(dim, outline, ignoredPoints)).ToList();
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

	private IList<PlannedDimension> BuildLeftStructureHeightDimensions(OutlineFeature2D outline)
	{
		List<Point2D> ignoredPoints = new List<Point2D>();
		List<PlannedDimension> list2;
		while (true)
		{
			List<Point2D> list = BuildLeftSideVerticalStructurePoints(outline, ignoredPoints);
			if (list.Count < 2)
			{
				return new List<PlannedDimension>();
			}
			list2 = BuildVerticalHeightCandidates(list, DimensionSide.Left, "LeftStructHeight");
			List<Point2D> leftExtensionCrossingPoints = GetLeftExtensionCrossingPoints(list2, outline);
			if (leftExtensionCrossingPoints.Count == 0)
			{
				break;
			}
			if (!AddIgnoredPoints(ignoredPoints, leftExtensionCrossingPoints))
			{
				return new List<PlannedDimension>();
			}
		}
		RemoveLongestLeftExtensionCandidate(list2, outline);
		// Phase 2: same complementary remainder path as right structure heights.
		SnapComplementaryVerticalRemainderEndpoints(list2, outline);
		RemoveComplementaryOverallRemainderCandidates(list2, outline.MinY, outline.MaxY, horizontal: false);
		return list2.Where((PlannedDimension dim) => IsLeftSideVerticalStructureCandidate(dim, outline, ignoredPoints)).ToList();
	}

	private IList<PlannedDimension> BuildRightStructureHeightDimensions(OutlineFeature2D outline)
	{
		List<Point2D> ignoredPoints = new List<Point2D>();
		List<PlannedDimension> list2;
		while (true)
		{
			List<Point2D> list = BuildRightSideVerticalStructurePoints(outline, ignoredPoints);
			if (list.Count < 2)
			{
				return new List<PlannedDimension>();
			}
			list2 = BuildVerticalHeightCandidates(list, DimensionSide.Right, "RightStructHeight");
			List<Point2D> rightExtensionCrossingPoints = GetRightExtensionCrossingPoints(list2, outline);
			if (rightExtensionCrossingPoints.Count == 0)
			{
				break;
			}
			if (!AddIgnoredPoints(ignoredPoints, rightExtensionCrossingPoints))
			{
				return new List<PlannedDimension>();
			}
		}
		RemoveLongestRightExtensionCandidate(list2, outline);
		SnapComplementaryVerticalRemainderEndpoints(list2, outline);
		RemoveComplementaryOverallRemainderCandidates(list2, outline.MinY, outline.MaxY, horizontal: false);
		return list2.Where((PlannedDimension dim) => IsRightSideVerticalStructureCandidate(dim, outline, ignoredPoints)).ToList();
	}

	private void RemoveComplementaryOverallRemainderCandidates(IList<PlannedDimension> candidates, double overallMin, double overallMax, bool horizontal)
	{
		if (candidates == null || candidates.Count < 2)
		{
			return;
		}
		for (int i = candidates.Count - 1; i >= 0; i--)
		{
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
			dimension.FirstPoint = SnapHorizontalPointToLeftConnectedVertical(dimension.FirstPoint, outline);
			dimension.SecondPoint = SnapHorizontalPointToLeftConnectedVertical(dimension.SecondPoint, outline);
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

	private IList<PlannedDimension> BuildBottomStructureWidthDimensions(OutlineFeature2D outline)
	{
		List<Point2D> ignoredPoints = new List<Point2D>();
		List<PlannedDimension> list2;
		while (true)
		{
			List<Point2D> list = BuildBottomSideHorizontalStructurePoints(outline, ignoredPoints);
			if (list.Count < 2)
			{
				return new List<PlannedDimension>();
			}
			list2 = BuildHorizontalWidthCandidates(list, DimensionSide.Bottom, "BottomStructWidth");
			List<Point2D> bottomExtensionCrossingPoints = GetBottomExtensionCrossingPoints(list2, outline);
			if (bottomExtensionCrossingPoints.Count == 0)
			{
				break;
			}
			if (!AddIgnoredPoints(ignoredPoints, bottomExtensionCrossingPoints))
			{
				return new List<PlannedDimension>();
			}
		}
		RemoveLongestBottomExtensionCandidate(list2, outline);
		return list2.Where((PlannedDimension dim) => IsBottomSideHorizontalStructureCandidate(dim, outline, ignoredPoints)).ToList();
	}

	private List<Point2D> BuildBottomSideHorizontalStructurePoints(OutlineFeature2D outline, IList<Point2D> ignoredPoints)
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

	private List<Point2D> BuildLeftSideVerticalStructurePoints(OutlineFeature2D outline, IList<Point2D> ignoredPoints)
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

	private List<Point2D> BuildRightSideVerticalStructurePoints(OutlineFeature2D outline, IList<Point2D> ignoredPoints)
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

	private Point2D? GetTopMostPoint(IEnumerable<Segment2D> segments)
	{
		List<Point2D> list = segments.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End }).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		return (from p in list
			orderby p.Y descending, p.X
			select p).First();
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

	private Point2D? GetBottomMostPoint(IEnumerable<Segment2D> segments, IList<Point2D> ignoredPoints)
	{
		List<Segment2D> source = segments.ToList();
		return ((IEnumerable<Point2D>)(from p in source.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			where !ContainsPoint(ignoredPoints, p)
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

	private Point2D? GetLeftMostPoint(IEnumerable<Segment2D> segments, IList<Point2D> ignoredPoints)
	{
		return ((IEnumerable<Point2D>)(from p in segments.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			where !ContainsPoint(ignoredPoints, p)
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

	private Point2D? GetRightMostPoint(IEnumerable<Segment2D> segments, IList<Point2D> ignoredPoints)
	{
		return ((IEnumerable<Point2D>)(from p in segments.SelectMany((Segment2D s) => new Point2D[2] { s.Start, s.End })
			where !ContainsPoint(ignoredPoints, p)
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

	private List<Point2D> GetBottomExtensionCrossingPoints(IList<PlannedDimension> candidates, OutlineFeature2D outline)
	{
		List<Point2D> list = new List<Point2D>();
		foreach (PlannedDimension candidate in candidates)
		{
			AddBottomCrossingPoint(list, candidate.FirstPoint, outline);
			AddBottomCrossingPoint(list, candidate.SecondPoint, outline);
			AddDirectionalInclinedEndpoint(list, candidate.FirstPoint, outline, invertDirection: false);
			AddDirectionalInclinedEndpoint(list, candidate.SecondPoint, outline, invertDirection: false);
		}
		return list;
	}

	private List<Point2D> GetLeftExtensionCrossingPoints(IList<PlannedDimension> candidates, OutlineFeature2D outline)
	{
		List<Point2D> list = new List<Point2D>();
		foreach (PlannedDimension candidate in candidates)
		{
			AddLeftCrossingPoint(list, candidate.FirstPoint, outline);
			AddLeftCrossingPoint(list, candidate.SecondPoint, outline);
			AddSideDirectionalInclinedEndpoint(list, candidate.FirstPoint, outline, DimensionSide.Left);
			AddSideDirectionalInclinedEndpoint(list, candidate.SecondPoint, outline, DimensionSide.Left);
		}
		return list;
	}

	private List<Point2D> GetRightExtensionCrossingPoints(IList<PlannedDimension> candidates, OutlineFeature2D outline)
	{
		List<Point2D> list = new List<Point2D>();
		foreach (PlannedDimension candidate in candidates)
		{
			AddRightCrossingPoint(list, candidate.FirstPoint, outline);
			AddRightCrossingPoint(list, candidate.SecondPoint, outline);
			AddSideDirectionalInclinedEndpoint(list, candidate.FirstPoint, outline, DimensionSide.Right);
			AddSideDirectionalInclinedEndpoint(list, candidate.SecondPoint, outline, DimensionSide.Right);
		}
		return list;
	}

	private void AddLeftCrossingPoint(IList<Point2D> points, Point2D featurePoint, OutlineFeature2D outline)
	{
		if (LeftExtensionCrossesOutline(featurePoint, outline) && !ContainsPoint(points, featurePoint))
		{
			points.Add(featurePoint);
		}
	}

	private void AddRightCrossingPoint(IList<Point2D> points, Point2D featurePoint, OutlineFeature2D outline)
	{
		if (RightExtensionCrossesOutline(featurePoint, outline) && !ContainsPoint(points, featurePoint))
		{
			points.Add(featurePoint);
		}
	}

	private void AddBottomCrossingPoint(IList<Point2D> points, Point2D featurePoint, OutlineFeature2D outline)
	{
		if (BottomExtensionCrossesOutline(featurePoint, outline) && !ContainsPoint(points, featurePoint))
		{
			points.Add(featurePoint);
		}
	}

	private void AddDirectionalInclinedEndpoint(IList<Point2D> points, Point2D point, OutlineFeature2D outline, bool invertDirection)
	{
		if (!ContainsPoint(points, point) && !IsEnvelopeSidePoint(point, outline) && !string.IsNullOrEmpty(GetDirectionalInclinedIgnoreReason(point, outline, invertDirection)))
		{
			points.Add(point);
		}
	}

	private void AddSideDirectionalInclinedEndpoint(IList<Point2D> points, Point2D point, OutlineFeature2D outline, DimensionSide side)
	{
		if (!ContainsPoint(points, point) && !IsEnvelopeHorizontalSidePoint(point, outline) && ShouldIgnoreSideDirectionalInclinedEndpoint(point, outline, side))
		{
			points.Add(point);
		}
	}

	private bool AddIgnoredPoints(IList<Point2D> ignoredPoints, IEnumerable<Point2D> crossingPoints)
	{
		bool result = false;
		foreach (Point2D crossingPoint in crossingPoints)
		{
			if (!ContainsPoint(ignoredPoints, crossingPoint))
			{
				ignoredPoints.Add(crossingPoint);
				result = true;
			}
		}
		return result;
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

	private bool IsBottomSideHorizontalStructureCandidate(PlannedDimension dim, OutlineFeature2D outline, IList<Point2D> ignoredPoints)
	{
		return _structureEndpointRules.IsBottomSideHorizontalStructureCandidate(dim, outline, ignoredPoints);
	}

	private bool IsCurrentBottomSideStructurePoint(Point2D point, OutlineFeature2D outline, IList<Point2D> ignoredPoints)
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

	private bool IsRightSideVerticalStructureCandidate(PlannedDimension dim, OutlineFeature2D outline, IList<Point2D> ignoredPoints)
	{
		return _structureEndpointRules.IsRightSideVerticalStructureCandidate(dim, outline, ignoredPoints);
	}

	private bool IsLeftSideVerticalStructureCandidate(PlannedDimension dim, OutlineFeature2D outline, IList<Point2D> ignoredPoints)
	{
		return _structureEndpointRules.IsLeftSideVerticalStructureCandidate(dim, outline, ignoredPoints);
	}

	private bool IsCurrentRightSideStructurePoint(Point2D point, OutlineFeature2D outline, IList<Point2D> ignoredPoints)
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

	private void AddSideInclinedEndpointStructurePoints(IList<Point2D> points, OutlineFeature2D outline, IList<Point2D> ignoredPoints, DimensionSide side)
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

	private void AddSideInclinedEndpointStructurePoint(IList<Point2D> points, Point2D point, OutlineFeature2D outline, IList<Point2D> ignoredPoints)
	{
		if (!ContainsPoint(points, point) && !ContainsPoint(ignoredPoints, point) && !IsEnvelopeHorizontalSidePoint(point, outline))
		{
			points.Add(point);
		}
	}

	private bool IsSideInclinedEndpointStructurePoint(Point2D point, OutlineFeature2D outline, IList<Point2D> ignoredPoints, DimensionSide side)
	{
		if (ContainsPoint(ignoredPoints, point) || IsEnvelopeHorizontalSidePoint(point, outline))
		{
			return false;
		}
		return (from s in (from s in outline.Segments
				where !s.IsHorizontal(_config.GeometryTolerance)
				where !s.IsVertical(_config.GeometryTolerance)
				select s).Where(IsFortyFiveDegreeSegment)
			where IsSideInnerGrooveChamferSegment(s, outline, side)
			select s).Any((Segment2D s) => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
	}

	private void AddBottomInclinedEndpointStructurePoints(IList<Point2D> points, OutlineFeature2D outline, IList<Point2D> ignoredPoints)
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

	private void AddBottomInclinedEndpointStructurePoint(IList<Point2D> points, Point2D point, OutlineFeature2D outline, IList<Point2D> ignoredPoints)
	{
		if (!ContainsPoint(points, point) && !ContainsPoint(ignoredPoints, point) && !IsEnvelopeSidePoint(point, outline))
		{
			points.Add(point);
		}
	}

	private bool IsBottomInclinedEndpointStructurePoint(Point2D point, OutlineFeature2D outline, IList<Point2D> ignoredPoints)
	{
		if (ContainsPoint(ignoredPoints, point) || IsEnvelopeSidePoint(point, outline))
		{
			return false;
		}
		return (from s in outline.Segments
			where !s.IsHorizontal(_config.GeometryTolerance)
			where !s.IsVertical(_config.GeometryTolerance)
			where IsInnerGrooveChamferSegment(s, outline, isTopSide: false)
			select s).Any((Segment2D s) => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
	}

	private bool BottomExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline)
	{
		double geometryTolerance = _config.GeometryTolerance;
		double num = outline.MinY - _config.FirstDimOffset;
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

	private bool LeftExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline)
	{
		double geometryTolerance = _config.GeometryTolerance;
		double num = outline.MinX - _config.FirstDimOffset;
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

	private bool RightExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline)
	{
		double geometryTolerance = _config.GeometryTolerance;
		double num = featurePoint.X + geometryTolerance;
		double num2 = outline.MaxX + _config.FirstDimOffset;
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

	private bool IsCurrentTopSideStructurePoint(Point2D point, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		if (IsTopInclinedEndpointStructurePoint(point, outline, ignoredPoints))
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
		StructurePoint topMostStructurePoint = GetTopMostStructurePoint(list, ignoredPoints);
		return topMostStructurePoint != null && PointsEqual(topMostStructurePoint.Point, point);
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

	private bool IsTopInclinedEndpointStructurePoint(Point2D point, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
	{
		if (ContainsIgnoredPoint(ignoredPoints, point) || IsEnvelopeSidePoint(point, outline))
		{
			return false;
		}
		return (from s in (from s in outline.Segments
				where !s.IsHorizontal(_config.GeometryTolerance)
				where !s.IsVertical(_config.GeometryTolerance)
				select s).Where(IsFortyFiveDegreeSegment)
			where IsInnerGrooveChamferSegment(s, outline, isTopSide: true)
			select s).Any((Segment2D s) => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
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
		double geometryTolerance = _config.GeometryTolerance;
		double num = featurePoint.Y + geometryTolerance;
		double num2 = outline.MaxY + _config.FirstDimOffset;
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

	private bool TryGetVerticalIntersectionY(Segment2D segment, double x, out double y)
	{
		y = 0.0;
		if (Math.Abs(segment.Start.X - segment.End.X) <= _config.GeometryTolerance)
		{
			return false;
		}
		if (x < segment.MinX - _config.GeometryTolerance || x > segment.MaxX + _config.GeometryTolerance)
		{
			return false;
		}
		y = segment.Start.Y + (x - segment.Start.X) * (segment.End.Y - segment.Start.Y) / (segment.End.X - segment.Start.X);
		return true;
	}

	private bool TryGetHorizontalIntersectionX(Segment2D segment, double y, out double x)
	{
		x = 0.0;
		if (Math.Abs(segment.Start.Y - segment.End.Y) <= _config.GeometryTolerance)
		{
			return false;
		}
		if (y < segment.MinY - _config.GeometryTolerance || y > segment.MaxY + _config.GeometryTolerance)
		{
			return false;
		}
		x = segment.Start.X + (y - segment.Start.Y) * (segment.End.X - segment.Start.X) / (segment.End.Y - segment.Start.Y);
		return true;
	}

	private bool VerticalExtensionOverlapsOutlineSegment(Segment2D segment, Point2D featurePoint, double minY, double maxY)
	{
		if (!segment.IsVertical(_config.GeometryTolerance))
		{
			return false;
		}
		if (Math.Abs(segment.MinX - featurePoint.X) > _config.GeometryTolerance)
		{
			return false;
		}
		double num = Math.Max(segment.MinY, minY);
		double num2 = Math.Min(segment.MaxY, maxY);
		if (num2 <= num + _config.GeometryTolerance)
		{
			return false;
		}
		return !PointLiesOnSegmentVerticalExtent(segment, featurePoint.Y);
	}

	private bool HorizontalExtensionOverlapsOutlineSegment(Segment2D segment, Point2D featurePoint, double minX, double maxX)
	{
		if (!segment.IsHorizontal(_config.GeometryTolerance))
		{
			return false;
		}
		if (Math.Abs(segment.MinY - featurePoint.Y) > _config.GeometryTolerance)
		{
			return false;
		}
		double num = Math.Max(segment.MinX, minX);
		double num2 = Math.Min(segment.MaxX, maxX);
		if (num2 <= num + _config.GeometryTolerance)
		{
			return false;
		}
		return !PointLiesOnSegmentHorizontalExtent(segment, featurePoint.X);
	}

	private bool PointLiesOnSegmentVerticalExtent(Segment2D segment, double y)
	{
		return y >= segment.MinY - _config.GeometryTolerance && y <= segment.MaxY + _config.GeometryTolerance;
	}

	private bool PointLiesOnSegmentHorizontalExtent(Segment2D segment, double x)
	{
		return x >= segment.MinX - _config.GeometryTolerance && x <= segment.MaxX + _config.GeometryTolerance;
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

	private bool IsDirectionalInclinedEndpoint(Point2D point, Segment2D segment, bool invertDirection)
	{
		return _structureSuppressionRules.IsDirectionalIgnoredEndpoint(point, segment, invertDirection);
	}

	private bool IsEnvelopeSidePoint(Point2D point, OutlineFeature2D outline)
	{
		return _structureSuppressionRules.IsEnvelopeSidePoint(point, outline);
	}

	private bool IsEnvelopeHorizontalSidePoint(Point2D point, OutlineFeature2D outline)
	{
		return _structureSuppressionRules.IsEnvelopeHorizontalSidePoint(point, outline);
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
	private void SuppressStructureDimensionsThatPartitionOverall(DimensionPlan plan, bool horizontal)
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
		List<PlannedDimension> structureDims = plan.Dimensions
			.Where((PlannedDimension d) => d.Kind == DimensionKind.Normal
				&& IsStructureWidthOrHeightRole(d.DebugRole)
				&& d.Orientation == expected)
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
		// Per-structure search: only suppress the *focus* structure when it participates in a
		// complete overall partition with other structures and/or collinear same-side OutlineSegments.
		// Same Side alone is not enough: L-shape TopStructWidth 20 (tower top) must not be erased
		// by Top OutlineSegment 71 on the arm (different Y, only shared placement side).
		foreach (PlannedDimension focus in structureDims)
		{
			List<PlannedDimension> partnerPool = new List<PlannedDimension> { focus };
			foreach (PlannedDimension other in structureDims)
			{
				if (other != focus)
				{
					partnerPool.Add(other);
				}
			}
			foreach (PlannedDimension os in outlineSegments)
			{
				if (os.Side == focus.Side && AreCollinearStructurePartners(focus, os, horizontal, tol))
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
			plan.MarkSuppressed(candidate, "StructureOverallPartition");
			plan.Dimensions.RemoveAt(num);
		}
	}

	private static bool IsStructureOverallPartitionPartnerRole(string debugRole)
	{
		return IsStructureWidthOrHeightRole(debugRole)
			|| string.Equals(debugRole, "OutlineSegment", StringComparison.Ordinal);
	}

	/// <summary>
	/// Structure + OutlineSegment overall-partition partners must be collinear on the
	/// measurement edge: same Y for horizontal dims, same X for vertical dims.
	/// Same placement side alone is insufficient (tower top vs arm top both Side=Top).
	/// </summary>
	private static bool AreCollinearStructurePartners(PlannedDimension a, PlannedDimension b, bool horizontal, double tol)
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

	private bool ContainsStructurePoint(IEnumerable<StructurePoint> points, Point2D point)
	{
		return points.Any((StructurePoint p) => PointsEqual(p.Point, point));
	}

	private bool ContainsIgnoredPoint(IEnumerable<IgnoredPoint> points, Point2D point)
	{
		return points.Any((IgnoredPoint p) => PointsEqual(p.Point, point));
	}

	private bool IsTooSmallStructureSpan(double span)
	{
		return _structureEndpointRules.IsTooSmallStructureSpan(span);
	}

	private bool IsCrossAxisStructureSpanTooLarge(Point2D first, Point2D second, bool horizontal)
	{
		return _structureEndpointRules.IsCrossAxisStructureSpanTooLarge(first, second, horizontal);
	}

	private Point2D GetLeftBoundaryPoint(OutlineFeature2D outline)
	{
		return _structureEndpointRules.GetBoundaryPoint(outline, DimensionSide.Left);
	}

	private Point2D GetRightBoundaryPoint(OutlineFeature2D outline)
	{
		return _structureEndpointRules.GetBoundaryPoint(outline, DimensionSide.Right);
	}

	private Point2D GetBottomBoundaryPoint(OutlineFeature2D outline)
	{
		return _structureEndpointRules.GetBoundaryPoint(outline, DimensionSide.Bottom);
	}

	private Point2D GetTopBoundaryPoint(OutlineFeature2D outline)
	{
		return _structureEndpointRules.GetBoundaryPoint(outline, DimensionSide.Top);
	}

	private bool AddHorizontalOutlineReferenceDimension(DimensionPlan plan, OutlineFeature2D outline, double preferredX, Point2D target, DimensionKind kind, DimensionSide side, string overrideText, string debugRole, string debugOwner = null, bool preferFeatureLocalPlacement = false, string alignmentKey = null, int alignmentPriority = 0, DimensionReadingLevel readingLevel = DimensionReadingLevel.LocalSpacing)
	{
		if (!OutlineGeometryQuery.TryFindVerticalBoundaryPoint(outline, preferredX, target.Y, _config.GeometryTolerance, out var point))
		{
			plan.AddSkippedDimension(kind, DimensionOrientation.Horizontal, side, new Point2D(preferredX, target.Y), target, "NoRealOutlineAttachment:XDatum=" + preferredX.ToString("0.########", CultureInfo.InvariantCulture), debugRole, debugOwner);
			return false;
		}
		AddDimension(plan, kind, DimensionOrientation.Horizontal, side, point, target, overrideText, debugRole, debugOwner, preferFeatureLocalPlacement, firstPointMustLieOnOutline: true, requiredOutlineReferenceCoordinate: preferredX, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority, readingLevel: readingLevel);
		return true;
	}

	private bool AddVerticalOutlineReferenceDimension(DimensionPlan plan, OutlineFeature2D outline, double preferredY, Point2D target, DimensionKind kind, DimensionSide side, string overrideText, string debugRole, string debugOwner = null, bool preferFeatureLocalPlacement = false, string alignmentKey = null, int alignmentPriority = 0, DimensionReadingLevel readingLevel = DimensionReadingLevel.LocalSpacing)
	{
		if (!OutlineGeometryQuery.TryFindHorizontalBoundaryPoint(outline, preferredY, target.X, _config.GeometryTolerance, out var point))
		{
			plan.AddSkippedDimension(kind, DimensionOrientation.Vertical, side, new Point2D(target.X, preferredY), target, "NoRealOutlineAttachment:YDatum=" + preferredY.ToString("0.########", CultureInfo.InvariantCulture), debugRole, debugOwner);
			return false;
		}
		AddDimension(plan, kind, DimensionOrientation.Vertical, side, point, target, overrideText, debugRole, debugOwner, preferFeatureLocalPlacement, firstPointMustLieOnOutline: true, requiredOutlineReferenceCoordinate: preferredY, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority, readingLevel: readingLevel);
		return true;
	}

	private void AddHorizontalChainFromOutlineDatum(DimensionPlan plan, OutlineFeature2D outline, double datumX, IList<Point2D> ordered, string debugRole)
	{
		if (ordered == null || ordered.Count == 0)
		{
			return;
		}
		// Edge→first hole + inter-hole spans form one continuous locating chain (e.g. U-slot
		// edge location + center distance) and must share a dim-line alignment lane.
		string alignmentKey = GetSlotChainAlignmentKey(DimensionSide.Bottom, horizontal: true, ordered[0].Y);
		const int alignmentPriority = 80;
		AddHorizontalOutlineReferenceDimension(plan, outline, datumX, ordered[0], DimensionKind.Normal, DimensionSide.Bottom, string.Empty, debugRole, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority);
		for (int i = 1; i < ordered.Count; i++)
		{
			AddHorizontalDim(plan, ordered[i - 1], ordered[i], debugRole, alignmentKey, alignmentPriority);
		}
	}

	private void AddVerticalChainFromOutlineDatum(DimensionPlan plan, OutlineFeature2D outline, double datumY, IList<Point2D> ordered, string debugRole)
	{
		if (ordered == null || ordered.Count == 0)
		{
			return;
		}
		// Edge→first U-slot location + slot-to-slot center distance stay on one left-side lane.
		string alignmentKey = GetSlotChainAlignmentKey(DimensionSide.Left, horizontal: false, ordered[0].X);
		const int alignmentPriority = 80;
		AddVerticalOutlineReferenceDimension(plan, outline, datumY, ordered[0], DimensionKind.Normal, DimensionSide.Left, string.Empty, debugRole, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority);
		for (int i = 1; i < ordered.Count; i++)
		{
			AddVerticalDim(plan, ordered[i - 1], ordered[i], debugRole, alignmentKey, alignmentPriority);
		}
	}

	private void AddDimension(DimensionPlan plan, DimensionKind kind, DimensionOrientation orientation, DimensionSide side, Point2D firstPoint, Point2D secondPoint, string overrideText, string debugRole, string debugOwner = null, bool preferFeatureLocalPlacement = false, bool firstPointMustLieOnOutline = false, double? requiredOutlineReferenceCoordinate = null, bool preservePreferredSide = false, string alignmentKey = null, int alignmentPriority = 0, bool preferLocalBoundary = false, int chainId = 0, DimensionReadingLevel readingLevel = DimensionReadingLevel.LocalSpacing, bool preserveAlignmentLevel = false)
	{
		if (!(GetSpan(firstPoint, secondPoint, orientation) <= _config.GeometryTolerance))
		{
			plan.Add(new PlannedDimension
			{
				Kind = kind,
				Orientation = orientation,
				Side = side,
				FirstPoint = firstPoint,
				SecondPoint = secondPoint,
				OverrideText = (overrideText ?? string.Empty),
				DebugRole = debugRole,
				DebugOwner = debugOwner,
				AlignmentKey = (alignmentKey ?? string.Empty),
				AlignmentPriority = alignmentPriority,
				PreserveAlignmentLevel = preserveAlignmentLevel,
				ReadingLevel = readingLevel,
				ChainId = chainId,
				PreferLocalBoundary = preferLocalBoundary,
				PreferFeatureLocalPlacement = preferFeatureLocalPlacement,
				PreservePreferredSide = preservePreferredSide,
				FirstPointMustLieOnOutline = firstPointMustLieOnOutline,
				RequiredOutlineReferenceCoordinate = requiredOutlineReferenceCoordinate,
				UseSegmentedExtensionLines = (kind != DimensionKind.OverallWidth && kind != DimensionKind.OverallHeight)
			});
		}
	}

	private static void AddSuppressedDimension(DimensionPlan plan, PlannedDimension dimension, string reason)
	{
		if (plan != null && dimension != null)
		{
			plan.Add(dimension);
			plan.MarkSuppressed(dimension, reason);
			plan.Dimensions.Remove(dimension);
		}
	}

	private void AddHorizontalDim(DimensionPlan plan, Point2D from, Point2D to, string debugRole, string alignmentKey = null, int alignmentPriority = 0)
	{
		AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Bottom, from, to, string.Empty, debugRole, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority);
	}

	private void AddVerticalDim(DimensionPlan plan, Point2D from, Point2D to, string debugRole, string alignmentKey = null, int alignmentPriority = 0)
	{
		AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Vertical, DimensionSide.Left, from, to, string.Empty, debugRole, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority);
	}

	private void AddHorizontalDimFromX(DimensionPlan plan, double x, Point2D target, string debugRole)
	{
		AddHorizontalDim(plan, new Point2D(x, target.Y), target, debugRole);
	}

	private void AddVerticalDimFromY(DimensionPlan plan, double y, Point2D target, string debugRole)
	{
		AddVerticalDim(plan, new Point2D(target.X, y), target, debugRole);
	}

	private void AddHorizontalChainFromDatum(DimensionPlan plan, double datumX, IList<Point2D> ordered, string debugRole)
	{
		if (ordered != null && ordered.Count != 0)
		{
			AddHorizontalDimFromX(plan, datumX, ordered[0], debugRole);
			for (int i = 1; i < ordered.Count; i++)
			{
				AddHorizontalDim(plan, ordered[i - 1], ordered[i], debugRole);
			}
		}
	}

	private void AddVerticalChainFromDatum(DimensionPlan plan, double datumY, IList<Point2D> ordered, string debugRole)
	{
		if (ordered != null && ordered.Count != 0)
		{
			AddVerticalDimFromY(plan, datumY, ordered[0], debugRole);
			for (int i = 1; i < ordered.Count; i++)
			{
				AddVerticalDim(plan, ordered[i - 1], ordered[i], debugRole);
			}
		}
	}

	private List<List<Point2D>> GroupSlotAnchorsByCoordinate(IEnumerable<SlotFeature2D> slots, Datum2D datum, Func<Point2D, double> coordinate)
	{
		List<List<Point2D>> list = new List<List<Point2D>>();
		if (slots == null || datum == null)
		{
			return list;
		}
		foreach (Point2D point in slots.Select((SlotFeature2D s) => PickSlotAnchorPoint(s, datum)))
		{
			List<Point2D> list2 = list.FirstOrDefault((List<Point2D> g) => Math.Abs(coordinate(g[0]) - coordinate(point)) <= _config.GeometryTolerance);
			if (list2 == null)
			{
				list2 = new List<Point2D>();
				list.Add(list2);
			}
			list2.Add(point);
		}
		return list;
	}

	private List<Point2D> UniquePointsByCoordinate(IEnumerable<Point2D> points, Func<Point2D, double> coordinate)
	{
		List<Point2D> list = new List<Point2D>();
		foreach (Point2D point in points)
		{
			if (list.Count == 0 || Math.Abs(coordinate(list[list.Count - 1]) - coordinate(point)) > _config.GeometryTolerance)
			{
				list.Add(point);
			}
		}
		return list;
	}

	private SlotFeature2D FindSlotByAnchor(IEnumerable<SlotFeature2D> slots, Point2D anchor)
	{
		return slots?.FirstOrDefault((SlotFeature2D s) => s != null && Math.Abs(s.FirstCenter.X - anchor.X) <= _config.GeometryTolerance && Math.Abs(s.FirstCenter.Y - anchor.Y) <= _config.GeometryTolerance);
	}

	private void AddSingleArcSlotHorizontalDatumDimension(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, SlotFeature2D slot, Point2D center)
	{
		if (!OutlineGeometryQuery.TryFindVerticalBoundaryPoint(outline, datum.BaseX, center.Y, _config.GeometryTolerance, out var firstPoint))
		{
			plan.AddSkippedDimension(DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Bottom, new Point2D(datum.BaseX, center.Y), center, "NoRealOutlineAttachment:XDatum=" + datum.BaseX.ToString("0.########", CultureInfo.InvariantCulture), "SingleArcSlotDatum", slot.GroupId);
			return;
		}
		Point2D singleArcSlotHorizontalGripPoint = GetSingleArcSlotHorizontalGripPoint(slot, firstPoint.Y);
		double num = Math.Abs(center.X - datum.BaseX);
		if (!(num <= _config.GeometryTolerance))
		{
			AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Bottom, firstPoint, singleArcSlotHorizontalGripPoint, _config.FormatNumber(num), "SingleArcSlotDatum", slot.GroupId);
		}
	}

	private Point2D FindOutlinePointAtX(OutlineFeature2D outline, double x, double preferredY)
	{
		List<Point2D> list = new List<Point2D>();
		foreach (Point2D vertex in outline.Vertices)
		{
			if (Math.Abs(vertex.X - x) <= _config.GeometryTolerance)
			{
				list.Add(vertex);
			}
		}
		foreach (Segment2D item in outline.Segments.Where((Segment2D s) => s.IsVertical(_config.GeometryTolerance)))
		{
			if (!(Math.Abs(item.Start.X - x) > _config.GeometryTolerance))
			{
				double y = Math.Max(item.MinY, Math.Min(item.MaxY, preferredY));
				list.Add(new Point2D(x, y));
			}
		}
		// Do not use FirstOrDefault() on Point2D (default (0,0) looks like a hit).
		if (list.Count == 0)
		{
			return new Point2D(x, preferredY);
		}
		return list.OrderBy((Point2D p) => Math.Abs(p.Y - preferredY)).First();
	}

	private Point2D GetSingleArcSlotHorizontalGripPoint(SlotFeature2D slot, double preferredY)
	{
		Point2D point2D = new Point2D(slot.FirstCenter.X, slot.FirstCenter.Y + slot.Radius);
		Point2D point2D2 = new Point2D(slot.FirstCenter.X, slot.FirstCenter.Y - slot.Radius);
		return (Math.Abs(point2D.Y - preferredY) <= Math.Abs(point2D2.Y - preferredY)) ? point2D : point2D2;
	}

	private static bool IsVerticalSlot(SlotFeature2D slot)
	{
		if (slot.IsSingleArcSlot)
		{
			return slot.IsVertical;
		}
		return Math.Abs(slot.FirstCenter.Y - slot.SecondCenter.Y) >= Math.Abs(slot.FirstCenter.X - slot.SecondCenter.X);
	}

	private static Point2D PickNearestPointByY(IEnumerable<Point2D> points, double y)
	{
		return points.OrderBy((Point2D p) => Math.Abs(p.Y - y)).First();
	}

	private static Point2D PickNearestPointByX(IEnumerable<Point2D> points, double x)
	{
		return points.OrderBy((Point2D p) => Math.Abs(p.X - x)).First();
	}

	private Point2D PickSlotAnchorPoint(SlotFeature2D slot, Datum2D datum)
	{
		Point2D firstCenter = slot.FirstCenter;
		if (slot.IsSingleArcSlot)
		{
			return firstCenter;
		}
		Point2D secondCenter = slot.SecondCenter;
		double num = Math.Abs(firstCenter.X - secondCenter.X);
		double num2 = Math.Abs(firstCenter.Y - secondCenter.Y);
		if (num >= num2)
		{
			return (Math.Abs(firstCenter.X - datum.BaseX) <= Math.Abs(secondCenter.X - datum.BaseX)) ? firstCenter : secondCenter;
		}
		return (Math.Abs(firstCenter.Y - datum.BaseY) <= Math.Abs(secondCenter.Y - datum.BaseY)) ? firstCenter : secondCenter;
	}

	private double GetSpan(Point2D firstPoint, Point2D secondPoint, DimensionOrientation orientation)
	{
		return (orientation == DimensionOrientation.Horizontal) ? Math.Abs(secondPoint.X - firstPoint.X) : Math.Abs(secondPoint.Y - firstPoint.Y);
	}

	private double GetAverageY(IList<HoleFeature2D> holes)
	{
		return (holes.Count == 0) ? 0.0 : holes.Average((HoleFeature2D h) => h.Center.Y);
	}

	private bool IsSamePinDiameter(HoleFeature2D a, HoleFeature2D b)
	{
		return a != null && b != null && Math.Abs(a.Diameter - b.Diameter) <= _config.GeometryTolerance;
	}

	private bool IsSameHole(HoleFeature2D a, HoleFeature2D b)
	{
		return a != null && b != null && PointsEqual(a.Center, b.Center);
	}

	private void RemoveGroupPins(IList<HoleFeature2D> remaining, PinGroupPlan group)
	{
		foreach (HoleFeature2D item in group.Pins.ToList())
		{
			for (int num = remaining.Count - 1; num >= 0; num--)
			{
				if (IsSameHole(remaining[num], item))
				{
					remaining.RemoveAt(num);
				}
			}
		}
	}

	private double DistanceToPinPair(HoleFeature2D hole, PinGroupPlan group)
	{
		if (hole == null || group == null || group.Pins.Count == 0)
		{
			return double.MaxValue;
		}
		if (group.Pins.Count == 1)
		{
			return Math.Sqrt(DistanceSquared(hole.Center, group.Pins[0].Center));
		}
		return group.Pins.Take(2).Sum((HoleFeature2D pin) => Math.Sqrt(DistanceSquared(hole.Center, pin.Center)));
	}

	/// <summary>
	/// True when the hole projects strictly inside the pin-pair segment and stays near the pin axis.
	/// Used to claim single (or non-grid) holes that sit between the two pin centers as functional holes.
	/// </summary>
	private bool IsHoleBetweenPinPair(HoleFeature2D hole, PinGroupPlan group)
	{
		if (hole == null || group == null || group.Pins.Count < 2)
		{
			return false;
		}
		Point2D pinA = group.Pins[0].Center;
		Point2D pinB = group.Pins[1].Center;
		Point2D center = hole.Center;
		double dx = pinB.X - pinA.X;
		double dy = pinB.Y - pinA.Y;
		double lengthSquared = dx * dx + dy * dy;
		if (lengthSquared <= _config.GeometryTolerance * _config.GeometryTolerance)
		{
			return false;
		}
		double t = ((center.X - pinA.X) * dx + (center.Y - pinA.Y) * dy) / lengthSquared;
		// Strictly between the two pins (exclude endpoints / outside the span).
		double endMargin = Math.Min(0.05, 0.1);
		if (t <= endMargin || t >= 1.0 - endMargin)
		{
			return false;
		}
		double projX = pinA.X + t * dx;
		double projY = pinA.Y + t * dy;
		double perpendicular = Math.Sqrt((center.X - projX) * (center.X - projX) + (center.Y - projY) * (center.Y - projY));
		double axisTolerance = Math.Max(GetFunctionalHoleAlignmentTolerance(), Math.Max(group.Pins[0].Diameter, group.Pins[1].Diameter) * 0.5);
		return perpendicular <= axisTolerance;
	}

	private double DistanceSquared(Point2D a, Point2D b)
	{
		double num = a.X - b.X;
		double num2 = a.Y - b.Y;
		return num * num + num2 * num2;
	}

	private Point2D Midpoint(Point2D a, Point2D b)
	{
		return new Point2D((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
	}

	private DimensionSide ChooseHorizontalHoleSide(OutlineFeature2D outline, Datum2D datum, Point2D point)
	{
		double num = Math.Abs(point.Y - outline.MinY);
		double num2 = Math.Abs(outline.MaxY - point.Y);
		if (Math.Abs(num - num2) <= _config.GeometryTolerance)
		{
			return ChooseOppositeHorizontalDatumSide(outline, datum);
		}
		return (!(num < num2)) ? DimensionSide.Top : DimensionSide.Bottom;
	}

	private DimensionSide ChooseVerticalHoleSide(OutlineFeature2D outline, Datum2D datum, Point2D point)
	{
		double num = Math.Abs(point.X - outline.MinX);
		double num2 = Math.Abs(outline.MaxX - point.X);
		if (Math.Abs(num - num2) <= _config.GeometryTolerance)
		{
			return ChooseOppositeVerticalDatumSide(outline, datum);
		}
		return (num < num2) ? DimensionSide.Left : DimensionSide.Right;
	}

	private DimensionSide ChooseHorizontalHoleSide(OutlineFeature2D outline, Point2D point)
	{
		double num = Math.Abs(point.Y - outline.MinY);
		double num2 = Math.Abs(outline.MaxY - point.Y);
		return (!(num <= num2)) ? DimensionSide.Top : DimensionSide.Bottom;
	}

	private DimensionSide ChooseVerticalHoleSide(OutlineFeature2D outline, Point2D point)
	{
		double num = Math.Abs(point.X - outline.MinX);
		double num2 = Math.Abs(outline.MaxX - point.X);
		return (num <= num2) ? DimensionSide.Left : DimensionSide.Right;
	}

	private DimensionSide ChooseOppositeHorizontalDatumSide(OutlineFeature2D outline, Datum2D datum)
	{
		double num = Math.Abs(datum.BaseY - outline.MinY);
		double num2 = Math.Abs(outline.MaxY - datum.BaseY);
		return (num <= num2 || 1 == 0) ? DimensionSide.Top : DimensionSide.Bottom;
	}

	private DimensionSide ChooseOppositeVerticalDatumSide(OutlineFeature2D outline, Datum2D datum)
	{
		double num = Math.Abs(datum.BaseX - outline.MinX);
		double num2 = Math.Abs(outline.MaxX - datum.BaseX);
		DimensionSide dimensionSide = ((num <= num2) ? DimensionSide.Left : DimensionSide.Right);
		return (dimensionSide == DimensionSide.Left) ? DimensionSide.Right : DimensionSide.Left;
	}

	private static bool ShouldUseDatumHoleLocationTolerance(Datum2D datum, bool isXDirection)
	{
		if (datum.DatumHole == null)
		{
			return true;
		}
		return isXDirection ? datum.DatumHoleLocationUseToleranceX : datum.DatumHoleLocationUseToleranceY;
	}

	private string GetPinGroupDebugOwner(PinGroupPlan group)
	{
		return (group == null) ? string.Empty : ("PG" + group.GroupIndex.ToString(CultureInfo.InvariantCulture));
	}

	private static string GetPinDatumAlignmentKey(PinGroupPlan firstGroup, bool horizontal)
	{
		if (firstGroup == null)
		{
			return string.Empty;
		}
		return "PG" + firstGroup.GroupIndex.ToString(CultureInfo.InvariantCulture)
			+ ":DatumChain:" + (horizontal ? "H" : "V");
	}

	private static string GetPinFunctionalHoleAlignmentKey(PinGroupPlan group, bool horizontal)
	{
		if (group == null)
		{
			return string.Empty;
		}
		return "PG" + group.GroupIndex.ToString(CultureInfo.InvariantCulture)
			+ ":FunctionalHoles:" + (horizontal ? "H" : "V");
	}

	private static string GetStructureAlignmentKey(DimensionSide side, bool horizontal)
	{
		string sideTag = side switch
		{
			DimensionSide.Top => "T",
			DimensionSide.Bottom => "B",
			DimensionSide.Left => "L",
			DimensionSide.Right => "R",
			_ => side.ToString()
		};
		return "Structure:" + sideTag + ":" + (horizontal ? "H" : "V");
	}

	/// <summary>
	/// Shared alignment lane for one continuous slot locating chain
	/// (edge location + inter-slot center distances on the same column/row).
	/// </summary>
	private static string GetSlotChainAlignmentKey(DimensionSide side, bool horizontal, double sharedCoordinate)
	{
		string sideTag = side switch
		{
			DimensionSide.Top => "T",
			DimensionSide.Bottom => "B",
			DimensionSide.Left => "L",
			DimensionSide.Right => "R",
			_ => side.ToString()
		};
		string coord = sharedCoordinate.ToString("0.###", CultureInfo.InvariantCulture);
		return "SlotChain:" + sideTag + ":" + (horizontal ? "H" : "V") + ":" + coord;
	}

}
