using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using CadAuto.Core.Rules;

namespace CadAuto.CadAdapter.Rendering
{
    public enum CornerCalloutJigResult
    {
        Picked,
        Skip,
        Cancel
    }

    public sealed class CornerCalloutRenderer
    {
        private readonly Database _database;
        private readonly CadEntityWriter _writer;
        private readonly DimensionRuleConfig _config;
        private readonly ObjectId _diameterCalloutDimStyleId;
        private readonly bool _appendToDatabase;
        private readonly string _annotationLayer;

        public CornerCalloutRenderer(
            Database database,
            CadEntityWriter writer,
            DimensionRuleConfig config,
            ObjectId diameterCalloutDimStyleId,
            bool appendToDatabase,
            string annotationLayer)
        {
            _database = database;
            _writer = writer;
            _config = config;
            _diameterCalloutDimStyleId = diameterCalloutDimStyleId;
            _appendToDatabase = appendToDatabase;
            _annotationLayer = annotationLayer;
        }

        public CornerCalloutJigResult PromptLeaderPoint(
            Editor editor,
            Point2d target,
            string text,
            double textHeight,
            out Point3d textPoint)
        {
            textPoint = Point3d.Origin;
            var arrowPoint = new Point3d(target.X, target.Y, 0.0);
            var jig = new CornerFeatureLeaderJig(arrowPoint, text, textHeight);
            var result = editor.Drag(jig);
            if (result.Status == PromptStatus.OK)
            {
                textPoint = jig.TextPoint;
                return CornerCalloutJigResult.Picked;
            }

            if (jig.SkipRequested || result.Status == PromptStatus.None)
            {
                return CornerCalloutJigResult.Skip;
            }

            return CornerCalloutJigResult.Cancel;
        }

        public void AddLeader(Point3d arrowPoint, Point3d landingPoint, Point3d textPoint, string text)
        {
            var mtext = new MText();
            mtext.SetDatabaseDefaults(_database);
            mtext.Contents = text;
            mtext.TextHeight = _writer.GetDimStyleTextHeight(_diameterCalloutDimStyleId);
            mtext.TextStyleId = _writer.GetDimStyleTextStyle(_diameterCalloutDimStyleId);
            mtext.Location = textPoint;
            var textIsLeft = textPoint.X < landingPoint.X - _config.GeometryTolerance
                || (Math.Abs(textPoint.X - landingPoint.X) <= _config.GeometryTolerance
                    && textPoint.X < arrowPoint.X);
            mtext.Attachment = textIsLeft
                ? AttachmentPoint.MiddleRight
                : AttachmentPoint.MiddleLeft;
            mtext.Layer = _annotationLayer;
            mtext.Color = _writer.GetDimStyleTextColor(_diameterCalloutDimStyleId);
            _writer.Append(mtext);

            if (!_appendToDatabase)
            {
                var previewLine = new Line(arrowPoint, landingPoint);
                previewLine.SetDatabaseDefaults(_database);
                previewLine.Layer = _writer.GetCurrentLayerName();
                previewLine.Color = _writer.GetCurrentEntityColor();
                _writer.Append(previewLine);
                return;
            }

            var leader = new Leader();
            leader.SetDatabaseDefaults(_database);
            leader.Layer = _writer.GetCurrentLayerName();
            leader.DimensionStyle = _diameterCalloutDimStyleId;
            leader.Color = _writer.GetCurrentEntityColor();
            leader.AppendVertex(arrowPoint);
            leader.AppendVertex(landingPoint);
            _writer.Append(leader);
            leader.Annotation = mtext.ObjectId;
            leader.EvaluateLeader();
            leader.Color = _writer.GetCurrentEntityColor();
        }

        public void AddRadialDimension(
            Point3d centerPoint,
            Point3d chordPoint,
            Point3d textPoint,
            double minimumLeaderLength,
            string text,
            bool useCustomTextPosition)
        {
            var leaderLength = Math.Max(chordPoint.DistanceTo(textPoint), minimumLeaderLength);
            var dimension = new RadialDimension(
                centerPoint,
                chordPoint,
                leaderLength,
                text,
                _diameterCalloutDimStyleId);
            dimension.SetDatabaseDefaults(_database);
            dimension.Layer = _annotationLayer;
            dimension.DimensionStyle = _diameterCalloutDimStyleId;
            dimension.DimensionText = text;
            if (useCustomTextPosition)
            {
                dimension.UsingDefaultTextPosition = false;
                dimension.TextPosition = textPoint;
            }

            _writer.Append(dimension);
            if (useCustomTextPosition && _appendToDatabase)
            {
                dimension.RecomputeDimensionBlock(true);
            }
        }

        private sealed class CornerFeatureLeaderJig : DrawJig
        {
            private readonly Point3d _arrowPoint;
            private readonly string _text;
            private readonly double _textHeight;
            private readonly double _redrawTolerance;
            private readonly double _textWidth;
            private readonly string _promptMessage;
            private Point3d _textPoint;

            public CornerFeatureLeaderJig(Point3d arrowPoint, string text, double textHeight)
            {
                _arrowPoint = arrowPoint;
                _text = text ?? string.Empty;
                _textHeight = textHeight;
                _redrawTolerance = Math.Max(textHeight * 0.18, 1e-6);
                _textWidth = Math.Max(_text.Length, 2) * _textHeight * 0.65;
                _promptMessage = "\nMove corner callout " + _text + ", click to place, Enter to skip: ";
                _textPoint = arrowPoint + new Vector3d(textHeight * 6.0, textHeight * 4.0, 0.0);
            }

            public Point3d TextPoint
            {
                get { return _textPoint; }
            }

            public bool SkipRequested { get; private set; }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var options = new JigPromptPointOptions(_promptMessage);
                options.UserInputControls =
                    UserInputControls.Accept3dCoordinates
                    | UserInputControls.NullResponseAccepted;
                var result = prompts.AcquirePoint(options);
                if (result.Status == PromptStatus.None)
                {
                    SkipRequested = true;
                    return SamplerStatus.Cancel;
                }

                if (result.Status != PromptStatus.OK)
                {
                    return SamplerStatus.Cancel;
                }

                if (result.Value.DistanceTo(_textPoint) <= _redrawTolerance)
                {
                    return SamplerStatus.NoChange;
                }

                _textPoint = result.Value;
                return SamplerStatus.OK;
            }

            protected override bool WorldDraw(WorldDraw draw)
            {
                if (draw == null)
                {
                    return false;
                }

                var direction = _textPoint - _arrowPoint;
                if (direction.Length <= 1e-9)
                {
                    direction = Vector3d.XAxis;
                }

                direction = direction.GetNormal();
                var normal = Vector3d.ZAxis;
                var textDirection = Vector3d.XAxis;
                var textStartsLeft = _textPoint.X < _arrowPoint.X;
                var textOrigin = textStartsLeft
                    ? _textPoint - textDirection * _textWidth
                    : _textPoint;

                draw.Geometry.WorldLine(_arrowPoint, _textPoint);
                draw.Geometry.Text(textOrigin, normal, textDirection, _textHeight, 1.0, 0.0, _text);
                return true;
            }
        }
    }
}
