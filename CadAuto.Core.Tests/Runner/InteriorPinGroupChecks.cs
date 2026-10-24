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
        private static List<HoleFeature2D> CreateInteriorPinGroupHoles()
        {
            // 测试.dwg, run 20261024103113940: A/D/C/F and counterbores confirmed by the user.
            return new List<HoleFeature2D> {
                new HoleFeature2D { Center = new Point2D(45.000001221866114, 30.000001967708158), Diameter = 5, Kind = HoleKind2D.Pin, SourceKey = "681EF1" },
                new HoleFeature2D { Center = new Point2D(111.00000122186611, 30.000004852654456), Diameter = 5, Kind = HoleKind2D.Pin, SourceKey = "681EF2" },
                new HoleFeature2D { Center = new Point2D(13.000002096086973, 10.000000568941687), Diameter = 11, Kind = HoleKind2D.Normal, SourceKey = "681EF3" },
                new HoleFeature2D { Center = new Point2D(13.000000347616151, 50.00000056894169), Diameter = 11, Kind = HoleKind2D.Normal, SourceKey = "681EF4" },
                new HoleFeature2D { Center = new Point2D(126.00000082826591, 39.00000550822551), Diameter = 8, Kind = HoleKind2D.Pin, SourceKey = "681EF5" },
                new HoleFeature2D { Center = new Point2D(125.99999951693462, 69.00000550822551), Diameter = 8, Kind = HoleKind2D.Pin, SourceKey = "681EF6" },
                new HoleFeature2D { Center = new Point2D(143.00000034761615, 50.00000625142093), Diameter = 12, Kind = HoleKind2D.Normal, SourceKey = "681EFA" },
                new HoleFeature2D { Center = new Point2D(83.00000034761615, 50.00000362873652), Diameter = 12, Kind = HoleKind2D.Normal, SourceKey = "681EFB" },
                new HoleFeature2D { Center = new Point2D(143.00000209608697, 10.000006251420928), Diameter = 12, Kind = HoleKind2D.Normal, SourceKey = "681EFC" },
                new HoleFeature2D { Center = new Point2D(83.00000209608697, 10.000003628736522), Diameter = 12, Kind = HoleKind2D.Normal, SourceKey = "681EFD" },
                new HoleFeature2D { Center = new Point2D(13.000002096086973, 10.000000175537934), Diameter = 18, Kind = HoleKind2D.Normal, SourceKey = "681F04" },
                new HoleFeature2D { Center = new Point2D(13.000000347616151, 50.000000175537934), Diameter = 18, Kind = HoleKind2D.Normal, SourceKey = "681F05" },
                new HoleFeature2D { Center = new Point2D(143.00000209608697, 10.000005858017175), Diameter = 18, Kind = HoleKind2D.Normal, SourceKey = "681F06" },
                new HoleFeature2D { Center = new Point2D(143.00000034761615, 50.000005858017175), Diameter = 18, Kind = HoleKind2D.Normal, SourceKey = "681F0F" },
                new HoleFeature2D { Center = new Point2D(143.00000209608697, 10.000006120286344), Diameter = 11, Kind = HoleKind2D.Normal, SourceKey = "681F10" },
                new HoleFeature2D { Center = new Point2D(143.00000034761615, 50.00000612028634), Diameter = 11, Kind = HoleKind2D.Normal, SourceKey = "681F11" },
                new HoleFeature2D { Center = new Point2D(83.00000034761615, 50.00000362873652), Diameter = 11, Kind = HoleKind2D.Normal, SourceKey = "681F15" },
                new HoleFeature2D { Center = new Point2D(83.00000209608697, 10.000003628736522), Diameter = 11, Kind = HoleKind2D.Normal, SourceKey = "681F16" },
                new HoleFeature2D { Center = new Point2D(78.00000165094389, 26.499998121113094), Diameter = 6, Kind = HoleKind2D.Thread, SourceKey = "681EB6", ThreadCallout = "M6" },
                new HoleFeature2D { Center = new Point2D(125.99999402154936, 83.9999981211131), Diameter = 8, Kind = HoleKind2D.Thread, SourceKey = "681EB9", ThreadCallout = "M8" },
                new HoleFeature2D { Center = new Point2D(125.99999402154936, 53.999998121113094), Diameter = 8, Kind = HoleKind2D.Thread, SourceKey = "681EBC", ThreadCallout = "M8" },
                new HoleFeature2D { Center = new Point2D(40.33677075494779, 57.999998121113094), Diameter = 6, Kind = HoleKind2D.Thread, SourceKey = "681EBF", ThreadCallout = "M6" },
                new HoleFeature2D { Center = new Point2D(40.33677075494779, 37.999998121113094), Diameter = 6, Kind = HoleKind2D.Thread, SourceKey = "681EC2", ThreadCallout = "M6" },
            };
        }

        private static bool IsConfirmedInteriorFunctionalHole(HoleFeature2D hole)
        {
            return !hole.IsPinHole && !hole.IsThreadHole
                && (Math.Abs(hole.Center.X - 13) < 0.001 || Math.Abs(hole.Center.X - 143) < 0.001);
        }

        private static void InteriorPinGroupKeepsCounterboresWithOwningPair()
        {
            foreach (string datumKey in new[] { "681EF1", "681EF2", "681EF5", "681EF6" })
            foreach (double angle in new[] { 0.0, Math.PI / 2, 0.37 })
            foreach (bool reverse in new[] { false, true })
            {
                var holes = CreateInteriorPinGroupHoles();
                var expected = holes.Where(IsConfirmedInteriorFunctionalHole).ToList();
                var datum = holes.Single(h => h.SourceKey == datumKey);
                foreach (var h in holes)
                {
                    var rotated = RotatePoint(h.Center, angle);
                    h.Center = new Point2D(rotated.X + 101745.41572174, rotated.Y + 8786.12020295);
                }
                if (reverse) holes.Reverse();
                var config = DimensionRuleConfig.CreateDefault();
                var groups = new DimensionPlanner(config).BuildPinGroupPlan(holes, datum);
                var inner = groups.Single(g => g.Pins.All(h => Math.Abs(h.Diameter - 5) < 0.001));
                var other = groups.Single(g => g.Pins.All(h => Math.Abs(h.Diameter - 8) < 0.001));
                Assert(expected.Count == 10 && expected.All(inner.MemberHoles.Contains),
                    "all ten A/D/C/F concentric circles must follow the interior pin pair despite nearer PG1 pins");
                Assert(!expected.Any(other.MemberHoles.Contains), "confirmed functional holes must have one owner");
                Assert(other.MemberHoles.Count(h => h.IsThreadHole && h.ThreadCallout == "M8") == 2,
                    "the existing two M8 holes must stay with the other pair");
                var callouts = new HoleCalloutPlanner(config).CreatePlans(holes, datum);
                foreach (var h in expected)
                    Assert(callouts.Single(c => c.Holes.Contains(h)).DebugOwner == "PG" + inner.GroupIndex,
                        "callouts and positioning must share counterbore ownership");
            }
        }

        private static void InteriorPinGroupEmitsFourFunctionalLocations()
        {
            foreach (double angle in new[] { 0.0, Math.PI / 2, Math.PI, 3 * Math.PI / 2, 0.37 })
            {
                var holes = CreateInteriorPinGroupHoles();
                foreach (var h in holes) h.Center = RotatePoint(h.Center, angle);
                var outline = RotateOutline(CreateRectangle(155, 94), angle);
                var datum = Datum2D.FromOutline(outline);
                datum.DatumHole = holes.Single(h => h.SourceKey == "681EF5");
                var plan = new DimensionPlanner(DimensionRuleConfig.CreateDefault()).CreateDimensionPlan(outline, datum, holes);
                var functional = plan.Diagnostics.DimensionCandidates.Where(c => c.DebugRole == "FunctionalHole" && c.SourceFeatureId == "PG2").ToList();
                Assert(functional.Count == 8, "four physical counterbore locations must emit eight X/Y candidates, not zero or one per circle");
                Assert(plan.Diagnostics.DimensionCandidates.Count(c => c.DebugRole == "FunctionalHole" && c.SourceFeatureId == "PG1") == 2,
                    "PG1 must retain the two existing M8 location candidates");
                Assert(functional.All(c => c.Value > 0), "functional positions must retain real nonzero spans");
            }
        }

        private static void InteriorPinGroupRejectsIncompleteAndUncenteredPatterns()
        {
            for (int variant = 0; variant < 4; variant++)
            {
                var holes = new List<HoleFeature2D> {
                    CreateHole(45, 30, 5, HoleKind2D.Pin), CreateHole(111, 30, 5, HoleKind2D.Pin),
                    CreateHole(13, 10, 11, HoleKind2D.Normal), CreateHole(13, 50, 11, HoleKind2D.Normal),
                    CreateHole(143, 10, 11, HoleKind2D.Normal), CreateHole(143, 50, 11, HoleKind2D.Normal)
                };
                if (variant == 0) holes.RemoveAt(5);
                if (variant == 1) foreach (var pin in holes.Take(2)) pin.Center = new Point2D(pin.Center.X + 2, pin.Center.Y);
                if (variant == 2) { holes[0].Center = new Point2D(10, 30); holes[1].Center = new Point2D(146, 30); }
                if (variant == 3) holes[5].Diameter = 12;
                var outline = CreateRectangle(160, 94);
                var datum = Datum2D.FromOutline(outline);
                datum.DatumHole = holes[0];
                var plan = new DimensionPlanner(DimensionRuleConfig.CreateDefault()).CreateDimensionPlan(outline, datum, holes);
                Assert(!plan.Diagnostics.DimensionCandidates.Any(c => c.DebugRole == "FunctionalHole"),
                    "incomplete, off-center, exterior-pin or incompatible-spec patterns must remain loose; variant=" + variant);
            }
        }
    }
}
