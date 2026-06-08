using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
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
                LooseChainId = chainId,
                DebugOwner = chainId == 0 ? string.Empty : "L" + chainId.ToString(CultureInfo.InvariantCulture),
                DebugRole = "LooseHole"
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
    }
}
