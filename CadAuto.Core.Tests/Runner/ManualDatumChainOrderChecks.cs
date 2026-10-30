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
        // Frozen run 20261024135402692; keep the selected centerline endpoints, which differ on both axes.
        private static Dictionary<int, DimensionLayoutItem> CreateManualOrderItems(bool bottom)
        {
            var items = new Dictionary<int, DimensionLayoutItem>();
            if (bottom) items.Add(17, new DimensionLayoutItem { FirstPoint = new Point2D(154.99999891, 0.00000678), SecondPoint = new Point2D(110.99999402, 26.74999812), Span = 44.00000488, Kind = DimensionKind.DatumHoleLocationX, SourceFeatureId = "PG1", AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 120, PreserveAlignmentLevel = false, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.DatumTransfer, OverrideText = "" });
            if (!bottom) items.Add(18, new DimensionLayoutItem { FirstPoint = new Point2D(155.00000253, 0.00000000), SecondPoint = new Point2D(114.24999402, 29.99999812), Span = 29.99999812, Kind = DimensionKind.DatumHoleLocationY, SourceFeatureId = "PG1", AlignmentKey = "PG1:DatumChain:V", AlignmentPriority = 120, PreserveAlignmentLevel = false, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.DatumTransfer, OverrideText = "" });
            if (bottom) items.Add(19, new DimensionLayoutItem { FirstPoint = new Point2D(110.99999402, 26.74999812), SecondPoint = new Point2D(45.00000165, 26.74999812), Span = 65.99999237, Kind = DimensionKind.PinDistance, SourceFeatureId = "PG1", AlignmentKey = "PG1:DatumChain:H", AlignmentPriority = 100, PreserveAlignmentLevel = false, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.IntraGroup, OverrideText = "66\\H0.8x;\u00b10.02\\H1x;" });
            if (!bottom) items.Add(20, new DimensionLayoutItem { FirstPoint = new Point2D(131.20000623, 38.99999812), SecondPoint = new Point2D(131.20000623, 68.99999812), Span = 30.00000000, Kind = DimensionKind.PinDistance, SourceFeatureId = "PG2", AlignmentKey = "PG1:DatumChain:V", AlignmentPriority = 100, PreserveAlignmentLevel = false, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.IntraGroup, OverrideText = "30\\H0.8x;\u00b10.02\\H1x;" });
            if (bottom) items.Add(21, new DimensionLayoutItem { FirstPoint = new Point2D(110.99999402, 26.74999812), SecondPoint = new Point2D(125.99999402, 33.80004695), Span = 15.00000000, Kind = DimensionKind.PinGroupDistance, SourceFeatureId = "PG2", AlignmentKey = "PG2:PinPair:H", AlignmentPriority = 110, PreserveAlignmentLevel = false, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.DatumTransfer, OverrideText = "15\\H0.8x;\u00b10.05\\H1x;" });
            if (!bottom) items.Add(22, new DimensionLayoutItem { FirstPoint = new Point2D(114.24999402, 29.99999812), SecondPoint = new Point2D(131.20000623, 38.99999812), Span = 9.00000000, Kind = DimensionKind.PinGroupDistance, SourceFeatureId = "PG2", AlignmentKey = "PG1:DatumChain:V", AlignmentPriority = 110, PreserveAlignmentLevel = false, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.DatumTransfer, OverrideText = "9\\H0.8x;\u00b10.05\\H1x;" });
            if (bottom) items.Add(23, new DimensionLayoutItem { FirstPoint = new Point2D(110.99999402, 26.74999812), SecondPoint = new Point2D(143.00000928, -1.69995305), Span = 32.00001526, Kind = DimensionKind.HoleLocation, SourceFeatureId = "PG1", AlignmentKey = "PG1:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, OverrideText = "" });
            if (!bottom) items.Add(24, new DimensionLayoutItem { FirstPoint = new Point2D(114.24999402, 29.99999812), SecondPoint = new Point2D(154.70000623, 49.99999812), Span = 20.00000000, Kind = DimensionKind.HoleLocation, SourceFeatureId = "PG1", AlignmentKey = "PG1:FunctionalHoles:V", AlignmentPriority = 90, PreserveAlignmentLevel = true, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, OverrideText = "" });
            if (bottom) items.Add(25, new DimensionLayoutItem { FirstPoint = new Point2D(110.99999402, 26.74999812), SecondPoint = new Point2D(13.00000165, -1.69995305), Span = 97.99999237, Kind = DimensionKind.HoleLocation, SourceFeatureId = "PG1", AlignmentKey = "PG1:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, OverrideText = "" });
            if (!bottom) items.Add(26, new DimensionLayoutItem { FirstPoint = new Point2D(114.24999402, 29.99999812), SecondPoint = new Point2D(154.70000623, 9.99999812), Span = 20.00000000, Kind = DimensionKind.HoleLocation, SourceFeatureId = "PG1", AlignmentKey = "PG1:FunctionalHoles:V", AlignmentPriority = 90, PreserveAlignmentLevel = true, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, OverrideText = "" });
            if (!bottom) items.Add(31, new DimensionLayoutItem { FirstPoint = new Point2D(131.20000623, 38.99999812), SecondPoint = new Point2D(130.32055066, 53.99999812), Span = 15.00000000, Kind = DimensionKind.HoleLocation, SourceFeatureId = "PG2", AlignmentKey = "PG2:FunctionalHoles:V", AlignmentPriority = 90, PreserveAlignmentLevel = true, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, OverrideText = "" });
            if (!bottom) items.Add(32, new DimensionLayoutItem { FirstPoint = new Point2D(131.20000623, 38.99999812), SecondPoint = new Point2D(130.32055066, 83.99999812), Span = 45.00000000, Kind = DimensionKind.HoleLocation, SourceFeatureId = "PG2", AlignmentKey = "PG2:FunctionalHoles:V", AlignmentPriority = 90, PreserveAlignmentLevel = true, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, OverrideText = "" });
            if (bottom) items.Add(35, new DimensionLayoutItem { FirstPoint = new Point2D(110.99999402, 26.74999812), SecondPoint = new Point2D(78.00000165, 23.30395320), Span = 32.99999237, Kind = DimensionKind.HoleLocation, SourceFeatureId = "L4", AlignmentKey = "", AlignmentPriority = 0, PreserveAlignmentLevel = false, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, OverrideText = "", LooseChainId = 4 });
            if (bottom) items.Add(37, new DimensionLayoutItem { FirstPoint = new Point2D(110.99999402, 26.74999812), SecondPoint = new Point2D(83.00000165, 2.19994929), Span = 27.99999237, Kind = DimensionKind.HoleLocation, SourceFeatureId = "L6", AlignmentKey = "", AlignmentPriority = 0, PreserveAlignmentLevel = false, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, OverrideText = "", LooseChainId = 6 });
            if (bottom) items.Add(41, new DimensionLayoutItem { FirstPoint = new Point2D(110.99999402, 26.74999812), SecondPoint = new Point2D(40.33677076, 34.80395320), Span = 70.66322327, Kind = DimensionKind.HoleLocation, SourceFeatureId = "L10", AlignmentKey = "", AlignmentPriority = 0, PreserveAlignmentLevel = false, PreferLocalBoundary = true, ReadingLevel = DimensionReadingLevel.LocalSpacing, OverrideText = "", LooseChainId = 10 });
            return items;
        }

        private static void ManualOrderKeepsDatumChainInsideLongerLooseLocation()
        {
            CheckManualDatumChainOrder(true);
            var items = CreateManualOrderItems(true);
            var loose = items[41];
            // The same projected coordinate alone cannot prove that two locations share a pin.
            loose.FirstPoint = new Point2D(loose.FirstPoint.X, loose.FirstPoint.Y + 7);
            var dimensions = new[] { items[17], items[19], loose };
            var order = new DimensionLayoutRules(DimensionRuleConfig.CreateDefault()).GetStackingOrder(dimensions, true);
            Assert(order.IndexOf(2) < order.IndexOf(0), "an unrelated projected datum must retain ordinary union-span order");
        }

        private static void ManualOrderUsesMeasuredAxisForFunctionalHole()
        {
            CheckManualDatumChainOrder(false);
        }

        private static void CheckManualDatumChainOrder(bool bottom)
        {
            foreach (double scale in new[] { 1.0, 2.0 })
            foreach (bool reverse in new[] { false, true })
            for (int turn = 0; turn < 4; turn++)
            {
                double angle = turn * Math.PI / 2;
                Func<Point2D, Point2D> transform = p => {
                    var q = RotatePoint(new Point2D(p.X * scale, p.Y * scale), angle);
                    return new Point2D(q.X + 101745.41572174, q.Y + 8786.12020295);
                };
                var items = CreateManualOrderItems(bottom);
                if (!bottom) CopyIndependentDatumPlanKeys(items);
                foreach (var d in items.Values)
                {
                    d.FirstPoint = transform(d.FirstPoint); d.SecondPoint = transform(d.SecondPoint); d.Span *= scale;
                }
                var contour = new[] { new Point2D(0, 0), new Point2D(0, 58), new Point2D(10, 68),
                    new Point2D(116, 68), new Point2D(116, 94), new Point2D(136, 94),
                    new Point2D(136, 83), new Point2D(155, 83), new Point2D(155, 0) }.Select(transform).ToArray();
                var outline = new OutlineFeature2D { MinX = contour.Min(p => p.X), MaxX = contour.Max(p => p.X),
                    MinY = contour.Min(p => p.Y), MaxY = contour.Max(p => p.Y) };
                for (int i = 0; i < contour.Length; i++) AddSegment(outline, contour[i], contour[(i + 1) % contour.Length], "manual-order-" + i);
                var config = DimensionRuleConfig.CreateDefault();
                config.TextHeight = 3.5 * scale;
                var rules = new DimensionLayoutRules(config);
                bool horizontal = bottom == (turn % 2 == 0);
                var side = bottom ? new[] { DimensionSide.Bottom, DimensionSide.Right, DimensionSide.Top, DimensionSide.Left }[turn]
                    : new[] { DimensionSide.Right, DimensionSide.Top, DimensionSide.Left, DimensionSide.Bottom }[turn];
                var dimensions = items.Values.ToList();
                if (reverse) dimensions.Reverse();
                dimensions = rules.GetStackingOrder(dimensions, horizontal).Select(i => dimensions[i]).ToList();
                var placements = rules.CreateStackingPlan(dimensions, side, outline, config.TextHeight, 1.75 * scale, 10 * scale, 6.5 * scale, horizontal);
                var byId = items.ToDictionary(pair => pair.Key, pair => placements.Single(p => dimensions[p.Index] == pair.Value));
                Func<int, double> distance = id => {
                    var p = byId[id];
                    double line = p.DimLineCoordinateOverride ?? rules.GetDimLineCoordinate(items[id], side, outline, p.Offset);
                    return side == DimensionSide.Bottom ? outline.MinY - line : side == DimensionSide.Top ? line - outline.MaxY
                        : side == DimensionSide.Left ? outline.MinX - line : line - outline.MaxX;
                };
                var chain = bottom ? new[] { 17, 19 } : new[] { 20, 22 };
                Assert(chain.Select(id => byId[id].LayoutBlockId).Distinct().Count() == 1
                    && chain.All(id => Math.Abs(distance(id) - distance(chain[0])) < .001),
                    "the rooted datum chain must remain one aligned block");
                int outer = bottom ? 41 : 32;
                Assert(distance(outer) >= distance(chain[0]) + 6.5 * scale - .001,
                    "manual order requires " + (bottom ? "66+44 inside 70.66" : "9/30 inside 45") + "; turn=" + turn);
                if (bottom)
                    Assert(distance(23) > distance(41) && Math.Abs(distance(23) - distance(25)) < .001,
                        "functional 98/32 must remain together outside 70.66");
                else
                    Assert(distance(31) < distance(24) && distance(24) < distance(20)
                        && Math.Abs(distance(24) - distance(26)) < .001,
                        "local 15 then 20/20 must remain inside the datum chain");
                if (!bottom)
                {
                    Assert(byId[18].LayoutBlockId != byId[20].LayoutBlockId && distance(18) >= distance(20) + 6.5 * scale - .001,
                        "the datum 30 must occupy an independent outer lane, beyond the other pin group pair");
                    CheckIndependentDatumText(dimensions, placements, side, outline, items[18], rules, scale, horizontal);
                }
                Assert(placements.Count == items.Count && placements.All(p => p.PhysicalOrderValidated),
                    "preserve every dimension and validate physical ordering");
            }
        }
    }
}
