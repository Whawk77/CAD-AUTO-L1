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

	private void AddNonPinHolesFromOutlineEdge(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, IList<HoleFeature2D> holes)
	{
		List<HoleFeature2D> list = (holes ?? new List<HoleFeature2D>()).Where((HoleFeature2D h) => h != null && !h.IsPinHole && !h.IsSlotPoint).ToList();
		HoleFeature2D baseHole = ChooseNonPinBaseHole(list, datum);
		if (baseHole != null)
		{
			List<HoleFeature2D> normalTargets = list.Where((HoleFeature2D h) => !h.IsThreadHole && !IsSameHole(h, baseHole)).ToList();
			string horizontalAlignmentKey = GetNonPinHorizontalChainAlignmentKey(outline, baseHole, normalTargets);
			AddNonPinHoleDatumLocation(plan, outline, datum, baseHole, horizontalAlignmentKey);
			AddNonPinHoleChainsFromBase(plan, outline, baseHole, normalTargets, "HoleChain", horizontalAlignmentKey);
			AddNonPinHoleChainsFromBase(plan, outline, baseHole, list.Where((HoleFeature2D h) => h.IsThreadHole && !IsSameHole(h, baseHole)).ToList(), "ThreadHoleChain");
		}
	}

	private HoleFeature2D ChooseNonPinBaseHole(IList<HoleFeature2D> holes, Datum2D datum)
	{
		List<HoleFeature2D> list = holes.Where((HoleFeature2D h) => !h.IsThreadHole).ToList();
		IList<HoleFeature2D> source = ((list.Count > 0) ? list : holes);
		return (from h in source
			orderby DistanceSquared(h.Center, new Point2D(datum.BaseX, datum.BaseY)), h.Center.X, h.Center.Y
			select h).FirstOrDefault();
	}

	private void AddNonPinHoleChainsFromBase(DimensionPlan plan, OutlineFeature2D outline, HoleFeature2D baseHole, IList<HoleFeature2D> targets, string debugRole, string horizontalAlignmentKey = null)
	{
		if (targets != null && targets.Count != 0)
		{
			List<HoleFeature2D> list = new List<HoleFeature2D> { baseHole };
			list.AddRange(targets);
			AddNonPinHoleAxisChain(plan, outline, list, horizontalAxis: true, debugRole + "H", horizontalAlignmentKey);
			List<HoleFeature2D> list2 = new List<HoleFeature2D> { baseHole };
			list2.AddRange(targets);
			AddNonPinHoleAxisChain(plan, outline, list2, horizontalAxis: false, debugRole + "V");
		}
	}

	private void AddNonPinHoleAxisChain(DimensionPlan plan, OutlineFeature2D outline, IList<HoleFeature2D> holes, bool horizontalAxis, string debugRole, string alignmentKey = null)
	{
		List<HoleFeature2D> list = (from h in holes
			orderby horizontalAxis ? h.Center.X : h.Center.Y, horizontalAxis ? h.Center.Y : h.Center.X
			select h).ToList();
		DimensionSide side = ChooseLooseDimensionSide(outline, list, horizontalAxis);
		for (int num = 1; num < list.Count; num++)
		{
			AddDimension(plan, DimensionKind.HoleLocation, (!horizontalAxis) ? DimensionOrientation.Vertical : DimensionOrientation.Horizontal, side, list[num - 1].Center, list[num].Center, string.Empty, debugRole, alignmentKey: horizontalAxis ? alignmentKey : null, alignmentPriority: horizontalAxis && !string.IsNullOrEmpty(alignmentKey) ? 80 : 0);
		}
	}

	private string GetNonPinHorizontalChainAlignmentKey(OutlineFeature2D outline, HoleFeature2D baseHole, IList<HoleFeature2D> targets)
	{
		List<HoleFeature2D> chain = new List<HoleFeature2D> { baseHole };
		chain.AddRange(targets ?? new HoleFeature2D[0]);
		if (chain.Count < 2)
		{
			return string.Empty;
		}
		DimensionSide side = ChooseLooseDimensionSide(outline, chain, horizontal: true);
		HoleFeature2D first = chain.OrderBy((HoleFeature2D h) => h.Center.X).ThenBy((HoleFeature2D h) => h.Center.Y).First();
		HoleFeature2D last = chain.OrderByDescending((HoleFeature2D h) => h.Center.X).ThenByDescending((HoleFeature2D h) => h.Center.Y).First();
		return "LooseHoleChain:" + side.ToString() + ":H:"
			+ baseHole.Center.X.ToString("0.###", CultureInfo.InvariantCulture) + "," + baseHole.Center.Y.ToString("0.###", CultureInfo.InvariantCulture)
			+ ":" + first.Center.X.ToString("0.###", CultureInfo.InvariantCulture) + "," + first.Center.Y.ToString("0.###", CultureInfo.InvariantCulture)
			+ "-" + last.Center.X.ToString("0.###", CultureInfo.InvariantCulture) + "," + last.Center.Y.ToString("0.###", CultureInfo.InvariantCulture);
	}

	private void AddNonPinHoleDatumLocation(DimensionPlan plan, OutlineFeature2D outline, Datum2D datum, HoleFeature2D hole, string horizontalAlignmentKey = null)
	{
		AddHorizontalOutlineReferenceDimension(plan, outline, datum.BaseX, hole.Center, DimensionKind.HoleLocation, ChooseHorizontalHoleSide(outline, hole.Center), string.Empty, "HoleDatumX", alignmentKey: horizontalAlignmentKey, alignmentPriority: string.IsNullOrEmpty(horizontalAlignmentKey) ? 0 : 80);
		AddVerticalOutlineReferenceDimension(plan, outline, datum.BaseY, hole.Center, DimensionKind.HoleLocation, ChooseVerticalHoleSide(outline, hole.Center), string.Empty, "HoleDatumY", preservePreferredSide: true);
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
			DimensionSide? anchoredSide = ChoosePinGroupAnchoredDimensionSide(referencePinGroup, lineGroup.Horizontal);
			DimensionSide side = anchoredSide ?? ChooseLooseDimensionSide(outline, list2, lineGroup.Horizontal);
			for (int num = 1; num < list2.Count; num++)
			{
				string looseDimKey = GetLooseDimKey(list2[num - 1], list2[num], lineGroup.Horizontal);
				if (hashSet.Add(looseDimKey))
				{
					AddLooseHoleLocationDimension(plan, list2[num - 1].Center, list2[num].Center, lineGroup.Horizontal, side, chainId, preservePreferredSide: anchoredSide.HasValue);
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
			DimensionSide? anchoredHorizontalSide = ChoosePinGroupAnchoredDimensionSide(referencePinGroup, horizontal: true);
			AddLooseHoleLocationDimension(plan, point2D, target.Center, horizontal: true, anchoredHorizontalSide ?? ChooseLooseDimensionSide(outline, new HoleFeature2D[1] { target }, horizontal: true), chainId, preservePreferredSide: anchoredHorizontalSide.HasValue);
		}
		Point2D point2D2 = (forcePinReference ? pinReference : ChooseLooseLocationReference(pinReference, target, located, horizontal: false));
		if (Math.Abs(point2D2.Y - target.Center.Y) > _config.GeometryTolerance)
		{
			int chainId2 = _nextLooseChainId++;
			DimensionSide? anchoredVerticalSide = ChoosePinGroupAnchoredDimensionSide(referencePinGroup, horizontal: false);
			AddLooseHoleLocationDimension(plan, point2D2, target.Center, horizontal: false, anchoredVerticalSide ?? ChooseLooseDimensionSide(outline, new HoleFeature2D[1] { target }, horizontal: false), chainId2, preservePreferredSide: anchoredVerticalSide.HasValue);
		}
	}

	private void AddLooseHoleLocationDimension(DimensionPlan plan, Point2D from, Point2D to, bool horizontal, DimensionSide side, int chainId, bool preservePreferredSide = false)
	{
		AddDimension(plan, DimensionKind.HoleLocation, (!horizontal) ? DimensionOrientation.Vertical : DimensionOrientation.Horizontal, side, from, to, string.Empty, "LooseHole", (chainId == 0) ? string.Empty : ("L" + chainId.ToString(CultureInfo.InvariantCulture)), preferLocalBoundary: true, chainId: chainId, preservePreferredSide: preservePreferredSide);
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
}
