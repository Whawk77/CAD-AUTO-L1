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
        private static DimensionPlan CreateIndependentDatumPlan()
        {
            var holes = CreateInteriorPinGroupHoles();
            var outline = CreateRectangle(160, 94);
            var datum = Datum2D.FromOutline(outline);
            datum.DatumHole = holes.Single(h => h.SourceKey == "681EF2");
            datum.DatumHoleLocationBaseX = 155;
            return new DimensionPlanner(DimensionRuleConfig.CreateDefault())
                .CreateDimensionPlan(outline, datum, holes);
        }

        private static void CopyIndependentDatumPlanKeys(IDictionary<int, DimensionLayoutItem> items)
        {
            var plan = CreateIndependentDatumPlan();
            items[18].AlignmentKey = plan.Dimensions.Single(d => d.Kind == DimensionKind.DatumHoleLocationY).AlignmentKey;
            items[20].AlignmentKey = plan.Dimensions.Single(d => d.Kind == DimensionKind.PinDistance && d.Orientation == DimensionOrientation.Vertical).AlignmentKey;
            items[22].AlignmentKey = plan.Dimensions.Single(d => d.Kind == DimensionKind.PinGroupDistance && d.Orientation == DimensionOrientation.Vertical).AlignmentKey;
        }

        private static void IndependentDatumDoesNotBorrowForeignPinPairLane()
        {
            var plan = CreateIndependentDatumPlan();
            var datum = plan.Dimensions.Single(d => d.Kind == DimensionKind.DatumHoleLocationY);
            var pin = plan.Dimensions.Single(d => d.Kind == DimensionKind.PinDistance && d.Orientation == DimensionOrientation.Vertical);
            var transfer = plan.Dimensions.Single(d => d.Kind == DimensionKind.PinGroupDistance && d.Orientation == DimensionOrientation.Vertical);
            Assert(datum.AlignmentKey != pin.AlignmentKey && pin.AlignmentKey == transfer.AlignmentKey,
                "datum 30 must stay independent of the other group's 9+30 pair, even when all face right");
            var datumX = plan.Dimensions.Single(d => d.Kind == DimensionKind.DatumHoleLocationX);
            var pinX = plan.Dimensions.Single(d => d.Kind == DimensionKind.PinDistance && d.Orientation == DimensionOrientation.Horizontal);
            Assert(datumX.AlignmentKey == pinX.AlignmentKey,
                "the owning group's legitimate bottom 44+66 chain must retain its shared lane");
        }

        private static void IndependentDatumPlacementClearsFinalText()
        {
            CheckManualDatumChainOrder(false);
        }

        private static void CheckIndependentDatumText(IList<DimensionLayoutItem> dimensions, IList<DimensionStackingPlacement> placements,
            DimensionSide side, OutlineFeature2D outline, DimensionLayoutItem datum, DimensionLayoutRules rules, double scale, bool horizontal)
        {
            var placed = placements.Select(p => {
                var d = dimensions[p.Index];
                double coordinate = p.DimLineCoordinateOverride ?? rules.GetDimLineCoordinate(d, side, outline, p.Offset);
                var line = horizontal ? new Point2D((d.FirstPoint.X + d.SecondPoint.X) / 2, coordinate)
                    : new Point2D(coordinate, (d.FirstPoint.Y + d.SecondPoint.Y) / 2);
                return new DimensionTextPlacementItem { Dimension = d, Side = side, DimLinePoint = line,
                    TextBounds = rules.ComputePlacedTextBounds(d, line, horizontal, 3.5 * scale) };
            }).ToList();
            var slides = rules.SelectShortLocalDimensionTextSlides(placed, new TextBounds2D[0], 3.5 * scale, 2.5 * scale, 3 * scale);
            var bounds = placed.Select(p => p.TextBounds).ToList();
            foreach (var slide in slides) bounds[slide.Index] = slide.TextBounds;
            int target = placed.FindIndex(p => p.Dimension == datum);
            int transfer = placed.FindIndex(p => p.Dimension.Kind == DimensionKind.PinGroupDistance);
            Assert(Enumerable.Range(0, bounds.Count).Where(i => i != target)
                .All(i => !rules.TextBoundsOverlap(bounds[target], bounds[i], (i == transfer ? 1.75 : 3) * scale)),
                "independent datum text must clear final neighboring text by the inter-lane text gap");
            double min = horizontal ? bounds[target].MinX : bounds[target].MinY;
            double max = horizontal ? bounds[target].MaxX : bounds[target].MaxY;
            double a = horizontal ? datum.FirstPoint.X : datum.FirstPoint.Y, b0 = horizontal ? datum.SecondPoint.X : datum.SecondPoint.Y;
            var transferText = bounds[transfer];
            double transferMin = horizontal ? transferText.MinX : transferText.MinY;
            double transferMax = horizontal ? transferText.MaxX : transferText.MaxY;
            Assert(transferMin > Math.Min(a, b0) + 2.5 * scale && transferMax < Math.Max(a, b0) - 2.5 * scale,
                "short transfer text must use the nearby space vacated by the outer datum, not escape beyond the whole chain");
            var moved = slides.Single(p => p.Index == transfer);
            Assert(Math.Abs((horizontal ? moved.TextPosition.Y : moved.TextPosition.X)
                - (horizontal ? placed[transfer].DimLinePoint.Y : placed[transfer].DimLinePoint.X)) < .001,
                "nearby transfer text must stay on the pin-pair dimension line");
            foreach (var item in placed)
            foreach (var endpoint in new[] { item.Dimension.FirstPoint, item.Dimension.SecondPoint })
            {
                double station = horizontal ? endpoint.X : endpoint.Y;
                double cross = horizontal ? item.DimLinePoint.Y : item.DimLinePoint.X;
                double textCrossMin = horizontal ? transferText.MinY : transferText.MinX;
                double textCrossMax = horizontal ? transferText.MaxY : transferText.MaxX;
                Assert(transferMax < station - 5.5 * scale + .001 || transferMin > station + 5.5 * scale - .001
                    || textCrossMax < cross - 3 * scale || textCrossMin > cross + 3 * scale,
                    "nearby transfer text must clear arrows along the line and across neighboring lanes");
            }
            Assert(min > Math.Min(a, b0) + 2.5 * scale && max < Math.Max(a, b0) - 2.5 * scale,
                "datum 30 must remain between its own arrows rather than read as another chain segment");
            double lineCoordinate = horizontal ? placed[target].DimLinePoint.Y : placed[target].DimLinePoint.X;
            for (int i = 0; i < bounds.Count; i++)
            {
                if (i == target) continue;
                var b = bounds[i];
                bool across = horizontal ? b.MinY <= lineCoordinate && lineCoordinate <= b.MaxY
                    : b.MinX <= lineCoordinate && lineCoordinate <= b.MaxX;
                double alongMin = horizontal ? b.MinX : b.MinY, alongMax = horizontal ? b.MaxX : b.MaxY;
                Assert(!across || alongMin > Math.Max(a, b0) || alongMax < Math.Min(a, b0),
                    "the independent datum dimension line must not pass through neighboring final text");
                foreach (var endpoint in new[] { datum.FirstPoint, datum.SecondPoint })
                {
                    double attachment = horizontal ? endpoint.Y : endpoint.X;
                    double station = horizontal ? endpoint.X : endpoint.Y;
                    bool crossesStation = alongMin <= station && station <= alongMax;
                    double crossMin = horizontal ? b.MinY : b.MinX, crossMax = horizontal ? b.MaxY : b.MaxX;
                    Assert(!crossesStation || crossMin > Math.Max(attachment, lineCoordinate)
                        || crossMax < Math.Min(attachment, lineCoordinate),
                        "the independent datum extension lines must not cross neighboring final text");
                }
            }
            for (int i = 0; i < bounds.Count; i++)
            for (int j = i + 1; j < bounds.Count; j++)
                Assert(!rules.TextBoundsOverlap(bounds[i], bounds[j], 0), "independent datum placement must not introduce another text collision");
        }
    }
}
