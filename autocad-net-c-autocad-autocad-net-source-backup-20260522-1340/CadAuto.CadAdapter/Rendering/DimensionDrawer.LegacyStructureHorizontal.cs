using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private void DrawTopStepWidth(OutlineFeature outline)
        {
            var ignoredPoints = new List<IgnoredPoint>();
            List<StructurePoint> structurePoints;
            List<DeferredDim> candidates;
            while (true)
            {
                structurePoints = BuildTopSideHorizontalStructurePoints(outline, ignoredPoints);
                var points = structurePoints
                    .Select(p => p.Point)
                    .ToList();
                if (points.Count < 2)
                {
                    return;
                }

                candidates = BuildHorizontalWidthCandidates(points, "TopStructWidth");
                var crossingPoints = GetTopExtensionIgnoredPoints(candidates, outline);
                if (crossingPoints.Count == 0)
                {
                    break;
                }

                if (!AddIgnoredPointMetadata(ignoredPoints, crossingPoints))
                {
                    return;
                }
            }

            if (_diagnosticsEnabled)
            {
                AddTopPointDebugLabels(structurePoints, ignoredPoints);
            }

            RemoveLongestTopExtensionCandidate(candidates, outline);
            foreach (var dim in candidates)
            {
                if (!IsTopSideHorizontalStructureCandidate(dim, outline, ignoredPoints))
                {
                    continue;
                }

                _topDims.Add(dim);
            }
        }

        private void DrawLowerRightStepWidth(OutlineFeature outline)
        {
            var ignoredPoints = new List<Point2d>();
            List<Point2d> points;
            List<DeferredDim> candidates;
            while (true)
            {
                points = BuildBottomSideHorizontalStructurePoints(outline, ignoredPoints);
                if (points.Count < 2)
                {
                    return;
                }

                candidates = BuildHorizontalWidthCandidates(points, "BottomStructWidth");
                var crossingPoints = GetBottomExtensionCrossingPoints(candidates, outline);
                if (crossingPoints.Count == 0)
                {
                    break;
                }

                if (!AddIgnoredPoints(ignoredPoints, crossingPoints))
                {
                    return;
                }
            }

            if (_diagnosticsEnabled && ShouldFlushDiagnosticSide(DimSide.Bottom))
            {
                AddDirectionalPointDebugLabels(points, ignoredPoints, "SP:VBM", "IG:UNK");
            }

            RemoveLongestBottomExtensionCandidate(candidates, outline);
            foreach (var dim in candidates)
            {
                if (!IsBottomSideHorizontalStructureCandidate(dim, outline, ignoredPoints))
                {
                    continue;
                }

                _bottomDims.Add(dim);
            }
        }

        private List<DeferredDim> BuildHorizontalWidthCandidates(IList<Point2d> points, string debugRole)
        {
            var candidates = new List<DeferredDim>();
            for (int i = 1; i < points.Count; i++)
            {
                var leftPoint = points[i - 1];
                var rightPoint = points[i];
                var span = Math.Abs(rightPoint.X - leftPoint.X);
                if (span <= _config.GeometryTolerance)
                {
                    continue;
                }

                candidates.Add(new DeferredDim
                {
                    Rotation = 0.0,
                    XLine1 = new Point3d(leftPoint.X, leftPoint.Y, 0.0),
                    XLine2 = new Point3d(rightPoint.X, rightPoint.Y, 0.0),
                    OverrideText = string.Empty,
                    Span = span,
                    DimType = DimensionType.Normal,
                    DebugRole = debugRole ?? string.Empty
                });
            }

            return candidates;
        }

        private List<StructurePoint> BuildTopSideHorizontalStructurePoints(OutlineFeature outline, IList<IgnoredPoint> ignoredPoints)
        {
            var tolerance = _config.GeometryTolerance;
            var groups = new List<List<OutlineSegment>>();
            foreach (var segment in outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthY > tolerance))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(g[0].MinX - segment.MinX) <= tolerance);
                if (group == null)
                {
                    group = new List<OutlineSegment>();
                    groups.Add(group);
                }

                group.Add(segment);
            }

            var points = groups
                .Select(g => GetTopMostStructurePoint(g, ignoredPoints))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            AddTopInclinedEndpointStructurePoints(points, outline, ignoredPoints);
            AddTopSlopeEndpointStructurePoints(points, outline, ignoredPoints);

            return points
                .OrderBy(p => p.Point.X)
                .ToList();
        }

        private List<Point2d> BuildBottomSideHorizontalStructurePoints(OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            var tolerance = _config.GeometryTolerance;
            var groups = new List<List<OutlineSegment>>();
            foreach (var segment in outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthY > tolerance))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(g[0].MinX - segment.MinX) <= tolerance);
                if (group == null)
                {
                    group = new List<OutlineSegment>();
                    groups.Add(group);
                }

                group.Add(segment);
            }

            var points = groups
                .Select(g => GetBottomMostPoint(g, ignoredPoints))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            AddBottomInclinedEndpointStructurePoints(points, outline, ignoredPoints);

            return points
                .OrderBy(p => p.X)
                .ToList();
        }

        private Point2d? GetTopMostPoint(IEnumerable<OutlineSegment> segments, IList<Point2d> ignoredPoints)
        {
            var point = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => !ContainsPoint(ignoredPoints, p))
                .OrderByDescending(p => p.Y)
                .ThenBy(p => p.X)
                .Select(p => (Point2d?)p)
                .FirstOrDefault();

            return point;
        }

        private StructurePoint? GetTopMostStructurePoint(IEnumerable<OutlineSegment> segments, IList<IgnoredPoint> ignoredPoints)
        {
            var point = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => !ContainsIgnoredPoint(ignoredPoints, p))
                .OrderByDescending(p => p.Y)
                .ThenBy(p => p.X)
                .Select(p => (Point2d?)p)
                .FirstOrDefault();

            if (!point.HasValue)
            {
                return null;
            }

            return new StructurePoint
            {
                Point = point.Value,
                Source = "VerticalTopMost"
            };
        }

        private Point2d? GetBottomMostPoint(IEnumerable<OutlineSegment> segments, IList<Point2d> ignoredPoints)
        {
            var point = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => !ContainsPoint(ignoredPoints, p))
                .OrderBy(p => p.Y)
                .ThenBy(p => p.X)
                .Select(p => (Point2d?)p)
                .FirstOrDefault();

            return point;
        }

        private void ResolveHorizontalStepBoundarySpan(
            OutlineFeature outline,
            OutlineSegment segment,
            DimSide side,
            out Point2d leftPoint,
            out Point2d rightPoint)
        {
            leftPoint = segment.Start.X <= segment.End.X ? segment.Start : segment.End;
            rightPoint = segment.Start.X > segment.End.X ? segment.Start : segment.End;

            if (side == DimSide.Bottom)
            {
                TryResolveBottomProtrusionWidthSpan(outline, segment, ref leftPoint, ref rightPoint);
            }
        }

        private bool TryResolveBottomProtrusionWidthSpan(
            OutlineFeature outline,
            OutlineSegment segment,
            ref Point2d leftPoint,
            ref Point2d rightPoint)
        {
            var tolerance = _config.GeometryTolerance;
            var lowerLimit = outline.MinY + outline.Height * 0.45;
            var localWidthLimit = Math.Max(outline.Width * 0.35, Scale(_config.ArrowSize * 10.0));
            var currentSpan = Math.Abs(rightPoint.X - leftPoint.X);
            var baseY = segment.MinY;

            var verticals = outline.Segments
                .Where(s => s.IsVertical(tolerance))
                .Where(s => s.MinY <= lowerLimit + tolerance)
                .Where(s => s.MinX >= outline.MinX - tolerance)
                .Where(s => s.MinX <= outline.MinX + localWidthLimit + tolerance)
                .OrderBy(s => s.MinX)
                .ToList();
            if (verticals.Count < 2)
            {
                return false;
            }

            var left = verticals.First();
            var right = verticals
                .Where(s => s.MinX > left.MinX + tolerance)
                .OrderBy(s => s.MinX)
                .FirstOrDefault();
            if (right == null)
            {
                return false;
            }

            var candidateSpan = Math.Abs(right.MinX - left.MinX);
            if (candidateSpan <= _config.GeometryTolerance || candidateSpan > currentSpan + Math.Max(tolerance, 2.0))
            {
                return false;
            }

            leftPoint = new Point2d(left.MinX, baseY);
            rightPoint = new Point2d(right.MinX, baseY);
            return true;
        }

        private bool IsHorizontalProtrusionStep(OutlineSegment segment, OutlineFeature outline, DimSide side)
        {
            if (GetNearestHorizontalDimSide(segment, outline) != side)
            {
                return false;
            }

            var maxLocalWidth = Math.Max(outline.Width * 0.35, Scale(_config.ArrowSize * 10.0));
            if (segment.LengthX > maxLocalWidth + _config.GeometryTolerance)
            {
                return false;
            }

            return TouchesHorizontalOuterBoundary(segment, outline)
                && HasHorizontalStepReturn(segment, outline);
        }

        private int ScoreHorizontalProtrusionStep(OutlineSegment segment, OutlineFeature outline, DimSide side)
        {
            var score = 0;
            if (TouchesHorizontalOuterBoundary(segment, outline))
            {
                score += 4;
            }

            if (HasHorizontalStepReturn(segment, outline))
            {
                score += 4;
            }

            if (side == DimSide.Bottom)
            {
                score += Math.Abs(segment.MinY - outline.MinY) <= outline.Height * 0.25 ? 2 : 0;
            }
            else
            {
                score += Math.Abs(segment.MaxY - outline.MaxY) <= outline.Height * 0.25 ? 2 : 0;
            }

            return score;
        }

        private bool TouchesHorizontalOuterBoundary(OutlineSegment segment, OutlineFeature outline)
        {
            var tolerance = Math.Max(_config.GeometryTolerance, 0.2);
            return Math.Abs(segment.MinX - outline.MinX) <= tolerance
                || Math.Abs(segment.MaxX - outline.MaxX) <= tolerance
                || EndpointConnectsToBoundary(segment.Start, outline, horizontalBoundary: true)
                || EndpointConnectsToBoundary(segment.End, outline, horizontalBoundary: true);
        }

        private bool HasHorizontalStepReturn(OutlineSegment segment, OutlineFeature outline)
        {
            return EndpointHasStructuralReturn(segment.Start, segment, outline)
                || EndpointHasStructuralReturn(segment.End, segment, outline);
        }

        private bool EndpointHasStructuralReturn(Point2d endpoint, OutlineSegment source, OutlineFeature outline)
        {
            if (outline.Segments.Any(s =>
                !ReferenceEquals(s, source)
                && SegmentTouchesPoint(s, endpoint)
                && !s.IsHorizontal(_config.GeometryTolerance)))
            {
                return true;
            }

            if (outline.Chamfers.Any(chamfer =>
                PointsEqual(chamfer.StartPoint, endpoint)
                || PointsEqual(chamfer.EndPoint, endpoint)))
            {
                return true;
            }

            return outline.Fillets.Any(fillet =>
                PointsEqual(fillet.StartPoint, endpoint)
                || PointsEqual(fillet.EndPoint, endpoint));
        }

        private bool EndpointConnectsToBoundary(Point2d endpoint, OutlineFeature outline, bool horizontalBoundary)
        {
            foreach (var segment in outline.Segments)
            {
                if (!SegmentTouchesPoint(segment, endpoint))
                {
                    continue;
                }

                if (horizontalBoundary)
                {
                    if (Math.Abs(segment.MinX - outline.MinX) <= _config.GeometryTolerance
                        || Math.Abs(segment.MaxX - outline.MaxX) <= _config.GeometryTolerance)
                    {
                        return true;
                    }
                }
                else if (Math.Abs(segment.MinY - outline.MinY) <= _config.GeometryTolerance
                    || Math.Abs(segment.MaxY - outline.MaxY) <= _config.GeometryTolerance)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
