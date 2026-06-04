using System;
using Autodesk.AutoCAD.DatabaseServices;
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

        public DimensionPlanRenderer(
            Database database,
            Transaction transaction,
            BlockTableRecord space,
            ObjectId dimStyleId,
            double firstOffset)
        {
            _database = database;
            _transaction = transaction;
            _space = space;
            _dimStyleId = dimStyleId;
            _firstOffset = firstOffset;
        }

        public void Render(DimensionPlan plan)
        {
            if (plan == null)
            {
                return;
            }

            foreach (var dimension in plan.Dimensions)
            {
                Render(dimension);
            }
        }

        private void Render(PlannedDimension dimension)
        {
            if (dimension.Orientation != DimensionOrientation.Horizontal
                && dimension.Orientation != DimensionOrientation.Vertical)
            {
                // TODO: Diameter, radius, and leader output need adapter-specific style and jig policy.
                return;
            }

            var first = AutoCadGeometryConverter.ToCad(dimension.FirstPoint);
            var second = AutoCadGeometryConverter.ToCad(dimension.SecondPoint);
            var dimLine = first;

            if (dimension.Orientation == DimensionOrientation.Horizontal)
            {
                var offset = dimension.Side == DimensionSide.Top ? _firstOffset : -_firstOffset;
                dimLine = new Autodesk.AutoCAD.Geometry.Point3d((first.X + second.X) * 0.5, first.Y + offset, 0.0);
            }
            else
            {
                var offset = dimension.Side == DimensionSide.Right ? _firstOffset : -_firstOffset;
                dimLine = new Autodesk.AutoCAD.Geometry.Point3d(first.X + offset, (first.Y + second.Y) * 0.5, 0.0);
            }

            var cadDimension = new RotatedDimension(
                dimension.Orientation == DimensionOrientation.Horizontal ? 0.0 : Math.PI / 2.0,
                first,
                second,
                dimLine,
                dimension.OverrideText ?? string.Empty,
                _dimStyleId);

            _space.AppendEntity(cadDimension);
            _transaction.AddNewlyCreatedDBObject(cadDimension, true);

            // Keep the field used so construction errors surface during compiler analysis.
            if (_database == null)
            {
                throw new InvalidOperationException("Database is required.");
            }
        }
    }
}
