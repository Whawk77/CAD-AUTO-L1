using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning
{
    public sealed class HoleCalloutPlanner
    {
        private sealed class HoleCalloutCluster
        {
            public HoleFeature2D BasePin { get; set; }
            public List<HoleFeature2D> Pins { get; private set; }
            public List<HoleFeature2D> Members { get; private set; }

            public HoleCalloutCluster()
            {
                Pins = new List<HoleFeature2D>();
                Members = new List<HoleFeature2D>();
            }
        }

        private readonly DimensionRuleConfig _config;

        public HoleCalloutPlanner(DimensionRuleConfig config)
        {
            _config = config ?? DimensionRuleConfig.CreateDefault();
        }

        public IList<HoleCalloutPlan> CreatePlans(IEnumerable<HoleFeature2D> holes, HoleFeature2D datumPin)
        {
            var holeList = holes == null
                ? new List<HoleFeature2D>()
                : holes.Where(h => h != null && !h.IsSlotPoint).ToList();
            var pinHoles = holeList.Where(h => h.IsPinHole).ToList();
            if (pinHoles.Count == 0)
            {
                return BuildPlansBySpatialRows(holeList);
            }

            var clusters = BuildPinCalloutClusters(pinHoles, datumPin);
            AssignNonPinHolesToCalloutClusters(holeList, clusters);

            var result = new List<HoleCalloutPlan>();
            var clusterIndex = 1;
            foreach (var cluster in clusters)
            {
                foreach (var group in GroupCalloutClusterMembers(cluster.Members))
                {
                    result.Add(CreatePlan(group, "PG" + clusterIndex.ToString(CultureInfo.InvariantCulture)));
                }

                clusterIndex++;
            }

            return result;
        }

        private IList<HoleCalloutPlan> BuildPlansBySpatialRows(IList<HoleFeature2D> holes)
        {
            var result = new List<HoleCalloutPlan>();
            var rows = new List<List<HoleFeature2D>>();
            foreach (var hole in holes.OrderBy(h => h.Center.Y).ThenBy(h => h.Center.X))
            {
                var row = rows.FirstOrDefault(r => Math.Abs(r.Average(h => h.Center.Y) - hole.Center.Y) <= _config.GeometryTolerance);
                if (row == null)
                {
                    row = new List<HoleFeature2D>();
                    rows.Add(row);
                }

                row.Add(hole);
            }

            foreach (var row in rows.OrderBy(r => r.Average(h => h.Center.Y)))
            {
                foreach (var group in GroupCalloutClusterMembers(row))
                {
                    result.Add(CreatePlan(group, string.Empty));
                }
            }

            return result;
        }

        private List<HoleCalloutCluster> BuildPinCalloutClusters(IList<HoleFeature2D> pinHoles, HoleFeature2D datumPin)
        {
            var remaining = pinHoles.OrderBy(h => h.Center.X).ThenBy(h => h.Center.Y).ToList();
            var clusters = new List<HoleCalloutCluster>();
            var seed = datumPin != null && datumPin.IsPinHole
                ? remaining.FirstOrDefault(h => IsSameHoleForCallout(h, datumPin)) ?? datumPin
                : remaining.First();

            while (remaining.Count > 0)
            {
                var reference = clusters.Count == 0 ? null : clusters[clusters.Count - 1].BasePin;
                if (clusters.Count > 0)
                {
                    seed = remaining
                        .OrderBy(h => DistanceSquared(h.Center, reference.Center))
                        .ThenBy(h => h.Center.X)
                        .ThenBy(h => h.Center.Y)
                        .First();
                }

                var cluster = CreatePinCalloutCluster(seed, remaining, clusters.Count == 0 ? seed : null, reference);
                clusters.Add(cluster);
                foreach (var pin in cluster.Pins.ToList())
                {
                    for (int i = remaining.Count - 1; i >= 0; i--)
                    {
                        if (IsSameHoleForCallout(remaining[i], pin))
                        {
                            remaining.RemoveAt(i);
                        }
                    }
                }

                if (remaining.Count > 0 && !remaining.Any(h => IsSameHoleForCallout(h, seed)))
                {
                    seed = remaining[0];
                }
            }

            return clusters;
        }

        private HoleCalloutCluster CreatePinCalloutCluster(
            HoleFeature2D seed,
            IList<HoleFeature2D> candidates,
            HoleFeature2D forcedBasePin,
            HoleFeature2D referenceBasePin)
        {
            var pins = new List<HoleFeature2D> { seed };
            var pairedPin = candidates
                .Where(h => !IsSameHoleForCallout(h, seed) && Math.Abs(h.Diameter - seed.Diameter) <= _config.GeometryTolerance)
                .OrderBy(h => DistanceSquared(h.Center, seed.Center))
                .ThenBy(h => h.Center.X)
                .ThenBy(h => h.Center.Y)
                .FirstOrDefault();
            if (pairedPin != null)
            {
                pins.Add(pairedPin);
            }

            var basePin = ChooseCalloutBasePin(pins, forcedBasePin, referenceBasePin, seed);
            var cluster = new HoleCalloutCluster { BasePin = basePin };
            foreach (var pin in pins.OrderBy(h => DistanceSquared(h.Center, basePin.Center)))
            {
                cluster.Pins.Add(pin);
                cluster.Members.Add(pin);
            }

            return cluster;
        }

        private HoleFeature2D ChooseCalloutBasePin(
            IList<HoleFeature2D> pins,
            HoleFeature2D forcedBasePin,
            HoleFeature2D referenceBasePin,
            HoleFeature2D fallbackPin)
        {
            if (forcedBasePin != null)
            {
                return pins.FirstOrDefault(h => IsSameHoleForCallout(h, forcedBasePin)) ?? forcedBasePin;
            }

            if (referenceBasePin != null)
            {
                return pins
                    .OrderBy(h => DistanceSquared(h.Center, referenceBasePin.Center))
                    .ThenBy(h => h.Center.X)
                    .ThenBy(h => h.Center.Y)
                    .First();
            }

            return pins.OrderBy(h => h.Center.X).ThenBy(h => h.Center.Y).FirstOrDefault() ?? fallbackPin;
        }

        private void AssignNonPinHolesToCalloutClusters(IList<HoleFeature2D> holes, IList<HoleCalloutCluster> clusters)
        {
            foreach (var hole in holes.Where(h => !h.IsPinHole && !h.IsSlotPoint))
            {
                var cluster = clusters
                    .OrderBy(g => DistanceToCalloutCluster(hole, g))
                    .ThenBy(g => DistanceSquared(hole.Center, g.BasePin.Center))
                    .FirstOrDefault();
                if (cluster != null)
                {
                    cluster.Members.Add(hole);
                }
            }
        }

        private double DistanceToCalloutCluster(HoleFeature2D hole, HoleCalloutCluster cluster)
        {
            if (cluster == null || cluster.Pins.Count == 0)
            {
                return double.MaxValue;
            }

            if (cluster.Pins.Count == 1)
            {
                return Math.Sqrt(DistanceSquared(hole.Center, cluster.Pins[0].Center));
            }

            return cluster.Pins.Take(2).Sum(pin => Math.Sqrt(DistanceSquared(hole.Center, pin.Center)));
        }

        private IEnumerable<IList<HoleFeature2D>> GroupCalloutClusterMembers(IEnumerable<HoleFeature2D> members)
        {
            foreach (var typeGroup in members
                .Where(h => h != null && !h.IsSlotPoint)
                .GroupBy(GetHoleCalloutTypeRank)
                .OrderBy(g => g.Key))
            {
                var groups = new List<List<HoleFeature2D>>();
                foreach (var hole in typeGroup.OrderBy(h => h.Diameter).ThenBy(h => h.Center.Y).ThenBy(h => h.Center.X))
                {
                    var group = groups.FirstOrDefault(g =>
                        Math.Abs(g.Average(h => h.Diameter) - hole.Diameter) <= _config.GeometryTolerance
                        && g[0].Kind == hole.Kind
                        && string.Equals(g[0].FitTolerance ?? string.Empty, hole.FitTolerance ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(g[0].ThreadCallout ?? string.Empty, hole.ThreadCallout ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                    if (group == null)
                    {
                        group = new List<HoleFeature2D>();
                        groups.Add(group);
                    }

                    group.Add(hole);
                }

                foreach (var group in groups)
                {
                    yield return group;
                }
            }
        }

        private HoleCalloutPlan CreatePlan(IList<HoleFeature2D> group, string debugOwner)
        {
            var ordered = group.OrderBy(h => h.Center.Y).ThenBy(h => h.Center.X).ToList();
            var representative = ordered[0];
            var plan = new HoleCalloutPlan
            {
                Kind = GetHoleCalloutKind(representative),
                AnchorPoint = representative.Center,
                DebugOwner = debugOwner ?? string.Empty,
                Text = representative.IsThreadHole
                    ? _config.FormatThreadCallout(representative.Diameter, ordered.Count, representative.ThreadCallout)
                    : _config.FormatHoleCallout(representative.Diameter, ordered.Count, representative.FitTolerance)
            };

            foreach (var hole in ordered)
            {
                plan.Holes.Add(hole);
            }

            return plan;
        }

        private static HoleCalloutKind GetHoleCalloutKind(HoleFeature2D hole)
        {
            if (hole.IsPinHole)
            {
                return HoleCalloutKind.Pin;
            }

            if (hole.IsThreadHole)
            {
                return HoleCalloutKind.Thread;
            }

            return HoleCalloutKind.Normal;
        }

        private static int GetHoleCalloutTypeRank(HoleFeature2D hole)
        {
            if (hole.IsPinHole)
            {
                return 0;
            }

            if (hole.IsThreadHole)
            {
                return 1;
            }

            return 2;
        }

        private bool IsSameHoleForCallout(HoleFeature2D a, HoleFeature2D b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return DistanceSquared(a.Center, b.Center) <= _config.GeometryTolerance * _config.GeometryTolerance
                && Math.Abs(a.Diameter - b.Diameter) <= _config.GeometryTolerance;
        }

        private static double DistanceSquared(CadAuto.Core.Geometry.Point2D a, CadAuto.Core.Geometry.Point2D b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}
