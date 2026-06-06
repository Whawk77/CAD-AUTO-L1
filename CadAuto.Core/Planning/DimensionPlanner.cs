using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning
{
    public sealed class DimensionPlanner
    {
        private readonly DimensionRuleConfig _config;

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

        private int _nextLooseChainId = 1;

        public DimensionPlanner(DimensionRuleConfig config)
        {
            _config = config;
        }

        public DimensionPlan CreateOutlinePlan(OutlineFeature2D outline)
        {
            if (outline == null)
            {
                throw new ArgumentNullException("outline");
            }

            _nextLooseChainId = 1;
            var plan = new DimensionPlan();
            AddOverallWidth(plan, outline);
            AddOverallHeight(plan, outline);
            AddStepOutlineDimensions(plan, outline);
            AddLinearSegmentDimensions(plan, outline);
            SuppressDuplicateDimensions(plan);
            return plan;
        }

        public DimensionPlan CreateDimensionPlan(
            OutlineFeature2D outline,
            Datum2D datum,
            IEnumerable<HoleFeature2D> holes)
        {
            return CreateDimensionPlan(outline, datum, holes, new SlotFeature2D[0]);
        }

        public DimensionPlan CreateDimensionPlan(
            OutlineFeature2D outline,
            Datum2D datum,
            IEnumerable<HoleFeature2D> holes,
            IEnumerable<SlotFeature2D> slots)
        {
            var plan = CreateOutlinePlan(outline);
            var effectiveDatum = datum ?? Datum2D.FromOutline(outline);
            var holeList = (holes ?? new HoleFeature2D[0]).Where(h => h != null).ToList();
            AddHolePositionDimensions(plan, outline, effectiveDatum, holeList);
            AddSlotDimensions(plan, outline, effectiveDatum, holeList, slots ?? new SlotFeature2D[0]);
            SuppressDuplicateDimensions(plan);
            return plan;
        }

        public IList<IList<HoleFeature2D>> GroupHolesByHorizontalRow(IEnumerable<HoleFeature2D> holes)
        {
            var sorted = (holes ?? new HoleFeature2D[0])
                .Where(h => h != null)
                .OrderBy(h => h.Center.Y)
                .ThenBy(h => h.Center.X)
                .ToList();
            var rows = new List<IList<HoleFeature2D>>();

            foreach (var hole in sorted)
            {
                var row = rows.FirstOrDefault(r => Math.Abs(GetAverageY(r) - hole.Center.Y) <= _config.GeometryTolerance);
                if (row == null)
                {
                    rows.Add(new List<HoleFeature2D> { hole });
                }
                else
                {
                    row.Add(hole);
                }
            }

            foreach (var row in rows)
            {
                var ordered = row.OrderBy(h => h.Center.X).ToList();
                row.Clear();
                foreach (var hole in ordered)
                {
                    row.Add(hole);
                }
            }

            return rows.OrderBy(r => GetAverageY(r)).ToList();
        }

        public IList<PinGroupPlan> BuildPinGroupPlan(IEnumerable<HoleFeature2D> holes, HoleFeature2D datumPin)
        {
            var allHoles = (holes ?? new HoleFeature2D[0])
                .Where(h => h != null && !h.IsSlotPoint)
                .ToList();
            var remaining = allHoles
                .Where(h => h.IsPinHole)
                .OrderBy(h => h.Center.X)
                .ThenBy(h => h.Center.Y)
                .ToList();
            var groups = new List<PinGroupPlan>();
            if (remaining.Count == 0)
            {
                return groups;
            }

            var seed = datumPin != null && datumPin.IsPinHole
                ? remaining.FirstOrDefault(h => IsSameHole(h, datumPin)) ?? datumPin
                : remaining.OrderBy(h => h.Center.X).ThenBy(h => h.Center.Y).First();

            var firstGroup = CreatePinPairGroup(seed, remaining, seed, null);
            groups.Add(firstGroup);
            RemoveGroupPins(remaining, firstGroup);

            while (remaining.Count > 0)
            {
                var previousBase = groups[groups.Count - 1].BasePin;
                seed = remaining
                    .OrderBy(h => DistanceSquared(h.Center, previousBase.Center))
                    .ThenBy(h => h.Center.X)
                    .ThenBy(h => h.Center.Y)
                    .First();

                var group = CreatePinPairGroup(seed, remaining, null, previousBase);
                groups.Add(group);
                RemoveGroupPins(remaining, group);
            }

            for (int i = 0; i < groups.Count; i++)
            {
                groups[i].GroupIndex = i + 1;
            }

            AssignNonPinHolesToNearestPinPair(allHoles, groups);
            return groups;
        }

        private void AddHolePositionDimensions(
            DimensionPlan plan,
            OutlineFeature2D outline,
            Datum2D datum,
            IEnumerable<HoleFeature2D> holes)
        {
            var holeList = (holes ?? new HoleFeature2D[0])
                .Where(h => h != null && !h.IsSlotPoint)
                .ToList();
            if (holeList.Count == 0)
            {
                return;
            }

            var pinGroups = BuildPinGroupPlan(holeList, datum.DatumHole);
            foreach (var group in pinGroups)
            {
                group.HorizontalSide = ChooseHorizontalHoleSide(outline, datum, group.BasePin.Center);
                group.VerticalSide = ChooseVerticalHoleSide(outline, datum, group.BasePin.Center);
                plan.PinGroups.Add(group);
            }

            if (pinGroups.Count == 0)
            {
                AddNonPinHolesFromOutlineEdge(plan, outline, datum, holeList);
                return;
            }

            AddFirstPinGroupBaseLocation(plan, datum, pinGroups[0]);
            AddPinGroupBaseTransfers(plan, pinGroups);
            AddSameGroupPinDistances(plan, outline, pinGroups);
            AddNonPinHoleLocationsFromPinGroups(plan, outline, holeList, pinGroups);
        }

        private void AddSlotDimensions(
            DimensionPlan plan,
            OutlineFeature2D outline,
            Datum2D datum,
            IEnumerable<HoleFeature2D> holes,
            IEnumerable<SlotFeature2D> slots)
        {
            var slotList = (slots ?? new SlotFeature2D[0]).Where(s => s != null).ToList();
            if (slotList.Count == 0)
            {
                return;
            }

            var holeRows = GroupHolesByHorizontalRow(holes ?? new HoleFeature2D[0]);
            var hasPinHoles = holeRows.SelectMany(r => r).Any(h => h.IsPinHole);
            foreach (var slot in slotList)
            {
                AddSlotCenterDistanceDimension(plan, slot);
            }

            if (!hasPinHoles)
            {
                AddSlotContinuousLocationsFromDatum(plan, outline, datum, slotList);
                return;
            }

            AddSlotAnchorLocations(plan, datum, holeRows, slotList);
        }

        private void AddSlotContinuousLocationsFromDatum(
            DimensionPlan plan,
            OutlineFeature2D outline,
            Datum2D datum,
            IList<SlotFeature2D> slots)
        {
            if (datum == null || slots == null || slots.Count == 0)
            {
                return;
            }

            var verticalSlots = slots.Where(IsVerticalSlot).ToList();
            var horizontalSlots = slots.Where(s => !IsVerticalSlot(s)).ToList();
            AddVerticalSlotContinuousDimensions(plan, datum, verticalSlots);
            AddHorizontalSlotContinuousDimensions(plan, outline, datum, horizontalSlots);
        }

        private void AddVerticalSlotContinuousDimensions(
            DimensionPlan plan,
            Datum2D datum,
            IList<SlotFeature2D> slots)
        {
            foreach (var group in GroupSlotAnchorsByCoordinate(slots, datum, p => p.Y))
            {
                var ordered = UniquePointsByCoordinate(group.OrderBy(p => p.X), p => p.X);
                AddHorizontalChainFromDatum(plan, datum.BaseX, ordered, "SlotChainH");
                if (ordered.Count > 0)
                {
                    AddVerticalDimFromY(plan, datum.BaseY, PickNearestPointByX(ordered, datum.BaseX), "SlotDatumV");
                }
            }
        }

        private void AddHorizontalSlotContinuousDimensions(
            DimensionPlan plan,
            OutlineFeature2D outline,
            Datum2D datum,
            IList<SlotFeature2D> slots)
        {
            foreach (var group in GroupSlotAnchorsByCoordinate(slots, datum, p => p.X))
            {
                var ordered = UniquePointsByCoordinate(group.OrderBy(p => p.Y), p => p.Y);
                AddVerticalChainFromDatum(plan, datum.BaseY, ordered, "SlotChainV");
                if (ordered.Count == 0)
                {
                    continue;
                }

                var target = PickNearestPointByY(ordered, datum.BaseY);
                var slot = FindSlotByAnchor(slots, target);
                if (slot != null && slot.IsSingleArcSlot && outline != null)
                {
                    AddSingleArcSlotHorizontalDatumDimension(plan, outline, datum, slot, target);
                }
                else
                {
                    AddHorizontalDimFromX(plan, datum.BaseX, target, "SlotDatumH");
                }
            }
        }

        private void AddSlotCenterDistanceDimension(DimensionPlan plan, SlotFeature2D slot)
        {
            if (slot == null || slot.CenterDistance <= _config.GeometryTolerance)
            {
                return;
            }

            var first = slot.FirstCenter;
            var second = slot.SecondCenter;
            var dx = Math.Abs(first.X - second.X);
            var dy = Math.Abs(first.Y - second.Y);
            if (dx <= _config.GeometryTolerance && dy <= _config.GeometryTolerance)
            {
                return;
            }

            if (dx >= dy)
            {
                AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Bottom,
                    first, second, string.Empty, "SlotCenter", slot.GroupId);
            }
            else
            {
                AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Vertical, DimensionSide.Left,
                    first, second, string.Empty, "SlotCenter", slot.GroupId);
            }
        }

        private void AddSlotAnchorLocations(
            DimensionPlan plan,
            Datum2D datum,
            IList<IList<HoleFeature2D>> rows,
            IList<SlotFeature2D> slots)
        {
            if (datum == null || rows == null || slots == null || slots.Count == 0)
            {
                return;
            }

            var holes = rows.SelectMany(r => r).Where(h => h != null && !h.IsSlotPoint).ToList();
            var pinGroups = BuildPinGroupPlan(holes, datum.DatumHole);
            if (pinGroups.Count == 0)
            {
                foreach (var slot in slots)
                {
                    var anchor = PickSlotAnchorPoint(slot, datum);
                    AddHorizontalDimFromX(plan, datum.BaseX, anchor, "SlotDatumH");
                    AddVerticalDimFromY(plan, datum.BaseY, anchor, "SlotDatumV");
                }

                return;
            }

            var referencePins = pinGroups
                .SelectMany(g => g.Pins.Select(p => new { Pin = p, IsGroupBase = IsSameHole(p, g.BasePin) }))
                .ToList();
            foreach (var slot in slots)
            {
                var anchor = PickSlotAnchorPoint(slot, datum);
                var reference = referencePins
                    .OrderBy(r => DistanceSquared(r.Pin.Center, anchor))
                    .ThenByDescending(r => r.IsGroupBase ? 1 : 0)
                    .FirstOrDefault();
                if (reference == null)
                {
                    continue;
                }

                AddHorizontalDim(plan, reference.Pin.Center, anchor, "SlotPinRef");
                AddVerticalDim(plan, reference.Pin.Center, anchor, "SlotPinRef");
            }
        }

        private void SuppressDuplicateDimensions(DimensionPlan plan)
        {
            SuppressRightStructureHeightsDuplicatingOverallHeight(plan);
            SuppressLeftStructureHeightsCoveredByRight(plan);
            SuppressMirroredDuplicates(plan, DimensionSide.Bottom, DimensionSide.Top, horizontal: true);
            SuppressMirroredDuplicates(plan, DimensionSide.Left, DimensionSide.Right, horizontal: false);
            SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Bottom, horizontal: true);
            SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Top, horizontal: true);
            SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Left, horizontal: false);
            SuppressDuplicateMeasuredDimensions(plan, DimensionSide.Right, horizontal: false);
        }

        private void SuppressRightStructureHeightsDuplicatingOverallHeight(DimensionPlan plan)
        {
            var overallHeights = plan.Dimensions
                .Where(dim => dim.Kind == DimensionKind.OverallHeight)
                .ToList();
            if (overallHeights.Count == 0)
            {
                return;
            }

            for (int i = plan.Dimensions.Count - 1; i >= 0; i--)
            {
                var right = plan.Dimensions[i];
                if (!IsRightStructureHeight(right))
                {
                    continue;
                }

                if (overallHeights.Any(overall => IsSameVerticalInterval(right, overall)))
                {
                    plan.Dimensions.RemoveAt(i);
                }
            }
        }

        private void SuppressLeftStructureHeightsCoveredByRight(DimensionPlan plan)
        {
            var rightStructureHeights = plan.Dimensions
                .Where(IsRightStructureHeight)
                .ToList();
            if (rightStructureHeights.Count == 0)
            {
                return;
            }

            for (int i = plan.Dimensions.Count - 1; i >= 0; i--)
            {
                var left = plan.Dimensions[i];
                if (!IsLeftStructureHeight(left))
                {
                    continue;
                }

                if (rightStructureHeights.Any(right => IsVerticalIntervalCoveredByRightStructure(left, right)))
                {
                    plan.Dimensions.RemoveAt(i);
                }
            }
        }

        private static bool IsLeftStructureHeight(PlannedDimension dim)
        {
            return dim.Kind == DimensionKind.Normal
                && string.Equals(dim.DebugRole, "LeftStructHeight", StringComparison.Ordinal);
        }

        private static bool IsRightStructureHeight(PlannedDimension dim)
        {
            return dim.Kind == DimensionKind.Normal
                && string.Equals(dim.DebugRole, "RightStructHeight", StringComparison.Ordinal);
        }

        private bool IsSameVerticalInterval(PlannedDimension a, PlannedDimension b)
        {
            var aMin = Math.Min(a.FirstPoint.Y, a.SecondPoint.Y);
            var aMax = Math.Max(a.FirstPoint.Y, a.SecondPoint.Y);
            var bMin = Math.Min(b.FirstPoint.Y, b.SecondPoint.Y);
            var bMax = Math.Max(b.FirstPoint.Y, b.SecondPoint.Y);
            return Math.Abs(aMin - bMin) <= _config.GeometryTolerance
                && Math.Abs(aMax - bMax) <= _config.GeometryTolerance;
        }

        private bool IsVerticalIntervalCoveredByRightStructure(PlannedDimension left, PlannedDimension right)
        {
            var leftMin = Math.Min(left.FirstPoint.Y, left.SecondPoint.Y);
            var leftMax = Math.Max(left.FirstPoint.Y, left.SecondPoint.Y);
            var rightMin = Math.Min(right.FirstPoint.Y, right.SecondPoint.Y);
            var rightMax = Math.Max(right.FirstPoint.Y, right.SecondPoint.Y);
            var sharesEndpoint = Math.Abs(leftMin - rightMin) <= _config.GeometryTolerance
                || Math.Abs(leftMin - rightMax) <= _config.GeometryTolerance
                || Math.Abs(leftMax - rightMin) <= _config.GeometryTolerance
                || Math.Abs(leftMax - rightMax) <= _config.GeometryTolerance;
            if (!sharesEndpoint)
            {
                return false;
            }

            return leftMin >= rightMin - _config.GeometryTolerance
                && leftMax <= rightMax + _config.GeometryTolerance
                && GetDimensionSpan(right, horizontal: false) >= GetDimensionSpan(left, horizontal: false) - _config.GeometryTolerance;
        }

        private void SuppressMirroredDuplicates(
            DimensionPlan plan,
            DimensionSide primarySide,
            DimensionSide secondarySide,
            bool horizontal)
        {
            var primary = plan.Dimensions.Where(d => d.Side == primarySide).ToList();
            var secondary = plan.Dimensions.Where(d => d.Side == secondarySide).ToList();
            foreach (var candidate in secondary.ToList())
            {
                foreach (var existing in primary.ToList())
                {
                    if (!CanSuppressMirroredDimension(existing, candidate)
                        || !IsSameMeasuredDimension(existing, candidate, horizontal))
                    {
                        continue;
                    }

                    var remove = CompareDuplicatePreference(candidate, existing) > 0 ? existing : candidate;
                    plan.Dimensions.Remove(remove);
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
            var dims = plan.Dimensions.Where(d => d.Side == side).ToList();
            for (int i = 0; i < dims.Count; i++)
            {
                var bestIndex = i;
                for (int j = i + 1; j < dims.Count; j++)
                {
                    if (!IsSameMeasuredDimension(dims[i], dims[j], horizontal))
                    {
                        continue;
                    }

                    if (CompareDuplicatePreference(dims[j], dims[bestIndex]) > 0)
                    {
                        bestIndex = j;
                    }
                }

                if (bestIndex != i)
                {
                    var best = dims[bestIndex];
                    dims[bestIndex] = dims[i];
                    dims[i] = best;
                }

                for (int j = dims.Count - 1; j > i; j--)
                {
                    if (IsSameMeasuredDimension(dims[i], dims[j], horizontal))
                    {
                        plan.Dimensions.Remove(dims[j]);
                        dims.RemoveAt(j);
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

            var aInterval = ComputeArrowInterval(a, horizontal);
            var bInterval = ComputeArrowInterval(b, horizontal);
            return Math.Abs(aInterval.Item1 - bInterval.Item1) <= _config.GeometryTolerance
                && Math.Abs(aInterval.Item2 - bInterval.Item2) <= _config.GeometryTolerance
                && Math.Abs(GetDimensionSpan(a, horizontal) - GetDimensionSpan(b, horizontal)) <= _config.GeometryTolerance;
        }

        private static Tuple<double, double> ComputeArrowInterval(PlannedDimension dim, bool horizontal)
        {
            if (horizontal)
            {
                return Tuple.Create(
                    Math.Min(dim.FirstPoint.X, dim.SecondPoint.X),
                    Math.Max(dim.FirstPoint.X, dim.SecondPoint.X));
            }

            return Tuple.Create(
                Math.Min(dim.FirstPoint.Y, dim.SecondPoint.Y),
                Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y));
        }

        private static double GetDimensionSpan(PlannedDimension dim, bool horizontal)
        {
            return horizontal
                ? Math.Abs(dim.SecondPoint.X - dim.FirstPoint.X)
                : Math.Abs(dim.SecondPoint.Y - dim.FirstPoint.Y);
        }

        private int CompareDuplicatePreference(PlannedDimension a, PlannedDimension b)
        {
            var toleranceA = TryExtractTolerance(a.OverrideText);
            var toleranceB = TryExtractTolerance(b.OverrideText);
            if (toleranceA.HasValue && !toleranceB.HasValue)
            {
                return 1;
            }

            if (!toleranceA.HasValue && toleranceB.HasValue)
            {
                return -1;
            }

            if (toleranceA.HasValue && toleranceB.HasValue)
            {
                var toleranceCompare = toleranceB.Value.CompareTo(toleranceA.Value);
                if (toleranceCompare != 0)
                {
                    return toleranceCompare;
                }
            }

            var typeCompare = GetDimensionPreferenceRank(b).CompareTo(GetDimensionPreferenceRank(a));
            if (typeCompare != 0)
            {
                return typeCompare;
            }

            if (a.ForceOuterLevel != b.ForceOuterLevel)
            {
                return a.ForceOuterLevel ? 1 : -1;
            }

            var hasTextA = !string.IsNullOrWhiteSpace(a.OverrideText);
            var hasTextB = !string.IsNullOrWhiteSpace(b.OverrideText);
            if (hasTextA != hasTextB)
            {
                return hasTextA ? 1 : -1;
            }

            return 0;
        }

        private static int GetDimensionPreferenceRank(PlannedDimension dim)
        {
            switch (dim.Kind)
            {
                case DimensionKind.PinDistance:
                    return 0;
                case DimensionKind.PinGroupDistance:
                    return 1;
                case DimensionKind.DatumHoleLocationX:
                case DimensionKind.DatumHoleLocationY:
                    return 2;
                case DimensionKind.OverallWidth:
                case DimensionKind.OverallHeight:
                    return 3;
                case DimensionKind.HoleLocation:
                case DimensionKind.Normal:
                    return 4;
                default:
                    return 5;
            }
        }

        private static double? TryExtractTolerance(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var index = text.IndexOf('\u00B1');
            if (index < 0)
            {
                index = text.IndexOf('\u5364');
            }

            if (index < 0 || index >= text.Length - 1)
            {
                return null;
            }

            var start = index + 1;
            while (start < text.Length && char.IsWhiteSpace(text[start]))
            {
                start++;
            }

            var end = start;
            while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.'))
            {
                end++;
            }

            double value;
            if (end <= start || !double.TryParse(text.Substring(start, end - start), out value))
            {
                return null;
            }

            return value;
        }

        private void AddNonPinHolesFromOutlineEdge(
            DimensionPlan plan,
            OutlineFeature2D outline,
            Datum2D datum,
            IList<HoleFeature2D> holes)
        {
            foreach (var hole in holes.Where(h => !h.IsPinHole && !h.IsSlotPoint))
            {
                AddDimension(plan, DimensionKind.HoleLocation, DimensionOrientation.Horizontal,
                    ChooseHorizontalHoleSide(outline, hole.Center),
                    new Point2D(datum.BaseX, hole.Center.Y),
                    hole.Center,
                    string.Empty,
                    "HoleDatumX");
                AddDimension(plan, DimensionKind.HoleLocation, DimensionOrientation.Vertical,
                    ChooseVerticalHoleSide(outline, hole.Center),
                    new Point2D(hole.Center.X, datum.BaseY),
                    hole.Center,
                    string.Empty,
                    "HoleDatumY");
            }
        }

        private PinGroupPlan CreatePinPairGroup(
            HoleFeature2D seed,
            IList<HoleFeature2D> candidates,
            HoleFeature2D forcedBasePin,
            HoleFeature2D referenceBasePin)
        {
            var pins = new List<HoleFeature2D> { seed };
            var pairedPin = candidates
                .Where(h => !IsSameHole(h, seed) && IsSamePinDiameter(h, seed))
                .OrderBy(h => DistanceSquared(h.Center, seed.Center))
                .ThenBy(h => h.Center.X)
                .ThenBy(h => h.Center.Y)
                .FirstOrDefault();
            if (pairedPin != null)
            {
                pins.Add(pairedPin);
            }

            var basePin = ChoosePinGroupBasePin(pins, forcedBasePin, referenceBasePin, seed);
            var group = new PinGroupPlan { BasePin = basePin };
            foreach (var pin in pins.OrderBy(h => DistanceSquared(h.Center, basePin.Center)))
            {
                group.Pins.Add(pin);
                group.MemberHoles.Add(pin);
            }

            return group;
        }

        private HoleFeature2D ChoosePinGroupBasePin(
            IList<HoleFeature2D> groupPins,
            HoleFeature2D forcedBasePin,
            HoleFeature2D referenceBasePin,
            HoleFeature2D fallbackPin)
        {
            if (forcedBasePin != null)
            {
                var matchedForced = groupPins.FirstOrDefault(h => IsSameHole(h, forcedBasePin));
                return matchedForced ?? forcedBasePin;
            }

            if (referenceBasePin != null && groupPins.Count > 0)
            {
                return groupPins
                    .OrderBy(h => DistanceSquared(h.Center, referenceBasePin.Center))
                    .ThenBy(h => Math.Abs(h.Center.X - referenceBasePin.Center.X))
                    .ThenBy(h => Math.Abs(h.Center.Y - referenceBasePin.Center.Y))
                    .ThenBy(h => h.Center.X)
                    .ThenBy(h => h.Center.Y)
                    .First();
            }

            return groupPins
                .OrderBy(h => h.Center.X)
                .ThenBy(h => h.Center.Y)
                .FirstOrDefault() ?? fallbackPin;
        }

        private void AssignNonPinHolesToNearestPinPair(IList<HoleFeature2D> holes, IList<PinGroupPlan> groups)
        {
            if (groups.Count == 0)
            {
                return;
            }

            foreach (var hole in holes.Where(h => !h.IsPinHole && !h.IsSlotPoint))
            {
                var group = groups
                    .OrderBy(g => DistanceToPinPair(hole, g))
                    .ThenBy(g => DistanceSquared(hole.Center, g.BasePin.Center))
                    .FirstOrDefault();
                if (group != null)
                {
                    group.MemberHoles.Add(hole);
                }
            }
        }

        private void AddFirstPinGroupBaseLocation(DimensionPlan plan, Datum2D datum, PinGroupPlan group)
        {
            var basePin = group.BasePin;
            var xRef = datum.DatumHoleLocationBaseX ?? datum.BaseX;
            var yRef = datum.DatumHoleLocationBaseY ?? datum.BaseY;
            var xToleranceText = ShouldUseDatumHoleLocationTolerance(datum, true)
                ? _config.DatumHoleLocationToleranceText ?? string.Empty
                : string.Empty;
            var yToleranceText = ShouldUseDatumHoleLocationTolerance(datum, false)
                ? _config.DatumHoleLocationToleranceText ?? string.Empty
                : string.Empty;

            AddDimension(plan, DimensionKind.DatumHoleLocationX, DimensionOrientation.Horizontal,
                group.HorizontalSide,
                new Point2D(xRef, basePin.Center.Y),
                basePin.Center,
                xToleranceText,
                "DatumX",
                GetPinGroupDebugOwner(group));
            AddDimension(plan, DimensionKind.DatumHoleLocationY, DimensionOrientation.Vertical,
                group.VerticalSide,
                new Point2D(basePin.Center.X, yRef),
                basePin.Center,
                yToleranceText,
                "DatumY",
                GetPinGroupDebugOwner(group));
        }

        private void AddPinGroupBaseTransfers(DimensionPlan plan, IList<PinGroupPlan> groups)
        {
            if (groups.Count < 2)
            {
                return;
            }

            var firstBase = groups[0].BasePin;
            for (int i = 1; i < groups.Count; i++)
            {
                var currentBase = groups[i].BasePin;
                var dx = Math.Abs(currentBase.Center.X - firstBase.Center.X);
                var dy = Math.Abs(currentBase.Center.Y - firstBase.Center.Y);

                if (dx > _config.GeometryTolerance)
                {
                    AddDimension(plan, DimensionKind.PinGroupDistance, DimensionOrientation.Horizontal,
                        groups[i].HorizontalSide,
                        firstBase.Center,
                        currentBase.Center,
                        _config.FormatPinGroupDistanceOverride(dx),
                        "PinGroupDistance",
                        GetPinGroupDebugOwner(groups[i]));
                }

                if (dy > _config.GeometryTolerance)
                {
                    AddDimension(plan, DimensionKind.PinGroupDistance, DimensionOrientation.Vertical,
                        groups[i].VerticalSide,
                        firstBase.Center,
                        currentBase.Center,
                        _config.FormatPinGroupDistanceOverride(dy),
                        "PinGroupDistance",
                        GetPinGroupDebugOwner(groups[i]));
                }
            }
        }

        private void AddSameGroupPinDistances(DimensionPlan plan, OutlineFeature2D outline, IList<PinGroupPlan> groups)
        {
            foreach (var group in groups)
            {
                foreach (var pin in group.Pins)
                {
                    if (IsSameHole(pin, group.BasePin))
                    {
                        continue;
                    }

                    var dx = Math.Abs(pin.Center.X - group.BasePin.Center.X);
                    var dy = Math.Abs(pin.Center.Y - group.BasePin.Center.Y);
                    var midpoint = Midpoint(group.BasePin.Center, pin.Center);
                    if (dx > _config.GeometryTolerance)
                    {
                        AddDimension(plan, DimensionKind.PinDistance, DimensionOrientation.Horizontal,
                            ChooseHorizontalHoleSide(outline, midpoint),
                            group.BasePin.Center,
                            pin.Center,
                            _config.FormatPinCenterDistanceOverride(dx),
                            "PinDistance",
                            GetPinGroupDebugOwner(group));
                    }

                    if (dy > _config.GeometryTolerance)
                    {
                        AddDimension(plan, DimensionKind.PinDistance, DimensionOrientation.Vertical,
                            ChooseVerticalHoleSide(outline, midpoint),
                            group.BasePin.Center,
                            pin.Center,
                            _config.FormatPinCenterDistanceOverride(dy),
                            "PinDistance",
                            GetPinGroupDebugOwner(group));
                    }
                }
            }
        }

        private void AddNonPinHoleLocationsFromPinGroups(
            DimensionPlan plan,
            OutlineFeature2D outline,
            IList<HoleFeature2D> holes,
            IList<PinGroupPlan> pinGroups)
        {
            var functionalGroups = BuildFunctionalHoleGroups(pinGroups);
            var groupedHoles = functionalGroups
                .SelectMany(g => g.Holes)
                .ToList();

            AddFunctionalHoleGroupLocations(plan, functionalGroups);
            AddLooseNonPinHoleLocations(plan, outline, holes, pinGroups, groupedHoles);
        }

        private List<FunctionalHoleGroupPlan> BuildFunctionalHoleGroups(IList<PinGroupPlan> pinGroups)
        {
            var result = new List<FunctionalHoleGroupPlan>();
            foreach (var pinGroup in pinGroups.Where(g => g.Pins.Count == 2 && g.BasePin != null))
            {
                var candidates = pinGroup.MemberHoles
                    .Where(h => h != null && !h.IsPinHole && !h.IsSlotPoint)
                    .OrderBy(h => DistanceToPinPair(h, pinGroup))
                    .ThenBy(h => h.Center.X)
                    .ThenBy(h => h.Center.Y)
                    .ToList();

                var used = new List<HoleFeature2D>();
                foreach (var functionalGroup in FindFunctionalHoleGroupsForPinPair(pinGroup, candidates, used))
                {
                    result.Add(functionalGroup);
                }
            }

            return result;
        }

        private IEnumerable<FunctionalHoleGroupPlan> FindFunctionalHoleGroupsForPinPair(
            PinGroupPlan pinGroup,
            IList<HoleFeature2D> candidates,
            IList<HoleFeature2D> used)
        {
            foreach (var attachedCount in new[] { 4, 2 })
            {
                var available = candidates
                    .Where(h => !ContainsHole(used, h))
                    .Take(10)
                    .ToList();
                if (available.Count < attachedCount)
                {
                    continue;
                }

                FunctionalHoleGroupPlan bestGroup = null;
                var bestScore = double.MaxValue;
                foreach (var subset in EnumerateHoleCombinations(available, attachedCount))
                {
                    if (!IsValidFunctionalHoleAttachmentSet(subset))
                    {
                        continue;
                    }

                    var allHoles = pinGroup.Pins.Concat(subset).ToList();
                    if (!FitsFunctionalHoleGrid(allHoles))
                    {
                        continue;
                    }

                    var score = allHoles.Sum(h => DistanceToPinPair(h, pinGroup));
                    if (score >= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    bestGroup = new FunctionalHoleGroupPlan { PinGroup = pinGroup };
                    foreach (var hole in subset)
                    {
                        bestGroup.Holes.Add(hole);
                    }
                }

                if (bestGroup == null)
                {
                    continue;
                }

                foreach (var hole in bestGroup.Holes)
                {
                    used.Add(hole);
                }

                yield return bestGroup;
            }
        }

        private IEnumerable<List<HoleFeature2D>> EnumerateHoleCombinations(IList<HoleFeature2D> holes, int count)
        {
            var selected = new List<HoleFeature2D>();
            foreach (var combination in EnumerateHoleCombinations(holes, count, 0, selected))
            {
                yield return combination;
            }
        }

        private IEnumerable<List<HoleFeature2D>> EnumerateHoleCombinations(
            IList<HoleFeature2D> holes,
            int count,
            int start,
            List<HoleFeature2D> selected)
        {
            if (selected.Count == count)
            {
                yield return selected.ToList();
                yield break;
            }

            for (int i = start; i <= holes.Count - (count - selected.Count); i++)
            {
                selected.Add(holes[i]);
                foreach (var combination in EnumerateHoleCombinations(holes, count, i + 1, selected))
                {
                    yield return combination;
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

            return holes
                .GroupBy(GetFunctionalHoleSpecKey)
                .All(g => g.Count() == 2 || g.Count() == 4);
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

            return hole.Kind + ":" + _config.FormatNumber(hole.Diameter);
        }

        private bool FitsFunctionalHoleGrid(IList<HoleFeature2D> holes)
        {
            var total = holes.Count;
            var allowed = total == 4
                ? new[] { Tuple.Create(1, 4), Tuple.Create(4, 1), Tuple.Create(2, 2) }
                : total == 6
                    ? new[] { Tuple.Create(1, 6), Tuple.Create(6, 1), Tuple.Create(2, 3), Tuple.Create(3, 2) }
                    : new Tuple<int, int>[0];

            return allowed.Any(shape => FitsGridShape(holes, shape.Item1, shape.Item2));
        }

        private bool FitsGridShape(IList<HoleFeature2D> holes, int rowCount, int columnCount)
        {
            var rowClusters = ClusterCoordinates(holes.Select(h => h.Center.Y));
            var columnClusters = ClusterCoordinates(holes.Select(h => h.Center.X));
            if (rowClusters.Count != rowCount || columnClusters.Count != columnCount)
            {
                return false;
            }

            var occupied = new HashSet<string>();
            foreach (var hole in holes)
            {
                var row = FindClusterIndex(rowClusters, hole.Center.Y);
                var column = FindClusterIndex(columnClusters, hole.Center.X);
                if (row < 0 || column < 0)
                {
                    return false;
                }

                if (!occupied.Add(row.ToString(CultureInfo.InvariantCulture) + ":" + column.ToString(CultureInfo.InvariantCulture)))
                {
                    return false;
                }
            }

            return occupied.Count == rowCount * columnCount
                && HasContinuousGridSpacing(rowClusters)
                && HasContinuousGridSpacing(columnClusters);
        }

        private List<double> ClusterCoordinates(IEnumerable<double> coordinates)
        {
            var tolerance = GetFunctionalHoleAlignmentTolerance();
            var clusters = new List<List<double>>();
            foreach (var coordinate in coordinates.OrderBy(v => v))
            {
                var cluster = clusters.FirstOrDefault(c => Math.Abs(c.Average() - coordinate) <= tolerance);
                if (cluster == null)
                {
                    cluster = new List<double>();
                    clusters.Add(cluster);
                }

                cluster.Add(coordinate);
            }

            return clusters.Select(c => c.Average()).OrderBy(v => v).ToList();
        }

        private int FindClusterIndex(IList<double> clusters, double coordinate)
        {
            var tolerance = GetFunctionalHoleAlignmentTolerance();
            for (int i = 0; i < clusters.Count; i++)
            {
                if (Math.Abs(clusters[i] - coordinate) <= tolerance)
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

            var gaps = new List<double>();
            for (int i = 1; i < clusters.Count; i++)
            {
                var gap = clusters[i] - clusters[i - 1];
                if (gap <= _config.GeometryTolerance)
                {
                    return false;
                }

                gaps.Add(gap);
            }

            return gaps.Max() <= gaps.Min() * 2.5;
        }

        private void AddFunctionalHoleGroupLocations(DimensionPlan plan, IList<FunctionalHoleGroupPlan> functionalGroups)
        {
            foreach (var functionalGroup in functionalGroups)
            {
                var pinGroup = functionalGroup.PinGroup;
                var reference = pinGroup == null ? null : pinGroup.BasePin;
                if (reference == null)
                {
                    continue;
                }

                foreach (var hole in functionalGroup.Holes)
                {
                    AddDimension(plan, DimensionKind.HoleLocation, DimensionOrientation.Horizontal,
                        pinGroup.HorizontalSide,
                        reference.Center,
                        hole.Center,
                        string.Empty,
                        "FunctionalHole",
                        GetPinGroupDebugOwner(pinGroup));
                    AddDimension(plan, DimensionKind.HoleLocation, DimensionOrientation.Vertical,
                        pinGroup.VerticalSide,
                        reference.Center,
                        hole.Center,
                        string.Empty,
                        "FunctionalHole",
                        GetPinGroupDebugOwner(pinGroup));
                }
            }
        }

        private void AddLooseNonPinHoleLocations(
            DimensionPlan plan,
            OutlineFeature2D outline,
            IList<HoleFeature2D> holes,
            IList<PinGroupPlan> pinGroups,
            IList<HoleFeature2D> groupedHoles)
        {
            var loose = holes
                .Where(h => h != null && !h.IsPinHole && !h.IsSlotPoint)
                .Where(h => !ContainsHole(groupedHoles, h))
                .ToList();
            if (loose.Count == 0 || pinGroups.Count == 0)
            {
                return;
            }

            var lineGroups = BuildLooseHoleLineGroups(loose);
            var macroGroups = BuildLooseHoleMacroGroups(loose, lineGroups);
            var plans = macroGroups
                .Select(macro => BuildLooseHoleLocationPlan(macro, pinGroups))
                .Where(p => p.ReferencePinGroup != null && p.ReferencePinGroup.BasePin != null && p.AnchorHole != null)
                .OrderBy(p => DistanceSquared(p.ReferencePinGroup.BasePin.Center, p.AnchorHole.Center))
                .ThenBy(p => p.AnchorHole.Center.X)
                .ThenBy(p => p.AnchorHole.Center.Y)
                .ToList();

            foreach (var loosePlan in plans)
            {
                AddLooseHoleMacroGroup(plan, outline, loosePlan);
            }
        }

        private List<LooseHoleLineGroup> BuildLooseHoleLineGroups(IList<HoleFeature2D> holes)
        {
            var result = new List<LooseHoleLineGroup>();
            foreach (var specGroup in GroupLooseHolesBySpec(holes))
            {
                foreach (var horizontal in new[] { true, false })
                {
                    foreach (var coordinateGroup in GroupLooseHolesByCoordinate(specGroup, horizontal))
                    {
                        foreach (var chain in SplitLooseCoordinateGroupIntoChains(coordinateGroup, horizontal))
                        {
                            if (chain.Count < 2)
                            {
                                continue;
                            }

                            var lineGroup = new LooseHoleLineGroup
                            {
                                Horizontal = horizontal,
                                SpecKey = GetLooseHoleSpecKey(chain[0])
                            };
                            foreach (var hole in chain)
                            {
                                lineGroup.Holes.Add(hole);
                            }

                            result.Add(lineGroup);
                        }
                    }
                }
            }

            return result;
        }

        private IEnumerable<List<HoleFeature2D>> GroupLooseHolesBySpec(IList<HoleFeature2D> holes)
        {
            var groups = new List<List<HoleFeature2D>>();
            foreach (var hole in holes.OrderBy(GetLooseHoleSpecKey).ThenBy(h => h.Center.X).ThenBy(h => h.Center.Y))
            {
                var group = groups.FirstOrDefault(g => AreSameLooseHoleSpec(g[0], hole));
                if (group == null)
                {
                    group = new List<HoleFeature2D>();
                    groups.Add(group);
                }

                group.Add(hole);
            }

            return groups;
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

            return hole.Kind + ":" + _config.FormatNumber(hole.Diameter);
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
            var tolerance = GetFunctionalHoleAlignmentTolerance();
            var groups = new List<List<HoleFeature2D>>();
            foreach (var hole in holes.OrderBy(h => horizontal ? h.Center.Y : h.Center.X))
            {
                var coordinate = horizontal ? hole.Center.Y : hole.Center.X;
                var group = groups.FirstOrDefault(g => Math.Abs(g.Average(h => horizontal ? h.Center.Y : h.Center.X) - coordinate) <= tolerance);
                if (group == null)
                {
                    group = new List<HoleFeature2D>();
                    groups.Add(group);
                }

                group.Add(hole);
            }

            return groups
                .Where(g => g.Count >= 2)
                .OrderBy(g => g.Average(h => horizontal ? h.Center.Y : h.Center.X))
                .Select(g => g
                    .OrderBy(h => horizontal ? h.Center.X : h.Center.Y)
                    .ThenBy(h => horizontal ? h.Center.Y : h.Center.X)
                    .ToList());
        }

        private IEnumerable<List<HoleFeature2D>> SplitLooseCoordinateGroupIntoChains(IList<HoleFeature2D> holes, bool horizontal)
        {
            var ordered = holes
                .OrderBy(h => horizontal ? h.Center.X : h.Center.Y)
                .ThenBy(h => horizontal ? h.Center.Y : h.Center.X)
                .ToList();
            if (ordered.Count <= 2 || HasContinuousHoleSpacing(ordered, horizontal))
            {
                yield return ordered;
                yield break;
            }

            var gaps = GetLooseHoleAxisGaps(ordered, horizontal);
            if (gaps.Count == 0)
            {
                yield return ordered;
                yield break;
            }

            var breakGap = gaps.Min() * 2.5;
            var current = new List<HoleFeature2D> { ordered[0] };
            for (int i = 1; i < ordered.Count; i++)
            {
                var gap = GetAxisDistance(ordered[i - 1].Center, ordered[i].Center, horizontal);
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

            var ordered = holes.OrderBy(h => horizontal ? h.Center.X : h.Center.Y).ToList();
            var gaps = new List<double>();
            for (int i = 1; i < ordered.Count; i++)
            {
                var gap = Math.Abs((horizontal ? ordered[i].Center.X : ordered[i].Center.Y)
                    - (horizontal ? ordered[i - 1].Center.X : ordered[i - 1].Center.Y));
                if (gap <= _config.GeometryTolerance)
                {
                    return false;
                }

                gaps.Add(gap);
            }

            return gaps.Max() <= gaps.Min() * 2.5;
        }

        private List<double> GetLooseHoleAxisGaps(IList<HoleFeature2D> ordered, bool horizontal)
        {
            var gaps = new List<double>();
            for (int i = 1; i < ordered.Count; i++)
            {
                var gap = GetAxisDistance(ordered[i - 1].Center, ordered[i].Center, horizontal);
                if (gap > _config.GeometryTolerance)
                {
                    gaps.Add(gap);
                }
            }

            return gaps;
        }

        private double GetAxisDistance(Point2D a, Point2D b, bool horizontal)
        {
            return Math.Abs((horizontal ? b.X : b.Y) - (horizontal ? a.X : a.Y));
        }

        private List<LooseHoleMacroGroup> BuildLooseHoleMacroGroups(IList<HoleFeature2D> holes, IList<LooseHoleLineGroup> lineGroups)
        {
            var seeds = new List<LooseHoleMacroGroup>();
            foreach (var lineGroup in lineGroups)
            {
                var seed = new LooseHoleMacroGroup();
                seed.LineGroups.Add(lineGroup);
                AddUniqueHoles(seed.Holes, lineGroup.Holes);
                seeds.Add(seed);
            }

            foreach (var hole in holes)
            {
                if (seeds.Any(existingSeed => ContainsHole(existingSeed.Holes, hole)))
                {
                    continue;
                }

                var singleSeed = new LooseHoleMacroGroup();
                singleSeed.Holes.Add(hole);
                seeds.Add(singleSeed);
            }

            var threshold = GetLooseMacroGroupDistanceThreshold(holes);
            var merged = true;
            while (merged)
            {
                merged = false;
                for (int i = 0; i < seeds.Count && !merged; i++)
                {
                    for (int j = i + 1; j < seeds.Count; j++)
                    {
                        if (GetMacroGroupDistance(seeds[i], seeds[j]) > threshold)
                        {
                            continue;
                        }

                        AddUniqueHoles(seeds[i].Holes, seeds[j].Holes);
                        foreach (var lineGroup in seeds[j].LineGroups)
                        {
                            if (!seeds[i].LineGroups.Contains(lineGroup))
                            {
                                seeds[i].LineGroups.Add(lineGroup);
                            }
                        }

                        seeds.RemoveAt(j);
                        merged = true;
                        break;
                    }
                }
            }

            return seeds
                .Where(seed => seed.Holes.Count > 0)
                .OrderBy(seed => seed.Holes.Average(h => h.Center.X))
                .ThenBy(seed => seed.Holes.Average(h => h.Center.Y))
                .ToList();
        }

        private double GetLooseMacroGroupDistanceThreshold(IList<HoleFeature2D> holes)
        {
            var spacings = new List<double>();
            foreach (var horizontal in new[] { true, false })
            {
                foreach (var coordinateGroup in GroupLooseHolesByCoordinate(holes, horizontal))
                {
                    var ordered = coordinateGroup
                        .OrderBy(h => horizontal ? h.Center.X : h.Center.Y)
                        .ToList();
                    spacings.AddRange(GetLooseHoleAxisGaps(ordered, horizontal));
                }
            }

            var medianSpacing = spacings.Count == 0
                ? 0.0
                : spacings.OrderBy(v => v).ElementAt(spacings.Count / 2);
            return Math.Max(medianSpacing * 2.5, _config.TextHeight * 6.0);
        }

        private double GetMacroGroupDistance(LooseHoleMacroGroup a, LooseHoleMacroGroup b)
        {
            if (a == null || b == null || a.Holes.Count == 0 || b.Holes.Count == 0)
            {
                return double.MaxValue;
            }

            var aMinX = a.Holes.Min(h => h.Center.X);
            var aMaxX = a.Holes.Max(h => h.Center.X);
            var aMinY = a.Holes.Min(h => h.Center.Y);
            var aMaxY = a.Holes.Max(h => h.Center.Y);
            var bMinX = b.Holes.Min(h => h.Center.X);
            var bMaxX = b.Holes.Max(h => h.Center.X);
            var bMinY = b.Holes.Min(h => h.Center.Y);
            var bMaxY = b.Holes.Max(h => h.Center.Y);

            var dx = Math.Max(0.0, Math.Max(bMinX - aMaxX, aMinX - bMaxX));
            var dy = Math.Max(0.0, Math.Max(bMinY - aMaxY, aMinY - bMaxY));
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private LooseHoleLocationPlan BuildLooseHoleLocationPlan(LooseHoleMacroGroup macroGroup, IList<PinGroupPlan> pinGroups)
        {
            var plan = new LooseHoleLocationPlan { MacroGroup = macroGroup };
            var bestMutualDistance = double.MaxValue;
            foreach (var pinGroup in pinGroups.Where(g => g != null && g.BasePin != null))
            {
                var nearestHole = macroGroup.Holes
                    .OrderBy(h => DistanceSquared(h.Center, pinGroup.BasePin.Center))
                    .ThenBy(h => h.Center.X)
                    .ThenBy(h => h.Center.Y)
                    .FirstOrDefault();
                if (nearestHole == null)
                {
                    continue;
                }

                var nearestPinGroup = pinGroups
                    .Where(g => g != null && g.BasePin != null)
                    .OrderBy(g => DistanceSquared(nearestHole.Center, g.BasePin.Center))
                    .ThenBy(g => g.BasePin.Center.X)
                    .ThenBy(g => g.BasePin.Center.Y)
                    .FirstOrDefault();
                if (nearestPinGroup != pinGroup)
                {
                    continue;
                }

                var distance = DistanceSquared(nearestHole.Center, pinGroup.BasePin.Center);
                if (distance >= bestMutualDistance)
                {
                    continue;
                }

                bestMutualDistance = distance;
                plan.ReferencePinGroup = pinGroup;
                plan.AnchorHole = nearestHole;
            }

            if (plan.ReferencePinGroup != null)
            {
                return plan;
            }

            var fallback = pinGroups
                .Where(g => g != null && g.BasePin != null)
                .SelectMany(g => macroGroup.Holes.Select(h => new { PinGroup = g, Hole = h, Distance = DistanceSquared(g.BasePin.Center, h.Center) }))
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Hole.Center.X)
                .ThenBy(x => x.Hole.Center.Y)
                .FirstOrDefault();
            if (fallback != null)
            {
                plan.ReferencePinGroup = fallback.PinGroup;
                plan.AnchorHole = fallback.Hole;
            }

            return plan;
        }

        private void AddLooseHoleMacroGroup(DimensionPlan plan, OutlineFeature2D outline, LooseHoleLocationPlan loosePlan)
        {
            var centerEdges = AddLooseHoleCenterDistances(plan, outline, loosePlan.MacroGroup.LineGroups);
            var located = new List<HoleFeature2D> { loosePlan.AnchorHole };
            ExpandLocatedLooseHolesByCenterEdges(located, centerEdges);

            AddLooseHoleLocationPair(
                plan,
                outline,
                loosePlan.ReferencePinGroup,
                loosePlan.ReferencePinGroup.BasePin.Center,
                loosePlan.AnchorHole,
                located,
                forcePinReference: true);

            while (located.Count < loosePlan.MacroGroup.Holes.Count)
            {
                var target = loosePlan.MacroGroup.Holes
                    .Where(h => !ContainsHole(located, h))
                    .OrderBy(h => GetNearestLooseLocationDistance(h, located, loosePlan.ReferencePinGroup.BasePin.Center))
                    .ThenBy(h => h.Center.X)
                    .ThenBy(h => h.Center.Y)
                    .FirstOrDefault();
                if (target == null)
                {
                    break;
                }

                AddLooseHoleLocationPair(
                    plan,
                    outline,
                    loosePlan.ReferencePinGroup,
                    loosePlan.ReferencePinGroup.BasePin.Center,
                    target,
                    located,
                    forcePinReference: false);
                located.Add(target);
                ExpandLocatedLooseHolesByCenterEdges(located, centerEdges);
            }
        }

        private List<Tuple<HoleFeature2D, HoleFeature2D>> AddLooseHoleCenterDistances(
            DimensionPlan plan,
            OutlineFeature2D outline,
            IList<LooseHoleLineGroup> lineGroups)
        {
            var edges = new List<Tuple<HoleFeature2D, HoleFeature2D>>();
            var emitted = new HashSet<string>();
            foreach (var lineGroup in lineGroups)
            {
                var ordered = lineGroup.Holes
                    .OrderBy(h => lineGroup.Horizontal ? h.Center.X : h.Center.Y)
                    .ThenBy(h => lineGroup.Horizontal ? h.Center.Y : h.Center.X)
                    .ToList();
                var chainId = _nextLooseChainId++;
                var side = ChooseLooseDimensionSide(outline, ordered, lineGroup.Horizontal);
                for (int i = 1; i < ordered.Count; i++)
                {
                    var key = GetLooseDimKey(ordered[i - 1], ordered[i], lineGroup.Horizontal);
                    if (!emitted.Add(key))
                    {
                        continue;
                    }

                    AddLooseHoleLocationDimension(plan, ordered[i - 1].Center, ordered[i].Center, lineGroup.Horizontal, side, chainId);
                    edges.Add(Tuple.Create(ordered[i - 1], ordered[i]));
                }
            }

            return edges;
        }

        private void AddLooseHoleLocationPair(
            DimensionPlan plan,
            OutlineFeature2D outline,
            PinGroupPlan referencePinGroup,
            Point2D pinReference,
            HoleFeature2D target,
            IList<HoleFeature2D> located,
            bool forcePinReference)
        {
            var horizontalReference = forcePinReference
                ? pinReference
                : ChooseLooseLocationReference(pinReference, target, located, horizontal: true);
            if (Math.Abs(horizontalReference.X - target.Center.X) > _config.GeometryTolerance)
            {
                var chainId = _nextLooseChainId++;
                AddLooseHoleLocationDimension(
                    plan,
                    horizontalReference,
                    target.Center,
                    horizontal: true,
                    side: ChooseLooseDimensionSide(outline, new[] { target }, horizontal: true),
                    chainId: chainId);
            }

            var verticalReference = forcePinReference
                ? pinReference
                : ChooseLooseLocationReference(pinReference, target, located, horizontal: false);
            if (Math.Abs(verticalReference.Y - target.Center.Y) > _config.GeometryTolerance)
            {
                var chainId = _nextLooseChainId++;
                AddLooseHoleLocationDimension(
                    plan,
                    verticalReference,
                    target.Center,
                    horizontal: false,
                    side: ChooseLooseDimensionSide(outline, new[] { target }, horizontal: false),
                    chainId: chainId);
            }
        }

        private void AddLooseHoleLocationDimension(
            DimensionPlan plan,
            Point2D from,
            Point2D to,
            bool horizontal,
            DimensionSide side,
            int chainId)
        {
            AddDimension(
                plan,
                DimensionKind.HoleLocation,
                horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical,
                side,
                from,
                to,
                string.Empty,
                "LooseHole",
                chainId == 0 ? string.Empty : "L" + chainId.ToString(CultureInfo.InvariantCulture));
        }

        private Point2D ChooseLooseLocationReference(
            Point2D pinReference,
            HoleFeature2D target,
            IList<HoleFeature2D> located,
            bool horizontal)
        {
            var candidates = new List<Point2D> { pinReference };
            candidates.AddRange(located.Where(h => !IsSameHole(h, target)).Select(h => h.Center));
            var sameAxisCandidates = candidates
                .Where(p => horizontal
                    ? Math.Abs(p.Y - target.Center.Y) <= GetFunctionalHoleAlignmentTolerance()
                    : Math.Abs(p.X - target.Center.X) <= GetFunctionalHoleAlignmentTolerance())
                .ToList();
            var usable = sameAxisCandidates.Count > 0 ? sameAxisCandidates : candidates;
            var fitting = usable
                .Where(p => LooseLocationTextFits(p, target.Center, horizontal))
                .ToList();
            if (fitting.Count > 0)
            {
                usable = fitting;
            }

            return usable
                .OrderBy(p => Math.Abs((horizontal ? p.X : p.Y) - (horizontal ? target.Center.X : target.Center.Y)))
                .ThenBy(p => DistanceSquared(p, target.Center))
                .First();
        }

        private bool LooseLocationTextFits(Point2D from, Point2D to, bool horizontal)
        {
            var span = horizontal ? Math.Abs(to.X - from.X) : Math.Abs(to.Y - from.Y);
            if (span <= _config.GeometryTolerance)
            {
                return false;
            }

            var text = _config.FormatNumber(span);
            var textLength = Math.Max(text.Length, 2) * _config.TextHeight * 0.7;
            return textLength <= span - Math.Max(_config.GeometryTolerance, _config.TextHeight * 0.5);
        }

        private double GetNearestLooseLocationDistance(HoleFeature2D target, IList<HoleFeature2D> located, Point2D pinReference)
        {
            var best = DistanceSquared(target.Center, pinReference);
            foreach (var hole in located)
            {
                best = Math.Min(best, DistanceSquared(target.Center, hole.Center));
            }

            return best;
        }

        private void ExpandLocatedLooseHolesByCenterEdges(IList<HoleFeature2D> located, IList<Tuple<HoleFeature2D, HoleFeature2D>> edges)
        {
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var edge in edges)
                {
                    var firstLocated = ContainsHole(located, edge.Item1);
                    var secondLocated = ContainsHole(located, edge.Item2);
                    if (firstLocated && !secondLocated)
                    {
                        located.Add(edge.Item2);
                        changed = true;
                    }
                    else if (secondLocated && !firstLocated)
                    {
                        located.Add(edge.Item1);
                        changed = true;
                    }
                }
            }
        }

        private DimensionSide ChooseLooseDimensionSide(OutlineFeature2D outline, IEnumerable<HoleFeature2D> holes, bool horizontal)
        {
            var points = holes.Select(h => h.Center).ToList();
            if (points.Count == 0)
            {
                return horizontal ? DimensionSide.Bottom : DimensionSide.Left;
            }

            if (horizontal)
            {
                var averageY = points.Average(p => p.Y);
                return Math.Abs(averageY - outline.MinY) <= Math.Abs(outline.MaxY - averageY)
                    ? DimensionSide.Bottom
                    : DimensionSide.Top;
            }

            var averageX = points.Average(p => p.X);
            return Math.Abs(averageX - outline.MinX) <= Math.Abs(outline.MaxX - averageX)
                ? DimensionSide.Left
                : DimensionSide.Right;
        }

        private string GetLooseDimKey(HoleFeature2D a, HoleFeature2D b, bool horizontal)
        {
            var first = GetHolePointKey(a);
            var second = GetHolePointKey(b);
            if (string.CompareOrdinal(first, second) > 0)
            {
                var temp = first;
                first = second;
                second = temp;
            }

            return (horizontal ? "H:" : "V:") + first + ":" + second;
        }

        private string GetHolePointKey(HoleFeature2D hole)
        {
            return _config.FormatNumber(hole.Center.X) + "," + _config.FormatNumber(hole.Center.Y);
        }

        private void AddUniqueHoles(IList<HoleFeature2D> target, IEnumerable<HoleFeature2D> source)
        {
            foreach (var hole in source)
            {
                if (!ContainsHole(target, hole))
                {
                    target.Add(hole);
                }
            }
        }

        private bool ContainsHole(IEnumerable<HoleFeature2D> holes, HoleFeature2D target)
        {
            return holes != null && holes.Any(h => IsSameHole(h, target));
        }

        private void AddOverallWidth(DimensionPlan plan, OutlineFeature2D outline)
        {
            if (outline.Width <= _config.GeometryTolerance)
            {
                return;
            }

            plan.Add(new PlannedDimension
            {
                Kind = DimensionKind.OverallWidth,
                Orientation = DimensionOrientation.Horizontal,
                Side = DimensionSide.Bottom,
                FirstPoint = GetLeftBoundaryPoint(outline),
                SecondPoint = GetRightBoundaryPoint(outline),
                ForceOuterLevel = true,
                DebugRole = "OverallWidth"
            });
        }

        private void AddOverallHeight(DimensionPlan plan, OutlineFeature2D outline)
        {
            if (outline.Height <= _config.GeometryTolerance)
            {
                return;
            }

            plan.Add(new PlannedDimension
            {
                Kind = DimensionKind.OverallHeight,
                Orientation = DimensionOrientation.Vertical,
                Side = DimensionSide.Left,
                FirstPoint = GetBottomBoundaryPoint(outline),
                SecondPoint = GetTopBoundaryPoint(outline),
                ForceOuterLevel = true,
                DebugRole = "OverallHeight"
            });
        }

        private void AddLinearSegmentDimensions(DimensionPlan plan, OutlineFeature2D outline)
        {
            foreach (var segment in outline.Segments)
            {
                if (segment.IsArcChord || IsOverallBoundarySegment(segment, outline) || IsSuppressedByCornerFeature(segment, outline))
                {
                    continue;
                }

                if (segment.IsHorizontal(_config.GeometryTolerance))
                {
                    plan.Add(new PlannedDimension
                    {
                        Kind = DimensionKind.Normal,
                        Orientation = DimensionOrientation.Horizontal,
                        Side = segment.MinY <= outline.MinY + _config.GeometryTolerance ? DimensionSide.Bottom : DimensionSide.Top,
                        FirstPoint = segment.Start,
                        SecondPoint = segment.End,
                        SourceKey = segment.SourceKey,
                        DebugRole = "OutlineSegment"
                    });
                }
                else if (segment.IsVertical(_config.GeometryTolerance))
                {
                    plan.Add(new PlannedDimension
                    {
                        Kind = DimensionKind.Normal,
                        Orientation = DimensionOrientation.Vertical,
                        Side = segment.MinX <= outline.MinX + _config.GeometryTolerance ? DimensionSide.Left : DimensionSide.Right,
                        FirstPoint = segment.Start,
                        SecondPoint = segment.End,
                        SourceKey = segment.SourceKey,
                        DebugRole = "OutlineSegment"
                    });
                }
            }
        }

        private void AddStepOutlineDimensions(DimensionPlan plan, OutlineFeature2D outline)
        {
            AddHorizontalStructureDimensions(
                plan,
                outline,
                BuildTopStructureWidthDimensions(outline));
            AddHorizontalStructureDimensions(
                plan,
                outline,
                BuildBottomStructureWidthDimensions(outline));
            AddVerticalStructureDimensions(
                plan,
                outline,
                BuildLeftStructureHeightDimensions(outline));
            AddVerticalStructureDimensions(
                plan,
                outline,
                BuildRightStructureHeightDimensions(outline));
        }

        private void AddHorizontalStructureDimensions(
            DimensionPlan plan,
            OutlineFeature2D outline,
            IList<PlannedDimension> dimensions)
        {
            foreach (var dimension in dimensions)
            {
                plan.Add(dimension);
            }
        }

        private void AddHorizontalStructureDimensions(
            DimensionPlan plan,
            OutlineFeature2D outline,
            IList<Point2D> points,
            DimensionSide side,
            string debugRole)
        {
            if (points.Count < 2)
            {
                return;
            }

            for (int i = 1; i < points.Count; i++)
            {
                var left = points[i - 1];
                var right = points[i];
                var span = Math.Abs(right.X - left.X);
                if (IsTooSmallStructureSpan(span)
                    || Math.Abs(span - outline.Width) <= _config.GeometryTolerance
                    || IsCrossAxisStructureSpanTooLarge(left, right, horizontal: true))
                {
                    continue;
                }

                AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Horizontal, side, left, right, string.Empty, debugRole);
            }
        }

        private IList<PlannedDimension> BuildTopStructureWidthDimensions(OutlineFeature2D outline)
        {
            var ignoredPoints = new List<IgnoredPoint>();
            List<StructurePoint> structurePoints;
            List<PlannedDimension> candidates;
            while (true)
            {
                structurePoints = BuildTopSideStructurePoints(outline, ignoredPoints);
                var points = structurePoints.Select(p => p.Point).ToList();
                if (points.Count < 2)
                {
                    return new List<PlannedDimension>();
                }

                candidates = BuildHorizontalWidthCandidates(points, DimensionSide.Top, "TopStructWidth");
                var crossingPoints = GetTopExtensionIgnoredPoints(candidates, outline);
                if (crossingPoints.Count == 0)
                {
                    break;
                }

                if (!AddIgnoredPointMetadata(ignoredPoints, crossingPoints))
                {
                    return new List<PlannedDimension>();
                }
            }

            RemoveLongestTopExtensionCandidate(candidates, outline);
            return candidates
                .Where(dim => IsTopSideHorizontalStructureCandidate(dim, outline, ignoredPoints))
                .ToList();
        }

        private List<PlannedDimension> BuildHorizontalWidthCandidates(
            IList<Point2D> points,
            DimensionSide side,
            string debugRole)
        {
            var candidates = new List<PlannedDimension>();
            for (int i = 1; i < points.Count; i++)
            {
                var leftPoint = points[i - 1];
                var rightPoint = points[i];
                var span = Math.Abs(rightPoint.X - leftPoint.X);
                if (span <= _config.GeometryTolerance)
                {
                    continue;
                }

                candidates.Add(new PlannedDimension
                {
                    Kind = DimensionKind.Normal,
                    Orientation = DimensionOrientation.Horizontal,
                    Side = side,
                    FirstPoint = leftPoint,
                    SecondPoint = rightPoint,
                    OverrideText = string.Empty,
                    DebugRole = debugRole ?? string.Empty
                });
            }

            return candidates;
        }

        private void AddVerticalStructureDimensions(
            DimensionPlan plan,
            OutlineFeature2D outline,
            IList<PlannedDimension> dimensions)
        {
            foreach (var dimension in dimensions)
            {
                plan.Add(dimension);
            }
        }

        private void AddVerticalStructureDimensions(
            DimensionPlan plan,
            OutlineFeature2D outline,
            IList<Point2D> points,
            DimensionSide side,
            string debugRole)
        {
            if (points.Count < 2)
            {
                return;
            }

            for (int i = 1; i < points.Count; i++)
            {
                var bottom = points[i - 1];
                var top = points[i];
                var span = Math.Abs(top.Y - bottom.Y);
                if (IsTooSmallStructureSpan(span)
                    || Math.Abs(span - outline.Height) <= _config.GeometryTolerance
                    || IsCrossAxisStructureSpanTooLarge(bottom, top, horizontal: false))
                {
                    continue;
                }

                AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Vertical, side, bottom, top, string.Empty, debugRole);
            }
        }

        private IList<PlannedDimension> BuildLeftStructureHeightDimensions(OutlineFeature2D outline)
        {
            var ignoredPoints = new List<Point2D>();
            List<Point2D> points;
            List<PlannedDimension> candidates;
            while (true)
            {
                points = BuildLeftSideVerticalStructurePoints(outline, ignoredPoints);
                if (points.Count < 2)
                {
                    return new List<PlannedDimension>();
                }

                candidates = BuildVerticalHeightCandidates(points, DimensionSide.Left, "LeftStructHeight");
                var crossingPoints = GetLeftExtensionCrossingPoints(candidates, outline);
                if (crossingPoints.Count == 0)
                {
                    break;
                }

                if (!AddIgnoredPoints(ignoredPoints, crossingPoints))
                {
                    return new List<PlannedDimension>();
                }
            }

            RemoveLongestLeftExtensionCandidate(candidates, outline);
            return candidates;
        }

        private IList<PlannedDimension> BuildRightStructureHeightDimensions(OutlineFeature2D outline)
        {
            var ignoredPoints = new List<Point2D>();
            List<Point2D> points;
            List<PlannedDimension> candidates;
            while (true)
            {
                points = BuildRightSideVerticalStructurePoints(outline, ignoredPoints);
                if (points.Count < 2)
                {
                    return new List<PlannedDimension>();
                }

                candidates = BuildVerticalHeightCandidates(points, DimensionSide.Right, "RightStructHeight");
                var crossingPoints = GetRightExtensionCrossingPoints(candidates, outline);
                if (crossingPoints.Count == 0)
                {
                    break;
                }

                if (!AddIgnoredPoints(ignoredPoints, crossingPoints))
                {
                    return new List<PlannedDimension>();
                }
            }

            RemoveLongestRightExtensionCandidate(candidates, outline);
            return candidates
                .Where(dim => IsRightSideVerticalStructureCandidate(dim, outline, ignoredPoints))
                .ToList();
        }

        private List<PlannedDimension> BuildVerticalHeightCandidates(
            IList<Point2D> points,
            DimensionSide side,
            string debugRole)
        {
            var candidates = new List<PlannedDimension>();
            for (int i = 1; i < points.Count; i++)
            {
                var upperPoint = points[i - 1];
                var lowerPoint = points[i];
                var span = Math.Abs(upperPoint.Y - lowerPoint.Y);
                if (span <= _config.GeometryTolerance)
                {
                    continue;
                }

                candidates.Add(new PlannedDimension
                {
                    Kind = DimensionKind.Normal,
                    Orientation = DimensionOrientation.Vertical,
                    Side = side,
                    FirstPoint = lowerPoint,
                    SecondPoint = upperPoint,
                    OverrideText = string.Empty,
                    DebugRole = debugRole ?? string.Empty
                });
            }

            return candidates;
        }

        private List<StructurePoint> BuildTopSideStructurePoints(OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
        {
            var points = GroupVerticalSegmentsByX(outline)
                .Select(g => GetTopMostStructurePoint(g, ignoredPoints))
                .Where(p => p != null)
                .ToList();

            AddTopSlopeEndpointStructurePoints(points, outline, ignoredPoints);

            return points
                .OrderBy(p => p.Point.X)
                .Select(p => new StructurePoint { Point = p.Point, Source = p.Source })
                .ToList();
        }

        private IList<Point2D> BuildBottomSideHorizontalStructurePoints(OutlineFeature2D outline)
        {
            return GroupVerticalSegmentsByX(outline)
                .Select(GetBottomMostPoint)
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .OrderBy(p => p.X)
                .ThenBy(p => p.Y)
                .ToList();
        }

        private IList<PlannedDimension> BuildBottomStructureWidthDimensions(OutlineFeature2D outline)
        {
            var ignoredPoints = new List<Point2D>();
            List<Point2D> points;
            List<PlannedDimension> candidates;
            while (true)
            {
                points = BuildBottomSideHorizontalStructurePoints(outline, ignoredPoints);
                if (points.Count < 2)
                {
                    return new List<PlannedDimension>();
                }

                candidates = BuildHorizontalWidthCandidates(points, DimensionSide.Bottom, "BottomStructWidth");
                var crossingPoints = GetBottomExtensionCrossingPoints(candidates, outline);
                if (crossingPoints.Count == 0)
                {
                    break;
                }

                if (!AddIgnoredPoints(ignoredPoints, crossingPoints))
                {
                    return new List<PlannedDimension>();
                }
            }

            RemoveLongestBottomExtensionCandidate(candidates, outline);
            return candidates
                .Where(dim => IsBottomSideHorizontalStructureCandidate(dim, outline, ignoredPoints))
                .ToList();
        }

        private List<Point2D> BuildBottomSideHorizontalStructurePoints(OutlineFeature2D outline, IList<Point2D> ignoredPoints)
        {
            var points = GroupVerticalSegmentsByX(outline)
                .Select(g => GetBottomMostPoint(g, ignoredPoints))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            AddBottomInclinedEndpointStructurePoints(points, outline, ignoredPoints);

            return points
                .OrderBy(p => p.X)
                .ToList();
        }

        private IList<Point2D> BuildLeftSideVerticalStructurePoints(OutlineFeature2D outline)
        {
            return GroupHorizontalSegmentsByY(outline)
                .Select(GetLeftMostPoint)
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .OrderBy(p => p.Y)
                .ThenBy(p => p.X)
                .ToList();
        }

        private List<Point2D> BuildLeftSideVerticalStructurePoints(OutlineFeature2D outline, IList<Point2D> ignoredPoints)
        {
            var points = GroupHorizontalSegmentsByY(outline)
                .Select(g => GetLeftMostPoint(g, ignoredPoints))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            AddSideInclinedEndpointStructurePoints(points, outline, ignoredPoints, DimensionSide.Left);

            return points
                .OrderByDescending(p => p.Y)
                .ToList();
        }

        private IList<Point2D> BuildRightSideVerticalStructurePoints(OutlineFeature2D outline)
        {
            return GroupHorizontalSegmentsByY(outline)
                .Select(GetRightMostPoint)
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .OrderBy(p => p.Y)
                .ThenBy(p => p.X)
                .ToList();
        }

        private List<Point2D> BuildRightSideVerticalStructurePoints(OutlineFeature2D outline, IList<Point2D> ignoredPoints)
        {
            var points = GroupHorizontalSegmentsByY(outline)
                .Select(g => GetRightMostPoint(g, ignoredPoints))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            AddSideInclinedEndpointStructurePoints(points, outline, ignoredPoints, DimensionSide.Right);

            return points
                .OrderByDescending(p => p.Y)
                .ToList();
        }

        private IList<IList<Segment2D>> GroupVerticalSegmentsByX(OutlineFeature2D outline)
        {
            var groups = new List<IList<Segment2D>>();
            foreach (var segment in outline.Segments
                .Where(s => !s.IsArcChord)
                .Where(s => s.IsVertical(_config.GeometryTolerance))
                .Where(s => s.LengthY > _config.GeometryTolerance))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(g[0].MinX - segment.MinX) <= _config.GeometryTolerance);
                if (group == null)
                {
                    group = new List<Segment2D>();
                    groups.Add(group);
                }

                group.Add(segment);
            }

            return groups;
        }

        private IList<IList<Segment2D>> GroupHorizontalSegmentsByY(OutlineFeature2D outline)
        {
            var groups = new List<IList<Segment2D>>();
            foreach (var segment in outline.Segments
                .Where(s => !s.IsArcChord)
                .Where(s => s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => s.LengthX > _config.GeometryTolerance))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(g[0].MinY - segment.MinY) <= _config.GeometryTolerance);
                if (group == null)
                {
                    group = new List<Segment2D>();
                    groups.Add(group);
                }

                group.Add(segment);
            }

            return groups;
        }

        private Point2D? GetTopMostPoint(IEnumerable<Segment2D> segments)
        {
            var points = segments.SelectMany(s => new[] { s.Start, s.End }).ToList();
            if (points.Count == 0)
            {
                return null;
            }

            return points.OrderByDescending(p => p.Y).ThenBy(p => p.X).First();
        }

        private StructurePoint GetTopMostStructurePoint(IEnumerable<Segment2D> segments, IList<IgnoredPoint> ignoredPoints)
        {
            var point = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => !ContainsIgnoredPoint(ignoredPoints, p))
                .OrderByDescending(p => p.Y)
                .ThenBy(p => p.X)
                .Select(p => (Point2D?)p)
                .FirstOrDefault();
            if (!point.HasValue)
            {
                return null;
            }

            return new StructurePoint
            {
                Point = point.Value,
                Source = "VerticalTopMost"
            };
        }

        private Point2D? GetBottomMostPoint(IEnumerable<Segment2D> segments)
        {
            var points = segments.SelectMany(s => new[] { s.Start, s.End }).ToList();
            if (points.Count == 0)
            {
                return null;
            }

            return points.OrderBy(p => p.Y).ThenBy(p => p.X).First();
        }

        private Point2D? GetBottomMostPoint(IEnumerable<Segment2D> segments, IList<Point2D> ignoredPoints)
        {
            var point = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => !ContainsPoint(ignoredPoints, p))
                .OrderBy(p => p.Y)
                .ThenBy(p => p.X)
                .Select(p => (Point2D?)p)
                .FirstOrDefault();
            return point;
        }

        private Point2D? GetLeftMostPoint(IEnumerable<Segment2D> segments)
        {
            var points = segments.SelectMany(s => new[] { s.Start, s.End }).ToList();
            if (points.Count == 0)
            {
                return null;
            }

            return points.OrderBy(p => p.X).ThenBy(p => p.Y).First();
        }

        private Point2D? GetLeftMostPoint(IEnumerable<Segment2D> segments, IList<Point2D> ignoredPoints)
        {
            var point = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => !ContainsPoint(ignoredPoints, p))
                .OrderBy(p => p.X)
                .ThenBy(p => p.Y)
                .Select(p => (Point2D?)p)
                .FirstOrDefault();
            return point;
        }

        private Point2D? GetRightMostPoint(IEnumerable<Segment2D> segments)
        {
            var points = segments.SelectMany(s => new[] { s.Start, s.End }).ToList();
            if (points.Count == 0)
            {
                return null;
            }

            return points.OrderByDescending(p => p.X).ThenBy(p => p.Y).First();
        }

        private Point2D? GetRightMostPoint(IEnumerable<Segment2D> segments, IList<Point2D> ignoredPoints)
        {
            var point = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => !ContainsPoint(ignoredPoints, p))
                .OrderByDescending(p => p.X)
                .ThenBy(p => p.Y)
                .Select(p => (Point2D?)p)
                .FirstOrDefault();
            return point;
        }

        private bool IsOverallBoundarySegment(Segment2D segment, OutlineFeature2D outline)
        {
            if (segment.IsHorizontal(_config.GeometryTolerance)
                && Math.Abs(segment.LengthX - outline.Width) <= _config.GeometryTolerance)
            {
                return true;
            }

            return segment.IsVertical(_config.GeometryTolerance)
                && Math.Abs(segment.LengthY - outline.Height) <= _config.GeometryTolerance;
        }

        private bool IsSuppressedByCornerFeature(Segment2D segment, OutlineFeature2D outline)
        {
            foreach (var chamfer in outline.Chamfers)
            {
                if (Touches(segment, chamfer.StartPoint) || Touches(segment, chamfer.EndPoint))
                {
                    return true;
                }
            }

            foreach (var fillet in outline.Fillets)
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
            return Math.Abs(a.X - b.X) <= _config.GeometryTolerance
                && Math.Abs(a.Y - b.Y) <= _config.GeometryTolerance;
        }

        private List<Point2D> GetBottomExtensionCrossingPoints(IList<PlannedDimension> candidates, OutlineFeature2D outline)
        {
            var points = new List<Point2D>();
            foreach (var dim in candidates)
            {
                AddBottomCrossingPoint(points, dim.FirstPoint, outline);
                AddBottomCrossingPoint(points, dim.SecondPoint, outline);
                AddDirectionalInclinedEndpoint(points, dim.FirstPoint, outline, invertDirection: false);
                AddDirectionalInclinedEndpoint(points, dim.SecondPoint, outline, invertDirection: false);
            }

            return points;
        }

        private List<Point2D> GetLeftExtensionCrossingPoints(IList<PlannedDimension> candidates, OutlineFeature2D outline)
        {
            var points = new List<Point2D>();
            foreach (var dim in candidates)
            {
                AddLeftCrossingPoint(points, dim.FirstPoint, outline);
                AddLeftCrossingPoint(points, dim.SecondPoint, outline);
                AddSideDirectionalInclinedEndpoint(points, dim.FirstPoint, outline, DimensionSide.Left);
                AddSideDirectionalInclinedEndpoint(points, dim.SecondPoint, outline, DimensionSide.Left);
            }

            return points;
        }

        private List<Point2D> GetRightExtensionCrossingPoints(IList<PlannedDimension> candidates, OutlineFeature2D outline)
        {
            var points = new List<Point2D>();
            foreach (var dim in candidates)
            {
                AddRightCrossingPoint(points, dim.FirstPoint, outline);
                AddRightCrossingPoint(points, dim.SecondPoint, outline);
                AddSideDirectionalInclinedEndpoint(points, dim.FirstPoint, outline, DimensionSide.Right);
                AddSideDirectionalInclinedEndpoint(points, dim.SecondPoint, outline, DimensionSide.Right);
            }

            return points;
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

        private void AddDirectionalInclinedEndpoint(
            IList<Point2D> points,
            Point2D point,
            OutlineFeature2D outline,
            bool invertDirection)
        {
            if (ContainsPoint(points, point)
                || IsEnvelopeSidePoint(point, outline)
                || string.IsNullOrEmpty(GetDirectionalInclinedIgnoreReason(point, outline, invertDirection)))
            {
                return;
            }

            points.Add(point);
        }

        private void AddSideDirectionalInclinedEndpoint(
            IList<Point2D> points,
            Point2D point,
            OutlineFeature2D outline,
            DimensionSide side)
        {
            if (ContainsPoint(points, point)
                || IsEnvelopeHorizontalSidePoint(point, outline)
                || !ShouldIgnoreSideDirectionalInclinedEndpoint(point, outline, side))
            {
                return;
            }

            points.Add(point);
        }

        private bool AddIgnoredPoints(IList<Point2D> ignoredPoints, IEnumerable<Point2D> crossingPoints)
        {
            var added = false;
            foreach (var point in crossingPoints)
            {
                if (!ContainsPoint(ignoredPoints, point))
                {
                    ignoredPoints.Add(point);
                    added = true;
                }
            }

            return added;
        }

        private void RemoveLongestBottomExtensionCandidate(IList<PlannedDimension> candidates, OutlineFeature2D outline)
        {
            if (candidates.Count <= 1)
            {
                return;
            }

            var removal = candidates
                .Select((dim, index) => new
                {
                    Index = index,
                    ExtensionLength = Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y) - outline.MinY,
                    Span = Math.Abs(dim.SecondPoint.X - dim.FirstPoint.X)
                })
                .OrderByDescending(x => x.ExtensionLength)
                .ThenByDescending(x => x.Span)
                .First();

            candidates.RemoveAt(removal.Index);
        }

        private void RemoveLongestLeftExtensionCandidate(IList<PlannedDimension> candidates, OutlineFeature2D outline)
        {
            if (candidates.Count <= 1)
            {
                return;
            }

            var removal = candidates
                .Select((dim, index) => new
                {
                    Index = index,
                    ExtensionLength = Math.Max(dim.FirstPoint.X, dim.SecondPoint.X) - outline.MinX,
                    Span = Math.Abs(dim.SecondPoint.Y - dim.FirstPoint.Y)
                })
                .OrderByDescending(x => x.ExtensionLength)
                .ThenByDescending(x => x.Span)
                .First();

            candidates.RemoveAt(removal.Index);
        }

        private void RemoveLongestRightExtensionCandidate(IList<PlannedDimension> candidates, OutlineFeature2D outline)
        {
            if (candidates.Count <= 1)
            {
                return;
            }

            var removal = candidates
                .Select((dim, index) => new
                {
                    Index = index,
                    ExtensionLength = outline.MaxX - Math.Min(dim.FirstPoint.X, dim.SecondPoint.X),
                    Span = Math.Abs(dim.SecondPoint.Y - dim.FirstPoint.Y)
                })
                .OrderByDescending(x => x.ExtensionLength)
                .ThenByDescending(x => x.Span)
                .First();

            candidates.RemoveAt(removal.Index);
        }

        private bool IsBottomSideHorizontalStructureCandidate(
            PlannedDimension dim,
            OutlineFeature2D outline,
            IList<Point2D> ignoredPoints)
        {
            return IsCurrentBottomSideStructurePoint(dim.FirstPoint, outline, ignoredPoints)
                && IsCurrentBottomSideStructurePoint(dim.SecondPoint, outline, ignoredPoints);
        }

        private bool IsCurrentBottomSideStructurePoint(Point2D point, OutlineFeature2D outline, IList<Point2D> ignoredPoints)
        {
            if (IsBottomInclinedEndpointStructurePoint(point, outline, ignoredPoints))
            {
                return true;
            }

            var levelSegments = outline.Segments
                .Where(s => s.IsVertical(_config.GeometryTolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthY > _config.GeometryTolerance)
                .Where(s => Math.Abs(s.MinX - point.X) <= _config.GeometryTolerance)
                .ToList();
            if (levelSegments.Count == 0)
            {
                return false;
            }

            var bottomMost = GetBottomMostPoint(levelSegments, ignoredPoints);
            return bottomMost.HasValue && PointsEqual(bottomMost.Value, point);
        }

        private bool IsRightSideVerticalStructureCandidate(
            PlannedDimension dim,
            OutlineFeature2D outline,
            IList<Point2D> ignoredPoints)
        {
            return IsCurrentRightSideStructurePoint(dim.FirstPoint, outline, ignoredPoints)
                && IsCurrentRightSideStructurePoint(dim.SecondPoint, outline, ignoredPoints);
        }

        private bool IsCurrentRightSideStructurePoint(Point2D point, OutlineFeature2D outline, IList<Point2D> ignoredPoints)
        {
            if (IsSideInclinedEndpointStructurePoint(point, outline, ignoredPoints, DimensionSide.Right))
            {
                return true;
            }

            var levelSegments = outline.Segments
                .Where(s => s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthX > _config.GeometryTolerance)
                .Where(s => Math.Abs(s.MinY - point.Y) <= _config.GeometryTolerance)
                .ToList();
            if (levelSegments.Count == 0)
            {
                return false;
            }

            var rightMost = GetRightMostPoint(levelSegments, ignoredPoints);
            return rightMost.HasValue && PointsEqual(rightMost.Value, point);
        }

        private void AddSideInclinedEndpointStructurePoints(
            IList<Point2D> points,
            OutlineFeature2D outline,
            IList<Point2D> ignoredPoints,
            DimensionSide side)
        {
            foreach (var segment in outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsSideInnerGrooveChamferSegment(s, outline, side)))
            {
                AddSideInclinedEndpointStructurePoint(points, segment.Start, outline, ignoredPoints);
                AddSideInclinedEndpointStructurePoint(points, segment.End, outline, ignoredPoints);
            }
        }

        private void AddSideInclinedEndpointStructurePoint(
            IList<Point2D> points,
            Point2D point,
            OutlineFeature2D outline,
            IList<Point2D> ignoredPoints)
        {
            if (ContainsPoint(points, point)
                || ContainsPoint(ignoredPoints, point)
                || IsEnvelopeHorizontalSidePoint(point, outline))
            {
                return;
            }

            points.Add(point);
        }

        private bool IsSideInclinedEndpointStructurePoint(
            Point2D point,
            OutlineFeature2D outline,
            IList<Point2D> ignoredPoints,
            DimensionSide side)
        {
            if (ContainsPoint(ignoredPoints, point) || IsEnvelopeHorizontalSidePoint(point, outline))
            {
                return false;
            }

            return outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsSideInnerGrooveChamferSegment(s, outline, side))
                .Any(s => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
        }

        private void AddBottomInclinedEndpointStructurePoints(
            IList<Point2D> points,
            OutlineFeature2D outline,
            IList<Point2D> ignoredPoints)
        {
            foreach (var segment in outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(s => IsInnerGrooveChamferSegment(s, outline, isTopSide: false)))
            {
                AddBottomInclinedEndpointStructurePoint(points, segment.Start, outline, ignoredPoints);
                AddBottomInclinedEndpointStructurePoint(points, segment.End, outline, ignoredPoints);
            }
        }

        private void AddBottomInclinedEndpointStructurePoint(
            IList<Point2D> points,
            Point2D point,
            OutlineFeature2D outline,
            IList<Point2D> ignoredPoints)
        {
            if (ContainsPoint(points, point)
                || ContainsPoint(ignoredPoints, point)
                || IsEnvelopeSidePoint(point, outline))
            {
                return;
            }

            points.Add(point);
        }

        private bool IsBottomInclinedEndpointStructurePoint(
            Point2D point,
            OutlineFeature2D outline,
            IList<Point2D> ignoredPoints)
        {
            if (ContainsPoint(ignoredPoints, point) || IsEnvelopeSidePoint(point, outline))
            {
                return false;
            }

            return outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(s => IsInnerGrooveChamferSegment(s, outline, isTopSide: false))
                .Any(s => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
        }

        private bool BottomExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline)
        {
            var tolerance = _config.GeometryTolerance;
            var dimY = outline.MinY - _config.FirstDimOffset;
            var maxY = featurePoint.Y - tolerance;
            if (maxY <= dimY + tolerance)
            {
                return false;
            }

            foreach (var segment in outline.Segments)
            {
                double y;
                if (TryGetVerticalIntersectionY(segment, featurePoint.X, out y)
                    && y >= dimY - tolerance
                    && y <= maxY)
                {
                    return true;
                }

                if (VerticalExtensionOverlapsOutlineSegment(segment, featurePoint, dimY, maxY))
                {
                    return true;
                }
            }

            return false;
        }

        private bool LeftExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline)
        {
            var tolerance = _config.GeometryTolerance;
            var dimX = outline.MinX - _config.FirstDimOffset;
            var maxX = featurePoint.X - tolerance;
            if (maxX <= dimX + tolerance)
            {
                return false;
            }

            foreach (var segment in outline.Segments)
            {
                double x;
                if (TryGetHorizontalIntersectionX(segment, featurePoint.Y, out x)
                    && x >= dimX - tolerance
                    && x <= maxX)
                {
                    return true;
                }

                if (HorizontalExtensionOverlapsOutlineSegment(segment, featurePoint, dimX, maxX))
                {
                    return true;
                }
            }

            return false;
        }

        private bool RightExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline)
        {
            var tolerance = _config.GeometryTolerance;
            var minX = featurePoint.X + tolerance;
            var dimX = outline.MaxX + _config.FirstDimOffset;
            if (dimX <= minX + tolerance)
            {
                return false;
            }

            foreach (var segment in outline.Segments)
            {
                double x;
                if (TryGetHorizontalIntersectionX(segment, featurePoint.Y, out x)
                    && x >= minX
                    && x <= dimX + tolerance)
                {
                    return true;
                }

                if (HorizontalExtensionOverlapsOutlineSegment(segment, featurePoint, minX, dimX))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsPoint(IEnumerable<Point2D> points, Point2D point)
        {
            return points.Any(p => PointsEqual(p, point));
        }

        private bool IsFortyFiveDegreeSegment(Segment2D segment)
        {
            var dx = Math.Abs(segment.Start.X - segment.End.X);
            var dy = Math.Abs(segment.Start.Y - segment.End.Y);
            var tolerance = Math.Max(_config.GeometryTolerance, Math.Max(dx, dy) * 0.08);
            return dx > _config.GeometryTolerance
                && dy > _config.GeometryTolerance
                && Math.Abs(dx - dy) <= tolerance;
        }

        private bool IsIgnorableFortyFiveDegreeSegment(Segment2D segment)
        {
            var dx = Math.Abs(segment.Start.X - segment.End.X);
            var dy = Math.Abs(segment.Start.Y - segment.End.Y);
            var tolerance = Math.Max(_config.GeometryTolerance, Math.Max(dx, dy) * 0.02);
            return dx > _config.GeometryTolerance
                && dy > _config.GeometryTolerance
                && Math.Abs(dx - dy) <= tolerance;
        }

        private bool IsInnerGrooveChamferSegment(Segment2D chamfer, OutlineFeature2D outline, bool isTopSide)
        {
            if (!IsFortyFiveDegreeSegment(chamfer))
            {
                return false;
            }

            var tolerance = _config.GeometryTolerance;
            var chamferTopY = Math.Max(chamfer.Start.Y, chamfer.End.Y);
            var chamferBottomY = Math.Min(chamfer.Start.Y, chamfer.End.Y);
            foreach (var horizontal in outline.Segments.Where(s => s.IsHorizontal(tolerance) && !s.IsArcChord))
            {
                if (Math.Abs(horizontal.MinY - outline.MaxY) <= tolerance
                    || Math.Abs(horizontal.MinY - outline.MinY) <= tolerance)
                {
                    continue;
                }

                if (isTopSide && horizontal.MinY >= chamferTopY - tolerance)
                {
                    continue;
                }

                if (!isTopSide && horizontal.MinY <= chamferBottomY + tolerance)
                {
                    continue;
                }

                if (Touches(horizontal, chamfer.Start)
                    && HorizontalOtherEndConnectsInnerGroove(horizontal, chamfer.Start, chamfer, outline))
                {
                    return true;
                }

                if (Touches(horizontal, chamfer.End)
                    && HorizontalOtherEndConnectsInnerGroove(horizontal, chamfer.End, chamfer, outline))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HorizontalOtherEndConnectsInnerGroove(
            Segment2D horizontal,
            Point2D sharedPoint,
            Segment2D currentChamfer,
            OutlineFeature2D outline)
        {
            var otherEnd = PointsEqual(horizontal.Start, sharedPoint) ? horizontal.End : horizontal.Start;
            return outline.Segments.Any(segment =>
                    !ReferenceEquals(segment, currentChamfer)
                    && !segment.IsHorizontal(_config.GeometryTolerance)
                    && !segment.IsVertical(_config.GeometryTolerance)
                    && IsFortyFiveDegreeSegment(segment)
                    && (PointsEqual(segment.Start, otherEnd) || PointsEqual(segment.End, otherEnd)))
                || outline.Chamfers.Any(chamfer =>
                    IsFortyFiveDegreeChamfer(chamfer)
                    && (PointsEqual(chamfer.StartPoint, otherEnd) || PointsEqual(chamfer.EndPoint, otherEnd)))
                || outline.Fillets.Any(fillet =>
                    PointsEqual(fillet.StartPoint, otherEnd) || PointsEqual(fillet.EndPoint, otherEnd));
        }

        private bool IsSideInnerGrooveChamferSegment(Segment2D chamfer, OutlineFeature2D outline, DimensionSide side)
        {
            if (!IsFortyFiveDegreeSegment(chamfer))
            {
                return false;
            }

            var tolerance = _config.GeometryTolerance;
            var chamferLeftX = Math.Min(chamfer.Start.X, chamfer.End.X);
            var chamferRightX = Math.Max(chamfer.Start.X, chamfer.End.X);
            foreach (var vertical in outline.Segments.Where(s => s.IsVertical(tolerance) && !s.IsArcChord))
            {
                if (Math.Abs(vertical.MinX - outline.MinX) <= tolerance
                    || Math.Abs(vertical.MinX - outline.MaxX) <= tolerance)
                {
                    continue;
                }

                if (side == DimensionSide.Left && vertical.MinX <= chamferLeftX + tolerance)
                {
                    continue;
                }

                if (side == DimensionSide.Right && vertical.MinX >= chamferRightX - tolerance)
                {
                    continue;
                }

                if (Touches(vertical, chamfer.Start)
                    && IsInternalSideGrooveVertical(vertical, chamfer, side, outline)
                    && VerticalOtherEndConnectsChamfer(vertical, chamfer.Start, chamfer, outline))
                {
                    return true;
                }

                if (Touches(vertical, chamfer.End)
                    && IsInternalSideGrooveVertical(vertical, chamfer, side, outline)
                    && VerticalOtherEndConnectsChamfer(vertical, chamfer.End, chamfer, outline))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSideInnerGrooveIgnoredEndpoint(
            Point2D point,
            Segment2D chamfer,
            OutlineFeature2D outline,
            DimensionSide side)
        {
            var sharedPoint = GetSideInnerGrooveVerticalSharedPoint(chamfer, outline, side);
            return sharedPoint.HasValue && PointsEqual(point, sharedPoint.Value);
        }

        private Point2D? GetSideInnerGrooveVerticalSharedPoint(
            Segment2D chamfer,
            OutlineFeature2D outline,
            DimensionSide side)
        {
            var tolerance = _config.GeometryTolerance;
            var chamferLeftX = Math.Min(chamfer.Start.X, chamfer.End.X);
            var chamferRightX = Math.Max(chamfer.Start.X, chamfer.End.X);
            foreach (var vertical in outline.Segments.Where(s => s.IsVertical(tolerance) && !s.IsArcChord))
            {
                if (Math.Abs(vertical.MinX - outline.MinX) <= tolerance
                    || Math.Abs(vertical.MinX - outline.MaxX) <= tolerance
                    || (side == DimensionSide.Left && vertical.MinX <= chamferLeftX + tolerance)
                    || (side == DimensionSide.Right && vertical.MinX >= chamferRightX - tolerance))
                {
                    continue;
                }

                if (Touches(vertical, chamfer.Start)
                    && IsInternalSideGrooveVertical(vertical, chamfer, side, outline)
                    && VerticalOtherEndConnectsChamfer(vertical, chamfer.Start, chamfer, outline))
                {
                    return chamfer.Start;
                }

                if (Touches(vertical, chamfer.End)
                    && IsInternalSideGrooveVertical(vertical, chamfer, side, outline)
                    && VerticalOtherEndConnectsChamfer(vertical, chamfer.End, chamfer, outline))
                {
                    return chamfer.End;
                }
            }

            return null;
        }

        private bool IsInternalSideGrooveVertical(
            Segment2D vertical,
            Segment2D chamfer,
            DimensionSide side,
            OutlineFeature2D outline)
        {
            if (!vertical.IsVertical(_config.GeometryTolerance)
                || Math.Abs(vertical.MinX - outline.MinX) <= _config.GeometryTolerance
                || Math.Abs(vertical.MinX - outline.MaxX) <= _config.GeometryTolerance)
            {
                return false;
            }

            var chamferLeftX = Math.Min(chamfer.Start.X, chamfer.End.X);
            var chamferRightX = Math.Max(chamfer.Start.X, chamfer.End.X);
            if (side == DimensionSide.Left)
            {
                return vertical.MinX > chamferLeftX + _config.GeometryTolerance;
            }

            if (side == DimensionSide.Right)
            {
                return vertical.MinX < chamferRightX - _config.GeometryTolerance;
            }

            return false;
        }

        private bool VerticalOtherEndConnectsChamfer(
            Segment2D vertical,
            Point2D sharedPoint,
            Segment2D currentChamfer,
            OutlineFeature2D outline)
        {
            var otherEnd = PointsEqual(vertical.Start, sharedPoint) ? vertical.End : vertical.Start;
            return outline.Segments.Any(segment =>
                    !ReferenceEquals(segment, currentChamfer)
                    && !segment.IsHorizontal(_config.GeometryTolerance)
                    && !segment.IsVertical(_config.GeometryTolerance)
                    && IsFortyFiveDegreeSegment(segment)
                    && (PointsEqual(segment.Start, otherEnd) || PointsEqual(segment.End, otherEnd)))
                || outline.Chamfers.Any(chamfer =>
                    IsFortyFiveDegreeChamfer(chamfer)
                    && (PointsEqual(chamfer.StartPoint, otherEnd) || PointsEqual(chamfer.EndPoint, otherEnd)))
                || outline.Fillets.Any(fillet =>
                    PointsEqual(fillet.StartPoint, otherEnd) || PointsEqual(fillet.EndPoint, otherEnd));
        }

        private bool IsFortyFiveDegreeChamfer(ChamferFeature2D chamfer)
        {
            if (chamfer == null)
            {
                return false;
            }

            var dx = Math.Abs(chamfer.DeltaX);
            var dy = Math.Abs(chamfer.DeltaY);
            if (dx <= _config.GeometryTolerance)
            {
                dx = Math.Abs(chamfer.StartPoint.X - chamfer.EndPoint.X);
                dy = Math.Abs(chamfer.StartPoint.Y - chamfer.EndPoint.Y);
            }

            var tolerance = Math.Max(_config.GeometryTolerance, Math.Max(dx, dy) * 0.08);
            return dx > _config.GeometryTolerance
                && dy > _config.GeometryTolerance
                && Math.Abs(dx - dy) <= tolerance;
        }

        private List<IgnoredPoint> GetTopExtensionIgnoredPoints(IList<PlannedDimension> candidates, OutlineFeature2D outline)
        {
            var points = new List<IgnoredPoint>();
            foreach (var dim in candidates)
            {
                AddTopCrossingIgnoredPoint(points, dim.FirstPoint, outline);
                AddTopCrossingIgnoredPoint(points, dim.SecondPoint, outline);
                AddDirectionalInclinedIgnoredPoint(points, dim.FirstPoint, outline, invertDirection: true);
                AddDirectionalInclinedIgnoredPoint(points, dim.SecondPoint, outline, invertDirection: true);
            }

            return points;
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

        private void AddDirectionalInclinedIgnoredPoint(
            IList<IgnoredPoint> points,
            Point2D point,
            OutlineFeature2D outline,
            bool invertDirection)
        {
            var reason = GetDirectionalInclinedIgnoreReason(point, outline, invertDirection);
            if (ContainsIgnoredPoint(points, point)
                || IsEnvelopeSidePoint(point, outline)
                || string.IsNullOrEmpty(reason))
            {
                return;
            }

            points.Add(new IgnoredPoint
            {
                Point = point,
                Reason = reason
            });
        }

        private bool AddIgnoredPointMetadata(IList<IgnoredPoint> ignoredPoints, IEnumerable<IgnoredPoint> crossingPoints)
        {
            var added = false;
            foreach (var point in crossingPoints)
            {
                if (!ContainsIgnoredPoint(ignoredPoints, point.Point))
                {
                    ignoredPoints.Add(point);
                    added = true;
                }
            }

            return added;
        }

        private void RemoveLongestTopExtensionCandidate(IList<PlannedDimension> candidates, OutlineFeature2D outline)
        {
            if (candidates.Count <= 1)
            {
                return;
            }

            var removal = candidates
                .Select((dim, index) => new
                {
                    Index = index,
                    ExtensionLength = outline.MaxY - Math.Min(dim.FirstPoint.Y, dim.SecondPoint.Y),
                    Span = Math.Abs(dim.SecondPoint.X - dim.FirstPoint.X)
                })
                .OrderByDescending(x => x.ExtensionLength)
                .ThenByDescending(x => x.Span)
                .First();

            candidates.RemoveAt(removal.Index);
        }

        private bool IsTopSideHorizontalStructureCandidate(
            PlannedDimension dim,
            OutlineFeature2D outline,
            IList<IgnoredPoint> ignoredPoints)
        {
            return IsCurrentTopSideStructurePoint(dim.FirstPoint, outline, ignoredPoints)
                && IsCurrentTopSideStructurePoint(dim.SecondPoint, outline, ignoredPoints);
        }

        private bool IsCurrentTopSideStructurePoint(Point2D point, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
        {
            if (IsTopSlopeEndpointStructurePoint(point, outline, ignoredPoints))
            {
                return true;
            }

            var levelSegments = outline.Segments
                .Where(s => s.IsVertical(_config.GeometryTolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthY > _config.GeometryTolerance)
                .Where(s => Math.Abs(s.MinX - point.X) <= _config.GeometryTolerance)
                .ToList();
            if (levelSegments.Count == 0)
            {
                return false;
            }

            var topMost = GetTopMostStructurePoint(levelSegments, ignoredPoints);
            return topMost != null && PointsEqual(topMost.Point, point);
        }

        private void AddTopSlopeEndpointStructurePoints(
            IList<StructurePoint> points,
            OutlineFeature2D outline,
            IList<IgnoredPoint> ignoredPoints)
        {
            foreach (var segment in outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(s => !s.IsArcChord))
            {
                var low = segment.Start.Y <= segment.End.Y ? segment.Start : segment.End;
                var high = PointsEqual(low, segment.Start) ? segment.End : segment.Start;
                if (!IsTopSlopeEndpointStructurePoint(low, high, segment, outline, ignoredPoints))
                {
                    continue;
                }

                AddTopSlopeEndpointStructurePoint(points, low, outline, ignoredPoints);
            }
        }

        private bool IsTopSlopeEndpointStructurePoint(
            Point2D low,
            Point2D high,
            Segment2D slope,
            OutlineFeature2D outline,
            IList<IgnoredPoint> ignoredPoints)
        {
            if (ContainsIgnoredPoint(ignoredPoints, low)
                || IsEnvelopeSidePoint(low, outline)
                || high.Y <= low.Y + _config.GeometryTolerance)
            {
                return false;
            }

            return EndpointConnectsHorizontalSegment(low, slope, outline)
                && EndpointConnectsHigherHorizontalSegment(high, low.Y, slope, outline);
        }

        private bool IsTopSlopeEndpointStructurePoint(Point2D point, OutlineFeature2D outline, IList<IgnoredPoint> ignoredPoints)
        {
            if (ContainsIgnoredPoint(ignoredPoints, point) || IsEnvelopeSidePoint(point, outline))
            {
                return false;
            }

            foreach (var segment in outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(s => !s.IsArcChord))
            {
                if (!PointsEqual(point, segment.Start) && !PointsEqual(point, segment.End))
                {
                    continue;
                }

                var low = segment.Start.Y <= segment.End.Y ? segment.Start : segment.End;
                var high = PointsEqual(low, segment.Start) ? segment.End : segment.Start;
                if (PointsEqual(point, low)
                    && IsTopSlopeEndpointStructurePoint(low, high, segment, outline, ignoredPoints))
                {
                    return true;
                }
            }

            return false;
        }

        private void AddTopSlopeEndpointStructurePoint(
            IList<StructurePoint> points,
            Point2D point,
            OutlineFeature2D outline,
            IList<IgnoredPoint> ignoredPoints)
        {
            if (ContainsStructurePoint(points, point)
                || ContainsIgnoredPoint(ignoredPoints, point)
                || IsEnvelopeSidePoint(point, outline))
            {
                return;
            }

            points.Add(new StructurePoint
            {
                Point = point,
                Source = "TopSlopeEndpoint"
            });
        }

        private bool EndpointConnectsHorizontalSegment(Point2D point, Segment2D source, OutlineFeature2D outline)
        {
            return outline.Segments.Any(segment =>
                !ReferenceEquals(segment, source)
                && segment.IsHorizontal(_config.GeometryTolerance)
                && !segment.IsArcChord
                && IsPointOnHorizontalSegment(point, segment));
        }

        private bool EndpointConnectsHigherHorizontalSegment(
            Point2D point,
            double referenceY,
            Segment2D source,
            OutlineFeature2D outline)
        {
            return outline.Segments.Any(segment =>
                !ReferenceEquals(segment, source)
                && segment.IsHorizontal(_config.GeometryTolerance)
                && !segment.IsArcChord
                && segment.MinY > referenceY + _config.GeometryTolerance
                && IsPointOnHorizontalSegment(point, segment));
        }

        private bool IsPointOnHorizontalSegment(Point2D point, Segment2D segment)
        {
            return segment.IsHorizontal(_config.GeometryTolerance)
                && Math.Abs(segment.MinY - point.Y) <= _config.GeometryTolerance
                && point.X >= segment.MinX - _config.GeometryTolerance
                && point.X <= segment.MaxX + _config.GeometryTolerance;
        }

        private bool TopExtensionCrossesOutline(Point2D featurePoint, OutlineFeature2D outline)
        {
            var tolerance = _config.GeometryTolerance;
            var minY = featurePoint.Y + tolerance;
            var dimY = outline.MaxY + _config.FirstDimOffset;
            if (dimY <= minY + tolerance)
            {
                return false;
            }

            foreach (var segment in outline.Segments)
            {
                double y;
                if (TryGetVerticalIntersectionY(segment, featurePoint.X, out y)
                    && y >= minY
                    && y <= dimY + tolerance)
                {
                    return true;
                }

                if (VerticalExtensionOverlapsOutlineSegment(segment, featurePoint, minY, dimY))
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

        private bool VerticalExtensionOverlapsOutlineSegment(
            Segment2D segment,
            Point2D featurePoint,
            double minY,
            double maxY)
        {
            if (!segment.IsVertical(_config.GeometryTolerance))
            {
                return false;
            }

            if (Math.Abs(segment.MinX - featurePoint.X) > _config.GeometryTolerance)
            {
                return false;
            }

            var overlapStart = Math.Max(segment.MinY, minY);
            var overlapEnd = Math.Min(segment.MaxY, maxY);
            if (overlapEnd <= overlapStart + _config.GeometryTolerance)
            {
                return false;
            }

            return !PointLiesOnSegmentVerticalExtent(segment, featurePoint.Y);
        }

        private bool HorizontalExtensionOverlapsOutlineSegment(
            Segment2D segment,
            Point2D featurePoint,
            double minX,
            double maxX)
        {
            if (!segment.IsHorizontal(_config.GeometryTolerance))
            {
                return false;
            }

            if (Math.Abs(segment.MinY - featurePoint.Y) > _config.GeometryTolerance)
            {
                return false;
            }

            var overlapStart = Math.Max(segment.MinX, minX);
            var overlapEnd = Math.Min(segment.MaxX, maxX);
            if (overlapEnd <= overlapStart + _config.GeometryTolerance)
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
            foreach (var segment in outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(s => !s.IsArcChord))
            {
                if (!PointsEqual(point, segment.Start) && !PointsEqual(point, segment.End))
                {
                    continue;
                }

                if (IsDirectionalInclinedEndpoint(point, segment, invertDirection))
                {
                    return invertDirection ? "DirectionalInclinedInverted" : "DirectionalInclined";
                }
            }

            return string.Empty;
        }

        private bool ShouldIgnoreSideDirectionalInclinedEndpoint(
            Point2D point,
            OutlineFeature2D outline,
            DimensionSide side)
        {
            return outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(IsIgnorableFortyFiveDegreeSegment)
                .Any(s =>
                    IsSideInnerGrooveChamferSegment(s, outline, side)
                        ? IsSideInnerGrooveIgnoredEndpoint(point, s, outline, side)
                        : IsSideDirectionalIgnoredEndpoint(point, s, side));
        }

        private bool IsSideDirectionalIgnoredEndpoint(Point2D point, Segment2D segment, DimensionSide side)
        {
            if (!PointsEqual(point, segment.Start) && !PointsEqual(point, segment.End))
            {
                return false;
            }

            var dx = segment.End.X - segment.Start.X;
            var dy = segment.End.Y - segment.Start.Y;
            if (Math.Abs(dx) <= _config.GeometryTolerance || Math.Abs(dy) <= _config.GeometryTolerance)
            {
                return false;
            }

            if (dx * dy > 0.0)
            {
                var ignoredPoint = side == DimensionSide.Left
                    ? (segment.Start.Y <= segment.End.Y ? segment.Start : segment.End)
                    : (segment.Start.Y >= segment.End.Y ? segment.Start : segment.End);
                return PointsEqual(point, ignoredPoint);
            }

            var negativeIgnoredPoint = side == DimensionSide.Left
                ? (segment.Start.Y >= segment.End.Y ? segment.Start : segment.End)
                : (segment.Start.Y <= segment.End.Y ? segment.Start : segment.End);
            return PointsEqual(point, negativeIgnoredPoint);
        }

        private bool IsDirectionalInclinedEndpoint(Point2D point, Segment2D segment, bool invertDirection)
        {
            var dx = segment.End.X - segment.Start.X;
            var dy = segment.End.Y - segment.Start.Y;
            if (Math.Abs(dx) <= _config.GeometryTolerance || Math.Abs(dy) <= _config.GeometryTolerance)
            {
                return false;
            }

            if (dx * dy > 0.0)
            {
                var ignoredPoint = invertDirection
                    ? (segment.Start.X >= segment.End.X ? segment.Start : segment.End)
                    : (segment.Start.X <= segment.End.X ? segment.Start : segment.End);
                return PointsEqual(point, ignoredPoint);
            }

            var negativeIgnoredPoint = invertDirection
                ? (segment.Start.X <= segment.End.X ? segment.Start : segment.End)
                : (segment.Start.X >= segment.End.X ? segment.Start : segment.End);
            return PointsEqual(point, negativeIgnoredPoint);
        }

        private bool IsEnvelopeSidePoint(Point2D point, OutlineFeature2D outline)
        {
            return point.X <= outline.MinX + _config.GeometryTolerance
                || point.X >= outline.MaxX - _config.GeometryTolerance;
        }

        private bool IsEnvelopeHorizontalSidePoint(Point2D point, OutlineFeature2D outline)
        {
            return point.Y <= outline.MinY + _config.GeometryTolerance
                || point.Y >= outline.MaxY - _config.GeometryTolerance;
        }

        private bool ContainsStructurePoint(IEnumerable<StructurePoint> points, Point2D point)
        {
            return points.Any(p => PointsEqual(p.Point, point));
        }

        private bool ContainsIgnoredPoint(IEnumerable<IgnoredPoint> points, Point2D point)
        {
            return points.Any(p => PointsEqual(p.Point, point));
        }

        private bool IsTooSmallStructureSpan(double span)
        {
            return span <= Math.Max(_config.GeometryTolerance, _config.TextHeight * 2.0);
        }

        private bool IsCrossAxisStructureSpanTooLarge(Point2D first, Point2D second, bool horizontal)
        {
            var primary = horizontal
                ? Math.Abs(second.X - first.X)
                : Math.Abs(second.Y - first.Y);
            var cross = horizontal
                ? Math.Abs(second.Y - first.Y)
                : Math.Abs(second.X - first.X);
            return cross > primary + _config.GeometryTolerance;
        }

        private Point2D GetLeftBoundaryPoint(OutlineFeature2D outline)
        {
            return outline.Vertices
                .Where(v => Math.Abs(v.X - outline.MinX) <= _config.GeometryTolerance)
                .OrderBy(v => v.Y)
                .FirstOrDefault();
        }

        private Point2D GetRightBoundaryPoint(OutlineFeature2D outline)
        {
            return outline.Vertices
                .Where(v => Math.Abs(v.X - outline.MaxX) <= _config.GeometryTolerance)
                .OrderBy(v => v.Y)
                .FirstOrDefault();
        }

        private Point2D GetBottomBoundaryPoint(OutlineFeature2D outline)
        {
            return outline.Vertices
                .Where(v => Math.Abs(v.Y - outline.MinY) <= _config.GeometryTolerance)
                .OrderBy(v => v.X)
                .FirstOrDefault();
        }

        private Point2D GetTopBoundaryPoint(OutlineFeature2D outline)
        {
            return outline.Vertices
                .Where(v => Math.Abs(v.Y - outline.MaxY) <= _config.GeometryTolerance)
                .OrderBy(v => v.X)
                .FirstOrDefault();
        }

        private void AddDimension(
            DimensionPlan plan,
            DimensionKind kind,
            DimensionOrientation orientation,
            DimensionSide side,
            Point2D firstPoint,
            Point2D secondPoint,
            string overrideText,
            string debugRole,
            string debugOwner = null)
        {
            if (GetSpan(firstPoint, secondPoint, orientation) <= _config.GeometryTolerance)
            {
                return;
            }

            plan.Add(new PlannedDimension
            {
                Kind = kind,
                Orientation = orientation,
                Side = side,
                FirstPoint = firstPoint,
                SecondPoint = secondPoint,
                OverrideText = overrideText ?? string.Empty,
                DebugRole = debugRole,
                DebugOwner = debugOwner
            });
        }

        private void AddHorizontalDim(DimensionPlan plan, Point2D from, Point2D to, string debugRole)
        {
            AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Bottom,
                from, to, string.Empty, debugRole);
        }

        private void AddVerticalDim(DimensionPlan plan, Point2D from, Point2D to, string debugRole)
        {
            AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Vertical, DimensionSide.Left,
                from, to, string.Empty, debugRole);
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
            if (ordered == null || ordered.Count == 0)
            {
                return;
            }

            AddHorizontalDimFromX(plan, datumX, ordered[0], debugRole);
            for (int i = 1; i < ordered.Count; i++)
            {
                AddHorizontalDim(plan, ordered[i - 1], ordered[i], debugRole);
            }
        }

        private void AddVerticalChainFromDatum(DimensionPlan plan, double datumY, IList<Point2D> ordered, string debugRole)
        {
            if (ordered == null || ordered.Count == 0)
            {
                return;
            }

            AddVerticalDimFromY(plan, datumY, ordered[0], debugRole);
            for (int i = 1; i < ordered.Count; i++)
            {
                AddVerticalDim(plan, ordered[i - 1], ordered[i], debugRole);
            }
        }

        private List<List<Point2D>> GroupSlotAnchorsByCoordinate(
            IEnumerable<SlotFeature2D> slots,
            Datum2D datum,
            Func<Point2D, double> coordinate)
        {
            var groups = new List<List<Point2D>>();
            if (slots == null || datum == null)
            {
                return groups;
            }

            foreach (var point in slots.Select(s => PickSlotAnchorPoint(s, datum)))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(coordinate(g[0]) - coordinate(point)) <= _config.GeometryTolerance);
                if (group == null)
                {
                    group = new List<Point2D>();
                    groups.Add(group);
                }

                group.Add(point);
            }

            return groups;
        }

        private List<Point2D> UniquePointsByCoordinate(IEnumerable<Point2D> points, Func<Point2D, double> coordinate)
        {
            var unique = new List<Point2D>();
            foreach (var point in points)
            {
                if (unique.Count == 0 || Math.Abs(coordinate(unique[unique.Count - 1]) - coordinate(point)) > _config.GeometryTolerance)
                {
                    unique.Add(point);
                }
            }

            return unique;
        }

        private SlotFeature2D FindSlotByAnchor(IEnumerable<SlotFeature2D> slots, Point2D anchor)
        {
            if (slots == null)
            {
                return null;
            }

            return slots.FirstOrDefault(s =>
                s != null
                && Math.Abs(s.FirstCenter.X - anchor.X) <= _config.GeometryTolerance
                && Math.Abs(s.FirstCenter.Y - anchor.Y) <= _config.GeometryTolerance);
        }

        private void AddSingleArcSlotHorizontalDatumDimension(
            DimensionPlan plan,
            OutlineFeature2D outline,
            Datum2D datum,
            SlotFeature2D slot,
            Point2D center)
        {
            var from = FindOutlinePointAtX(outline, datum.BaseX, center.Y);
            var to = GetSingleArcSlotHorizontalGripPoint(slot, from.Y);
            var span = Math.Abs(center.X - datum.BaseX);
            if (span <= _config.GeometryTolerance)
            {
                return;
            }

            AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Bottom,
                from, to, _config.FormatNumber(span), "SingleArcSlotDatum", slot.GroupId);
        }

        private Point2D FindOutlinePointAtX(OutlineFeature2D outline, double x, double preferredY)
        {
            var candidates = new List<Point2D>();
            foreach (var vertex in outline.Vertices)
            {
                if (Math.Abs(vertex.X - x) <= _config.GeometryTolerance)
                {
                    candidates.Add(vertex);
                }
            }

            foreach (var segment in outline.Segments.Where(s => s.IsVertical(_config.GeometryTolerance)))
            {
                if (Math.Abs(segment.Start.X - x) > _config.GeometryTolerance)
                {
                    continue;
                }

                var y = Math.Max(segment.MinY, Math.Min(segment.MaxY, preferredY));
                candidates.Add(new Point2D(x, y));
            }

            return candidates
                .OrderBy(p => Math.Abs(p.Y - preferredY))
                .FirstOrDefault();
        }

        private Point2D GetSingleArcSlotHorizontalGripPoint(SlotFeature2D slot, double preferredY)
        {
            var upper = new Point2D(slot.FirstCenter.X, slot.FirstCenter.Y + slot.Radius);
            var lower = new Point2D(slot.FirstCenter.X, slot.FirstCenter.Y - slot.Radius);
            return Math.Abs(upper.Y - preferredY) <= Math.Abs(lower.Y - preferredY) ? upper : lower;
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
            return points.OrderBy(p => Math.Abs(p.Y - y)).First();
        }

        private static Point2D PickNearestPointByX(IEnumerable<Point2D> points, double x)
        {
            return points.OrderBy(p => Math.Abs(p.X - x)).First();
        }

        private Point2D PickSlotAnchorPoint(SlotFeature2D slot, Datum2D datum)
        {
            var first = slot.FirstCenter;
            if (slot.IsSingleArcSlot)
            {
                return first;
            }

            var second = slot.SecondCenter;
            var dx = Math.Abs(first.X - second.X);
            var dy = Math.Abs(first.Y - second.Y);
            if (dx >= dy)
            {
                return Math.Abs(first.X - datum.BaseX) <= Math.Abs(second.X - datum.BaseX) ? first : second;
            }

            return Math.Abs(first.Y - datum.BaseY) <= Math.Abs(second.Y - datum.BaseY) ? first : second;
        }

        private double GetSpan(Point2D firstPoint, Point2D secondPoint, DimensionOrientation orientation)
        {
            return orientation == DimensionOrientation.Horizontal
                ? Math.Abs(secondPoint.X - firstPoint.X)
                : Math.Abs(secondPoint.Y - firstPoint.Y);
        }

        private double GetAverageY(IList<HoleFeature2D> holes)
        {
            return holes.Count == 0 ? 0.0 : holes.Average(h => h.Center.Y);
        }

        private bool IsSamePinDiameter(HoleFeature2D a, HoleFeature2D b)
        {
            return a != null && b != null && Math.Abs(a.Diameter - b.Diameter) <= _config.GeometryTolerance;
        }

        private bool IsSameHole(HoleFeature2D a, HoleFeature2D b)
        {
            return a != null
                && b != null
                && PointsEqual(a.Center, b.Center);
        }

        private void RemoveGroupPins(IList<HoleFeature2D> remaining, PinGroupPlan group)
        {
            foreach (var pin in group.Pins.ToList())
            {
                for (int i = remaining.Count - 1; i >= 0; i--)
                {
                    if (IsSameHole(remaining[i], pin))
                    {
                        remaining.RemoveAt(i);
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

            return group.Pins.Take(2).Sum(pin => Math.Sqrt(DistanceSquared(hole.Center, pin.Center)));
        }

        private double DistanceSquared(Point2D a, Point2D b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private Point2D Midpoint(Point2D a, Point2D b)
        {
            return new Point2D((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
        }

        private DimensionSide ChooseHorizontalHoleSide(OutlineFeature2D outline, Datum2D datum, Point2D point)
        {
            var bottomDistance = Math.Abs(point.Y - outline.MinY);
            var topDistance = Math.Abs(outline.MaxY - point.Y);
            if (Math.Abs(bottomDistance - topDistance) <= _config.GeometryTolerance)
            {
                return ChooseOppositeHorizontalDatumSide(outline, datum);
            }

            return bottomDistance < topDistance ? DimensionSide.Bottom : DimensionSide.Top;
        }

        private DimensionSide ChooseVerticalHoleSide(OutlineFeature2D outline, Datum2D datum, Point2D point)
        {
            var leftDistance = Math.Abs(point.X - outline.MinX);
            var rightDistance = Math.Abs(outline.MaxX - point.X);
            if (Math.Abs(leftDistance - rightDistance) <= _config.GeometryTolerance)
            {
                return ChooseOppositeVerticalDatumSide(outline, datum);
            }

            return leftDistance < rightDistance ? DimensionSide.Left : DimensionSide.Right;
        }

        private DimensionSide ChooseHorizontalHoleSide(OutlineFeature2D outline, Point2D point)
        {
            var bottomDistance = Math.Abs(point.Y - outline.MinY);
            var topDistance = Math.Abs(outline.MaxY - point.Y);
            return bottomDistance <= topDistance ? DimensionSide.Bottom : DimensionSide.Top;
        }

        private DimensionSide ChooseVerticalHoleSide(OutlineFeature2D outline, Point2D point)
        {
            var leftDistance = Math.Abs(point.X - outline.MinX);
            var rightDistance = Math.Abs(outline.MaxX - point.X);
            return leftDistance <= rightDistance ? DimensionSide.Left : DimensionSide.Right;
        }

        private DimensionSide ChooseOppositeHorizontalDatumSide(OutlineFeature2D outline, Datum2D datum)
        {
            var datumToBottom = Math.Abs(datum.BaseY - outline.MinY);
            var datumToTop = Math.Abs(outline.MaxY - datum.BaseY);
            var datumSide = datumToBottom <= datumToTop ? DimensionSide.Bottom : DimensionSide.Top;
            return datumSide == DimensionSide.Bottom ? DimensionSide.Top : DimensionSide.Bottom;
        }

        private DimensionSide ChooseOppositeVerticalDatumSide(OutlineFeature2D outline, Datum2D datum)
        {
            var datumToLeft = Math.Abs(datum.BaseX - outline.MinX);
            var datumToRight = Math.Abs(outline.MaxX - datum.BaseX);
            var datumSide = datumToLeft <= datumToRight ? DimensionSide.Left : DimensionSide.Right;
            return datumSide == DimensionSide.Left ? DimensionSide.Right : DimensionSide.Left;
        }

        private static bool ShouldUseDatumHoleLocationTolerance(Datum2D datum, bool isXDirection)
        {
            if (datum.DatumHole == null)
            {
                return true;
            }

            return isXDirection
                ? datum.DatumHoleLocationUseToleranceX
                : datum.DatumHoleLocationUseToleranceY;
        }

        private string GetPinGroupDebugOwner(PinGroupPlan group)
        {
            return group == null ? string.Empty : "PG" + group.GroupIndex.ToString(CultureInfo.InvariantCulture);
        }
    }
}
