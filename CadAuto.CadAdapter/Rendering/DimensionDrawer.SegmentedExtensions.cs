using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.Geometry;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private bool HasExtensionLineTextConflict(PlacedDim placed, IList<PlacedDim> allPlacedDims)
        {
            foreach (var text in allPlacedDims.Select(p => p.TextBounds))
            {
                if (ExtensionLineCrossesText(placed, placed.Dim.XLine1, text) ||
                    ExtensionLineCrossesText(placed, placed.Dim.XLine2, text))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ExtensionLineCrossesText(PlacedDim placed, Point3d featurePoint, TextBounds text)
        {
            var tol = _config.GeometryTolerance;
            if (placed.Side == DimSide.Bottom || placed.Side == DimSide.Top)
            {
                var x = featurePoint.X;
                var y1 = Math.Min(featurePoint.Y, placed.DimLinePoint.Y);
                var y2 = Math.Max(featurePoint.Y, placed.DimLinePoint.Y);
                return x >= text.MinX - tol && x <= text.MaxX + tol &&
                    y1 <= text.MaxY + tol && y2 >= text.MinY - tol;
            }

            var y = featurePoint.Y;
            var x1 = Math.Min(featurePoint.X, placed.DimLinePoint.X);
            var x2 = Math.Max(featurePoint.X, placed.DimLinePoint.X);
            return y >= text.MinY - tol && y <= text.MaxY + tol &&
                x1 <= text.MaxX + tol && x2 >= text.MinX - tol;
        }

        private void DrawSegmentedExtensionLines(PlacedDim placed, IList<PlacedDim> allPlacedDims, double textHeight)
        {
            DrawSegmentedExtensionLine(placed, placed.Dim.XLine1, allPlacedDims, textHeight);
            DrawSegmentedExtensionLine(placed, placed.Dim.XLine2, allPlacedDims, textHeight);
        }

        private void DrawSegmentedExtensionLine(PlacedDim placed, Point3d featurePoint, IList<PlacedDim> allPlacedDims, double textHeight)
        {
            var breakLength = Math.Max(textHeight * 0.9, Scale(_config.ArrowSize * 0.75));
            var ranges = GetBreakRangesForExtensionLine(placed, featurePoint, allPlacedDims, breakLength);
            var dimPoint = GetExtensionLineEndPoint(placed, featurePoint);
            if (placed.Side == DimSide.Bottom || placed.Side == DimSide.Top)
            {
                AddSegmentedLine(featurePoint.X, featurePoint.Y, dimPoint.Y, ranges, vertical: true);
            }
            else
            {
                AddSegmentedLine(featurePoint.Y, featurePoint.X, dimPoint.X, ranges, vertical: false);
            }
        }

        private Point3d GetExtensionLineEndPoint(PlacedDim placed, Point3d featurePoint)
        {
            if (placed.Side == DimSide.Bottom || placed.Side == DimSide.Top)
            {
                return new Point3d(featurePoint.X, placed.DimLinePoint.Y, 0.0);
            }

            return new Point3d(placed.DimLinePoint.X, featurePoint.Y, 0.0);
        }

        private List<(double A, double B)> GetBreakRangesForExtensionLine(
            PlacedDim placed,
            Point3d featurePoint,
            IList<PlacedDim> allPlacedDims,
            double breakLength)
        {
            var dimPoint = GetExtensionLineEndPoint(placed, featurePoint);
            var candidates = allPlacedDims
                .Select(p => p.TextBounds)
                .Where(text => ExtensionLineCrossesText(placed, featurePoint, text))
                .Select(text => ToBreakRangeCandidate(placed, featurePoint, dimPoint, text, breakLength))
                .Where(r => r.B > r.A + _config.GeometryTolerance)
                .OrderBy(r => r.DistanceToFeature)
                .ThenBy(r => r.Length)
                .ToList();

            if (candidates.Count == 0)
            {
                return new List<(double A, double B)>();
            }

            var best = candidates[0];
            return new List<(double A, double B)> { (best.A, best.B) };
        }

        private (double A, double B, double DistanceToFeature, double Length) ToBreakRangeCandidate(
            PlacedDim placed,
            Point3d featurePoint,
            Point3d dimPoint,
            TextBounds text,
            double breakLength)
        {
            double featureVariable;
            double dimVariable;
            double textCenter;

            if (placed.Side == DimSide.Bottom || placed.Side == DimSide.Top)
            {
                featureVariable = featurePoint.Y;
                dimVariable = dimPoint.Y;
                textCenter = (text.MinY + text.MaxY) / 2.0;
            }
            else
            {
                featureVariable = featurePoint.X;
                dimVariable = dimPoint.X;
                textCenter = (text.MinX + text.MaxX) / 2.0;
            }

            var min = Math.Min(featureVariable, dimVariable);
            var max = Math.Max(featureVariable, dimVariable);
            var center = Math.Max(min, Math.Min(max, textCenter));
            var half = breakLength / 2.0;
            var a = Math.Max(min, center - half);
            var b = Math.Min(max, center + half);
            return (a, b, Math.Abs(center - featureVariable), b - a);
        }

        private void AddSegmentedLine(double fixedCoord, double startVariable, double endVariable, IList<(double A, double B)> breakRanges, bool vertical)
        {
            var ranges = breakRanges
                .Select(r => Tuple.Create(r.A, r.B))
                .ToList();
            _extensionLineRenderer.AddSegmentedLine(
                fixedCoord,
                startVariable,
                endVariable,
                ranges,
                vertical,
                _config.GeometryTolerance);
        }
    }
}
