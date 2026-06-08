using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;
using CadAuto.Core.Planning;

namespace CadAuto.CadAdapter.Rendering
{
    internal sealed class DimensionPlanCadItem
    {
        public double Rotation { get; set; }
        public Point3d FirstPoint { get; set; }
        public Point3d SecondPoint { get; set; }
        public string OverrideText { get; set; }
        public double Span { get; set; }
        public DimensionType DimensionType { get; set; }
        public bool UseSegmentedExtensionLines { get; set; }
        public bool ForceOuterLevel { get; set; }
        public int ChainId { get; set; }
        public bool PreferLocalBoundary { get; set; }
        public string DebugOwner { get; set; }
        public string DebugRole { get; set; }
        public DimensionSide Side { get; set; }
    }

    internal sealed class DimensionPlanCadMapper
    {
        public IEnumerable<DimensionPlanCadItem> Map(DimensionPlan plan)
        {
            if (plan == null)
            {
                yield break;
            }

            foreach (var dimension in plan.Dimensions)
            {
                var item = Map(dimension);
                if (item != null)
                {
                    yield return item;
                }
            }
        }

        private static DimensionPlanCadItem Map(PlannedDimension dimension)
        {
            if (dimension == null)
            {
                return null;
            }

            if (dimension.Orientation != DimensionOrientation.Horizontal
                && dimension.Orientation != DimensionOrientation.Vertical)
            {
                return null;
            }

            return new DimensionPlanCadItem
            {
                Rotation = dimension.Orientation == DimensionOrientation.Horizontal ? 0.0 : Math.PI / 2.0,
                FirstPoint = new Point3d(dimension.FirstPoint.X, dimension.FirstPoint.Y, 0.0),
                SecondPoint = new Point3d(dimension.SecondPoint.X, dimension.SecondPoint.Y, 0.0),
                OverrideText = dimension.OverrideText ?? string.Empty,
                Span = GetSpan(dimension),
                DimensionType = ToDimensionType(dimension.Kind),
                ForceOuterLevel = dimension.ForceOuterLevel,
                UseSegmentedExtensionLines = dimension.UseSegmentedExtensionLines,
                ChainId = dimension.ChainId,
                PreferLocalBoundary = dimension.PreferLocalBoundary,
                DebugOwner = dimension.DebugOwner,
                DebugRole = dimension.DebugRole,
                Side = dimension.Side
            };
        }

        private static double GetSpan(PlannedDimension dimension)
        {
            return dimension.Orientation == DimensionOrientation.Horizontal
                ? Math.Abs(dimension.SecondPoint.X - dimension.FirstPoint.X)
                : Math.Abs(dimension.SecondPoint.Y - dimension.FirstPoint.Y);
        }

        private static DimensionType ToDimensionType(DimensionKind kind)
        {
            switch (kind)
            {
                case DimensionKind.OverallWidth:
                    return DimensionType.OverallWidth;
                case DimensionKind.OverallHeight:
                    return DimensionType.OverallHeight;
                case DimensionKind.HoleDiameter:
                    return DimensionType.HoleDiameter;
                case DimensionKind.PinDistance:
                    return DimensionType.PinDistance;
                case DimensionKind.PinGroupDistance:
                    return DimensionType.PinGroupDistance;
                case DimensionKind.HoleLocation:
                    return DimensionType.HoleLocation;
                case DimensionKind.DatumHoleLocationX:
                    return DimensionType.DatumHoleLocationX;
                case DimensionKind.DatumHoleLocationY:
                    return DimensionType.DatumHoleLocationY;
                default:
                    return DimensionType.Normal;
            }
        }
    }
}
