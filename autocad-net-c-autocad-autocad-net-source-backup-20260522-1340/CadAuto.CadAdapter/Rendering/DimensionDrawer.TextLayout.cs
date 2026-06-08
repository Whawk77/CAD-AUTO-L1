using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private void PromoteLooseChainLevel(
            List<List<(DeferredDim Dim, double TxtA, double TxtB, double ArrA, double ArrB)>> layers,
            int chainId,
            int targetLevel)
        {
            if (chainId == 0)
            {
                return;
            }

            while (layers.Count <= targetLevel)
            {
                layers.Add(new List<(DeferredDim, double, double, double, double)>());
            }

            for (int level = 0; level < layers.Count; level++)
            {
                if (level == targetLevel)
                {
                    continue;
                }

                for (int i = layers[level].Count - 1; i >= 0; i--)
                {
                    if (layers[level][i].Dim.LooseChainId != chainId)
                    {
                        continue;
                    }

                    var item = layers[level][i];
                    layers[level].RemoveAt(i);
                    layers[targetLevel].Add(item);
                }
            }
        }

        private void AdjustVerticalHoleLocationTextPositions(IList<PlacedDim> placedDims, double textHeight, double gap)
        {
            for (int i = 0; i < placedDims.Count; i++)
            {
                var placed = placedDims[i];
                if (!CanSlideVerticalHoleLocationText(placed))
                {
                    continue;
                }

                var defaultScore = ScoreTextBoundsAgainstPlaced(placed.TextBounds, placedDims, i, gap);
                var fitsInsideOwnLines = VerticalDimensionTextFitsInsideOwnLines(placed, textHeight);
                var hasHardOverlap = TextBoundsHasHardOverlap(placed.TextBounds, placedDims, i, gap);
                if (fitsInsideOwnLines && !hasHardOverlap)
                {
                    continue;
                }

                var candidates = GetVerticalTextSlideCandidates(placed, textHeight, gap)
                    .Select(c => new
                    {
                        Position = c.Position,
                        Bounds = c.Bounds,
                        Score = ScoreTextBoundsAgainstPlaced(c.Bounds, placedDims, i, gap)
                    })
                    .OrderBy(c => c.Score)
                    .ThenBy(c => Math.Abs(c.Position.Y - placed.DimLinePoint.Y))
                    .ToList();

                var best = candidates.FirstOrDefault();
                if (best == null)
                {
                    continue;
                }

                if (fitsInsideOwnLines && best.Score >= defaultScore)
                {
                    continue;
                }

                if (!fitsInsideOwnLines && best.Score > defaultScore)
                {
                    continue;
                }

                placed.HasCustomTextPosition = true;
                placed.TextPosition = best.Position;
                placed.TextBounds = best.Bounds;
                placedDims[i] = placed;
            }
        }

        private bool VerticalDimensionTextFitsInsideOwnLines(PlacedDim placed, double textHeight)
        {
            var arrow = GetVerticalInterval(placed.Dim);
            var text = GetDimensionText(placed.Dim);
            var textLength = Math.Max(text.Length, 1) * textHeight * 1.6;
            var innerClearance = Math.Max(_config.GeometryTolerance, textHeight * 0.5);
            return textLength + innerClearance * 2.0 <= arrow.B - arrow.A;
        }

        private bool CanSlideVerticalHoleLocationText(PlacedDim placed)
        {
            return (placed.Side == DimSide.Left || placed.Side == DimSide.Right)
                && placed.Dim.DimType == DimensionType.HoleLocation
                && IsShortVerticalDimension(placed.Dim);
        }

        private IEnumerable<(Point3d Position, TextBounds Bounds)> GetVerticalTextSlideCandidates(
            PlacedDim placed,
            double textHeight,
            double gap)
        {
            var textLength = GetDimensionTextLength(placed.Dim, textHeight);
            var arrow = GetVerticalInterval(placed.Dim);
            var lowerCenter = arrow.A - textLength / 2.0 - gap;
            var upperCenter = arrow.B + textLength / 2.0 + gap;
            var x = placed.DimLinePoint.X;

            yield return CreateVerticalCustomTextCandidate(x, lowerCenter, textLength, textHeight);
            yield return CreateVerticalCustomTextCandidate(x, upperCenter, textLength, textHeight);
        }

        private (Point3d Position, TextBounds Bounds) CreateVerticalCustomTextCandidate(
            double x,
            double centerY,
            double textLength,
            double textHeight)
        {
            var halfHeight = textHeight * 0.65;
            var position = new Point3d(x, centerY, 0.0);
            var bounds = new TextBounds
            {
                MinX = x - halfHeight,
                MaxX = x + halfHeight,
                MinY = centerY - textLength / 2.0,
                MaxY = centerY + textLength / 2.0
            };

            return (position, bounds);
        }

        private int ScoreTextBoundsAgainstPlaced(
            TextBounds candidate,
            IList<PlacedDim> placedDims,
            int selfIndex,
            double gap)
        {
            var score = 0;
            for (int i = 0; i < placedDims.Count; i++)
            {
                if (i == selfIndex)
                {
                    continue;
                }

                if (TextBoundsOverlap(candidate, placedDims[i].TextBounds, gap))
                {
                    score += 4;
                }

                if (TextBoundsAreTooClose(candidate, placedDims[i].TextBounds, gap))
                {
                    score += 2;
                }
            }

            foreach (var obstacle in _linearDimTextObstacles)
            {
                if (TextBoundsOverlap(candidate, obstacle, gap))
                {
                    score += 4;
                }

                if (TextBoundsAreTooClose(candidate, obstacle, gap))
                {
                    score += 2;
                }
            }

            return score;
        }

        private bool TextBoundsHasHardOverlap(
            TextBounds candidate,
            IList<PlacedDim> placedDims,
            int selfIndex,
            double gap)
        {
            for (int i = 0; i < placedDims.Count; i++)
            {
                if (i == selfIndex)
                {
                    continue;
                }

                if (TextBoundsOverlap(candidate, placedDims[i].TextBounds, gap))
                {
                    return true;
                }
            }

            return _linearDimTextObstacles.Any(obstacle => TextBoundsOverlap(candidate, obstacle, gap));
        }

        private bool TextBoundsOverlap(TextBounds first, TextBounds second, double gap)
        {
            return first.MinX <= second.MaxX + gap
                && second.MinX <= first.MaxX + gap
                && first.MinY <= second.MaxY + gap
                && second.MinY <= first.MaxY + gap;
        }

        private bool TextBoundsAreTooClose(TextBounds first, TextBounds second, double gap)
        {
            var yOverlap = first.MinY <= second.MaxY + gap && second.MinY <= first.MaxY + gap;
            if (!yOverlap)
            {
                return false;
            }

            double xGap;
            if (first.MaxX < second.MinX)
            {
                xGap = second.MinX - first.MaxX;
            }
            else if (second.MaxX < first.MinX)
            {
                xGap = first.MinX - second.MaxX;
            }
            else
            {
                xGap = 0.0;
            }

            return xGap <= Math.Max(gap, Scale(_config.TextHeight * 1.4));
        }
    }
}
