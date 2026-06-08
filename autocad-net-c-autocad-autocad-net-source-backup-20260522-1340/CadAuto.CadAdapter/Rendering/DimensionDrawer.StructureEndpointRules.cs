using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private bool IsTopSideHorizontalStructureCandidate(DeferredDim dim, OutlineFeature outline, IList<IgnoredPoint> ignoredPoints)
        {
            return IsCurrentTopSideStructurePoint(new Point2d(dim.XLine1.X, dim.XLine1.Y), outline, ignoredPoints)
                && IsCurrentTopSideStructurePoint(new Point2d(dim.XLine2.X, dim.XLine2.Y), outline, ignoredPoints);
        }

        private bool IsBottomSideHorizontalStructureCandidate(DeferredDim dim, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            return IsCurrentBottomSideStructurePoint(new Point2d(dim.XLine1.X, dim.XLine1.Y), outline, ignoredPoints)
                && IsCurrentBottomSideStructurePoint(new Point2d(dim.XLine2.X, dim.XLine2.Y), outline, ignoredPoints);
        }

        private bool IsCurrentTopSideStructurePoint(Point2d point, OutlineFeature outline, IList<IgnoredPoint> ignoredPoints)
        {
            if (IsTopInclinedEndpointStructurePoint(point, outline, ignoredPoints))
            {
                return true;
            }

            var tolerance = _config.GeometryTolerance;
            var levelSegments = outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthY > tolerance)
                .Where(s => Math.Abs(s.MinX - point.X) <= tolerance)
                .ToList();
            if (levelSegments.Count == 0)
            {
                return false;
            }

            var topMost = GetTopMostStructurePoint(levelSegments, ignoredPoints);
            return topMost.HasValue && PointsEqual(topMost.Value.Point, point);
        }

        private void AddTopInclinedEndpointStructurePoints(IList<StructurePoint> points, OutlineFeature outline, IList<IgnoredPoint> ignoredPoints)
        {
            var tolerance = _config.GeometryTolerance;
            foreach (var segment in outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsInnerGrooveChamferSegment(s, outline, isTopSide: true)))
            {
                AddTopInclinedEndpointStructurePoint(points, segment.Start, outline, ignoredPoints);
                AddTopInclinedEndpointStructurePoint(points, segment.End, outline, ignoredPoints);
            }
        }

        private void AddTopInclinedEndpointStructurePoint(
            IList<StructurePoint> points,
            Point2d point,
            OutlineFeature outline,
            IList<IgnoredPoint> ignoredPoints)
        {
            if (ContainsStructurePoint(points, point)
                || ContainsIgnoredPoint(ignoredPoints, point)
                || IsEnvelopeSidePoint(point, outline))
            {
                return;
            }

            points.Add(new StructurePoint
            {
                Point = point,
                Source = "TopInclinedEndpoint"
            });
        }

        private bool IsTopInclinedEndpointStructurePoint(Point2d point, OutlineFeature outline, IList<IgnoredPoint> ignoredPoints)
        {
            if (ContainsIgnoredPoint(ignoredPoints, point) || IsEnvelopeSidePoint(point, outline))
            {
                return false;
            }

            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsInnerGrooveChamferSegment(s, outline, isTopSide: true))
                .Any(s => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
        }

        private void AddTopSlopeEndpointStructurePoints(IList<StructurePoint> points, OutlineFeature outline, IList<IgnoredPoint> ignoredPoints)
        {
            var tolerance = _config.GeometryTolerance;
            foreach (var segment in outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(s => !s.IsArcChord))
            {
                var low = segment.Start.Y <= segment.End.Y ? segment.Start : segment.End;
                var high = PointsEqual(low, segment.Start) ? segment.End : segment.Start;
                if (!IsTopSlopeEndpointStructurePoint(low, high, segment, outline, ignoredPoints))
                {
                    continue;
                }

                AddTopSlopeEndpointStructurePoint(points, low, outline, ignoredPoints);
            }
        }

        private bool IsTopSlopeEndpointStructurePoint(
            Point2d low,
            Point2d high,
            OutlineSegment slope,
            OutlineFeature outline,
            IList<IgnoredPoint> ignoredPoints)
        {
            if (ContainsIgnoredPoint(ignoredPoints, low)
                || IsEnvelopeSidePoint(low, outline)
                || high.Y <= low.Y + _config.GeometryTolerance)
            {
                return false;
            }

            return EndpointConnectsHorizontalSegment(low, slope, outline)
                && EndpointConnectsHigherHorizontalSegment(high, low.Y, slope, outline);
        }

        private void AddTopSlopeEndpointStructurePoint(
            IList<StructurePoint> points,
            Point2d point,
            OutlineFeature outline,
            IList<IgnoredPoint> ignoredPoints)
        {
            if (ContainsStructurePoint(points, point)
                || ContainsIgnoredPoint(ignoredPoints, point)
                || IsEnvelopeSidePoint(point, outline))
            {
                return;
            }

            points.Add(new StructurePoint
            {
                Point = point,
                Source = "TopSlopeEndpoint"
            });
        }

        private bool EndpointConnectsHorizontalSegment(Point2d point, OutlineSegment source, OutlineFeature outline)
        {
            return outline.Segments.Any(segment =>
                !ReferenceEquals(segment, source)
                && segment.IsHorizontal(_config.GeometryTolerance)
                && !segment.IsArcChord
                && IsPointOnHorizontalSegment(point, segment));
        }

        private bool EndpointConnectsHigherHorizontalSegment(Point2d point, double referenceY, OutlineSegment source, OutlineFeature outline)
        {
            return outline.Segments.Any(segment =>
                !ReferenceEquals(segment, source)
                && segment.IsHorizontal(_config.GeometryTolerance)
                && !segment.IsArcChord
                && segment.MinY > referenceY + _config.GeometryTolerance
                && IsPointOnHorizontalSegment(point, segment));
        }

        private bool IsPointOnHorizontalSegment(Point2d point, OutlineSegment segment)
        {
            var tolerance = _config.GeometryTolerance;
            return segment.IsHorizontal(tolerance)
                && Math.Abs(segment.MinY - point.Y) <= tolerance
                && point.X >= segment.MinX - tolerance
                && point.X <= segment.MaxX + tolerance;
        }

        private bool IsCurrentBottomSideStructurePoint(Point2d point, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            if (IsBottomInclinedEndpointStructurePoint(point, outline, ignoredPoints))
            {
                return true;
            }

            var tolerance = _config.GeometryTolerance;
            var levelSegments = outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthY > tolerance)
                .Where(s => Math.Abs(s.MinX - point.X) <= tolerance)
                .ToList();
            if (levelSegments.Count == 0)
            {
                return false;
            }

            var bottomMost = GetBottomMostPoint(levelSegments, ignoredPoints);
            return bottomMost.HasValue && PointsEqual(bottomMost.Value, point);
        }

        private void AddBottomInclinedEndpointStructurePoints(IList<Point2d> points, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            var tolerance = _config.GeometryTolerance;
            foreach (var segment in outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsInnerGrooveChamferSegment(s, outline, isTopSide: false)))
            {
                AddBottomInclinedEndpointStructurePoint(points, segment.Start, outline, ignoredPoints);
                AddBottomInclinedEndpointStructurePoint(points, segment.End, outline, ignoredPoints);
            }
        }

        private void AddBottomInclinedEndpointStructurePoint(
            IList<Point2d> points,
            Point2d point,
            OutlineFeature outline,
            IList<Point2d> ignoredPoints)
        {
            if (ContainsPoint(points, point)
                || ContainsPoint(ignoredPoints, point)
                || IsEnvelopeSidePoint(point, outline))
            {
                return;
            }

            points.Add(point);
        }

        private bool IsBottomInclinedEndpointStructurePoint(Point2d point, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            if (ContainsPoint(ignoredPoints, point) || IsEnvelopeSidePoint(point, outline))
            {
                return false;
            }

            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsInnerGrooveChamferSegment(s, outline, isTopSide: false))
                .Any(s => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
        }
    }
}
