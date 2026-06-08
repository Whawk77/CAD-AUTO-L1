using System;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private bool TryGetLocalDimLineCoordinate(DeferredDim dim, DimSide side, OutlineFeature outline, double offset, out double coordinate)
        {
            coordinate = 0.0;
            double boundary;
            if (TryGetDimensionLocalBoundary(dim, side, outline, out boundary))
            {
                switch (side)
                {
                    case DimSide.Bottom:
                    case DimSide.Left:
                    {
                        coordinate = boundary - offset;
                        if (!DimensionLineEntersOutlineInterior(dim, side, coordinate, outline))
                        {
                            return true;
                        }
                        break;
                    }
                    case DimSide.Top:
                    case DimSide.Right:
                    {
                        coordinate = boundary + offset;
                        if (!DimensionLineEntersOutlineInterior(dim, side, coordinate, outline))
                        {
                            return true;
                        }
                        break;
                    }
                }
            }

            return false;
        }

        private bool TryGetDimensionLocalBoundary(DeferredDim dim, DimSide side, OutlineFeature outline, out double boundary)
        {
            boundary = 0.0;
            if (!CanUseLocalDimensionBoundary(dim))
            {
                return false;
            }

            if (dim.LooseChainId != 0)
            {
                return false;
            }

            return TryGetLocalHoleLocationBoundary(dim, side, outline, out boundary);
        }

        private bool DimensionLineEntersOutlineInterior(DeferredDim dim, DimSide side, double coordinate, OutlineFeature outline)
        {
            var isHorizontal = side == DimSide.Bottom || side == DimSide.Top;
            var min = isHorizontal
                ? Math.Min(dim.XLine1.X, dim.XLine2.X)
                : Math.Min(dim.XLine1.Y, dim.XLine2.Y);
            var max = isHorizontal
                ? Math.Max(dim.XLine1.X, dim.XLine2.X)
                : Math.Max(dim.XLine1.Y, dim.XLine2.Y);
            if (max - min <= _config.GeometryTolerance)
            {
                return false;
            }

            var samples = new[] { min, (min + max) / 2.0, max };
            foreach (var sample in samples)
            {
                var x = isHorizontal ? sample : coordinate;
                var y = isHorizontal ? coordinate : sample;
                if (IsPointInsideOutlineByRayCast(x, y, outline)
                    && !IsPointOnAnyOutlineSegment(new Point2d(x, y), outline))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPointOnAnyOutlineSegment(Point2d point, OutlineFeature outline)
        {
            return outline != null
                && outline.Segments.Any(segment => IsPointOnSegment(point, segment));
        }

        private bool TryGetLocalHoleLocationBoundary(DeferredDim dim, DimSide side, OutlineFeature outline, out double boundary)
        {
            boundary = 0.0;
            if (outline == null || outline.Segments.Count == 0)
            {
                return false;
            }

            var tolerance = Math.Max(_config.GeometryTolerance, 0.05);
            var midX = (dim.XLine1.X + dim.XLine2.X) / 2.0;
            var midY = (dim.XLine1.Y + dim.XLine2.Y) / 2.0;
            var minX = Math.Min(dim.XLine1.X, dim.XLine2.X);
            var maxX = Math.Max(dim.XLine1.X, dim.XLine2.X);
            var minY = Math.Min(dim.XLine1.Y, dim.XLine2.Y);
            var maxY = Math.Max(dim.XLine1.Y, dim.XLine2.Y);

            if (side == DimSide.Left || side == DimSide.Right)
            {
                var candidate = outline.Segments
                    .Where(s => s.IsVertical(tolerance))
                    .Where(s => s.LengthY > tolerance)
                    .Where(s => side == DimSide.Left ? s.MinX <= minX + tolerance : s.MinX >= maxX - tolerance)
                    .Select(s => new
                    {
                        Segment = s,
                        Coordinate = (s.Start.X + s.End.X) / 2.0,
                        Overlap = IntervalOverlap(minY, maxY, s.MinY, s.MaxY),
                        CoversMid = midY >= s.MinY - tolerance && midY <= s.MaxY + tolerance
                    })
                    .Where(c => c.Overlap > tolerance || c.CoversMid)
                    .OrderBy(c => Math.Abs(c.Coordinate - midX))
                    .ThenByDescending(c => c.Overlap)
                    .ThenByDescending(c => c.Segment.LengthY)
                    .FirstOrDefault();
                if (candidate == null)
                {
                    return false;
                }

                boundary = candidate.Coordinate;
                return true;
            }

            if (side == DimSide.Bottom || side == DimSide.Top)
            {
                var candidate = outline.Segments
                    .Where(s => s.IsHorizontal(tolerance))
                    .Where(s => s.LengthX > tolerance)
                    .Where(s => side == DimSide.Bottom ? s.MinY <= minY + tolerance : s.MinY >= maxY - tolerance)
                    .Select(s => new
                    {
                        Segment = s,
                        Coordinate = (s.Start.Y + s.End.Y) / 2.0,
                        Overlap = IntervalOverlap(minX, maxX, s.MinX, s.MaxX),
                        CoversMid = midX >= s.MinX - tolerance && midX <= s.MaxX + tolerance
                    })
                    .Where(c => c.Overlap > tolerance || c.CoversMid)
                    .OrderBy(c => Math.Abs(c.Coordinate - midY))
                    .ThenByDescending(c => c.Overlap)
                    .ThenByDescending(c => c.Segment.LengthX)
                    .FirstOrDefault();
                if (candidate == null)
                {
                    return false;
                }

                boundary = candidate.Coordinate;
                return true;
            }

            return false;
        }

        private static bool CanUseLocalDimensionBoundary(DeferredDim dim)
        {
            return dim.PreferLocalBoundary
                || dim.DimType == DimensionType.PinDistance
                || dim.DimType == DimensionType.PinGroupDistance;
        }

        private double IntervalOverlap(double firstMin, double firstMax, double secondMin, double secondMax)
        {
            return Math.Min(firstMax, secondMax) - Math.Max(firstMin, secondMin);
        }

        private TextBounds ComputePlacedTextBounds(DeferredDim dim, Point3d dimLinePoint, bool isHorizontal, double textHeight)
        {
            var interval = ComputeTextInterval(dim, isHorizontal, textHeight);
            var halfHeight = textHeight * 0.65;
            if (isHorizontal)
            {
                return new TextBounds
                {
                    MinX = interval.A,
                    MaxX = interval.B,
                    MinY = dimLinePoint.Y - halfHeight,
                    MaxY = dimLinePoint.Y + halfHeight
                };
            }

            return new TextBounds
            {
                MinX = dimLinePoint.X - halfHeight,
                MaxX = dimLinePoint.X + halfHeight,
                MinY = interval.A,
                MaxY = interval.B
            };
        }

        private (double A, double B) ComputeTextInterval(DeferredDim dim, bool isHorizontal, double textHeight)
        {
            double center, span;
            if (isHorizontal)
            {
                center = (dim.XLine1.X + dim.XLine2.X) / 2.0;
                span = Math.Abs(dim.XLine2.X - dim.XLine1.X);
            }
            else
            {
                center = (dim.XLine1.Y + dim.XLine2.Y) / 2.0;
                span = Math.Abs(dim.XLine2.Y - dim.XLine1.Y);
            }

            var text = string.IsNullOrEmpty(dim.OverrideText)
                ? _config.FormatNumber(span)
                : dim.OverrideText;
            var textWidth = Math.Max(text.Length, 2) * textHeight * 0.7;

            return (center - textWidth / 2.0, center + textWidth / 2.0);
        }

        private double GetDimensionTextLength(DeferredDim dim, double textHeight)
        {
            var text = GetDimensionText(dim);
            return Math.Max(text.Length, 2) * textHeight * 0.7;
        }

        private string GetDimensionText(DeferredDim dim)
        {
            return string.IsNullOrEmpty(dim.OverrideText)
                ? _config.FormatNumber(dim.Span)
                : dim.OverrideText;
        }

        private bool TextCoversOutline(DeferredDim dim, DimSide side, double offset, double textHeight, OutlineFeature outline, bool isHorizontal)
        {
            double localBoundary;
            if (TryGetDimensionLocalBoundary(dim, side, outline, out localBoundary)
                && !IsGlobalBoundaryCoordinate(localBoundary, side, outline))
            {
                var localCoordinate = (side == DimSide.Bottom || side == DimSide.Left)
                    ? localBoundary - offset
                    : localBoundary + offset;
                if (!DimensionLineEntersOutlineInterior(dim, side, localCoordinate, outline))
                {
                    return false;
                }
            }

            var textBox = ComputeTextInterval(dim, isHorizontal, textHeight);
            var halfHeight = textHeight / 2.0;

            switch (side)
            {
                case DimSide.Bottom:
                {
                    var textY = outline.MinY - offset;
                    if (textY + halfHeight < outline.MinY - _config.GeometryTolerance) return false;
                    return textBox.A < outline.MaxX + _config.GeometryTolerance
                        && textBox.B > outline.MinX - _config.GeometryTolerance;
                }
                case DimSide.Top:
                {
                    var textY = outline.MaxY + offset;
                    if (textY - halfHeight > outline.MaxY + _config.GeometryTolerance) return false;
                    return textBox.A < outline.MaxX + _config.GeometryTolerance
                        && textBox.B > outline.MinX - _config.GeometryTolerance;
                }
                case DimSide.Left:
                {
                    var textX = outline.MinX - offset;
                    if (textX + halfHeight < outline.MinX - _config.GeometryTolerance) return false;
                    return textBox.A < outline.MaxY + _config.GeometryTolerance
                        && textBox.B > outline.MinY - _config.GeometryTolerance;
                }
                case DimSide.Right:
                {
                    var textX = outline.MaxX + offset;
                    if (textX - halfHeight > outline.MaxX + _config.GeometryTolerance) return false;
                    return textBox.A < outline.MaxY + _config.GeometryTolerance
                        && textBox.B > outline.MinY - _config.GeometryTolerance;
                }
                default:
                    return false;
            }
        }

        private bool IsGlobalBoundaryCoordinate(double coordinate, DimSide side, OutlineFeature outline)
        {
            switch (side)
            {
                case DimSide.Bottom:
                    return Math.Abs(coordinate - outline.MinY) <= _config.GeometryTolerance;
                case DimSide.Top:
                    return Math.Abs(coordinate - outline.MaxY) <= _config.GeometryTolerance;
                case DimSide.Left:
                    return Math.Abs(coordinate - outline.MinX) <= _config.GeometryTolerance;
                case DimSide.Right:
                    return Math.Abs(coordinate - outline.MaxX) <= _config.GeometryTolerance;
                default:
                    return false;
            }
        }
    }
}
