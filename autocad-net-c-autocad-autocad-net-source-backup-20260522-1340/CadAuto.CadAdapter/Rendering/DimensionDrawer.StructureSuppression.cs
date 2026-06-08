using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private bool IsUsefulHorizontalStep(OutlineSegment segment, OutlineFeature outline)
        {
            return segment.IsHorizontal(_config.GeometryTolerance)
                && segment.LengthX > _config.GeometryTolerance
                && segment.LengthX < outline.Width - _config.GeometryTolerance;
        }

        private bool IsUsefulVerticalStep(OutlineSegment segment, OutlineFeature outline)
        {
            return segment.IsVertical(_config.GeometryTolerance)
                && segment.LengthY > _config.GeometryTolerance
                && segment.LengthY < outline.Height - _config.GeometryTolerance;
        }

        private bool IsFeatureRedundantStepDimension(OutlineSegment segment, OutlineFeature outline)
        {
            return IsRedundantChamferStepDimension(segment, outline)
                || IsRedundantFilletStepDimension(segment, outline);
        }

        private bool IsRedundantChamferStepDimension(OutlineSegment segment, OutlineFeature outline)
        {
            if (IsBetweenTwoChamfersAndDerivedFromOverall(segment, outline))
            {
                return true;
            }

            return outline.Chamfers.Any(chamfer =>
            {
                var featureSize = Math.Max(chamfer.DeltaX, chamfer.DeltaY);
                return IsSmallFeatureAdjacentDimension(segment, featureSize)
                    && SegmentTouchesPoint(segment, chamfer.StartPoint)
                    && SegmentTouchesPoint(segment, chamfer.EndPoint);
            });
        }

        private bool IsBetweenTwoChamfersAndDerivedFromOverall(OutlineSegment segment, OutlineFeature outline)
        {
            if (!segment.IsHorizontal(_config.GeometryTolerance) && !segment.IsVertical(_config.GeometryTolerance))
            {
                return false;
            }

            var startChamfer = FindChamferTouchingPoint(outline, segment.Start);
            var endChamfer = FindChamferTouchingPoint(outline, segment.End);
            if (startChamfer == null || endChamfer == null || ReferenceEquals(startChamfer, endChamfer))
            {
                return false;
            }

            if (segment.IsVertical(_config.GeometryTolerance))
            {
                var derived = outline.Height - GetChamferProjectionY(startChamfer) - GetChamferProjectionY(endChamfer);
                return Math.Abs(segment.LengthY - derived) <= Math.Max(_config.GeometryTolerance, 0.2);
            }

            var horizontalDerived = outline.Width - GetChamferProjectionX(startChamfer) - GetChamferProjectionX(endChamfer);
            return Math.Abs(segment.LengthX - horizontalDerived) <= Math.Max(_config.GeometryTolerance, 0.2);
        }

        private ChamferFeature FindChamferTouchingPoint(OutlineFeature outline, Point2d point)
        {
            return outline.Chamfers.FirstOrDefault(chamfer =>
                PointsEqual(chamfer.StartPoint, point) || PointsEqual(chamfer.EndPoint, point));
        }

        private static double GetChamferProjectionX(ChamferFeature chamfer)
        {
            return Math.Abs(chamfer.StartPoint.X - chamfer.EndPoint.X);
        }

        private static double GetChamferProjectionY(ChamferFeature chamfer)
        {
            return Math.Abs(chamfer.StartPoint.Y - chamfer.EndPoint.Y);
        }

        private bool IsRedundantFilletStepDimension(OutlineSegment segment, OutlineFeature outline)
        {
            return outline.Fillets.Any(fillet =>
            {
                var featureSize = fillet.Radius * 2.0;
                return IsSmallFeatureAdjacentDimension(segment, featureSize)
                    && ((SegmentTouchesPoint(segment, fillet.StartPoint)
                            && SegmentTouchesPoint(segment, fillet.EndPoint))
                        || IsSingleTangentFilletStepDimension(segment, fillet));
            });
        }

        private bool IsSingleTangentFilletStepDimension(OutlineSegment segment, FilletFeature fillet)
        {
            var touchesOneTangent = SegmentTouchesPoint(segment, fillet.StartPoint)
                || SegmentTouchesPoint(segment, fillet.EndPoint);
            if (!touchesOneTangent)
            {
                return false;
            }

            var tangentLimit = Math.Max(fillet.Radius * 10.0, Scale(_config.ArrowSize * 6.0));
            return segment.Length <= tangentLimit + _config.GeometryTolerance;
        }

        private bool IsSmallFeatureAdjacentDimension(OutlineSegment segment, double featureSize)
        {
            var limit = Math.Max(featureSize * 2.5, Scale(_config.ArrowSize * 2.0));
            return segment.Length <= limit + _config.GeometryTolerance;
        }

        private bool SegmentTouchesPoint(OutlineSegment segment, Point2d point)
        {
            return PointsEqual(segment.Start, point) || PointsEqual(segment.End, point);
        }

        private bool PointsEqual(Point2d a, Point2d b)
        {
            return a.GetDistanceTo(b) <= Math.Max(_config.GeometryTolerance, 0.2);
        }

        private DimSide GetNearestVerticalDimSide(OutlineSegment segment, OutlineFeature outline)
        {
            var distanceToLeft = Math.Abs(segment.MinX - outline.MinX);
            var distanceToRight = Math.Abs(outline.MaxX - segment.MaxX);
            return distanceToLeft <= distanceToRight ? DimSide.Left : DimSide.Right;
        }

        private DimSide GetNearestHorizontalDimSide(OutlineSegment segment, OutlineFeature outline)
        {
            var distanceToBottom = Math.Abs(segment.MinY - outline.MinY);
            var distanceToTop = Math.Abs(outline.MaxY - segment.MaxY);
            return distanceToBottom < distanceToTop ? DimSide.Bottom : DimSide.Top;
        }

        private bool StepHeightExtensionCrossesOutlineInterior(OutlineSegment segment, OutlineFeature outline, DimSide side)
        {
            var offset = Scale(_config.FirstDimOffset);
            var dimX = side == DimSide.Left ? outline.MinX - offset : outline.MaxX + offset;
            return HorizontalProbeCrossesInterior(segment.MaxY, segment.MinX, dimX, outline)
                || HorizontalProbeCrossesInterior(segment.MinY, segment.MinX, dimX, outline);
        }

        private bool HorizontalProbeCrossesInterior(double y, double fromX, double toX, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            var minX = Math.Min(fromX, toX);
            var maxX = Math.Max(fromX, toX);

            foreach (var sampleX in GetProbeSampleXs(minX, maxX, outline))
            {
                if (sampleX <= minX + tolerance || sampleX >= maxX - tolerance)
                {
                    continue;
                }

                if (IsPointInsideOutlineByRayCast(sampleX, y, outline))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<double> GetProbeSampleXs(double minX, double maxX, OutlineFeature outline)
        {
            foreach (var segment in outline.Segments)
            {
                yield return Math.Max(minX, Math.Min(maxX, segment.MinX));
                yield return Math.Max(minX, Math.Min(maxX, segment.MaxX));
            }

            const int slices = 8;
            for (int i = 1; i < slices; i++)
            {
                yield return minX + (maxX - minX) * i / slices;
            }
        }

        private bool IsPointInsideOutlineByRayCast(double x, double y, OutlineFeature outline)
        {
            bool inside = false;
            var tolerance = _config.GeometryTolerance;

            foreach (var segment in outline.Segments)
            {
                var y1 = segment.Start.Y;
                var y2 = segment.End.Y;
                if (Math.Abs(y1 - y2) <= tolerance)
                {
                    continue;
                }

                bool crosses = (y1 > y) != (y2 > y);
                if (!crosses)
                {
                    continue;
                }

                var xAtY = segment.Start.X + (y - y1) * (segment.End.X - segment.Start.X) / (y2 - y1);
                if (xAtY > x + tolerance)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private bool IsOuterHorizontalSegment(OutlineSegment segment, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            if (!segment.IsHorizontal(tolerance))
            {
                return false;
            }

            return Math.Abs(segment.MinY - outline.MinY) <= tolerance
                || Math.Abs(segment.MaxY - outline.MaxY) <= tolerance;
        }

        private bool IsOuterVerticalSegment(OutlineSegment segment, OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            if (!segment.IsVertical(tolerance))
            {
                return false;
            }

            return Math.Abs(segment.MinX - outline.MinX) <= tolerance
                || Math.Abs(segment.MaxX - outline.MaxX) <= tolerance;
        }
    }
}
