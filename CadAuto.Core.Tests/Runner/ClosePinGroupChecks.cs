using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        private static List<HoleFeature2D> CreateClosePinGroupHoles(HoleKind2D kind)
        {
            // 易错孔组.dwg: 889/887 and 88F/88E; grouping confirmed by the user.
            var holes = new List<HoleFeature2D> {
                CreateHole(25, 55, 8, HoleKind2D.Pin), CreateHole(25, 25, 8, HoleKind2D.Pin),
                CreateHole(10, 46, 8, HoleKind2D.Pin), CreateHole(40, 46, 8, HoleKind2D.Pin),
                CreateHole(25, 40, 8, kind), CreateHole(25, 10, 8, kind),
                CreateHole(10, 31, 8, kind), CreateHole(10, 61, 8, kind),
                CreateHole(40, 31, 8, kind), CreateHole(40, 61, 8, kind)
            };
            foreach (var hole in holes.Where(h => h.IsThreadHole)) hole.ThreadCallout = "M8";
            return holes;
        }

        private static void ClosePinGroupsUseCompletePatternsBeforeDistance()
        {
            var config = DimensionRuleConfig.CreateDefault();
            foreach (var kind in new[] { HoleKind2D.Thread, HoleKind2D.Normal })
            foreach (int datumIndex in new[] { 0, 1, 2, 3 })
            foreach (bool reverse in new[] { false, true })
            {
                var holes = CreateClosePinGroupHoles(kind);
                var expectedVertical = holes.Take(2).Concat(holes.Skip(4).Take(2)).ToList();
                var expectedHorizontal = holes.Skip(2).Take(2).Concat(holes.Skip(6)).ToList();
                var datumPin = holes[datumIndex];
                if (reverse) holes.Reverse();
                var groups = new DimensionPlanner(config).BuildPinGroupPlan(holes, datumPin);
                Assert(groups.Count == 2, "close groups must retain two pin pairs");
                Assert(groups.Any(g => g.MemberHoles.Count == 4 && expectedVertical.All(g.MemberHoles.Contains)),
                    "vertical pair must own its two functional holes despite closer diagonal pins");
                Assert(groups.Any(g => g.MemberHoles.Count == 6 && expectedHorizontal.All(g.MemberHoles.Contains)),
                    "horizontal pair must own all four functional holes despite nearest-pair competition");
                var callouts = new HoleCalloutPlanner(config).CreatePlans(holes, datumPin);
                foreach (var group in groups)
                {
                    var members = callouts.Where(c => c.DebugOwner == "PG" + group.GroupIndex).SelectMany(c => c.Holes).ToList();
                    Assert(members.Count == group.MemberHoles.Count && group.MemberHoles.All(members.Contains),
                        "diameter/thread callouts and positioning must use identical membership");
                }
            }
        }

        private static void ClosePinGroupsKeepFunctionalLocationsAfterRotation()
        {
            var config = DimensionRuleConfig.CreateDefault();
            foreach (double angle in new[] { 0.0, Math.PI / 2, Math.PI, 3 * Math.PI / 2, 0.37 })
            {
                var holes = CreateClosePinGroupHoles(HoleKind2D.Thread);
                foreach (var hole in holes) hole.Center = RotatePoint(hole.Center, angle);
                var outline = RotateOutline(CreateRectangle(50, 71), angle);
                var datum = Datum2D.FromOutline(outline);
                datum.DatumHole = holes[0];
                var plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, holes);
                Assert(plan.PinGroups.Count == 2 && plan.PinGroups.All(g => g.Pins.Count == 2), "rotation must preserve two complete pairs");
                Assert(plan.Diagnostics.DimensionCandidates.Where(c => c.DebugRole == "PinDistance").All(c => Math.Abs(c.Value - 30) < 0.001),
                    "pin dimensions must measure the real 30 spans, not diagonal-pair projections 15/9/21");
                Assert(!plan.Diagnostics.DimensionCandidates.Any(c => c.DebugRole == "LooseHole"), "all six confirmed functional holes must leave the loose path");
                Assert(plan.Diagnostics.DimensionCandidates.Any(c => c.DebugRole == "FunctionalHole" && c.SourceFeatureId == "PG1")
                    && plan.Diagnostics.DimensionCandidates.Any(c => c.DebugRole == "FunctionalHole" && c.SourceFeatureId == "PG2"),
                    "both groups must emit their own functional locations");
            }
        }

        private static void ClosePinGroupsKeepConcentricDiametersTogether()
        {
            var holes = CreateClosePinGroupHoles(HoleKind2D.Normal);
            holes.AddRange(holes.Skip(4).ToList().Select(h => CreateHole(h.Center.X, h.Center.Y, 12, HoleKind2D.Normal)));
            var groups = new DimensionPlanner(DimensionRuleConfig.CreateDefault()).BuildPinGroupPlan(holes, holes[0]);
            Assert(groups.Count == 2 && groups[0].MemberHoles.Count == 6 && groups[1].MemberHoles.Count == 10,
                "concentric inner/outer circles must follow one owning pattern without counting as extra locations");
            var callouts = new HoleCalloutPlanner(DimensionRuleConfig.CreateDefault()).CreatePlans(holes, holes[0]);
            Assert(callouts.Where(c => c.Kind == HoleCalloutKind.Normal).Count() == 4,
                "both diameters must remain separate in each pin group");
        }

        private static void ClosePinGroupsWithoutPatternKeepDistanceFallback()
        {
            var holes = new List<HoleFeature2D> {
                CreateHole(10, 10, 8, HoleKind2D.Pin), CreateHole(25, 19, 8, HoleKind2D.Pin),
                CreateHole(10, 40, 8, HoleKind2D.Pin), CreateHole(70, 70, 8, HoleKind2D.Pin)
            };
            var groups = new DimensionPlanner(DimensionRuleConfig.CreateDefault()).BuildPinGroupPlan(holes, holes[0]);
            Assert(groups[0].Pins.Contains(holes[1]), "missing pattern evidence must preserve legacy diagonal nearest pairing");
        }
    }
}
