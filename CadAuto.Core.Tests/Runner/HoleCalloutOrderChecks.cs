using System;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        private static void HoleCalloutsFinishThroughHolesBeforeNextPinGroup()
        {
            foreach (double angle in new[] { 0.0, Math.PI / 2, Math.PI, 3 * Math.PI / 2, .37 })
            foreach (bool mirror in new[] { false, true })
            foreach (int datumIndex in new[] { 0, 2 })
            foreach (bool reverse in new[] { false, true })
            {
                var config = DimensionRuleConfig.CreateDefault();
                var holes = CreateClosePinGroupHoles(HoleKind2D.Normal);
                var pins = new[] { holes.Take(2).ToList(), holes.Skip(2).Take(2).ToList() };
                var through = new[] { holes.Skip(4).Take(2).ToList(), holes.Skip(6).ToList() };
                var datum = holes[datumIndex];
                for (int i = 0; i < holes.Count; i++) holes[i].SourceKey = "callout-order-" + i;
                foreach (var hole in holes)
                {
                    hole.Center = RotatePoint(new Point2D(mirror ? -hole.Center.X : hole.Center.X, hole.Center.Y), angle);
                    hole.Center = new Point2D(hole.Center.X + 103238, hole.Center.Y + 8519);
                    if (hole.IsPinHole) hole.FitTolerance = config.PinHoleFitToleranceText;
                }
                if (reverse) holes.Reverse();
                var frame = new CoordinateFrame2D(new Point2D(103238, 8519), RotatePoint(new Point2D(1, 0), angle));
                var positionPlan = new DimensionPlan { CoordinateFrame = frame };
                foreach (var group in new DimensionPlanner(config).BuildPinGroupPlan(
                    CoordinateFrameModelTransform.ToLocal(holes, frame), CoordinateFrameModelTransform.ToLocal(datum, frame)))
                    positionPlan.PinGroups.Add(group);
                var plans = new HoleCalloutPlanner(config).CreatePlans(holes, datum, positionPlan);
                Assert(plans.Select(p => p.DebugOwner + ":" + p.Kind).SequenceEqual(
                    new[] { "PG1:Pin", "PG1:Normal", "PG2:Pin", "PG2:Normal" }),
                    "interactive order must finish PG1 pins and through holes before starting PG2");
                for (int group = 0; group < 2; group++)
                {
                    int owner = datumIndex == 0 ? group : 1 - group;
                    Assert(plans[2 * group].Holes.Count == 2 && pins[owner].All(plans[2 * group].Holes.Contains),
                        "the pin prompt must retain the selected datum group's real pins");
                    var normal = plans[2 * group + 1];
                    Assert(normal.Holes.Count == through[owner].Count && through[owner].All(normal.Holes.Contains)
                        && normal.Text == config.FormatHoleCallout(8, through[owner].Count, string.Empty),
                        "equal diameters in different groups must keep separate counts and ownership");
                }
                Assert(plans.SelectMany(p => p.Holes).All(holes.Contains)
                    && plans.All(p => p.Holes.Any(h => h.Center.Equals(p.AnchorPoint))),
                    "interactive anchors must use original WCS holes rather than transformed planning copies");
            }
        }

        private static void HoleCalloutsKeepCounterboresInsideCurrentGroup()
        {
            foreach (int datumIndex in new[] { 0, 2 })
            foreach (bool reverse in new[] { false, true })
            {
                var config = DimensionRuleConfig.CreateDefault();
                var holes = CreateClosePinGroupHoles(HoleKind2D.Normal);
                var inner = new[] { holes.Skip(4).Take(2).ToList(), holes.Skip(6).ToList() };
                var outer = inner.Select(group => group.Select(h => CreateHole(h.Center.X, h.Center.Y, 12, HoleKind2D.Normal)).ToList()).ToArray();
                var datum = holes[datumIndex];
                holes.AddRange(outer.SelectMany(group => group));
                if (reverse) holes.Reverse();
                var datumDefinition = Datum2D.FromOutline(CreateRectangle(50, 71));
                datumDefinition.DatumHole = datum;
                var positionPlan = new DimensionPlanner(config).CreateDimensionPlan(CreateRectangle(50, 71), datumDefinition, holes);
                var plans = new HoleCalloutPlanner(config).CreatePlans(holes, datum, positionPlan);
                Assert(plans.Select(p => p.DebugOwner + ":" + p.Kind).SequenceEqual(new[] {
                    "PG1:Pin", "PG1:Normal", "PG1:Normal", "PG2:Pin", "PG2:Normal", "PG2:Normal" }),
                    "both counterbore diameters must finish before the next group's pin prompt");
                for (int group = 0; group < 2; group++)
                {
                    int owner = datumIndex == 0 ? group : 1 - group;
                    var current = plans.Skip(3 * group + 1).Take(2).ToList();
                    Assert(current[0].Holes.Count == inner[owner].Count && inner[owner].All(current[0].Holes.Contains)
                        && current[1].Holes.Count == outer[owner].Count && outer[owner].All(current[1].Holes.Contains),
                        "concentric components must retain both diameters within their actual owner group");
                }
                Assert(plans.Sum(p => p.Holes.Count) == holes.Count,
                    "ordering must retain every pin and counterbore component exactly once");
            }
        }

        private static void HoleCalloutsKeepThreadVariantsInsideCurrentGroup()
        {
            foreach (string datumKey in new[] { "681EF1", "681EF5" })
            foreach (bool reverse in new[] { false, true })
            {
                var config = DimensionRuleConfig.CreateDefault();
                var holes = CreateInteriorPinGroupHoles();
                var datum = holes.Single(h => h.SourceKey == datumKey);
                if (reverse) holes.Reverse();
                var outline = CreateRectangle(155, 94);
                var datumDefinition = Datum2D.FromOutline(outline);
                datumDefinition.DatumHole = datum;
                var positionPlan = new DimensionPlanner(config).CreateDimensionPlan(outline, datumDefinition, holes);
                positionPlan.PinGroups.Reverse();
                var plans = new HoleCalloutPlanner(config).CreatePlans(holes, datum, positionPlan);
                Assert(plans.Select(p => p.DebugOwner).Distinct().SequenceEqual(new[] { "PG1", "PG2" })
                    && plans.TakeWhile(p => p.DebugOwner == "PG1").Count() == plans.Count(p => p.DebugOwner == "PG1"),
                    "the declared group index must keep every PG1 specification before PG2");
                foreach (var group in positionPlan.PinGroups)
                {
                    var current = plans.Where(p => p.DebugOwner == "PG" + group.GroupIndex).ToList();
                    Assert(current[0].Kind == HoleCalloutKind.Pin
                        && current.SelectMany(p => p.Holes).Count() == group.MemberHoles.Count
                        && group.MemberHoles.All(h => current.Any(p => p.Holes.Contains(h)))
                        && current.Where(p => p.Kind == HoleCalloutKind.Thread)
                            .All(p => p.Holes.Select(h => h.ThreadCallout).Distinct().Count() == 1),
                        "separate thread specifications must keep their count and group identity");
                }
            }
        }
    }
}
