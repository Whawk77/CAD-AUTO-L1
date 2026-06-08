using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadAuto.Core.Rules;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed class CadEntityWriter
    {
        private readonly Database _database;
        private readonly Transaction _transaction;
        private readonly BlockTableRecord _space;
        private readonly DimensionRuleConfig _config;
        private readonly ObjectId _dimStyleId;
        private readonly double _dimScale;
        private readonly string _annotationLayer;
        private readonly bool _appendToDatabase;
        private readonly string _groupId;
        private readonly System.Collections.Generic.IList<Entity> _previewEntities;

        public CadEntityWriter(
            Database database,
            Transaction transaction,
            BlockTableRecord space,
            DimensionRuleConfig config,
            ObjectId dimStyleId,
            double dimScale,
            string annotationLayer,
            bool appendToDatabase,
            string groupId,
            System.Collections.Generic.IList<Entity> previewEntities)
        {
            _database = database;
            _transaction = transaction;
            _space = space;
            _config = config;
            _dimStyleId = dimStyleId;
            _dimScale = dimScale <= 0.0 ? 1.0 : dimScale;
            _annotationLayer = annotationLayer;
            _appendToDatabase = appendToDatabase;
            _groupId = groupId;
            _previewEntities = previewEntities;
        }

        public void AddRotatedDimension(
            double rotation,
            Point3d xLine1,
            Point3d xLine2,
            Point3d dimLinePoint,
            string overrideText,
            bool useSegmentedExtensionLines,
            bool useCustomTextPosition = false,
            Point3d customTextPosition = default(Point3d))
        {
            var dimension = new RotatedDimension(
                rotation,
                xLine1,
                xLine2,
                dimLinePoint,
                overrideText ?? string.Empty,
                _dimStyleId);
            dimension.SetDatabaseDefaults(_database);
            dimension.Layer = _annotationLayer;
            dimension.DimensionStyle = _dimStyleId;
            if (useSegmentedExtensionLines)
            {
                dimension.Dimse1 = true;
                dimension.Dimse2 = true;
            }

            Append(dimension);
            if (useCustomTextPosition)
            {
                dimension.UsingDefaultTextPosition = false;
                dimension.TextPosition = customTextPosition;
                if (_appendToDatabase)
                {
                    dimension.RecomputeDimensionBlock(true);
                }
            }
        }

        public ObjectId GetDimStyleTextStyle(ObjectId dimStyleId)
        {
            var record = _transaction.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
            if (record != null && !record.Dimtxsty.IsNull)
            {
                return record.Dimtxsty;
            }

            return _database.Textstyle;
        }

        public string GetCurrentLayerName()
        {
            var currentLayer = _transaction.GetObject(_database.Clayer, OpenMode.ForRead) as LayerTableRecord;
            return currentLayer != null ? currentLayer.Name : _annotationLayer;
        }

        public Autodesk.AutoCAD.Colors.Color GetCurrentEntityColor()
        {
            var colorText = Convert.ToString(Application.GetSystemVariable("CECOLOR"));
            if (string.IsNullOrWhiteSpace(colorText))
            {
                return _database.Cecolor;
            }

            colorText = colorText.Trim();
            if (string.Equals(colorText, "BYLAYER", StringComparison.OrdinalIgnoreCase))
            {
                return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
            }

            if (string.Equals(colorText, "BYBLOCK", StringComparison.OrdinalIgnoreCase))
            {
                return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByBlock, 0);
            }

            short colorIndex;
            if (short.TryParse(colorText, out colorIndex))
            {
                return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex);
            }

            switch (colorText.ToUpperInvariant())
            {
                case "RED":
                case "红":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 1);
                case "YELLOW":
                case "黄":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 2);
                case "GREEN":
                case "绿":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 3);
                case "CYAN":
                case "青":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 4);
                case "BLUE":
                case "蓝":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 5);
                case "MAGENTA":
                case "洋红":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 6);
                case "WHITE":
                case "白":
                    return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, 7);
                default:
                    return _database.Cecolor;
            }
        }

        public Autodesk.AutoCAD.Colors.Color GetDimStyleTextColor(ObjectId dimStyleId)
        {
            var record = _transaction.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
            if (record != null && record.Dimclrt != null)
            {
                return record.Dimclrt;
            }

            return Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByLayer, 256);
        }

        public double GetDimStyleTextHeight(ObjectId dimStyleId)
        {
            var record = _transaction.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
            if (record != null && record.Dimtxt > _config.GeometryTolerance)
            {
                return record.Dimtxt * _dimScale;
            }

            return Scale(_config.TextHeight);
        }

        public int GetDimStyleLinearPrecision(ObjectId dimStyleId)
        {
            var record = _transaction.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
            if (record == null)
            {
                return 3;
            }

            return Math.Max(0, Math.Min(8, record.Dimdec));
        }

        public void Append(Entity entity)
        {
            if (_appendToDatabase)
            {
                _space.AppendEntity(entity);
                _transaction.AddNewlyCreatedDBObject(entity, true);
                AnnotationMetadata.Mark(entity, _groupId);
            }
            else
            {
                _previewEntities.Add(entity);
            }
        }

        public double Scale(double value)
        {
            return value * _dimScale;
        }
    }
}
