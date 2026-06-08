using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private DimSide ChooseVerticalNormalDimensionSide(DeferredDim dim, DimSide preferredSide)
        {
            if (dim.DimType != DimensionType.HoleLocation)
            {
                return preferredSide;
            }

            if (preferredSide != DimSide.Left && preferredSide != DimSide.Right)
            {
                return preferredSide;
            }

            var oppositeSide = preferredSide == DimSide.Left ? DimSide.Right : DimSide.Left;
            var preferredDims = preferredSide == DimSide.Left ? _leftDims : _rightDims;
            var oppositeDims = oppositeSide == DimSide.Left ? _leftDims : _rightDims;
            var preferredScore = ScoreVerticalSideCrowding(dim, preferredDims);
            var oppositeScore = ScoreVerticalSideCrowding(dim, oppositeDims);
            var switchThreshold = IsShortVerticalDimension(dim) ? 1 : 3;
            if (preferredScore - oppositeScore >= switchThreshold)
            {
                return oppositeSide;
            }

            return preferredSide;
        }

        private int ScoreVerticalSideCrowding(DeferredDim candidate, IList<DeferredDim> existingDims)
        {
            var score = 0;
            var candidateArrow = GetVerticalInterval(candidate);
            var candidateText = EstimateVerticalTextInterval(candidate);
            var candidateCenter = (candidateArrow.A + candidateArrow.B) / 2.0;
            var shortCandidate = IsShortVerticalDimension(candidate);
            foreach (var existing in existingDims)
            {
                var existingArrow = GetVerticalInterval(existing);
                var existingText = EstimateVerticalTextInterval(existing);
                var existingCenter = (existingArrow.A + existingArrow.B) / 2.0;
                var obstacleWeight = existing.DimType == DimensionType.Normal || existing.DimType == DimensionType.HoleLocation ? 1 : 2;

                if (IntervalsOverlap(candidateArrow, existingArrow, _config.GeometryTolerance))
                {
                    score += (shortCandidate || IsShortVerticalDimension(existing) ? 2 : 1) * obstacleWeight;
                }

                if (IntervalsOverlap(candidateText, existingText, Scale(_config.TextHeight * 0.8)))
                {
                    score += (shortCandidate || IsShortVerticalDimension(existing) ? 4 : 2) * obstacleWeight;
                }

                if (Math.Abs(candidateCenter - existingCenter) <= Scale(_config.TextHeight * 2.5))
                {
                    score += (shortCandidate ? 2 : 1) * obstacleWeight;
                }
            }

            return score;
        }

        private (double A, double B) GetVerticalInterval(DeferredDim dim)
        {
            return (Math.Min(dim.XLine1.Y, dim.XLine2.Y), Math.Max(dim.XLine1.Y, dim.XLine2.Y));
        }

        private (double A, double B) EstimateVerticalTextInterval(DeferredDim dim)
        {
            var interval = GetVerticalInterval(dim);
            var center = (interval.A + interval.B) / 2.0;
            var text = string.IsNullOrEmpty(dim.OverrideText)
                ? _config.FormatNumber(dim.Span)
                : dim.OverrideText;
            var textLength = Math.Max(text.Length, 2) * Scale(_config.TextHeight) * 0.75;
            return (center - textLength / 2.0, center + textLength / 2.0);
        }

        private bool IsShortVerticalDimension(DeferredDim dim)
        {
            return dim.Span <= Scale(_config.TextHeight * 3.0);
        }

        private bool IntervalsOverlap((double A, double B) first, (double A, double B) second, double tolerance)
        {
            return first.A <= second.B + tolerance && second.A <= first.B + tolerance;
        }

        private void RebalanceVerticalHoleLocationSides()
        {
            RebalanceVerticalHoleLocationSide(_rightDims, _leftDims);
            RebalanceVerticalHoleLocationSide(_leftDims, _rightDims);
        }

        private void RebalanceVerticalHoleLocationSide(List<DeferredDim> sourceDims, List<DeferredDim> targetDims)
        {
            RebalanceVerticalLooseChains(sourceDims, targetDims);

            for (int i = sourceDims.Count - 1; i >= 0; i--)
            {
                var dim = sourceDims[i];
                if (!CanRebalanceVerticalHoleLocation(dim))
                {
                    continue;
                }

                var sourceWithoutCandidate = sourceDims
                    .Where((existing, index) => index != i)
                    .ToList();
                var sourceScore = ScoreVerticalSideCrowding(dim, sourceWithoutCandidate);
                var targetScore = ScoreVerticalSideCrowding(dim, targetDims);
                var threshold = IsShortVerticalDimension(dim) ? 1 : 3;
                if (sourceScore - targetScore < threshold)
                {
                    continue;
                }

                sourceDims.RemoveAt(i);
                targetDims.Add(dim);
            }
        }

        private void RebalanceVerticalLooseChains(List<DeferredDim> sourceDims, List<DeferredDim> targetDims)
        {
            var chainIds = sourceDims
                .Where(dim => dim.LooseChainId != 0 && dim.DimType == DimensionType.HoleLocation)
                .Select(dim => dim.LooseChainId)
                .Distinct()
                .ToList();

            foreach (var chainId in chainIds)
            {
                var chainDims = sourceDims.Where(dim => dim.LooseChainId == chainId).ToList();
                if (chainDims.Count == 0 || chainDims.Any(dim => !CanRebalanceVerticalLooseChainDimension(dim)))
                {
                    continue;
                }

                var sourceWithoutChain = sourceDims
                    .Where(dim => dim.LooseChainId != chainId)
                    .ToList();
                var sourceScore = chainDims.Sum(dim => ScoreVerticalSideCrowding(dim, sourceWithoutChain));
                var targetScore = chainDims.Sum(dim => ScoreVerticalSideCrowding(dim, targetDims));
                var threshold = Math.Max(2, chainDims.Count);
                if (sourceScore - targetScore < threshold)
                {
                    continue;
                }

                for (int i = sourceDims.Count - 1; i >= 0; i--)
                {
                    if (sourceDims[i].LooseChainId != chainId)
                    {
                        continue;
                    }

                    targetDims.Add(sourceDims[i]);
                    sourceDims.RemoveAt(i);
                }
            }
        }

        private bool CanRebalanceVerticalLooseChainDimension(DeferredDim dim)
        {
            return dim.DimType == DimensionType.HoleLocation
                && IsShortVerticalDimension(dim)
                && !dim.ForceOuterLevel;
        }

        private bool CanRebalanceVerticalHoleLocation(DeferredDim dim)
        {
            return dim.DimType == DimensionType.HoleLocation
                && dim.LooseChainId == 0
                && IsShortVerticalDimension(dim)
                && !dim.ForceOuterLevel;
        }
    }
}
