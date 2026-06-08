using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
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
                    AddHorizontalDimToSide(
                        reference.Center,
                        hole.Center,
                        string.Empty,
                        DimensionType.HoleLocation,
                        pinGroup.HorizontalSide,
                        preferLocalBoundary: true,
                        debugOwner: GetPinGroupDebugOwner(pinGroup),
                        debugRole: "FunctionalHole");
                    AddVerticalDimToSide(
                        reference.Center,
                        hole.Center,
                        string.Empty,
                        DimensionType.HoleLocation,
                        pinGroup.VerticalSide,
                        preferLocalBoundary: true,
                        debugOwner: GetPinGroupDebugOwner(pinGroup),
                        debugRole: "FunctionalHole");
                }
            }
        }
    }
}
