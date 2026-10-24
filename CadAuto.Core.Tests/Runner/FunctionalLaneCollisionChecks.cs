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
        private static void FunctionalLaneKeeps28And32ClearOfSlot35()
        {
            // ASD6 run 20261024124015414: the user confirmed the overlap of these dimensions.
            foreach (double scale in new[] { 1.0, 2.0 })
            for (int turn = 0; turn < 4; turn++)
            {
                var dimensions = new List<DimensionLayoutItem> {
                new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(80.99998493000749, 31.75000413000089), SecondPoint = new Point2D(146.99998493000749, 31.75000413000089), Span = 66, SourceFeatureId = "PG2", ReadingLevel = DimensionReadingLevel.IntraGroup, DatumFamilyKey = "PG2:BasePin", AlignmentKey = "PG2:PinPair:H", OverrideText = "66\\H0.8x;\u00b10.02\\H1x;", AlignmentPriority = 100, PreferLocalBoundary = true },
                new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(10.198898510003346, 13.8001750300009), SecondPoint = new Point2D(80.99998493000749, 31.75000413000089), Span = 70.80108643, SourceFeatureId = "PG2", ReadingLevel = DimensionReadingLevel.DatumTransfer, AlignmentKey = "PG2:PinPair:H", OverrideText = "70.801\\H0.8x;\u00b10.05\\H1x;", AlignmentPriority = 110, PreferLocalBoundary = true },
                new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(10.198898510003346, 13.8001750300009), SecondPoint = new Point2D(202.99998493000749, 23.800052959999448), Span = 192.80108643, SourceFeatureId = "PG3", ReadingLevel = DimensionReadingLevel.DatumTransfer, AlignmentKey = "PG1:DatumChain:H", OverrideText = "192.801\\H0.8x;\u00b10.05\\H1x;", AlignmentPriority = 110, PreferLocalBoundary = true },
                new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(80.99998493000749, 31.75000413000089), SecondPoint = new Point2D(48.999984930007486, 3.300052959999448), Span = 32, SourceFeatureId = "PG2", ReadingLevel = DimensionReadingLevel.LocalSpacing, DatumFamilyKey = "PG2:BasePin", ParallelStackKey = "PG2:BasePin:Horizontal", AlignmentKey = "PG2:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, PreferLocalBoundary = true },
                new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(80.99998493000749, 31.75000413000089), SecondPoint = new Point2D(178.99998493000749, 3.300052959999448), Span = 98, SourceFeatureId = "PG2", ReadingLevel = DimensionReadingLevel.LocalSpacing, DatumFamilyKey = "PG2:BasePin", ParallelStackKey = "PG2:BasePin:Horizontal", AlignmentKey = "PG2:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, PreferLocalBoundary = true },
                new DimensionLayoutItem { Kind = DimensionKind.HoleLocation, FirstPoint = new Point2D(80.99998493000749, 31.75000413000089), SecondPoint = new Point2D(108.99998493000749, 30.679447489999802), Span = 28, SourceFeatureId = "PG2", ReadingLevel = DimensionReadingLevel.LocalSpacing, DatumFamilyKey = "PG2:BasePin", ParallelStackKey = "PG2:BasePin:Horizontal", AlignmentKey = "PG2:FunctionalHoles:H", AlignmentPriority = 90, PreserveAlignmentLevel = true, PreferLocalBoundary = true },
                new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(91.49999999000283, 13.5), SecondPoint = new Point2D(126.49999999000283, 13.5), Span = 35, SourceFeatureId = "SLOT1", ReadingLevel = DimensionReadingLevel.LocalSpacing },
                new DimensionLayoutItem { Kind = DimensionKind.Normal, FirstPoint = new Point2D(80.99998493000749, 31.75000413000089), SecondPoint = new Point2D(91.49998493000749, 11.69995530000051), Span = 10.5, SourceFeatureId = "SlotPinRef", ReadingLevel = DimensionReadingLevel.LocalSpacing },
                };
                var vertices = new[] {
                    new Point2D(-3.754394128918648e-09, -4.129105946049094e-10),
                    new Point2D(-3.754394128918648e-09, 73.99999999958709),
                    new Point2D(19.999999996245606, 73.99999999958709),
                    new Point2D(19.999999996245606, 64.9999999702577),
                    new Point2D(193.0000003860623, 64.9999999702577),
                    new Point2D(193.0000003860623, 84.00008817364323),
                    new Point2D(212.9999999962456, 84.00008817364323),
                    new Point2D(212.9999999962456, 19.000088173643235),
                    new Point2D(192.9999999962456, -4.129105946049094e-10)
                };
                double angle = turn * Math.PI / 2;
                foreach (var dim in dimensions)
                {
                    dim.FirstPoint = RotatePoint(new Point2D(dim.FirstPoint.X * scale, dim.FirstPoint.Y * scale), angle);
                    dim.SecondPoint = RotatePoint(new Point2D(dim.SecondPoint.X * scale, dim.SecondPoint.Y * scale), angle);
                    dim.Span *= scale;
                }
                var points = vertices.Select(p => RotatePoint(new Point2D(p.X * scale, p.Y * scale), angle)).ToArray();
                var outline = new OutlineFeature2D { MinX = points.Min(p => p.X), MaxX = points.Max(p => p.X), MinY = points.Min(p => p.Y), MaxY = points.Max(p => p.Y) };
                for (int i = 0; i < points.Length; i++) AddSegment(outline, points[i], points[(i + 1) % points.Length], "report-boundary-" + i);
                var side = new[] { DimensionSide.Bottom, DimensionSide.Right, DimensionSide.Top, DimensionSide.Left }[turn];
                bool horizontal = turn % 2 == 0;
                var config = DimensionRuleConfig.CreateDefault();
                config.TextHeight = 3.5 * scale;
                config.ArrowSize = 2.5 * scale;
                var rules = new DimensionLayoutRules(config);
                dimensions = rules.GetStackingOrder(dimensions, horizontal).Select(i => dimensions[i]).ToList();
                var placements = rules.CreateStackingPlan(dimensions, side, outline, config.TextHeight, 1.75 * scale, 10 * scale, 6.5 * scale, horizontal);
                Func<double, DimensionStackingPlacement> get = span => placements.Single(p => Math.Abs(dimensions[p.Index].Span - span * scale) < .001);
                Func<DimensionStackingPlacement, double> coordinate = p => p.DimLineCoordinateOverride ?? rules.GetDimLineCoordinate(dimensions[p.Index], side, outline, p.Offset);
                var hole28 = get(28); var hole32 = get(32); var slot35 = get(35); var hole98 = get(98);
                Assert(Math.Abs(coordinate(hole28) - coordinate(slot35)) >= 6.5 * scale - config.GeometryTolerance,
                    "functional hole 28 and slot 35 must remain separated after alignment; turn=" + turn + " coords=" + coordinate(hole28) + "/" + coordinate(slot35));
                Assert(Math.Abs(coordinate(hole28) - coordinate(hole32)) < config.GeometryTolerance,
                    "adjacent same-group hole locations 28 and 32 must retain their safe shared-endpoint alignment");
                Assert(Math.Abs(coordinate(hole98) - coordinate(hole28)) >= 6.5 * scale - config.GeometryTolerance,
                    "overlapping same-group hole spans 28 and 98 must stay on separate dimension lines");
                Assert(placements.All(p => p.PhysicalOrderValidated), "the final physical-order diagnostic must accept the safe alignment and separation");
            }
        }
    }
}
