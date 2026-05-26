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
                    var diameterGroups = BuildDiameterGroupsForPlacement(holes, datum.DatumHole, config);
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

                    if (!skipHoleDimensions)
                    {
                        diameterGroupsForPlacement = diameterGroups;
                        diameterCalloutDimStyleIdForPlacement = diameterCalloutDimStyleId;
                        annotationLayerForPlacement = annotationLayer;
                    }

                    tr.Commit();
                }

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

        private static System.Collections.Generic.IList<System.Collections.Generic.IList<HoleFeature>> BuildDiameterGroupsForPlacement(
            System.Collections.Generic.IEnumerable<HoleFeature> holes,
            HoleFeature datumPin,
            DimensionRuleConfig config)
        {
            var holeList = holes == null
                ? new System.Collections.Generic.List<HoleFeature>()
                : holes.Where(h => h != null && !h.IsSlotPoint).ToList();
            var pinHoles = holeList.Where(h => h.IsPinHole).ToList();
            if (pinHoles.Count == 0)
            {
                return BuildDiameterGroupsBySpatialRows(holeList, config);
            }

            var clusters = BuildPinCalloutClusters(pinHoles, datumPin, config);
            AssignNonPinHolesToCalloutClusters(holeList, clusters);
            var result = new System.Collections.Generic.List<System.Collections.Generic.IList<HoleFeature>>();
            foreach (var cluster in clusters)
            {
                result.AddRange(GroupCalloutClusterMembers(cluster.Members, config));
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
