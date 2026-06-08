using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;

namespace CadAuto.CadAdapter.Rendering
{
    public sealed partial class DimensionDrawer
    {
        public void DrawHoleDiameterLeaders(OutlineFeature outline, IList<IList<HoleFeature>> diameterGroups)
        {
            _holeDiameterLeaderRenderer.DrawHoleDiameterLeaders(outline, diameterGroups);
        }

        public void DrawChamferLeaders(OutlineFeature outline, bool manualTextPlacement)
        {
            ApplyChamferTextPrecision(outline);
            var groupIndex = 0;
            foreach (var group in outline.Chamfers.GroupBy(c => c.Text).OrderBy(g => g.Key))
            {
                var features = group.ToList();
                if (features.Count == 0)
                {
                    continue;
                }

                var representative = PickChamferLeaderTarget(features, outline);
                var target = Midpoint(representative.StartPoint, representative.EndPoint);
                var text = FormatRepeatedFeatureText(group.Key, features.Count);
                Point3d manualTextPoint;
                if (manualTextPlacement && TryPromptCornerLeaderTextPoint(text, out manualTextPoint))
                {
                    AddManualCornerFeatureLeader(target, manualTextPoint, text);
                }
                else
                {
                    AddCornerFeatureLeader(outline, target, text, groupIndex, strictOutside: true);
                }
                groupIndex++;
            }
        }

        public void DrawFilletLeaders(OutlineFeature outline, bool manualTextPlacement)
        {
            var groupIndex = outline.Chamfers.GroupBy(c => c.Text).Count();
            foreach (var group in outline.Fillets.GroupBy(f => f.Text).OrderBy(g => g.Key))
            {
                var features = group.ToList();
                if (features.Count == 0)
                {
                    continue;
                }

                var representative = PickFilletLeaderTarget(features, outline);
                var text = FormatRepeatedFeatureText(group.Key, features.Count);
                Point3d manualTextPoint;
                if (manualTextPlacement && TryPromptCornerLeaderTextPoint(text, out manualTextPoint))
                {
                    AddManualFilletRadiusDimension(representative, manualTextPoint, text);
                }
                else
                {
                    AddFilletRadiusDimension(outline, representative, text, groupIndex);
                }
                groupIndex++;
            }
        }

        public void DrawCornerFeatureLeadersWithJig(Editor editor, OutlineFeature outline)
        {
            ApplyChamferTextPrecision(outline);
            var textHeight = GetDimStyleTextHeight(_diameterCalloutDimStyleId);
            foreach (var group in outline.Chamfers.GroupBy(c => c.Text).OrderBy(g => g.Key))
            {
                var features = group.ToList();
                if (features.Count == 0)
                {
                    continue;
                }

                var representative = PickChamferLeaderTarget(features, outline);
                var target = Midpoint(representative.StartPoint, representative.EndPoint);
                var text = FormatRepeatedFeatureText(group.Key, features.Count);
                Point3d textPoint;
                var result = _cornerCalloutRenderer.PromptLeaderPoint(editor, target, text, textHeight, out textPoint);
                if (result == CornerCalloutJigResult.Skip)
                {
                    editor.WriteMessage("\nSkipped corner callout {0}.", text);
                    continue;
                }

                if (result == CornerCalloutJigResult.Cancel)
                {
                    editor.WriteMessage("\nCorner callout placement canceled.");
                    return;
                }

                AddManualCornerFeatureLeader(target, textPoint, text);
            }

            foreach (var group in outline.Fillets.GroupBy(f => f.Text).OrderBy(g => g.Key))
            {
                var features = group.ToList();
                if (features.Count == 0)
                {
                    continue;
                }

                var representative = PickFilletLeaderTarget(features, outline);
                var target = GetArcLeaderTarget(representative);
                var text = FormatRepeatedFeatureText(group.Key, features.Count);
                Point3d textPoint;
                var result = _cornerCalloutRenderer.PromptLeaderPoint(editor, target, text, textHeight, out textPoint);
                if (result == CornerCalloutJigResult.Skip)
                {
                    editor.WriteMessage("\nSkipped corner callout {0}.", text);
                    continue;
                }

                if (result == CornerCalloutJigResult.Cancel)
                {
                    editor.WriteMessage("\nCorner callout placement canceled.");
                    return;
                }

                AddManualFilletRadiusDimension(representative, textPoint, text);
            }
        }

