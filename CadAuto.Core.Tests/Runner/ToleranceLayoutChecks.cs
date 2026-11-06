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
        private static void ToleranceTextDoesNotBorrowAlignedNeighborInterval()
        {
            foreach (double scale in new[] { 1.0, 2.0 })
            foreach (bool reverse in new[] { false, true })
            for (int turn = 0; turn < 4; turn++)
            {
                double angle = turn * Math.PI / 2;
                Func<Point2D, Point2D> transform = p => RotatePoint(new Point2D(p.X * scale, p.Y * scale), angle);
                bool horizontal = turn % 2 == 1;
                var side = new[] { DimensionSide.Right, DimensionSide.Top, DimensionSide.Left, DimensionSide.Bottom }[turn];
                var rules = new DimensionLayoutRules(DimensionRuleConfig.CreateDefault());
                var dimensions = new[] {
                    new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = transform(new Point2D(20, 30)),
                        SecondPoint = transform(new Point2D(20, 39)), Span = 9 * scale, OverrideText = @"9\H0.8x;±0.05\H1x;" },
                    new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = transform(new Point2D(20, 39)),
                        SecondPoint = transform(new Point2D(20, 119)), Span = 80 * scale, OverrideText = @"80\H0.8x;±0.02\H1x;" }
                };
                var placed = dimensions.Select(d => {
                    var line = transform(new Point2D(30, d == dimensions[0] ? 34.5 : 79));
                    return new DimensionTextPlacementItem { Dimension = d, Side = side, DimLinePoint = line,
                        TextBounds = rules.ComputePlacedTextBounds(d, line, horizontal, 3.5 * scale) };
                }).ToList();
                if (reverse) placed.Reverse();
                var corners = new[] { transform(new Point2D(27, 8)), transform(new Point2D(33, 26)) };
                var obstacle = new TextBounds2D { MinX = corners.Min(p => p.X), MaxX = corners.Max(p => p.X),
                    MinY = corners.Min(p => p.Y), MaxY = corners.Max(p => p.Y) };
                var slides = rules.SelectShortLocalDimensionTextSlides(placed, new[] { obstacle }, 3.5 * scale, 2.5 * scale, 3 * scale);
                var moved = slides.Single(p => placed[p.Index].Dimension == dimensions[0]);
                double low = horizontal ? moved.TextBounds.MinX : moved.TextBounds.MinY;
                double high = horizontal ? moved.TextBounds.MaxX : moved.TextBounds.MaxY;
                double a = horizontal ? dimensions[1].FirstPoint.X : dimensions[1].FirstPoint.Y;
                double b = horizontal ? dimensions[1].SecondPoint.X : dimensions[1].SecondPoint.Y;
                Assert(high < Math.Min(a, b) || low > Math.Max(a, b),
                    "a clear text box must not borrow the measured interval of an aligned neighbor");
                Assert(!rules.TextBoundsOverlap(moved.TextBounds, obstacle, 1.75 * scale),
                    "rejecting the upper interval must not fall back onto the lower obstacle");
            }
        }

        private static void ToleranceLayoutReservesTextSpaceAfterAlignment()
        {
            foreach (double scale in new[] { 1.0, 2.0 })
            foreach (bool reverse in new[] { false, true })
            for (int turn = 0; turn < 4; turn++)
            foreach (bool tolerance in new[] { false, true })
            {
                double angle = turn * Math.PI / 2;
                Func<Point2D, Point2D> transform = p => {
                    var q = RotatePoint(new Point2D(p.X * scale, p.Y * scale), angle);
                    return new Point2D(q.X + 101745.41572174, q.Y + 8786.12020295);
                };
                var items = CreateManualOrderItems(false);
                CopyIndependentDatumPlanKeys(items);
                var dimensions = new[] { items[18], items[20], items[22] }.ToList();
                foreach (var d in dimensions)
                {
                    d.FirstPoint = transform(d.FirstPoint); d.SecondPoint = transform(d.SecondPoint); d.Span *= scale;
                    d.DatumFamilyKey = "PG1:BasePin"; d.ParallelStackKey = "ToleranceTextStack";
                    if (!tolerance) d.OverrideText = "";
                }
                var points = new[] { transform(new Point2D(0, 0)), transform(new Point2D(155, 0)),
                    transform(new Point2D(155, 94)), transform(new Point2D(0, 94)) };
                var outline = new OutlineFeature2D { MinX = points.Min(p => p.X), MaxX = points.Max(p => p.X),
                    MinY = points.Min(p => p.Y), MaxY = points.Max(p => p.Y) };
                for (int i = 0; i < 4; i++) AddSegment(outline, points[i], points[(i + 1) % 4], "tolerance-space-" + i);
                if (reverse) dimensions.Reverse();
                bool horizontal = turn % 2 == 1;
                var side = new[] { DimensionSide.Right, DimensionSide.Top, DimensionSide.Left, DimensionSide.Bottom }[turn];
                var config = DimensionRuleConfig.CreateDefault(); config.TextHeight = 5 * scale;
                var rules = new DimensionLayoutRules(config);
                var placements = rules.CreateStackingPlan(dimensions, side, outline, 5 * scale, 2.5 * scale, 10 * scale, 8 * scale, horizontal);
                Func<DimensionLayoutItem, double> coordinate = d => {
                    var p = placements.Single(x => dimensions[x.Index] == d);
                    return p.DimLineCoordinateOverride ?? rules.GetDimLineCoordinate(d, side, outline, p.Offset);
                };
                Assert(Math.Abs(coordinate(items[20]) - coordinate(items[22])) < .001,
                    "text-space reservation must keep the pin distance and transfer aligned");
                double distance = Math.Abs(coordinate(items[18]) - coordinate(items[22]));
                Assert(tolerance ? distance > 9 * scale : Math.Abs(distance - 8 * scale) < .001,
                    "reserve the complete tolerance text gap only when the text no longer fits the short interval");
                Assert(placements.All(p => p.PhysicalOrderValidated), "text-space adjustment must preserve physical order");
                var placed = dimensions.Select(d => {
                    var line = horizontal ? new Point2D((d.FirstPoint.X + d.SecondPoint.X) / 2, coordinate(d))
                        : new Point2D(coordinate(d), (d.FirstPoint.Y + d.SecondPoint.Y) / 2);
                    return new DimensionTextPlacementItem { Dimension = d, Side = side, DimLinePoint = line,
                        TextBounds = rules.ComputePlacedTextBounds(d, line, horizontal, 5 * scale) };
                }).ToList();
                var slides = rules.SelectShortLocalDimensionTextSlides(placed, new TextBounds2D[0], 5 * scale, 2.5 * scale, 3 * scale);
                var bounds = placed.Select(p => p.TextBounds).ToArray();
                foreach (var slide in slides) bounds[slide.Index] = slide.TextBounds;
                for (int i = 0; i < bounds.Length; i++)
                for (int j = i + 1; j < bounds.Length; j++)
                    Assert(!rules.TextBoundsOverlap(bounds[i], bounds[j], 2.5 * scale), "reserved lanes must clear the final complete text");
            }
        }
    }
}
