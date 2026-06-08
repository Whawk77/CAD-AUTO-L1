using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed class DebugAnnotationRenderer
    {
        private readonly Database _database;
        private readonly CadEntityWriter _writer;
        private readonly ObjectId _textStyleId;
        private readonly string _annotationLayer;

        public DebugAnnotationRenderer(
            Database database,
            CadEntityWriter writer,
            ObjectId textStyleId,
            string annotationLayer)
        {
            _database = database;
            _writer = writer;
            _textStyleId = textStyleId;
            _annotationLayer = annotationLayer;
        }

        public void AddDimensionLabel(
            string label,
            Point3d point,
            double textHeight,
            Autodesk.AutoCAD.Colors.Color color)
        {
            if (string.IsNullOrEmpty(label))
            {
                return;
            }

            var mtext = new MText();
            mtext.SetDatabaseDefaults(_database);
            mtext.Contents = label;
            mtext.TextHeight = textHeight;
            mtext.TextStyleId = _textStyleId;
            mtext.Location = point;
            mtext.Attachment = AttachmentPoint.MiddleCenter;
            mtext.Layer = _annotationLayer;
            mtext.Color = color;
            _writer.Append(mtext);
        }

        public void AddPointLabel(
            Point2d point,
            string label,
            short colorIndex,
            double xOffset,
            double yOffset,
            double textHeight)
        {
            if (string.IsNullOrEmpty(label))
            {
                return;
            }

            var labelPoint = new Point3d(point.X + xOffset, point.Y + yOffset, 0.0);
            var targetPoint = new Point3d(point.X, point.Y, 0.0);
            AddLine(labelPoint, targetPoint, colorIndex);

            var mtext = new MText();
            mtext.SetDatabaseDefaults(_database);
            mtext.Contents = label;
            mtext.TextHeight = textHeight;
            mtext.TextStyleId = _textStyleId;
            mtext.Location = labelPoint;
            mtext.Attachment = AttachmentPoint.MiddleCenter;
            mtext.Layer = _annotationLayer;
            mtext.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex);
            _writer.Append(mtext);
        }

        public void AddLine(Point3d start, Point3d end, short colorIndex)
        {
            var line = new Line(start, end);
            line.SetDatabaseDefaults(_database);
            line.Layer = _annotationLayer;
            line.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, colorIndex);
            _writer.Append(line);
        }
    }
}
