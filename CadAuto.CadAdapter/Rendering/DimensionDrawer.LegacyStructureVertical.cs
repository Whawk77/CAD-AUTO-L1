using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private void DrawRightStepHeight(OutlineFeature outline)
        {
            var ignoredPoints = new List<Point2d>();
            List<Point2d> points;
            List<DeferredDim> candidates;
            while (true)
            {
                points = BuildLeftSideVerticalStructurePoints(outline, ignoredPoints);
                if (points.Count < 2)
                {
                    return;
                }

                candidates = BuildLeftSideVerticalHeightCandidates(points, "LeftStructHeight");
                var crossingPoints = GetLeftExtensionCrossingPoints(candidates, outline);
                if (crossingPoints.Count == 0)
                {
                    break;
                }

                if (!AddIgnoredPoints(ignoredPoints, crossingPoints))
                {
                    return;
                }
            }

            if (_diagnosticsEnabled && ShouldFlushDiagnosticSide(DimSide.Left))
            {
                AddDirectionalPointDebugLabels(points, ignoredPoints, "SP:HLM", "IG:UNK");
            }

            RemoveLongestExtensionCandidate(candidates, outline);
            foreach (var dim in candidates)
            {
                _leftDims.Add(dim);
            }
        }

        private void DrawRightSideStepHeight(OutlineFeature outline)
        {
            var ignoredPoints = new List<Point2d>();
            List<Point2d> points;
            List<DeferredDim> candidates;
            while (true)
            {
                points = BuildRightSideVerticalStructurePoints(outline, ignoredPoints);
                if (points.Count < 2)
                {
                    return;
                }

                candidates = BuildLeftSideVerticalHeightCandidates(points, "RightStructHeight");
                var crossingPoints = GetRightExtensionCrossingPoints(candidates, outline);
                if (crossingPoints.Count == 0)
                {
                    break;
                }

                if (!AddIgnoredPoints(ignoredPoints, crossingPoints))
                {
                    return;
                }
            }

            if (_diagnosticsEnabled && ShouldFlushDiagnosticSide(DimSide.Right))
            {
                AddDirectionalPointDebugLabels(points, ignoredPoints, "SP:HRM", "IG:UNK");
            }

            RemoveLongestRightExtensionCandidate(candidates, outline);
            foreach (var dim in candidates)
            {
                if (!IsRightSideVerticalStructureCandidate(dim, outline, ignoredPoints))
                {
                    continue;
                }

                _rightDims.Add(dim);
            }
        }

        private List<DeferredDim> BuildLeftSideVerticalHeightCandidates(IList<Point2d> points, string debugRole)
        {
            var candidates = new List<DeferredDim>();
            for (int i = 1; i < points.Count; i++)
            {
                var upperPoint = points[i - 1];
                var lowerPoint = points[i];
                var span = Math.Abs(upperPoint.Y - lowerPoint.Y);
                if (span <= _config.GeometryTolerance)
                {
                    continue;
                }

                candidates.Add(new DeferredDim
                {
                    Rotation = Math.PI / 2.0,
                    XLine1 = new Point3d(lowerPoint.X, lowerPoint.Y, 0.0),
                    XLine2 = new Point3d(upperPoint.X, upperPoint.Y, 0.0),
                    OverrideText = string.Empty,
                    Span = span,
                    DimType = DimensionType.Normal,
                    DebugRole = debugRole ?? string.Empty
                });
            }

            return candidates;
        }

        private List<Point2d> BuildLeftSideVerticalStructurePoints(OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            var tolerance = _config.GeometryTolerance;
            var groups = new List<List<OutlineSegment>>();
            foreach (var segment in outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthX > tolerance))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(g[0].MinY - segment.MinY) <= tolerance);
                if (group == null)
                {
                    group = new List<OutlineSegment>();
                    groups.Add(group);
                }

                group.Add(segment);
            }

            var points = groups
                .Select(g => GetLeftMostPoint(g, ignoredPoints))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            AddSideInclinedEndpointStructurePoints(points, outline, ignoredPoints, DimSide.Left);

            return points
                .OrderByDescending(p => p.Y)
                .ToList();
        }

        private Point2d? GetLeftMostPoint(IEnumerable<OutlineSegment> segments, IList<Point2d> ignoredPoints)
        {
            var point = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => !ContainsPoint(ignoredPoints, p))
                .OrderBy(p => p.X)
                .ThenBy(p => p.Y)
                .Select(p => (Point2d?)p)
                .FirstOrDefault();

            return point;
        }

        private List<Point2d> BuildRightSideVerticalStructurePoints(OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            var tolerance = _config.GeometryTolerance;
            var groups = new List<List<OutlineSegment>>();
            foreach (var segment in outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => s.LengthX > tolerance))
            {
                var group = groups.FirstOrDefault(g => Math.Abs(g[0].MinY - segment.MinY) <= tolerance);
                if (group == null)
                {
                    group = new List<OutlineSegment>();
                    groups.Add(group);
                }

                group.Add(segment);
            }

            var points = groups
                .Select(g => GetRightMostPoint(g, ignoredPoints))
                .Where(p => p.HasValue)
                .Select(p => p.Value)
                .ToList();

            AddSideInclinedEndpointStructurePoints(points, outline, ignoredPoints, DimSide.Right);

            return points
                .OrderByDescending(p => p.Y)
                .ToList();
        }

        private Point2d? GetRightMostPoint(IEnumerable<OutlineSegment> segments, IList<Point2d> ignoredPoints)
        {
            var point = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => !ContainsPoint(ignoredPoints, p))
                .OrderByDescending(p => p.X)
                .ThenBy(p => p.Y)
                .Select(p => (Point2d?)p)
                .FirstOrDefault();

            return point;
        }

        private bool IsRightSideVerticalStructureCandidate(DeferredDim dim, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            return IsCurrentRightSideStructurePoint(new Point2d(dim.XLine1.X, dim.XLine1.Y), outline, ignoredPoints)
                && IsCurrentRightSideStructurePoint(new Point2d(dim.XLine2.X, dim.XLine2.Y), outline, ignoredPoints);
        }

        private bool IsCurrentRightSideStructurePoint(Point2d point, OutlineFeature outline, IList<Point2d> ignoredPoints)
        {
            if (IsSideInclinedEndpointStructurePoint(point, outline, ignoredPoints, DimSide.Right))
            {
                return true;
            }

            var tolerance = _config.GeometryTolerance;
            var levelSegments = outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => !s.IsArcChord)
                .Where(s => s.LengthX > tolerance)
                .Where(s => Math.Abs(s.MinY - point.Y) <= tolerance)
                .ToList();
            if (levelSegments.Count == 0)
            {
                return false;
            }

            var rightMost = GetRightMostPoint(levelSegments, ignoredPoints);
            return rightMost.HasValue && PointsEqual(rightMost.Value, point);
        }

        private void AddSideInclinedEndpointStructurePoints(
            IList<Point2d> points,
            OutlineFeature outline,
            IList<Point2d> ignoredPoints,
            DimSide side)
        {
            var tolerance = _config.GeometryTolerance;
            foreach (var segment in outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsSideInnerGrooveChamferSegment(s, outline, side)))
            {
                AddSideInclinedEndpointStructurePoint(points, segment.Start, outline, ignoredPoints);
                AddSideInclinedEndpointStructurePoint(points, segment.End, outline, ignoredPoints);
            }
        }

        private void AddSideInclinedEndpointStructurePoint(
            IList<Point2d> points,
            Point2d point,
            OutlineFeature outline,
            IList<Point2d> ignoredPoints)
        {
            if (ContainsPoint(points, point)
                || ContainsPoint(ignoredPoints, point)
                || IsEnvelopeHorizontalSidePoint(point, outline))
            {
                return;
            }

            points.Add(point);
        }

        private bool IsSideInclinedEndpointStructurePoint(Point2d point, OutlineFeature outline, IList<Point2d> ignoredPoints, DimSide side)
        {
            if (ContainsPoint(ignoredPoints, point) || IsEnvelopeHorizontalSidePoint(point, outline))
            {
                return false;
            }

            var tolerance = _config.GeometryTolerance;
            return outline.Segments
                .Where(s => !s.IsHorizontal(tolerance))
                .Where(s => !s.IsVertical(tolerance))
                .Where(IsFortyFiveDegreeSegment)
                .Where(s => IsSideInnerGrooveChamferSegment(s, outline, side))
                .Any(s => PointsEqual(point, s.Start) || PointsEqual(point, s.End));
        }

        private IEnumerable<OutlineSegment> PickVerticalStepHeightSegments(IList<OutlineSegment> segments, OutlineFeature outline)
        {
            foreach (var side in new[] { DimSide.Left, DimSide.Right })
            {
                var segment = segments
                    .Where(s => GetNearestVerticalDimSide(s, outline) == side)
                    .OrderByDescending(s => ScoreVerticalProtrusionStep(s, outline))
                    .ThenByDescending(s => s.LengthY)
                    .FirstOrDefault();
                if (segment != null)
                {
                    yield return segment;
                }
            }
        }

        private bool IsVerticalProtrusionStep(OutlineSegment segment, OutlineFeature outline)
        {
            if (IsOuterVerticalSegment(segment, outline))
            {
                return false;
            }

            var maxLocalHeight = Math.Max(outline.Height * 0.8, Scale(_config.ArrowSize * 12.0));
            if (segment.LengthY > maxLocalHeight + _config.GeometryTolerance)
            {
                return false;
            }

            return TouchesVerticalOuterBoundary(segment, outline)
                && HasVerticalStepReturn(segment, outline);
        }

        private int ScoreVerticalProtrusionStep(OutlineSegment segment, OutlineFeature outline)
        {
            var score = 0;
            if (TouchesVerticalOuterBoundary(segment, outline))
            {
                score += 4;
            }

            if (HasVerticalStepReturn(segment, outline))
            {
                score += 4;
            }

            if (GetNearestVerticalDimSide(segment, outline) == DimSide.Left)
            {
                score += Math.Abs(segment.MinX - outline.MinX) <= outline.Width * 0.25 ? 2 : 0;
            }
            else
            {
                score += Math.Abs(segment.MaxX - outline.MaxX) <= outline.Width * 0.25 ? 2 : 0;
            }

            return score;
        }

        private bool TouchesVerticalOuterBoundary(OutlineSegment segment, OutlineFeature outline)
        {
            var tolerance = Math.Max(_config.GeometryTolerance, 0.2);
            return Math.Abs(segment.MinY - outline.MinY) <= tolerance
                || Math.Abs(segment.MaxY - outline.MaxY) <= tolerance
                || EndpointConnectsToBoundary(segment.Start, outline, horizontalBoundary: false)
                || EndpointConnectsToBoundary(segment.End, outline, horizontalBoundary: false);
        }

        private bool HasVerticalStepReturn(OutlineSegment segment, OutlineFeature outline)
        {
            return EndpointHasStructuralReturn(segment.Start, segment, outline)
                || EndpointHasStructuralReturn(segment.End, segment, outline);
        }

        private void ResolveVerticalStepBoundarySpan(
            OutlineFeature outline,
            OutlineSegment segment,
            DimSide side,
            out Point2d lowerPoint,
            out Point2d upperPoint)
        {
            lowerPoint = segment.Start.Y <= segment.End.Y ? segment.Start : segment.End;
            upperPoint = segment.Start.Y > segment.End.Y ? segment.Start : segment.End;

            Point2d resolved;
            if (TryResolveAdjacentVerticalStepEndpoint(outline, segment, lowerPoint, lowerPoint.Y, searchLower: true, out resolved))
            {
                lowerPoint = resolved;
            }

            if (TryResolveAdjacentVerticalStepEndpoint(outline, segment, upperPoint, upperPoint.Y, searchLower: false, out resolved))
            {
                upperPoint = resolved;
            }

            if (side == DimSide.Left)
            {
                TryResolveLeftProtrusionHeightSpan(outline, segment, ref lowerPoint, ref upperPoint);
            }
            else if (side == DimSide.Right)
            {
                TryResolveRightProtrusionHeightSpan(outline, segment, ref lowerPoint, ref upperPoint);
            }
        }

        private bool TryResolveLeftProtrusionHeightSpan(
            OutlineFeature outline,
            OutlineSegment segment,
            ref Point2d lowerPoint,
            ref Point2d upperPoint)
        {
            var tolerance = _config.GeometryTolerance;
            var leftLimit = outline.MinX + outline.Width * 0.45;
            var topLimit = outline.MinY + outline.Height * 0.7;
            var currentSpan = Math.Abs(upperPoint.Y - lowerPoint.Y);
            var lowerY = lowerPoint.Y;

            var bottom = outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => s.LengthX <= Math.Max(outline.Width * 0.35, Scale(_config.ArrowSize * 10.0)) + tolerance)
                .Where(s => s.MinY <= lowerY + Math.Max(tolerance, 0.2))
                .Where(s => s.MinX <= leftLimit)
                .OrderBy(s => s.MinY)
                .ThenBy(s => Math.Abs(s.MinX - outline.MinX))
                .FirstOrDefault();

            var top = outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => s.MinX <= leftLimit)
                .Where(s => s.MinY > lowerY + tolerance)
                .Where(s => s.MinY <= topLimit + tolerance)
                .OrderByDescending(s => s.MinY)
                .ThenBy(s => Math.Abs(s.MinX - segment.MinX))
                .FirstOrDefault();

            if (bottom == null || top == null)
            {
                return false;
            }

            var candidateLower = Math.Abs(bottom.Start.X - segment.MinX) <= Math.Abs(bottom.End.X - segment.MinX)
                ? bottom.Start
                : bottom.End;
            var candidateUpper = Math.Abs(top.Start.X - segment.MinX) <= Math.Abs(top.End.X - segment.MinX)
                ? top.Start
                : top.End;
            var candidateSpan = Math.Abs(candidateUpper.Y - candidateLower.Y);
            if (candidateSpan <= currentSpan + Math.Max(tolerance, 0.2))
            {
                return false;
            }

            lowerPoint = candidateLower;
            upperPoint = candidateUpper;
            return true;
        }

        private bool TryResolveRightProtrusionHeightSpan(
            OutlineFeature outline,
            OutlineSegment segment,
            ref Point2d lowerPoint,
            ref Point2d upperPoint)
        {
            var tolerance = _config.GeometryTolerance;
            var rightLimit = outline.MaxX - outline.Width * 0.45;
            var currentSpan = Math.Abs(upperPoint.Y - lowerPoint.Y);
            var points = new List<Point2d> { segment.Start, segment.End };
            var structureX = segment.MinX;
            var xTolerance = Math.Max(_config.GeometryTolerance, Scale(_config.ArrowSize * 2.0));

            foreach (var neighbor in outline.Segments)
            {
                if (ReferenceEquals(neighbor, segment))
                {
                    continue;
                }

                if (neighbor.MinX < rightLimit - tolerance)
                {
                    continue;
                }

                var nearSameStructure =
                    Math.Abs(neighbor.MinX - structureX) <= xTolerance
                    || Math.Abs(neighbor.MaxX - structureX) <= xTolerance
                    || SegmentTouchesPoint(neighbor, segment.Start)
                    || SegmentTouchesPoint(neighbor, segment.End);
                if (nearSameStructure)
                {
                    points.Add(neighbor.Start);
                    points.Add(neighbor.End);
                }
            }

            foreach (var chamfer in outline.Chamfers)
            {
                if (chamfer.StartPoint.X < rightLimit - tolerance && chamfer.EndPoint.X < rightLimit - tolerance)
                {
                    continue;
                }

                if (Math.Abs(chamfer.StartPoint.X - structureX) <= xTolerance
                    || Math.Abs(chamfer.EndPoint.X - structureX) <= xTolerance
                    || PointsEqual(chamfer.StartPoint, segment.Start)
                    || PointsEqual(chamfer.EndPoint, segment.Start)
                    || PointsEqual(chamfer.StartPoint, segment.End)
                    || PointsEqual(chamfer.EndPoint, segment.End))
                {
                    points.Add(chamfer.StartPoint);
                    points.Add(chamfer.EndPoint);
                }
            }

            var minY = points.Min(p => p.Y);
            var maxY = points.Max(p => p.Y);
            var top = outline.Segments
                .Where(s => s.IsHorizontal(tolerance))
                .Where(s => s.MaxX >= rightLimit - tolerance)
                .Where(s => s.MinY >= maxY - tolerance)
                .OrderBy(s => s.MinY)
                .FirstOrDefault();
            if (top != null)
            {
                maxY = Math.Max(maxY, top.MinY);
            }

            var candidateSpan = maxY - minY;
            if (candidateSpan <= currentSpan + Math.Max(tolerance, 0.2))
            {
                return false;
            }

            var x = segment.MinX;
            lowerPoint = new Point2d(x, minY);
            upperPoint = new Point2d(x, maxY);
            return true;
        }

        private bool TryResolveAdjacentVerticalStepEndpoint(
            OutlineFeature outline,
            OutlineSegment segment,
            Point2d endpoint,
            double originalY,
            bool searchLower,
            out Point2d resolved)
        {
            resolved = endpoint;
            var candidates = new List<Point2d>();

            foreach (var neighbor in outline.Segments)
            {
                if (ReferenceEquals(neighbor, segment))
                {
                    continue;
                }

                if (PointsEqual(neighbor.Start, endpoint))
                {
                    candidates.Add(neighbor.End);
                }
                else if (PointsEqual(neighbor.End, endpoint))
                {
                    candidates.Add(neighbor.Start);
                }
            }

            foreach (var arc in outline.Arcs)
            {
                if (PointsEqual(arc.Start, endpoint))
                {
                    candidates.Add(arc.End);
                }
                else if (PointsEqual(arc.End, endpoint))
                {
                    candidates.Add(arc.Start);
                }
            }

            foreach (var fillet in outline.Fillets)
            {
                if (PointsEqual(fillet.StartPoint, endpoint))
                {
                    candidates.Add(fillet.EndPoint);
                }
                else if (PointsEqual(fillet.EndPoint, endpoint))
                {
                    candidates.Add(fillet.StartPoint);
                }
            }

            var maxLocalExtension = Math.Max(segment.LengthY * 0.25, Scale(_config.ArrowSize * 8.0));
            var best = candidates
                .Where(p => searchLower
                    ? p.Y < originalY - _config.GeometryTolerance
                    : p.Y > originalY + _config.GeometryTolerance)
                .Select(p => new { Point = p, Delta = Math.Abs(p.Y - originalY) })
                .Where(x => x.Delta <= maxLocalExtension + _config.GeometryTolerance)
                .OrderByDescending(x => x.Delta)
                .FirstOrDefault();

            if (best == null)
            {
                return false;
            }

            resolved = best.Point;
            return true;
        }
    }
}
