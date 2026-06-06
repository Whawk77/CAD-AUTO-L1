using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace AutoFixtureDim
{
    public static class NativeDiameterDimensioner
    {
        private static readonly string PinRoughnessBlockName =
            "CadAider_" + "\u56FD\u6807\u7C97\u7CD9\u5EA616\u4E0B";

        public static void PromptDiameterDimensions(
            Document document,
            IList<IList<HoleFeature>> diameterGroups,
            DimensionRuleConfig config,
            string annotationLayer,
            ObjectId diameterDimStyleId,
            string groupId)
        {
            if (document == null || diameterGroups == null || diameterGroups.Count == 0)
            {
                return;
            }

            Database db = document.Database;
            Editor editor = document.Editor;

            foreach (var group in diameterGroups.Where(g => g.Count > 0))
            {
                var ordered = group.OrderBy(h => h.Center.Y).ThenBy(h => h.Center.X).ToList();
                var representative = ordered[0];
                string calloutText = representative.IsThreadHole
                    ? config.FormatThreadCallout(representative.Diameter, ordered.Count, representative.ThreadCallout)
                    : config.FormatHoleCallout(representative.Diameter, ordered.Count, representative.FitTolerance);

                Point3d textPoint;
                var jigResult = PromptCalloutPointWithJig(
                    db,
                    editor,
                    representative,
                    annotationLayer,
                    diameterDimStyleId,
                    calloutText,
                    out textPoint);

                if (jigResult == DiameterJigResult.Skip)
                {
                    editor.WriteMessage("\nSkipped hole callout {0}.", calloutText);
                    continue;
                }

                if (jigResult == DiameterJigResult.Cancel)
                {
                    editor.WriteMessage("\nHole callout placement canceled.");
                    break;
                }

                CreateCalloutDimension(
                    db,
                    representative,
                    textPoint,
                    annotationLayer,
                    diameterDimStyleId,
                    calloutText,
                    groupId);
            }
        }

        public static void PromptHoleCalloutPlans(
            Document document,
            IList<CadAuto.Core.Planning.HoleCalloutPlan> calloutPlans,
            IList<HoleFeature> sourceHoles,
            DimensionRuleConfig config,
            string annotationLayer,
            ObjectId diameterDimStyleId,
            string groupId)
        {
            if (document == null || calloutPlans == null || calloutPlans.Count == 0 || sourceHoles == null)
            {
                return;
            }

            Database db = document.Database;
            Editor editor = document.Editor;

            foreach (var plan in calloutPlans.Where(p => p != null && p.Holes.Count > 0))
            {
                var representative = FindRepresentativeHole(plan, sourceHoles, config);
                if (representative == null)
                {
                    continue;
                }

                var calloutText = plan.Text ?? string.Empty;
                Point3d textPoint;
                var jigResult = PromptCalloutPointWithJig(
                    db,
                    editor,
                    representative,
                    annotationLayer,
                    diameterDimStyleId,
                    calloutText,
                    out textPoint);

                if (jigResult == DiameterJigResult.Skip)
                {
                    editor.WriteMessage("\nSkipped hole callout {0}.", calloutText);
                    continue;
                }

                if (jigResult == DiameterJigResult.Cancel)
                {
                    editor.WriteMessage("\nHole callout placement canceled.");
                    break;
                }

                CreateCalloutDimension(
                    db,
                    representative,
                    textPoint,
                    annotationLayer,
                    diameterDimStyleId,
                    calloutText,
                    groupId);
            }
        }

        private static HoleFeature FindRepresentativeHole(
            CadAuto.Core.Planning.HoleCalloutPlan plan,
            IList<HoleFeature> sourceHoles,
            DimensionRuleConfig config)
        {
            foreach (var coreHole in plan.Holes.OrderBy(h => h.Center.Y).ThenBy(h => h.Center.X))
            {
                var source = sourceHoles.FirstOrDefault(h => IsSameHoleForCallout(coreHole, h, config));
                if (source != null)
                {
                    return source;
                }
            }

            return null;
        }

        private static bool IsSameHoleForCallout(
            CadAuto.Core.Model.HoleFeature2D coreHole,
            HoleFeature sourceHole,
            DimensionRuleConfig config)
        {
            if (coreHole == null || sourceHole == null)
            {
                return false;
            }

            var dx = coreHole.Center.X - sourceHole.Center.X;
            var dy = coreHole.Center.Y - sourceHole.Center.Y;
            return dx * dx + dy * dy <= config.GeometryTolerance * config.GeometryTolerance
                && Math.Abs(coreHole.Diameter - sourceHole.Diameter) <= config.GeometryTolerance;
        }

        private enum DiameterJigResult
        {
            Picked,
            Skip,
            Cancel
        }

        private static DiameterJigResult PromptCalloutPointWithJig(
            Database db,
            Editor editor,
            HoleFeature hole,
            string annotationLayer,
            ObjectId dimStyleId,
            string calloutText,
            out Point3d textPoint)
        {
            textPoint = Point3d.Origin;
            Point3d center;
            double radius;
            double textHeight;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                if (!TryGetHoleGeometry(tr, hole, out center, out radius))
                {
                    tr.Commit();
                    return DiameterJigResult.Skip;
                }

                textHeight = GetDimStyleTextHeight(db, tr, dimStyleId);
                tr.Commit();
            }

            editor.WriteMessage("\nMove hole callout {0}, click to place, Enter to skip.", calloutText);
            var jig = new DiameterCalloutJig(center, radius, textHeight, calloutText);
            var result = editor.Drag(jig);
            if (result.Status == PromptStatus.OK)
            {
                textPoint = jig.TextPoint;
                return DiameterJigResult.Picked;
            }

            if (jig.SkipRequested || result.Status == PromptStatus.None)
            {
                return DiameterJigResult.Skip;
            }

            return DiameterJigResult.Cancel;
        }

        private static void CreateCalloutDimension(
            Database db,
            HoleFeature hole,
            Point3d textPoint,
            string annotationLayer,
            ObjectId dimStyleId,
            string calloutText,
            string groupId)
        {
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                AnnotationMetadata.EnsureRegApp(db, tr);
                var dimension = BuildCalloutDimension(db, tr, hole, textPoint, annotationLayer, dimStyleId, calloutText) as Dimension;
                if (dimension == null)
                {
                    tr.Commit();
                    return;
                }

                var space = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                space.AppendEntity(dimension);
                tr.AddNewlyCreatedDBObject(dimension, true);
                dimension.UsingDefaultTextPosition = false;
                dimension.TextPosition = textPoint;
                dimension.RecomputeDimensionBlock(true);
                AnnotationMetadata.Mark(dimension, groupId);

                if (hole.IsPinHole)
                {
                    InsertPinRoughnessBlock(
                        db,
                        tr,
                        space,
                        hole,
                        dimension.TextPosition,
                        annotationLayer,
                        dimStyleId,
                        calloutText,
                        groupId);
                }

                tr.Commit();
            }
        }

        private static void InsertPinRoughnessBlock(
            Database db,
            Transaction tr,
            BlockTableRecord space,
            HoleFeature hole,
            Point3d textPoint,
            string annotationLayer,
            ObjectId dimStyleId,
            string calloutText,
            string groupId)
        {
            var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (!blockTable.Has(PinRoughnessBlockName))
            {
                return;
            }

            var blockReference = new BlockReference(textPoint, blockTable[PinRoughnessBlockName]);
            blockReference.SetDatabaseDefaults(db);
            blockReference.Layer = annotationLayer;
            var globalScale = GetDimStyleGlobalScale(db, tr, dimStyleId);
            blockReference.ScaleFactors = new Scale3d(globalScale);
            blockReference.Rotation = Math.PI;
            space.AppendEntity(blockReference);
            tr.AddNewlyCreatedDBObject(blockReference, true);
            AnnotationMetadata.Mark(blockReference, groupId);
        }

        private static Entity BuildCalloutDimension(
            Database db,
            Transaction tr,
            HoleFeature hole,
            Point3d textPoint,
            string annotationLayer,
            ObjectId dimStyleId,
            string calloutText)
        {
            Point3d center;
            double radius;
            if (!TryGetHoleGeometry(tr, hole, out center, out radius))
            {
                return null;
            }

            return BuildCalloutDimension(db, center, radius, textPoint, annotationLayer, dimStyleId, calloutText);
        }

        private static DiametricDimension BuildCalloutDimension(
            Database db,
            Point3d center,
            double radius,
            Point3d textPoint,
            string annotationLayer,
            ObjectId dimStyleId,
            string calloutText)
        {
            var direction = textPoint - center;
            if (direction.Length <= 1e-9)
            {
                direction = Vector3d.XAxis;
            }

            direction = direction.GetNormal();
            var arrowPoint = center + direction * radius;
            var oppositePoint = center - direction * radius;
            var leaderLength = arrowPoint.DistanceTo(textPoint);

            var dimension = new DiametricDimension(
                arrowPoint,
                oppositePoint,
                leaderLength,
                calloutText,
                dimStyleId);
            dimension.SetDatabaseDefaults(db);
            dimension.Layer = annotationLayer;
            dimension.DimensionStyle = dimStyleId;
            dimension.DimensionText = calloutText;
            return dimension;
        }

        private static bool TryGetHoleGeometry(Transaction tr, HoleFeature hole, out Point3d center, out double radius)
        {
            center = Point3d.Origin;
            radius = 0.0;
            ObjectId sourceId = !hole.SourceId.IsNull ? hole.SourceId : hole.CircleId;
            if (sourceId.IsNull)
            {
                return false;
            }

            var entity = tr.GetObject(sourceId, OpenMode.ForRead) as Entity;
            var arc = entity as Arc;
            if (arc != null)
            {
                center = arc.Center;
                radius = arc.Radius;
                return true;
            }

            var circle = entity as Circle;
            if (circle != null)
            {
                center = circle.Center;
                radius = circle.Radius;
                return true;
            }

            return false;
        }

        private static double GetDimStyleTextHeight(Database db, Transaction tr, ObjectId dimStyleId)
        {
            var dimScale = db.Dimscale <= 0.0 ? 1.0 : db.Dimscale;
            var record = tr.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
            if (record != null && record.Dimtxt > 1e-9)
            {
                return record.Dimtxt * dimScale;
            }

            return 2.5 * dimScale;
        }

        private static double GetDimStyleGlobalScale(Database db, Transaction tr, ObjectId dimStyleId)
        {
            var record = tr.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
            if (record != null && record.Dimscale > 1e-9)
            {
                return record.Dimscale;
            }

            return db.Dimscale <= 0.0 ? 1.0 : db.Dimscale;
        }

        private static double EstimateCalloutTextWidth(string calloutText, double textHeight)
        {
            var visualText = (calloutText ?? string.Empty).Replace("%%c", "O");
            return Math.Max(visualText.Length, 2) * textHeight * 0.65;
        }

        private sealed class DiameterCalloutJig : DrawJig
        {
            private readonly Point3d _center;
            private readonly double _radius;
            private readonly double _textHeight;
            private readonly string _calloutText;
            private readonly string _previewText;
            private readonly double _redrawTolerance;
            private readonly double _textWidth;
            private Point3d _textPoint;

            public DiameterCalloutJig(
                Point3d center,
                double radius,
                double textHeight,
                string calloutText)
            {
                _center = center;
                _radius = radius;
                _textHeight = textHeight;
                _calloutText = calloutText ?? string.Empty;
                _previewText = _calloutText.Replace("%%c", "D");
                _redrawTolerance = Math.Max(textHeight * 0.45, radius * 0.08);
                _textWidth = Math.Max(_previewText.Length, 2) * _textHeight * 0.65;
                _textPoint = center + new Vector3d(radius * 3.0, radius * 3.0, 0.0);
            }

            public Point3d TextPoint
            {
                get { return _textPoint; }
            }

            public bool SkipRequested { get; private set; }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                var options = new JigPromptPointOptions("\nPick point: ");
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
                var direction = _textPoint - _center;
                if (direction.Length <= 1e-9)
                {
                    direction = Vector3d.XAxis;
                }

                direction = direction.GetNormal();
                var arrowPoint = _center + direction * _radius;
                var textDirection = Vector3d.XAxis;
                var normal = Vector3d.ZAxis;
                var textStartsLeft = _textPoint.X < arrowPoint.X;
                var textOrigin = textStartsLeft
                    ? _textPoint - textDirection * _textWidth
                    : _textPoint;

                draw.Geometry.WorldLine(arrowPoint, _textPoint);
                draw.Geometry.Text(textOrigin, normal, textDirection, _textHeight, 1.0, 0.0, _previewText);
                return true;
            }

            private void DrawArrowHead(WorldDraw draw, Point3d arrowPoint, Vector3d direction)
            {
                var arrowLength = Math.Max(_textHeight * 1.2, _radius * 0.35);
                var back = -direction;
                var perpendicular = new Vector3d(-direction.Y, direction.X, 0.0).GetNormal();
                var wing1 = arrowPoint + (back + perpendicular * 0.45).GetNormal() * arrowLength;
                var wing2 = arrowPoint + (back - perpendicular * 0.45).GetNormal() * arrowLength;
                draw.Geometry.WorldLine(arrowPoint, wing1);
                draw.Geometry.WorldLine(arrowPoint, wing2);
            }
        }
    }
}
