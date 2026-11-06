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
        private static bool LooseDatumConnects(PlannedDimension dimension, Point2D first, Point2D second)
        {
            return dimension.FirstPoint.DistanceTo(first) < .001 && dimension.SecondPoint.DistanceTo(second) < .001
                || dimension.FirstPoint.DistanceTo(second) < .001 && dimension.SecondPoint.DistanceTo(first) < .001;
        }

        private static void LooseDatumKeeps8And28DropsRedundant20()
        {
            // The user's manual drawing keeps the two datum locations, not the closed-loop thread pitch.
            foreach (double angle in new[] { 0.0, Math.PI / 2, Math.PI, 3 * Math.PI / 2, .37 })
            foreach (bool mirror in new[] { false, true })
            foreach (bool reverse in new[] { false, true })
            {
                var holes = CreateInteriorPinGroupHoles();
                var pin = holes.Single(h => h.SourceKey == "681EF2");
                var lower = holes.Single(h => h.SourceKey == "681EC2");
                var upper = holes.Single(h => h.SourceKey == "681EBF");
                var origin = new Point2D(101745.41572174, 8786.12020295);
                var frame = new CoordinateFrame2D(origin, RotatePoint(new Point2D(1, 0), angle));
                foreach (var hole in holes)
                {
                    var point = new Point2D(mirror ? 160 - hole.Center.X : hole.Center.X, hole.Center.Y);
                    hole.Center = frame.ToWorld(point);
                }
                if (reverse) holes.Reverse();
                var points = new[] { new Point2D(0, 0), new Point2D(160, 0), new Point2D(160, 94), new Point2D(0, 94) }
                    .Select(frame.ToWorld).ToArray();
                var outline = new OutlineFeature2D { MinX = points.Min(q => q.X), MaxX = points.Max(q => q.X),
                    MinY = points.Min(q => q.Y), MaxY = points.Max(q => q.Y) };
                for (int i = 0; i < points.Length; i++) AddSegment(outline, points[i], points[(i + 1) % points.Length], "datum-boundary-" + i);
                var datum = Datum2D.FromOutline(outline);
                datum.DatumHole = pin;
                var plan = new DimensionPlanner(DimensionRuleConfig.CreateDefault())
                    .CreateDimensionPlan(outline, datum, holes, new SlotFeature2D[0], frame);
                var p = frame.ToLocal(pin.Center); var a = frame.ToLocal(lower.Center); var b = frame.ToLocal(upper.Center);
                Assert(plan.Dimensions.Any(d => d.DebugRole == "LooseHole" && LooseDatumConnects(d, p, a)
                    && Math.Abs(GetSpan(d) - 8) < .001), "the lower thread must keep its direct datum location 8");
                Assert(plan.Dimensions.Any(d => d.DebugRole == "LooseHole" && LooseDatumConnects(d, p, b)
                    && Math.Abs(GetSpan(d) - 28) < .001), "the upper thread must keep its direct datum location 28");
                Assert(!plan.Dimensions.Any(d => LooseDatumConnects(d, a, b)), "redundant thread pitch 20 must be removed");
                var suppressed = plan.Diagnostics.DimensionCandidates.Single(c => c.IsSuppressed
                    && c.RuleId == "LooseHole.CommonDatumRedundantPitch" && Math.Abs(c.Value - 20) < .001);
                Assert(new[] { pin.SourceKey, lower.SourceKey, upper.SourceKey }.All(suppressed.SourceGeometryIds.Contains)
                    && suppressed.TopologyEvidence.Contains("SupportCandidateIds="),
                    "suppression must identify the real three holes and both retained datum supports");
                Assert(plan.Dimensions.Count(d => d.DebugRole == "FunctionalHole" && Math.Abs(GetSpan(d) - 20) < .001) == 2,
                    "the two legal functional-hole locations 20 must remain");
                Assert(plan.Dimensions.Any(d => d.DebugRole == "LooseHole" && Math.Abs(GetSpan(d) - 40) < .001),
                    "the normal-hole pair spanning both sides of the datum must retain its group pitch 40");
            }
        }

        private static void LooseDatumKeepsPitchAcrossDatumAndWithoutTwoSupports()
        {
            foreach (double lowerY in new[] { 22.0, 30.0 })
            {
                var holes = CreateInteriorPinGroupHoles();
                var lower = holes.Single(h => h.SourceKey == "681EC2");
                var upper = holes.Single(h => h.SourceKey == "681EBF");
                lower.Center = new Point2D(lower.Center.X, lowerY);
                upper.Center = new Point2D(upper.Center.X, lowerY + 20);
                var outline = CreateRectangle(160, 94);
                var datum = Datum2D.FromOutline(outline);
                datum.DatumHole = holes.Single(h => h.SourceKey == "681EF2");
                var plan = new DimensionPlanner(DimensionRuleConfig.CreateDefault()).CreateDimensionPlan(outline, datum, holes);
                Assert(!plan.Diagnostics.DimensionCandidates.Any(c => c.RuleId == "LooseHole.CommonDatumRedundantPitch"),
                    "straddling or zero-reference thread positions must not trigger common-datum pitch suppression");
                Assert(plan.Dimensions.All(d => GetSpan(d) > .001), "a hole on the datum axis must not produce a zero dimension");
            }
        }

        private static void LooseDatumNestedSpansDoNotMergeThroughBridge()
        {
            foreach (bool includePitch in new[] { false, true })
            foreach (double scale in new[] { 1.0, 2.0 })
            for (int turn = 0; turn < 4; turn++)
            {
                var dimensions = new List<DimensionLayoutItem> {
                    new DimensionLayoutItem { FirstPoint = new Point2D(40, 38), SecondPoint = new Point2D(40, 58), Span = 20, LooseChainId = 3 },
                    new DimensionLayoutItem { FirstPoint = new Point2D(111, 30), SecondPoint = new Point2D(78, 26.5), Span = 3.5, LooseChainId = 5 },
                    new DimensionLayoutItem { FirstPoint = new Point2D(111, 30), SecondPoint = new Point2D(40, 38), Span = 8, LooseChainId = 11 },
                    new DimensionLayoutItem { FirstPoint = new Point2D(111, 30), SecondPoint = new Point2D(40, 58), Span = 28, LooseChainId = 13 }
                };
                if (!includePitch) dimensions.RemoveAt(0);
                double angle = turn * Math.PI / 2;
                foreach (var dim in dimensions)
                {
                    dim.Kind = DimensionKind.HoleLocation;
                    dim.PreferLocalBoundary = true;
                    dim.FirstPoint = RotatePoint(new Point2D(dim.FirstPoint.X * scale, dim.FirstPoint.Y * scale), angle);
                    dim.SecondPoint = RotatePoint(new Point2D(dim.SecondPoint.X * scale, dim.SecondPoint.Y * scale), angle);
                    dim.Span *= scale;
                }
                var outline = RotateOutline(CreateRectangle(160 * scale, 94 * scale), angle);
                var side = new[] { DimensionSide.Left, DimensionSide.Bottom, DimensionSide.Right, DimensionSide.Top }[turn];
                bool horizontal = turn % 2 == 1;
                var config = DimensionRuleConfig.CreateDefault();
                config.TextHeight = 3.5 * scale;
                var rules = new DimensionLayoutRules(config);
                dimensions = rules.GetStackingOrder(dimensions, horizontal).Select(i => dimensions[i]).ToList();
                var placements = rules.CreateStackingPlan(dimensions, side, outline, config.TextHeight, 1.75 * scale, 10 * scale, 6.5 * scale, horizontal);
                foreach (var first in placements)
                foreach (var second in placements.Where(p => p.Index > first.Index))
                {
                    var a = dimensions[first.Index]; var b = dimensions[second.Index];
                    double a1 = horizontal ? a.FirstPoint.X : a.FirstPoint.Y, a2 = horizontal ? a.SecondPoint.X : a.SecondPoint.Y;
                    double b1 = horizontal ? b.FirstPoint.X : b.FirstPoint.Y, b2 = horizontal ? b.SecondPoint.X : b.SecondPoint.Y;
                    if (Math.Min(Math.Max(a1, a2), Math.Max(b1, b2)) - Math.Max(Math.Min(a1, a2), Math.Min(b1, b2)) < .001) continue;
                    double x = first.DimLineCoordinateOverride ?? rules.GetDimLineCoordinate(dimensions[first.Index], side, outline, first.Offset);
                    double y = second.DimLineCoordinateOverride ?? rules.GetDimLineCoordinate(dimensions[second.Index], side, outline, second.Offset);
                    Assert(Math.Abs(x - y) >= 6.5 * scale - .001,
                        "a shared-endpoint bridge must not pull overlapping loose spans onto one dimension line; turn=" + turn);
                }
            }
        }
    }
}
