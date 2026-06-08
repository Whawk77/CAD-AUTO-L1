using System;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private void AddHorizontalDim(Point3d from, Point3d to, string overrideText, DimensionType dimType, string debugRole = null)
        {
            AddHorizontalDimFromPoint(new Point3d(from.X, from.Y, from.Z), to, overrideText, dimType, debugRole);
        }

        private void AddHorizontalDimToSide(
            Point3d from,
            Point3d to,
            string overrideText,
            DimensionType dimType,
            DimSide side,
            bool preferLocalBoundary = false,
            string debugOwner = null,
            string debugRole = null)
        {
            AddHorizontalDimFromPointToSide(new Point3d(from.X, from.Y, from.Z), to, overrideText, dimType, side, preferLocalBoundary, debugOwner, debugRole);
        }

        private void AddHorizontalDimFromX(double fromX, Point3d to, string overrideText, DimensionType dimType, string debugRole = null)
        {
            AddHorizontalDimFromPoint(new Point3d(fromX, to.Y, 0.0), to, overrideText, dimType, debugRole);
        }

        private void AddHorizontalDimFromXToSide(double fromX, Point3d to, string overrideText, DimensionType dimType, DimSide side, string debugRole = null)
        {
            AddHorizontalDimFromPointToSide(new Point3d(fromX, to.Y, 0.0), to, overrideText, dimType, side, debugRole: debugRole);
        }

        private void AddHorizontalDimFromPoint(Point3d from, Point3d to, string overrideText, DimensionType dimType, string debugRole = null)
        {
            AddHorizontalDimFromPointToSide(from, to, overrideText, dimType, DimSide.Bottom, debugRole: debugRole);
        }

        private void AddHorizontalDimFromPointToSide(
            Point3d from,
            Point3d to,
            string overrideText,
            DimensionType dimType,
            DimSide side,
            bool preferLocalBoundary = false,
            string debugOwner = null,
            string debugRole = null)
        {
            var span = Math.Abs(to.X - from.X);
            if (span <= _config.GeometryTolerance)
            {
                return;
            }

            var dim = new DeferredDim
            {
                Rotation = 0.0,
                XLine1 = from,
                XLine2 = to,
                OverrideText = overrideText ?? string.Empty,
                Span = span,
                DimType = dimType,
                UseSegmentedExtensionLines = true,
                PreferLocalBoundary = preferLocalBoundary,
                DebugOwner = debugOwner ?? string.Empty,
                DebugRole = debugRole ?? string.Empty
            };

            if (side == DimSide.Top)
            {
                _topDims.Add(dim);
                return;
            }

            _bottomDims.Add(dim);
        }

        private void AddVerticalDim(Point3d from, Point3d to, string overrideText, DimensionType dimType, string debugRole = null)
        {
            AddVerticalDimFromPoint(new Point3d(from.X, from.Y, from.Z), to, overrideText, dimType, debugRole);
        }

        private void AddVerticalDimToSide(
            Point3d from,
            Point3d to,
            string overrideText,
            DimensionType dimType,
            DimSide side,
            bool preferLocalBoundary = false,
            string debugOwner = null,
            string debugRole = null)
        {
            AddVerticalDimFromPointToSide(new Point3d(from.X, from.Y, from.Z), to, overrideText, dimType, side, preferLocalBoundary, debugOwner, debugRole);
        }

        private void AddVerticalDimFromY(double fromY, Point3d to, string overrideText, DimensionType dimType, string debugRole = null)
        {
            AddVerticalDimFromPoint(new Point3d(to.X, fromY, 0.0), to, overrideText, dimType, debugRole);
        }

        private void AddVerticalDimFromYToSide(double fromY, Point3d to, string overrideText, DimensionType dimType, DimSide side, string debugRole = null)
        {
            AddVerticalDimFromPointToSide(new Point3d(to.X, fromY, 0.0), to, overrideText, dimType, side, debugRole: debugRole);
        }

        private void AddVerticalDimFromPoint(Point3d from, Point3d to, string overrideText, DimensionType dimType, string debugRole = null)
        {
            AddVerticalDimFromPointToSide(from, to, overrideText, dimType, DimSide.Left, debugRole: debugRole);
        }

        private void AddVerticalDimFromPointToSide(
            Point3d from,
            Point3d to,
            string overrideText,
            DimensionType dimType,
            DimSide side,
            bool preferLocalBoundary = false,
            string debugOwner = null,
            string debugRole = null)
        {
            var span = Math.Abs(to.Y - from.Y);
            if (span <= _config.GeometryTolerance)
            {
                return;
            }

            var dim = new DeferredDim
            {
                Rotation = Math.PI / 2.0,
                XLine1 = from,
                XLine2 = new Point3d(to.X, to.Y, to.Z),
                OverrideText = overrideText ?? string.Empty,
                Span = span,
                DimType = dimType,
                UseSegmentedExtensionLines = true,
                PreferLocalBoundary = preferLocalBoundary,
                DebugOwner = debugOwner ?? string.Empty,
                DebugRole = debugRole ?? string.Empty
            };

            side = ChooseVerticalNormalDimensionSide(dim, side);
            if (side == DimSide.Right)
            {
                _rightDims.Add(dim);
                return;
            }

            _leftDims.Add(dim);
        }

        private bool IsSameHole(HoleFeature a, HoleFeature b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return DistanceSquared(a.Center, b.Center) <= _config.GeometryTolerance * _config.GeometryTolerance;
        }

        private static double DistanceSquared(Point3d a, Point3d b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}
