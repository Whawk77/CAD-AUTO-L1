using System;
using System.Linq;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        public void DrawStepOutlineDimensions(OutlineFeature outline)
        {
            if (outline.Segments.Count == 0)
            {
                return;
            }

            if (ShouldGenerateDiagnosticSide(DimSide.Top))
            {
                DrawTopStepWidth(outline);
            }

            if (ShouldGenerateDiagnosticSide(DimSide.Bottom))
            {
                DrawLowerRightStepWidth(outline);
            }

            if (ShouldGenerateDiagnosticSide(DimSide.Left))
            {
                DrawRightStepHeight(outline);
            }

            if (ShouldGenerateDiagnosticSide(DimSide.Right))
            {
                DrawRightSideStepHeight(outline);
            }

            SuppressRightStructureHeightsDuplicatingOverallHeight();
            SuppressLeftStructureHeightsCoveredByRight();
        }

        private bool ShouldGenerateDiagnosticSide(DimSide side)
        {
            if (!_diagnosticsEnabled || _diagnosticSide == DiagnosticDimensionSide.All)
            {
                return true;
            }

            return MatchesDiagnosticSide(side);
        }

        private bool MatchesDiagnosticSide(DimSide side)
        {
            return (side == DimSide.Bottom && _diagnosticSide == DiagnosticDimensionSide.Bottom)
                || (side == DimSide.Top && _diagnosticSide == DiagnosticDimensionSide.Top)
                || (side == DimSide.Left && _diagnosticSide == DiagnosticDimensionSide.Left)
                || (side == DimSide.Right && _diagnosticSide == DiagnosticDimensionSide.Right);
        }

        private void SuppressRightStructureHeightsDuplicatingOverallHeight()
        {
            var overallHeights = _leftDims
                .Where(dim => dim.DimType == DimensionType.OverallHeight)
                .ToList();
            if (overallHeights.Count == 0 || _rightDims.Count == 0)
            {
                return;
            }

            for (int i = _rightDims.Count - 1; i >= 0; i--)
            {
                var right = _rightDims[i];
                if (!IsRightStructureHeight(right))
                {
                    continue;
                }

                if (overallHeights.Any(overall => IsSameVerticalInterval(right, overall)))
                {
                    _rightDims.RemoveAt(i);
                }
            }
        }

        private void SuppressLeftStructureHeightsCoveredByRight()
        {
            if (_leftDims.Count == 0 || _rightDims.Count == 0)
            {
                return;
            }

            var rightStructureHeights = _rightDims
                .Where(IsRightStructureHeight)
                .ToList();
            if (rightStructureHeights.Count == 0)
            {
                return;
            }

            for (int i = _leftDims.Count - 1; i >= 0; i--)
            {
                var left = _leftDims[i];
                if (!IsLeftStructureHeight(left))
                {
                    continue;
                }

                if (rightStructureHeights.Any(right => IsVerticalIntervalCoveredByRightStructure(left, right)))
                {
                    _leftDims.RemoveAt(i);
                }
            }
        }

        private static bool IsLeftStructureHeight(DeferredDim dim)
        {
            return dim.DimType == DimensionType.Normal
                && !dim.PreferLocalBoundary
                && string.Equals(dim.DebugRole, "LeftStructHeight", StringComparison.Ordinal);
        }

        private static bool IsRightStructureHeight(DeferredDim dim)
        {
            return dim.DimType == DimensionType.Normal
                && !dim.PreferLocalBoundary
                && string.Equals(dim.DebugRole, "RightStructHeight", StringComparison.Ordinal);
        }

        private bool IsVerticalIntervalCoveredByRightStructure(DeferredDim left, DeferredDim right)
        {
            var leftMin = Math.Min(left.XLine1.Y, left.XLine2.Y);
            var leftMax = Math.Max(left.XLine1.Y, left.XLine2.Y);
            var rightMin = Math.Min(right.XLine1.Y, right.XLine2.Y);
            var rightMax = Math.Max(right.XLine1.Y, right.XLine2.Y);
            var tolerance = _config.GeometryTolerance;

            var sharesEndpoint = Math.Abs(leftMin - rightMin) <= tolerance
                || Math.Abs(leftMin - rightMax) <= tolerance
                || Math.Abs(leftMax - rightMin) <= tolerance
                || Math.Abs(leftMax - rightMax) <= tolerance;
            if (!sharesEndpoint)
            {
                return false;
            }

            return leftMin >= rightMin - tolerance
                && leftMax <= rightMax + tolerance
                && right.Span >= left.Span - tolerance;
        }

        private bool IsSameVerticalInterval(DeferredDim a, DeferredDim b)
        {
            return Math.Abs(Math.Min(a.XLine1.Y, a.XLine2.Y) - Math.Min(b.XLine1.Y, b.XLine2.Y)) <= _config.GeometryTolerance
                && Math.Abs(Math.Max(a.XLine1.Y, a.XLine2.Y) - Math.Max(b.XLine1.Y, b.XLine2.Y)) <= _config.GeometryTolerance
                && Math.Abs(a.Span - b.Span) <= _config.GeometryTolerance;
        }
    }
}