        private void ApplyChamferTextPrecision(OutlineFeature outline)
        {
            if (outline == null || outline.Chamfers.Count == 0)
            {
                return;
            }

            var precision = GetDimStyleLinearPrecision(_diameterCalloutDimStyleId);
            foreach (var chamfer in outline.Chamfers)
            {
                var value = chamfer.Value > _config.GeometryTolerance
                    ? chamfer.Value
                    : Math.Max(chamfer.DeltaX, chamfer.DeltaY);
                chamfer.Text = "C" + FormatNumberWithPrecision(value, precision);
            }
        }

        public void DrawSlotRadiusLeadersWithJig(Editor editor, IEnumerable<SlotFeature> slots)
        {
            if (editor == null || slots == null)
            {
                return;
            }

            var textHeight = GetDimStyleTextHeight(_diameterCalloutDimStyleId);
            foreach (var group in GroupSlotsByRadius(slots))
            {
                if (group.Count == 0)
                {
                    continue;
                }

                var slot = group[0];
                var target = slot.LeaderArcCenter;
                var text = FormatSlotRadiusCallout(group);
                Point3d textPoint;
                var result = _cornerCalloutRenderer.PromptLeaderPoint(editor, target, text, textHeight, out textPoint);
                if (result == CornerCalloutJigResult.Skip)
                {
                    editor.WriteMessage("\nSkipped U slot radius callout {0}.", text);
                    continue;
                }

                if (result == CornerCalloutJigResult.Cancel)
                {
                    editor.WriteMessage("\nU slot radius callout placement canceled.");
                    return;
                }

                AddManualSlotRadiusDimension(slot, textPoint, text);
            }
        }

        private void AddFilletRadiusDimension(OutlineFeature outline, FilletFeature feature, string text, int groupIndex)
        {
            var target = GetArcLeaderTarget(feature);
            var leaderOffset = Scale(_config.LeaderOffset + groupIndex * 3.0);
            var textGap = Scale(1.2);
            var placement = PickCornerLeaderPlacement(outline, target, leaderOffset, textGap, groupIndex, text, strictOutside: false);
            var centerPoint = new Point3d(feature.Center.X, feature.Center.Y, 0.0);
            var chordPoint = new Point3d(target.X, target.Y, 0.0);
            var textPoint = new Point3d(placement.Text.X, placement.Text.Y, 0.0);
            _cornerCalloutRenderer.AddRadialDimension(
                centerPoint,
                chordPoint,
                textPoint,
                Scale(_config.LeaderOffset),
                text,
                useCustomTextPosition: false);
        }

        private bool TryPromptCornerLeaderTextPoint(string text, out Point3d textPoint)
        {
            textPoint = Point3d.Origin;
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return false;
            }

            var options = new PromptPointOptions("\nSelect text point for " + text + " <Auto>: ");
            options.AllowNone = true;
            var result = doc.Editor.GetPoint(options);
            if (result.Status != PromptStatus.OK)
            {
                return false;
            }

            textPoint = result.Value;
            return true;
        }

        private void AddManualCornerFeatureLeader(Point2d target, Point3d textPoint, string text)
        {
            var arrowPoint = new Point3d(target.X, target.Y, 0.0);
            AddCornerLeader(arrowPoint, textPoint, textPoint, text);
        }

        private void AddManualFilletRadiusDimension(FilletFeature feature, Point3d textPoint, string text)
        {
            var target = GetArcLeaderTarget(feature);
            var centerPoint = new Point3d(feature.Center.X, feature.Center.Y, 0.0);
            var chordPoint = new Point3d(target.X, target.Y, 0.0);
            _cornerCalloutRenderer.AddRadialDimension(
                centerPoint,
                chordPoint,
                textPoint,
                Scale(_config.LeaderOffset),
                text,
                useCustomTextPosition: true);
        }

        private void AddManualSlotRadiusDimension(SlotFeature slot, Point3d textPoint, string text)
        {
            var target = slot.LeaderArcCenter;
            var arrowPoint = new Point3d(target.X, target.Y, 0.0);
            AddCornerLeader(arrowPoint, textPoint, textPoint, text);
        }

        private static string FormatRepeatedFeatureText(string text, int count)
        {
            return count > 1 ? count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + text : text;
        }

        private void AddCornerLeader(Point3d arrowPoint, Point3d landingPoint, Point3d textPoint, string text)
        {
            _cornerCalloutRenderer.AddLeader(arrowPoint, landingPoint, textPoint, text);
        }
    }
}
