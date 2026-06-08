using System;
using System.Linq;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        public void DrawOutlineDimensions(OutlineFeature outline)
        {
            AddBoundingOverallWidth(outline);
            AddBoundingOverallHeight(outline);
        }

        private void AddBoundingOverallWidth(OutlineFeature outline)
        {
            if (outline.Width <= _config.GeometryTolerance)
            {
                return;
            }

            var dim = new DeferredDim
            {
                Rotation = 0.0,
                XLine1 = GetLeftBoundaryPoint(outline),
                XLine2 = GetRightBoundaryPoint(outline),
                OverrideText = string.Empty,
                Span = outline.Width,
                DimType = DimensionType.OverallWidth,
                ForceOuterLevel = true
            };
            _bottomDims.Add(dim);
        }

        private void AddBoundingOverallHeight(OutlineFeature outline)
        {
            if (outline.Height <= _config.GeometryTolerance)
            {
                return;
            }

            var dim = new DeferredDim
            {
                Rotation = Math.PI / 2.0,
                XLine1 = GetBottomBoundaryPoint(outline),
                XLine2 = GetTopBoundaryPoint(outline),
                OverrideText = string.Empty,
                Span = outline.Height,
                DimType = DimensionType.OverallHeight,
                ForceOuterLevel = true
            };
            _leftDims.Add(dim);
        }

        private OutlineSegment FindOverallWidthSegment(OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => s.LengthX > tolerance)
                .Where(s => IsOuterHorizontalSegment(s, outline))
                .OrderByDescending(s => s.LengthX)
                .ThenByDescending(s => s.MaxY)
                .ThenBy(s => Math.Abs(s.MinX - outline.MinX) + Math.Abs(s.MaxX - outline.MaxX))
                .FirstOrDefault();
        }

        private OutlineSegment FindOverallHeightSegment(OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Where(s => s.LengthY > tolerance)
                .Where(s => IsOuterVerticalSegment(s, outline))
                .OrderByDescending(s => s.LengthY)
                .ThenBy(s => s.MinX)
                .ThenBy(s => Math.Abs(s.MinY - outline.MinY) + Math.Abs(s.MaxY - outline.MaxY))
                .FirstOrDefault();
        }
    }
}
