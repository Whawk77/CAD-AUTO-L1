using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;
using CadAuto.Core.Rules;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed class HoleDiameterLeaderRenderer
    {
        private readonly Database _database;
        private readonly CadEntityWriter _writer;
        private readonly DimensionRuleConfig _config;
        private readonly ObjectId _diameterCalloutDimStyleId;
        private readonly bool _appendToDatabase;
        private readonly string _annotationLayer;

        public HoleDiameterLeaderRenderer(
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

        public void DrawHoleDiameterLeaders(OutlineFeature outline, IList<IList<HoleFeature>> diameterGroups)
        {
            if (outline == null || diameterGroups == null)
            {
                return;
            }

            var groupIndex = 0;
            foreach (var group in diameterGroups.Where(g => g.Count > 0))
            {
                var ordered = group.OrderBy(h => h.Center.Y).ThenBy(h => h.Center.X).ToList();
                var representative = PickLeaderTarget(ordered, outline);
                var points = BuildHoleLeaderPoints(ordered, representative, groupIndex);
                var text = representative.IsThreadHole
                    ? _config.FormatThreadCallout(representative.Diameter, ordered.Count, representative.ThreadCallout)
                    : _config.FormatHoleCallout(representative.Diameter, ordered.Count, representative.FitTolerance);

                AddDiameterLeader(points.ArrowPoint, points.LandingPoint, points.TextPoint, text);
                groupIndex++;
            }
        }

        private HoleFeature PickLeaderTarget(IList<HoleFeature> ordered, OutlineFeature outline)
        {
            if (ordered.Count == 1)
            {
                return ordered[0];
            }

            var outlineCenterX = (outline.MinX + outline.MaxX) / 2.0;
            var groupCenterX = ordered.Average(h => h.Center.X);
            return groupCenterX <= outlineCenterX ? ordered.First() : ordered.Last();
        }

        private HoleLeaderPoints BuildHoleLeaderPoints(IList<HoleFeature> group, HoleFeature representative, int groupIndex)
        {
            var radius = representative.Diameter / 2.0;
            var leaderOffset = _writer.Scale(_config.LeaderOffset + groupIndex * 3.0);
            var textGap = _writer.Scale(1.2);

            if (group.Count > 1)
            {
                var arrowDirection = new Vector3d(-1.0, -1.0, 0.0).GetNormal();
                var arrowPoint = new Point3d(
                    representative.Center.X + arrowDirection.X * radius,
                    representative.Center.Y + arrowDirection.Y * radius,
                    representative.Center.Z);
                var landingPoint = new Point3d(
                    representative.Center.X - leaderOffset,
                    representative.Center.Y - leaderOffset,
                    representative.Center.Z);
                var textPoint = new Point3d(
                    landingPoint.X - textGap,
                    landingPoint.Y,
                    landingPoint.Z);

                return new HoleLeaderPoints(arrowPoint, landingPoint, textPoint);
            }

            var singleArrowDirection = new Vector3d(-1.0, -1.0, 0.0).GetNormal();
            var singleArrowPoint = new Point3d(
                representative.Center.X + singleArrowDirection.X * radius,
                representative.Center.Y + singleArrowDirection.Y * radius,
                representative.Center.Z);
            var singleLandingPoint = new Point3d(
                representative.Center.X - leaderOffset * 0.85,
                representative.Center.Y - leaderOffset * 0.85,
                representative.Center.Z);
            var singleTextPoint = new Point3d(
                singleLandingPoint.X - textGap,
                singleLandingPoint.Y,
                singleLandingPoint.Z);

            return new HoleLeaderPoints(singleArrowPoint, singleLandingPoint, singleTextPoint);
        }

        private void AddDiameterLeader(Point3d arrowPoint, Point3d landingPoint, Point3d textPoint, string text)
        {
            var mtext = new MText();
            mtext.SetDatabaseDefaults(_database);
            mtext.Contents = text;
            mtext.TextHeight = _writer.GetDimStyleTextHeight(_diameterCalloutDimStyleId);
            mtext.TextStyleId = _writer.GetDimStyleTextStyle(_diameterCalloutDimStyleId);
            mtext.Location = textPoint;
            mtext.Attachment = AttachmentPoint.MiddleLeft;
            mtext.Layer = _annotationLayer;
            _writer.Append(mtext);

            if (!_appendToDatabase)
            {
                var previewLine = new Line(arrowPoint, landingPoint);
                previewLine.SetDatabaseDefaults(_database);
                previewLine.Layer = _annotationLayer;
                _writer.Append(previewLine);
                return;
            }

            var leader = new Leader();
            leader.SetDatabaseDefaults(_database);
            leader.Layer = _annotationLayer;
            leader.DimensionStyle = _diameterCalloutDimStyleId;
            leader.AppendVertex(arrowPoint);
            leader.AppendVertex(landingPoint);
            _writer.Append(leader);
            leader.Annotation = mtext.ObjectId;
            leader.EvaluateLeader();
        }

        private sealed class HoleLeaderPoints
        {
            public HoleLeaderPoints(Point3d arrowPoint, Point3d landingPoint, Point3d textPoint)
            {
                ArrowPoint = arrowPoint;
                LandingPoint = landingPoint;
                TextPoint = textPoint;
            }

            public Point3d ArrowPoint { get; private set; }
            public Point3d LandingPoint { get; private set; }
            public Point3d TextPoint { get; private set; }
        }
    }
}
