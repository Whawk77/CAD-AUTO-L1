using System;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using CadAuto.CadAdapter;
using CadAuto.CadAdapter.Collection;
using CadAuto.CadAdapter.Environment;
using CadAuto.CadAdapter.Mapping;
using CadAuto.CadAdapter.Model;
using CadAuto.CadAdapter.Recognition;
using CadAuto.CadAdapter.Rendering;
using CadAuto.Core.Rules;

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

        [CommandMethod("ASD2")]
        public void Asd2()
        {
            RunAutoFixDim(clearExistingBeforeGenerate: true);
        }

        [CommandMethod("ASD4")]
        public void AsdDebug()
        {
            RunAutoFixDim(clearExistingBeforeGenerate: true, diagnosticsEnabled: true);
        }

        [CommandMethod("ASDCOREDBG")]
        public void AsdCoreDebug()
        {
            RunCoreDebug();
        }

        [CommandMethod("ASD3")]
        public void Asd3()
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
                editor.WriteMessage("\nASD3 发生异常: {0}", ex.Message);
            }
        }

        private void RunCoreDebug()
        {
            Document document = Application.DocumentManager.MdiActiveDocument;
            if (document == null)
            {
                return;
            }

            Database db = document.Database;
            Editor editor = document.Editor;
            var config = DimensionRuleConfig.CreateDefault();

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    ObjectId dimStyleId = DimStyleManager.ResolveDimStyle(db, tr);
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
                        editor.WriteMessage("\nASDCOREDBG: no holes found in selection; select Circle holes manually or press Enter to skip holes.");
                        holeSourceIds = collector.PromptForCircleHoles();
                        if (holeSourceIds == null || holeSourceIds.Count == 0)
                        {
                            holeSourceIds = new System.Collections.Generic.List<ObjectId>();
                            skipHoleDimensions = true;
                        }
                    }

                    var holes = skipHoleDimensions
                        ? new System.Collections.Generic.List<HoleFeature>()
                        : recognizer.RecognizeHoles(holeSourceIds, tr, outlineSelection.SelectedIds);

                    if (!skipHoleDimensions && holes.Count == 0)
                    {
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

                                double? xBase, yBase;
                                bool useToleranceX, useToleranceY;
                                if (!collector.PromptForDatumHoleLocationPoints(config, out xBase, out yBase, out useToleranceX, out useToleranceY))
                                {
                                    editor.WriteMessage("\nASDCOREDBG: datum-hole base selection cancelled.");
                                    return;
                                }

                                datum.DatumHoleLocationBaseX = xBase;
                                datum.DatumHoleLocationBaseY = yBase;
                                datum.DatumHoleLocationUseToleranceX = useToleranceX;
                                datum.DatumHoleLocationUseToleranceY = useToleranceY;
                            }
                        }
                    }

                    var coreConfig = config;
                    var coreOutline = CadToCoreModelMapper.ToCoreOutline(outline);
                    var coreDatum = CadToCoreModelMapper.ToCoreDatum(datum);
                    var coreHoles = CadToCoreModelMapper.ToCoreHoles(holes);
                    var coreSlots = CadToCoreModelMapper.ToCoreSlots(recognizer.LastRecognizedSlots);
                    var planner = new CadAuto.Core.Planning.DimensionPlanner(coreConfig);
                    var plan = planner.CreateDimensionPlan(coreOutline, coreDatum, coreHoles, coreSlots);
                    var holeCalloutPlans = new CadAuto.Core.Planning.HoleCalloutPlanner(coreConfig)
                        .CreatePlans(coreHoles, coreDatum.DatumHole);
                    var renderPlan = CreateCoreDebugRenderPlan(plan);

                    string debugLayer = EnsureCoreDebugLayer(db, tr);
                    var currentSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    var renderer = new CadAuto.CadAdapter.DimensionPlanRenderer(
                        db,
                        tr,
                        currentSpace,
                        dimStyleId,
                        coreConfig.FirstDimOffset,
                        debugLayer);
                    renderer.Render(renderPlan, coreOutline);

                    WriteCoreDebugSummary(editor, outline, holes, coreSlots.Count, plan, renderPlan, holeCalloutPlans, debugLayer);
                    tr.Commit();
                }
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                editor.WriteMessage("\nASDCOREDBG cancelled or failed: {0}", ex.Message);
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\nASDCOREDBG failed: {0}", ex.Message);
            }
        }

        private void RunAutoFixDim(bool clearExistingBeforeGenerate, bool diagnosticsEnabled = false)
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
            var diagnosticSide = diagnosticsEnabled
                ? PromptForDiagnosticSide(editor)
                : DiagnosticDimensionSide.All;
            System.Collections.Generic.IList<CadAuto.Core.Planning.HoleCalloutPlan> holeCalloutPlansForPlacement = null;
            System.Collections.Generic.IList<HoleFeature> holeCalloutSourceHolesForPlacement = null;
            OutlineFeature outlineForInteractivePlacement = null;
            System.Collections.Generic.IList<SlotFeature> slotFeaturesForInteractivePlacement = null;
            ObjectId dimStyleIdForInteractivePlacement = ObjectId.Null;
            ObjectId diameterCalloutDimStyleIdForPlacement = ObjectId.Null;
            string annotationLayerForPlacement = string.Empty;
            double dimScaleForInteractivePlacement = 1.0;

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
                    var holeCalloutPlans = BuildHoleCalloutPlansForPlacement(holes, datum.DatumHole, config);
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
                        groupId: groupId,
                        diagnosticsEnabled: diagnosticsEnabled,
                        diagnosticSide: diagnosticSide);

                    DrawLinearDimensions(drawer, outline, datum, rows, slotFeatures, skipHoleDimensions);
                    outlineForInteractivePlacement = outline;
                    slotFeaturesForInteractivePlacement = slotFeatures;
                    dimStyleIdForInteractivePlacement = dimStyleId;
                    diameterCalloutDimStyleIdForPlacement = diameterCalloutDimStyleId;
                    annotationLayerForPlacement = annotationLayer;
                    dimScaleForInteractivePlacement = dimScale;
                    if (EnableInlineInteractiveCallouts())
                    {
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
                    }

                    if (!diagnosticsEnabled && !skipHoleDimensions)
                    {
                        holeCalloutPlansForPlacement = holeCalloutPlans;
                        holeCalloutSourceHolesForPlacement = holes;
                        diameterCalloutDimStyleIdForPlacement = diameterCalloutDimStyleId;
                        annotationLayerForPlacement = annotationLayer;
                    }

                    tr.Commit();
                }

                if (!diagnosticsEnabled)
                {
                    DrawPostLinearInteractiveAnnotations(
                        document,
                        config,
                        dimStyleIdForInteractivePlacement,
                        diameterCalloutDimStyleIdForPlacement,
                        dimScaleForInteractivePlacement,
                        annotationLayerForPlacement,
                        groupId,
                        outlineForInteractivePlacement,
                        slotFeaturesForInteractivePlacement);
                }

                if (holeCalloutPlansForPlacement != null)
                {
                    NativeDiameterDimensioner.PromptHoleCalloutPlans(
                        document,
                        holeCalloutPlansForPlacement,
                        holeCalloutSourceHolesForPlacement,
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

        private static string EnsureCoreDebugLayer(Database db, Transaction tr)
        {
            const string layerName = "AUTOFIXDIM_COREDBG";
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!layerTable.Has(layerName))
            {
                layerTable.UpgradeOpen();
                var layer = new LayerTableRecord
                {
                    Name = layerName
                };
                layerTable.Add(layer);
                tr.AddNewlyCreatedDBObject(layer, true);
            }

            return layerName;
        }

        private static void WriteCoreDebugSummary(
            Editor editor,
            OutlineFeature outline,
            System.Collections.Generic.IList<HoleFeature> holes,
            int slotCount,
            CadAuto.Core.Planning.DimensionPlan plan,
            CadAuto.Core.Planning.DimensionPlan renderPlan,
            System.Collections.Generic.IList<CadAuto.Core.Planning.HoleCalloutPlan> holeCalloutPlans,
            string debugLayer)
        {
            editor.WriteMessage(
                "\nASDCOREDBG: outline W={0:0.###}, H={1:0.###}; holes={2}; slots={3}; core dims={4}; rendered={5}; skipped={6}; pin groups={7}; layer={8}",
                outline.Width,
                outline.Height,
                holes == null ? 0 : holes.Count,
                slotCount,
                plan.Dimensions.Count,
                renderPlan.Dimensions.Count,
                plan.Dimensions.Count - renderPlan.Dimensions.Count,
                plan.PinGroups.Count,
                debugLayer);

            foreach (var group in plan.Dimensions
                .GroupBy(d => d.Kind)
                .OrderBy(g => g.Key.ToString()))
            {
                editor.WriteMessage("\n  {0}: {1}", group.Key, group.Count());
            }

            if (holeCalloutPlans != null && holeCalloutPlans.Count > 0)
            {
                var calloutIndex = 1;
                foreach (var callout in holeCalloutPlans)
                {
                    editor.WriteMessage(
                        "\n  callout#{0:00} kind={1} holes={2} anchor=({3:0.###},{4:0.###}) owner={5} text='{6}'",
                        calloutIndex,
                        callout.Kind,
                        callout.Holes.Count,
                        callout.AnchorPoint.X,
                        callout.AnchorPoint.Y,
                        callout.DebugOwner ?? string.Empty,
                        callout.Text ?? string.Empty);
                    calloutIndex++;
                }
            }

            if (holes != null && holes.Count > 0)
            {
                var holeIndex = 1;
                foreach (var hole in holes
                    .OrderBy(h => h.Center.X)
                    .ThenBy(h => h.Center.Y))
                {
                    editor.WriteMessage(
                        "\n  hole#{0:00} kind={1} dia={2:0.###} center=({3:0.###},{4:0.###}) fit='{5}' thread='{6}'",
                        holeIndex,
                        hole.HoleKind,
                        hole.Diameter,
                        hole.Center.X,
                        hole.Center.Y,
                        hole.FitTolerance ?? string.Empty,
                        hole.ThreadCallout ?? string.Empty);
                    holeIndex++;
                }
            }

            foreach (var pinGroup in plan.PinGroups.OrderBy(g => g.GroupIndex))
            {
                editor.WriteMessage(
                    "\n  pinGroup PG{0}: base=({1:0.###},{2:0.###}) pins={3} members={4} hSide={5} vSide={6}",
                    pinGroup.GroupIndex,
                    pinGroup.BasePin == null ? 0.0 : pinGroup.BasePin.Center.X,
                    pinGroup.BasePin == null ? 0.0 : pinGroup.BasePin.Center.Y,
                    pinGroup.Pins.Count,
                    pinGroup.MemberHoles.Count,
                    pinGroup.HorizontalSide,
                    pinGroup.VerticalSide);

                foreach (var member in pinGroup.MemberHoles
                    .OrderBy(h => h.Center.X)
                    .ThenBy(h => h.Center.Y))
                {
                    editor.WriteMessage(
                        "\n    member kind={0} dia={1:0.###} center=({2:0.###},{3:0.###})",
                        member.Kind,
                        member.Diameter,
                        member.Center.X,
                        member.Center.Y);
                }
            }

            int index = 1;
            foreach (var dim in plan.Dimensions
                .OrderBy(d => d.Side)
                .ThenBy(d => d.Kind)
                .ThenBy(d => d.DebugRole ?? string.Empty))
            {
                editor.WriteMessage(
                    "\n  #{0:00} {1}/{2}/{3} span={4:0.###} from=({5:0.###},{6:0.###}) to=({7:0.###},{8:0.###}) role={9} owner={10} text='{11}'",
                    index,
                    dim.Kind,
                    dim.Side,
                    dim.Orientation,
                    GetCoreDebugSpan(dim),
                    dim.FirstPoint.X,
                    dim.FirstPoint.Y,
                    dim.SecondPoint.X,
                    dim.SecondPoint.Y,
                    dim.DebugRole ?? string.Empty,
                    dim.DebugOwner ?? string.Empty,
                    dim.OverrideText ?? string.Empty);
                index++;
            }
        }

        private static double GetCoreDebugSpan(CadAuto.Core.Planning.PlannedDimension dim)
        {
            return dim.Orientation == CadAuto.Core.Planning.DimensionOrientation.Horizontal
                ? Math.Abs(dim.SecondPoint.X - dim.FirstPoint.X)
                : Math.Abs(dim.SecondPoint.Y - dim.FirstPoint.Y);
        }

        private static CadAuto.Core.Planning.DimensionPlan CreateCoreDebugRenderPlan(CadAuto.Core.Planning.DimensionPlan source)
        {
            var target = new CadAuto.Core.Planning.DimensionPlan();
            foreach (var group in source.PinGroups)
            {
                target.PinGroups.Add(group);
            }

            foreach (var dim in source.Dimensions)
            {
                target.Dimensions.Add(dim);
            }

            return target;
        }

        private static DiagnosticDimensionSide PromptForDiagnosticSide(Editor editor)
        {
            var options = new PromptKeywordOptions("\n选择诊断方向 [全部(A)/顶部(T)/底部(B)/左侧(L)/右侧(R)]", "All Top Bottom Left Right");
            options.AllowNone = true;
            options.Keywords.Default = "All";
            var result = editor.GetKeywords(options);
            if (result.Status != PromptStatus.OK)
            {
                return DiagnosticDimensionSide.All;
            }

            switch (result.StringResult)
            {
                case "Top":
                    return DiagnosticDimensionSide.Top;
                case "Bottom":
                    return DiagnosticDimensionSide.Bottom;
                case "Left":
                    return DiagnosticDimensionSide.Left;
                case "Right":
                    return DiagnosticDimensionSide.Right;
                default:
                    return DiagnosticDimensionSide.All;
            }
        }

        private static void DrawPostLinearInteractiveAnnotations(
            Document document,
            DimensionRuleConfig config,
            ObjectId dimStyleId,
            ObjectId diameterCalloutDimStyleId,
            double dimScale,
            string annotationLayer,
            string groupId,
            OutlineFeature outline,
            System.Collections.Generic.IEnumerable<SlotFeature> slotFeatures)
        {
            if (document == null || outline == null)
            {
                return;
            }

            Database db = document.Database;
            Editor editor = document.Editor;
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
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

                    try
                    {
                        drawer.DrawCornerFeatureLeadersWithJig(editor, outline);
                    }
                    catch (System.Exception ex)
                    {
                        editor.WriteMessage("\nChamfer/fillet callouts skipped: {0}", ex.Message);
                    }

                    var slots = slotFeatures == null
                        ? new System.Collections.Generic.List<SlotFeature>()
                        : slotFeatures.Where(s => s != null).ToList();
                    if (slots.Count > 0)
                    {
                        try
                        {
                            drawer.DrawSlotRadiusLeadersWithJig(editor, slots);
                        }
                        catch (System.Exception ex)
                        {
                            editor.WriteMessage("\nU slot radius callouts skipped: {0}", ex.Message);
                        }
                    }

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                editor.WriteMessage("\nInteractive corner/slot callouts skipped: {0}", ex.Message);
            }
        }

        private static bool EnableInlineInteractiveCallouts()
        {
            return false;
        }

        private sealed class HoleCalloutCluster
        {
            public HoleFeature BasePin { get; set; }
            public System.Collections.Generic.List<HoleFeature> Pins { get; } = new System.Collections.Generic.List<HoleFeature>();
            public System.Collections.Generic.List<HoleFeature> Members { get; } = new System.Collections.Generic.List<HoleFeature>();
        }

        private static System.Collections.Generic.IList<CadAuto.Core.Planning.HoleCalloutPlan> BuildHoleCalloutPlansForPlacement(
            System.Collections.Generic.IEnumerable<HoleFeature> holes,
            HoleFeature datumPin,
            DimensionRuleConfig config)
        {
            var holeList = holes == null
                ? new System.Collections.Generic.List<HoleFeature>()
                : holes.Where(h => h != null && !h.IsSlotPoint).ToList();
            var coreConfig = config;
            var coreHoles = CadToCoreModelMapper.ToCoreHoles(holeList);
            var coreDatumPin = datumPin == null
                ? null
                : coreHoles.FirstOrDefault(h => IsSameHoleForCallout(h, datumPin, config)) ?? CadToCoreModelMapper.ToCoreHoles(new[] { datumPin }).FirstOrDefault();
            return new CadAuto.Core.Planning.HoleCalloutPlanner(coreConfig).CreatePlans(coreHoles, coreDatumPin);
        }

        private static System.Collections.Generic.IList<System.Collections.Generic.IList<HoleFeature>> BuildDiameterGroupsForPlacement(
            System.Collections.Generic.IEnumerable<HoleFeature> holes,
            HoleFeature datumPin,
            DimensionRuleConfig config)
        {
            var holeList = holes == null
                ? new System.Collections.Generic.List<HoleFeature>()
                : holes.Where(h => h != null && !h.IsSlotPoint).ToList();
            var coreConfig = config;
            var coreHoles = CadToCoreModelMapper.ToCoreHoles(holeList);
            var coreDatumPin = datumPin == null
                ? null
                : coreHoles.FirstOrDefault(h => IsSameHoleForCallout(h, datumPin, config)) ?? CadToCoreModelMapper.ToCoreHoles(new[] { datumPin }).FirstOrDefault();
            var plans = new CadAuto.Core.Planning.HoleCalloutPlanner(coreConfig).CreatePlans(coreHoles, coreDatumPin);
            return ConvertHoleCalloutPlansToDiameterGroups(plans, holeList, config);
        }

        private static System.Collections.Generic.IList<System.Collections.Generic.IList<HoleFeature>> ConvertHoleCalloutPlansToDiameterGroups(
            System.Collections.Generic.IEnumerable<CadAuto.Core.Planning.HoleCalloutPlan> plans,
            System.Collections.Generic.IList<HoleFeature> sourceHoles,
            DimensionRuleConfig config)
        {
            var result = new System.Collections.Generic.List<System.Collections.Generic.IList<HoleFeature>>();
            foreach (var plan in plans ?? Enumerable.Empty<CadAuto.Core.Planning.HoleCalloutPlan>())
            {
                var group = new System.Collections.Generic.List<HoleFeature>();
                foreach (var coreHole in plan.Holes)
                {
                    var source = sourceHoles.FirstOrDefault(h => IsSameHoleForCallout(coreHole, h, config) && !group.Any(existing => IsSameHoleForCallout(existing, h, config)));
                    if (source != null)
                    {
                        group.Add(source);
                    }
                }

                if (group.Count > 0)
                {
                    result.Add(group);
                }
            }

            return result;
        }

        private static System.Collections.Generic.IList<System.Collections.Generic.IList<HoleFeature>> BuildDiameterGroupsBySpatialRows(
            System.Collections.Generic.IList<HoleFeature> holes,
            DimensionRuleConfig config)
        {
            var result = new System.Collections.Generic.List<System.Collections.Generic.IList<HoleFeature>>();
            var rows = new System.Collections.Generic.List<System.Collections.Generic.List<HoleFeature>>();
            foreach (var hole in holes.OrderBy(h => h.Center.Y).ThenBy(h => h.Center.X))
            {
                var row = rows.FirstOrDefault(r => System.Math.Abs(r.Average(h => h.Center.Y) - hole.Center.Y) <= config.GeometryTolerance);
                if (row == null)
                {
                    row = new System.Collections.Generic.List<HoleFeature>();
                    rows.Add(row);
                }

                row.Add(hole);
            }

            foreach (var row in rows.OrderBy(r => r.Average(h => h.Center.Y)))
            {
                result.AddRange(GroupCalloutClusterMembers(row, config));
            }

            return result;
        }

        private static System.Collections.Generic.List<HoleCalloutCluster> BuildPinCalloutClusters(
            System.Collections.Generic.IList<HoleFeature> pinHoles,
            HoleFeature datumPin,
            DimensionRuleConfig config)
        {
            var remaining = pinHoles.OrderBy(h => h.Center.X).ThenBy(h => h.Center.Y).ToList();
            var clusters = new System.Collections.Generic.List<HoleCalloutCluster>();
            var seed = datumPin != null && datumPin.IsPinHole
                ? remaining.FirstOrDefault(h => IsSameHoleForCallout(h, datumPin, config)) ?? datumPin
                : remaining.First();

            while (remaining.Count > 0)
            {
                var reference = clusters.Count == 0 ? null : clusters[clusters.Count - 1].BasePin;
                if (clusters.Count > 0)
                {
                    seed = remaining
                        .OrderBy(h => DistanceSquared(h.Center, reference.Center))
                        .ThenBy(h => h.Center.X)
                        .ThenBy(h => h.Center.Y)
                        .First();
                }

                var cluster = CreatePinCalloutCluster(seed, remaining, clusters.Count == 0 ? seed : null, reference, config);
                clusters.Add(cluster);
                foreach (var pin in cluster.Pins.ToList())
                {
                    for (int i = remaining.Count - 1; i >= 0; i--)
                    {
                        if (IsSameHoleForCallout(remaining[i], pin, config))
                        {
                            remaining.RemoveAt(i);
                        }
                    }
                }

                if (remaining.Count > 0 && !remaining.Any(h => IsSameHoleForCallout(h, seed, config)))
                {
                    seed = remaining[0];
                }
            }

            return clusters;
        }

        private static HoleCalloutCluster CreatePinCalloutCluster(
            HoleFeature seed,
            System.Collections.Generic.IList<HoleFeature> candidates,
            HoleFeature forcedBasePin,
            HoleFeature referenceBasePin,
            DimensionRuleConfig config)
        {
            var pins = new System.Collections.Generic.List<HoleFeature> { seed };
            var pairedPin = candidates
                .Where(h => !IsSameHoleForCallout(h, seed, config) && System.Math.Abs(h.Diameter - seed.Diameter) <= config.GeometryTolerance)
                .OrderBy(h => DistanceSquared(h.Center, seed.Center))
                .ThenBy(h => h.Center.X)
                .ThenBy(h => h.Center.Y)
                .FirstOrDefault();
            if (pairedPin != null)
            {
                pins.Add(pairedPin);
            }

            var basePin = ChooseCalloutBasePin(pins, forcedBasePin, referenceBasePin, seed, config);
            var cluster = new HoleCalloutCluster { BasePin = basePin };
            foreach (var pin in pins.OrderBy(h => DistanceSquared(h.Center, basePin.Center)))
            {
                cluster.Pins.Add(pin);
                cluster.Members.Add(pin);
            }

            return cluster;
        }

        private static HoleFeature ChooseCalloutBasePin(
            System.Collections.Generic.IList<HoleFeature> pins,
            HoleFeature forcedBasePin,
            HoleFeature referenceBasePin,
            HoleFeature fallbackPin,
            DimensionRuleConfig config)
        {
            if (forcedBasePin != null)
            {
                return pins.FirstOrDefault(h => IsSameHoleForCallout(h, forcedBasePin, config)) ?? forcedBasePin;
            }

            if (referenceBasePin != null)
            {
                return pins
                    .OrderBy(h => DistanceSquared(h.Center, referenceBasePin.Center))
                    .ThenBy(h => h.Center.X)
                    .ThenBy(h => h.Center.Y)
                    .First();
            }

            return pins.OrderBy(h => h.Center.X).ThenBy(h => h.Center.Y).FirstOrDefault() ?? fallbackPin;
        }

        private static void AssignNonPinHolesToCalloutClusters(
            System.Collections.Generic.IList<HoleFeature> holes,
            System.Collections.Generic.IList<HoleCalloutCluster> clusters)
        {
            foreach (var hole in holes.Where(h => !h.IsPinHole && !h.IsSlotPoint))
            {
                var cluster = clusters
                    .OrderBy(g => DistanceToCalloutCluster(hole, g))
                    .ThenBy(g => DistanceSquared(hole.Center, g.BasePin.Center))
                    .FirstOrDefault();
                if (cluster != null)
                {
                    cluster.Members.Add(hole);
                }
            }
        }

        private static double DistanceToCalloutCluster(HoleFeature hole, HoleCalloutCluster cluster)
        {
            if (cluster == null || cluster.Pins.Count == 0)
            {
                return double.MaxValue;
            }

            if (cluster.Pins.Count == 1)
            {
                return System.Math.Sqrt(DistanceSquared(hole.Center, cluster.Pins[0].Center));
            }

            return cluster.Pins.Take(2).Sum(pin => System.Math.Sqrt(DistanceSquared(hole.Center, pin.Center)));
        }

        private static System.Collections.Generic.IEnumerable<System.Collections.Generic.IList<HoleFeature>> GroupCalloutClusterMembers(
            System.Collections.Generic.IEnumerable<HoleFeature> members,
            DimensionRuleConfig config)
        {
            foreach (var typeGroup in members
                .Where(h => h != null && !h.IsSlotPoint)
                .GroupBy(GetHoleCalloutTypeRank)
                .OrderBy(g => g.Key))
            {
                var groups = new System.Collections.Generic.List<System.Collections.Generic.List<HoleFeature>>();
                foreach (var hole in typeGroup.OrderBy(h => h.Diameter).ThenBy(h => h.Center.Y).ThenBy(h => h.Center.X))
                {
                    var group = groups.FirstOrDefault(g =>
                        System.Math.Abs(g.Average(h => h.Diameter) - hole.Diameter) <= config.GeometryTolerance
                        && g[0].HoleKind == hole.HoleKind
                        && string.Equals(g[0].FitTolerance ?? string.Empty, hole.FitTolerance ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(g[0].ThreadCallout ?? string.Empty, hole.ThreadCallout ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                    if (group == null)
                    {
                        group = new System.Collections.Generic.List<HoleFeature>();
                        groups.Add(group);
                    }

                    group.Add(hole);
                }

                foreach (var group in groups)
                {
                    yield return group;
                }
            }
        }

        private static int GetHoleCalloutTypeRank(HoleFeature hole)
        {
            if (hole.IsPinHole)
            {
                return 0;
            }

            if (hole.IsThreadHole)
            {
                return 1;
            }

            return 2;
        }

        private static bool IsSameHoleForCallout(HoleFeature a, HoleFeature b, DimensionRuleConfig config)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return a.Center.DistanceTo(b.Center) <= config.GeometryTolerance
                && System.Math.Abs(a.Diameter - b.Diameter) <= config.GeometryTolerance;
        }

        private static bool IsSameHoleForCallout(CadAuto.Core.Model.HoleFeature2D a, HoleFeature b, DimensionRuleConfig config)
        {
            if (a == null || b == null)
            {
                return false;
            }

            var dx = a.Center.X - b.Center.X;
            var dy = a.Center.Y - b.Center.Y;
            return dx * dx + dy * dy <= config.GeometryTolerance * config.GeometryTolerance
                && System.Math.Abs(a.Diameter - b.Diameter) <= config.GeometryTolerance;
        }

        private static double DistanceSquared(Autodesk.AutoCAD.Geometry.Point3d a, Autodesk.AutoCAD.Geometry.Point3d b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return dx * dx + dy * dy;
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
