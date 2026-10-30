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

        private static void CollinearPinGroupsKeepBetweenPinHolesWithOwningPair()
        {
            // Run 20261026085621430: normalized centers; D34 incorrectly joined PG3 to PG2's middle hole.
            foreach (double angle in new[] { 0.0, Math.PI / 2, Math.PI, 3 * Math.PI / 2, .37 })
            foreach (bool mirror in new[] { false, true })
            foreach (bool reverse in new[] { false, true })
            {
                var holes = new List<HoleFeature2D> {
                    CreateHole(220, 10, 8, HoleKind2D.Pin), CreateHole(190, 10, 8, HoleKind2D.Pin),
                    CreateHole(165, 10, 8, HoleKind2D.Pin), CreateHole(135, 10, 8, HoleKind2D.Pin),
                    CreateHole(105, 10, 8, HoleKind2D.Pin), CreateHole(75, 10, 8, HoleKind2D.Pin),
                    CreateHole(205, 10, 9, HoleKind2D.Normal), CreateHole(235, 10, 9, HoleKind2D.Normal),
                    CreateHole(150, 10, 9, HoleKind2D.Normal), CreateHole(90, 10, 9, HoleKind2D.Normal),
                    CreateHole(30, 10, 6, HoleKind2D.Thread)
                };
                for (int i = 0; i < holes.Count; i++) holes[i].SourceKey = "bar-report-position-" + i;
                holes[10].ThreadCallout = "M6";
                var expectedPins = new[] { holes.Take(2).ToList(), holes.Skip(2).Take(2).ToList(), holes.Skip(4).Take(2).ToList() };
                var expectedHoles = new[] { holes.Skip(6).Take(2).ToList(), holes.Skip(8).Take(1).ToList(), holes.Skip(9).Take(1).ToList() };
                var datumPin = holes[0];
                var frame = new CoordinateFrame2D(new Point2D(102631.96596602, 8535.15431285), RotatePoint(new Point2D(1, 0), angle));
                foreach (var hole in holes)
                    hole.Center = frame.ToWorld(new Point2D(mirror ? 245 - hole.Center.X : hole.Center.X, hole.Center.Y));
                if (reverse) holes.Reverse();
                var config = DimensionRuleConfig.CreateDefault();
                var planner = new DimensionPlanner(config);
                var groups = planner.BuildPinGroupPlan(holes, datumPin);
                var callouts = new HoleCalloutPlanner(config).CreatePlans(holes, datumPin);
                Assert(groups.Count == 3, "the bar must retain its three real pin pairs");
                for (int i = 0; i < expectedPins.Length; i++)
                {
                    var group = groups.Single(g => expectedPins[i].All(g.Pins.Contains));
                    Assert(expectedHoles[i].All(group.MemberHoles.Contains),
                        "a between-pin hole must belong to its containing pair before a distant grid or loose-chain reference");
                    Assert(expectedHoles[i].All(h => callouts.Any(c => c.DebugOwner == "PG" + group.GroupIndex && c.Holes.Contains(h))),
                        "diameter callouts must retain the same pin-pair ownership as positioning");
                }
                var corners = new[] { new Point2D(0, 0), new Point2D(245, 0), new Point2D(245, 20), new Point2D(0, 20) }
                    .Select(frame.ToWorld).ToArray();
                var outline = new OutlineFeature2D { MinX = corners.Min(p => p.X), MaxX = corners.Max(p => p.X),
                    MinY = corners.Min(p => p.Y), MaxY = corners.Max(p => p.Y) };
                for (int i = 0; i < corners.Length; i++) AddSegment(outline, corners[i], corners[(i + 1) % corners.Length], "bar-boundary-" + i);
                var datum = Datum2D.FromOutline(outline);
                datum.DatumHole = datumPin;
                var plan = planner.CreateDimensionPlan(outline, datum, holes, new SlotFeature2D[0], frame);
                for (int i = 0; i < expectedPins.Length; i++)
                {
                    var group = plan.PinGroups.Single(g => g.Pins.Any(p => p.SourceKey == expectedPins[i][0].SourceKey));
                    foreach (var expected in expectedHoles[i])
                    {
                        var target = group.MemberHoles.Single(h => h.SourceKey == expected.SourceKey);
                        var locations = plan.Dimensions.Where(d => d.Kind == DimensionKind.HoleLocation
                            && IsNear(d.SecondPoint, target.Center, config)).ToList();
                        Assert(locations.Count == 1 && locations[0].DebugRole == "FunctionalHole"
                            && locations[0].DebugOwner == "PG" + group.GroupIndex
                            && IsNear(locations[0].FirstPoint, group.BasePin.Center, config)
                            && Math.Abs(GetSpan(locations[0]) - 15) < .001,
                            "each normal hole must keep its own 15 location from its owning group's base pin, without a cross-group 45");
                    }
                }
                Assert(plan.Dimensions.Count(d => d.Kind == DimensionKind.PinDistance && Math.Abs(GetSpan(d) - 30) < .001) == 3,
                    "all three legitimate equal-valued pin spacings must survive");
                Assert(plan.Dimensions.Any(d => d.DebugRole == "LooseHole"), "the isolated thread must remain loose");
            }
        }
    }
}
