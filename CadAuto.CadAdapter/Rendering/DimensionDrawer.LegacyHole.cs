using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        public void DrawHolePositionDimensions(OutlineFeature outline, DatumDefinition datum, IList<IList<HoleFeature>> rows)
        {
            var activeRows = rows.Where(r => r.Count > 0).ToList();
            if (activeRows.Count == 0)
            {
                return;
            }

            if (datum.DatumHole != null)
            {
                DrawHolePositionFromDatumHole(outline, datum, activeRows);
            }
            else
            {
                DrawHolePositionFromOutlineEdge(outline, datum, activeRows);
            }
        }

        private void DrawHolePositionFromDatumHole(OutlineFeature outline, DatumDefinition datum, IList<IList<HoleFeature>> rows)
        {
            var datumHole = datum.DatumHole;
            var holes = rows.SelectMany(r => r).ToList();
            var pinGroups = BuildPinGroupPlan(holes, datumHole);

            if (pinGroups.Count == 0)
            {
                DrawNonPinHolesFromOutlineEdge(outline, datum, holes);
                return;
            }

            AssignPinGroupPlacementSides(outline, datum, pinGroups);
            EmitFirstPinGroupBaseLocation(datum, pinGroups[0]);
            EmitPinGroupBaseTransfers(pinGroups);
            EmitSameGroupPinDistances(outline, pinGroups);
            EmitNonPinHoleLocations(outline, holes, pinGroups);
        }

        private void DrawHolePositionFromOutlineEdge(OutlineFeature outline, DatumDefinition datum, IList<IList<HoleFeature>> rows)
        {
            var holes = rows.SelectMany(r => r).ToList();
            var pinGroups = BuildPinGroupPlan(holes, null);
            if (pinGroups.Count == 0)
            {
                DrawNonPinHolesFromOutlineEdge(outline, datum, holes);
                return;
            }

            AssignPinGroupPlacementSides(outline, datum, pinGroups);
            EmitFirstPinGroupBaseLocation(datum, pinGroups[0]);
            EmitPinGroupBaseTransfers(pinGroups);
            EmitSameGroupPinDistances(outline, pinGroups);
            EmitNonPinHoleLocations(outline, holes, pinGroups);
        }

        private List<PinGroupPlan> BuildPinGroupPlan(IEnumerable<HoleFeature> holes, HoleFeature datumPin)
        {
            var allHoles = holes.Where(h => h != null && !h.IsSlotPoint).ToList();
            var remaining = allHoles.Where(h => h.IsPinHole).OrderBy(h => h.Center.X).ThenBy(h => h.Center.Y).ToList();
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

        private PinGroupPlan CreatePinPairGroup(
            HoleFeature seed,
            IList<HoleFeature> candidates,
            HoleFeature forcedBasePin,
            HoleFeature referenceBasePin)
        {
            var pins = new List<HoleFeature> { seed };
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

        private bool IsSamePinDiameter(HoleFeature a, HoleFeature b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return System.Math.Abs(a.Diameter - b.Diameter) <= _config.GeometryTolerance;
        }

        private void AssignNonPinHolesToNearestPinPair(IList<HoleFeature> holes, IList<PinGroupPlan> groups)
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

        private double DistanceToPinPair(HoleFeature hole, PinGroupPlan group)
        {
            if (hole == null || group == null || group.Pins.Count == 0)
            {
                return double.MaxValue;
            }

            if (group.Pins.Count == 1)
            {
                return System.Math.Sqrt(DistanceSquared(hole.Center, group.Pins[0].Center));
            }

            return group.Pins
                .Take(2)
                .Sum(pin => System.Math.Sqrt(DistanceSquared(hole.Center, pin.Center)));
        }

        private HoleFeature ChoosePinGroupBasePin(
            IList<HoleFeature> groupPins,
            HoleFeature forcedBasePin,
            HoleFeature referenceBasePin,
            HoleFeature fallbackPin)
        {
            if (forcedBasePin != null)
            {
                var matchedForced = groupPins.FirstOrDefault(h => IsSameHole(h, forcedBasePin));
                if (matchedForced != null)
                {
                    return matchedForced;
                }

                return forcedBasePin;
            }

            if (referenceBasePin != null && groupPins.Count > 0)
            {
                return groupPins
                    .OrderBy(h => DistanceSquared(h.Center, referenceBasePin.Center))
                    .ThenBy(h => System.Math.Abs(h.Center.X - referenceBasePin.Center.X))
                    .ThenBy(h => System.Math.Abs(h.Center.Y - referenceBasePin.Center.Y))
                    .ThenBy(h => h.Center.X)
                    .ThenBy(h => h.Center.Y)
                    .First();
            }

            return groupPins
                .OrderBy(h => h.Center.X)
                .ThenBy(h => h.Center.Y)
                .FirstOrDefault() ?? fallbackPin;
        }

        private void RemoveGroupPins(IList<HoleFeature> remaining, PinGroupPlan group)
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

        private void AssignPinGroupPlacementSides(OutlineFeature outline, DatumDefinition datum, IList<PinGroupPlan> groups)
        {
            foreach (var group in groups)
            {
                group.HorizontalSide = ChooseHorizontalHoleSide(outline, datum, group.BasePin.Center);
                group.VerticalSide = ChooseVerticalHoleSide(outline, datum, group.BasePin.Center);
            }
        }

        private DimSide ChooseHorizontalHoleSide(OutlineFeature outline, DatumDefinition datum, Point3d point)
        {
            var bottomDistance = System.Math.Abs(point.Y - outline.MinY);
            var topDistance = System.Math.Abs(outline.MaxY - point.Y);
            var distanceDelta = System.Math.Abs(bottomDistance - topDistance);
            if (distanceDelta <= _config.GeometryTolerance)
            {
                return ChooseOppositeHorizontalDatumSide(outline, datum);
            }

            return bottomDistance < topDistance ? DimSide.Bottom : DimSide.Top;
        }

        private DimSide ChooseVerticalHoleSide(OutlineFeature outline, DatumDefinition datum, Point3d point)
        {
            var leftDistance = System.Math.Abs(point.X - outline.MinX);
            var rightDistance = System.Math.Abs(outline.MaxX - point.X);
            var distanceDelta = System.Math.Abs(leftDistance - rightDistance);
            if (distanceDelta <= _config.GeometryTolerance)
            {
                return ChooseOppositeVerticalDatumSide(outline, datum);
            }

            return leftDistance < rightDistance ? DimSide.Left : DimSide.Right;
        }

        private DimSide ChooseOppositeHorizontalDatumSide(OutlineFeature outline, DatumDefinition datum)
        {
            var datumToBottom = System.Math.Abs(datum.BaseY - outline.MinY);
            var datumToTop = System.Math.Abs(outline.MaxY - datum.BaseY);
            var datumSide = datumToBottom <= datumToTop ? DimSide.Bottom : DimSide.Top;
            return datumSide == DimSide.Bottom ? DimSide.Top : DimSide.Bottom;
        }

        private DimSide ChooseOppositeVerticalDatumSide(OutlineFeature outline, DatumDefinition datum)
        {
            var datumToLeft = System.Math.Abs(datum.BaseX - outline.MinX);
            var datumToRight = System.Math.Abs(outline.MaxX - datum.BaseX);
            var datumSide = datumToLeft <= datumToRight ? DimSide.Left : DimSide.Right;
            return datumSide == DimSide.Left ? DimSide.Right : DimSide.Left;
        }

        private void EmitFirstPinGroupBaseLocation(DatumDefinition datum, PinGroupPlan group)
        {
            var basePin = group.BasePin;
            var xRef = datum.DatumHoleLocationBaseX ?? datum.BaseX;
            var yRef = datum.DatumHoleLocationBaseY ?? datum.BaseY;
            var xToleranceText = ShouldUseDatumHoleLocationTolerance(datum, isXDirection: true)
                ? _config.DatumHoleLocationToleranceText ?? string.Empty
                : string.Empty;
            var yToleranceText = ShouldUseDatumHoleLocationTolerance(datum, isXDirection: false)
                ? _config.DatumHoleLocationToleranceText ?? string.Empty
                : string.Empty;
            AddHorizontalDimFromPointToSide(
                new Point3d(xRef, basePin.Center.Y, 0.0),
                basePin.Center,
                xToleranceText,
                DimensionType.DatumHoleLocationX,
                group.HorizontalSide,
                debugOwner: GetPinGroupDebugOwner(group),
                debugRole: "DatumX");
            AddVerticalDimFromPointToSide(
                new Point3d(basePin.Center.X, yRef, 0.0),
                basePin.Center,
                yToleranceText,
                DimensionType.DatumHoleLocationY,
                group.VerticalSide,
                debugOwner: GetPinGroupDebugOwner(group),
                debugRole: "DatumY");
        }

        private static bool ShouldUseDatumHoleLocationTolerance(DatumDefinition datum, bool isXDirection)
        {
            if (datum.DatumHole == null)
            {
                return true;
            }

            return isXDirection
                ? datum.DatumHoleLocationUseToleranceX
                : datum.DatumHoleLocationUseToleranceY;
        }

        private void EmitPinGroupBaseTransfers(IList<PinGroupPlan> groups)
        {
            if (groups.Count < 2)
            {
                return;
            }

            var firstBase = groups[0].BasePin;
            for (int i = 1; i < groups.Count; i++)
            {
                var currentBase = groups[i].BasePin;
                var dx = System.Math.Abs(currentBase.Center.X - firstBase.Center.X);
                var dy = System.Math.Abs(currentBase.Center.Y - firstBase.Center.Y);

                if (dx > _config.GeometryTolerance)
                {
                    AddHorizontalDimToSide(
                        firstBase.Center,
                        currentBase.Center,
                        _config.FormatPinGroupDistanceOverride(dx),
                        DimensionType.PinGroupDistance,
                        groups[i].HorizontalSide,
                        debugOwner: GetPinGroupDebugOwner(groups[i]),
                        debugRole: "PinGroupDistance");
                }

                if (dy > _config.GeometryTolerance)
                {
                    AddVerticalDimToSide(
                        firstBase.Center,
                        currentBase.Center,
                        _config.FormatPinGroupDistanceOverride(dy),
                        DimensionType.PinGroupDistance,
                        groups[i].VerticalSide,
                        debugOwner: GetPinGroupDebugOwner(groups[i]),
                        debugRole: "PinGroupDistance");
                }
            }
        }

        private void EmitSameGroupPinDistances(OutlineFeature outline, IList<PinGroupPlan> groups)
        {
            foreach (var group in groups)
            {
                foreach (var pin in group.Pins)
                {
                    if (IsSameHole(pin, group.BasePin))
                    {
                        continue;
                    }

                    var dx = System.Math.Abs(pin.Center.X - group.BasePin.Center.X);
                    var dy = System.Math.Abs(pin.Center.Y - group.BasePin.Center.Y);
                    if (dx > _config.GeometryTolerance)
                    {
                        var horizontalSide = ChooseHorizontalHoleSide(outline, Midpoint(group.BasePin.Center, pin.Center));
                        AddHorizontalDimToSide(
                            group.BasePin.Center,
                            pin.Center,
                            _config.FormatPinCenterDistanceOverride(dx),
                            DimensionType.PinDistance,
                            horizontalSide,
                            debugOwner: GetPinGroupDebugOwner(group),
                            debugRole: "PinDistance");
                    }

                    if (dy > _config.GeometryTolerance)
                    {
                        var verticalSide = ChooseVerticalHoleSide(outline, Midpoint(group.BasePin.Center, pin.Center));
                        AddVerticalDimToSide(
                            group.BasePin.Center,
                            pin.Center,
                            _config.FormatPinCenterDistanceOverride(dy),
                            DimensionType.PinDistance,
                            verticalSide,
                            debugOwner: GetPinGroupDebugOwner(group),
                            debugRole: "PinDistance");
                    }
                }
            }
        }

        private static string GetPinGroupDebugOwner(PinGroupPlan group)
        {
            if (group == null || group.GroupIndex <= 0)
            {
                return string.Empty;
            }

            return "PG" + group.GroupIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
