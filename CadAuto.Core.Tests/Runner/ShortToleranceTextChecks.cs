using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Planning;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Tests
{
    internal static partial class Program
    {
        private static void ShortToleranceTextAvoids30And9Chain()
        {
            // ASD6 run 20261024131255382: datum 30, pin spacing 30 and transfer 9 share the right lane.
            foreach (double scale in new[] { 1.0, 2.0 })
            foreach (bool reverse in new[] { false, true })
            for (int turn = 0; turn < 4; turn++)
            {
                var dimensions = new[] {
                    new DimensionLayoutItem { Kind = DimensionKind.DatumHoleLocationY, FirstPoint = new Point2D(155, 0), SecondPoint = new Point2D(114.25, 30), Span = 30 },
                    new DimensionLayoutItem { Kind = DimensionKind.PinDistance, FirstPoint = new Point2D(131.2, 39), SecondPoint = new Point2D(131.2, 69), Span = 30, OverrideText = @"30\H0.8x;±0.02\H1x;" },
                    new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance, FirstPoint = new Point2D(114.25, 30), SecondPoint = new Point2D(131.2, 39), Span = 9, OverrideText = @"9\H0.8x;±0.05\H1x;" }
                };
                var config = DimensionRuleConfig.CreateDefault();
                config.TextHeight = 3.5 * scale;
                var rules = new DimensionLayoutRules(config);
                double angle = turn * Math.PI / 2;
                Func<Point2D, Point2D> transform = point => {
                    var p = RotatePoint(new Point2D(point.X * scale, point.Y * scale), angle);
                    return new Point2D(p.X + 101745.41572174, p.Y + 8786.12020295);
                };
                var side = new[] { DimensionSide.Right, DimensionSide.Top, DimensionSide.Left, DimensionSide.Bottom }[turn];
                bool horizontal = turn % 2 == 1;
                var placed = new List<DimensionTextPlacementItem>();
                foreach (var dimension in dimensions)
                {
                    var line = transform(new Point2D(204, (dimension.FirstPoint.Y + dimension.SecondPoint.Y) / 2));
                    dimension.FirstPoint = transform(dimension.FirstPoint);
                    dimension.SecondPoint = transform(dimension.SecondPoint);
                    dimension.Span *= scale;
                    placed.Add(new DimensionTextPlacementItem { Dimension = dimension, Side = side, DimLinePoint = line,
                        TextBounds = rules.ComputePlacedTextBounds(dimension, line, horizontal, config.TextHeight) });
                }
                if (reverse) placed.Reverse();
                var slides = rules.SelectShortLocalDimensionTextSlides(placed, new TextBounds2D[0], config.TextHeight, 2.5 * scale, 1.75 * scale);
                var bounds = placed.Select(p => p.TextBounds).ToList();
                foreach (var slide in slides) bounds[slide.Index] = slide.TextBounds;
                for (int i = 0; i < bounds.Count; i++)
                for (int j = i + 1; j < bounds.Count; j++)
                    Assert(!rules.TextBoundsOverlap(bounds[i], bounds[j], 0),
                        "all final 30/9/30 text must be disjoint, including text shifted after the datum was processed; turn=" + turn);
                int target = placed.FindIndex(p => p.Dimension.Kind == DimensionKind.PinGroupDistance);
                var moved = slides.Single(p => p.Index == target);
                Assert(Math.Abs((horizontal ? moved.TextPosition.Y : moved.TextPosition.X)
                    - (horizontal ? placed[target].DimLinePoint.Y : placed[target].DimLinePoint.X)) < .001,
                    "text movement must retain the common dimension-line coordinate");
                foreach (var item in placed)
                foreach (var point in new[] { item.Dimension.FirstPoint, item.Dimension.SecondPoint })
                {
                    double axis = horizontal ? point.X : point.Y;
                    double min = horizontal ? moved.TextBounds.MinX : moved.TextBounds.MinY;
                    double max = horizontal ? moved.TextBounds.MaxX : moved.TextBounds.MaxY;
                    Assert(max < axis - 2.5 * scale || min > axis + 2.5 * scale,
                        "the displaced 9 tolerance text must also clear every arrow in its chain");
                }
                Assert(placed.Count == 3 && dimensions.Count(d => d.OverrideText != null && d.OverrideText.Contains("±")) == 2,
                    "text avoidance must preserve all dimensions and tolerance strings");
            }
        }

        private static void ShortToleranceTextEscapesBlockedCandidates()
        {
            var rules = new DimensionLayoutRules(DimensionRuleConfig.CreateDefault());
            var dimension = new DimensionLayoutItem { Kind = DimensionKind.PinGroupDistance,
                FirstPoint = new Point2D(20, 30), SecondPoint = new Point2D(20, 39), Span = 9,
                OverrideText = @"9\H0.8x;±0.05\H1x;" };
            var line = new Point2D(30, 34.5);
            var placed = new DimensionTextPlacementItem { Dimension = dimension, Side = DimensionSide.Right, DimLinePoint = line,
                TextBounds = rules.ComputePlacedTextBounds(dimension, line, false, 3.5) };
            var obstacles = new[] {
                new TextBounds2D { MinX = 27, MaxX = 33, MinY = -20, MaxY = 29 },
                new TextBounds2D { MinX = 27, MaxX = 33, MinY = 40, MaxY = 90 }
            };
            var moved = rules.SelectShortLocalDimensionTextSlides(new[] { placed }, obstacles, 3.5, 2.5, 1.75).Single();
            Assert(obstacles.All(b => !rules.TextBoundsOverlap(moved.TextBounds, b, 1.75)),
                "when both immediate candidates are blocked, find a free position beyond finite obstacles instead of accepting a collision");
            Assert(moved.TextPosition.X == line.X, "obstacle avoidance must not change the chain's dimension-line coordinate");
        }
    }
}
