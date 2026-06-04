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

            var plan = new DimensionPlan();
            AddOverallWidth(plan, outline);
            AddOverallHeight(plan, outline);
            AddLinearSegmentDimensions(plan, outline);
            return plan;
        }

        public DimensionPlan CreateDimensionPlan(
            OutlineFeature2D outline,
            Datum2D datum,
            IEnumerable<HoleFeature2D> holes)
        {
            var plan = CreateOutlinePlan(outline);
            AddHolePositionDimensions(plan, outline, datum ?? Datum2D.FromOutline(outline), holes ?? new HoleFeature2D[0]);
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
            foreach (var hole in holes.Where(h => !h.IsPinHole && !h.IsSlotPoint))
            {
                var group = pinGroups
                    .Where(g => g.MemberHoles.Any(member => IsSameHole(member, hole)))
                    .OrderBy(g => DistanceSquared(g.BasePin.Center, hole.Center))
                    .FirstOrDefault();
                if (group == null)
                {
                    continue;
                }

                if (Math.Abs(group.BasePin.Center.X - hole.Center.X) > _config.GeometryTolerance)
                {
                    AddDimension(plan, DimensionKind.HoleLocation, DimensionOrientation.Horizontal,
                        ChooseHorizontalHoleSide(outline, hole.Center),
                        group.BasePin.Center,
                        hole.Center,
                        string.Empty,
                        "HolePinRef",
                        GetPinGroupDebugOwner(group));
                }

                if (Math.Abs(group.BasePin.Center.Y - hole.Center.Y) > _config.GeometryTolerance)
                {
                    AddDimension(plan, DimensionKind.HoleLocation, DimensionOrientation.Vertical,
                        ChooseVerticalHoleSide(outline, hole.Center),
                        group.BasePin.Center,
                        hole.Center,
                        string.Empty,
                        "HolePinRef",
                        GetPinGroupDebugOwner(group));
                }
            }
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
                FirstPoint = new Point2D(outline.MinX, outline.MinY),
                SecondPoint = new Point2D(outline.MaxX, outline.MinY),
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
                FirstPoint = new Point2D(outline.MinX, outline.MinY),
                SecondPoint = new Point2D(outline.MinX, outline.MaxY),
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
                && PointsEqual(a.Center, b.Center)
                && Math.Abs(a.Diameter - b.Diameter) <= _config.GeometryTolerance
                && a.Kind == b.Kind;
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
