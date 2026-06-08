using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private bool AddIgnoredPoints(IList<Point2d> ignoredPoints, IEnumerable<Point2d> crossingPoints)
        {
            var added = false;
            foreach (var point in crossingPoints)
            {
                if (!ContainsPoint(ignoredPoints, point))
                {
                    ignoredPoints.Add(point);
                    added = true;
                }
            }

            return added;
        }

        private bool AddIgnoredPointMetadata(IList<IgnoredPoint> ignoredPoints, IEnumerable<IgnoredPoint> crossingPoints)
        {
            var added = false;
            foreach (var point in crossingPoints)
            {
                if (!ContainsIgnoredPoint(ignoredPoints, point.Point))
                {
                    ignoredPoints.Add(point);
                    added = true;
                }
            }

            return added;
        }

        private bool ContainsPoint(IEnumerable<Point2d> points, Point2d point)
        {
            return points.Any(p => PointsEqual(p, point));
        }

        private bool ContainsStructurePoint(IEnumerable<StructurePoint> points, Point2d point)
        {
            return points.Any(p => PointsEqual(p.Point, point));
        }

        private bool ContainsIgnoredPoint(IEnumerable<IgnoredPoint> points, Point2d point)
        {
            return points.Any(p => PointsEqual(p.Point, point));
        }

        private bool LeftExtensionCrossesOutline(Point3d featurePoint, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var dimX = outline.MinX - Scale(_config.FirstDimOffset);
            var maxX = featurePoint.X - tolerance;
            if (maxX <= dimX + tolerance)
            {
                return false;
            }

            foreach (var segment in outline.Segments)
            {
                if (TryGetHorizontalIntersectionX(segment, featurePoint.Y, out var x)
                    && x >= dimX - tolerance
                    && x <= maxX)
                {
                    return true;
                }

                if (HorizontalExtensionOverlapsOutlineSegment(segment, featurePoint, dimX, maxX))
                {
                    return true;
                }
            }

            return false;
        }

        private bool RightExtensionCrossesOutline(Point3d featurePoint, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var minX = featurePoint.X + tolerance;
            var dimX = outline.MaxX + Scale(_config.FirstDimOffset);
            if (dimX <= minX + tolerance)
            {
                return false;
            }

            foreach (var segment in outline.Segments)
            {
                if (TryGetHorizontalIntersectionX(segment, featurePoint.Y, out var x)
                    && x >= minX
                    && x <= dimX + tolerance)
                {
                    return true;
                }

                if (HorizontalExtensionOverlapsOutlineSegment(segment, featurePoint, minX, dimX))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TopExtensionCrossesOutline(Point3d featurePoint, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var minY = featurePoint.Y + tolerance;
            var dimY = outline.MaxY + Scale(_config.FirstDimOffset);
            if (dimY <= minY + tolerance)
            {
                return false;
            }

            foreach (var segment in outline.Segments)
            {
                if (TryGetVerticalIntersectionY(segment, featurePoint.X, out var y)
                    && y >= minY
                    && y <= dimY + tolerance)
                {
                    return true;
                }

                if (VerticalExtensionOverlapsOutlineSegment(segment, featurePoint, minY, dimY))
                {
                    return true;
                }
            }

            return false;
        }

        private bool BottomExtensionCrossesOutline(Point3d featurePoint, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var dimY = outline.MinY - Scale(_config.FirstDimOffset);
            var maxY = featurePoint.Y - tolerance;
            if (maxY <= dimY + tolerance)
            {
                return false;
            }

            foreach (var segment in outline.Segments)
            {
                if (TryGetVerticalIntersectionY(segment, featurePoint.X, out var y)
                    && y >= dimY - tolerance
                    && y <= maxY)
                {
                    return true;
                }

                if (VerticalExtensionOverlapsOutlineSegment(segment, featurePoint, dimY, maxY))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetHorizontalIntersectionX(OutlineSegment segment, double y, out double x)
        {
            var tolerance = _config.GeometryTolerance;
            x = 0.0;
            if (Math.Abs(segment.Start.Y - segment.End.Y) <= tolerance)
            {
                return false;
            }

            var minY = segment.MinY;
            var maxY = segment.MaxY;
            if (y < minY - tolerance || y > maxY + tolerance)
            {
                return false;
            }

            x = segment.Start.X + (y - segment.Start.Y) * (segment.End.X - segment.Start.X) / (segment.End.Y - segment.Start.Y);
            return true;
        }

        private bool TryGetVerticalIntersectionY(OutlineSegment segment, double x, out double y)
        {
            var tolerance = _config.GeometryTolerance;
            y = 0.0;
            if (Math.Abs(segment.Start.X - segment.End.X) <= tolerance)
            {
                return false;
            }

            var minX = segment.MinX;
            var maxX = segment.MaxX;
            if (x < minX - tolerance || x > maxX + tolerance)
            {
                return false;
            }

            y = segment.Start.Y + (x - segment.Start.X) * (segment.End.Y - segment.Start.Y) / (segment.End.X - segment.Start.X);
            return true;
        }

        private bool HorizontalExtensionOverlapsOutlineSegment(OutlineSegment segment, Point3d featurePoint, double minX, double maxX)
        {
            var tolerance = _config.GeometryTolerance;
            if (!segment.IsHorizontal(tolerance))
            {
                return false;
            }

            if (Math.Abs(segment.MinY - featurePoint.Y) > tolerance)
            {
                return false;
            }

            var overlapStart = Math.Max(segment.MinX, minX);
            var overlapEnd = Math.Min(segment.MaxX, maxX);
            if (overlapEnd <= overlapStart + tolerance)
            {
                return false;
            }

            return !PointLiesOnSegmentHorizontalExtent(segment, featurePoint.X);
        }

        private bool PointLiesOnSegmentHorizontalExtent(OutlineSegment segment, double x)
        {
            var tolerance = _config.GeometryTolerance;
            return x >= segment.MinX - tolerance && x <= segment.MaxX + tolerance;
        }

        private bool VerticalExtensionOverlapsOutlineSegment(OutlineSegment segment, Point3d featurePoint, double minY, double maxY)
        {
            var tolerance = _config.GeometryTolerance;
            if (!segment.IsVertical(tolerance))
            {
                return false;
            }

            if (Math.Abs(segment.MinX - featurePoint.X) > tolerance)
            {
                return false;
            }

            var overlapStart = Math.Max(segment.MinY, minY);
            var overlapEnd = Math.Min(segment.MaxY, maxY);
            if (overlapEnd <= overlapStart + tolerance)
            {
                return false;
            }

            return !PointLiesOnSegmentVerticalExtent(segment, featurePoint.Y);
        }

        private bool PointLiesOnSegmentVerticalExtent(OutlineSegment segment, double y)
        {
            var tolerance = _config.GeometryTolerance;
            return y >= segment.MinY - tolerance && y <= segment.MaxY + tolerance;
        }

        private void RemoveLongestExtensionCandidate(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            if (candidates.Count <= 1)
            {
                return;
            }

            var removal = candidates
                .Select((dim, index) => new
                {
                    Index = index,
                    ExtensionLength = Math.Max(dim.XLine1.X, dim.XLine2.X) - outline.MinX,
                    Span = dim.Span
                })
                .OrderByDescending(x => x.ExtensionLength)
                .ThenByDescending(x => x.Span)
                .First();

            candidates.RemoveAt(removal.Index);
        }

        private void RemoveLongestRightExtensionCandidate(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            if (candidates.Count <= 1)
            {
                return;
            }

            var removal = candidates
                .Select((dim, index) => new
                {
                    Index = index,
                    ExtensionLength = outline.MaxX - Math.Min(dim.XLine1.X, dim.XLine2.X),
                    Span = dim.Span
                })
                .OrderByDescending(x => x.ExtensionLength)
                .ThenByDescending(x => x.Span)
                .First();

            candidates.RemoveAt(removal.Index);
        }

        private void RemoveLongestTopExtensionCandidate(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            if (candidates.Count <= 1)
            {
                return;
            }

            var removal = candidates
                .Select((dim, index) => new
                {
                    Index = index,
                    ExtensionLength = outline.MaxY - Math.Min(dim.XLine1.Y, dim.XLine2.Y),
                    Span = dim.Span
                })
                .OrderByDescending(x => x.ExtensionLength)
                .ThenByDescending(x => x.Span)
                .First();

            candidates.RemoveAt(removal.Index);
        }

        private void RemoveLongestBottomExtensionCandidate(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            if (candidates.Count <= 1)
            {
                return;
            }

            var removal = candidates
                .Select((dim, index) => new
                {
                    Index = index,
                    ExtensionLength = Math.Max(dim.XLine1.Y, dim.XLine2.Y) - outline.MinY,
                    Span = dim.Span
                })
                .OrderByDescending(x => x.ExtensionLength)
                .ThenByDescending(x => x.Span)
                .First();

            candidates.RemoveAt(removal.Index);
        }
    }
}
