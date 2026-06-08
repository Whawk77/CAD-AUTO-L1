using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        private void AssignDebugIndexes(IList<PlacedDim> placedDims)
        {
            var groups = new Dictionary<string, List<int>>();
            for (int i = 0; i < placedDims.Count; i++)
            {
                var placed = placedDims[i];
                var key = GetDebugOwnerText(placed.Dim) + "|"
                    + GetDebugRoleText(placed.Dim) + "|"
                    + GetDebugBoundaryText(placed) + "|"
                    + GetDebugSideText(placed.Side);

                List<int> indexes;
                if (!groups.TryGetValue(key, out indexes))
                {
                    indexes = new List<int>();
                    groups[key] = indexes;
                }

                indexes.Add(i);
            }

            foreach (var indexes in groups.Values)
            {
                var ordered = indexes
                    .OrderBy(i => GetDebugIndexSortKey(placedDims[i]))
                    .ThenBy(i => placedDims[i].Dim.Span)
                    .ThenBy(i => i)
                    .ToList();

                for (int order = 0; order < ordered.Count; order++)
                {
                    var placed = placedDims[ordered[order]];
                    placed.Dim.DebugIndex = order + 1;
                    placedDims[ordered[order]] = placed;
                }
            }
        }

        private static double GetDebugIndexSortKey(PlacedDim placed)
        {
            switch (placed.Side)
            {
                case DimSide.Left:
                case DimSide.Right:
                    return -placed.DimLinePoint.Y;
                case DimSide.Bottom:
                case DimSide.Top:
                    return placed.DimLinePoint.X;
                default:
                    return 0.0;
            }
        }

        private void AddDimensionDebugLabel(PlacedDim placed, bool isHorizontal, double textHeight)
        {
            var label = BuildDimensionDebugLabel(placed);
            if (string.IsNullOrEmpty(label))
            {
                return;
            }

            var height = Math.Max(textHeight * 0.38, Scale(1.2));
            var offset = height * 2.2;
            var point = placed.DimLinePoint;
            if (isHorizontal)
            {
                point = placed.Side == DimSide.Top
                    ? new Point3d(point.X, point.Y + offset, 0.0)
                    : new Point3d(point.X, point.Y - offset, 0.0);
            }
            else
            {
                point = placed.Side == DimSide.Right
                    ? new Point3d(point.X + offset, point.Y, 0.0)
                    : new Point3d(point.X - offset, point.Y, 0.0);
            }

            _debugAnnotationRenderer.AddDimensionLabel(label, point, height, GetDebugLabelColor(placed.Dim));
        }

        private void AddTopPointDebugLabels(IEnumerable<StructurePoint> structurePoints, IEnumerable<IgnoredPoint> ignoredPoints)
        {
            var labels = new List<PointDebugLabel>();
            labels.AddRange(structurePoints.Select(point => new PointDebugLabel
            {
                Point = point.Point,
                Label = "SP:" + GetStructurePointSourceText(point.Source),
                ColorIndex = 4
            }));

            labels.AddRange(ignoredPoints.Select(point => new PointDebugLabel
            {
                Point = point.Point,
                Label = "IG:" + GetIgnoredPointReasonText(point.Reason),
                ColorIndex = 1
            }));

            AddPointDebugLabels(labels);
        }

        private void AddDirectionalPointDebugLabels(
            IEnumerable<Point2d> structurePoints,
            IEnumerable<Point2d> ignoredPoints,
            string structureLabel,
            string ignoredLabel)
        {
            var labels = new List<PointDebugLabel>();
            labels.AddRange(structurePoints.Select(point => new PointDebugLabel
            {
                Point = point,
                Label = structureLabel,
                ColorIndex = 4
            }));
            labels.AddRange(ignoredPoints.Select(point => new PointDebugLabel
            {
                Point = point,
                Label = ignoredLabel,
                ColorIndex = 1
            }));

            AddPointDebugLabels(labels);
        }

        private void AddPointDebugLabels(IEnumerable<PointDebugLabel> labels)
        {
            var ordered = labels
                .OrderBy(label => label.Point.X)
                .ThenBy(label => label.Point.Y)
                .ThenBy(label => label.Label)
                .ToList();
            if (ordered.Count == 0)
            {
                return;
            }

            var placed = new List<PointDebugLabelPlacement>();
            var textHeight = Math.Max(Scale(1.1), _dimScale * 1.1);

            foreach (var label in ordered)
            {
                var offset = ChoosePointDebugLabelOffset(label, placed, textHeight);
                var bounds = EstimatePointDebugLabelBounds(label.Point, label.Label, offset.X, offset.Y, textHeight);
                placed.Add(new PointDebugLabelPlacement { Bounds = bounds });
                AddPointDebugLabel(
                    label.Point,
                    label.Label,
                    label.ColorIndex,
                    offset.X,
                    offset.Y,
                    textHeight);
            }
        }

        private Point2d ChoosePointDebugLabelOffset(PointDebugLabel label, IList<PointDebugLabelPlacement> placed, double textHeight)
        {
            foreach (var offset in BuildPointDebugLabelOffsetCandidates(textHeight))
            {
                var bounds = EstimatePointDebugLabelBounds(label.Point, label.Label, offset.X, offset.Y, textHeight);
                if (!placed.Any(existing => PointDebugTextBoundsOverlap(existing.Bounds, bounds, Scale(0.8))))
                {
                    return offset;
                }
            }

            var fallbackY = Scale(10.0) + placed.Count * Scale(4.5);
            return new Point2d(Scale(18.0), fallbackY);
        }

        private IEnumerable<Point2d> BuildPointDebugLabelOffsetCandidates(double textHeight)
        {
            var horizontal = Math.Max(Scale(16.0), textHeight * 8.0);
            var vertical = Math.Max(Scale(8.0), textHeight * 5.0);
            var verticalStep = Math.Max(Scale(4.0), textHeight * 2.8);
            var horizontalStep = Math.Max(Scale(5.0), textHeight * 3.0);
            for (int level = 0; level < 6; level++)
            {
                var y = vertical + level * verticalStep;
                var x = horizontal + (level % 2) * horizontalStep;
                yield return new Point2d(-x, y);
                yield return new Point2d(x, y);
                yield return new Point2d(-x * 0.65, y + verticalStep * 0.5);
                yield return new Point2d(x * 0.65, y + verticalStep * 0.5);
            }
        }

        private TextBounds EstimatePointDebugLabelBounds(Point2d point, string label, double xOffset, double yOffset, double textHeight)
        {
            var width = Math.Max(textHeight * 4.0, (label ?? string.Empty).Length * textHeight * 0.75);
            var centerX = point.X + xOffset;
            var centerY = point.Y + yOffset;
            return new TextBounds
            {
                MinX = centerX - width * 0.5,
                MaxX = centerX + width * 0.5,
                MinY = centerY - textHeight * 0.7,
                MaxY = centerY + textHeight * 0.7
            };
        }

        private static bool PointDebugTextBoundsOverlap(TextBounds a, TextBounds b, double gap)
        {
            return a.MinX <= b.MaxX + gap
                && a.MaxX + gap >= b.MinX
                && a.MinY <= b.MaxY + gap
                && a.MaxY + gap >= b.MinY;
        }

        private void AddPointDebugLabel(Point2d point, string label, short colorIndex, double xOffset, double yOffset, double textHeight)
        {
            if (string.IsNullOrEmpty(label))
            {
                return;
            }

            _debugAnnotationRenderer.AddPointLabel(point, label, colorIndex, xOffset, yOffset, textHeight);
        }

        private void AddDebugLine(Point3d start, Point3d end, short colorIndex)
        {
            _debugAnnotationRenderer.AddLine(start, end, colorIndex);
        }

        private static string GetStructurePointSourceText(string source)
        {
            switch (source)
            {
                case "VerticalTopMost":
                    return "VTM";
                case "TopInclinedEndpoint":
                    return "TIE";
                case "TopSlopeEndpoint":
                    return "TSE";
                default:
                    return string.IsNullOrEmpty(source) ? "UNK" : source;
            }
        }

        private static string GetIgnoredPointReasonText(string reason)
        {
            switch (reason)
            {
                case "TopExtensionCrossesOutline":
                    return "XOUT";
                case "TopDirectional45Endpoint":
                    return "D45";
                case "TopInnerGrooveSharedEndpoint":
                    return "IGR";
                case "BottomDirectional45Endpoint":
                    return "D45B";
                case "BottomInnerGrooveSharedEndpoint":
                    return "IGRB";
                default:
                    return string.IsNullOrEmpty(reason) ? "UNK" : reason;
            }
        }

        private string BuildDimensionDebugLabel(PlacedDim placed)
        {
            var owner = GetDebugOwnerText(placed.Dim);
            var role = GetDebugRoleText(placed.Dim);
            if (placed.Dim.DebugIndex > 0)
            {
                role += placed.Dim.DebugIndex.ToString(CultureInfo.InvariantCulture);
            }

            return owner + "|" + role + "|" + GetDebugBoundaryText(placed) + "|" + GetDebugSideText(placed.Side);
        }

        private static string GetDebugBoundaryText(PlacedDim placed)
        {
            return placed.UsesLocalBoundary ? "LB" : "GB";
        }

        private string GetDebugOwnerText(DeferredDim dim)
        {
            if (!string.IsNullOrEmpty(dim.DebugOwner))
            {
                return dim.DebugOwner;
            }

            if (dim.DimType == DimensionType.DatumHoleLocationX || dim.DimType == DimensionType.DatumHoleLocationY)
            {
                return "DAT";
            }

            return "GEN";
        }

        private string GetDebugRoleText(DeferredDim dim)
        {
            var role = string.IsNullOrEmpty(dim.DebugRole) ? dim.DimType.ToString() : dim.DebugRole;
            switch (role)
            {
                case "PinDistance":
                    return "PD";
                case "PinGroupDistance":
                    return "GD";
                case "FunctionalHole":
                    return "FH";
                case "LooseHole":
                    return "LH";
                case "DatumX":
                    return "DX";
                case "DatumY":
                    return "DY";
                case "DatumHoleLocationX":
                    return "DX";
                case "DatumHoleLocationY":
                    return "DY";
                case "OverallWidth":
                    return "OW";
                case "OverallHeight":
                    return "OH";
                case "HoleLocation":
                    return "HL";
                case "Normal":
                    return "N";
                case "TopStructWidth":
                    return "TSW";
                case "BottomStructWidth":
                    return "BSW";
                case "LeftStructHeight":
                    return "LSH";
                case "RightStructHeight":
                    return "RSH";
                case "SlotCenter":
                    return "SC";
                case "SlotChainH":
                    return "SCH";
                case "SlotChainV":
                    return "SCV";
                case "SlotDatumH":
                    return "SDH";
                case "SlotDatumV":
                    return "SDV";
                case "SlotPinRef":
                    return "SPR";
                case "SingleArcSlotDatum":
                    return "SAS";
                default:
                    return role;
            }
        }

        private static string GetDebugSideText(DimSide side)
        {
            switch (side)
            {
                case DimSide.Bottom:
                    return "B";
                case DimSide.Top:
                    return "T";
                case DimSide.Left:
                    return "L";
                case DimSide.Right:
                    return "R";
                default:
                    return side.ToString();
            }
        }

        private Autodesk.AutoCAD.Colors.Color GetDebugLabelColor(DeferredDim dim)
        {
            short colorIndex;
            switch (GetDebugRoleText(dim))
            {
                case "PD":
                    colorIndex = 1;
                    break;
                case "GD":
                    colorIndex = 6;
                    break;
                case "FH":
                    colorIndex = 4;
                    break;
                case "LH":
                    colorIndex = 2;
                    break;
                case "OW":
                case "OH":
                    colorIndex = 3;
                    break;
                default:
                    colorIndex = 8;
                    break;
            }

            return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex);
        }
    }
}
