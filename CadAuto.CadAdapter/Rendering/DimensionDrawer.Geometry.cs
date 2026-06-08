using System;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private Point3d GetDimLinePoint(DeferredDim dim, DimSide side, OutlineFeature outline, double offset)
        {
            switch (side)
            {
                case DimSide.Bottom:
                    return new Point3d((dim.XLine1.X + dim.XLine2.X) / 2.0, GetDimLineCoordinate(dim, side, outline, offset), 0.0);
                case DimSide.Top:
                    return new Point3d((dim.XLine1.X + dim.XLine2.X) / 2.0, GetDimLineCoordinate(dim, side, outline, offset), 0.0);
                case DimSide.Left:
                    return new Point3d(GetDimLineCoordinate(dim, side, outline, offset), (dim.XLine1.Y + dim.XLine2.Y) / 2.0, 0.0);
                case DimSide.Right:
                    return new Point3d(GetDimLineCoordinate(dim, side, outline, offset), (dim.XLine1.Y + dim.XLine2.Y) / 2.0, 0.0);
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }

        private double GetDimLineCoordinate(DeferredDim dim, DimSide side, OutlineFeature outline, double offset)
        {
            double localCoordinate;
            if (TryGetLocalDimLineCoordinate(dim, side, outline, offset, out localCoordinate))
            {
                return localCoordinate;
            }

            switch (side)
            {
                case DimSide.Bottom:
                    return outline.MinY - offset;
                case DimSide.Top:
                    return outline.MaxY + offset;
                case DimSide.Left:
                    return outline.MinX - offset;
                case DimSide.Right:
                    return outline.MaxX + offset;
                default:
                    throw new ArgumentOutOfRangeException(nameof(side), side, null);
            }
        }

        private (double A, double B) ComputeArrowInterval(DeferredDim dim, bool isHorizontal)
        {
            if (isHorizontal)
            {
                return (Math.Min(dim.XLine1.X, dim.XLine2.X), Math.Max(dim.XLine1.X, dim.XLine2.X));
            }

            return (Math.Min(dim.XLine1.Y, dim.XLine2.Y), Math.Max(dim.XLine1.Y, dim.XLine2.Y));
        }

        private Vector2d GetHorizontalOutwardDirection(OutlineFeature outline, Point2d target)
        {
            var centerX = (outline.MinX + outline.MaxX) / 2.0;
            return new Vector2d(target.X < centerX ? -1.0 : 1.0, 0.0);
        }

        private double GetCornerLeaderYShift(OutlineFeature outline, Point2d target, int groupIndex)
        {
            var centerY = (outline.MinY + outline.MaxY) / 2.0;
            var nearTop = Math.Abs(outline.MaxY - target.Y) <= outline.Height * 0.25;
            var nearBottom = Math.Abs(target.Y - outline.MinY) <= outline.Height * 0.25;
            var stagger = Scale(groupIndex * 3.0);

            if (nearTop)
            {
                return Scale(8.0) + stagger;
            }

            if (nearBottom)
            {
                return -Scale(8.0) - stagger;
            }

            return target.Y >= centerY ? Scale(4.0) + stagger : -Scale(4.0) - stagger;
        }

        private Vector2d GetOutwardDirection(OutlineFeature outline, Point2d target)
        {
            var centerX = (outline.MinX + outline.MaxX) / 2.0;
            var centerY = (outline.MinY + outline.MaxY) / 2.0;
            var x = target.X < centerX ? -1.0 : 1.0;
            var y = target.Y < centerY ? -1.0 : 1.0;
            return new Vector2d(x, y).GetNormal();
        }

        private static Point2d Midpoint(Point2d a, Point2d b)
        {
            return new Point2d((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);
        }

        private static Point3d Midpoint(Point3d a, Point3d b)
        {
            return new Point3d((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);
        }

        private Point3d GetLeftBoundaryPoint(OutlineFeature outline)
        {
            var candidate = outline.Vertices
                .Where(v => Math.Abs(v.X - outline.MinX) <= _config.GeometryTolerance)
                .OrderBy(v => v.Y)
                .FirstOrDefault();

            return ToPoint3d(candidate, outline.MinX, outline.MinY);
        }

        private Point3d GetRightBoundaryPoint(OutlineFeature outline)
        {
            var candidate = outline.Vertices
                .Where(v => Math.Abs(v.X - outline.MaxX) <= _config.GeometryTolerance)
                .OrderBy(v => v.Y)
                .FirstOrDefault();

            return ToPoint3d(candidate, outline.MaxX, outline.MinY);
        }

        private Point3d GetBottomBoundaryPoint(OutlineFeature outline)
        {
            var candidate = outline.Vertices
                .Where(v => Math.Abs(v.Y - outline.MinY) <= _config.GeometryTolerance)
                .OrderBy(v => v.X)
                .FirstOrDefault();

            return ToPoint3d(candidate, outline.MinX, outline.MinY);
        }

        private Point3d GetTopBoundaryPoint(OutlineFeature outline)
        {
            var candidate = outline.Vertices
                .Where(v => Math.Abs(v.Y - outline.MaxY) <= _config.GeometryTolerance)
                .OrderBy(v => v.X)
                .FirstOrDefault();

            return ToPoint3d(candidate, outline.MinX, outline.MaxY);
        }

        private static Point3d ToPoint3d(Point2d point, double fallbackX, double fallbackY)
        {
            if (point == default(Point2d))
            {
                return new Point3d(fallbackX, fallbackY, 0.0);
            }

            return new Point3d(point.X, point.Y, 0.0);
        }
    }
}
