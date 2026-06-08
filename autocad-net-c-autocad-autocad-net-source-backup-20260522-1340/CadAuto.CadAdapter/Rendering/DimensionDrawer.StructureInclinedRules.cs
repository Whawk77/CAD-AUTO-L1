using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private List<Point2d> GetLeftExtensionCrossingPoints(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            var points = new List<Point2d>();
            foreach (var dim in candidates)
            {
                AddCrossingPoint(points, dim.XLine1, outline);
                AddCrossingPoint(points, dim.XLine2, outline);
                AddSideDirectionalInclinedEndpoint(points, dim.XLine1, outline, DimSide.Left);
                AddSideDirectionalInclinedEndpoint(points, dim.XLine2, outline, DimSide.Left);
            }

            return points;
        }

        private List<Point2d> GetRightExtensionCrossingPoints(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            var points = new List<Point2d>();
            foreach (var dim in candidates)
            {
                AddRightCrossingPoint(points, dim.XLine1, outline);
                AddRightCrossingPoint(points, dim.XLine2, outline);
                AddSideDirectionalInclinedEndpoint(points, dim.XLine1, outline, DimSide.Right);
                AddSideDirectionalInclinedEndpoint(points, dim.XLine2, outline, DimSide.Right);
            }

            return points;
        }

        private List<Point2d> GetTopExtensionCrossingPoints(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            var points = new List<Point2d>();
            foreach (var dim in candidates)
            {
                AddTopCrossingPoint(points, dim.XLine1, outline);
                AddTopCrossingPoint(points, dim.XLine2, outline);
                AddDirectionalInclinedEndpoint(points, dim.XLine1, outline, invertDirection: true);
                AddDirectionalInclinedEndpoint(points, dim.XLine2, outline, invertDirection: true);
            }

            return points;
        }

        private List<IgnoredPoint> GetTopExtensionIgnoredPoints(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            var points = new List<IgnoredPoint>();
            foreach (var dim in candidates)
            {
                AddTopCrossingIgnoredPoint(points, dim.XLine1, outline);
                AddTopCrossingIgnoredPoint(points, dim.XLine2, outline);
                AddDirectionalInclinedIgnoredPoint(points, dim.XLine1, outline, invertDirection: true);
                AddDirectionalInclinedIgnoredPoint(points, dim.XLine2, outline, invertDirection: true);
            }

            return points;
        }

        private List<Point2d> GetBottomExtensionCrossingPoints(IList<DeferredDim> candidates, OutlineFeature outline)
        {
            var points = new List<Point2d>();
            foreach (var dim in candidates)
            {
                AddBottomCrossingPoint(points, dim.XLine1, outline);
                AddBottomCrossingPoint(points, dim.XLine2, outline);
                AddDirectionalInclinedEndpoint(points, dim.XLine1, outline, invertDirection: false);
                AddDirectionalInclinedEndpoint(points, dim.XLine2, outline, invertDirection: false);
            }

            return points;
        }

        private void AddCrossingPoint(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (LeftExtensionCrossesOutline(featurePoint, outline)
                && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private void AddRightCrossingPoint(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (RightExtensionCrossesOutline(featurePoint, outline)
                && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private void AddTopCrossingPoint(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (TopExtensionCrossesOutline(featurePoint, outline)
                && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private void AddTopCrossingIgnoredPoint(IList<IgnoredPoint> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (TopExtensionCrossesOutline(featurePoint, outline)
                && !ContainsIgnoredPoint(points, point))
            {
                points.Add(new IgnoredPoint
                {
                    Point = point,
                    Reason = "TopExtensionCrossesOutline"
                });
            }
        }

        private void AddBottomCrossingPoint(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (BottomExtensionCrossesOutline(featurePoint, outline)
                && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private void AddInclinedEdgeProjectionPoints(IList<Point2d> points, DeferredDim dim, OutlineFeature outline)
        {
            AddVerticalOutlinePointOnInclinedProjection(points, dim.XLine1, dim, outline);
            AddVerticalOutlinePointOnInclinedProjection(points, dim.XLine2, dim, outline);
        }

        private void AddRightInclinedEdgeProjectionPoints(IList<Point2d> points, DeferredDim dim, OutlineFeature outline)
        {
            AddVerticalOutlinePointOnInclinedProjection(points, dim.XLine1, dim, outline);
            AddVerticalOutlinePointOnInclinedProjection(points, dim.XLine2, dim, outline);
        }

        private void AddHorizontalInclinedEdgeProjectionPoints(
            IList<Point2d> points,
            Point3d featurePoint,
            DeferredDim dim,
            OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (ContainsPoint(points, point) || !IsPointOnVerticalOutlineSegment(point, outline))
            {
                return;
            }

            if (MatchesFortyFiveInclinedHorizontalProjectionAtPoint(dim, point, outline))
            {
                points.Add(point);
            }
        }

        private void AddTopChamferEndpointPoints(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (IsTopChamferEndpoint(point, outline) && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private void AddDirectionalInclinedEndpoint(
            IList<Point2d> points,
            Point3d featurePoint,
            OutlineFeature outline,
            bool invertDirection)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (ContainsPoint(points, point)
                // 保护全局左右外包络端点：如果斜边端点落在 outline.MinX / outline.MaxX，
                // 当前 Top/Bottom 斜边忽略流程不会把它加入 ignoredPoints。
                // 这会让靠近最左/最右边界的顶部斜边端点保留下来，避免误删外轮廓最大宽度端点。
                || IsEnvelopeSidePoint(point, outline)
                || !ShouldIgnoreDirectionalInclinedEndpoint(point, outline, invertDirection))
            {
                return;
            }

            points.Add(point);
        }

        private void AddDirectionalInclinedIgnoredPoint(
            IList<IgnoredPoint> points,
            Point3d featurePoint,
            OutlineFeature outline,
            bool invertDirection)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            var reason = GetDirectionalInclinedIgnoreReason(point, outline, invertDirection);
            if (ContainsIgnoredPoint(points, point)
                || IsEnvelopeSidePoint(point, outline)
                || string.IsNullOrEmpty(reason))
            {
                return;
            }

            points.Add(new IgnoredPoint
            {
                Point = point,
                Reason = reason
            });
        }

        private void AddSideInnerGrooveEndpoint(
            IList<Point2d> points,
            Point3d featurePoint,
            OutlineFeature outline,
            DimSide side)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (ContainsPoint(points, point)
                || IsEnvelopeHorizontalSidePoint(point, outline)
                || !ShouldIgnoreSideInnerGrooveEndpoint(point, outline, side))
            {
                return;
            }

            points.Add(point);
        }

        private void AddSideDirectionalInclinedEndpoint(
            IList<Point2d> points,
            Point3d featurePoint,
            OutlineFeature outline,
            DimSide side)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (ContainsPoint(points, point)
                || IsEnvelopeHorizontalSidePoint(point, outline)
                || !ShouldIgnoreSideDirectionalInclinedEndpoint(point, outline, side))
            {
                return;
            }

            points.Add(point);
        }

        private bool ShouldIgnoreSideDirectionalInclinedEndpoint(Point2d point, OutlineFeature outline, DimSide side)
        {
            if (outline == null)
            {
                return false;
            }

            return outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(IsIgnorableFortyFiveDegreeSegment)
                .Any(s =>
                    IsSideInnerGrooveChamferSegment(s, outline, side)
                        ? IsSideInnerGrooveIgnoredEndpoint(point, s, outline, side)
                        : IsSideDirectionalIgnoredEndpoint(point, s, side));
        }

        private bool IsSideDirectionalIgnoredEndpoint(Point2d point, OutlineSegment segment, DimSide side)
        {
            if (!PointsEqual(point, segment.Start) && !PointsEqual(point, segment.End))
            {
                return false;
            }

            var dx = segment.End.X - segment.Start.X;
            var dy = segment.End.Y - segment.Start.Y;
            if (Math.Abs(dx) <= _config.GeometryTolerance || Math.Abs(dy) <= _config.GeometryTolerance)
            {
                return false;
            }

            if (dx * dy > 0.0)
            {
                var ignoredPoint = side == DimSide.Left
                    ? (segment.Start.Y <= segment.End.Y ? segment.Start : segment.End)
                    : (segment.Start.Y >= segment.End.Y ? segment.Start : segment.End);
                return PointsEqual(point, ignoredPoint);
            }

            var negativeIgnoredPoint = side == DimSide.Left
                ? (segment.Start.Y >= segment.End.Y ? segment.Start : segment.End)
                : (segment.Start.Y <= segment.End.Y ? segment.Start : segment.End);
            return PointsEqual(point, negativeIgnoredPoint);
        }

        private bool ShouldIgnoreSideInnerGrooveEndpoint(Point2d point, OutlineFeature outline, DimSide side)
        {
            if (outline == null)
            {
                return false;
            }

            return outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(IsIgnorableFortyFiveDegreeSegment)
                .Where(s => IsSideInnerGrooveChamferSegment(s, outline, side))
                .Any(s => IsSideInnerGrooveIgnoredEndpoint(point, s, outline, side));
        }

        private bool ShouldIgnoreDirectionalInclinedEndpoint(Point2d point, OutlineFeature outline, bool invertDirection)
        {
            return !string.IsNullOrEmpty(GetDirectionalInclinedIgnoreReason(point, outline, invertDirection));
        }

        private string GetDirectionalInclinedIgnoreReason(Point2d point, OutlineFeature outline, bool invertDirection)
        {
            if (outline == null)
            {
                return string.Empty;
            }

            var segments = outline.Segments
                .Where(s => !s.IsHorizontal(_config.GeometryTolerance))
                .Where(s => !s.IsVertical(_config.GeometryTolerance))
                .Where(IsIgnorableFortyFiveDegreeSegment);

            if (invertDirection)
            {
                foreach (var segment in segments)
                {
                    if (IsInnerGrooveChamferSegment(segment, outline, isTopSide: true)
                        && IsInnerGrooveIgnoredEndpoint(point, segment, outline, isTopSide: true))
                    {
                        return "TopInnerGrooveSharedEndpoint";
                    }

                    if (IsDirectionalIgnoredEndpoint(point, segment, invertDirection: true))
                    {
                        return "TopDirectional45Endpoint";
                    }
                }

                return string.Empty;
            }

            foreach (var segment in segments)
            {
                if (IsInnerGrooveChamferSegment(segment, outline, isTopSide: false)
                    && IsInnerGrooveIgnoredEndpoint(point, segment, outline, isTopSide: false))
                {
                    return "BottomInnerGrooveSharedEndpoint";
                }

                if (IsDirectionalIgnoredEndpoint(point, segment, invertDirection: false))
                {
                    return "BottomDirectional45Endpoint";
                }
            }

            return string.Empty;
        }

        private bool IsInnerGrooveChamferSegment(OutlineSegment chamfer, OutlineFeature outline, bool isTopSide)
        {
            if (!IsFortyFiveDegreeSegment(chamfer))
            {
                return false;
            }

            var tolerance = _config.GeometryTolerance;
            var chamferTopY = Math.Max(chamfer.Start.Y, chamfer.End.Y);
            var chamferBottomY = Math.Min(chamfer.Start.Y, chamfer.End.Y);
            foreach (var horizontal in outline.Segments.Where(s => s.IsHorizontal(tolerance) && !s.IsArcChord))
            {
                if (Math.Abs(horizontal.MinY - outline.MaxY) <= tolerance
                    || Math.Abs(horizontal.MinY - outline.MinY) <= tolerance)
                {
                    continue;
                }

                if (isTopSide && horizontal.MinY >= chamferTopY - tolerance)
                {
                    continue;
                }

                if (!isTopSide && horizontal.MinY <= chamferBottomY + tolerance)
                {
                    continue;
                }

                if (SegmentTouchesPoint(horizontal, chamfer.Start)
                    && HorizontalOtherEndConnectsInnerGroove(horizontal, chamfer.Start, chamfer, outline))
                {
                    return true;
                }

                if (SegmentTouchesPoint(horizontal, chamfer.End)
                    && HorizontalOtherEndConnectsInnerGroove(horizontal, chamfer.End, chamfer, outline))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSideInnerGrooveChamferSegment(OutlineSegment chamfer, OutlineFeature outline, DimSide side)
        {
            if (!IsFortyFiveDegreeSegment(chamfer))
            {
                return false;
            }

            var tolerance = _config.GeometryTolerance;
            var chamferLeftX = Math.Min(chamfer.Start.X, chamfer.End.X);
            var chamferRightX = Math.Max(chamfer.Start.X, chamfer.End.X);
            foreach (var vertical in outline.Segments.Where(s => s.IsVertical(tolerance) && !s.IsArcChord))
            {
                if (Math.Abs(vertical.MinX - outline.MinX) <= tolerance
                    || Math.Abs(vertical.MinX - outline.MaxX) <= tolerance)
                {
                    continue;
                }

                if (side == DimSide.Left && vertical.MinX <= chamferLeftX + tolerance)
                {
                    continue;
                }

                if (side == DimSide.Right && vertical.MinX >= chamferRightX - tolerance)
                {
                    continue;
                }

                if (SegmentTouchesPoint(vertical, chamfer.Start)
                    && IsInternalSideGrooveVertical(vertical, chamfer, side, outline)
                    && VerticalOtherEndConnectsChamfer(vertical, chamfer.Start, chamfer, outline))
                {
                    return true;
                }

                if (SegmentTouchesPoint(vertical, chamfer.End)
                    && IsInternalSideGrooveVertical(vertical, chamfer, side, outline)
                    && VerticalOtherEndConnectsChamfer(vertical, chamfer.End, chamfer, outline))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsInnerGrooveIgnoredEndpoint(Point2d point, OutlineSegment chamfer, OutlineFeature outline, bool isTopSide)
        {
            var sharedPoint = GetInnerGrooveHorizontalSharedPoint(chamfer, outline, isTopSide);
            return sharedPoint.HasValue
                && PointsEqual(point, sharedPoint.Value)
                && IsDirectionalIgnoredEndpoint(point, chamfer, invertDirection: isTopSide);
        }

        private Point2d? GetInnerGrooveHorizontalSharedPoint(OutlineSegment chamfer, OutlineFeature outline, bool isTopSide)
        {
            var tolerance = _config.GeometryTolerance;
            var chamferTopY = Math.Max(chamfer.Start.Y, chamfer.End.Y);
            var chamferBottomY = Math.Min(chamfer.Start.Y, chamfer.End.Y);
            foreach (var horizontal in outline.Segments.Where(s => s.IsHorizontal(tolerance) && !s.IsArcChord))
            {
                if (Math.Abs(horizontal.MinY - outline.MaxY) <= tolerance
                    || Math.Abs(horizontal.MinY - outline.MinY) <= tolerance
                    || (isTopSide && horizontal.MinY >= chamferTopY - tolerance)
                    || (!isTopSide && horizontal.MinY <= chamferBottomY + tolerance))
                {
                    continue;
                }

                if (SegmentTouchesPoint(horizontal, chamfer.Start)
                    && HorizontalOtherEndConnectsInnerGroove(horizontal, chamfer.Start, chamfer, outline))
                {
                    return chamfer.Start;
                }

                if (SegmentTouchesPoint(horizontal, chamfer.End)
                    && HorizontalOtherEndConnectsInnerGroove(horizontal, chamfer.End, chamfer, outline))
                {
                    return chamfer.End;
                }
            }

            return null;
        }

        private bool IsSideInnerGrooveIgnoredEndpoint(Point2d point, OutlineSegment chamfer, OutlineFeature outline, DimSide side)
        {
            var sharedPoint = GetSideInnerGrooveVerticalSharedPoint(chamfer, outline, side);
            return sharedPoint.HasValue && PointsEqual(point, sharedPoint.Value);
        }

        private Point2d? GetSideInnerGrooveVerticalSharedPoint(OutlineSegment chamfer, OutlineFeature outline, DimSide side)
        {
            var tolerance = _config.GeometryTolerance;
            var chamferLeftX = Math.Min(chamfer.Start.X, chamfer.End.X);
            var chamferRightX = Math.Max(chamfer.Start.X, chamfer.End.X);
            foreach (var vertical in outline.Segments.Where(s => s.IsVertical(tolerance) && !s.IsArcChord))
            {
                if (Math.Abs(vertical.MinX - outline.MinX) <= tolerance
                    || Math.Abs(vertical.MinX - outline.MaxX) <= tolerance
                    || (side == DimSide.Left && vertical.MinX <= chamferLeftX + tolerance)
                    || (side == DimSide.Right && vertical.MinX >= chamferRightX - tolerance))
                {
                    continue;
                }

                if (SegmentTouchesPoint(vertical, chamfer.Start)
                    && IsInternalSideGrooveVertical(vertical, chamfer, side, outline)
                    && VerticalOtherEndConnectsChamfer(vertical, chamfer.Start, chamfer, outline))
                {
                    return chamfer.Start;
                }

                if (SegmentTouchesPoint(vertical, chamfer.End)
                    && IsInternalSideGrooveVertical(vertical, chamfer, side, outline)
                    && VerticalOtherEndConnectsChamfer(vertical, chamfer.End, chamfer, outline))
                {
                    return chamfer.End;
                }
            }

            return null;
        }

        private bool IsInternalSideGrooveVertical(
            OutlineSegment vertical,
            OutlineSegment chamfer,
            DimSide side,
            OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            if (!vertical.IsVertical(tolerance)
                || Math.Abs(vertical.MinX - outline.MinX) <= tolerance
                || Math.Abs(vertical.MinX - outline.MaxX) <= tolerance)
            {
                return false;
            }

            var chamferLeftX = Math.Min(chamfer.Start.X, chamfer.End.X);
            var chamferRightX = Math.Max(chamfer.Start.X, chamfer.End.X);
            if (side == DimSide.Left)
            {
                return vertical.MinX > chamferLeftX + tolerance;
            }

            if (side == DimSide.Right)
            {
                return vertical.MinX < chamferRightX - tolerance;
            }

            return false;
        }

        private bool IsKnownChamferSegment(OutlineSegment segment, OutlineFeature outline)
        {
            return outline.Chamfers.Any(chamfer =>
                IsFortyFiveDegreeChamfer(chamfer)
                && ((PointsEqual(chamfer.StartPoint, segment.Start) && PointsEqual(chamfer.EndPoint, segment.End))
                    || (PointsEqual(chamfer.StartPoint, segment.End) && PointsEqual(chamfer.EndPoint, segment.Start))));
        }

        private bool HorizontalOtherEndConnectsInnerGroove(
            OutlineSegment horizontal,
            Point2d sharedPoint,
            OutlineSegment currentChamfer,
            OutlineFeature outline)
        {
            var otherEnd = PointsEqual(horizontal.Start, sharedPoint) ? horizontal.End : horizontal.Start;
            return outline.Segments.Any(segment =>
                    !ReferenceEquals(segment, currentChamfer)
                    && !segment.IsHorizontal(_config.GeometryTolerance)
                    && !segment.IsVertical(_config.GeometryTolerance)
                    && IsFortyFiveDegreeSegment(segment)
                    && (PointsEqual(segment.Start, otherEnd) || PointsEqual(segment.End, otherEnd)))
                || outline.Chamfers.Any(chamfer =>
                    IsFortyFiveDegreeChamfer(chamfer)
                    && (PointsEqual(chamfer.StartPoint, otherEnd) || PointsEqual(chamfer.EndPoint, otherEnd)))
                || outline.Fillets.Any(fillet =>
                    PointsEqual(fillet.StartPoint, otherEnd) || PointsEqual(fillet.EndPoint, otherEnd));
        }

        private bool VerticalOtherEndConnectsInnerGroove(
            OutlineSegment vertical,
            Point2d sharedPoint,
            OutlineSegment currentChamfer,
            OutlineFeature outline)
        {
            var otherEnd = PointsEqual(vertical.Start, sharedPoint) ? vertical.End : vertical.Start;
            return outline.Segments.Any(segment =>
                    !ReferenceEquals(segment, currentChamfer)
                    && !segment.IsHorizontal(_config.GeometryTolerance)
                    && !segment.IsVertical(_config.GeometryTolerance)
                    && IsFortyFiveDegreeSegment(segment)
                    && (PointsEqual(segment.Start, otherEnd) || PointsEqual(segment.End, otherEnd)))
                || outline.Chamfers.Any(chamfer =>
                    IsFortyFiveDegreeChamfer(chamfer)
                    && (PointsEqual(chamfer.StartPoint, otherEnd) || PointsEqual(chamfer.EndPoint, otherEnd)))
                || outline.Fillets.Any(fillet =>
                    PointsEqual(fillet.StartPoint, otherEnd) || PointsEqual(fillet.EndPoint, otherEnd));
        }

        private bool VerticalOtherEndConnectsChamfer(
            OutlineSegment vertical,
            Point2d sharedPoint,
            OutlineSegment currentChamfer,
            OutlineFeature outline)
        {
            var otherEnd = PointsEqual(vertical.Start, sharedPoint) ? vertical.End : vertical.Start;
            return outline.Segments.Any(segment =>
                    !ReferenceEquals(segment, currentChamfer)
                    && !segment.IsHorizontal(_config.GeometryTolerance)
                    && !segment.IsVertical(_config.GeometryTolerance)
                    && IsFortyFiveDegreeSegment(segment)
                    && (PointsEqual(segment.Start, otherEnd) || PointsEqual(segment.End, otherEnd)))
                || outline.Chamfers.Any(chamfer =>
                    IsFortyFiveDegreeChamfer(chamfer)
                    && (PointsEqual(chamfer.StartPoint, otherEnd) || PointsEqual(chamfer.EndPoint, otherEnd)))
                || outline.Fillets.Any(fillet =>
                    PointsEqual(fillet.StartPoint, otherEnd) || PointsEqual(fillet.EndPoint, otherEnd));
        }

        private bool IsDirectionalIgnoredEndpoint(Point2d point, OutlineSegment segment, bool invertDirection)
        {
            if (!PointsEqual(point, segment.Start) && !PointsEqual(point, segment.End))
            {
                return false;
            }

            var dx = segment.End.X - segment.Start.X;
            var dy = segment.End.Y - segment.Start.Y;
            if (Math.Abs(dx) <= _config.GeometryTolerance || Math.Abs(dy) <= _config.GeometryTolerance)
            {
                return false;
            }

            if (dx * dy > 0.0)
            {
                var ignoredPoint = invertDirection
                    ? (segment.Start.X >= segment.End.X ? segment.Start : segment.End)
                    : (segment.Start.X <= segment.End.X ? segment.Start : segment.End);
                return PointsEqual(point, ignoredPoint);
            }

            var negativeIgnoredPoint = invertDirection
                ? (segment.Start.X <= segment.End.X ? segment.Start : segment.End)
                : (segment.Start.X >= segment.End.X ? segment.Start : segment.End);
            return PointsEqual(point, negativeIgnoredPoint);
        }

        private bool IsEnvelopeSidePoint(Point2d point, OutlineFeature outline)
        {
            if (outline == null)
            {
                return false;
            }

            // 只按 X 判断是否在全局左右外包络边界上。
            // 注意：这不是判断点是否在顶部/底部边界；Top/Bottom 的斜边端点如果刚好在 MaxX，
            // 也会被视为外包络端点，从而跳过 ignoredPoints。
            return point.X <= outline.MinX + _config.GeometryTolerance
                || point.X >= outline.MaxX - _config.GeometryTolerance;
        }

        private bool IsEnvelopeHorizontalSidePoint(Point2d point, OutlineFeature outline)
        {
            if (outline == null)
            {
                return false;
            }

            return point.Y <= outline.MinY + _config.GeometryTolerance
                || point.Y >= outline.MaxY - _config.GeometryTolerance;
        }

        private bool IsTopChamferEndpoint(Point2d point, OutlineFeature outline)
        {
            if (outline == null || outline.Chamfers.Count == 0)
            {
                return false;
            }

            var topBandDepth = Math.Max(outline.Height * 0.25, Scale(_config.ArrowSize * 6.0));
            if (point.Y < outline.MaxY - topBandDepth - _config.GeometryTolerance)
            {
                return false;
            }

            if (point.X <= outline.MinX + _config.GeometryTolerance
                || point.X >= outline.MaxX - _config.GeometryTolerance)
            {
                return false;
            }

            return outline.Chamfers.Any(chamfer =>
                (PointsEqual(chamfer.StartPoint, point) || PointsEqual(chamfer.EndPoint, point))
                && IsFortyFiveDegreeChamfer(chamfer));
        }

        private bool IsFortyFiveDegreeChamfer(ChamferFeature chamfer)
        {
            var dx = Math.Abs(chamfer.StartPoint.X - chamfer.EndPoint.X);
            var dy = Math.Abs(chamfer.StartPoint.Y - chamfer.EndPoint.Y);
            var tolerance = Math.Max(_config.GeometryTolerance, Math.Max(dx, dy) * 0.08);
            return dx > _config.GeometryTolerance
                && dy > _config.GeometryTolerance
                && Math.Abs(dx - dy) <= tolerance;
        }

        private void AddVerticalOutlinePointOnInclinedProjection(
            IList<Point2d> points,
            Point3d featurePoint,
            DeferredDim dim,
            OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (ContainsPoint(points, point) || !IsPointOnVerticalOutlineSegment(point, outline))
            {
                return;
            }

            if (MatchesFortyFiveInclinedVerticalProjectionAtPoint(dim, point, outline))
            {
                points.Add(point);
            }
        }

        private bool MatchesFortyFiveInclinedVerticalProjectionAtPoint(DeferredDim dim, Point2d point, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var dimMinY = Math.Min(dim.XLine1.Y, dim.XLine2.Y);
            var dimMaxY = Math.Max(dim.XLine1.Y, dim.XLine2.Y);

            return outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Any(s => Math.Abs(s.MinY - dimMinY) <= tolerance
                    && Math.Abs(s.MaxY - dimMaxY) <= tolerance
                    && IsPointXWithinSegmentXRange(point, s));
        }

        private bool IsFortyFiveDegreeSegment(OutlineSegment segment)
        {
            var dx = Math.Abs(segment.Start.X - segment.End.X);
            var dy = Math.Abs(segment.Start.Y - segment.End.Y);
            var tolerance = Math.Max(_config.GeometryTolerance, Math.Max(dx, dy) * 0.08);
            return dx > _config.GeometryTolerance
                && dy > _config.GeometryTolerance
                && Math.Abs(dx - dy) <= tolerance;
        }

        private bool IsIgnorableFortyFiveDegreeSegment(OutlineSegment segment)
        {
            var dx = Math.Abs(segment.Start.X - segment.End.X);
            var dy = Math.Abs(segment.Start.Y - segment.End.Y);
            var tolerance = Math.Max(_config.GeometryTolerance, Math.Max(dx, dy) * 0.02);
            return dx > _config.GeometryTolerance
                && dy > _config.GeometryTolerance
                && Math.Abs(dx - dy) <= tolerance;
        }

        private bool IsPointXWithinSegmentXRange(Point2d point, OutlineSegment segment)
        {
            var tolerance = _config.GeometryTolerance;
            var minX = Math.Min(segment.Start.X, segment.End.X);
            var maxX = Math.Max(segment.Start.X, segment.End.X);
            return point.X >= minX - tolerance && point.X <= maxX + tolerance;
        }

        private bool IsPointOnSegment(Point2d point, OutlineSegment segment)
        {
            var tolerance = Math.Max(_config.GeometryTolerance, 0.2);
            var minX = Math.Min(segment.Start.X, segment.End.X) - tolerance;
            var maxX = Math.Max(segment.Start.X, segment.End.X) + tolerance;
            var minY = Math.Min(segment.Start.Y, segment.End.Y) - tolerance;
            var maxY = Math.Max(segment.Start.Y, segment.End.Y) + tolerance;
            if (point.X < minX || point.X > maxX || point.Y < minY || point.Y > maxY)
            {
                return false;
            }

            var dx = segment.End.X - segment.Start.X;
            var dy = segment.End.Y - segment.Start.Y;
            var cross = Math.Abs((point.X - segment.Start.X) * dy - (point.Y - segment.Start.Y) * dx);
            var length = Math.Sqrt(dx * dx + dy * dy);
            return length <= tolerance || cross / length <= tolerance;
        }

        private bool MatchesFortyFiveInclinedHorizontalProjectionAtPoint(DeferredDim dim, Point2d point, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var dimMinX = Math.Min(dim.XLine1.X, dim.XLine2.X);
            var dimMaxX = Math.Max(dim.XLine1.X, dim.XLine2.X);

            return outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Any(s => Math.Abs(s.MinX - dimMinX) <= tolerance
                    && Math.Abs(s.MaxX - dimMaxX) <= tolerance
                    && IsPointXWithinSegmentXRange(point, s));
        }

        private void AddVerticalOutlinePoint(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (IsPointOnVerticalOutlineSegment(point, outline)
                && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private bool IsPointOnVerticalOutlineSegment(Point2d point, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Any(s => Math.Abs(s.MinX - point.X) <= tolerance
                    && point.Y >= s.MinY - tolerance
                    && point.Y <= s.MaxY + tolerance);
        }

        private void AddHorizontalOutlinePoint(IList<Point2d> points, Point3d featurePoint, OutlineFeature outline)
        {
            var point = new Point2d(featurePoint.X, featurePoint.Y);
            if (IsPointOnHorizontalOutlineSegment(point, outline)
                && !ContainsPoint(points, point))
            {
                points.Add(point);
            }
        }

        private bool IsPointOnHorizontalOutlineSegment(Point2d point, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Any(s => Math.Abs(s.MinY - point.Y) <= tolerance
                    && point.X >= s.MinX - tolerance
                    && point.X <= s.MaxX + tolerance);
        }
    }
}
