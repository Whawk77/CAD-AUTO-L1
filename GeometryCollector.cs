using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace AutoFixtureDim
{
    public sealed class GeometryCollector
    {
        private readonly Editor _editor;

        public GeometryCollector(Editor editor)
        {
            _editor = editor;
        }

        public OutlineSelection PromptForOutlineSelection(Transaction tr)
        {
            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择 DRAWING 图层上的零件外轮廓，可点选或框选: ",
                SingleOnly = false
            };

            // Do not apply a DXF filter here. Some drawings use old POLYLINE entities,
            // and a hard filter can make AutoCAD report "0 found" before our diagnostics.
            var result = _editor.GetSelection(options);
            if (result.Status != PromptStatus.OK)
            {
                throw new OperationCanceledException("用户取消选择外轮廓。");
            }

            var candidates = new List<OutlineEntityCandidate>();
            var fallbackCandidates = new List<OutlineEntityCandidate>();
            var selectedIds = new List<ObjectId>();
            var selectedCount = 0;
            var drawingLayerCount = 0;
            var polylineCount = 0;
            var closedPolylineCount = 0;
            var fallbackOutlineCount = 0;

            foreach (SelectedObject selected in result.Value)
            {
                if (selected == null || selected.ObjectId.IsNull)
                {
                    continue;
                }

                selectedCount++;
                selectedIds.Add(selected.ObjectId);
                var entity = tr.GetObject(selected.ObjectId, OpenMode.ForRead) as Entity;
                if (entity == null)
                {
                    continue;
                }

                if (IsDrawingLayer(entity.Layer))
                {
                    drawingLayerCount++;
                }
                else
                {
                    continue;
                }

                if (!HasOutlineLayerLinetype(entity, tr))
                {
                    continue;
                }

                if (IsFallbackOutlineEntity(entity))
                {
                    fallbackOutlineCount++;
                    fallbackCandidates.Add(BuildOutlineEntityCandidate(selected.ObjectId, entity, tr));
                }

                if (!IsSupportedPolyline(entity))
                {
                    continue;
                }

                polylineCount++;
                if (!IsClosedPolyline(entity))
                {
                    continue;
                }

                closedPolylineCount++;
                candidates.Add(BuildOutlineEntityCandidate(selected.ObjectId, entity, tr));
            }

            if (candidates.Count > 0)
            {
                var selection = new OutlineSelection
                {
                    PrimaryPolylineId = candidates
                        .OrderByDescending(c => c.Score)
                        .ThenByDescending(c => c.BoundsArea)
                        .First()
                        .ObjectId
                };
                foreach (ObjectId id in selectedIds)
                {
                    selection.SelectedIds.Add(id);
                }

                return selection;
            }

            if (fallbackCandidates.Count > 0)
            {
                var mainOutline = SelectMainOutlineComponent(fallbackCandidates);
                _editor.WriteMessage(
                    "\n未找到闭合 Polyline，已改用 DRAWING 图层上的 {0} 个线/圆弧轮廓对象计算外包围盒。",
                    mainOutline.Count);

                var selection = new OutlineSelection();
                foreach (ObjectId id in mainOutline.Select(c => c.ObjectId))
                {
                    selection.EntityIds.Add(id);
                }

                foreach (ObjectId id in selectedIds)
                {
                    selection.SelectedIds.Add(id);
                }

                return selection;
            }

            _editor.WriteMessage(
                    "\n外轮廓筛选结果: 已选 {0} 个对象，DRAWING 图层 {1} 个，多段线 {2} 个，闭合多段线 {3} 个，可用线/圆弧轮廓对象 {4} 个。",
                    selectedCount,
                    drawingLayerCount,
                    polylineCount,
                    closedPolylineCount,
                    fallbackOutlineCount);
            throw new InvalidOperationException("选择集中没有 DRAWING 图层上的可用外轮廓对象。");
        }

        public IList<ObjectId> PromptForCircleHoles()
        {
            var options = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择需要标注的圆孔 Circle: "
            };
            var filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "CIRCLE")
            });

            var result = _editor.GetSelection(options, filter);
            if (result.Status != PromptStatus.OK)
            {
                return null;
            }

            var ids = new List<ObjectId>();
            foreach (SelectedObject selected in result.Value)
            {
                if (selected != null && !selected.ObjectId.IsNull)
                {
                    ids.Add(selected.ObjectId);
                }
            }

            if (ids.Count == 0)
            {
                return null;
            }

            return ids;
        }

        public IList<ObjectId> CollectCircleHolesFromOutlineSelection(Transaction tr, OutlineFeature outline, OutlineSelection outlineSelection, double tolerance)
        {
            var outlineIds = new HashSet<ObjectId>();
            if (outlineSelection != null)
            {
                if (outlineSelection.HasPrimaryPolyline)
                {
                    outlineIds.Add(outlineSelection.PrimaryPolylineId);
                }

                foreach (ObjectId id in outlineSelection.EntityIds)
                {
                    outlineIds.Add(id);
                }
            }

            var ids = new List<ObjectId>();
            if (outlineSelection == null)
            {
                return ids;
            }

            foreach (ObjectId entityId in outlineSelection.SelectedIds)
            {
                if (outlineIds.Contains(entityId))
                {
                    continue;
                }

                var circle = tr.GetObject(entityId, OpenMode.ForRead) as Circle;
                if (circle == null)
                {
                    continue;
                }

                if (IsPointInsideBounds(circle.Center, outline, tolerance))
                {
                    ids.Add(entityId);
                }
            }

            _editor.WriteMessage("\n从本次框选对象中自动识别到 Circle 孔 {0} 个。", ids.Count);
            return ids;
        }

        public IList<ObjectId> CollectHoleSourcesFromOutlineSelection(Transaction tr, OutlineFeature outline, OutlineSelection outlineSelection, DimensionRuleConfig config)
        {
            double tolerance = config.GeometryTolerance;
            var outlineIds = new HashSet<ObjectId>();
            if (outlineSelection != null)
            {
                if (outlineSelection.HasPrimaryPolyline)
                {
                    outlineIds.Add(outlineSelection.PrimaryPolylineId);
                }

                foreach (ObjectId id in outlineSelection.EntityIds)
                {
                    outlineIds.Add(id);
                }
            }

            var ids = new List<ObjectId>();
            if (outlineSelection == null)
            {
                return ids;
            }

            foreach (ObjectId entityId in outlineSelection.SelectedIds)
            {
                var entity = tr.GetObject(entityId, OpenMode.ForRead) as Entity;
                if (entity == null)
                {
                    continue;
                }

                var circle = entity as Circle;
                if (circle != null)
                {
                    if (!outlineIds.Contains(entityId) && IsPointInsideBounds(circle.Center, outline, tolerance))
                    {
                        ids.Add(entityId);
                    }

                    continue;
                }

                var arc = entity as Arc;
                if (arc != null && IsDrawingLayer(arc.Layer) && IsThreadArc(arc, config) && IsPointInsideBounds(arc.Center, outline, tolerance))
                {
                    ids.Add(entityId);
                    continue;
                }

                if (outlineIds.Contains(entityId))
                {
                    continue;
                }

                if (arc != null && IsDrawingLayer(arc.Layer) && !IsThreadArc(arc, config) && IsPointInsideBounds(arc.Center, outline, tolerance))
                {
                    ids.Add(entityId);
                    continue;
                }

                var line = entity as Line;
                if (line != null && IsDrawingLayer(line.Layer) && IsLineInsideBounds(line, outline, tolerance))
                {
                    ids.Add(entityId);
                }
            }

            _editor.WriteMessage("\n从本次框选对象中自动识别到孔特征 {0} 个。", ids.Count);
            return ids;
        }

        public DatumDefinition PromptForDatum(OutlineFeature outline)
        {
            var datum = DatumDefinition.FromOutline(outline);
            var keywordOptions = new PromptKeywordOptions("\n基准边 [Default/Specify] <Default>: ");
            keywordOptions.Keywords.Add("Default");
            keywordOptions.Keywords.Add("Specify");
            keywordOptions.Keywords.Default = "Default";
            keywordOptions.AllowNone = true;

            var keywordResult = _editor.GetKeywords(keywordOptions);
            if (keywordResult.Status != PromptStatus.OK || keywordResult.StringResult == "Default")
            {
                return datum;
            }

            var leftPrompt = new PromptPointOptions("\n指定左侧基准边上的一点，用于确定基准 X: ");
            var leftResult = _editor.GetPoint(leftPrompt);
            if (leftResult.Status == PromptStatus.OK)
            {
                datum.BaseX = leftResult.Value.X;
            }

            var bottomPrompt = new PromptPointOptions("\n指定下侧基准边上的一点，用于确定基准 Y: ");
            var bottomResult = _editor.GetPoint(bottomPrompt);
            if (bottomResult.Status == PromptStatus.OK)
            {
                datum.BaseY = bottomResult.Value.Y;
            }

            return datum;
        }

        public HoleFeature PromptForDatumHole(List<HoleFeature> pinHoles, Transaction tr, OutlineFeature outline)
        {
            _editor.WriteMessage("\n自动识别销孔数量: {0}", pinHoles.Count);

            var options = new PromptEntityOptions("\n请选择一个销孔作为基准孔: ");
            options.SetRejectMessage("\n所选对象不是 Circle，请重新选择销孔。");
            options.AddAllowedClass(typeof(Circle), exactMatch: true);

            var result = _editor.GetEntity(options);
            if (result.Status != PromptStatus.OK)
            {
                return null;
            }

            foreach (var hole in pinHoles)
            {
                if (hole.CircleId == result.ObjectId)
                {
                    _editor.WriteMessage("\n已匹配到已识别销孔 (ObjectId)，直径={0:0.###}", hole.Diameter);
                    return hole;
                }
            }

            var selectedCircle = tr.GetObject(result.ObjectId, OpenMode.ForRead) as Circle;
            if (selectedCircle == null)
            {
                _editor.WriteMessage("\n所选对象无法读取为 Circle，将使用轮廓边线基准。");
                return null;
            }

            _editor.WriteMessage("\n用户选择 Circle 直径={0:0.###}", selectedCircle.Radius * 2.0);

            foreach (var hole in pinHoles)
            {
                var centerDist = hole.Center.DistanceTo(selectedCircle.Center);
                var radiusDiff = Math.Abs(hole.Diameter / 2.0 - selectedCircle.Radius);
                if (centerDist <= 0.01 && radiusDiff <= 0.01)
                {
                    _editor.WriteMessage("\n已匹配到已识别销孔 (容差匹配)，直径={0:0.###}", hole.Diameter);
                    return hole;
                }
            }

            if (selectedCircle.Radius < 0.5 || selectedCircle.Radius > 100.0)
            {
                _editor.WriteMessage("\n所选 Circle 半径={0:0.###} 超出合理孔径范围，将使用轮廓边线基准。", selectedCircle.Radius);
                return null;
            }

            if (outline != null && !IsPointInsideBounds(selectedCircle.Center, outline, 0.01))
            {
                _editor.WriteMessage("\n所选 Circle 圆心不在外轮廓范围内，将使用轮廓边线基准。");
                return null;
            }

            _editor.WriteMessage("\n所选 Circle 未在自动识别销孔列表中，已作为手动基准销孔使用。");
            return new HoleFeature
            {
                Center = selectedCircle.Center,
                Diameter = selectedCircle.Radius * 2.0,
                SourceId = result.ObjectId,
                CircleId = result.ObjectId,
                HoleKind = HoleKind.Pin
            };
        }

        public bool PromptForDatumHoleLocationPoints(DimensionRuleConfig config, out double? xBase, out double? yBase, out bool useToleranceX, out bool useToleranceY)
        {
            xBase = null;
            yBase = null;
            useToleranceX = false;
            useToleranceY = false;

            var xOptions = new PromptPointOptions("\n请选择基准孔 X 轴方向上的基准点: ");
            var xResult = _editor.GetPoint(xOptions);
            if (xResult.Status != PromptStatus.OK)
            {
                return false;
            }
            xBase = xResult.Value.X;

            if (!PromptForDatumHoleToleranceMode(config, "X", out useToleranceX))
            {
                return false;
            }

            var yOptions = new PromptPointOptions("\n请选择基准孔 Y 轴方向上的基准点: ");
            var yResult = _editor.GetPoint(yOptions);
            if (yResult.Status != PromptStatus.OK)
            {
                return false;
            }
            yBase = yResult.Value.Y;

            if (!PromptForDatumHoleToleranceMode(config, "Y", out useToleranceY))
            {
                return false;
            }

            return true;
        }

        private bool PromptForDatumHoleToleranceMode(DimensionRuleConfig config, string axisName, out bool useTolerance)
        {
            useTolerance = config != null && config.DatumHoleLocationDefaultUseTolerance;
            var defaultKeyword = useTolerance ? "S" : "A";
            var options = new PromptKeywordOptions(
                "\nDatum hole " + axisName + " tolerance [A=NoTolerance/S=PlusMinus0.05] <" + defaultKeyword + ">: ");
            options.Keywords.Add("A");
            options.Keywords.Add("S");
            options.Keywords.Default = defaultKeyword;
            options.AllowNone = true;

            var result = _editor.GetKeywords(options);
            if (result.Status == PromptStatus.None)
            {
                return true;
            }

            if (result.Status != PromptStatus.OK)
            {
                return false;
            }

            useTolerance = string.Equals(result.StringResult, "S", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        public bool PromptForManualCornerLeaderPlacement()
        {
            var options = new PromptKeywordOptions("\nCorner feature text placement [Auto/Manual] <Auto>: ");
            options.Keywords.Add("Auto");
            options.Keywords.Add("Manual");
            options.Keywords.Default = "Auto";
            options.AllowNone = true;

            var result = _editor.GetKeywords(options);
            if (result.Status == PromptStatus.None)
            {
                return false;
            }

            return result.Status == PromptStatus.OK
                && string.Equals(result.StringResult, "Manual", StringComparison.OrdinalIgnoreCase);
        }

        private static double GetBoundsArea(Entity entity)
        {
            try
            {
                Extents3d extents = entity.GeometricExtents;
                return (extents.MaxPoint.X - extents.MinPoint.X) * (extents.MaxPoint.Y - extents.MinPoint.Y);
            }
            catch
            {
                return 0.0;
            }
        }

        private static OutlineEntityCandidate BuildOutlineEntityCandidate(ObjectId id, Entity entity, Transaction tr)
        {
            var endpoints = GetContinuityPoints(entity, tr);
            var boundsArea = GetBoundsArea(entity);
            var layerSemanticScore = GetLayerSemanticScore(entity.Layer);
            var lineWeightScore = GetLineWeightScore(entity, tr);
            var isClosed = IsClosedPolyline(entity);

            return new OutlineEntityCandidate
            {
                ObjectId = id,
                BoundsArea = boundsArea,
                ContinuityPoints = endpoints,
                IsClosed = isClosed,
                LineWeightScore = lineWeightScore,
                LayerSemanticScore = layerSemanticScore,
                Length = GetEntityLength(entity, tr),
                Score = layerSemanticScore * 1000000000000.0
                    + lineWeightScore * 1000000000.0
                    + (isClosed ? 100000000.0 : 0.0)
                    + endpoints.Count * 1000000.0
                    + boundsArea
            };
        }

        private static List<OutlineEntityCandidate> SelectMainOutlineComponent(IList<OutlineEntityCandidate> candidates)
        {
            var remaining = new HashSet<OutlineEntityCandidate>(candidates);
            var components = new List<List<OutlineEntityCandidate>>();

            while (remaining.Count > 0)
            {
                var seed = remaining.First();
                var component = new List<OutlineEntityCandidate>();
                var queue = new Queue<OutlineEntityCandidate>();
                queue.Enqueue(seed);
                remaining.Remove(seed);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    component.Add(current);

                    foreach (var next in remaining.ToList())
                    {
                        if (!AreConnected(current, next))
                        {
                            continue;
                        }

                        remaining.Remove(next);
                        queue.Enqueue(next);
                    }
                }

                components.Add(component);
            }

            return components
                .OrderByDescending(GetComponentScore)
                .First();
        }

        private static double GetComponentScore(IList<OutlineEntityCandidate> component)
        {
            var minX = double.MaxValue;
            var minY = double.MaxValue;
            var maxX = double.MinValue;
            var maxY = double.MinValue;

            foreach (var candidate in component)
            {
                foreach (var point in candidate.ContinuityPoints)
                {
                    minX = Math.Min(minX, point.X);
                    maxX = Math.Max(maxX, point.X);
                    minY = Math.Min(minY, point.Y);
                    maxY = Math.Max(maxY, point.Y);
                }
            }

            var area = minX == double.MaxValue ? 0.0 : (maxX - minX) * (maxY - minY);
            return component.Max(c => c.LayerSemanticScore) * 1000000000000.0
                + component.Max(c => c.LineWeightScore) * 1000000000.0
                + (IsClosedComponent(component) ? 100000000.0 : 0.0)
                + component.Count * 1000000.0
                + component.Sum(c => c.Length)
                + area;
        }

        private static bool IsClosedComponent(IList<OutlineEntityCandidate> component)
        {
            if (component.Any(c => c.IsClosed))
            {
                return true;
            }

            var points = component.SelectMany(c => c.ContinuityPoints).ToList();
            if (points.Count <= 2)
            {
                return false;
            }

            foreach (var point in points)
            {
                if (points.Count(p => AreSamePoint(point, p)) < 2)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreConnected(OutlineEntityCandidate a, OutlineEntityCandidate b)
        {
            foreach (var pa in a.ContinuityPoints)
            {
                foreach (var pb in b.ContinuityPoints)
                {
                    if (AreSamePoint(pa, pb))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool AreSamePoint(Point2d a, Point2d b)
        {
            return a.GetDistanceTo(b) <= 0.01;
        }

        private static List<Point2d> GetContinuityPoints(Entity entity, Transaction tr)
        {
            var line = entity as Line;
            if (line != null)
            {
                return new List<Point2d>
                {
                    new Point2d(line.StartPoint.X, line.StartPoint.Y),
                    new Point2d(line.EndPoint.X, line.EndPoint.Y)
                };
            }

            var arc = entity as Arc;
            if (arc != null)
            {
                return new List<Point2d>
                {
                    new Point2d(arc.StartPoint.X, arc.StartPoint.Y),
                    new Point2d(arc.EndPoint.X, arc.EndPoint.Y)
                };
            }

            var lightweight = entity as Polyline;
            if (lightweight != null)
            {
                var points = new List<Point2d>();
                for (var i = 0; i < lightweight.NumberOfVertices; i++)
                {
                    points.Add(lightweight.GetPoint2dAt(i));
                }

                return points;
            }

            var polyline2d = entity as Polyline2d;
            if (polyline2d != null)
            {
                var points = new List<Point2d>();
                foreach (ObjectId vertexId in polyline2d)
                {
                    var vertex = tr.GetObject(vertexId, OpenMode.ForRead) as Vertex2d;
                    if (vertex != null)
                    {
                        points.Add(new Point2d(vertex.Position.X, vertex.Position.Y));
                    }
                }

                return points;
            }

            return new List<Point2d>();
        }

        private static double GetEntityLength(Entity entity, Transaction tr)
        {
            var line = entity as Line;
            if (line != null)
            {
                return line.Length;
            }

            var arc = entity as Arc;
            if (arc != null)
            {
                return arc.Length;
            }

            var lightweight = entity as Polyline;
            if (lightweight != null)
            {
                return lightweight.Length;
            }

            var polyline2d = entity as Polyline2d;
            if (polyline2d != null)
            {
                var points = GetContinuityPoints(polyline2d, tr);
                double length = 0.0;
                for (int i = 1; i < points.Count; i++)
                {
                    length += points[i - 1].GetDistanceTo(points[i]);
                }

                if (polyline2d.Closed && points.Count > 1)
                {
                    length += points[points.Count - 1].GetDistanceTo(points[0]);
                }

                return length;
            }

            return 0.0;
        }

        private static double GetLayerSemanticScore(string layerName)
        {
            var layer = (layerName ?? string.Empty).Trim();
            if (string.Equals(layer, "DRAWING", StringComparison.OrdinalIgnoreCase))
            {
                return 100.0;
            }

            return layer.IndexOf("OUTLINE", StringComparison.OrdinalIgnoreCase) >= 0
                || layer.IndexOf("CONTOUR", StringComparison.OrdinalIgnoreCase) >= 0
                ? 80.0
                : 0.0;
        }

        private static double GetLineWeightScore(Entity entity, Transaction tr)
        {
            var lineWeight = entity.LineWeight;
            if (lineWeight == LineWeight.ByLayer)
            {
                var layer = GetLayerRecord(entity, tr);
                if (layer != null)
                {
                    lineWeight = layer.LineWeight;
                }
            }

            var value = (int)lineWeight;
            return value > 0 ? value : 0.0;
        }

        private static bool HasOutlineLayerLinetype(Entity entity, Transaction tr)
        {
            var layer = GetLayerRecord(entity, tr);
            if (layer == null || layer.LinetypeObjectId.IsNull)
            {
                return true;
            }

            var linetype = tr.GetObject(layer.LinetypeObjectId, OpenMode.ForRead) as LinetypeTableRecord;
            var name = linetype == null ? string.Empty : linetype.Name;
            return !IsNonOutlineLinetype(name);
        }

        private static LayerTableRecord GetLayerRecord(Entity entity, Transaction tr)
        {
            if (entity == null || entity.LayerId.IsNull)
            {
                return null;
            }

            return tr.GetObject(entity.LayerId, OpenMode.ForRead) as LayerTableRecord;
        }

        private static bool IsNonOutlineLinetype(string linetypeName)
        {
            var name = (linetypeName ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                return false;
            }

            return name.IndexOf("CENTER", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("HIDDEN", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("DASH", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("DOT", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("PHANTOM", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDrawingLayer(string layerName)
        {
            return string.Equals((layerName ?? string.Empty).Trim(), "DRAWING", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSupportedPolyline(Entity entity)
        {
            return entity is Polyline || entity is Polyline2d;
        }

        private static bool IsClosedPolyline(Entity entity)
        {
            var lightweight = entity as Polyline;
            if (lightweight != null)
            {
                return lightweight.Closed;
            }

            var polyline2d = entity as Polyline2d;
            return polyline2d != null && polyline2d.Closed;
        }

        private static bool IsFallbackOutlineEntity(Entity entity)
        {
            var arc = entity as Arc;
            if (arc != null && IsDrawingLayer(arc.Layer) && IsThreadArc(arc, DimensionRuleConfig.CreateDefault()))
            {
                return false;
            }

            return entity is Line
                || entity is Arc
                || entity is Polyline
                || entity is Polyline2d;
        }

        private static bool IsPointInsideBounds(Point3d point, OutlineFeature outline, double tolerance)
        {
            return point.X >= outline.MinX - tolerance
                && point.X <= outline.MaxX + tolerance
                && point.Y >= outline.MinY - tolerance
                && point.Y <= outline.MaxY + tolerance;
        }

        private static bool IsLineInsideBounds(Line line, OutlineFeature outline, double tolerance)
        {
            var midpoint = new Point3d(
                (line.StartPoint.X + line.EndPoint.X) / 2.0,
                (line.StartPoint.Y + line.EndPoint.Y) / 2.0,
                0.0);
            return IsPointInsideBounds(line.StartPoint, outline, tolerance)
                || IsPointInsideBounds(line.EndPoint, outline, tolerance)
                || IsPointInsideBounds(midpoint, outline, tolerance);
        }

        private static bool IsThreadArc(Arc arc, DimensionRuleConfig config)
        {
            double sweep = arc.EndAngle - arc.StartAngle;
            while (sweep < 0.0)
            {
                sweep += Math.PI * 2.0;
            }

            while (sweep > Math.PI * 2.0)
            {
                sweep -= Math.PI * 2.0;
            }

            double minimum = Math.PI * 1.5;
            return sweep >= minimum - config.GeometryTolerance;
        }

        private sealed class OutlineEntityCandidate
        {
            public ObjectId ObjectId { get; set; }
            public double BoundsArea { get; set; }
            public List<Point2d> ContinuityPoints { get; set; }
            public bool IsClosed { get; set; }
            public double LineWeightScore { get; set; }
            public double LayerSemanticScore { get; set; }
            public double Length { get; set; }
            public double Score { get; set; }
        }
    }
}
