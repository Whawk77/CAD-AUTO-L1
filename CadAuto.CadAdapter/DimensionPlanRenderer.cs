using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;

namespace CadAuto.CadAdapter
{
    public sealed class DimensionPlanRenderer
    {
        private readonly Database _database;
        private readonly Transaction _transaction;
        private readonly BlockTableRecord _space;
        private readonly ObjectId _dimStyleId;
        private readonly double _firstOffset;
        private readonly string _layerName;

        public DimensionPlanRenderer(
            Database database,
            Transaction transaction,
            BlockTableRecord space,
            ObjectId dimStyleId,
            double firstOffset,
            string layerName = null)
        {
            _database = database;
            _transaction = transaction;
            _space = space;
            _dimStyleId = dimStyleId;
            _firstOffset = firstOffset;
            _layerName = layerName;
        }

        public void Render(DimensionPlan plan)
        {
            Render(plan, null);
        }

        public void Render(DimensionPlan plan, OutlineFeature2D outline)
        {
            if (plan == null)
            {
                return;
            }

            foreach (var item in CreateLayeredRenderItems(plan, outline))
            {
                Render(item.Dimension, item.Level, outline);
            }
        }

        private void Render(PlannedDimension dimension, int level, OutlineFeature2D outline)
        {
            if (dimension.Orientation != DimensionOrientation.Horizontal
                && dimension.Orientation != DimensionOrientation.Vertical)
            {
                // TODO: Diameter, radius, and leader output need adapter-specific style and jig policy.
                return;
            }

            var first = AutoCadGeometryConverter.ToCad(dimension.FirstPoint);
            var second = AutoCadGeometryConverter.ToCad(dimension.SecondPoint);
            var dimLine = GetDimensionLinePoint(dimension, level, outline);

            var cadDimension = new RotatedDimension(
                dimension.Orientation == DimensionOrientation.Horizontal ? 0.0 : Math.PI / 2.0,
                first,
                second,
                dimLine,
                dimension.OverrideText ?? string.Empty,
                _dimStyleId);
            if (!string.IsNullOrEmpty(_layerName))
            {
                cadDimension.Layer = _layerName;
            }

            _space.AppendEntity(cadDimension);
            _transaction.AddNewlyCreatedDBObject(cadDimension, true);

            // Keep the field used so construction errors surface during compiler analysis.
            if (_database == null)
            {
                throw new InvalidOperationException("Database is required.");
            }
        }

        private List<RenderItem> CreateLayeredRenderItems(DimensionPlan plan, OutlineFeature2D outline)
        {
            var renderItems = new List<RenderItem>();
            foreach (var sideGroup in plan.Dimensions
                .Where(IsRenderableLinearDimension)
                .GroupBy(d => d.Side))
            {
                var level = 0;
                foreach (var dimension in sideGroup.OrderBy(GetLayerPriority).ThenBy(GetSpan).ThenBy(d => d.DebugRole ?? string.Empty))
                {
                    renderItems.Add(new RenderItem(dimension, level));
                    level++;
                }
            }

            return renderItems;
        }

        private Autodesk.AutoCAD.Geometry.Point3d GetDimensionLinePoint(
            PlannedDimension dimension,
            int level,
            OutlineFeature2D outline)
        {
            var first = AutoCadGeometryConverter.ToCad(dimension.FirstPoint);
            var second = AutoCadGeometryConverter.ToCad(dimension.SecondPoint);
            var offset = _firstOffset * (level + 1);

            if (dimension.Orientation == DimensionOrientation.Horizontal)
            {
                var y = dimension.Side == DimensionSide.Top
                    ? GetTopBase(outline, first, second) + offset
                    : GetBottomBase(outline, first, second) - offset;
                return new Autodesk.AutoCAD.Geometry.Point3d((first.X + second.X) * 0.5, y, 0.0);
            }

            var x = dimension.Side == DimensionSide.Right
                ? GetRightBase(outline, first, second) + offset
                : GetLeftBase(outline, first, second) - offset;
            return new Autodesk.AutoCAD.Geometry.Point3d(x, (first.Y + second.Y) * 0.5, 0.0);
        }

        private static bool IsRenderableLinearDimension(PlannedDimension dimension)
        {
            return dimension.Orientation == DimensionOrientation.Horizontal
                || dimension.Orientation == DimensionOrientation.Vertical;
        }

        private static int GetLayerPriority(PlannedDimension dimension)
        {
            if (dimension.ForceOuterLevel
                || dimension.Kind == DimensionKind.OverallWidth
                || dimension.Kind == DimensionKind.OverallHeight)
            {
                return 1000;
            }

            if (dimension.Kind == DimensionKind.PinGroupDistance)
            {
                return 800;
            }

            if (dimension.Kind == DimensionKind.PinDistance
                || dimension.Kind == DimensionKind.DatumHoleLocationX
                || dimension.Kind == DimensionKind.DatumHoleLocationY)
            {
                return 500;
            }

            return 100;
        }

        private static double GetSpan(PlannedDimension dimension)
        {
            return dimension.Orientation == DimensionOrientation.Horizontal
                ? Math.Abs(dimension.SecondPoint.X - dimension.FirstPoint.X)
                : Math.Abs(dimension.SecondPoint.Y - dimension.FirstPoint.Y);
        }

        private static double GetTopBase(
            OutlineFeature2D outline,
            Autodesk.AutoCAD.Geometry.Point3d first,
            Autodesk.AutoCAD.Geometry.Point3d second)
        {
            return outline == null ? Math.Max(first.Y, second.Y) : outline.MaxY;
        }

        private static double GetBottomBase(
            OutlineFeature2D outline,
            Autodesk.AutoCAD.Geometry.Point3d first,
            Autodesk.AutoCAD.Geometry.Point3d second)
        {
            return outline == null ? Math.Min(first.Y, second.Y) : outline.MinY;
        }

        private static double GetRightBase(
            OutlineFeature2D outline,
            Autodesk.AutoCAD.Geometry.Point3d first,
            Autodesk.AutoCAD.Geometry.Point3d second)
        {
            return outline == null ? Math.Max(first.X, second.X) : outline.MaxX;
        }

        private static double GetLeftBase(
            OutlineFeature2D outline,
            Autodesk.AutoCAD.Geometry.Point3d first,
            Autodesk.AutoCAD.Geometry.Point3d second)
        {
            return outline == null ? Math.Min(first.X, second.X) : outline.MinX;
        }

        private sealed class RenderItem
        {
            public RenderItem(PlannedDimension dimension, int level)
            {
                Dimension = dimension;
                Level = level;
            }

            public PlannedDimension Dimension { get; private set; }
            public int Level { get; private set; }
        }
    }
}
