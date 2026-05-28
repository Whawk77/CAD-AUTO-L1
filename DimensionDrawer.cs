using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace AutoFixtureDim
{
    public sealed class DimensionDrawer
    {
        private readonly Database _db;
        private readonly Transaction _tr;
        private readonly BlockTableRecord _space;
        private readonly DimensionRuleConfig _config;
        private readonly ObjectId _dimStyleId;
        private readonly ObjectId _diameterCalloutDimStyleId;
        private readonly double _dimScale;
        private readonly string _annotationLayer;
        private readonly bool _appendToDatabase;
        private readonly string _groupId;
        private readonly IList<Entity> _previewEntities = new List<Entity>();
        private readonly List<DeferredDim> _bottomDims = new List<DeferredDim>();
        private readonly List<DeferredDim> _topDims = new List<DeferredDim>();
        private readonly List<DeferredDim> _leftDims = new List<DeferredDim>();
        private readonly List<DeferredDim> _rightDims = new List<DeferredDim>();
        private readonly List<TextBounds> _linearDimTextObstacles = new List<TextBounds>();
        private int _nextLooseChainId = 1;

        private enum DimSide { Bottom, Top, Left, Right }

        private struct DeferredDim
        {
            public double Rotation;
            public Point3d XLine1;
            public Point3d XLine2;
            public string OverrideText;
            public double Span;
            public DimensionType DimType;
            public bool UseSegmentedExtensionLines;
            public bool ForceOuterLevel;
            public int LooseChainId;
        }

        private struct PlacedDim
        {
            public DeferredDim Dim;
            public DimSide Side;
            public Point3d DimLinePoint;
            public TextBounds TextBounds;
            public bool HasCustomTextPosition;
            public Point3d TextPosition;
        }

        private struct TextBounds
        {
            public double MinX;
            public double MaxX;
            public double MinY;
            public double MaxY;
        }

        private sealed class PinGroupPlan
        {
            public HoleFeature BasePin { get; set; }
            public List<HoleFeature> Pins { get; } = new List<HoleFeature>();
            public List<HoleFeature> MemberHoles { get; } = new List<HoleFeature>();
            public DimSide HorizontalSide { get; set; } = DimSide.Bottom;
            public DimSide VerticalSide { get; set; } = DimSide.Left;
        }

        private sealed class FunctionalHoleGroupPlan
        {
            public PinGroupPlan PinGroup { get; set; }
            public List<HoleFeature> Holes { get; } = new List<HoleFeature>();
        }

        private sealed class LooseHoleLineGroup
        {
            public bool Horizontal { get; set; }
            public string SpecKey { get; set; }
            public List<HoleFeature> Holes { get; } = new List<HoleFeature>();
        }

        private sealed class LooseHoleMacroGroup
        {
            public List<LooseHoleLineGroup> LineGroups { get; } = new List<LooseHoleLineGroup>();
            public List<HoleFeature> Holes { get; } = new List<HoleFeature>();
        }

        private sealed class LooseHoleLocationPlan
        {
            public LooseHoleMacroGroup MacroGroup { get; set; }
            public PinGroupPlan ReferencePinGroup { get; set; }
            public HoleFeature AnchorHole { get; set; }
        }

        public DimensionDrawer(
            Database db,
            Transaction tr,
            BlockTableRecord space,
            DimensionRuleConfig config,
            ObjectId dimStyleId,
            ObjectId diameterCalloutDimStyleId,
            double dimScale,
            string annotationLayer,
            bool appendToDatabase,
            string groupId)
        {
            _db = db;
            _tr = tr;
            _space = space;
            _config = config;
            _dimStyleId = dimStyleId;
            _diameterCalloutDimStyleId = diameterCalloutDimStyleId;
            _dimScale = dimScale <= 0.0 ? 1.0 : dimScale;
            _annotationLayer = annotationLayer;
            _appendToDatabase = appendToDatabase;
            _groupId = groupId;
        }

        public IList<Entity> PreviewEntities
        {
            get { return _previewEntities; }
        }

        public void DrawOutlineDimensions(OutlineFeature outline)
        {
            AddBoundingOverallWidth(outline);
            AddBoundingOverallHeight(outline);
        }

        private void AddBoundingOverallWidth(OutlineFeature outline)
        {
            if (outline.Width <= _config.GeometryTolerance)
            {
                return;
            }

            var dim = new DeferredDim
            {
                Rotation = 0.0,
                XLine1 = GetLeftBoundaryPoint(outline),
                XLine2 = GetRightBoundaryPoint(outline),
                OverrideText = string.Empty,
                Span = outline.Width,
                DimType = DimensionType.OverallWidth,
                ForceOuterLevel = true
            };
            _bottomDims.Add(dim);
        }

        private void AddBoundingOverallHeight(OutlineFeature outline)
        {
            if (outline.Height <= _config.GeometryTolerance)
            {
                return;
            }

            var dim = new DeferredDim
            {
                Rotation = Math.PI / 2.0,
                XLine1 = GetBottomBoundaryPoint(outline),
                XLine2 = GetTopBoundaryPoint(outline),
                OverrideText = string.Empty,
                Span = outline.Height,
                DimType = DimensionType.OverallHeight,
                ForceOuterLevel = true
            };
            _leftDims.Add(dim);
        }

        public void DrawStepOutlineDimensions(OutlineFeature outline)
        {
            if (outline.Segments.Count == 0)
            {
                return;
            }

            DrawTopStepWidth(outline);
            DrawLowerRightStepWidth(outline);
            DrawRightStepHeight(outline);
            DrawRightSideStepHeight(outline);
        }

        private OutlineSegment FindOverallWidthSegment(OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => s.LengthX > tolerance)
                .Where(s => IsOuterHorizontalSegment(s, outline))
                .OrderByDescending(s => s.LengthX)
                .ThenByDescending(s => s.MaxY)
                .ThenBy(s => Math.Abs(s.MinX - outline.MinX) + Math.Abs(s.MaxX - outline.MaxX))
                .FirstOrDefault();
        }

        private OutlineSegment FindOverallHeightSegment(OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Where(s => s.LengthY > tolerance)
                .Where(s => IsOuterVerticalSegment(s, outline))
                .OrderByDescending(s => s.LengthY)
                .ThenBy(s => s.MinX)
                .ThenBy(s => Math.Abs(s.MinY - outline.MinY) + Math.Abs(s.MaxY - outline.MaxY))
                .FirstOrDefault();
        }

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

            return Math.Abs(a.Diameter - b.Diameter) <= _config.GeometryTolerance;
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
                return Math.Sqrt(DistanceSquared(hole.Center, group.Pins[0].Center));
            }

            return group.Pins
                .Take(2)
                .Sum(pin => Math.Sqrt(DistanceSquared(hole.Center, pin.Center)));
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
            var bottomDistance = Math.Abs(point.Y - outline.MinY);
            var topDistance = Math.Abs(outline.MaxY - point.Y);
            var distanceDelta = Math.Abs(bottomDistance - topDistance);
            if (distanceDelta <= _config.GeometryTolerance)
            {
                return ChooseOppositeHorizontalDatumSide(outline, datum);
            }

            return bottomDistance < topDistance ? DimSide.Bottom : DimSide.Top;
        }

        private DimSide ChooseVerticalHoleSide(OutlineFeature outline, DatumDefinition datum, Point3d point)
        {
            var leftDistance = Math.Abs(point.X - outline.MinX);
            var rightDistance = Math.Abs(outline.MaxX - point.X);
            var distanceDelta = Math.Abs(leftDistance - rightDistance);
            if (distanceDelta <= _config.GeometryTolerance)
            {
                return ChooseOppositeVerticalDatumSide(outline, datum);
            }

            return leftDistance < rightDistance ? DimSide.Left : DimSide.Right;
        }

        private DimSide ChooseOppositeHorizontalDatumSide(OutlineFeature outline, DatumDefinition datum)
        {
            var datumToBottom = Math.Abs(datum.BaseY - outline.MinY);
            var datumToTop = Math.Abs(outline.MaxY - datum.BaseY);
            var datumSide = datumToBottom <= datumToTop ? DimSide.Bottom : DimSide.Top;
            return datumSide == DimSide.Bottom ? DimSide.Top : DimSide.Bottom;
        }

        private DimSide ChooseOppositeVerticalDatumSide(OutlineFeature outline, DatumDefinition datum)
        {
            var datumToLeft = Math.Abs(datum.BaseX - outline.MinX);
            var datumToRight = Math.Abs(outline.MaxX - datum.BaseX);
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
            AddHorizontalDimFromPointToSide(new Point3d(xRef, basePin.Center.Y, 0.0), basePin.Center, xToleranceText, DimensionType.DatumHoleLocationX, group.HorizontalSide);
            AddVerticalDimFromPointToSide(new Point3d(basePin.Center.X, yRef, 0.0), basePin.Center, yToleranceText, DimensionType.DatumHoleLocationY, group.VerticalSide);
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
                var dx = Math.Abs(currentBase.Center.X - firstBase.Center.X);
                var dy = Math.Abs(currentBase.Center.Y - firstBase.Center.Y);

                if (dx > _config.GeometryTolerance)
                {
                    AddHorizontalDimToSide(
                        firstBase.Center,
                        currentBase.Center,
                        _config.FormatPinGroupDistanceOverride(dx),
                        DimensionType.PinGroupDistance,
                        groups[i].HorizontalSide);
                }

                if (dy > _config.GeometryTolerance)
                {
                    AddVerticalDimToSide(
                        firstBase.Center,
                        currentBase.Center,
                        _config.FormatPinGroupDistanceOverride(dy),
                        DimensionType.PinGroupDistance,
                        groups[i].VerticalSide);
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

                    var dx = Math.Abs(pin.Center.X - group.BasePin.Center.X);
                    var dy = Math.Abs(pin.Center.Y - group.BasePin.Center.Y);
                    if (dx > _config.GeometryTolerance)
                    {
                        var horizontalSide = ChooseHorizontalHoleSide(outline, Midpoint(group.BasePin.Center, pin.Center));
                        AddHorizontalDimToSide(
                            group.BasePin.Center,
                            pin.Center,
                            _config.FormatPinCenterDistanceOverride(dx),
                            DimensionType.PinDistance,
                            horizontalSide);
                    }

                    if (dy > _config.GeometryTolerance)
                    {
                        var verticalSide = ChooseVerticalHoleSide(outline, Midpoint(group.BasePin.Center, pin.Center));
                        AddVerticalDimToSide(
                            group.BasePin.Center,
                            pin.Center,
                            _config.FormatPinCenterDistanceOverride(dy),
                            DimensionType.PinDistance,
                            verticalSide);
                    }
                }
            }
        }

        private void EmitNonPinHoleLocations(OutlineFeature outline, IList<HoleFeature> holes, IList<PinGroupPlan> pinGroups)
        {
            var functionalGroups = BuildFunctionalHoleGroups(pinGroups);
            var groupedHoles = functionalGroups
                .SelectMany(g => g.Holes)
                .ToList();

            EmitFunctionalHoleGroupLocations(functionalGroups);
            EmitLooseNonPinHoleLocations(outline, holes, pinGroups, groupedHoles);
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

                var used = new List<HoleFeature>();
                foreach (var functionalGroup in FindFunctionalHoleGroupsForPinPair(pinGroup, candidates, used))
                {
                    result.Add(functionalGroup);
                }
            }

            return result;
        }

        private IEnumerable<FunctionalHoleGroupPlan> FindFunctionalHoleGroupsForPinPair(
            PinGroupPlan pinGroup,
            IList<HoleFeature> candidates,
            IList<HoleFeature> used)
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
                double bestScore = double.MaxValue;
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

        private IEnumerable<List<HoleFeature>> EnumerateHoleCombinations(IList<HoleFeature> holes, int count)
        {
            var selected = new List<HoleFeature>();
            foreach (var combination in EnumerateHoleCombinations(holes, count, 0, selected))
            {
                yield return combination;
            }
        }

        private IEnumerable<List<HoleFeature>> EnumerateHoleCombinations(
            IList<HoleFeature> holes,
            int count,
            int start,
            List<HoleFeature> selected)
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

        private bool IsValidFunctionalHoleAttachmentSet(IList<HoleFeature> holes)
        {
            if (holes.Count != 2 && holes.Count != 4)
            {
                return false;
            }

            var groups = holes
                .GroupBy(GetFunctionalHoleSpecKey)
                .ToList();

            return groups.All(g => g.Count() == 2 || g.Count() == 4);
        }

        private string GetFunctionalHoleSpecKey(HoleFeature hole)
        {
            if (hole == null)
            {
                return string.Empty;
            }

            if (hole.IsThreadHole)
            {
                return "Thread:" + (hole.ThreadCallout ?? string.Empty) + ":" + _config.FormatNumber(hole.Diameter);
            }

            return hole.HoleKind + ":" + _config.FormatNumber(hole.Diameter);
        }

        private bool FitsFunctionalHoleGrid(IList<HoleFeature> holes)
        {
            var total = holes.Count;
            var allowed = total == 4
                ? new[] { Tuple.Create(1, 4), Tuple.Create(4, 1), Tuple.Create(2, 2) }
                : total == 6
                    ? new[] { Tuple.Create(1, 6), Tuple.Create(6, 1), Tuple.Create(2, 3), Tuple.Create(3, 2) }
                    : new Tuple<int, int>[0];

            return allowed.Any(shape => FitsGridShape(holes, shape.Item1, shape.Item2));
        }

        private bool FitsGridShape(IList<HoleFeature> holes, int rowCount, int columnCount)
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

            if (occupied.Count != rowCount * columnCount)
            {
                return false;
            }

            return HasContinuousGridSpacing(rowClusters) && HasContinuousGridSpacing(columnClusters);
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

            var minGap = gaps.Min();
            var maxGap = gaps.Max();
            return maxGap <= minGap * 2.5;
        }

        private void EmitFunctionalHoleGroupLocations(IList<FunctionalHoleGroupPlan> functionalGroups)
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
                    AddHorizontalDimToSide(reference.Center, hole.Center, string.Empty, DimensionType.HoleLocation, pinGroup.HorizontalSide);
                    AddVerticalDimToSide(reference.Center, hole.Center, string.Empty, DimensionType.HoleLocation, pinGroup.VerticalSide);
                }
            }
        }

        private void EmitLooseNonPinHoleLocations(
            OutlineFeature outline,
            IList<HoleFeature> holes,
            IList<PinGroupPlan> pinGroups,
            IList<HoleFeature> groupedHoles)
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
                .Where(plan => plan.ReferencePinGroup != null && plan.ReferencePinGroup.BasePin != null && plan.AnchorHole != null)
                .OrderBy(plan => DistanceSquared(plan.ReferencePinGroup.BasePin.Center, plan.AnchorHole.Center))
                .ThenBy(plan => plan.AnchorHole.Center.X)
                .ThenBy(plan => plan.AnchorHole.Center.Y)
                .ToList();

            foreach (var plan in plans)
            {
                EmitLooseHoleMacroGroup(outline, plan);
            }
        }

        private List<LooseHoleLineGroup> BuildLooseHoleLineGroups(IList<HoleFeature> holes)
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

        private IEnumerable<List<HoleFeature>> GroupLooseHolesBySpec(IList<HoleFeature> holes)
        {
            var groups = new List<List<HoleFeature>>();
            foreach (var hole in holes.OrderBy(GetLooseHoleSpecKey).ThenBy(h => h.Center.X).ThenBy(h => h.Center.Y))
            {
                var group = groups.FirstOrDefault(g => AreSameLooseHoleSpec(g[0], hole));
                if (group == null)
                {
                    group = new List<HoleFeature>();
                    groups.Add(group);
                }

                group.Add(hole);
            }

            return groups;
        }

        private string GetLooseHoleSpecKey(HoleFeature hole)
        {
            if (hole == null)
            {
                return string.Empty;
            }

            if (hole.IsThreadHole)
            {
                return "Thread:" + (hole.ThreadCallout ?? string.Empty) + ":" + _config.FormatNumber(hole.Diameter);
            }

            return hole.HoleKind + ":" + _config.FormatNumber(hole.Diameter);
        }

        private bool AreSameLooseHoleSpec(HoleFeature a, HoleFeature b)
        {
            if (a == null || b == null || a.HoleKind != b.HoleKind)
            {
                return false;
            }

            if (a.IsThreadHole && !string.Equals(a.ThreadCallout ?? string.Empty, b.ThreadCallout ?? string.Empty, StringComparison.Ordinal))
            {
                return false;
            }

            return Math.Abs(a.Diameter - b.Diameter) <= _config.GeometryTolerance;
        }

        private IEnumerable<List<HoleFeature>> GroupLooseHolesByCoordinate(IList<HoleFeature> holes, bool horizontal)
        {
            var tolerance = GetFunctionalHoleAlignmentTolerance();
            var groups = new List<List<HoleFeature>>();
            foreach (var hole in holes.OrderBy(h => horizontal ? h.Center.Y : h.Center.X))
            {
                var coordinate = horizontal ? hole.Center.Y : hole.Center.X;
                var group = groups.FirstOrDefault(g => Math.Abs(g.Average(h => horizontal ? h.Center.Y : h.Center.X) - coordinate) <= tolerance);
                if (group == null)
                {
                    group = new List<HoleFeature>();
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

        private IEnumerable<List<HoleFeature>> SplitLooseCoordinateGroupIntoChains(IList<HoleFeature> holes, bool horizontal)
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
            var current = new List<HoleFeature> { ordered[0] };
            for (int i = 1; i < ordered.Count; i++)
            {
                var gap = GetAxisDistance(ordered[i - 1].Center, ordered[i].Center, horizontal);
                if (gap > breakGap)
                {
                    yield return current;
                    current = new List<HoleFeature>();
                }

                current.Add(ordered[i]);
            }

            yield return current;
        }

        private bool HasContinuousHoleSpacing(IList<HoleFeature> holes, bool horizontal)
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

        private List<double> GetLooseHoleAxisGaps(IList<HoleFeature> ordered, bool horizontal)
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

        private double GetAxisDistance(Point3d a, Point3d b, bool horizontal)
        {
            return Math.Abs((horizontal ? b.X : b.Y) - (horizontal ? a.X : a.Y));
        }

        private List<LooseHoleMacroGroup> BuildLooseHoleMacroGroups(IList<HoleFeature> holes, IList<LooseHoleLineGroup> lineGroups)
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

        private double GetLooseMacroGroupDistanceThreshold(IList<HoleFeature> holes)
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
            return Math.Max(medianSpacing * 2.5, Scale(_config.TextHeight * 6.0));
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

        private void EmitLooseHoleMacroGroup(OutlineFeature outline, LooseHoleLocationPlan plan)
        {
            var centerEdges = EmitLooseHoleCenterDistances(outline, plan.MacroGroup.LineGroups);
            var located = new List<HoleFeature> { plan.AnchorHole };
            ExpandLocatedLooseHolesByCenterEdges(located, centerEdges);

            EmitLooseHoleLocationPair(outline, plan.ReferencePinGroup, plan.ReferencePinGroup.BasePin.Center, plan.AnchorHole, located, forcePinReference: true);

            while (located.Count < plan.MacroGroup.Holes.Count)
            {
                var target = plan.MacroGroup.Holes
                    .Where(h => !ContainsHole(located, h))
                    .OrderBy(h => GetNearestLooseLocationDistance(h, located, plan.ReferencePinGroup.BasePin.Center))
                    .ThenBy(h => h.Center.X)
                    .ThenBy(h => h.Center.Y)
                    .FirstOrDefault();
                if (target == null)
                {
                    break;
                }

                EmitLooseHoleLocationPair(outline, plan.ReferencePinGroup, plan.ReferencePinGroup.BasePin.Center, target, located, forcePinReference: false);
                located.Add(target);
                ExpandLocatedLooseHolesByCenterEdges(located, centerEdges);
            }
        }

        private List<Tuple<HoleFeature, HoleFeature>> EmitLooseHoleCenterDistances(OutlineFeature outline, IList<LooseHoleLineGroup> lineGroups)
        {
            var edges = new List<Tuple<HoleFeature, HoleFeature>>();
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

                    var dim = CreateHoleLocationDim(ordered[i - 1].Center, ordered[i].Center, lineGroup.Horizontal, chainId);
                    AddDeferredDimensionToSide(dim, side);
                    edges.Add(Tuple.Create(ordered[i - 1], ordered[i]));
                }
            }

            return edges;
        }

        private void EmitLooseHoleLocationPair(
            OutlineFeature outline,
            PinGroupPlan referencePinGroup,
            Point3d pinReference,
            HoleFeature target,
            IList<HoleFeature> located,
            bool forcePinReference)
        {
            var horizontalReference = forcePinReference
                ? pinReference
                : ChooseLooseLocationReference(pinReference, target, located, horizontal: true);
            if (Math.Abs(horizontalReference.X - target.Center.X) > _config.GeometryTolerance)
            {
                var chainId = _nextLooseChainId++;
                var dim = CreateHoleLocationDim(horizontalReference, target.Center, horizontal: true, chainId: chainId);
                AddDeferredDimensionToSide(dim, ChooseLooseDimensionSide(outline, new[] { target }, horizontal: true));
            }

            var verticalReference = forcePinReference
                ? pinReference
                : ChooseLooseLocationReference(pinReference, target, located, horizontal: false);
            if (Math.Abs(verticalReference.Y - target.Center.Y) > _config.GeometryTolerance)
            {
                var chainId = _nextLooseChainId++;
                var dim = CreateHoleLocationDim(verticalReference, target.Center, horizontal: false, chainId: chainId);
                AddDeferredDimensionToSide(dim, ChooseLooseDimensionSide(outline, new[] { target }, horizontal: false));
            }
        }

        private Point3d ChooseLooseLocationReference(Point3d pinReference, HoleFeature target, IList<HoleFeature> located, bool horizontal)
        {
            var candidates = new List<Point3d> { pinReference };
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

        private bool LooseLocationTextFits(Point3d from, Point3d to, bool horizontal)
        {
            var span = horizontal ? Math.Abs(to.X - from.X) : Math.Abs(to.Y - from.Y);
            if (span <= _config.GeometryTolerance)
            {
                return false;
            }

            var text = _config.FormatNumber(span);
            var textLength = Math.Max(text.Length, 2) * GetDimStyleTextHeight(_dimStyleId) * 0.7;
            return textLength <= span - Math.Max(_config.GeometryTolerance, GetDimStyleTextHeight(_dimStyleId) * 0.5);
        }

        private double GetNearestLooseLocationDistance(HoleFeature target, IList<HoleFeature> located, Point3d pinReference)
        {
            var best = DistanceSquared(target.Center, pinReference);
            foreach (var hole in located)
            {
                best = Math.Min(best, DistanceSquared(target.Center, hole.Center));
            }

            return best;
        }

        private void ExpandLocatedLooseHolesByCenterEdges(IList<HoleFeature> located, IList<Tuple<HoleFeature, HoleFeature>> edges)
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

        private DeferredDim CreateHoleLocationDim(Point3d from, Point3d to, bool horizontal, int chainId)
        {
            var span = horizontal ? Math.Abs(to.X - from.X) : Math.Abs(to.Y - from.Y);
            return new DeferredDim
            {
                Rotation = horizontal ? 0.0 : Math.PI / 2.0,
                XLine1 = from,
                XLine2 = to,
                OverrideText = string.Empty,
                Span = span,
                DimType = DimensionType.HoleLocation,
                UseSegmentedExtensionLines = true,
                LooseChainId = chainId
            };
        }

        private DimSide ChooseLooseDimensionSide(OutlineFeature outline, IEnumerable<HoleFeature> holes, bool horizontal)
        {
            var points = holes.Select(h => h.Center).ToList();
            if (points.Count == 0)
            {
                return horizontal ? DimSide.Bottom : DimSide.Left;
            }

            if (horizontal)
            {
                var averageY = points.Average(p => p.Y);
                return Math.Abs(averageY - outline.MinY) <= Math.Abs(outline.MaxY - averageY)
                    ? DimSide.Bottom
                    : DimSide.Top;
            }

            var averageX = points.Average(p => p.X);
            return Math.Abs(averageX - outline.MinX) <= Math.Abs(outline.MaxX - averageX)
                ? DimSide.Left
                : DimSide.Right;
        }

        private void AddDeferredDimensionToSide(DeferredDim dim, DimSide side)
        {
            if (dim.Span <= _config.GeometryTolerance)
            {
                return;
            }

            if (side == DimSide.Bottom)
            {
                _bottomDims.Add(dim);
            }
            else if (side == DimSide.Top)
            {
                _topDims.Add(dim);
            }
            else if (side == DimSide.Right)
            {
                _rightDims.Add(dim);
            }
            else
            {
                _leftDims.Add(dim);
            }
        }

        private string GetLooseDimKey(HoleFeature a, HoleFeature b, bool horizontal)
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

        private string GetHolePointKey(HoleFeature hole)
        {
            return _config.FormatNumber(hole.Center.X) + "," + _config.FormatNumber(hole.Center.Y);
        }

        private void AddUniqueHoles(IList<HoleFeature> target, IEnumerable<HoleFeature> source)
        {
            foreach (var hole in source)
            {
                if (!ContainsHole(target, hole))
                {
                    target.Add(hole);
                }
            }
        }

        private bool ContainsHole(IEnumerable<HoleFeature> holes, HoleFeature target)
        {
            return holes != null && holes.Any(h => IsSameHole(h, target));
        }

        private DimSide ChooseHorizontalHoleSide(OutlineFeature outline, Point3d point)
        {
            var bottomDistance = Math.Abs(point.Y - outline.MinY);
            var topDistance = Math.Abs(outline.MaxY - point.Y);
            return bottomDistance <= topDistance ? DimSide.Bottom : DimSide.Top;
        }

        private DimSide ChooseVerticalHoleSide(OutlineFeature outline, Point3d point)
        {
            var leftDistance = Math.Abs(point.X - outline.MinX);
            var rightDistance = Math.Abs(outline.MaxX - point.X);
            return leftDistance <= rightDistance ? DimSide.Left : DimSide.Right;
        }

        private void DrawNonPinHolesFromOutlineEdge(OutlineFeature outline, DatumDefinition datum, IList<HoleFeature> holes)
        {
            foreach (var hole in holes.Where(h => !h.IsPinHole && !h.IsSlotPoint))
            {
                AddHorizontalDimFromXToSide(datum.BaseX, hole.Center, string.Empty, DimensionType.HoleLocation, ChooseHorizontalHoleSide(outline, hole.Center));
                AddVerticalDimFromYToSide(datum.BaseY, hole.Center, string.Empty, DimensionType.HoleLocation, ChooseVerticalHoleSide(outline, hole.Center));
            }
        }

        public void DrawSlotDimensions(OutlineFeature outline, DatumDefinition datum, IList<IList<HoleFeature>> rows, IEnumerable<SlotFeature> slots)
        {
            if (slots == null)
            {
                return;
            }

            var slotList = slots.Where(s => s != null).ToList();
            var hasPinHoles = rows != null && rows.SelectMany(r => r).Any(h => h.IsPinHole);
            if (!hasPinHoles)
            {
                foreach (var slot in slotList)
                {
                    DrawSlotCenterDistanceDimension(slot);
                }

                DrawSlotContinuousLocationsFromDatum(outline, datum, slotList);
                return;
            }

            foreach (var slot in slotList)
            {
                DrawSlotCenterDistanceDimension(slot);
            }

            DrawSlotAnchorLocations(datum, rows, slotList);
        }

        private void DrawSlotContinuousLocationsFromDatum(OutlineFeature outline, DatumDefinition datum, IList<SlotFeature> slots)
        {
            if (datum == null || slots == null || slots.Count == 0)
            {
                return;
            }

            var verticalSlots = slots.Where(IsVerticalSlot).ToList();
            var horizontalSlots = slots.Where(s => !IsVerticalSlot(s)).ToList();

            AddVerticalSlotContinuousDimensions(datum, verticalSlots);
            AddHorizontalSlotContinuousDimensions(outline, datum, horizontalSlots);
        }

        private void AddVerticalSlotContinuousDimensions(DatumDefinition datum, IList<SlotFeature> slots)
        {
            foreach (var group in GroupSlotAnchorsByCoordinate(slots, datum, p => p.Y))
            {
                var ordered = UniquePointsByCoordinate(group.OrderBy(p => p.X), p => p.X);
                AddHorizontalChainFromDatum(datum.BaseX, ordered);
                if (ordered.Count > 0)
                {
                    AddVerticalDimFromY(datum.BaseY, PickNearestPointByX(ordered, datum.BaseX), string.Empty, DimensionType.Normal);
                }
            }
        }

        private void AddHorizontalSlotContinuousDimensions(OutlineFeature outline, DatumDefinition datum, IList<SlotFeature> slots)
        {
            foreach (var group in GroupSlotAnchorsByCoordinate(slots, datum, p => p.X))
            {
                var ordered = UniquePointsByCoordinate(group.OrderBy(p => p.Y), p => p.Y);
                AddVerticalChainFromDatum(datum.BaseY, ordered);
                if (ordered.Count > 0)
                {
                    var target = PickNearestPointByY(ordered, datum.BaseY);
                    var slot = FindSlotByAnchor(slots, target);
                    if (slot != null && slot.IsSingleArcSlot && outline != null)
                    {
                        AddSingleArcSlotHorizontalDatumDimension(outline, datum, slot, target);
                    }
                    else
                    {
                        AddHorizontalDimFromX(datum.BaseX, target, string.Empty, DimensionType.Normal);
                    }
                }
            }
        }

        private void AddVerticalChainFromDatum(double datumY, IList<Point3d> ordered)
        {
            if (ordered == null || ordered.Count == 0)
            {
                return;
            }

            AddVerticalDimFromY(datumY, ordered[0], string.Empty, DimensionType.Normal);
            for (int i = 1; i < ordered.Count; i++)
            {
                AddVerticalDimFromPoint(ordered[i - 1], ordered[i], string.Empty, DimensionType.Normal);
            }
        }

        private void AddHorizontalChainFromDatum(double datumX, IList<Point3d> ordered)
        {
            if (ordered == null || ordered.Count == 0)
            {
                return;
            }

            AddHorizontalDimFromX(datumX, ordered[0], string.Empty, DimensionType.Normal);
            for (int i = 1; i < ordered.Count; i++)
            {
                AddHorizontalDimFromPoint(ordered[i - 1], ordered[i], string.Empty, DimensionType.Normal);
            }
        }

        private List<List<Point3d>> GroupSlotCentersByCoordinate(IEnumerable<SlotFeature> slots, Func<Point3d, double> coordinate)
        {
            var groups = new List<List<Point3d>>();
            if (slots == null)
            {
                return groups;
            }

            foreach (var point in slots.SelectMany(GetSlotCenterPoints))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(coordinate(g[0]) - coordinate(point)) <= _config.GeometryTolerance);
                if (group == null)
                {
                    group = new List<Point3d>();
                    groups.Add(group);
                }

                group.Add(point);
            }

            return groups;
        }

        private List<List<Point3d>> GroupSlotAnchorsByCoordinate(
            IEnumerable<SlotFeature> slots,
            DatumDefinition datum,
            Func<Point3d, double> coordinate)
        {
            var groups = new List<List<Point3d>>();
            if (slots == null || datum == null)
            {
                return groups;
            }

            foreach (var point in slots.Select(s => PickSlotAnchorPoint(s, datum)))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(coordinate(g[0]) - coordinate(point)) <= _config.GeometryTolerance);
                if (group == null)
                {
                    group = new List<Point3d>();
                    groups.Add(group);
                }

                group.Add(point);
            }

            return groups;
        }

        private List<Point3d> UniquePointsByCoordinate(IEnumerable<Point3d> points, Func<Point3d, double> coordinate)
        {
            var unique = new List<Point3d>();
            foreach (var point in points)
            {
                if (unique.Count == 0 || Math.Abs(coordinate(unique[unique.Count - 1]) - coordinate(point)) > _config.GeometryTolerance)
                {
                    unique.Add(point);
                }
            }

            return unique;
        }

        private SlotFeature FindSlotByAnchor(IEnumerable<SlotFeature> slots, Point3d anchor)
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
            OutlineFeature outline,
            DatumDefinition datum,
            SlotFeature slot,
            Point3d center)
        {
            var from = FindOutlinePointAtX(outline, datum.BaseX, center.Y);
            var to = GetSingleArcSlotHorizontalGripPoint(slot, from.Y);
            var span = Math.Abs(center.X - datum.BaseX);
            if (span <= _config.GeometryTolerance)
            {
                return;
            }

            AddHorizontalDimFromPoint(from, to, _config.FormatNumber(span), DimensionType.Normal);
        }

        private Point3d FindOutlinePointAtX(OutlineFeature outline, double x, double preferredY)
        {
            var candidates = new List<Point2d>();
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
                candidates.Add(new Point2d(x, y));
            }

            var best = candidates
                .OrderBy(p => Math.Abs(p.Y - preferredY))
                .FirstOrDefault();
            return ToPoint3d(best, x, preferredY);
        }

        private Point3d GetSingleArcSlotHorizontalGripPoint(SlotFeature slot, double preferredY)
        {
            var upper = new Point3d(slot.FirstCenter.X, slot.FirstCenter.Y + slot.Radius, 0.0);
            var lower = new Point3d(slot.FirstCenter.X, slot.FirstCenter.Y - slot.Radius, 0.0);
            return Math.Abs(upper.Y - preferredY) <= Math.Abs(lower.Y - preferredY) ? upper : lower;
        }

        private static IEnumerable<Point3d> GetSlotCenterPoints(SlotFeature slot)
        {
            if (slot.IsSingleArcSlot)
            {
                yield return new Point3d(slot.FirstCenter.X, slot.FirstCenter.Y, 0.0);
                yield break;
            }

            yield return new Point3d(slot.FirstCenter.X, slot.FirstCenter.Y, 0.0);
            yield return new Point3d(slot.SecondCenter.X, slot.SecondCenter.Y, 0.0);
        }

        private static bool IsVerticalSlot(SlotFeature slot)
        {
            if (slot.IsSingleArcSlot)
            {
                return slot.IsVertical;
            }

            return Math.Abs(slot.FirstCenter.Y - slot.SecondCenter.Y) >= Math.Abs(slot.FirstCenter.X - slot.SecondCenter.X);
        }

        private static Point3d PickNearestPointByY(IEnumerable<Point3d> points, double y)
        {
            return points.OrderBy(p => Math.Abs(p.Y - y)).First();
        }

        private static Point3d PickNearestPointByX(IEnumerable<Point3d> points, double x)
        {
            return points.OrderBy(p => Math.Abs(p.X - x)).First();
        }

        private void DrawSlotCenterDistanceDimension(SlotFeature slot)
        {
            if (slot == null || slot.CenterDistance <= _config.GeometryTolerance)
            {
                return;
            }

            var first = new Point3d(slot.FirstCenter.X, slot.FirstCenter.Y, 0.0);
            var second = new Point3d(slot.SecondCenter.X, slot.SecondCenter.Y, 0.0);
            var dx = Math.Abs(first.X - second.X);
            var dy = Math.Abs(first.Y - second.Y);
            if (dx <= _config.GeometryTolerance && dy <= _config.GeometryTolerance)
            {
                return;
            }

            if (dx >= dy)
            {
                AddHorizontalDimFromPoint(first, second, string.Empty, DimensionType.Normal);
            }
            else
            {
                AddVerticalDimFromPoint(first, second, string.Empty, DimensionType.Normal);
            }
        }

        private void DrawSlotAnchorLocations(DatumDefinition datum, IList<IList<HoleFeature>> rows, IList<SlotFeature> slots)
        {
            if (datum == null || rows == null || slots == null || slots.Count == 0)
            {
                return;
            }

            var holes = rows.SelectMany(r => r).ToList();
            var pinGroups = BuildPinGroupPlan(holes, datum.DatumHole);
            if (pinGroups.Count == 0)
            {
                foreach (var slot in slots)
                {
                    var anchor = PickSlotAnchorPoint(slot, datum);
                    AddHorizontalDimFromX(datum.BaseX, anchor, string.Empty, DimensionType.Normal);
                    AddVerticalDimFromY(datum.BaseY, anchor, string.Empty, DimensionType.Normal);
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

                AddHorizontalDim(reference.Pin.Center, anchor, string.Empty, DimensionType.Normal);
                AddVerticalDim(reference.Pin.Center, anchor, string.Empty, DimensionType.Normal);
            }
        }

        private Point3d PickSlotAnchorPoint(SlotFeature slot, DatumDefinition datum)
        {
            var first = new Point3d(slot.FirstCenter.X, slot.FirstCenter.Y, 0.0);
            if (slot.IsSingleArcSlot)
            {
                return first;
            }

            var second = new Point3d(slot.SecondCenter.X, slot.SecondCenter.Y, 0.0);
            var dx = Math.Abs(first.X - second.X);
            var dy = Math.Abs(first.Y - second.Y);

            if (dx >= dy)
            {
                return Math.Abs(first.X - datum.BaseX) <= Math.Abs(second.X - datum.BaseX) ? first : second;
            }

            return Math.Abs(first.Y - datum.BaseY) <= Math.Abs(second.Y - datum.BaseY) ? first : second;
        }

        private void AddHorizontalDim(Point3d from, Point3d to, string overrideText, DimensionType dimType)
        {
            AddHorizontalDimFromPoint(new Point3d(from.X, from.Y, from.Z), to, overrideText, dimType);
        }

        private void AddHorizontalDimToSide(Point3d from, Point3d to, string overrideText, DimensionType dimType, DimSide side)
        {
            AddHorizontalDimFromPointToSide(new Point3d(from.X, from.Y, from.Z), to, overrideText, dimType, side);
        }

        private void AddHorizontalDimFromX(double fromX, Point3d to, string overrideText, DimensionType dimType)
        {
            AddHorizontalDimFromPoint(new Point3d(fromX, to.Y, 0.0), to, overrideText, dimType);
        }

        private void AddHorizontalDimFromXToSide(double fromX, Point3d to, string overrideText, DimensionType dimType, DimSide side)
        {
            AddHorizontalDimFromPointToSide(new Point3d(fromX, to.Y, 0.0), to, overrideText, dimType, side);
        }

        private void AddHorizontalDimFromPoint(Point3d from, Point3d to, string overrideText, DimensionType dimType)
        {
            AddHorizontalDimFromPointToSide(from, to, overrideText, dimType, DimSide.Bottom);
        }

        private void AddHorizontalDimFromPointToSide(Point3d from, Point3d to, string overrideText, DimensionType dimType, DimSide side)
        {
            var span = Math.Abs(to.X - from.X);
            if (span <= _config.GeometryTolerance)
            {
                return;
            }

            var dim = new DeferredDim
            {
                Rotation = 0.0,
                XLine1 = from,
                XLine2 = to,
                OverrideText = overrideText ?? string.Empty,
                Span = span,
                DimType = dimType,
                UseSegmentedExtensionLines = true
            };

            if (side == DimSide.Top)
            {
                _topDims.Add(dim);
                return;
            }

            _bottomDims.Add(dim);
        }

        private void AddVerticalDim(Point3d from, Point3d to, string overrideText, DimensionType dimType)
        {
            AddVerticalDimFromPoint(new Point3d(from.X, from.Y, from.Z), to, overrideText, dimType);
        }

        private void AddVerticalDimToSide(Point3d from, Point3d to, string overrideText, DimensionType dimType, DimSide side)
        {
            AddVerticalDimFromPointToSide(new Point3d(from.X, from.Y, from.Z), to, overrideText, dimType, side);
        }

        private void AddVerticalDimFromY(double fromY, Point3d to, string overrideText, DimensionType dimType)
        {
            AddVerticalDimFromPoint(new Point3d(to.X, fromY, 0.0), to, overrideText, dimType);
        }

        private void AddVerticalDimFromYToSide(double fromY, Point3d to, string overrideText, DimensionType dimType, DimSide side)
        {
            AddVerticalDimFromPointToSide(new Point3d(to.X, fromY, 0.0), to, overrideText, dimType, side);
        }

        private void AddVerticalDimFromPoint(Point3d from, Point3d to, string overrideText, DimensionType dimType)
        {
            AddVerticalDimFromPointToSide(from, to, overrideText, dimType, DimSide.Left);
        }

        private void AddVerticalDimFromPointToSide(Point3d from, Point3d to, string overrideText, DimensionType dimType, DimSide side)
        {
            var span = Math.Abs(to.Y - from.Y);
            if (span <= _config.GeometryTolerance)
            {
                return;
            }

            var dim = new DeferredDim
            {
                Rotation = Math.PI / 2.0,
                XLine1 = from,
                XLine2 = new Point3d(to.X, to.Y, to.Z),
                OverrideText = overrideText ?? string.Empty,
                Span = span,
                DimType = dimType,
                UseSegmentedExtensionLines = true
            };

            side = ChooseVerticalNormalDimensionSide(dim, side);
            if (side == DimSide.Right)
            {
                _rightDims.Add(dim);
                return;
            }

            _leftDims.Add(dim);
        }

        private DimSide ChooseVerticalNormalDimensionSide(DeferredDim dim, DimSide preferredSide)
        {
            if (dim.DimType != DimensionType.HoleLocation)
            {
                return preferredSide;
            }

            if (preferredSide != DimSide.Left && preferredSide != DimSide.Right)
            {
                return preferredSide;
            }

            var oppositeSide = preferredSide == DimSide.Left ? DimSide.Right : DimSide.Left;
            var preferredDims = preferredSide == DimSide.Left ? _leftDims : _rightDims;
            var oppositeDims = oppositeSide == DimSide.Left ? _leftDims : _rightDims;
            var preferredScore = ScoreVerticalSideCrowding(dim, preferredDims);
            var oppositeScore = ScoreVerticalSideCrowding(dim, oppositeDims);
            var switchThreshold = IsShortVerticalDimension(dim) ? 1 : 3;
            if (preferredScore - oppositeScore >= switchThreshold)
            {
                return oppositeSide;
            }

            return preferredSide;
        }

        private int ScoreVerticalSideCrowding(DeferredDim candidate, IList<DeferredDim> existingDims)
        {
            var score = 0;
            var candidateArrow = GetVerticalInterval(candidate);
            var candidateText = EstimateVerticalTextInterval(candidate);
            var candidateCenter = (candidateArrow.A + candidateArrow.B) / 2.0;
            var shortCandidate = IsShortVerticalDimension(candidate);
            foreach (var existing in existingDims)
            {
                var existingArrow = GetVerticalInterval(existing);
                var existingText = EstimateVerticalTextInterval(existing);
                var existingCenter = (existingArrow.A + existingArrow.B) / 2.0;
                var obstacleWeight = existing.DimType == DimensionType.Normal || existing.DimType == DimensionType.HoleLocation ? 1 : 2;

                if (IntervalsOverlap(candidateArrow, existingArrow, _config.GeometryTolerance))
                {
                    score += (shortCandidate || IsShortVerticalDimension(existing) ? 2 : 1) * obstacleWeight;
                }

                if (IntervalsOverlap(candidateText, existingText, Scale(_config.TextHeight * 0.8)))
                {
                    score += (shortCandidate || IsShortVerticalDimension(existing) ? 4 : 2) * obstacleWeight;
                }

                if (Math.Abs(candidateCenter - existingCenter) <= Scale(_config.TextHeight * 2.5))
                {
                    score += (shortCandidate ? 2 : 1) * obstacleWeight;
                }
            }

            return score;
        }

        private (double A, double B) GetVerticalInterval(DeferredDim dim)
        {
            return (Math.Min(dim.XLine1.Y, dim.XLine2.Y), Math.Max(dim.XLine1.Y, dim.XLine2.Y));
        }

        private (double A, double B) EstimateVerticalTextInterval(DeferredDim dim)
        {
            var interval = GetVerticalInterval(dim);
            var center = (interval.A + interval.B) / 2.0;
            var text = string.IsNullOrEmpty(dim.OverrideText)
                ? _config.FormatNumber(dim.Span)
                : dim.OverrideText;
            var textLength = Math.Max(text.Length, 2) * Scale(_config.TextHeight) * 0.75;
            return (center - textLength / 2.0, center + textLength / 2.0);
        }

        private bool IsShortVerticalDimension(DeferredDim dim)
        {
            return dim.Span <= Scale(_config.TextHeight * 3.0);
        }

        private bool IntervalsOverlap((double A, double B) first, (double A, double B) second, double tolerance)
        {
            return first.A <= second.B + tolerance && second.A <= first.B + tolerance;
        }

        private bool IsSameHole(HoleFeature a, HoleFeature b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return DistanceSquared(a.Center, b.Center) <= _config.GeometryTolerance * _config.GeometryTolerance;
        }

        private static double DistanceSquared(Point3d a, Point3d b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return dx * dx + dy * dy;
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

        private void DrawTopStepWidth(OutlineFeature outline)
        {
            var ignoredPoints = new List<Point2d>();
            List<DeferredDim> candidates;
            while (true)
            {
                var points = BuildTopSideHorizontalStructurePoints(outline, ignoredPoints);
                if (points.Count < 2)
                {
                    return;
                }

                candidates = BuildHorizontalWidthCandidates(points);
                var crossingPoints = GetTopExtensionCrossingPoints(candidates, outline);
                if (crossingPoints.Count == 0)
                {
                    break;
                }

                if (!AddIgnoredPoints(ignoredPoints, crossingPoints))
                {
                    return;
                }
            }

            RemoveLongestTopExtensionCandidate(candidates, outline);
            foreach (var dim in candidates)
            {
                if (!IsTopSideHorizontalStructureCandidate(dim, outline, ignoredPoints))
                {
                    continue;
                }

                _topDims.Add(dim);
            }
        }

        private void DrawLowerRightStepWidth(OutlineFeature outline)
        {
            var ignoredPoints = new List<Point2d>();
            List<DeferredDim> candidates;
            while (true)
            {
                var points = BuildBottomSideHorizontalStructurePoints(outline, ignoredPoints);
                if (points.Count < 2)
                {
                    return;
                }

                candidates = BuildHorizontalWidthCandidates(points);
                var crossingPoints = GetBottomExtensionCrossingPoints(candidates, outline);
                if (crossingPoints.Count == 0)
                {
                    break;
                }

                if (!AddIgnoredPoints(ignoredPoints, crossingPoints))
                {
                    return;
                }
            }

            RemoveLongestBottomExtensionCandidate(candidates, outline);
            foreach (var dim in candidates)
            {
                if (!IsBottomSideHorizontalStructureCandidate(dim, outline, ignoredPoints))
                {
                    continue;
                }

                _bottomDims.Add(dim);
            }
        }

        private List<DeferredDim> BuildHorizontalWidthCandidates(IList<Point2d> points)
        {
            var candidates = new List<DeferredDim>();
            for (int i = 1; i < points.Count; i++)
            {
                var leftPoint = points[i - 1];
                var rightPoint = points[i];
                var span = Math.Abs(rightPoint.X - leftPoint.X);
                if (span <= _config.GeometryTolerance)
                {
                    continue;
                }

                candidates.Add(new DeferredDim
                {
                    Rotation = 0.0,
                    XLine1 = new Point3d(leftPoint.X, leftPoint.Y, 0.0),
                    XLine2 = new Point3d(rightPoint.X, rightPoint.Y, 0.0),
                    OverrideText = string.Empty,
                    Span = span,
                    DimType = DimensionType.Normal
                });
            }

            return candidates;
        }

        private List<Point2d> BuildTopSideHorizontalStructurePoints(OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            var tolerance = _config.GeometryTolerance;
            var groups = new List<List<OutlineSegment>>();
            foreach (var segment in outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthY > tolerance))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(g[0].MinX - segment.MinX) <= tolerance);
                if (group == null)
                {
                    group = new List<OutlineSegment>();
                    groups.Add(group);
                }

                group.Add(segment);
            }

            var points = groups
                .Select(g => GetTopMostPoint(g, ignoredPoints))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            AddTopInclinedEndpointStructurePoints(points, outline, ignoredPoints);

            return points
                .OrderBy(p => p.X)
                .ToList();
        }

        private List<Point2d> BuildBottomSideHorizontalStructurePoints(OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            var tolerance = _config.GeometryTolerance;
            var groups = new List<List<OutlineSegment>>();
            foreach (var segment in outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthY > tolerance))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(g[0].MinX - segment.MinX) <= tolerance);
                if (group == null)
                {
                    group = new List<OutlineSegment>();
                    groups.Add(group);
                }

                group.Add(segment);
            }

            var points = groups
                .Select(g => GetBottomMostPoint(g, ignoredPoints))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            AddBottomInclinedEndpointStructurePoints(points, outline, ignoredPoints);

            return points
                .OrderBy(p => p.X)
                .ToList();
        }

        private Point2d? GetTopMostPoint(IEnumerable<OutlineSegment> segments, IList<Point2d> ignoredPoints)
        {
            var point = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => !ContainsPoint(ignoredPoints, p))
                .OrderByDescending(p => p.Y)
                .ThenBy(p => p.X)
                .Select(p => (Point2d?)p)
                .FirstOrDefault();

            return point;
        }

        private Point2d? GetBottomMostPoint(IEnumerable<OutlineSegment> segments, IList<Point2d> ignoredPoints)
        {
            var point = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => !ContainsPoint(ignoredPoints, p))
                .OrderBy(p => p.Y)
                .ThenBy(p => p.X)
                .Select(p => (Point2d?)p)
                .FirstOrDefault();

            return point;
        }

        private void ResolveHorizontalStepBoundarySpan(
            OutlineFeature outline,
            OutlineSegment segment,
            DimSide side,
            out Point2d leftPoint,
            out Point2d rightPoint)
        {
            leftPoint = segment.Start.X <= segment.End.X ? segment.Start : segment.End;
            rightPoint = segment.Start.X > segment.End.X ? segment.Start : segment.End;

            if (side == DimSide.Bottom)
            {
                TryResolveBottomProtrusionWidthSpan(outline, segment, ref leftPoint, ref rightPoint);
            }
        }

        private bool TryResolveBottomProtrusionWidthSpan(
            OutlineFeature outline,
            OutlineSegment segment,
            ref Point2d leftPoint,
            ref Point2d rightPoint)
        {
            var tolerance = _config.GeometryTolerance;
            var lowerLimit = outline.MinY + outline.Height * 0.45;
            var localWidthLimit = Math.Max(outline.Width * 0.35, Scale(_config.ArrowSize * 10.0));
            var currentSpan = Math.Abs(rightPoint.X - leftPoint.X);
            var baseY = segment.MinY;

            var verticals = outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Where(s => s.MinY <= lowerLimit + tolerance)
                .Where(s => s.MinX >= outline.MinX - tolerance)
                .Where(s => s.MinX <= outline.MinX + localWidthLimit + tolerance)
                .OrderBy(s => s.MinX)
                .ToList();
            if (verticals.Count < 2)
            {
                return false;
            }

            var left = verticals.First();
            var right = verticals
                .Where(s => s.MinX > left.MinX + tolerance)
                .OrderBy(s => s.MinX)
                .FirstOrDefault();
            if (right == null)
            {
                return false;
            }

            var candidateSpan = Math.Abs(right.MinX - left.MinX);
            if (candidateSpan <= _config.GeometryTolerance || candidateSpan > currentSpan + Math.Max(tolerance, 2.0))
            {
                return false;
            }

            leftPoint = new Point2d(left.MinX, baseY);
            rightPoint = new Point2d(right.MinX, baseY);
            return true;
        }

        private bool IsHorizontalProtrusionStep(OutlineSegment segment, OutlineFeature outline, DimSide side)
        {
            if (GetNearestHorizontalDimSide(segment, outline) != side)
            {
                return false;
            }

            var maxLocalWidth = Math.Max(outline.Width * 0.35, Scale(_config.ArrowSize * 10.0));
            if (segment.LengthX > maxLocalWidth + _config.GeometryTolerance)
            {
                return false;
            }

            return TouchesHorizontalOuterBoundary(segment, outline)
                && HasHorizontalStepReturn(segment, outline);
        }

        private int ScoreHorizontalProtrusionStep(OutlineSegment segment, OutlineFeature outline, DimSide side)
        {
            var score = 0;
            if (TouchesHorizontalOuterBoundary(segment, outline))
            {
                score += 4;
            }

            if (HasHorizontalStepReturn(segment, outline))
            {
                score += 4;
            }

            if (side == DimSide.Bottom)
            {
                score += Math.Abs(segment.MinY - outline.MinY) <= outline.Height * 0.25 ? 2 : 0;
            }
            else
            {
                score += Math.Abs(segment.MaxY - outline.MaxY) <= outline.Height * 0.25 ? 2 : 0;
            }

            return score;
        }

        private bool TouchesHorizontalOuterBoundary(OutlineSegment segment, OutlineFeature outline)
        {
            var tolerance = Math.Max(_config.GeometryTolerance, 0.2);
            return Math.Abs(segment.MinX - outline.MinX) <= tolerance
                || Math.Abs(segment.MaxX - outline.MaxX) <= tolerance
                || EndpointConnectsToBoundary(segment.Start, outline, horizontalBoundary: true)
                || EndpointConnectsToBoundary(segment.End, outline, horizontalBoundary: true);
        }

        private bool HasHorizontalStepReturn(OutlineSegment segment, OutlineFeature outline)
        {
            return EndpointHasStructuralReturn(segment.Start, segment, outline)
                || EndpointHasStructuralReturn(segment.End, segment, outline);
        }

        private bool EndpointHasStructuralReturn(Point2d endpoint, OutlineSegment source, OutlineFeature outline)
        {
            if (outline.Segments.Any(s =>
                !ReferenceEquals(s, source)
                && SegmentTouchesPoint(s, endpoint)
                && !s.IsHorizontal(_config.GeometryTolerance)))
            {
                return true;
            }

            if (outline.Chamfers.Any(chamfer =>
                PointsEqual(chamfer.StartPoint, endpoint)
                || PointsEqual(chamfer.EndPoint, endpoint)))
            {
                return true;
            }

            return outline.Fillets.Any(fillet =>
                PointsEqual(fillet.StartPoint, endpoint)
                || PointsEqual(fillet.EndPoint, endpoint));
        }

        private bool EndpointConnectsToBoundary(Point2d endpoint, OutlineFeature outline, bool horizontalBoundary)
        {
            foreach (var segment in outline.Segments)
            {
                if (!SegmentTouchesPoint(segment, endpoint))
                {
                    continue;
                }

                if (horizontalBoundary)
                {
                    if (Math.Abs(segment.MinX - outline.MinX) <= _config.GeometryTolerance
                        || Math.Abs(segment.MaxX - outline.MaxX) <= _config.GeometryTolerance)
                    {
                        return true;
                    }
                }
                else if (Math.Abs(segment.MinY - outline.MinY) <= _config.GeometryTolerance
                    || Math.Abs(segment.MaxY - outline.MaxY) <= _config.GeometryTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private void DrawRightStepHeight(OutlineFeature outline)
        {
            var ignoredPoints = new List<Point2d>();
            List<DeferredDim> candidates;
            while (true)
            {
                var points = BuildLeftSideVerticalStructurePoints(outline, ignoredPoints);
                if (points.Count < 2)
                {
                    return;
                }

                candidates = BuildLeftSideVerticalHeightCandidates(points);
                var crossingPoints = GetLeftExtensionCrossingPoints(candidates, outline);
                if (crossingPoints.Count == 0)
                {
                    break;
                }

                if (!AddIgnoredPoints(ignoredPoints, crossingPoints))
                {
                    return;
                }
            }

            RemoveLongestExtensionCandidate(candidates, outline);
            foreach (var dim in candidates)
            {
                _leftDims.Add(dim);
            }
        }

        private void DrawRightSideStepHeight(OutlineFeature outline)
        {
            var ignoredPoints = new List<Point2d>();
            List<DeferredDim> candidates;
            while (true)
            {
                var points = BuildRightSideVerticalStructurePoints(outline, ignoredPoints);
                if (points.Count < 2)
                {
                    return;
                }

                candidates = BuildLeftSideVerticalHeightCandidates(points);
                var crossingPoints = GetRightExtensionCrossingPoints(candidates, outline);
                if (crossingPoints.Count == 0)
                {
                    break;
                }

                if (!AddIgnoredPoints(ignoredPoints, crossingPoints))
                {
                    return;
                }
            }

            RemoveLongestRightExtensionCandidate(candidates, outline);
            foreach (var dim in candidates)
            {
                if (!IsRightSideVerticalStructureCandidate(dim, outline, ignoredPoints))
                {
                    continue;
                }

                _rightDims.Add(dim);
            }
        }

        private List<DeferredDim> BuildLeftSideVerticalHeightCandidates(IList<Point2d> points)
        {
            var candidates = new List<DeferredDim>();
            for (int i = 1; i < points.Count; i++)
            {
                var upperPoint = points[i - 1];
                var lowerPoint = points[i];
                var span = Math.Abs(upperPoint.Y - lowerPoint.Y);
                if (span <= _config.GeometryTolerance)
                {
                    continue;
                }

                candidates.Add(new DeferredDim
                {
                    Rotation = Math.PI / 2.0,
                    XLine1 = new Point3d(lowerPoint.X, lowerPoint.Y, 0.0),
                    XLine2 = new Point3d(upperPoint.X, upperPoint.Y, 0.0),
                    OverrideText = string.Empty,
                    Span = span,
                    DimType = DimensionType.Normal
                });
            }

            return candidates;
        }

        private List<Point2d> BuildLeftSideVerticalStructurePoints(OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            var tolerance = _config.GeometryTolerance;
            var groups = new List<List<OutlineSegment>>();
            foreach (var segment in outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthX > tolerance))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(g[0].MinY - segment.MinY) <= tolerance);
                if (group == null)
                {
                    group = new List<OutlineSegment>();
                    groups.Add(group);
                }

                group.Add(segment);
            }

            var points = groups
                .Select(g => GetLeftMostPoint(g, ignoredPoints))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            AddSideInclinedEndpointStructurePoints(points, outline, ignoredPoints, DimSide.Left);

            return points
                .OrderByDescending(p => p.Y)
                .ToList();
        }

        private Point2d? GetLeftMostPoint(IEnumerable<OutlineSegment> segments, IList<Point2d> ignoredPoints)
        {
            var point = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => !ContainsPoint(ignoredPoints, p))
                .OrderBy(p => p.X)
                .ThenBy(p => p.Y)
                .Select(p => (Point2d?)p)
                .FirstOrDefault();

            return point;
        }

        private List<Point2d> BuildRightSideVerticalStructurePoints(OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            var tolerance = _config.GeometryTolerance;
            var groups = new List<List<OutlineSegment>>();
            foreach (var segment in outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => s.LengthX > tolerance))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(g[0].MinY - segment.MinY) <= tolerance);
                if (group == null)
                {
                    group = new List<OutlineSegment>();
                    groups.Add(group);
                }

                group.Add(segment);
            }

            var points = groups
                .Select(g => GetRightMostPoint(g, ignoredPoints))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            AddSideInclinedEndpointStructurePoints(points, outline, ignoredPoints, DimSide.Right);

            return points
                .OrderByDescending(p => p.Y)
                .ToList();
        }

        private Point2d? GetRightMostPoint(IEnumerable<OutlineSegment> segments, IList<Point2d> ignoredPoints)
        {
            var point = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => !ContainsPoint(ignoredPoints, p))
                .OrderByDescending(p => p.X)
                .ThenBy(p => p.Y)
                .Select(p => (Point2d?)p)
                .FirstOrDefault();

            return point;
        }

        private bool IsRightSideVerticalStructureCandidate(DeferredDim dim, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            return IsCurrentRightSideStructurePoint(new Point2d(dim.XLine1.X, dim.XLine1.Y), outline, ignoredPoints)
                && IsCurrentRightSideStructurePoint(new Point2d(dim.XLine2.X, dim.XLine2.Y), outline, ignoredPoints);
        }

        private bool IsCurrentRightSideStructurePoint(Point2d point, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            if (IsSideInclinedEndpointStructurePoint(point, outline, ignoredPoints, DimSide.Right))
            {
                return true;
            }

            var tolerance = _config.GeometryTolerance;
            var levelSegments = outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthX > tolerance)
                .Where(s => Math.Abs(s.MinY - point.Y) <= tolerance)
                .ToList();
            if (levelSegments.Count == 0)
            {
                return false;
            }

            var rightMost = GetRightMostPoint(levelSegments, ignoredPoints);
            return rightMost.HasValue && PointsEqual(rightMost.Value, point);
        }

        private void AddSideInclinedEndpointStructurePoints(
            IList<Point2d> points,
            OutlineFeature outline,
            IList<Point2d> ignoredPoints,
            DimSide side)
        {
            var tolerance = _config.GeometryTolerance;
            foreach (var segment in outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsSideInnerGrooveChamferSegment(s, outline, side)))
            {
                AddSideInclinedEndpointStructurePoint(points, segment.Start, outline, ignoredPoints);
                AddSideInclinedEndpointStructurePoint(points, segment.End, outline, ignoredPoints);
            }
        }

        private void AddSideInclinedEndpointStructurePoint(
            IList<Point2d> points,
            Point2d point,
            OutlineFeature outline,
            IList<Point2d> ignoredPoints)
        {
            if (ContainsPoint(points, point)
                || ContainsPoint(ignoredPoints, point)
                || IsEnvelopeHorizontalSidePoint(point, outline))
            {
                return;
            }

            points.Add(point);
        }

        private bool IsSideInclinedEndpointStructurePoint(Point2d point, OutlineFeature outline, IList<Point2d> ignoredPoints, DimSide side)
        {
            if (ContainsPoint(ignoredPoints, point) || IsEnvelopeHorizontalSidePoint(point, outline))
            {
                return false;
            }

            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsSideInnerGrooveChamferSegment(s, outline, side))
                .Any(s => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
        }

        private bool IsTopSideHorizontalStructureCandidate(DeferredDim dim, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            return IsCurrentTopSideStructurePoint(new Point2d(dim.XLine1.X, dim.XLine1.Y), outline, ignoredPoints)
                && IsCurrentTopSideStructurePoint(new Point2d(dim.XLine2.X, dim.XLine2.Y), outline, ignoredPoints);
        }

        private bool IsBottomSideHorizontalStructureCandidate(DeferredDim dim, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            return IsCurrentBottomSideStructurePoint(new Point2d(dim.XLine1.X, dim.XLine1.Y), outline, ignoredPoints)
                && IsCurrentBottomSideStructurePoint(new Point2d(dim.XLine2.X, dim.XLine2.Y), outline, ignoredPoints);
        }

        private bool IsCurrentTopSideStructurePoint(Point2d point, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            if (IsTopInclinedEndpointStructurePoint(point, outline, ignoredPoints))
            {
                return true;
            }

            var tolerance = _config.GeometryTolerance;
            var levelSegments = outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthY > tolerance)
                .Where(s => Math.Abs(s.MinX - point.X) <= tolerance)
                .ToList();
            if (levelSegments.Count == 0)
            {
                return false;
            }

            var topMost = GetTopMostPoint(levelSegments, ignoredPoints);
            return topMost.HasValue && PointsEqual(topMost.Value, point);
        }

        private void AddTopInclinedEndpointStructurePoints(IList<Point2d> points, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            var tolerance = _config.GeometryTolerance;
            foreach (var segment in outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsInnerGrooveChamferSegment(s, outline, isTopSide: true)))
            {
                AddTopInclinedEndpointStructurePoint(points, segment.Start, outline, ignoredPoints);
                AddTopInclinedEndpointStructurePoint(points, segment.End, outline, ignoredPoints);
            }
        }

        private void AddTopInclinedEndpointStructurePoint(
            IList<Point2d> points,
            Point2d point,
            OutlineFeature outline,
            IList<Point2d> ignoredPoints)
        {
            if (ContainsPoint(points, point)
                || ContainsPoint(ignoredPoints, point)
                || IsEnvelopeSidePoint(point, outline))
            {
                return;
            }

            points.Add(point);
        }

        private bool IsTopInclinedEndpointStructurePoint(Point2d point, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            if (ContainsPoint(ignoredPoints, point) || IsEnvelopeSidePoint(point, outline))
            {
                return false;
            }

            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsInnerGrooveChamferSegment(s, outline, isTopSide: true))
                .Any(s => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
        }

        private bool IsCurrentBottomSideStructurePoint(Point2d point, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            if (IsBottomInclinedEndpointStructurePoint(point, outline, ignoredPoints))
            {
                return true;
            }

            var tolerance = _config.GeometryTolerance;
            var levelSegments = outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthY > tolerance)
                .Where(s => Math.Abs(s.MinX - point.X) <= tolerance)
                .ToList();
            if (levelSegments.Count == 0)
            {
                return false;
            }

            var bottomMost = GetBottomMostPoint(levelSegments, ignoredPoints);
            return bottomMost.HasValue && PointsEqual(bottomMost.Value, point);
        }

        private void AddBottomInclinedEndpointStructurePoints(IList<Point2d> points, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            var tolerance = _config.GeometryTolerance;
            foreach (var segment in outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsInnerGrooveChamferSegment(s, outline, isTopSide: false)))
            {
                AddBottomInclinedEndpointStructurePoint(points, segment.Start, outline, ignoredPoints);
                AddBottomInclinedEndpointStructurePoint(points, segment.End, outline, ignoredPoints);
            }
        }

        private void AddBottomInclinedEndpointStructurePoint(
            IList<Point2d> points,
            Point2d point,
            OutlineFeature outline,
            IList<Point2d> ignoredPoints)
        {
            if (ContainsPoint(points, point)
                || ContainsPoint(ignoredPoints, point)
                || IsEnvelopeSidePoint(point, outline))
            {
                return;
            }

            points.Add(point);
        }

        private bool IsBottomInclinedEndpointStructurePoint(Point2d point, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            if (ContainsPoint(ignoredPoints, point) || IsEnvelopeSidePoint(point, outline))
            {
                return false;
            }

            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsInnerGrooveChamferSegment(s, outline, isTopSide: false))
                .Any(s => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
        }

        private List<Point2d> GetLeftExtensionCrossingPoints(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            var points = new List<Point2d>();
            foreach (var dim in candidates)
            {
                AddCrossingPoint(points, dim.XLine1, outline);
                AddCrossingPoint(points, dim.XLine2, outline);
                AddSideDirectionalInclinedEndpoint(points, dim.XLine1, outline, DimSide.Left);
                AddSideDirectionalInclinedEndpoint(points, dim.XLine2, outline, DimSide.Left);
            }

            return points;
        }

        private List<Point2d> GetRightExtensionCrossingPoints(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            var points = new List<Point2d>();
            foreach (var dim in candidates)
            {
                AddRightCrossingPoint(points, dim.XLine1, outline);
                AddRightCrossingPoint(points, dim.XLine2, outline);
                AddSideDirectionalInclinedEndpoint(points, dim.XLine1, outline, DimSide.Right);
                AddSideDirectionalInclinedEndpoint(points, dim.XLine2, outline, DimSide.Right);
            }

            return points;
        }

        private List<Point2d> GetTopExtensionCrossingPoints(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            var points = new List<Point2d>();
            foreach (var dim in candidates)
            {
                AddTopCrossingPoint(points, dim.XLine1, outline);
                AddTopCrossingPoint(points, dim.XLine2, outline);
                AddDirectionalInclinedEndpoint(points, dim.XLine1, outline, invertDirection: true);
                AddDirectionalInclinedEndpoint(points, dim.XLine2, outline, invertDirection: true);
            }

            return points;
        }

        private List<Point2d> GetBottomExtensionCrossingPoints(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            var points = new List<Point2d>();
            foreach (var dim in candidates)
            {
                AddBottomCrossingPoint(points, dim.XLine1, outline);
                AddBottomCrossingPoint(points, dim.XLine2, outline);
                AddDirectionalInclinedEndpoint(points, dim.XLine1, outline, invertDirection: false);
                AddDirectionalInclinedEndpoint(points, dim.XLine2, outline, invertDirection: false);
            }

            return points;
        }

        private void AddCrossingPoint(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (LeftExtensionCrossesOutline(featurePoint, outline)
                && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private void AddRightCrossingPoint(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (RightExtensionCrossesOutline(featurePoint, outline)
                && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private void AddTopCrossingPoint(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (TopExtensionCrossesOutline(featurePoint, outline)
                && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private void AddBottomCrossingPoint(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (BottomExtensionCrossesOutline(featurePoint, outline)
                && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private void AddInclinedEdgeProjectionPoints(IList<Point2d> points, DeferredDim dim, OutlineFeature outline)
        {
            AddVerticalOutlinePointOnInclinedProjection(points, dim.XLine1, dim, outline);
            AddVerticalOutlinePointOnInclinedProjection(points, dim.XLine2, dim, outline);
        }

        private void AddRightInclinedEdgeProjectionPoints(IList<Point2d> points, DeferredDim dim, OutlineFeature outline)
        {
            AddVerticalOutlinePointOnInclinedProjection(points, dim.XLine1, dim, outline);
            AddVerticalOutlinePointOnInclinedProjection(points, dim.XLine2, dim, outline);
        }

        private void AddHorizontalInclinedEdgeProjectionPoints(
            IList<Point2d> points,
            Point3d featurePoint,
            DeferredDim dim,
            OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (ContainsPoint(points, point) || !IsPointOnVerticalOutlineSegment(point, outline))
            {
                return;
            }

            if (MatchesFortyFiveInclinedHorizontalProjectionAtPoint(dim, point, outline))
            {
                points.Add(point);
            }
        }

        private void AddTopChamferEndpointPoints(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (IsTopChamferEndpoint(point, outline) && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private void AddDirectionalInclinedEndpoint(
            IList<Point2d> points,
            Point3d featurePoint,
            OutlineFeature outline,
            bool invertDirection)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (ContainsPoint(points, point)
                || IsEnvelopeSidePoint(point, outline)
                || !ShouldIgnoreDirectionalInclinedEndpoint(point, outline, invertDirection))
            {
                return;
            }

            points.Add(point);
        }

        private void AddSideInnerGrooveEndpoint(
            IList<Point2d> points,
            Point3d featurePoint,
            OutlineFeature outline,
            DimSide side)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (ContainsPoint(points, point)
                || IsEnvelopeHorizontalSidePoint(point, outline)
                || !ShouldIgnoreSideInnerGrooveEndpoint(point, outline, side))
            {
                return;
            }

            points.Add(point);
        }

        private void AddSideDirectionalInclinedEndpoint(
            IList<Point2d> points,
            Point3d featurePoint,
            OutlineFeature outline,
            DimSide side)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (ContainsPoint(points, point)
                || IsEnvelopeHorizontalSidePoint(point, outline)
                || !ShouldIgnoreSideDirectionalInclinedEndpoint(point, outline, side))
            {
                return;
            }

            points.Add(point);
        }

        private bool ShouldIgnoreSideDirectionalInclinedEndpoint(Point2d point, OutlineFeature outline, DimSide side)
        {
            if (outline == null)
            {
                return false;
            }

            return outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Any(s =>
                    IsSideInnerGrooveChamferSegment(s, outline, side)
                        ? IsSideInnerGrooveIgnoredEndpoint(point, s, outline, side)
                        : IsSideDirectionalIgnoredEndpoint(point, s, side));
        }

        private bool IsSideDirectionalIgnoredEndpoint(Point2d point, OutlineSegment segment, DimSide side)
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
                var ignoredPoint = side == DimSide.Left
                    ? (segment.Start.Y <= segment.End.Y ? segment.Start : segment.End)
                    : (segment.Start.Y >= segment.End.Y ? segment.Start : segment.End);
                return PointsEqual(point, ignoredPoint);
            }

            var negativeIgnoredPoint = side == DimSide.Left
                ? (segment.Start.Y >= segment.End.Y ? segment.Start : segment.End)
                : (segment.Start.Y <= segment.End.Y ? segment.Start : segment.End);
            return PointsEqual(point, negativeIgnoredPoint);
        }

        private bool ShouldIgnoreSideInnerGrooveEndpoint(Point2d point, OutlineFeature outline, DimSide side)
        {
            if (outline == null)
            {
                return false;
            }

            return outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsSideInnerGrooveChamferSegment(s, outline, side))
                .Any(s => IsSideInnerGrooveIgnoredEndpoint(point, s, outline, side));
        }

        private bool ShouldIgnoreDirectionalInclinedEndpoint(Point2d point, OutlineFeature outline, bool invertDirection)
        {
            if (outline == null)
            {
                return false;
            }

            var segments = outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(IsFortyFiveDegreeSegment);

            if (invertDirection)
            {
                return segments.Any(s =>
                    IsInnerGrooveChamferSegment(s, outline, isTopSide: true)
                        ? IsInnerGrooveIgnoredEndpoint(point, s, outline, isTopSide: true)
                        : IsDirectionalIgnoredEndpoint(point, s, invertDirection: true));
            }

            return segments.Any(s =>
                IsInnerGrooveChamferSegment(s, outline, isTopSide: false)
                    ? IsInnerGrooveIgnoredEndpoint(point, s, outline, isTopSide: false)
                    : IsDirectionalIgnoredEndpoint(point, s, invertDirection: false));
        }

        private bool IsInnerGrooveChamferSegment(OutlineSegment chamfer, OutlineFeature outline, bool isTopSide)
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

                if (SegmentTouchesPoint(horizontal, chamfer.Start)
                    && HorizontalOtherEndConnectsInnerGroove(horizontal, chamfer.Start, chamfer, outline))
                {
                    return true;
                }

                if (SegmentTouchesPoint(horizontal, chamfer.End)
                    && HorizontalOtherEndConnectsInnerGroove(horizontal, chamfer.End, chamfer, outline))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSideInnerGrooveChamferSegment(OutlineSegment chamfer, OutlineFeature outline, DimSide side)
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

                if (side == DimSide.Left && vertical.MinX <= chamferLeftX + tolerance)
                {
                    continue;
                }

                if (side == DimSide.Right && vertical.MinX >= chamferRightX - tolerance)
                {
                    continue;
                }

                if (SegmentTouchesPoint(vertical, chamfer.Start)
                    && IsInternalSideGrooveVertical(vertical, chamfer, side, outline)
                    && VerticalOtherEndConnectsChamfer(vertical, chamfer.Start, chamfer, outline))
                {
                    return true;
                }

                if (SegmentTouchesPoint(vertical, chamfer.End)
                    && IsInternalSideGrooveVertical(vertical, chamfer, side, outline)
                    && VerticalOtherEndConnectsChamfer(vertical, chamfer.End, chamfer, outline))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsInnerGrooveIgnoredEndpoint(Point2d point, OutlineSegment chamfer, OutlineFeature outline, bool isTopSide)
        {
            var sharedPoint = GetInnerGrooveHorizontalSharedPoint(chamfer, outline, isTopSide);
            return sharedPoint.HasValue && PointsEqual(point, sharedPoint.Value);
        }

        private Point2d? GetInnerGrooveHorizontalSharedPoint(OutlineSegment chamfer, OutlineFeature outline, bool isTopSide)
        {
            var tolerance = _config.GeometryTolerance;
            var chamferTopY = Math.Max(chamfer.Start.Y, chamfer.End.Y);
            var chamferBottomY = Math.Min(chamfer.Start.Y, chamfer.End.Y);
            foreach (var horizontal in outline.Segments.Where(s => s.IsHorizontal(tolerance) && !s.IsArcChord))
            {
                if (Math.Abs(horizontal.MinY - outline.MaxY) <= tolerance
                    || Math.Abs(horizontal.MinY - outline.MinY) <= tolerance
                    || (isTopSide && horizontal.MinY >= chamferTopY - tolerance)
                    || (!isTopSide && horizontal.MinY <= chamferBottomY + tolerance))
                {
                    continue;
                }

                if (SegmentTouchesPoint(horizontal, chamfer.Start)
                    && HorizontalOtherEndConnectsInnerGroove(horizontal, chamfer.Start, chamfer, outline))
                {
                    return chamfer.Start;
                }

                if (SegmentTouchesPoint(horizontal, chamfer.End)
                    && HorizontalOtherEndConnectsInnerGroove(horizontal, chamfer.End, chamfer, outline))
                {
                    return chamfer.End;
                }
            }

            return null;
        }

        private bool IsSideInnerGrooveIgnoredEndpoint(Point2d point, OutlineSegment chamfer, OutlineFeature outline, DimSide side)
        {
            var sharedPoint = GetSideInnerGrooveVerticalSharedPoint(chamfer, outline, side);
            return sharedPoint.HasValue && PointsEqual(point, sharedPoint.Value);
        }

        private Point2d? GetSideInnerGrooveVerticalSharedPoint(OutlineSegment chamfer, OutlineFeature outline, DimSide side)
        {
            var tolerance = _config.GeometryTolerance;
            var chamferLeftX = Math.Min(chamfer.Start.X, chamfer.End.X);
            var chamferRightX = Math.Max(chamfer.Start.X, chamfer.End.X);
            foreach (var vertical in outline.Segments.Where(s => s.IsVertical(tolerance) && !s.IsArcChord))
            {
                if (Math.Abs(vertical.MinX - outline.MinX) <= tolerance
                    || Math.Abs(vertical.MinX - outline.MaxX) <= tolerance
                    || (side == DimSide.Left && vertical.MinX <= chamferLeftX + tolerance)
                    || (side == DimSide.Right && vertical.MinX >= chamferRightX - tolerance))
                {
                    continue;
                }

                if (SegmentTouchesPoint(vertical, chamfer.Start)
                    && IsInternalSideGrooveVertical(vertical, chamfer, side, outline)
                    && VerticalOtherEndConnectsChamfer(vertical, chamfer.Start, chamfer, outline))
                {
                    return chamfer.Start;
                }

                if (SegmentTouchesPoint(vertical, chamfer.End)
                    && IsInternalSideGrooveVertical(vertical, chamfer, side, outline)
                    && VerticalOtherEndConnectsChamfer(vertical, chamfer.End, chamfer, outline))
                {
                    return chamfer.End;
                }
            }

            return null;
        }

        private bool IsInternalSideGrooveVertical(
            OutlineSegment vertical,
            OutlineSegment chamfer,
            DimSide side,
            OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            if (!vertical.IsVertical(tolerance)
                || Math.Abs(vertical.MinX - outline.MinX) <= tolerance
                || Math.Abs(vertical.MinX - outline.MaxX) <= tolerance)
            {
                return false;
            }

            var chamferLeftX = Math.Min(chamfer.Start.X, chamfer.End.X);
            var chamferRightX = Math.Max(chamfer.Start.X, chamfer.End.X);
            if (side == DimSide.Left)
            {
                return vertical.MinX > chamferLeftX + tolerance;
            }

            if (side == DimSide.Right)
            {
                return vertical.MinX < chamferRightX - tolerance;
            }

            return false;
        }

        private bool IsKnownChamferSegment(OutlineSegment segment, OutlineFeature outline)
        {
            return outline.Chamfers.Any(chamfer =>
                IsFortyFiveDegreeChamfer(chamfer)
                && ((PointsEqual(chamfer.StartPoint, segment.Start) && PointsEqual(chamfer.EndPoint, segment.End))
                    || (PointsEqual(chamfer.StartPoint, segment.End) && PointsEqual(chamfer.EndPoint, segment.Start))));
        }

        private bool HorizontalOtherEndConnectsInnerGroove(
            OutlineSegment horizontal,
            Point2d sharedPoint,
            OutlineSegment currentChamfer,
            OutlineFeature outline)
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

        private bool VerticalOtherEndConnectsInnerGroove(
            OutlineSegment vertical,
            Point2d sharedPoint,
            OutlineSegment currentChamfer,
            OutlineFeature outline)
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

        private bool VerticalOtherEndConnectsChamfer(
            OutlineSegment vertical,
            Point2d sharedPoint,
            OutlineSegment currentChamfer,
            OutlineFeature outline)
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

        private bool IsDirectionalIgnoredEndpoint(Point2d point, OutlineSegment segment, bool invertDirection)
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

        private bool IsEnvelopeSidePoint(Point2d point, OutlineFeature outline)
        {
            if (outline == null)
            {
                return false;
            }

            return point.X <= outline.MinX + _config.GeometryTolerance
                || point.X >= outline.MaxX - _config.GeometryTolerance;
        }

        private bool IsEnvelopeHorizontalSidePoint(Point2d point, OutlineFeature outline)
        {
            if (outline == null)
            {
                return false;
            }

            return point.Y <= outline.MinY + _config.GeometryTolerance
                || point.Y >= outline.MaxY - _config.GeometryTolerance;
        }

        private bool IsTopChamferEndpoint(Point2d point, OutlineFeature outline)
        {
            if (outline == null || outline.Chamfers.Count == 0)
            {
                return false;
            }

            var topBandDepth = Math.Max(outline.Height * 0.25, Scale(_config.ArrowSize * 6.0));
            if (point.Y < outline.MaxY - topBandDepth - _config.GeometryTolerance)
            {
                return false;
            }

            if (point.X <= outline.MinX + _config.GeometryTolerance
                || point.X >= outline.MaxX - _config.GeometryTolerance)
            {
                return false;
            }

            return outline.Chamfers.Any(chamfer =>
                (PointsEqual(chamfer.StartPoint, point) || PointsEqual(chamfer.EndPoint, point))
                && IsFortyFiveDegreeChamfer(chamfer));
        }

        private bool IsFortyFiveDegreeChamfer(ChamferFeature chamfer)
        {
            var dx = Math.Abs(chamfer.StartPoint.X - chamfer.EndPoint.X);
            var dy = Math.Abs(chamfer.StartPoint.Y - chamfer.EndPoint.Y);
            var tolerance = Math.Max(_config.GeometryTolerance, Math.Max(dx, dy) * 0.08);
            return dx > _config.GeometryTolerance
                && dy > _config.GeometryTolerance
                && Math.Abs(dx - dy) <= tolerance;
        }

        private void AddVerticalOutlinePointOnInclinedProjection(
            IList<Point2d> points,
            Point3d featurePoint,
            DeferredDim dim,
            OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (ContainsPoint(points, point) || !IsPointOnVerticalOutlineSegment(point, outline))
            {
                return;
            }

            if (MatchesFortyFiveInclinedVerticalProjectionAtPoint(dim, point, outline))
            {
                points.Add(point);
            }
        }

        private bool MatchesFortyFiveInclinedVerticalProjectionAtPoint(DeferredDim dim, Point2d point, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var dimMinY = Math.Min(dim.XLine1.Y, dim.XLine2.Y);
            var dimMaxY = Math.Max(dim.XLine1.Y, dim.XLine2.Y);

            return outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Any(s => Math.Abs(s.MinY - dimMinY) <= tolerance
                    && Math.Abs(s.MaxY - dimMaxY) <= tolerance
                    && IsPointXWithinSegmentXRange(point, s));
        }

        private bool IsFortyFiveDegreeSegment(OutlineSegment segment)
        {
            var dx = Math.Abs(segment.Start.X - segment.End.X);
            var dy = Math.Abs(segment.Start.Y - segment.End.Y);
            var tolerance = Math.Max(_config.GeometryTolerance, Math.Max(dx, dy) * 0.08);
            return dx > _config.GeometryTolerance
                && dy > _config.GeometryTolerance
                && Math.Abs(dx - dy) <= tolerance;
        }

        private bool IsPointXWithinSegmentXRange(Point2d point, OutlineSegment segment)
        {
            var tolerance = _config.GeometryTolerance;
            var minX = Math.Min(segment.Start.X, segment.End.X);
            var maxX = Math.Max(segment.Start.X, segment.End.X);
            return point.X >= minX - tolerance && point.X <= maxX + tolerance;
        }

        private bool IsPointOnSegment(Point2d point, OutlineSegment segment)
        {
            var tolerance = Math.Max(_config.GeometryTolerance, 0.2);
            var minX = Math.Min(segment.Start.X, segment.End.X) - tolerance;
            var maxX = Math.Max(segment.Start.X, segment.End.X) + tolerance;
            var minY = Math.Min(segment.Start.Y, segment.End.Y) - tolerance;
            var maxY = Math.Max(segment.Start.Y, segment.End.Y) + tolerance;
            if (point.X < minX || point.X > maxX || point.Y < minY || point.Y > maxY)
            {
                return false;
            }

            var dx = segment.End.X - segment.Start.X;
            var dy = segment.End.Y - segment.Start.Y;
            var cross = Math.Abs((point.X - segment.Start.X) * dy - (point.Y - segment.Start.Y) * dx);
            var length = Math.Sqrt(dx * dx + dy * dy);
            return length <= tolerance || cross / length <= tolerance;
        }

        private bool MatchesFortyFiveInclinedHorizontalProjectionAtPoint(DeferredDim dim, Point2d point, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var dimMinX = Math.Min(dim.XLine1.X, dim.XLine2.X);
            var dimMaxX = Math.Max(dim.XLine1.X, dim.XLine2.X);

            return outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Any(s => Math.Abs(s.MinX - dimMinX) <= tolerance
                    && Math.Abs(s.MaxX - dimMaxX) <= tolerance
                    && IsPointXWithinSegmentXRange(point, s));
        }

        private void AddVerticalOutlinePoint(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (IsPointOnVerticalOutlineSegment(point, outline)
                && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private bool IsPointOnVerticalOutlineSegment(Point2d point, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Any(s => Math.Abs(s.MinX - point.X) <= tolerance
                    && point.Y >= s.MinY - tolerance
                    && point.Y <= s.MaxY + tolerance);
        }

        private void AddHorizontalOutlinePoint(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (IsPointOnHorizontalOutlineSegment(point, outline)
                && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private bool IsPointOnHorizontalOutlineSegment(Point2d point, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Any(s => Math.Abs(s.MinY - point.Y) <= tolerance
                    && point.X >= s.MinX - tolerance
                    && point.X <= s.MaxX + tolerance);
        }

        private bool AddIgnoredPoints(IList<Point2d> ignoredPoints, IEnumerable<Point2d> crossingPoints)
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

        private bool ContainsPoint(IEnumerable<Point2d> points, Point2d point)
        {
            return points.Any(p => PointsEqual(p, point));
        }

        private bool LeftExtensionCrossesOutline(Point3d featurePoint, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var dimX = outline.MinX - Scale(_config.FirstDimOffset);
            var maxX = featurePoint.X - tolerance;
            if (maxX <= dimX + tolerance)
            {
                return false;
            }

            foreach (var segment in outline.Segments)
            {
                if (TryGetHorizontalIntersectionX(segment, featurePoint.Y, out var x)
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

        private bool RightExtensionCrossesOutline(Point3d featurePoint, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var minX = featurePoint.X + tolerance;
            var dimX = outline.MaxX + Scale(_config.FirstDimOffset);
            if (dimX <= minX + tolerance)
            {
                return false;
            }

            foreach (var segment in outline.Segments)
            {
                if (TryGetHorizontalIntersectionX(segment, featurePoint.Y, out var x)
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

        private bool TopExtensionCrossesOutline(Point3d featurePoint, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var minY = featurePoint.Y + tolerance;
            var dimY = outline.MaxY + Scale(_config.FirstDimOffset);
            if (dimY <= minY + tolerance)
            {
                return false;
            }

            foreach (var segment in outline.Segments)
            {
                if (TryGetVerticalIntersectionY(segment, featurePoint.X, out var y)
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

        private bool BottomExtensionCrossesOutline(Point3d featurePoint, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var dimY = outline.MinY - Scale(_config.FirstDimOffset);
            var maxY = featurePoint.Y - tolerance;
            if (maxY <= dimY + tolerance)
            {
                return false;
            }

            foreach (var segment in outline.Segments)
            {
                if (TryGetVerticalIntersectionY(segment, featurePoint.X, out var y)
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

        private bool TryGetHorizontalIntersectionX(OutlineSegment segment, double y, out double x)
        {
            var tolerance = _config.GeometryTolerance;
            x = 0.0;
            if (Math.Abs(segment.Start.Y - segment.End.Y) <= tolerance)
            {
                return false;
            }

            var minY = segment.MinY;
            var maxY = segment.MaxY;
            if (y < minY - tolerance || y > maxY + tolerance)
            {
                return false;
            }

            x = segment.Start.X + (y - segment.Start.Y) * (segment.End.X - segment.Start.X) / (segment.End.Y - segment.Start.Y);
            return true;
        }

        private bool TryGetVerticalIntersectionY(OutlineSegment segment, double x, out double y)
        {
            var tolerance = _config.GeometryTolerance;
            y = 0.0;
            if (Math.Abs(segment.Start.X - segment.End.X) <= tolerance)
            {
                return false;
            }

            var minX = segment.MinX;
            var maxX = segment.MaxX;
            if (x < minX - tolerance || x > maxX + tolerance)
            {
                return false;
            }

            y = segment.Start.Y + (x - segment.Start.X) * (segment.End.Y - segment.Start.Y) / (segment.End.X - segment.Start.X);
            return true;
        }

        private bool HorizontalExtensionOverlapsOutlineSegment(OutlineSegment segment, Point3d featurePoint, double minX, double maxX)
        {
            var tolerance = _config.GeometryTolerance;
            if (!segment.IsHorizontal(tolerance))
            {
                return false;
            }

            if (Math.Abs(segment.MinY - featurePoint.Y) > tolerance)
            {
                return false;
            }

            var overlapStart = Math.Max(segment.MinX, minX);
            var overlapEnd = Math.Min(segment.MaxX, maxX);
            if (overlapEnd <= overlapStart + tolerance)
            {
                return false;
            }

            return !PointLiesOnSegmentHorizontalExtent(segment, featurePoint.X);
        }

        private bool PointLiesOnSegmentHorizontalExtent(OutlineSegment segment, double x)
        {
            var tolerance = _config.GeometryTolerance;
            return x >= segment.MinX - tolerance && x <= segment.MaxX + tolerance;
        }

        private bool VerticalExtensionOverlapsOutlineSegment(OutlineSegment segment, Point3d featurePoint, double minY, double maxY)
        {
            var tolerance = _config.GeometryTolerance;
            if (!segment.IsVertical(tolerance))
            {
                return false;
            }

            if (Math.Abs(segment.MinX - featurePoint.X) > tolerance)
            {
                return false;
            }

            var overlapStart = Math.Max(segment.MinY, minY);
            var overlapEnd = Math.Min(segment.MaxY, maxY);
            if (overlapEnd <= overlapStart + tolerance)
            {
                return false;
            }

            return !PointLiesOnSegmentVerticalExtent(segment, featurePoint.Y);
        }

        private bool PointLiesOnSegmentVerticalExtent(OutlineSegment segment, double y)
        {
            var tolerance = _config.GeometryTolerance;
            return y >= segment.MinY - tolerance && y <= segment.MaxY + tolerance;
        }

        private void RemoveLongestExtensionCandidate(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            if (candidates.Count <= 1)
            {
                return;
            }

            var removal = candidates
                .Select((dim, index) => new
                {
                    Index = index,
                    ExtensionLength = Math.Max(dim.XLine1.X, dim.XLine2.X) - outline.MinX,
                    Span = dim.Span
                })
                .OrderByDescending(x => x.ExtensionLength)
                .ThenByDescending(x => x.Span)
                .First();

            candidates.RemoveAt(removal.Index);
        }

        private void RemoveLongestRightExtensionCandidate(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            if (candidates.Count <= 1)
            {
                return;
            }

            var removal = candidates
                .Select((dim, index) => new
                {
                    Index = index,
                    ExtensionLength = outline.MaxX - Math.Min(dim.XLine1.X, dim.XLine2.X),
                    Span = dim.Span
                })
                .OrderByDescending(x => x.ExtensionLength)
                .ThenByDescending(x => x.Span)
                .First();

            candidates.RemoveAt(removal.Index);
        }

        private void RemoveLongestTopExtensionCandidate(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            if (candidates.Count <= 1)
            {
                return;
            }

            var removal = candidates
                .Select((dim, index) => new
                {
                    Index = index,
                    ExtensionLength = outline.MaxY - Math.Min(dim.XLine1.Y, dim.XLine2.Y),
                    Span = dim.Span
                })
                .OrderByDescending(x => x.ExtensionLength)
                .ThenByDescending(x => x.Span)
                .First();

            candidates.RemoveAt(removal.Index);
        }

        private void RemoveLongestBottomExtensionCandidate(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            if (candidates.Count <= 1)
            {
                return;
            }

            var removal = candidates
                .Select((dim, index) => new
                {
                    Index = index,
                    ExtensionLength = Math.Max(dim.XLine1.Y, dim.XLine2.Y) - outline.MinY,
                    Span = dim.Span
                })
                .OrderByDescending(x => x.ExtensionLength)
                .ThenByDescending(x => x.Span)
                .First();

            candidates.RemoveAt(removal.Index);
        }

        private IEnumerable<OutlineSegment> PickVerticalStepHeightSegments(IList<OutlineSegment> segments, OutlineFeature outline)
        {
            foreach (var side in new[] { DimSide.Left, DimSide.Right })
            {
                var segment = segments
                    .Where(s => GetNearestVerticalDimSide(s, outline) == side)
                    .OrderByDescending(s => ScoreVerticalProtrusionStep(s, outline))
                    .ThenByDescending(s => s.LengthY)
                    .FirstOrDefault();
                if (segment != null)
                {
                    yield return segment;
                }
            }
        }

        private bool IsVerticalProtrusionStep(OutlineSegment segment, OutlineFeature outline)
        {
            if (IsOuterVerticalSegment(segment, outline))
            {
                return false;
            }

            var maxLocalHeight = Math.Max(outline.Height * 0.8, Scale(_config.ArrowSize * 12.0));
            if (segment.LengthY > maxLocalHeight + _config.GeometryTolerance)
            {
                return false;
            }

            return TouchesVerticalOuterBoundary(segment, outline)
                && HasVerticalStepReturn(segment, outline);
        }

        private int ScoreVerticalProtrusionStep(OutlineSegment segment, OutlineFeature outline)
        {
            var score = 0;
            if (TouchesVerticalOuterBoundary(segment, outline))
            {
                score += 4;
            }

            if (HasVerticalStepReturn(segment, outline))
            {
                score += 4;
            }

            if (GetNearestVerticalDimSide(segment, outline) == DimSide.Left)
            {
                score += Math.Abs(segment.MinX - outline.MinX) <= outline.Width * 0.25 ? 2 : 0;
            }
            else
            {
                score += Math.Abs(segment.MaxX - outline.MaxX) <= outline.Width * 0.25 ? 2 : 0;
            }

            return score;
        }

        private bool TouchesVerticalOuterBoundary(OutlineSegment segment, OutlineFeature outline)
        {
            var tolerance = Math.Max(_config.GeometryTolerance, 0.2);
            return Math.Abs(segment.MinY - outline.MinY) <= tolerance
                || Math.Abs(segment.MaxY - outline.MaxY) <= tolerance
                || EndpointConnectsToBoundary(segment.Start, outline, horizontalBoundary: false)
                || EndpointConnectsToBoundary(segment.End, outline, horizontalBoundary: false);
        }

        private bool HasVerticalStepReturn(OutlineSegment segment, OutlineFeature outline)
        {
            return EndpointHasStructuralReturn(segment.Start, segment, outline)
                || EndpointHasStructuralReturn(segment.End, segment, outline);
        }

        private void ResolveVerticalStepBoundarySpan(
            OutlineFeature outline,
            OutlineSegment segment,
            DimSide side,
            out Point2d lowerPoint,
            out Point2d upperPoint)
        {
            lowerPoint = segment.Start.Y <= segment.End.Y ? segment.Start : segment.End;
            upperPoint = segment.Start.Y > segment.End.Y ? segment.Start : segment.End;

            Point2d resolved;
            if (TryResolveAdjacentVerticalStepEndpoint(outline, segment, lowerPoint, lowerPoint.Y, searchLower: true, out resolved))
            {
                lowerPoint = resolved;
            }

            if (TryResolveAdjacentVerticalStepEndpoint(outline, segment, upperPoint, upperPoint.Y, searchLower: false, out resolved))
            {
                upperPoint = resolved;
            }

            if (side == DimSide.Left)
            {
                TryResolveLeftProtrusionHeightSpan(outline, segment, ref lowerPoint, ref upperPoint);
            }
            else if (side == DimSide.Right)
            {
                TryResolveRightProtrusionHeightSpan(outline, segment, ref lowerPoint, ref upperPoint);
            }
        }

        private bool TryResolveLeftProtrusionHeightSpan(
            OutlineFeature outline,
            OutlineSegment segment,
            ref Point2d lowerPoint,
            ref Point2d upperPoint)
        {
            var tolerance = _config.GeometryTolerance;
            var leftLimit = outline.MinX + outline.Width * 0.45;
            var topLimit = outline.MinY + outline.Height * 0.7;
            var currentSpan = Math.Abs(upperPoint.Y - lowerPoint.Y);
            var lowerY = lowerPoint.Y;

            var bottom = outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => s.LengthX <= Math.Max(outline.Width * 0.35, Scale(_config.ArrowSize * 10.0)) + tolerance)
                .Where(s => s.MinY <= lowerY + Math.Max(tolerance, 0.2))
                .Where(s => s.MinX <= leftLimit)
                .OrderBy(s => s.MinY)
                .ThenBy(s => Math.Abs(s.MinX - outline.MinX))
                .FirstOrDefault();

            var top = outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => s.MinX <= leftLimit)
                .Where(s => s.MinY > lowerY + tolerance)
                .Where(s => s.MinY <= topLimit + tolerance)
                .OrderByDescending(s => s.MinY)
                .ThenBy(s => Math.Abs(s.MinX - segment.MinX))
                .FirstOrDefault();

            if (bottom == null || top == null)
            {
                return false;
            }

            var candidateLower = Math.Abs(bottom.Start.X - segment.MinX) <= Math.Abs(bottom.End.X - segment.MinX)
                ? bottom.Start
                : bottom.End;
            var candidateUpper = Math.Abs(top.Start.X - segment.MinX) <= Math.Abs(top.End.X - segment.MinX)
                ? top.Start
                : top.End;
            var candidateSpan = Math.Abs(candidateUpper.Y - candidateLower.Y);
            if (candidateSpan <= currentSpan + Math.Max(tolerance, 0.2))
            {
                return false;
            }

            lowerPoint = candidateLower;
            upperPoint = candidateUpper;
            return true;
        }

        private bool TryResolveRightProtrusionHeightSpan(
            OutlineFeature outline,
            OutlineSegment segment,
            ref Point2d lowerPoint,
            ref Point2d upperPoint)
        {
            var tolerance = _config.GeometryTolerance;
            var rightLimit = outline.MaxX - outline.Width * 0.45;
            var currentSpan = Math.Abs(upperPoint.Y - lowerPoint.Y);
            var points = new List<Point2d> { segment.Start, segment.End };
            var structureX = segment.MinX;
            var xTolerance = Math.Max(_config.GeometryTolerance, Scale(_config.ArrowSize * 2.0));

            foreach (var neighbor in outline.Segments)
            {
                if (ReferenceEquals(neighbor, segment))
                {
                    continue;
                }

                if (neighbor.MinX < rightLimit - tolerance)
                {
                    continue;
                }

                var nearSameStructure =
                    Math.Abs(neighbor.MinX - structureX) <= xTolerance
                    || Math.Abs(neighbor.MaxX - structureX) <= xTolerance
                    || SegmentTouchesPoint(neighbor, segment.Start)
                    || SegmentTouchesPoint(neighbor, segment.End);
                if (nearSameStructure)
                {
                    points.Add(neighbor.Start);
                    points.Add(neighbor.End);
                }
            }

            foreach (var chamfer in outline.Chamfers)
            {
                if (chamfer.StartPoint.X < rightLimit - tolerance && chamfer.EndPoint.X < rightLimit - tolerance)
                {
                    continue;
                }

                if (Math.Abs(chamfer.StartPoint.X - structureX) <= xTolerance
                    || Math.Abs(chamfer.EndPoint.X - structureX) <= xTolerance
                    || PointsEqual(chamfer.StartPoint, segment.Start)
                    || PointsEqual(chamfer.EndPoint, segment.Start)
                    || PointsEqual(chamfer.StartPoint, segment.End)
                    || PointsEqual(chamfer.EndPoint, segment.End))
                {
                    points.Add(chamfer.StartPoint);
                    points.Add(chamfer.EndPoint);
                }
            }

            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);
            var top = outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => s.MaxX >= rightLimit - tolerance)
                .Where(s => s.MinY >= maxY - tolerance)
                .OrderBy(s => s.MinY)
                .FirstOrDefault();
            if (top != null)
            {
                maxY = Math.Max(maxY, top.MinY);
            }

            var candidateSpan = maxY - minY;
            if (candidateSpan <= currentSpan + Math.Max(tolerance, 0.2))
            {
                return false;
            }

            var x = segment.MinX;
            lowerPoint = new Point2d(x, minY);
            upperPoint = new Point2d(x, maxY);
            return true;
        }

        private bool TryResolveAdjacentVerticalStepEndpoint(
            OutlineFeature outline,
            OutlineSegment segment,
            Point2d endpoint,
            double originalY,
            bool searchLower,
            out Point2d resolved)
        {
            resolved = endpoint;
            var candidates = new List<Point2d>();

            foreach (var neighbor in outline.Segments)
            {
                if (ReferenceEquals(neighbor, segment))
                {
                    continue;
                }

                if (PointsEqual(neighbor.Start, endpoint))
                {
                    candidates.Add(neighbor.End);
                }
                else if (PointsEqual(neighbor.End, endpoint))
                {
                    candidates.Add(neighbor.Start);
                }
            }

            foreach (var arc in outline.Arcs)
            {
                if (PointsEqual(arc.Start, endpoint))
                {
                    candidates.Add(arc.End);
                }
                else if (PointsEqual(arc.End, endpoint))
                {
                    candidates.Add(arc.Start);
                }
            }

            foreach (var fillet in outline.Fillets)
            {
                if (PointsEqual(fillet.StartPoint, endpoint))
                {
                    candidates.Add(fillet.EndPoint);
                }
                else if (PointsEqual(fillet.EndPoint, endpoint))
                {
                    candidates.Add(fillet.StartPoint);
                }
            }

            var maxLocalExtension = Math.Max(segment.LengthY * 0.25, Scale(_config.ArrowSize * 8.0));
            var best = candidates
                .Where(p => searchLower
                    ? p.Y < originalY - _config.GeometryTolerance
                    : p.Y > originalY + _config.GeometryTolerance)
                .Select(p => new { Point = p, Delta = Math.Abs(p.Y - originalY) })
                .Where(x => x.Delta <= maxLocalExtension + _config.GeometryTolerance)
                .OrderByDescending(x => x.Delta)
                .FirstOrDefault();

            if (best == null)
            {
                return false;
            }

            resolved = best.Point;
            return true;
        }

        private bool IsUsefulHorizontalStep(OutlineSegment segment, OutlineFeature outline)
        {
            return segment.IsHorizontal(_config.GeometryTolerance)
                && segment.LengthX > _config.GeometryTolerance
                && segment.LengthX < outline.Width - _config.GeometryTolerance;
        }

        private bool IsUsefulVerticalStep(OutlineSegment segment, OutlineFeature outline)
        {
            return segment.IsVertical(_config.GeometryTolerance)
                && segment.LengthY > _config.GeometryTolerance
                && segment.LengthY < outline.Height - _config.GeometryTolerance;
        }

        private bool IsFeatureRedundantStepDimension(OutlineSegment segment, OutlineFeature outline)
        {
            return IsRedundantChamferStepDimension(segment, outline)
                || IsRedundantFilletStepDimension(segment, outline);
        }

        private bool IsRedundantChamferStepDimension(OutlineSegment segment, OutlineFeature outline)
        {
            if (IsBetweenTwoChamfersAndDerivedFromOverall(segment, outline))
            {
                return true;
            }

            return outline.Chamfers.Any(chamfer =>
            {
                var featureSize = Math.Max(chamfer.DeltaX, chamfer.DeltaY);
                return IsSmallFeatureAdjacentDimension(segment, featureSize)
                    && SegmentTouchesPoint(segment, chamfer.StartPoint)
                    && SegmentTouchesPoint(segment, chamfer.EndPoint);
            });
        }

        private bool IsBetweenTwoChamfersAndDerivedFromOverall(OutlineSegment segment, OutlineFeature outline)
        {
            if (!segment.IsHorizontal(_config.GeometryTolerance) && !segment.IsVertical(_config.GeometryTolerance))
            {
                return false;
            }

            var startChamfer = FindChamferTouchingPoint(outline, segment.Start);
            var endChamfer = FindChamferTouchingPoint(outline, segment.End);
            if (startChamfer == null || endChamfer == null || ReferenceEquals(startChamfer, endChamfer))
            {
                return false;
            }

            if (segment.IsVertical(_config.GeometryTolerance))
            {
                var derived = outline.Height - GetChamferProjectionY(startChamfer) - GetChamferProjectionY(endChamfer);
                return Math.Abs(segment.LengthY - derived) <= Math.Max(_config.GeometryTolerance, 0.2);
            }

            var horizontalDerived = outline.Width - GetChamferProjectionX(startChamfer) - GetChamferProjectionX(endChamfer);
            return Math.Abs(segment.LengthX - horizontalDerived) <= Math.Max(_config.GeometryTolerance, 0.2);
        }

        private ChamferFeature FindChamferTouchingPoint(OutlineFeature outline, Point2d point)
        {
            return outline.Chamfers.FirstOrDefault(chamfer =>
                PointsEqual(chamfer.StartPoint, point) || PointsEqual(chamfer.EndPoint, point));
        }

        private static double GetChamferProjectionX(ChamferFeature chamfer)
        {
            return Math.Abs(chamfer.StartPoint.X - chamfer.EndPoint.X);
        }

        private static double GetChamferProjectionY(ChamferFeature chamfer)
        {
            return Math.Abs(chamfer.StartPoint.Y - chamfer.EndPoint.Y);
        }

        private bool IsRedundantFilletStepDimension(OutlineSegment segment, OutlineFeature outline)
        {
            return outline.Fillets.Any(fillet =>
            {
                var featureSize = fillet.Radius * 2.0;
                return IsSmallFeatureAdjacentDimension(segment, featureSize)
                    && ((SegmentTouchesPoint(segment, fillet.StartPoint)
                            && SegmentTouchesPoint(segment, fillet.EndPoint))
                        || IsSingleTangentFilletStepDimension(segment, fillet));
            });
        }

        private bool IsSingleTangentFilletStepDimension(OutlineSegment segment, FilletFeature fillet)
        {
            var touchesOneTangent = SegmentTouchesPoint(segment, fillet.StartPoint)
                || SegmentTouchesPoint(segment, fillet.EndPoint);
            if (!touchesOneTangent)
            {
                return false;
            }

            var tangentLimit = Math.Max(fillet.Radius * 10.0, Scale(_config.ArrowSize * 6.0));
            return segment.Length <= tangentLimit + _config.GeometryTolerance;
        }

        private bool IsSmallFeatureAdjacentDimension(OutlineSegment segment, double featureSize)
        {
            var limit = Math.Max(featureSize * 2.5, Scale(_config.ArrowSize * 2.0));
            return segment.Length <= limit + _config.GeometryTolerance;
        }

        private bool SegmentTouchesPoint(OutlineSegment segment, Point2d point)
        {
            return PointsEqual(segment.Start, point) || PointsEqual(segment.End, point);
        }

        private bool PointsEqual(Point2d a, Point2d b)
        {
            return a.GetDistanceTo(b) <= Math.Max(_config.GeometryTolerance, 0.2);
        }

        private DimSide GetNearestVerticalDimSide(OutlineSegment segment, OutlineFeature outline)
        {
            var distanceToLeft = Math.Abs(segment.MinX - outline.MinX);
            var distanceToRight = Math.Abs(outline.MaxX - segment.MaxX);
            return distanceToLeft <= distanceToRight ? DimSide.Left : DimSide.Right;
        }

        private DimSide GetNearestHorizontalDimSide(OutlineSegment segment, OutlineFeature outline)
        {
            var distanceToBottom = Math.Abs(segment.MinY - outline.MinY);
            var distanceToTop = Math.Abs(outline.MaxY - segment.MaxY);
            return distanceToBottom < distanceToTop ? DimSide.Bottom : DimSide.Top;
        }

        private bool StepHeightExtensionCrossesOutlineInterior(OutlineSegment segment, OutlineFeature outline, DimSide side)
        {
            var offset = Scale(_config.FirstDimOffset);
            var dimX = side == DimSide.Left ? outline.MinX - offset : outline.MaxX + offset;
            return HorizontalProbeCrossesInterior(segment.MaxY, segment.MinX, dimX, outline)
                || HorizontalProbeCrossesInterior(segment.MinY, segment.MinX, dimX, outline);
        }

        private bool HorizontalProbeCrossesInterior(double y, double fromX, double toX, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var minX = Math.Min(fromX, toX);
            var maxX = Math.Max(fromX, toX);

            foreach (var sampleX in GetProbeSampleXs(minX, maxX, outline))
            {
                if (sampleX <= minX + tolerance || sampleX >= maxX - tolerance)
                {
                    continue;
                }

                if (IsPointInsideOutlineByRayCast(sampleX, y, outline))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<double> GetProbeSampleXs(double minX, double maxX, OutlineFeature outline)
        {
            foreach (var segment in outline.Segments)
            {
                yield return Math.Max(minX, Math.Min(maxX, segment.MinX));
                yield return Math.Max(minX, Math.Min(maxX, segment.MaxX));
            }

            const int slices = 8;
            for (int i = 1; i < slices; i++)
            {
                yield return minX + (maxX - minX) * i / slices;
            }
        }

        private bool IsPointInsideOutlineByRayCast(double x, double y, OutlineFeature outline)
        {
            bool inside = false;
            var tolerance = _config.GeometryTolerance;

            foreach (var segment in outline.Segments)
            {
                var y1 = segment.Start.Y;
                var y2 = segment.End.Y;
                if (Math.Abs(y1 - y2) <= tolerance)
                {
                    continue;
                }

                bool crosses = (y1 > y) != (y2 > y);
                if (!crosses)
                {
                    continue;
                }

                var xAtY = segment.Start.X + (y - y1) * (segment.End.X - segment.Start.X) / (y2 - y1);
                if (xAtY > x + tolerance)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private bool IsOuterHorizontalSegment(OutlineSegment segment, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            if (!segment.IsHorizontal(tolerance))
            {
                return false;
            }

            return Math.Abs(segment.MinY - outline.MinY) <= tolerance
                || Math.Abs(segment.MaxY - outline.MaxY) <= tolerance;
        }

        private bool IsOuterVerticalSegment(OutlineSegment segment, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            if (!segment.IsVertical(tolerance))
            {
                return false;
            }

            return Math.Abs(segment.MinX - outline.MinX) <= tolerance
                || Math.Abs(segment.MaxX - outline.MaxX) <= tolerance;
        }

        public void FlushStackedDimensions(OutlineFeature outline)
        {
            var textHeight = GetDimStyleTextHeight(_dimStyleId);
            var perLevelSpacing = textHeight + Scale(_config.DimTextClearance);

            RebalanceVerticalHoleLocationSides();
            SuppressMirroredHorizontalDuplicates();
            SuppressMirroredVerticalDuplicates();

            FlushSide(_bottomDims, DimSide.Bottom, outline, perLevelSpacing);
            FlushSide(_topDims, DimSide.Top, outline, perLevelSpacing);
            FlushSide(_leftDims, DimSide.Left, outline, perLevelSpacing);
            FlushSide(_rightDims, DimSide.Right, outline, perLevelSpacing);
        }

        private void RebalanceVerticalHoleLocationSides()
        {
            RebalanceVerticalHoleLocationSide(_rightDims, _leftDims);
            RebalanceVerticalHoleLocationSide(_leftDims, _rightDims);
        }

        private void RebalanceVerticalHoleLocationSide(List<DeferredDim> sourceDims, List<DeferredDim> targetDims)
        {
            RebalanceVerticalLooseChains(sourceDims, targetDims);

            for (int i = sourceDims.Count - 1; i >= 0; i--)
            {
                var dim = sourceDims[i];
                if (!CanRebalanceVerticalHoleLocation(dim))
                {
                    continue;
                }

                var sourceWithoutCandidate = sourceDims
                    .Where((existing, index) => index != i)
                    .ToList();
                var sourceScore = ScoreVerticalSideCrowding(dim, sourceWithoutCandidate);
                var targetScore = ScoreVerticalSideCrowding(dim, targetDims);
                var threshold = IsShortVerticalDimension(dim) ? 1 : 3;
                if (sourceScore - targetScore < threshold)
                {
                    continue;
                }

                sourceDims.RemoveAt(i);
                targetDims.Add(dim);
            }
        }

        private void RebalanceVerticalLooseChains(List<DeferredDim> sourceDims, List<DeferredDim> targetDims)
        {
            var chainIds = sourceDims
                .Where(dim => dim.LooseChainId != 0 && dim.DimType == DimensionType.HoleLocation)
                .Select(dim => dim.LooseChainId)
                .Distinct()
                .ToList();

            foreach (var chainId in chainIds)
            {
                var chainDims = sourceDims.Where(dim => dim.LooseChainId == chainId).ToList();
                if (chainDims.Count == 0 || chainDims.Any(dim => !CanRebalanceVerticalLooseChainDimension(dim)))
                {
                    continue;
                }

                var sourceWithoutChain = sourceDims
                    .Where(dim => dim.LooseChainId != chainId)
                    .ToList();
                var sourceScore = chainDims.Sum(dim => ScoreVerticalSideCrowding(dim, sourceWithoutChain));
                var targetScore = chainDims.Sum(dim => ScoreVerticalSideCrowding(dim, targetDims));
                var threshold = Math.Max(2, chainDims.Count);
                if (sourceScore - targetScore < threshold)
                {
                    continue;
                }

                for (int i = sourceDims.Count - 1; i >= 0; i--)
                {
                    if (sourceDims[i].LooseChainId != chainId)
                    {
                        continue;
                    }

                    targetDims.Add(sourceDims[i]);
                    sourceDims.RemoveAt(i);
                }
            }
        }

        private bool CanRebalanceVerticalLooseChainDimension(DeferredDim dim)
        {
            return dim.DimType == DimensionType.HoleLocation
                && IsShortVerticalDimension(dim)
                && !dim.ForceOuterLevel;
        }

        private bool CanRebalanceVerticalHoleLocation(DeferredDim dim)
        {
            return dim.DimType == DimensionType.HoleLocation
                && dim.LooseChainId == 0
                && IsShortVerticalDimension(dim)
                && !dim.ForceOuterLevel;
        }

        private void SuppressMirroredHorizontalDuplicates()
        {
            for (int i = _topDims.Count - 1; i >= 0; i--)
            {
                var top = _topDims[i];
                if (!CanSuppressMirroredHorizontalDimension(top))
                {
                    continue;
                }

                if (_bottomDims.Any(bottom =>
                    CanSuppressMirroredHorizontalDimension(bottom)
                        && IsDuplicate(top, bottom, isHorizontal: true)))
                {
                    _topDims.RemoveAt(i);
                }
            }

            SuppressMirroredHoleRelatedDuplicates(_bottomDims, _topDims, isHorizontal: true);
        }

        private static bool CanSuppressMirroredHorizontalDimension(DeferredDim dim)
        {
            return dim.DimType == DimensionType.Normal && !dim.ForceOuterLevel;
        }

        private void SuppressMirroredVerticalDuplicates()
        {
            for (int i = _rightDims.Count - 1; i >= 0; i--)
            {
                var right = _rightDims[i];
                if (!CanSuppressMirroredVerticalDimension(right))
                {
                    continue;
                }

                if (_leftDims.Any(left =>
                    CanSuppressMirroredVerticalDimension(left)
                        && IsDuplicate(right, left, isHorizontal: false)))
                {
                    _rightDims.RemoveAt(i);
                }
            }

            SuppressMirroredHoleRelatedDuplicates(_leftDims, _rightDims, isHorizontal: false);
        }

        private static bool CanSuppressMirroredVerticalDimension(DeferredDim dim)
        {
            return dim.DimType == DimensionType.Normal && !dim.ForceOuterLevel;
        }

        private void SuppressMirroredHoleRelatedDuplicates(List<DeferredDim> primaryDims, List<DeferredDim> secondaryDims, bool isHorizontal)
        {
            for (int i = secondaryDims.Count - 1; i >= 0; i--)
            {
                var secondary = secondaryDims[i];
                for (int j = primaryDims.Count - 1; j >= 0; j--)
                {
                    var primary = primaryDims[j];
                    if (!CanSuppressMirroredHoleRelatedDimension(primary, secondary)
                        || !IsSameMeasuredDimension(primary, secondary, isHorizontal))
                    {
                        continue;
                    }

                    if (CompareDuplicatePreference(secondary, primary) > 0)
                    {
                        primaryDims.RemoveAt(j);
                    }
                    else
                    {
                        secondaryDims.RemoveAt(i);
                    }

                    break;
                }
            }
        }

        private static bool CanSuppressMirroredHoleRelatedDimension(DeferredDim a, DeferredDim b)
        {
            return !a.ForceOuterLevel
                && !b.ForceOuterLevel
                && (a.DimType == DimensionType.HoleLocation || b.DimType == DimensionType.HoleLocation);
        }

        private void FlushSide(List<DeferredDim> dims, DimSide side, OutlineFeature outline, double perLevelSpacing)
        {
            if (dims.Count == 0)
            {
                return;
            }

            var textHeight = GetDimStyleTextHeight(_dimStyleId);
            var gap = textHeight * 0.5;
            var isHorizontal = side == DimSide.Bottom || side == DimSide.Top;
            dims.Sort((a, b) => a.Span.CompareTo(b.Span));
            SuppressDuplicateMeasuredDimensions(dims, isHorizontal);

            var firstOffset = Scale(_config.FirstDimOffset);
            var layers = new List<List<(DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB)>>();
            var looseChainLevels = new Dictionary<int, int>();
            int? looseSideLevel = null;

            foreach (var dim in dims)
            {
                var textBox = ComputeTextInterval(dim, isHorizontal, textHeight);
                var arrow = ComputeArrowInterval(dim, isHorizontal);

                int minLevel = 0;
                for (int li = 0; li < layers.Count; li++)
                {
                    foreach (var existing in layers[li])
                    {
                        if (HasStrictArrowConflict(arrow.A, arrow.B, existing.ArrA, existing.ArrB))
                        {
                            if (li + 1 > minLevel) minLevel = li + 1;
                        }
                    }
                }

                int chainLevel;
                int targetLevel = dim.ForceOuterLevel ? Math.Max(minLevel, layers.Count) : minLevel;
                if (isHorizontal
                    && !dim.ForceOuterLevel
                    && dim.LooseChainId != 0
                    && looseChainLevels.TryGetValue(dim.LooseChainId, out chainLevel))
                {
                    targetLevel = Math.Max(chainLevel, minLevel);
                }
                if (isHorizontal && !dim.ForceOuterLevel && dim.LooseChainId != 0 && looseSideLevel.HasValue)
                {
                    targetLevel = Math.Max(looseSideLevel.Value, minLevel);
                }

                while (true)
                {
                    double offset = firstOffset + targetLevel * perLevelSpacing;
                    if (TextCoversOutline(dim, side, offset, textHeight, outline, isHorizontal))
                    {
                        targetLevel++;
                        continue;
                    }

                    if (targetLevel < layers.Count)
                    {
                        bool textOk = true;
                        foreach (var existing in layers[targetLevel])
                        {
                            if (isHorizontal && dim.LooseChainId != 0 && existing.Dim.LooseChainId != 0)
                            {
                                continue;
                            }

                            if (isHorizontal
                                && CanIgnoreTextConflictWithLooseChain(dim, existing.Dim)
                                && !HasStrictArrowConflict(arrow.A, arrow.B, existing.ArrA, existing.ArrB))
                            {
                                continue;
                            }

                            if (!AreCompatible(existing.TxtA, existing.TxtB, textBox.A, textBox.B, gap))
                            {
                                textOk = false;
                                break;
                            }
                        }
                        if (!textOk)
                        {
                            targetLevel++;
                            continue;
                        }
                    }

                    while (layers.Count <= targetLevel)
                    {
                        layers.Add(new List<(DeferredDim, double, double, double, double)>());
                    }
                    break;
                }

                if (isHorizontal && dim.LooseChainId != 0)
                {
                    PromoteLooseSideLevel(layers, targetLevel);
                    looseChainLevels[dim.LooseChainId] = targetLevel;
                    looseSideLevel = targetLevel;
                }

                layers[targetLevel].Add((dim, textBox.A, textBox.B, arrow.A, arrow.B));
            }

            AlignDimensionsBySharedExtensionLines(layers, side, outline, textHeight, gap, firstOffset, perLevelSpacing, isHorizontal);

            var placedDims = new List<PlacedDim>();

            for (int level = 0; level < layers.Count; level++)
            {
                double offset = firstOffset + level * perLevelSpacing;

                foreach (var item in layers[level])
                {
                    var dim = item.Dim;
                    var dimLinePoint = GetDimLinePoint(dim, side, outline, offset);

                    placedDims.Add(new PlacedDim
                    {
                        Dim = dim,
                        Side = side,
                        DimLinePoint = dimLinePoint,
                        TextBounds = ComputePlacedTextBounds(dim, dimLinePoint, isHorizontal, textHeight)
                    });
                }
            }

            AdjustVerticalHoleLocationTextPositions(placedDims, textHeight, gap);
            foreach (var placed in placedDims)
            {
                _linearDimTextObstacles.Add(placed.TextBounds);
            }

            foreach (var placed in placedDims)
            {
                AddRotatedDimension(
                    placed.Dim.Rotation,
                    placed.Dim.XLine1,
                    placed.Dim.XLine2,
                    placed.DimLinePoint,
                    placed.Dim.OverrideText,
                    useSegmentedExtensionLines: false,
                    placed.HasCustomTextPosition,
                    placed.TextPosition);
            }
        }

        private static bool CanIgnoreTextConflictWithLooseChain(DeferredDim candidate, DeferredDim existing)
        {
            return candidate.LooseChainId != 0 || existing.LooseChainId != 0;
        }

        private void PromoteLooseChainLevel(
            List<List<(DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB)>> layers,
            int chainId,
            int targetLevel)
        {
            if (chainId == 0)
            {
                return;
            }

            while (layers.Count <= targetLevel)
            {
                layers.Add(new List<(DeferredDim, double, double, double, double)>());
            }

            for (int level = 0; level < layers.Count; level++)
            {
                if (level == targetLevel)
                {
                    continue;
                }

                for (int i = layers[level].Count - 1; i >= 0; i--)
                {
                    if (layers[level][i].Dim.LooseChainId != chainId)
                    {
                        continue;
                    }

                    var item = layers[level][i];
                    layers[level].RemoveAt(i);
                    layers[targetLevel].Add(item);
                }
            }
        }

        private void PromoteLooseSideLevel(
            List<List<(DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB)>> layers,
            int targetLevel)
        {
            while (layers.Count <= targetLevel)
            {
                layers.Add(new List<(DeferredDim, double, double, double, double)>());
            }

            for (int level = 0; level < layers.Count; level++)
            {
                if (level == targetLevel)
                {
                    continue;
                }

                for (int i = layers[level].Count - 1; i >= 0; i--)
                {
                    if (layers[level][i].Dim.LooseChainId == 0)
                    {
                        continue;
                    }

                    var item = layers[level][i];
                    layers[level].RemoveAt(i);
                    layers[targetLevel].Add(item);
                }
            }
        }

        private void AdjustVerticalHoleLocationTextPositions(IList<PlacedDim> placedDims, double textHeight, double gap)
        {
            for (int i = 0; i < placedDims.Count; i++)
            {
                var placed = placedDims[i];
                if (!CanSlideVerticalHoleLocationText(placed))
                {
                    continue;
                }

                var defaultScore = ScoreTextBoundsAgainstPlaced(placed.TextBounds, placedDims, i, gap);
                var fitsInsideOwnLines = VerticalDimensionTextFitsInsideOwnLines(placed, textHeight);
                var hasHardOverlap = TextBoundsHasHardOverlap(placed.TextBounds, placedDims, i, gap);
                if (fitsInsideOwnLines && !hasHardOverlap)
                {
                    continue;
                }

                var candidates = GetVerticalTextSlideCandidates(placed, textHeight, gap)
                    .Select(c => new
                    {
                        Position = c.Position,
                        Bounds = c.Bounds,
                        Score = ScoreTextBoundsAgainstPlaced(c.Bounds, placedDims, i, gap)
                    })
                    .OrderBy(c => c.Score)
                    .ThenBy(c => Math.Abs(c.Position.Y - placed.DimLinePoint.Y))
                    .ToList();

                var best = candidates.FirstOrDefault();
                if (best == null)
                {
                    continue;
                }

                if (fitsInsideOwnLines && best.Score >= defaultScore)
                {
                    continue;
                }

                if (!fitsInsideOwnLines && best.Score > defaultScore)
                {
                    continue;
                }

                placed.HasCustomTextPosition = true;
                placed.TextPosition = best.Position;
                placed.TextBounds = best.Bounds;
                placedDims[i] = placed;
            }
        }

        private bool VerticalDimensionTextFitsInsideOwnLines(PlacedDim placed, double textHeight)
        {
            var arrow = GetVerticalInterval(placed.Dim);
            var text = GetDimensionText(placed.Dim);
            var textLength = Math.Max(text.Length, 1) * textHeight * 1.6;
            var innerClearance = Math.Max(_config.GeometryTolerance, textHeight * 0.5);
            return textLength + innerClearance * 2.0 <= arrow.B - arrow.A;
        }

        private bool CanSlideVerticalHoleLocationText(PlacedDim placed)
        {
            return (placed.Side == DimSide.Left || placed.Side == DimSide.Right)
                && placed.Dim.DimType == DimensionType.HoleLocation
                && IsShortVerticalDimension(placed.Dim);
        }

        private IEnumerable<(Point3d Position, TextBounds Bounds)> GetVerticalTextSlideCandidates(
            PlacedDim placed,
            double textHeight,
            double gap)
        {
            var textLength = GetDimensionTextLength(placed.Dim, textHeight);
            var arrow = GetVerticalInterval(placed.Dim);
            var lowerCenter = arrow.A - textLength / 2.0 - gap;
            var upperCenter = arrow.B + textLength / 2.0 + gap;
            var x = placed.DimLinePoint.X;

            yield return CreateVerticalCustomTextCandidate(x, lowerCenter, textLength, textHeight);
            yield return CreateVerticalCustomTextCandidate(x, upperCenter, textLength, textHeight);
        }

        private (Point3d Position, TextBounds Bounds) CreateVerticalCustomTextCandidate(
            double x,
            double centerY,
            double textLength,
            double textHeight)
        {
            var halfHeight = textHeight * 0.65;
            var position = new Point3d(x, centerY, 0.0);
            var bounds = new TextBounds
            {
                MinX = x - halfHeight,
                MaxX = x + halfHeight,
                MinY = centerY - textLength / 2.0,
                MaxY = centerY + textLength / 2.0
            };

            return (position, bounds);
        }

        private int ScoreTextBoundsAgainstPlaced(
            TextBounds candidate,
            IList<PlacedDim> placedDims,
            int selfIndex,
            double gap)
        {
            var score = 0;
            for (int i = 0; i < placedDims.Count; i++)
            {
                if (i == selfIndex)
                {
                    continue;
                }

                if (TextBoundsOverlap(candidate, placedDims[i].TextBounds, gap))
                {
                    score += 4;
                }

                if (TextBoundsAreTooClose(candidate, placedDims[i].TextBounds, gap))
                {
                    score += 2;
                }
            }

            foreach (var obstacle in _linearDimTextObstacles)
            {
                if (TextBoundsOverlap(candidate, obstacle, gap))
                {
                    score += 4;
                }

                if (TextBoundsAreTooClose(candidate, obstacle, gap))
                {
                    score += 2;
                }
            }

            return score;
        }

        private bool TextBoundsHasHardOverlap(
            TextBounds candidate,
            IList<PlacedDim> placedDims,
            int selfIndex,
            double gap)
        {
            for (int i = 0; i < placedDims.Count; i++)
            {
                if (i == selfIndex)
                {
                    continue;
                }

                if (TextBoundsOverlap(candidate, placedDims[i].TextBounds, gap))
                {
                    return true;
                }
            }

            return _linearDimTextObstacles.Any(obstacle => TextBoundsOverlap(candidate, obstacle, gap));
        }

        private bool TextBoundsOverlap(TextBounds first, TextBounds second, double gap)
        {
            return first.MinX <= second.MaxX + gap
                && second.MinX <= first.MaxX + gap
                && first.MinY <= second.MaxY + gap
                && second.MinY <= first.MaxY + gap;
        }

        private bool TextBoundsAreTooClose(TextBounds first, TextBounds second, double gap)
        {
            var yOverlap = first.MinY <= second.MaxY + gap && second.MinY <= first.MaxY + gap;
            if (!yOverlap)
            {
                return false;
            }

            double xGap;
            if (first.MaxX < second.MinX)
            {
                xGap = second.MinX - first.MaxX;
            }
            else if (second.MaxX < first.MinX)
            {
                xGap = first.MinX - second.MaxX;
            }
            else
            {
                xGap = 0.0;
            }

            return xGap <= Math.Max(gap, Scale(_config.TextHeight * 1.4));
        }

        private void AlignDimensionsBySharedExtensionLines(
            List<List<(DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB)>> layers,
            DimSide side,
            OutlineFeature outline,
            double textHeight,
            double gap,
            double firstOffset,
            double perLevelSpacing,
            bool isHorizontal)
        {
            for (int sourceLevel = 0; sourceLevel < layers.Count - 1; sourceLevel++)
            {
                for (int i = layers[sourceLevel].Count - 1; i >= 0; i--)
                {
                    var item = layers[sourceLevel][i];
                    if (item.Dim.DimType == DimensionType.HoleLocation)
                    {
                        continue;
                    }

                    if (HasArrowEndpointTouch(item, layers, sourceLevel, i, side, outline, firstOffset, perLevelSpacing, isHorizontal))
                    {
                        continue;
                    }

                    var targetLevel = FindSharedExtensionAlignmentLevel(
                        item,
                        layers,
                        sourceLevel,
                        side,
                        outline,
                        textHeight,
                        gap,
                        firstOffset,
                        perLevelSpacing,
                        isHorizontal);

                    if (targetLevel <= sourceLevel)
                    {
                        continue;
                    }

                    layers[sourceLevel].RemoveAt(i);
                    layers[targetLevel].Add(item);
                }
            }
        }

        private bool HasArrowEndpointTouch(
            (DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB) candidate,
            List<List<(DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB)>> layers,
            int candidateLevel,
            int candidateIndex,
            DimSide side,
            OutlineFeature outline,
            double firstOffset,
            double perLevelSpacing,
            bool isHorizontal)
        {
            var candidateOffset = firstOffset + candidateLevel * perLevelSpacing;
            var candidateDimLinePoint = GetDimLinePoint(candidate.Dim, side, outline, candidateOffset);
            for (int level = 0; level < layers.Count; level++)
            {
                var otherOffset = firstOffset + level * perLevelSpacing;
                for (int index = 0; index < layers[level].Count; index++)
                {
                    if (level == candidateLevel && index == candidateIndex)
                    {
                        continue;
                    }

                    var other = layers[level][index];
                    var otherDimLinePoint = GetDimLinePoint(other.Dim, side, outline, otherOffset);
                    if (ArrowEndpointTouches(candidate.Dim.XLine1, candidateDimLinePoint, other.Dim.XLine1, otherDimLinePoint, isHorizontal)
                        || ArrowEndpointTouches(candidate.Dim.XLine1, candidateDimLinePoint, other.Dim.XLine2, otherDimLinePoint, isHorizontal)
                        || ArrowEndpointTouches(candidate.Dim.XLine2, candidateDimLinePoint, other.Dim.XLine1, otherDimLinePoint, isHorizontal)
                        || ArrowEndpointTouches(candidate.Dim.XLine2, candidateDimLinePoint, other.Dim.XLine2, otherDimLinePoint, isHorizontal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool ArrowEndpointTouches(
            Point3d featureA,
            Point3d dimLineA,
            Point3d featureB,
            Point3d dimLineB,
            bool isHorizontal)
        {
            var arrowA = isHorizontal
                ? new Point3d(featureA.X, dimLineA.Y, 0.0)
                : new Point3d(dimLineA.X, featureA.Y, 0.0);
            var arrowB = isHorizontal
                ? new Point3d(featureB.X, dimLineB.Y, 0.0)
                : new Point3d(dimLineB.X, featureB.Y, 0.0);

            return arrowA.DistanceTo(arrowB) <= _config.GeometryTolerance;
        }

        private int FindSharedExtensionAlignmentLevel(
            (DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB) candidate,
            List<List<(DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB)>> layers,
            int sourceLevel,
            DimSide side,
            OutlineFeature outline,
            double textHeight,
            double gap,
            double firstOffset,
            double perLevelSpacing,
            bool isHorizontal)
        {
            for (int targetLevel = sourceLevel + 1; targetLevel < layers.Count; targetLevel++)
            {
                var targetOffset = firstOffset + targetLevel * perLevelSpacing;
                if (TextCoversOutline(candidate.Dim, side, targetOffset, textHeight, outline, isHorizontal))
                {
                    continue;
                }

                bool sharesExtension = false;
                bool placementOk = true;
                foreach (var existing in layers[targetLevel])
                {
                    if (HasSharedExtensionLine(candidate.Dim, existing.Dim, side, outline, targetOffset))
                    {
                        sharesExtension = true;
                    }

                    if (HasStrictArrowConflict(candidate.ArrA, candidate.ArrB, existing.ArrA, existing.ArrB)
                        || !AreCompatible(existing.TxtA, existing.TxtB, candidate.TxtA, candidate.TxtB, gap))
                    {
                        placementOk = false;
                        break;
                    }
                }

                if (sharesExtension && placementOk)
                {
                    return targetLevel;
                }
            }

            return sourceLevel;
        }

        private bool HasSharedExtensionLine(DeferredDim a, DeferredDim b, DimSide side, OutlineFeature outline, double targetOffset)
        {
            if (side == DimSide.Bottom || side == DimSide.Top)
            {
                var lineY = side == DimSide.Bottom
                    ? outline.MinY - targetOffset
                    : outline.MaxY + targetOffset;
                return ExtensionLineOverlapsAtCoordinate(a.XLine1.X, a.XLine1.Y, lineY, b.XLine1.X, b.XLine1.Y, lineY)
                    || ExtensionLineOverlapsAtCoordinate(a.XLine1.X, a.XLine1.Y, lineY, b.XLine2.X, b.XLine2.Y, lineY)
                    || ExtensionLineOverlapsAtCoordinate(a.XLine2.X, a.XLine2.Y, lineY, b.XLine1.X, b.XLine1.Y, lineY)
                    || ExtensionLineOverlapsAtCoordinate(a.XLine2.X, a.XLine2.Y, lineY, b.XLine2.X, b.XLine2.Y, lineY);
            }

            var lineX = side == DimSide.Left
                ? outline.MinX - targetOffset
                : outline.MaxX + targetOffset;
            return ExtensionLineOverlapsAtCoordinate(a.XLine1.Y, a.XLine1.X, lineX, b.XLine1.Y, b.XLine1.X, lineX)
                || ExtensionLineOverlapsAtCoordinate(a.XLine1.Y, a.XLine1.X, lineX, b.XLine2.Y, b.XLine2.X, lineX)
                || ExtensionLineOverlapsAtCoordinate(a.XLine2.Y, a.XLine2.X, lineX, b.XLine1.Y, b.XLine1.X, lineX)
                || ExtensionLineOverlapsAtCoordinate(a.XLine2.Y, a.XLine2.X, lineX, b.XLine2.Y, b.XLine2.X, lineX);
        }

        private bool ExtensionLineOverlapsAtCoordinate(
            double coordinateA,
            double featureA,
            double lineA,
            double coordinateB,
            double featureB,
            double lineB)
        {
            if (Math.Abs(coordinateA - coordinateB) > _config.GeometryTolerance)
            {
                return false;
            }

            var a1 = Math.Min(featureA, lineA);
            var a2 = Math.Max(featureA, lineA);
            var b1 = Math.Min(featureB, lineB);
            var b2 = Math.Max(featureB, lineB);
            return a1 <= b2 + _config.GeometryTolerance && b1 <= a2 + _config.GeometryTolerance;
        }

        private Point3d GetDimLinePoint(DeferredDim dim, DimSide side, OutlineFeature outline, double offset)
        {
            switch (side)
            {
                case DimSide.Bottom:
                    return new Point3d((dim.XLine1.X + dim.XLine2.X) / 2.0, GetDimLineCoordinate(dim, side, outline, offset), 0.0);
                case DimSide.Top:
                    return new Point3d((dim.XLine1.X + dim.XLine2.X) / 2.0, GetDimLineCoordinate(dim, side, outline, offset), 0.0);
                case DimSide.Left:
                    return new Point3d(GetDimLineCoordinate(dim, side, outline, offset), (dim.XLine1.Y + dim.XLine2.Y) / 2.0, 0.0);
                case DimSide.Right:
                    return new Point3d(GetDimLineCoordinate(dim, side, outline, offset), (dim.XLine1.Y + dim.XLine2.Y) / 2.0, 0.0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }

        private double GetDimLineCoordinate(DeferredDim dim, DimSide side, OutlineFeature outline, double offset)
        {
            double boundary;
            if (TryGetDimensionLocalBoundary(dim, side, outline, out boundary))
            {
                switch (side)
                {
                    case DimSide.Bottom:
                    case DimSide.Left:
                    {
                        var coordinate = boundary - offset;
                        if (!DimensionLineEntersOutlineInterior(dim, side, coordinate, outline))
                        {
                            return coordinate;
                        }
                        break;
                    }
                    case DimSide.Top:
                    case DimSide.Right:
                    {
                        var coordinate = boundary + offset;
                        if (!DimensionLineEntersOutlineInterior(dim, side, coordinate, outline))
                        {
                            return coordinate;
                        }
                        break;
                    }
                }
            }

            switch (side)
            {
                case DimSide.Bottom:
                    return outline.MinY - offset;
                case DimSide.Top:
                    return outline.MaxY + offset;
                case DimSide.Left:
                    return outline.MinX - offset;
                case DimSide.Right:
                    return outline.MaxX + offset;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }

        private bool TryGetDimensionLocalBoundary(DeferredDim dim, DimSide side, OutlineFeature outline, out double boundary)
        {
            boundary = 0.0;
            if (!CanUseLocalDimensionBoundary(dim))
            {
                return false;
            }

            if (dim.LooseChainId != 0)
            {
                return false;
            }

            return TryGetLocalHoleLocationBoundary(dim, side, outline, out boundary);
        }

        private bool DimensionLineEntersOutlineInterior(DeferredDim dim, DimSide side, double coordinate, OutlineFeature outline)
        {
            var isHorizontal = side == DimSide.Bottom || side == DimSide.Top;
            var min = isHorizontal
                ? Math.Min(dim.XLine1.X, dim.XLine2.X)
                : Math.Min(dim.XLine1.Y, dim.XLine2.Y);
            var max = isHorizontal
                ? Math.Max(dim.XLine1.X, dim.XLine2.X)
                : Math.Max(dim.XLine1.Y, dim.XLine2.Y);
            if (max - min <= _config.GeometryTolerance)
            {
                return false;
            }

            var samples = new[] { min, (min + max) / 2.0, max };
            foreach (var sample in samples)
            {
                var x = isHorizontal ? sample : coordinate;
                var y = isHorizontal ? coordinate : sample;
                if (IsPointInsideOutlineByRayCast(x, y, outline)
                    && !IsPointOnAnyOutlineSegment(new Point2d(x, y), outline))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPointOnAnyOutlineSegment(Point2d point, OutlineFeature outline)
        {
            return outline != null
                && outline.Segments.Any(segment => IsPointOnSegment(point, segment));
        }

        private bool TryGetLocalHoleLocationBoundary(DeferredDim dim, DimSide side, OutlineFeature outline, out double boundary)
        {
            boundary = 0.0;
            if (outline == null || outline.Segments.Count == 0)
            {
                return false;
            }

            var tolerance = Math.Max(_config.GeometryTolerance, 0.05);
            var midX = (dim.XLine1.X + dim.XLine2.X) / 2.0;
            var midY = (dim.XLine1.Y + dim.XLine2.Y) / 2.0;
            var minX = Math.Min(dim.XLine1.X, dim.XLine2.X);
            var maxX = Math.Max(dim.XLine1.X, dim.XLine2.X);
            var minY = Math.Min(dim.XLine1.Y, dim.XLine2.Y);
            var maxY = Math.Max(dim.XLine1.Y, dim.XLine2.Y);

            if (side == DimSide.Left || side == DimSide.Right)
            {
                var candidate = outline.Segments
                    .Where(s => s.IsVertical(tolerance))
                    .Where(s => s.LengthY > tolerance)
                    .Where(s => side == DimSide.Left ? s.MinX <= minX + tolerance : s.MinX >= maxX - tolerance)
                    .Select(s => new
                    {
                        Segment = s,
                        Coordinate = (s.Start.X + s.End.X) / 2.0,
                        Overlap = IntervalOverlap(minY, maxY, s.MinY, s.MaxY),
                        CoversMid = midY >= s.MinY - tolerance && midY <= s.MaxY + tolerance
                    })
                    .Where(c => c.Overlap > tolerance || c.CoversMid)
                    .OrderBy(c => Math.Abs(c.Coordinate - midX))
                    .ThenByDescending(c => c.Overlap)
                    .ThenByDescending(c => c.Segment.LengthY)
                    .FirstOrDefault();
                if (candidate == null)
                {
                    return false;
                }

                boundary = candidate.Coordinate;
                return true;
            }

            if (side == DimSide.Bottom || side == DimSide.Top)
            {
                var candidate = outline.Segments
                    .Where(s => s.IsHorizontal(tolerance))
                    .Where(s => s.LengthX > tolerance)
                    .Where(s => side == DimSide.Bottom ? s.MinY <= minY + tolerance : s.MinY >= maxY - tolerance)
                    .Select(s => new
                    {
                        Segment = s,
                        Coordinate = (s.Start.Y + s.End.Y) / 2.0,
                        Overlap = IntervalOverlap(minX, maxX, s.MinX, s.MaxX),
                        CoversMid = midX >= s.MinX - tolerance && midX <= s.MaxX + tolerance
                    })
                    .Where(c => c.Overlap > tolerance || c.CoversMid)
                    .OrderBy(c => Math.Abs(c.Coordinate - midY))
                    .ThenByDescending(c => c.Overlap)
                    .ThenByDescending(c => c.Segment.LengthX)
                    .FirstOrDefault();
                if (candidate == null)
                {
                    return false;
                }

                boundary = candidate.Coordinate;
                return true;
            }

            return false;
        }

        private static bool CanUseLocalDimensionBoundary(DeferredDim dim)
        {
            return dim.DimType == DimensionType.PinDistance
                || dim.DimType == DimensionType.PinGroupDistance;
        }

        private double IntervalOverlap(double firstMin, double firstMax, double secondMin, double secondMax)
        {
            return Math.Min(firstMax, secondMax) - Math.Max(firstMin, secondMin);
        }

        private (double A, double B) ComputeArrowInterval(DeferredDim dim, bool isHorizontal)
        {
            if (isHorizontal)
            {
                return (Math.Min(dim.XLine1.X, dim.XLine2.X), Math.Max(dim.XLine1.X, dim.XLine2.X));
            }
            return (Math.Min(dim.XLine1.Y, dim.XLine2.Y), Math.Max(dim.XLine1.Y, dim.XLine2.Y));
        }

        private TextBounds ComputePlacedTextBounds(DeferredDim dim, Point3d dimLinePoint, bool isHorizontal, double textHeight)
        {
            var interval = ComputeTextInterval(dim, isHorizontal, textHeight);
            var halfHeight = textHeight * 0.65;
            if (isHorizontal)
            {
                return new TextBounds
                {
                    MinX = interval.A,
                    MaxX = interval.B,
                    MinY = dimLinePoint.Y - halfHeight,
                    MaxY = dimLinePoint.Y + halfHeight
                };
            }

            return new TextBounds
            {
                MinX = dimLinePoint.X - halfHeight,
                MaxX = dimLinePoint.X + halfHeight,
                MinY = interval.A,
                MaxY = interval.B
            };
        }

        private bool HasExtensionLineTextConflict(PlacedDim placed, IList<PlacedDim> allPlacedDims)
        {
            foreach (var text in allPlacedDims.Select(p => p.TextBounds))
            {
                if (ExtensionLineCrossesText(placed, placed.Dim.XLine1, text) ||
                    ExtensionLineCrossesText(placed, placed.Dim.XLine2, text))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ExtensionLineCrossesText(PlacedDim placed, Point3d featurePoint, TextBounds text)
        {
            var tol = _config.GeometryTolerance;
            if (placed.Side == DimSide.Bottom || placed.Side == DimSide.Top)
            {
                var x = featurePoint.X;
                var y1 = Math.Min(featurePoint.Y, placed.DimLinePoint.Y);
                var y2 = Math.Max(featurePoint.Y, placed.DimLinePoint.Y);
                return x >= text.MinX - tol && x <= text.MaxX + tol &&
                    y1 <= text.MaxY + tol && y2 >= text.MinY - tol;
            }

            var y = featurePoint.Y;
            var x1 = Math.Min(featurePoint.X, placed.DimLinePoint.X);
            var x2 = Math.Max(featurePoint.X, placed.DimLinePoint.X);
            return y >= text.MinY - tol && y <= text.MaxY + tol &&
                x1 <= text.MaxX + tol && x2 >= text.MinX - tol;
        }

        private void DrawSegmentedExtensionLines(PlacedDim placed, IList<PlacedDim> allPlacedDims, double textHeight)
        {
            DrawSegmentedExtensionLine(placed, placed.Dim.XLine1, allPlacedDims, textHeight);
            DrawSegmentedExtensionLine(placed, placed.Dim.XLine2, allPlacedDims, textHeight);
        }

        private void DrawSegmentedExtensionLine(PlacedDim placed, Point3d featurePoint, IList<PlacedDim> allPlacedDims, double textHeight)
        {
            var breakLength = Math.Max(textHeight * 0.9, Scale(_config.ArrowSize * 0.75));
            var ranges = GetBreakRangesForExtensionLine(placed, featurePoint, allPlacedDims, breakLength);
            var dimPoint = GetExtensionLineEndPoint(placed, featurePoint);
            if (placed.Side == DimSide.Bottom || placed.Side == DimSide.Top)
            {
                AddSegmentedLine(featurePoint.X, featurePoint.Y, dimPoint.Y, ranges, vertical: true);
            }
            else
            {
                AddSegmentedLine(featurePoint.Y, featurePoint.X, dimPoint.X, ranges, vertical: false);
            }
        }

        private Point3d GetExtensionLineEndPoint(PlacedDim placed, Point3d featurePoint)
        {
            if (placed.Side == DimSide.Bottom || placed.Side == DimSide.Top)
            {
                return new Point3d(featurePoint.X, placed.DimLinePoint.Y, 0.0);
            }

            return new Point3d(placed.DimLinePoint.X, featurePoint.Y, 0.0);
        }

        private List<(double A, double B)> GetBreakRangesForExtensionLine(
            PlacedDim placed,
            Point3d featurePoint,
            IList<PlacedDim> allPlacedDims,
            double breakLength)
        {
            var dimPoint = GetExtensionLineEndPoint(placed, featurePoint);
            var candidates = allPlacedDims
                .Select(p => p.TextBounds)
                .Where(text => ExtensionLineCrossesText(placed, featurePoint, text))
                .Select(text => ToBreakRangeCandidate(placed, featurePoint, dimPoint, text, breakLength))
                .Where(r => r.B > r.A + _config.GeometryTolerance)
                .OrderBy(r => r.DistanceToFeature)
                .ThenBy(r => r.Length)
                .ToList();

            if (candidates.Count == 0)
            {
                return new List<(double A, double B)>();
            }

            var best = candidates[0];
            return new List<(double A, double B)> { (best.A, best.B) };
        }

        private (double A, double B, double DistanceToFeature, double Length) ToBreakRangeCandidate(
            PlacedDim placed,
            Point3d featurePoint,
            Point3d dimPoint,
            TextBounds text,
            double breakLength)
        {
            double featureVariable;
            double dimVariable;
            double textCenter;

            if (placed.Side == DimSide.Bottom || placed.Side == DimSide.Top)
            {
                featureVariable = featurePoint.Y;
                dimVariable = dimPoint.Y;
                textCenter = (text.MinY + text.MaxY) / 2.0;
            }
            else
            {
                featureVariable = featurePoint.X;
                dimVariable = dimPoint.X;
                textCenter = (text.MinX + text.MaxX) / 2.0;
            }

            var min = Math.Min(featureVariable, dimVariable);
            var max = Math.Max(featureVariable, dimVariable);
            var center = Math.Max(min, Math.Min(max, textCenter));
            var half = breakLength / 2.0;
            var a = Math.Max(min, center - half);
            var b = Math.Min(max, center + half);
            return (a, b, Math.Abs(center - featureVariable), b - a);
        }

        private void AddSegmentedLine(double fixedCoord, double startVariable, double endVariable, IList<(double A, double B)> breakRanges, bool vertical)
        {
            var min = Math.Min(startVariable, endVariable);
            var max = Math.Max(startVariable, endVariable);
            var ranges = breakRanges
                .Select(r => (A: Math.Max(min, Math.Min(r.A, r.B)), B: Math.Min(max, Math.Max(r.A, r.B))))
                .Where(r => r.B > r.A + _config.GeometryTolerance)
                .OrderBy(r => r.A)
                .ToList();

            var cursor = min;
            foreach (var range in ranges)
            {
                AddLineSegment(fixedCoord, cursor, range.A, vertical);
                if (range.B > cursor)
                {
                    cursor = range.B;
                }
            }

            AddLineSegment(fixedCoord, cursor, max, vertical);
        }

        private void AddLineSegment(double fixedCoord, double a, double b, bool vertical)
        {
            if (b - a <= _config.GeometryTolerance)
            {
                return;
            }

            var start = vertical ? new Point3d(fixedCoord, a, 0.0) : new Point3d(a, fixedCoord, 0.0);
            var end = vertical ? new Point3d(fixedCoord, b, 0.0) : new Point3d(b, fixedCoord, 0.0);
            var line = new Line(start, end);
            line.SetDatabaseDefaults(_db);
            line.Layer = _annotationLayer;
            Append(line);
        }

        private bool HasStrictArrowConflict(double aA, double aB, double bA, double bB)
        {
            // Two arrow intervals [aA,aB] and [bA,bB] are strictly overlapping if
            // any endpoint of one falls strictly inside the other (not just touching).
            // Endpoint-shared chains like [0,10] and [10,16] are allowed (no strict overlap).
            var tol = _config.GeometryTolerance;
            bool aEndInB = (bA + tol < aA && aA < bB - tol) || (bA + tol < aB && aB < bB - tol);
            bool bEndInA = (aA + tol < bA && bA < aB - tol) || (aA + tol < bB && bB < aB - tol);
            return aEndInB || bEndInA;
        }

        private (double A, double B) ComputeTextInterval(DeferredDim dim, bool isHorizontal, double textHeight)
        {
            double center, span;
            if (isHorizontal)
            {
                center = (dim.XLine1.X + dim.XLine2.X) / 2.0;
                span = Math.Abs(dim.XLine2.X - dim.XLine1.X);
            }
            else
            {
                center = (dim.XLine1.Y + dim.XLine2.Y) / 2.0;
                span = Math.Abs(dim.XLine2.Y - dim.XLine1.Y);
            }

            var text = string.IsNullOrEmpty(dim.OverrideText)
                ? _config.FormatNumber(span)
                : dim.OverrideText;
            var textWidth = Math.Max(text.Length, 2) * textHeight * 0.7;

            return (center - textWidth / 2.0, center + textWidth / 2.0);
        }

        private double GetDimensionTextLength(DeferredDim dim, double textHeight)
        {
            var text = GetDimensionText(dim);
            return Math.Max(text.Length, 2) * textHeight * 0.7;
        }

        private string GetDimensionText(DeferredDim dim)
        {
            return string.IsNullOrEmpty(dim.OverrideText)
                ? _config.FormatNumber(dim.Span)
                : dim.OverrideText;
        }

        private bool IsDuplicate(DeferredDim a, DeferredDim b, bool isHorizontal)
        {
            double a1, a2, b1, b2;
            if (isHorizontal)
            {
                a1 = Math.Min(a.XLine1.X, a.XLine2.X); a2 = Math.Max(a.XLine1.X, a.XLine2.X);
                b1 = Math.Min(b.XLine1.X, b.XLine2.X); b2 = Math.Max(b.XLine1.X, b.XLine2.X);
            }
            else
            {
                a1 = Math.Min(a.XLine1.Y, a.XLine2.Y); a2 = Math.Max(a.XLine1.Y, a.XLine2.Y);
                b1 = Math.Min(b.XLine1.Y, b.XLine2.Y); b2 = Math.Max(b.XLine1.Y, b.XLine2.Y);
            }
            if (Math.Abs(a1 - b1) > _config.GeometryTolerance) return false;
            if (Math.Abs(a2 - b2) > _config.GeometryTolerance) return false;
            var textA = a.OverrideText ?? string.Empty;
            var textB = b.OverrideText ?? string.Empty;
            return string.Equals(textA, textB, StringComparison.Ordinal);
        }

        private void SuppressDuplicateMeasuredDimensions(List<DeferredDim> dims, bool isHorizontal)
        {
            for (int i = 0; i < dims.Count; i++)
            {
                var bestIndex = i;
                for (int j = i + 1; j < dims.Count; j++)
                {
                    if (!IsSameMeasuredDimension(dims[i], dims[j], isHorizontal))
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
                    if (IsSameMeasuredDimension(dims[i], dims[j], isHorizontal))
                    {
                        dims.RemoveAt(j);
                    }
                }
            }
        }

        private bool IsSameMeasuredDimension(DeferredDim a, DeferredDim b, bool isHorizontal)
        {
            var aArrow = ComputeArrowInterval(a, isHorizontal);
            var bArrow = ComputeArrowInterval(b, isHorizontal);
            if (Math.Abs(aArrow.A - bArrow.A) > _config.GeometryTolerance)
            {
                return false;
            }

            if (Math.Abs(aArrow.B - bArrow.B) > _config.GeometryTolerance)
            {
                return false;
            }

            return Math.Abs(a.Span - b.Span) <= _config.GeometryTolerance;
        }

        private int CompareDuplicatePreference(DeferredDim a, DeferredDim b)
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

        private static int GetDimensionPreferenceRank(DeferredDim dim)
        {
            switch (dim.DimType)
            {
                case DimensionType.PinDistance:
                    return 0;
                case DimensionType.PinGroupDistance:
                    return 1;
                case DimensionType.DatumHoleLocationX:
                case DimensionType.DatumHoleLocationY:
                    return 2;
                case DimensionType.OverallWidth:
                case DimensionType.OverallHeight:
                    return 3;
                case DimensionType.HoleLocation:
                case DimensionType.Normal:
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

            if (end <= start)
            {
                return null;
            }

            double value;
            if (double.TryParse(
                text.Substring(start, end - start),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out value))
            {
                return value;
            }

            return null;
        }

        private bool TextCoversOutline(DeferredDim dim, DimSide side, double offset, double textHeight, OutlineFeature outline, bool isHorizontal)
        {
            double localBoundary;
            if (TryGetDimensionLocalBoundary(dim, side, outline, out localBoundary)
                && !IsGlobalBoundaryCoordinate(localBoundary, side, outline))
            {
                var localCoordinate = (side == DimSide.Bottom || side == DimSide.Left)
                    ? localBoundary - offset
                    : localBoundary + offset;
                if (!DimensionLineEntersOutlineInterior(dim, side, localCoordinate, outline))
                {
                    return false;
                }
            }

            var textBox = ComputeTextInterval(dim, isHorizontal, textHeight);
            var halfHeight = textHeight / 2.0;

            switch (side)
            {
                case DimSide.Bottom:
                {
                    var textY = outline.MinY - offset;
                    if (textY + halfHeight < outline.MinY - _config.GeometryTolerance) return false;
                    return textBox.A < outline.MaxX + _config.GeometryTolerance
                        && textBox.B > outline.MinX - _config.GeometryTolerance;
                }
                case DimSide.Top:
                {
                    var textY = outline.MaxY + offset;
                    if (textY - halfHeight > outline.MaxY + _config.GeometryTolerance) return false;
                    return textBox.A < outline.MaxX + _config.GeometryTolerance
                        && textBox.B > outline.MinX - _config.GeometryTolerance;
                }
                case DimSide.Left:
                {
                    var textX = outline.MinX - offset;
                    if (textX + halfHeight < outline.MinX - _config.GeometryTolerance) return false;
                    return textBox.A < outline.MaxY + _config.GeometryTolerance
                        && textBox.B > outline.MinY - _config.GeometryTolerance;
                }
                case DimSide.Right:
                {
                    var textX = outline.MaxX + offset;
                    if (textX - halfHeight > outline.MaxX + _config.GeometryTolerance) return false;
                    return textBox.A < outline.MaxY + _config.GeometryTolerance
                        && textBox.B > outline.MinY - _config.GeometryTolerance;
                }
                default:
                    return false;
            }
        }

        private bool IsGlobalBoundaryCoordinate(double coordinate, DimSide side, OutlineFeature outline)
        {
            switch (side)
            {
                case DimSide.Bottom:
                    return Math.Abs(coordinate - outline.MinY) <= _config.GeometryTolerance;
                case DimSide.Top:
                    return Math.Abs(coordinate - outline.MaxY) <= _config.GeometryTolerance;
                case DimSide.Left:
                    return Math.Abs(coordinate - outline.MinX) <= _config.GeometryTolerance;
                case DimSide.Right:
                    return Math.Abs(coordinate - outline.MaxX) <= _config.GeometryTolerance;
                default:
                    return false;
            }
        }

        private static bool AreCompatible(double aA, double aB, double bA, double bB, double gap)
        {
            return aB + gap <= bA || bB + gap <= aA;
        }

        public void DrawHoleDiameterLeaders(OutlineFeature outline, IList<IList<HoleFeature>> diameterGroups)
        {
            var groupIndex = 0;
            foreach (var group in diameterGroups.Where(g => g.Count > 0))
            {
                var ordered = group.OrderBy(h => h.Center.Y).ThenBy(h => h.Center.X).ToList();
                var representative = PickLeaderTarget(ordered, outline);
                var points = BuildHoleLeaderPoints(ordered, representative, groupIndex);

                AddDiameterLeader(
                    points.ArrowPoint,
                    points.LandingPoint,
                    points.TextPoint,
                    representative.IsThreadHole
                        ? _config.FormatThreadCallout(representative.Diameter, ordered.Count, representative.ThreadCallout)
                        : _config.FormatHoleCallout(representative.Diameter, ordered.Count, representative.FitTolerance));
                groupIndex++;
            }
        }

        public void DrawChamferLeaders(OutlineFeature outline, bool manualTextPlacement)
        {
            ApplyChamferTextPrecision(outline);
            var groupIndex = 0;
            foreach (var group in outline.Chamfers.GroupBy(c => c.Text).OrderBy(g => g.Key))
            {
                var features = group.ToList();
                if (features.Count == 0)
                {
                    continue;
                }

                var representative = PickChamferLeaderTarget(features, outline);
                var target = Midpoint(representative.StartPoint, representative.EndPoint);
                var text = FormatRepeatedFeatureText(group.Key, features.Count);
                Point3d manualTextPoint;
                if (manualTextPlacement && TryPromptCornerLeaderTextPoint(text, out manualTextPoint))
                {
                    AddManualCornerFeatureLeader(target, manualTextPoint, text);
                }
                else
                {
                    AddCornerFeatureLeader(outline, target, text, groupIndex, strictOutside: true);
                }
                groupIndex++;
            }
        }

        public void DrawFilletLeaders(OutlineFeature outline, bool manualTextPlacement)
        {
            var groupIndex = outline.Chamfers.GroupBy(c => c.Text).Count();
            foreach (var group in outline.Fillets.GroupBy(f => f.Text).OrderBy(g => g.Key))
            {
                var features = group.ToList();
                if (features.Count == 0)
                {
                    continue;
                }

                var representative = PickFilletLeaderTarget(features, outline);
                var text = FormatRepeatedFeatureText(group.Key, features.Count);
                Point3d manualTextPoint;
                if (manualTextPlacement && TryPromptCornerLeaderTextPoint(text, out manualTextPoint))
                {
                    AddManualFilletRadiusDimension(representative, manualTextPoint, text);
                }
                else
                {
                    AddFilletRadiusDimension(outline, representative, text, groupIndex);
                }
                groupIndex++;
            }
        }

        public void DrawCornerFeatureLeadersWithJig(Editor editor, OutlineFeature outline)
        {
            ApplyChamferTextPrecision(outline);
            var textHeight = GetDimStyleTextHeight(_diameterCalloutDimStyleId);
            foreach (var group in outline.Chamfers.GroupBy(c => c.Text).OrderBy(g => g.Key))
            {
                var features = group.ToList();
                if (features.Count == 0)
                {
                    continue;
                }

                var representative = PickChamferLeaderTarget(features, outline);
                var target = Midpoint(representative.StartPoint, representative.EndPoint);
                var text = FormatRepeatedFeatureText(group.Key, features.Count);
                Point3d textPoint;
                var result = PromptCornerFeatureLeaderPoint(editor, target, text, textHeight, out textPoint);
                if (result == CornerFeatureJigResult.Skip)
                {
                    editor.WriteMessage("\nSkipped corner callout {0}.", text);
                    continue;
                }

                if (result == CornerFeatureJigResult.Cancel)
                {
                    editor.WriteMessage("\nCorner callout placement canceled.");
                    return;
                }

                AddManualCornerFeatureLeader(target, textPoint, text);
            }

            foreach (var group in outline.Fillets.GroupBy(f => f.Text).OrderBy(g => g.Key))
            {
                var features = group.ToList();
                if (features.Count == 0)
                {
                    continue;
                }

                var representative = PickFilletLeaderTarget(features, outline);
                var target = GetArcLeaderTarget(representative);
                var text = FormatRepeatedFeatureText(group.Key, features.Count);
                Point3d textPoint;
                var result = PromptCornerFeatureLeaderPoint(editor, target, text, textHeight, out textPoint);
                if (result == CornerFeatureJigResult.Skip)
                {
                    editor.WriteMessage("\nSkipped corner callout {0}.", text);
                    continue;
                }

                if (result == CornerFeatureJigResult.Cancel)
                {
                    editor.WriteMessage("\nCorner callout placement canceled.");
                    return;
                }

                AddManualFilletRadiusDimension(representative, textPoint, text);
            }
        }

        private void ApplyChamferTextPrecision(OutlineFeature outline)
        {
            if (outline == null || outline.Chamfers.Count == 0)
            {
                return;
            }

            var precision = GetDimStyleLinearPrecision(_diameterCalloutDimStyleId);
            foreach (var chamfer in outline.Chamfers)
            {
                var value = chamfer.Value > _config.GeometryTolerance
                    ? chamfer.Value
                    : Math.Max(chamfer.DeltaX, chamfer.DeltaY);
                chamfer.Text = "C" + FormatNumberWithPrecision(value, precision);
            }
        }

        public void DrawSlotRadiusLeadersWithJig(Editor editor, IEnumerable<SlotFeature> slots)
        {
            if (editor == null || slots == null)
            {
                return;
            }

            var textHeight = GetDimStyleTextHeight(_diameterCalloutDimStyleId);
            foreach (var group in GroupSlotsByRadius(slots))
            {
                if (group.Count == 0)
                {
                    continue;
                }

                var slot = group[0];
                var target = slot.LeaderArcTarget;
                var text = FormatSlotRadiusCallout(group);
                Point3d textPoint;
                var result = PromptCornerFeatureLeaderPoint(editor, target, text, textHeight, out textPoint);
                if (result == CornerFeatureJigResult.Skip)
                {
                    editor.WriteMessage("\nSkipped U slot radius callout {0}.", text);
                    continue;
                }

                if (result == CornerFeatureJigResult.Cancel)
                {
                    editor.WriteMessage("\nU slot radius callout placement canceled.");
                    return;
                }

                AddManualSlotRadiusDimension(slot, textPoint, text);
            }
        }

        private List<List<SlotFeature>> GroupSlotsByRadius(IEnumerable<SlotFeature> slots)
        {
            var groups = new List<List<SlotFeature>>();
            if (slots == null)
            {
                return groups;
            }

            foreach (var slot in slots.Where(s => s != null && s.Radius > _config.GeometryTolerance))
            {
                var tolerance = Math.Max(_config.GeometryTolerance, Math.Max(slot.Radius, 1.0) * 0.02);
                var group = groups.FirstOrDefault(g =>
                    g[0].IsSingleArcSlot == slot.IsSingleArcSlot
                    && Math.Abs(g[0].Radius - slot.Radius) <= tolerance);
                if (group == null)
                {
                    group = new List<SlotFeature>();
                    groups.Add(group);
                }

                group.Add(slot);
            }

            return groups
                .OrderBy(g => g[0].Radius)
                .ToList();
        }

        private string FormatSlotRadiusCallout(IList<SlotFeature> slots)
        {
            var slot = slots[0];
            var baseText = (slot.IsSingleArcSlot ? "R" : "2-R") + _config.FormatNumber(slot.Radius);
            return slots.Count > 1
                ? slots.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "x" + baseText
                : baseText;
        }

        private void AddFilletRadiusDimension(OutlineFeature outline, FilletFeature feature, string text, int groupIndex)
        {
            var target = GetArcLeaderTarget(feature);
            var leaderOffset = Scale(_config.LeaderOffset + groupIndex * 3.0);
            var textGap = Scale(1.2);
            var placement = PickCornerLeaderPlacement(outline, target, leaderOffset, textGap, groupIndex, text, strictOutside: false);
            var centerPoint = new Point3d(feature.Center.X, feature.Center.Y, 0.0);
            var chordPoint = new Point3d(target.X, target.Y, 0.0);
            var textPoint = new Point3d(placement.Text.X, placement.Text.Y, 0.0);
            var leaderLength = Math.Max(chordPoint.DistanceTo(textPoint), Scale(_config.LeaderOffset));

            var dimension = new RadialDimension(
                centerPoint,
                chordPoint,
                leaderLength,
                text,
                _diameterCalloutDimStyleId);
            dimension.SetDatabaseDefaults(_db);
            dimension.Layer = _annotationLayer;
            dimension.DimensionStyle = _diameterCalloutDimStyleId;
            dimension.DimensionText = text;
            Append(dimension);
        }

        private bool TryPromptCornerLeaderTextPoint(string text, out Point3d textPoint)
        {
            textPoint = Point3d.Origin;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return false;
            }

            var options = new PromptPointOptions("\nSelect text point for " + text + " <Auto>: ");
            options.AllowNone = true;
            var result = doc.Editor.GetPoint(options);
            if (result.Status != PromptStatus.OK)
            {
                return false;
            }

            textPoint = result.Value;
            return true;
        }

        private void AddManualCornerFeatureLeader(Point2d target, Point3d textPoint, string text)
        {
            var arrowPoint = new Point3d(target.X, target.Y, 0.0);
            AddCornerLeader(arrowPoint, textPoint, textPoint, text);
        }

        private void AddManualFilletRadiusDimension(FilletFeature feature, Point3d textPoint, string text)
        {
            var target = GetArcLeaderTarget(feature);
            var centerPoint = new Point3d(feature.Center.X, feature.Center.Y, 0.0);
            var chordPoint = new Point3d(target.X, target.Y, 0.0);
            var leaderLength = Math.Max(chordPoint.DistanceTo(textPoint), Scale(_config.LeaderOffset));

            var dimension = new RadialDimension(
                centerPoint,
                chordPoint,
                leaderLength,
                text,
                _diameterCalloutDimStyleId);
            dimension.SetDatabaseDefaults(_db);
            dimension.Layer = _annotationLayer;
            dimension.DimensionStyle = _diameterCalloutDimStyleId;
            dimension.DimensionText = text;
            dimension.UsingDefaultTextPosition = false;
            dimension.TextPosition = textPoint;
            Append(dimension);
            if (_appendToDatabase)
            {
                dimension.RecomputeDimensionBlock(true);
            }
        }

        private void AddManualSlotRadiusDimension(SlotFeature slot, Point3d textPoint, string text)
        {
            var target = slot.LeaderArcTarget;
            var arrowPoint = new Point3d(target.X, target.Y, 0.0);
            AddCornerLeader(arrowPoint, textPoint, textPoint, text);
        }

        private static string FormatRepeatedFeatureText(string text, int count)
        {
            return count > 1 ? count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + text : text;
        }

        private ChamferFeature PickChamferLeaderTarget(IList<ChamferFeature> features, OutlineFeature outline)
        {
            var centerX = (outline.MinX + outline.MaxX) / 2.0;
            var centerY = (outline.MinY + outline.MaxY) / 2.0;
            return features
                .OrderByDescending(f => DistanceSquared(Midpoint(f.StartPoint, f.EndPoint), new Point2d(centerX, centerY)))
                .First();
        }

        private FilletFeature PickFilletLeaderTarget(IList<FilletFeature> features, OutlineFeature outline)
        {
            var centerX = (outline.MinX + outline.MaxX) / 2.0;
            var centerY = (outline.MinY + outline.MaxY) / 2.0;
            return features
                .OrderByDescending(f => DistanceSquared(GetArcLeaderTarget(f), new Point2d(centerX, centerY)))
                .First();
        }

        private void AddCornerFeatureLeader(OutlineFeature outline, Point2d target, string text, int groupIndex, bool strictOutside)
        {
            var leaderOffset = Scale(_config.LeaderOffset + groupIndex * 3.0);
            var textGap = Scale(1.2);
            var arrowPoint = new Point3d(target.X, target.Y, 0.0);
            var placement = PickCornerLeaderPlacement(outline, target, leaderOffset, textGap, groupIndex, text, strictOutside);
            var landingPoint = new Point3d(
                placement.Landing.X,
                placement.Landing.Y,
                0.0);
            var textPoint = new Point3d(
                placement.Text.X,
                placement.Text.Y,
                0.0);

            AddCornerLeader(arrowPoint, landingPoint, textPoint, text);
        }

        private CornerLeaderPlacement PickCornerLeaderPlacement(
            OutlineFeature outline,
            Point2d target,
            double leaderOffset,
            double textGap,
            int groupIndex,
            string text,
            bool strictOutside)
        {
            var centerX = (outline.MinX + outline.MaxX) / 2.0;
            var centerY = (outline.MinY + outline.MaxY) / 2.0;
            var sx = target.X < centerX ? -1.0 : 1.0;
            var sy = target.Y < centerY ? -1.0 : 1.0;
            var stagger = Scale(groupIndex * 3.0);
            var directions = new List<Vector2d>
            {
                new Vector2d(0.0, sy),
                new Vector2d(sx, sy).GetNormal(),
                new Vector2d(-sx, sy).GetNormal(),
                new Vector2d(sx, 0.0),
                new Vector2d(-sx, 0.0),
                new Vector2d(sx, -sy).GetNormal(),
                new Vector2d(-sx, -sy).GetNormal(),
                new Vector2d(0.0, -sy)
            };
            double[] distanceFactors = strictOutside
                ? new[] { 0.85, 1.0, 1.2, 1.45, 1.75, 2.1, 2.5 }
                : new[] { 1.0, 1.25, 1.5, 2.0, 2.6 };

            var placements = directions
                .SelectMany(d => distanceFactors.Select(f => BuildCornerLeaderPlacement(target, d, leaderOffset * f, textGap, stagger)))
                .ToList();

            if (strictOutside)
            {
                var outsidePlacements = placements
                    .Where(p => IsCornerLeaderOutsideOutline(outline, target, p))
                    .ToList();
                if (outsidePlacements.Count > 0)
                {
                    placements = outsidePlacements;
                }
            }

            return placements
                .OrderBy(p => ScoreCornerLeaderPlacement(outline, target, p, text))
                .First();
        }

        private bool IsCornerLeaderOutsideOutline(OutlineFeature outline, Point2d target, CornerLeaderPlacement placement)
        {
            if (IsPointInsideOutlineByRayCast(placement.Landing.X, placement.Landing.Y, outline))
            {
                return false;
            }

            if (IsPointInsideOutlineByRayCast(placement.Text.X, placement.Text.Y, outline))
            {
                return false;
            }

            var midpoint = Midpoint(target, placement.Landing);
            return !IsPointInsideOutlineByRayCast(midpoint.X, midpoint.Y, outline);
        }

        private static CornerLeaderPlacement BuildCornerLeaderPlacement(Point2d target, Vector2d direction, double leaderOffset, double textGap, double stagger)
        {
            var landing = new Point2d(
                target.X + direction.X * leaderOffset,
                target.Y + direction.Y * leaderOffset + Math.Sign(direction.Y) * stagger);
            var textDirectionX = Math.Abs(direction.X) <= 1e-9 ? -1.0 : Math.Sign(direction.X);
            var text = new Point2d(
                landing.X + textDirectionX * textGap,
                landing.Y);
            return new CornerLeaderPlacement(landing, text, textDirectionX < 0.0);
        }

        private double ScoreCornerLeaderPlacement(OutlineFeature outline, Point2d target, CornerLeaderPlacement placement, string textValue)
        {
            var score = 0.0;
            var text = placement.Text;
            var margin = Scale(_config.FirstDimOffset + _config.TextHeight * 3.0);
            var textBounds = ComputeCornerLeaderTextBounds(placement, textValue);
            var centerY = (outline.MinY + outline.MaxY) / 2.0;
            var targetNearTop = target.Y >= centerY;
            var targetNearBottom = target.Y < centerY;

            if (text.X > outline.MinX && text.X < outline.MaxX && text.Y > outline.MinY && text.Y < outline.MaxY)
            {
                score += 50000.0;
            }

            if (IsPointInsideOutlineByRayCast(placement.Landing.X, placement.Landing.Y, outline))
            {
                score += 50000.0;
            }

            if (IsPointInsideOutlineByRayCast(text.X, text.Y, outline))
            {
                score += 50000.0;
            }

            var leaderMidpoint = Midpoint(target, placement.Landing);
            if (IsPointInsideOutlineByRayCast(leaderMidpoint.X, leaderMidpoint.Y, outline))
            {
                score += 50000.0;
            }

            if (targetNearTop && text.Y < target.Y - _config.GeometryTolerance)
            {
                score += 8000.0;
            }

            if (targetNearBottom && text.Y > target.Y + _config.GeometryTolerance)
            {
                score += 8000.0;
            }

            if (_leftDims.Count > 0 && text.X < outline.MinX && text.X > outline.MinX - margin * 2.5)
            {
                score += 400.0;
            }

            if (_rightDims.Count > 0 && text.X > outline.MaxX && text.X < outline.MaxX + margin * 2.5)
            {
                score += 400.0;
            }

            if (_bottomDims.Count > 0 && text.Y < outline.MinY && text.Y > outline.MinY - margin * 2.5)
            {
                score += 400.0;
            }

            if (_topDims.Count > 0 && text.Y > outline.MaxY && text.Y < outline.MaxY + margin * 2.5)
            {
                score += 400.0;
            }

            foreach (var obstacle in _linearDimTextObstacles)
            {
                if (BoundsOverlap(textBounds, obstacle, Scale(0.8)))
                {
                    score += 3500.0;
                }
            }

            var preferredDistance = Scale(_config.LeaderOffset * 1.35);
            var actualDistance = placement.Landing.GetDistanceTo(target);
            score += Math.Abs(actualDistance - preferredDistance) * 40.0;
            score += actualDistance * 2.0;
            score += Math.Abs(text.X - placement.Landing.X) * 0.01;
            score += Math.Abs(text.Y - placement.Landing.Y) * 0.01;
            return score;
        }

        private TextBounds ComputeCornerLeaderTextBounds(Point2d textPoint, string text)
        {
            var textHeight = GetDimStyleTextHeight(_diameterCalloutDimStyleId);
            var width = Math.Max((text ?? string.Empty).Length, 2) * textHeight * 0.75;
            var halfHeight = textHeight * 0.65;
            return new TextBounds
            {
                MinX = textPoint.X,
                MaxX = textPoint.X + width,
                MinY = textPoint.Y - halfHeight,
                MaxY = textPoint.Y + halfHeight
            };
        }

        private TextBounds ComputeCornerLeaderTextBounds(CornerLeaderPlacement placement, string text)
        {
            var textHeight = GetDimStyleTextHeight(_diameterCalloutDimStyleId);
            var width = Math.Max((text ?? string.Empty).Length, 2) * textHeight * 0.75;
            var halfHeight = textHeight * 0.65;
            return new TextBounds
            {
                MinX = placement.TextExtendsLeft ? placement.Text.X - width : placement.Text.X,
                MaxX = placement.TextExtendsLeft ? placement.Text.X : placement.Text.X + width,
                MinY = placement.Text.Y - halfHeight,
                MaxY = placement.Text.Y + halfHeight
            };
        }

        private static bool BoundsOverlap(TextBounds a, TextBounds b, double gap)
        {
            return a.MinX <= b.MaxX + gap
                && a.MaxX >= b.MinX - gap
                && a.MinY <= b.MaxY + gap
                && a.MaxY >= b.MinY - gap;
        }

        private enum CornerFeatureJigResult
        {
            Picked,
            Skip,
            Cancel
        }

        private static CornerFeatureJigResult PromptCornerFeatureLeaderPoint(
            Editor editor,
            Point2d target,
            string text,
            double textHeight,
            out Point3d textPoint)
        {
            textPoint = Point3d.Origin;
            var arrowPoint = new Point3d(target.X, target.Y, 0.0);
            var jig = new CornerFeatureLeaderJig(arrowPoint, text, textHeight);
            var result = editor.Drag(jig);
            if (result.Status == PromptStatus.OK)
            {
                textPoint = jig.TextPoint;
                return CornerFeatureJigResult.Picked;
            }

            if (jig.SkipRequested || result.Status == PromptStatus.None)
            {
                return CornerFeatureJigResult.Skip;
            }

            return CornerFeatureJigResult.Cancel;
        }

        private void AddCornerLeader(Point3d arrowPoint, Point3d landingPoint, Point3d textPoint, string text)
        {
            var mtext = new MText();
            mtext.SetDatabaseDefaults(_db);
            mtext.Contents = text;
            mtext.TextHeight = GetDimStyleTextHeight(_diameterCalloutDimStyleId);
            mtext.TextStyleId = GetDimStyleTextStyle(_diameterCalloutDimStyleId);
            mtext.Location = textPoint;
            var textIsLeft = textPoint.X < landingPoint.X - _config.GeometryTolerance
                || (Math.Abs(textPoint.X - landingPoint.X) <= _config.GeometryTolerance
                    && textPoint.X < arrowPoint.X);
            mtext.Attachment = textIsLeft
                ? AttachmentPoint.MiddleRight
                : AttachmentPoint.MiddleLeft;
            mtext.Layer = _annotationLayer;
            mtext.Color = GetDimStyleTextColor(_diameterCalloutDimStyleId);
            Append(mtext);

            if (!_appendToDatabase)
            {
                var previewLine = new Line(arrowPoint, landingPoint);
                previewLine.SetDatabaseDefaults(_db);
                previewLine.Layer = GetCurrentLayerName();
                previewLine.Color = GetCurrentEntityColor();
                Append(previewLine);
                return;
            }

            var leader = new Leader();
            leader.SetDatabaseDefaults(_db);
            leader.Layer = GetCurrentLayerName();
            leader.DimensionStyle = _diameterCalloutDimStyleId;
            leader.Color = GetCurrentEntityColor();
            leader.AppendVertex(arrowPoint);
            leader.AppendVertex(landingPoint);
            Append(leader);
            leader.Annotation = mtext.ObjectId;
            leader.EvaluateLeader();
            leader.Color = GetCurrentEntityColor();
        }

        private Vector2d GetHorizontalOutwardDirection(OutlineFeature outline, Point2d target)
        {
            var centerX = (outline.MinX + outline.MaxX) / 2.0;
            return new Vector2d(target.X < centerX ? -1.0 : 1.0, 0.0);
        }

        private double GetCornerLeaderYShift(OutlineFeature outline, Point2d target, int groupIndex)
        {
            var centerY = (outline.MinY + outline.MaxY) / 2.0;
            var nearTop = Math.Abs(outline.MaxY - target.Y) <= outline.Height * 0.25;
            var nearBottom = Math.Abs(target.Y - outline.MinY) <= outline.Height * 0.25;
            var stagger = Scale(groupIndex * 3.0);

            if (nearTop)
            {
                return Scale(8.0) + stagger;
            }

            if (nearBottom)
            {
                return -Scale(8.0) - stagger;
            }

            return target.Y >= centerY ? Scale(4.0) + stagger : -Scale(4.0) - stagger;
        }

        private sealed class CornerLeaderPlacement
        {
            public CornerLeaderPlacement(Point2d landing, Point2d text, bool textExtendsLeft)
            {
                Landing = landing;
                Text = text;
                TextExtendsLeft = textExtendsLeft;
            }

            public Point2d Landing { get; }
            public Point2d Text { get; }
            public bool TextExtendsLeft { get; }
        }

        private Vector2d GetOutwardDirection(OutlineFeature outline, Point2d target)
        {
            var centerX = (outline.MinX + outline.MaxX) / 2.0;
            var centerY = (outline.MinY + outline.MaxY) / 2.0;
            var x = target.X < centerX ? -1.0 : 1.0;
            var y = target.Y < centerY ? -1.0 : 1.0;
            return new Vector2d(x, y).GetNormal();
        }

        private static Point2d Midpoint(Point2d a, Point2d b)
        {
            return new Point2d((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
        }

        private static Point3d Midpoint(Point3d a, Point3d b)
        {
            return new Point3d((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);
        }

        private static Point2d GetArcLeaderTarget(FilletFeature feature)
        {
            var startVector = feature.StartPoint - feature.Center;
            var endVector = feature.EndPoint - feature.Center;
            var combined = startVector + endVector;
            if (combined.Length <= 1e-9)
            {
                combined = startVector;
            }

            var direction = combined.GetNormal();
            return feature.Center + direction * feature.Radius;
        }

        private static double DistanceSquared(Point2d a, Point2d b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        private HoleFeature PickLeaderTarget(IList<HoleFeature> ordered, OutlineFeature outline)
        {
            if (ordered.Count == 1)
            {
                return ordered[0];
            }

            var outlineCenterX = (outline.MinX + outline.MaxX) / 2.0;
            var groupCenterX = ordered.Average(h => h.Center.X);
            return groupCenterX <= outlineCenterX ? ordered.First() : ordered.Last();
        }

        private HoleLeaderPoints BuildHoleLeaderPoints(IList<HoleFeature> group, HoleFeature representative, int groupIndex)
        {
            var radius = representative.Diameter / 2.0;
            var leaderOffset = Scale(_config.LeaderOffset + groupIndex * 3.0);
            var textGap = Scale(1.2);

            if (group.Count > 1)
            {
                var arrowDirection = new Vector3d(-1.0, -1.0, 0.0).GetNormal();
                var arrowPoint = new Point3d(
                    representative.Center.X + arrowDirection.X * radius,
                    representative.Center.Y + arrowDirection.Y * radius,
                    representative.Center.Z);
                var landingPoint = new Point3d(
                    representative.Center.X - leaderOffset,
                    representative.Center.Y - leaderOffset,
                    representative.Center.Z);
                var textPoint = new Point3d(
                    landingPoint.X - textGap,
                    landingPoint.Y,
                    landingPoint.Z);

                return new HoleLeaderPoints(arrowPoint, landingPoint, textPoint);
            }

            var singleArrowDirection = new Vector3d(-1.0, -1.0, 0.0).GetNormal();
            var singleArrowPoint = new Point3d(
                representative.Center.X + singleArrowDirection.X * radius,
                representative.Center.Y + singleArrowDirection.Y * radius,
                representative.Center.Z);
            var singleLandingPoint = new Point3d(
                representative.Center.X - leaderOffset * 0.85,
                representative.Center.Y - leaderOffset * 0.85,
                representative.Center.Z);
            var singleTextPoint = new Point3d(
                singleLandingPoint.X - textGap,
                singleLandingPoint.Y,
                singleLandingPoint.Z);

            return new HoleLeaderPoints(singleArrowPoint, singleLandingPoint, singleTextPoint);
        }

        private void AddRotatedDimension(
            double rotation,
            Point3d xLine1,
            Point3d xLine2,
            Point3d dimLinePoint,
            string overrideText,
            bool useSegmentedExtensionLines,
            bool useCustomTextPosition = false,
            Point3d customTextPosition = default(Point3d))
        {
            var dimension = new RotatedDimension(
                rotation,
                xLine1,
                xLine2,
                dimLinePoint,
                overrideText ?? string.Empty,
                _dimStyleId);
            dimension.SetDatabaseDefaults(_db);
            dimension.Layer = _annotationLayer;
            dimension.DimensionStyle = _dimStyleId;
            if (useSegmentedExtensionLines)
            {
                dimension.Dimse1 = true;
                dimension.Dimse2 = true;
            }
            Append(dimension);
            if (useCustomTextPosition)
            {
                dimension.UsingDefaultTextPosition = false;
                dimension.TextPosition = customTextPosition;
                if (_appendToDatabase)
                {
                    dimension.RecomputeDimensionBlock(true);
                }
            }
        }

        private Point3d GetLeftBoundaryPoint(OutlineFeature outline)
        {
            var candidate = outline.Vertices
                .Where(v => Math.Abs(v.X - outline.MinX) <= _config.GeometryTolerance)
                .OrderBy(v => v.Y)
                .FirstOrDefault();

            return ToPoint3d(candidate, outline.MinX, outline.MinY);
        }

        private Point3d GetRightBoundaryPoint(OutlineFeature outline)
        {
            var candidate = outline.Vertices
                .Where(v => Math.Abs(v.X - outline.MaxX) <= _config.GeometryTolerance)
                .OrderBy(v => v.Y)
                .FirstOrDefault();

            return ToPoint3d(candidate, outline.MaxX, outline.MinY);
        }

        private Point3d GetBottomBoundaryPoint(OutlineFeature outline)
        {
            var candidate = outline.Vertices
                .Where(v => Math.Abs(v.Y - outline.MinY) <= _config.GeometryTolerance)
                .OrderBy(v => v.X)
                .FirstOrDefault();

            return ToPoint3d(candidate, outline.MinX, outline.MinY);
        }

        private Point3d GetTopBoundaryPoint(OutlineFeature outline)
        {
            var candidate = outline.Vertices
                .Where(v => Math.Abs(v.Y - outline.MaxY) <= _config.GeometryTolerance)
                .OrderBy(v => v.X)
                .FirstOrDefault();

            return ToPoint3d(candidate, outline.MinX, outline.MaxY);
        }

        private static Point3d ToPoint3d(Point2d point, double fallbackX, double fallbackY)
        {
            if (point == default(Point2d))
            {
                return new Point3d(fallbackX, fallbackY, 0.0);
            }

            return new Point3d(point.X, point.Y, 0.0);
        }

        private void AddDiameterLeader(Point3d arrowPoint, Point3d landingPoint, Point3d textPoint, string text)
        {
            var mtext = new MText();
            mtext.SetDatabaseDefaults(_db);
            mtext.Contents = text;
            mtext.TextHeight = GetDimStyleTextHeight(_diameterCalloutDimStyleId);
            mtext.TextStyleId = GetDimStyleTextStyle(_diameterCalloutDimStyleId);
            mtext.Location = textPoint;
            mtext.Attachment = AttachmentPoint.MiddleLeft;
            mtext.Layer = _annotationLayer;
            Append(mtext);

            if (!_appendToDatabase)
            {
                var previewLine = new Line(arrowPoint, landingPoint);
                previewLine.SetDatabaseDefaults(_db);
                previewLine.Layer = _annotationLayer;
                Append(previewLine);
                return;
            }

            var leader = new Leader();
            leader.SetDatabaseDefaults(_db);
            leader.Layer = _annotationLayer;
            leader.DimensionStyle = _diameterCalloutDimStyleId;
            leader.AppendVertex(arrowPoint);
            leader.AppendVertex(landingPoint);
            Append(leader);
            leader.Annotation = mtext.ObjectId;
            leader.EvaluateLeader();
        }

        private ObjectId GetDimStyleTextStyle(ObjectId dimStyleId)
        {
            var record = _tr.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
            if (record != null && !record.Dimtxsty.IsNull)
            {
                return record.Dimtxsty;
            }

            return _db.Textstyle;
        }

        private string GetCurrentLayerName()
        {
            var currentLayer = _tr.GetObject(_db.Clayer, OpenMode.ForRead) as LayerTableRecord;
            return currentLayer != null ? currentLayer.Name : _annotationLayer;
        }

        private Autodesk.AutoCAD.Colors.Color GetCurrentEntityColor()
        {
            var colorText = Convert.ToString(Application.GetSystemVariable("CECOLOR"));
            if (string.IsNullOrWhiteSpace(colorText))
            {
                return _db.Cecolor;
            }

            colorText = colorText.Trim();
            if (string.Equals(colorText, "BYLAYER", StringComparison.OrdinalIgnoreCase))
            {
                return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
            }

            if (string.Equals(colorText, "BYBLOCK", StringComparison.OrdinalIgnoreCase))
            {
                return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByBlock, 0);
            }

            short colorIndex;
            if (short.TryParse(colorText, out colorIndex))
            {
                return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex);
            }

            switch (colorText.ToUpperInvariant())
            {
                case "RED":
                case "红":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1);
                case "YELLOW":
                case "黄":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 2);
                case "GREEN":
                case "绿":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3);
                case "CYAN":
                case "青":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 4);
                case "BLUE":
                case "蓝":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 5);
                case "MAGENTA":
                case "洋红":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 6);
                case "WHITE":
                case "白":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 7);
                default:
                    return _db.Cecolor;
            }
        }

        private Autodesk.AutoCAD.Colors.Color GetDimStyleTextColor(ObjectId dimStyleId)
        {
            var record = _tr.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
            if (record != null && record.Dimclrt != null)
            {
                return record.Dimclrt;
            }

            return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
        }

        private double GetDimStyleTextHeight(ObjectId dimStyleId)
        {
            var record = _tr.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
            if (record != null && record.Dimtxt > _config.GeometryTolerance)
            {
                return record.Dimtxt * _dimScale;
            }

            return Scale(_config.TextHeight);
        }

        private int GetDimStyleLinearPrecision(ObjectId dimStyleId)
        {
            var record = _tr.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
            if (record == null)
            {
                return 3;
            }

            return Math.Max(0, Math.Min(8, record.Dimdec));
        }

        private static string FormatNumberWithPrecision(double value, int precision)
        {
            var rounded = Math.Round(value, precision);
            var roundedInteger = Math.Round(rounded);
            if (Math.Abs(rounded - roundedInteger) <= 1e-9)
            {
                return roundedInteger.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            }

            var format = precision <= 0 ? "0" : "0." + new string('0', precision);
            return rounded.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        }

        private void Append(Entity entity)
        {
            if (_appendToDatabase)
            {
                _space.AppendEntity(entity);
                _tr.AddNewlyCreatedDBObject(entity, true);
                AnnotationMetadata.Mark(entity, _groupId);
            }
            else
            {
                _previewEntities.Add(entity);
            }
        }

        private double Scale(double value)
        {
            return value * _dimScale;
        }

        private sealed class CornerFeatureLeaderJig : DrawJig
        {
            private readonly Point3d _arrowPoint;
            private readonly string _text;
            private readonly double _textHeight;
            private readonly double _redrawTolerance;
            private readonly double _textWidth;
            private readonly string _promptMessage;
            private Point3d _textPoint;

            public CornerFeatureLeaderJig(Point3d arrowPoint, string text, double textHeight)
            {
                _arrowPoint = arrowPoint;
                _text = text ?? string.Empty;
                _textHeight = textHeight;
                _redrawTolerance = Math.Max(textHeight * 0.18, 1e-6);
                _textWidth = Math.Max(_text.Length, 2) * _textHeight * 0.65;
                _promptMessage = "\nMove corner callout " + _text + ", click to place, Enter to skip: ";
                _textPoint = arrowPoint + new Vector3d(textHeight * 6.0, textHeight * 4.0, 0.0);
            }

            public Point3d TextPoint
            {
                get { return _textPoint; }
            }

            public bool SkipRequested { get; private set; }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var options = new JigPromptPointOptions(_promptMessage);
                options.UserInputControls =
                    UserInputControls.Accept3dCoordinates
                    | UserInputControls.NullResponseAccepted;

                var result = prompts.AcquirePoint(options);
                if (result.Status == PromptStatus.None)
                {
                    SkipRequested = true;
                    return SamplerStatus.Cancel;
                }

                if (result.Status != PromptStatus.OK)
                {
                    return SamplerStatus.Cancel;
                }

                if (result.Value.DistanceTo(_textPoint) <= _redrawTolerance)
                {
                    return SamplerStatus.NoChange;
                }

                _textPoint = result.Value;
                return SamplerStatus.OK;
            }

            protected override bool WorldDraw(WorldDraw draw)
            {
                var direction = _textPoint - _arrowPoint;
                if (direction.Length <= 1e-9)
                {
                    direction = Vector3d.XAxis;
                }

                direction = direction.GetNormal();
                var normal = Vector3d.ZAxis;
                var textDirection = Vector3d.XAxis;
                var textStartsLeft = _textPoint.X < _arrowPoint.X;
                var textOrigin = textStartsLeft
                    ? _textPoint - textDirection * _textWidth
                    : _textPoint;

                draw.Geometry.WorldLine(_arrowPoint, _textPoint);
                draw.Geometry.Text(textOrigin, normal, textDirection, _textHeight, 1.0, 0.0, _text);
                return true;
            }

            private void DrawArrowHead(WorldDraw draw, Point3d arrowPoint, Vector3d direction)
            {
                var arrowLength = _textHeight * 1.2;
                var back = -direction;
                var perpendicular = new Vector3d(-direction.Y, direction.X, 0.0).GetNormal();
                var wing1 = arrowPoint + (back + perpendicular * 0.45).GetNormal() * arrowLength;
                var wing2 = arrowPoint + (back - perpendicular * 0.45).GetNormal() * arrowLength;
                draw.Geometry.WorldLine(arrowPoint, wing1);
                draw.Geometry.WorldLine(arrowPoint, wing2);
            }
        }

        private sealed class HoleLeaderPoints
        {
            public HoleLeaderPoints(Point3d arrowPoint, Point3d landingPoint, Point3d textPoint)
            {
                ArrowPoint = arrowPoint;
                LandingPoint = landingPoint;
                TextPoint = textPoint;
            }

            public Point3d ArrowPoint { get; }
            public Point3d LandingPoint { get; }
            public Point3d TextPoint { get; }
        }
    }
}
