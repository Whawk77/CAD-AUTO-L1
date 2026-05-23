using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;

namespace AutoFixtureDim
{
    public sealed class Commands
    {
        [CommandMethod("ASD")]
        public void Asd()
        {
            RunAutoFixDim(clearExistingBeforeGenerate: false);
        }

        [CommandMethod("AUTOFIXDIM")]
        public void AutoFixDim()
        {
            RunAutoFixDim(clearExistingBeforeGenerate: false);
        }

        [CommandMethod("AUTOFIXDIMREGEN")]
        public void AutoFixDimRegen()
        {
            RunAutoFixDim(clearExistingBeforeGenerate: true);
        }

        [CommandMethod("AUTOFIXDIMCLEAR")]
        public void AutoFixDimClear()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            Database db = document.Database;
            Editor editor = document.Editor;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    int count = AnnotationMetadata.ClearGeneratedAnnotations(db, tr);
                    tr.Commit();
                    editor.WriteMessage("\nAUTOFIXDIM 已清除插件标注 {0} 个。", count);
                }
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\nAUTOFIXDIMCLEAR 发生异常: {0}", ex.Message);
            }
        }

        private void RunAutoFixDim(bool clearExistingBeforeGenerate)
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            Database db = document.Database;
            Editor editor = document.Editor;
            var config = DimensionRuleConfig.CreateDefault();
            var groupId = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            System.Collections.Generic.IList<System.Collections.Generic.IList<HoleFeature>> diameterGroupsForPlacement = null;
            ObjectId diameterCalloutDimStyleIdForPlacement = ObjectId.Null;
            string annotationLayerForPlacement = string.Empty;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    string annotationLayer = LayerManager.ResolveAnnotationLayer(db, tr);
                    double dimScale = db.Dimscale <= 0.0 ? 1.0 : db.Dimscale;
                    ObjectId dimStyleId = DimStyleManager.ResolveDimStyle(db, tr);
                    ObjectId diameterCalloutDimStyleId = DimStyleManager.ResolveDiameterCalloutDimStyle(db, tr, dimStyleId);

                    var collector = new GeometryCollector(editor);
                    OutlineSelection outlineSelection = collector.PromptForOutlineSelection(tr);

                    var recognizer = new FeatureRecognizer(config);
                    OutlineFeature outline;
                    if (outlineSelection.HasPrimaryPolyline)
                    {
                        var outlineEntity = (Entity)tr.GetObject(outlineSelection.PrimaryPolylineId, OpenMode.ForRead);
                        outline = recognizer.RecognizeOutline(outlineEntity, tr);
                    }
                    else
                    {
                        outline = recognizer.RecognizeOutline(outlineSelection.EntityIds, tr);
                    }

                    DatumDefinition datum = collector.PromptForDatum(outline);
                    var outlineSlotFeatures = recognizer.RecognizeOutlineSlotFeatures(outline);
                    var holeSourceIds = collector.CollectHoleSourcesFromOutlineSelection(tr, outline, outlineSelection, config);
                    bool skipHoleDimensions = false;
                    if (holeSourceIds.Count == 0 && outlineSlotFeatures.Count > 0)
                    {
                        holeSourceIds = new System.Collections.Generic.List<ObjectId>();
                        skipHoleDimensions = true;
                    }

                    if (holeSourceIds.Count == 0 && !skipHoleDimensions)
                    {
                        editor.WriteMessage("\n本次框选对象中未识别到孔，请手动选择需要标注的圆孔。");
                        holeSourceIds = collector.PromptForCircleHoles();
                        if (holeSourceIds == null || holeSourceIds.Count == 0)
                        {
                            editor.WriteMessage("\n未选择孔，已跳过孔标注，继续外轮廓标注。");
                            holeSourceIds = new System.Collections.Generic.List<ObjectId>();
                            skipHoleDimensions = true;
                        }
                    }

                    var holes = skipHoleDimensions
                        ? new System.Collections.Generic.List<HoleFeature>()
                        : recognizer.RecognizeHoles(holeSourceIds, tr, outlineSelection.SelectedIds);

                    if (!skipHoleDimensions && holes.Count == 0)
                    {
                        editor.WriteMessage("\n未识别到有效圆孔，已跳过孔标注，继续外轮廓标注。");
                        skipHoleDimensions = true;
                    }

                    if (!skipHoleDimensions)
                    {
                        var pinHoles = holes.Where(h => h.IsPinHole).ToList();
                        if (pinHoles.Count > 0)
                        {
                            var datumHole = collector.PromptForDatumHole(pinHoles, tr, outline);
                            if (datumHole != null)
                            {
                                datum.DatumHole = datumHole;
                                editor.WriteMessage("\n已设置基准孔: X={0:0.###}, Y={1:0.###}", datumHole.Center.X, datumHole.Center.Y);

                                double? xBase, yBase;
                                bool useToleranceX, useToleranceY;
                                if (!collector.PromptForDatumHoleLocationPoints(config, out xBase, out yBase, out useToleranceX, out useToleranceY))
                                {
                                    editor.WriteMessage("\n用户取消基准点选择，命令结束。");
                                    return;
                                }
                                datum.DatumHoleLocationBaseX = xBase;
                                datum.DatumHoleLocationBaseY = yBase;
                                datum.DatumHoleLocationUseToleranceX = useToleranceX;
                                datum.DatumHoleLocationUseToleranceY = useToleranceY;
                                editor.WriteMessage("\n基准孔定位基准: X基准={0:0.###}, Y基准={1:0.###}", xBase, yBase);
                            }
                        }
                    }

                    if (clearExistingBeforeGenerate)
                    {
                        AnnotationMetadata.EnsureRegApp(db, tr);
                        int erased = AnnotationMetadata.ClearGeneratedAnnotations(db, tr);
                        editor.WriteMessage("\n已清除旧插件标注 {0} 个。", erased);
                    }

                    var slotFeatures = recognizer.LastRecognizedSlots
                        .Concat(outlineSlotFeatures)
                        .ToList();
                    var rows = recognizer.GroupHolesByHorizontalRow(holes);
                    var diameterGroups = recognizer.GroupHolesByDiameter(holes);
                    var currentSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    AnnotationMetadata.EnsureRegApp(db, tr);

                    var drawer = new DimensionDrawer(
                        db,
                        tr,
                        currentSpace,
                        config,
                        dimStyleId,
                        diameterCalloutDimStyleId,
                        dimScale,
                        annotationLayer,
                        appendToDatabase: true,
                        groupId: groupId);

                    DrawLinearDimensions(drawer, outline, datum, rows, slotFeatures, skipHoleDimensions);
                    try
                    {
                        DrawCornerFeatureLeadersWithPreview(
                            editor,
                            db,
                            tr,
                            currentSpace,
                            config,
                            dimStyleId,
                            diameterCalloutDimStyleId,
                            dimScale,
                            annotationLayer,
                            groupId,
                            drawer,
                            outline,
                            collector);
                    }
                    catch (System.Exception ex)
                    {
                        editor.WriteMessage("\n倒角/圆角标注已跳过: {0}", ex.Message);
                    }

                    if (slotFeatures.Count > 0)
                    {
                        try
                        {
                            drawer.DrawSlotRadiusLeadersWithJig(editor, slotFeatures);
                        }
                        catch (System.Exception ex)
                        {
                            editor.WriteMessage("\nU slot radius callouts skipped: {0}", ex.Message);
                        }
                    }

                    if (!skipHoleDimensions)
                    {
                        diameterGroupsForPlacement = diameterGroups;
                        diameterCalloutDimStyleIdForPlacement = diameterCalloutDimStyleId;
                        annotationLayerForPlacement = annotationLayer;
                    }

                    tr.Commit();
                }

                if (diameterGroupsForPlacement != null)
                {
                    NativeDiameterDimensioner.PromptDiameterDimensions(
                        document,
                        diameterGroupsForPlacement,
                        config,
                        annotationLayerForPlacement,
                        diameterCalloutDimStyleIdForPlacement,
                        groupId);
                }

                editor.WriteMessage("\nAUTOFIXDIM 标注完成。");
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                editor.WriteMessage("\nAUTOFIXDIM 取消或失败: {0}", ex.Message);
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\nAUTOFIXDIM 发生异常: {0}", ex.Message);
            }
        }

        private static void DrawLinearDimensions(
            DimensionDrawer drawer,
            OutlineFeature outline,
            DatumDefinition datum,
            System.Collections.Generic.IList<System.Collections.Generic.IList<HoleFeature>> rows,
            System.Collections.Generic.IEnumerable<SlotFeature> slots,
            bool skipHoleDimensions)
        {
            drawer.DrawOutlineDimensions(outline);
            drawer.DrawStepOutlineDimensions(outline);
            if (!skipHoleDimensions)
            {
                drawer.DrawHolePositionDimensions(outline, datum, rows);
            }
            drawer.DrawSlotDimensions(outline, datum, rows, slots);
            drawer.FlushStackedDimensions(outline);
        }

        private static void DrawCornerFeatureLeadersWithPreview(
            Editor editor,
            Database db,
            Transaction tr,
            BlockTableRecord currentSpace,
            DimensionRuleConfig config,
            ObjectId dimStyleId,
            ObjectId diameterCalloutDimStyleId,
            double dimScale,
            string annotationLayer,
            string groupId,
            DimensionDrawer drawer,
            OutlineFeature outline,
            GeometryCollector collector)
        {
            if (outline.Chamfers.Count == 0 && outline.Fillets.Count == 0)
            {
                return;
            }

            if (DateTime.UtcNow.Ticks >= 0)
            {
                drawer.DrawCornerFeatureLeadersWithJig(editor, outline);
                return;
            }

            bool manualCornerLeaderPlacement = collector.PromptForManualCornerLeaderPlacement();
            if (manualCornerLeaderPlacement)
            {
                drawer.DrawChamferLeaders(outline, true);
                drawer.DrawFilletLeaders(outline, true);
                return;
            }

            var previewDrawer = new DimensionDrawer(
                db,
                tr,
                currentSpace,
                config,
                dimStyleId,
                diameterCalloutDimStyleId,
                dimScale,
                annotationLayer,
                appendToDatabase: false,
                groupId: groupId);

            previewDrawer.DrawChamferLeaders(outline, false);
            previewDrawer.DrawFilletLeaders(outline, false);
            if (previewDrawer.PreviewEntities.Count == 0)
            {
                return;
            }

            using (var preview = new AnnotationPreview(previewDrawer.PreviewEntities))
            {
                preview.Show();
                var options = new PromptKeywordOptions("\nPreview chamfer/fillet leaders [Accept/Manual/Skip] <Accept>: ");
                options.Keywords.Add("Accept");
                options.Keywords.Add("Manual");
                options.Keywords.Add("Skip");
                options.Keywords.Default = "Accept";
                options.AllowNone = true;
                var result = editor.GetKeywords(options);

                if (result.Status == PromptStatus.None
                    || (result.Status == PromptStatus.OK && result.StringResult == "Accept"))
                {
                    drawer.DrawChamferLeaders(outline, false);
                    drawer.DrawFilletLeaders(outline, false);
                    return;
                }

                if (result.Status == PromptStatus.OK && result.StringResult == "Manual")
                {
                    drawer.DrawChamferLeaders(outline, true);
                    drawer.DrawFilletLeaders(outline, true);
                    return;
                }
            }

            editor.WriteMessage("\n已跳过倒角/圆角标注。");
        }
    }
}
