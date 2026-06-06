using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;
using CadAuto.Core.Rules;

namespace CadAuto.CadAdapter.Recognition
{
    public sealed class FeatureRecognizer
    {
        private static readonly bool DiagnosticsEnabled = false;
        private readonly DimensionRuleConfig _config;

        public FeatureRecognizer(DimensionRuleConfig config)
        {
            _config = config;
        }

        public IList<SlotFeature> LastRecognizedSlots { get; private set; } = new List<SlotFeature>();

        public OutlineFeature RecognizeOutline(Entity entity, Transaction tr)
        {
            OutlineFeature outline;
            var polyline = entity as Polyline;
            if (polyline != null)
            {
                outline = RecognizeLightweightOutline(polyline);
                RecognizeOutlineCornerFeatures(outline);
                return outline;
            }

            var polyline2d = entity as Polyline2d;
            if (polyline2d != null)
            {
                outline = RecognizePolyline2dOutline(polyline2d, tr);
                RecognizeOutlineCornerFeatures(outline);
                return outline;
            }

            throw new InvalidOperationException("Outline must be a closed Polyline or DRAWING outline component.");
        }

        public OutlineFeature RecognizeOutline(IEnumerable<ObjectId> outlineEntityIds, Transaction tr)
        {
            var outline = CreateEmptyOutline();

            foreach (ObjectId id in outlineEntityIds)
            {
                var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (entity == null)
                {
                    continue;
                }

                AddEntityExtents(outline, entity);
                AddEntityKeyPoints(outline, entity, tr);
            }

            if (outline.MinX == double.MaxValue)
            {
                throw new InvalidOperationException("Unable to calculate outline extents from DRAWING objects.");
            }

            RecognizeOutlineCornerFeatures(outline);
            return outline;
        }

        public IList<HoleFeature> RecognizeHoles(IEnumerable<ObjectId> sourceIds, Transaction tr, IEnumerable<ObjectId> contextIds)
        {
            var sourceList = sourceIds == null ? new List<ObjectId>() : sourceIds.ToList();
            LastRecognizedSlots = RecognizeSlotFeatures(sourceList, tr);
            var slotSourceIds = new HashSet<ObjectId>(LastRecognizedSlots.SelectMany(GetSlotSourceIds));
            var pinMarkers = CollectPinMarkers(contextIds, sourceList, tr);
            var threadArcs = CollectThreadArcInfos(sourceList, tr);
            var threadMinorCircles = CollectThreadMinorCircles(sourceList, tr, threadArcs);
            var holes = new List<HoleFeature>();

            int circleCount = 0;
            int suppressedCount = 0;
            int threadByMinorCount = 0;
            int pinMatchCount = 0;
            int pinNoMatchCount = 0;
            var suppressedCircles = new List<string>();
            var threadArcDiag = new List<string>();

            foreach (var ta in threadArcs)
            {
                threadArcDiag.Add(string.Format("(涓績={0:0.###},{1:0.###} R={2:0.###})", ta.Center.X, ta.Center.Y, ta.Radius));
            }

            foreach (var slot in LastRecognizedSlots)
            {
                holes.Add(CreateSlotPoint(slot, slot.FirstCenter, slot.FirstArcId));
                if (!slot.IsSingleArcSlot)
                {
                    holes.Add(CreateSlotPoint(slot, slot.SecondCenter, slot.SecondArcId));
                }
            }

            foreach (ObjectId id in sourceList)
            {
                if (slotSourceIds.Contains(id))
                {
                    continue;
                }

                var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                var circle = entity as Circle;
                if (circle != null)
                {
                    circleCount++;
                    var diameter = circle.Radius * 2.0;
                    var threadCallout = _config.GetThreadCalloutForMinorDiameter(diameter);
                    var hasThreadArcAtCenter = HasThreadArcAtCenter(circle.Center, threadArcs);
                    if (!string.IsNullOrEmpty(threadCallout) && hasThreadArcAtCenter)
                    {
                        suppressedCount++;
                        threadByMinorCount++;
                        suppressedCircles.Add(FormatCircleSuppression(circle, "thread minor " + threadCallout));
                        continue;
                    }

                    if (IsSuppressedByThreadArc(circle, threadArcs))
                    {
                        suppressedCount++;
                        suppressedCircles.Add(string.Format("(涓績={0:0.###},{1:0.###} R={2:0.###})", circle.Center.X, circle.Center.Y, circle.Radius));
                        continue;
                    }

                    var isPinHole = HasPinMarker(circle.Center, circle.Radius, pinMarkers);
                    if (isPinHole) pinMatchCount++;
                    else pinNoMatchCount++;

                    holes.Add(new HoleFeature
                    {
                        Center = circle.Center,
                        Diameter = diameter,
                        SourceId = id,
                        CircleId = id,
                        HoleKind = isPinHole ? HoleKind.Pin : HoleKind.Normal,
                        FitTolerance = isPinHole ? _config.PinHoleFitToleranceText : string.Empty,
                        ThreadCallout = string.Empty
                    });
                    continue;
                }

                var arc = entity as Arc;
                if (arc != null && IsConfirmedThreadArc(arc, sourceList, tr))
                {
                    var threadCallout = FindThreadMinorCallout(arc.Center, threadMinorCircles);
                    if (string.IsNullOrEmpty(threadCallout))
                    {
                        threadCallout = _config.GetThreadCalloutForMinorDiameter(arc.Radius * 2.0);
                    }

                    holes.Add(new HoleFeature
                    {
                        Center = arc.Center,
                        Diameter = arc.Radius * 2.0,
                        SourceId = id,
                        CircleId = ObjectId.Null,
                        HoleKind = HoleKind.Thread,
                        FitTolerance = string.Empty,
                        ThreadCallout = threadCallout
                    });
                }
            }

            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc != null && DiagnosticsEnabled)
            {
                var ed = doc.Editor;
                ed.WriteMessage("\n[璇婃柇] Circle鎬绘暟={0}, 琚灪绾瑰姬鎶戝埗={1}, 琚灪绾瑰皬寰勫尮閰?{2}, 閿€瀛旀爣璁板尮閰?{3}, 閿€瀛旀爣璁版湭鍖归厤={4}",
                    circleCount, suppressedCount, threadByMinorCount, pinMatchCount, pinNoMatchCount);
                ed.WriteMessage("\n[diagnostic] Hole recognition: circles={0}, suppressed={1}, threadMinorSuppressed={2}, pinMatched={3}, normalCircles={4}",
                    circleCount, suppressedCount, threadByMinorCount, pinMatchCount, pinNoMatchCount);
                WriteHoleDiagnostics(ed, holes, suppressedCircles);
                WriteSlotDiagnostics(ed, LastRecognizedSlots);
                if (pinNoMatchCount > 0 && pinMarkers.Count > 0)
                {
                    ed.WriteMessage("\n[璇婃柇] 閿€瀛旀爣璁颁綅缃?");
                    foreach (var m in pinMarkers)
                    {
                        ed.WriteMessage(" ({0:0.###},{1:0.###})", m.X, m.Y);
                    }
                    ed.WriteMessage("\n[璇婃柇] 鏈尮閰嶉攢瀛旂殑Circle:");
                    foreach (var h in holes)
                    {
                        if (h.HoleKind == HoleKind.Normal)
                        {
                            ed.WriteMessage(" (涓績={0:0.###},{1:0.###} R={2:0.###})", h.Center.X, h.Center.Y, h.Diameter / 2.0);
                        }
                    }
                }
                if (threadArcDiag.Count > 0)
                {
                    ed.WriteMessage("\n[璇婃柇] 纭铻虹汗寮? {0}", string.Join(", ", threadArcDiag));
                }
                if (suppressedCircles.Count > 0)
                {
                    ed.WriteMessage("\n[璇婃柇] 琚姂鍒剁殑Circle: {0}", string.Join(", ", suppressedCircles));
                }
            }

            return holes;
        }

        public IList<SlotFeature> RecognizeOutlineSlotFeatures(OutlineFeature outline)
        {
            var slots = new List<SlotFeature>();
            if (outline == null || outline.Arcs.Count == 0 || outline.Segments.Count < 2)
            {
                return slots;
            }

            var slotIndex = 1;
            foreach (var arc in outline.Arcs)
            {
                if (!IsHalfArc(arc))
                {
                    continue;
                }

                var candidates = outline.Segments
                    .Where(s => !s.IsArcChord)
                    .Select(s => new SlotLineCandidate
                    {
                        Id = s.SourceId,
                        Start = s.Start,
                        End = s.End
                    })
                    .Where(l => LineTouchesSlotArc(l, ToSlotArcCandidate(arc)))
                    .ToList();

                for (int i = 0; i < candidates.Count; i++)
                {
                    for (int j = i + 1; j < candidates.Count; j++)
                    {
                        bool horizontal;
                        var arcCandidate = ToSlotArcCandidate(arc);
                        if (!CanPairSingleArcSlotLines(arcCandidate, candidates[i], candidates[j], out horizontal))
                        {
                            continue;
                        }

                        slots.Add(new SlotFeature
                        {
                            GroupId = "OUTLINE_SLOT" + slotIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            FirstCenter = arc.Center,
                            SecondCenter = arc.Center,
                            Radius = arc.Radius,
                            CenterDistance = 0.0,
                            FirstArcId = arc.SourceId,
                            FirstLineId = candidates[i].Id,
                            SecondLineId = candidates[j].Id,
                            IsSingleArcSlot = true,
                            IsVertical = !horizontal,
                            ArcLeaderTarget = GetOutlineArcMidPoint(arc)
                        });

                        slotIndex++;
                        i = candidates.Count;
                        break;
                    }
                }
            }

            return slots;
        }

        private static void WriteHoleDiagnostics(
            Autodesk.AutoCAD.EditorInput.Editor editor,
            IEnumerable<HoleFeature> holes,
            IEnumerable<string> suppressedCircles)
        {
            var holeList = holes == null ? new List<HoleFeature>() : holes.ToList();
            var pinHoles = holeList.Where(h => h.HoleKind == HoleKind.Pin).ToList();
            var normalHoles = holeList.Where(h => h.HoleKind == HoleKind.Normal).ToList();
            var threadHoles = holeList.Where(h => h.HoleKind == HoleKind.Thread).ToList();
            var slotPoints = holeList.Where(h => h.HoleKind == HoleKind.Slot).ToList();

            editor.WriteMessage("\n[diagnostic] Pin holes ({0}):", pinHoles.Count);
            foreach (var hole in pinHoles)
            {
                editor.WriteMessage(
                    " center=({0:0.###},{1:0.###}) diameter={2:0.###} fit={3};",
                    hole.Center.X,
                    hole.Center.Y,
                    hole.Diameter,
                    string.IsNullOrEmpty(hole.FitTolerance) ? "none" : hole.FitTolerance);
            }

            editor.WriteMessage("\n[diagnostic] Normal holes ({0}):", normalHoles.Count);
            foreach (var hole in normalHoles)
            {
                editor.WriteMessage(
                    " center=({0:0.###},{1:0.###}) diameter={2:0.###};",
                    hole.Center.X,
                    hole.Center.Y,
                    hole.Diameter);
            }

            editor.WriteMessage("\n[diagnostic] Thread holes ({0}):", threadHoles.Count);
            foreach (var hole in threadHoles)
            {
                editor.WriteMessage(
                    " center=({0:0.###},{1:0.###}) callout={2};",
                    hole.Center.X,
                    hole.Center.Y,
                    string.IsNullOrEmpty(hole.ThreadCallout) ? "unknown" : hole.ThreadCallout);
            }

            editor.WriteMessage("\n[diagnostic] Slot points ({0}):", slotPoints.Count);
            foreach (var hole in slotPoints)
            {
                editor.WriteMessage(
                    " center=({0:0.###},{1:0.###}) radius={2:0.###};",
                    hole.Center.X,
                    hole.Center.Y,
                    hole.Diameter / 2.0);
            }

            var suppressed = suppressedCircles == null
                ? new List<string>()
                : suppressedCircles
                    .Select(s => s.IndexOf("reason=", StringComparison.OrdinalIgnoreCase) >= 0 ? s : s + " reason=inside thread arc")
                    .ToList();
            if (suppressed.Count > 0)
            {
                editor.WriteMessage("\n[diagnostic] Suppressed circles with reason: {0}", string.Join(", ", suppressed));
            }
        }

        private static string FormatCircleSuppression(Circle circle, string reason)
        {
            return string.Format(
                "center=({0:0.###},{1:0.###}) radius={2:0.###} reason={3}",
                circle.Center.X,
                circle.Center.Y,
                circle.Radius,
                reason);
        }

        public IList<HoleFeature> RecognizeHoles(IEnumerable<ObjectId> circleIds, Transaction tr)
        {
            return RecognizeHoles(circleIds, tr, new ObjectId[0]);
        }

        public IList<IList<HoleFeature>> GroupHolesByHorizontalRow(IEnumerable<HoleFeature> holes)
        {
            var sorted = holes.OrderBy(h => h.Center.Y).ThenBy(h => h.Center.X).ToList();
            var rows = new List<IList<HoleFeature>>();

            foreach (var hole in sorted)
            {
                var row = rows.FirstOrDefault(r => Math.Abs(GetAverageY(r) - hole.Center.Y) <= _config.GeometryTolerance);
                if (row == null)
                {
                    rows.Add(new List<HoleFeature> { hole });
                }
                else
                {
                    row.Add(hole);
                }
            }

            foreach (var row in rows)
            {
                var ordered = row.OrderBy(h => h.Center.X).ToList();
                row.Clear();
                foreach (var hole in ordered)
                {
                    row.Add(hole);
                }
            }

            return rows.OrderBy(r => GetAverageY(r)).ToList();
        }

        public IList<IList<HoleFeature>> GroupHolesByDiameter(IEnumerable<HoleFeature> holes)
        {
            var sorted = holes
                .Where(h => !h.IsSlotPoint)
                .OrderBy(h => h.HoleKind)
                .ThenBy(h => h.Diameter)
                .ThenBy(h => h.FitTolerance)
                .ToList();
            var groups = new List<IList<HoleFeature>>();

            foreach (var hole in sorted)
            {
                var group = groups.FirstOrDefault(g =>
                    Math.Abs(GetAverageDiameter(g) - hole.Diameter) <= _config.GeometryTolerance
                    && g[0].HoleKind == hole.HoleKind
                    && string.Equals(g[0].FitTolerance ?? string.Empty, hole.FitTolerance ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(g[0].ThreadCallout ?? string.Empty, hole.ThreadCallout ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                if (group == null)
                {
                    groups.Add(new List<HoleFeature> { hole });
                }
                else
                {
                    group.Add(hole);
                }
            }

            return groups;
        }

        private IList<SlotFeature> RecognizeSlotFeatures(IList<ObjectId> sourceIds, Transaction tr)
        {
            var arcs = new List<SlotArcCandidate>();
            var lines = new List<SlotLineCandidate>();

            foreach (ObjectId id in sourceIds)
            {
                var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                var arc = entity as Arc;
                if (arc != null && IsDrawingLayer(arc.Layer) && !IsConfirmedThreadArc(arc, sourceIds, tr) && IsHalfArc(arc))
                {
                    arcs.Add(new SlotArcCandidate
                    {
                        Id = id,
                        Center = new Point2d(arc.Center.X, arc.Center.Y),
                        Start = new Point2d(arc.StartPoint.X, arc.StartPoint.Y),
                        End = new Point2d(arc.EndPoint.X, arc.EndPoint.Y),
                        Mid = GetArcMidPoint(arc),
                        Radius = arc.Radius
                    });
                    continue;
                }

                var line = entity as Line;
                if (line != null && IsDrawingLayer(line.Layer))
                {
                    lines.Add(new SlotLineCandidate
                    {
                        Id = id,
                        Start = new Point2d(line.StartPoint.X, line.StartPoint.Y),
                        End = new Point2d(line.EndPoint.X, line.EndPoint.Y)
                    });
                }
            }

            var slots = new List<SlotFeature>();
            var usedArcs = new HashSet<ObjectId>();
            var usedLines = new HashSet<ObjectId>();
            var slotIndex = 1;

            for (int i = 0; i < arcs.Count; i++)
            {
                if (usedArcs.Contains(arcs[i].Id))
                {
                    continue;
                }

                for (int j = i + 1; j < arcs.Count; j++)
                {
                    if (usedArcs.Contains(arcs[j].Id))
                    {
                        continue;
                    }

                    bool horizontal;
                    if (!CanPairSlotArcs(arcs[i], arcs[j], out horizontal))
                    {
                        continue;
                    }

                    var connectingLines = lines
                        .Where(l => !usedLines.Contains(l.Id)
                            && IsLineParallelToSlot(l, horizontal)
                            && ConnectsSlotArcs(l, arcs[i], arcs[j]))
                        .ToList();

                    if (connectingLines.Count != 2)
                    {
                        continue;
                    }

                    slots.Add(new SlotFeature
                    {
                        GroupId = "SLOT" + slotIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        FirstCenter = arcs[i].Center,
                        SecondCenter = arcs[j].Center,
                        Radius = (arcs[i].Radius + arcs[j].Radius) / 2.0,
                        CenterDistance = arcs[i].Center.GetDistanceTo(arcs[j].Center),
                        FirstArcId = arcs[i].Id,
                        SecondArcId = arcs[j].Id,
                        FirstLineId = connectingLines[0].Id,
                        SecondLineId = connectingLines[1].Id,
                        IsVertical = !horizontal
                    });

                    slotIndex++;
                    usedArcs.Add(arcs[i].Id);
                    usedArcs.Add(arcs[j].Id);
                    usedLines.Add(connectingLines[0].Id);
                    usedLines.Add(connectingLines[1].Id);
                    break;
                }
            }

            RecognizeSingleArcSlots(arcs, lines, usedArcs, usedLines, slots, ref slotIndex);
            return slots;
        }

        private void RecognizeSingleArcSlots(
            IList<SlotArcCandidate> arcs,
            IList<SlotLineCandidate> lines,
            ISet<ObjectId> usedArcs,
            ISet<ObjectId> usedLines,
            IList<SlotFeature> slots,
            ref int slotIndex)
        {
            foreach (var arc in arcs)
            {
                if (usedArcs.Contains(arc.Id))
                {
                    continue;
                }

                var candidates = lines
                    .Where(l => !usedLines.Contains(l.Id))
                    .Where(l => LineTouchesSlotArc(l, arc))
                    .ToList();

                for (int i = 0; i < candidates.Count; i++)
                {
                    for (int j = i + 1; j < candidates.Count; j++)
                    {
                        bool horizontal;
                        if (!CanPairSingleArcSlotLines(arc, candidates[i], candidates[j], out horizontal))
                        {
                            continue;
                        }

                        slots.Add(new SlotFeature
                        {
                            GroupId = "SLOT" + slotIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            FirstCenter = arc.Center,
                            SecondCenter = arc.Center,
                            Radius = arc.Radius,
                            CenterDistance = 0.0,
                            FirstArcId = arc.Id,
                            FirstLineId = candidates[i].Id,
                            SecondLineId = candidates[j].Id,
                            IsSingleArcSlot = true,
                            IsVertical = !horizontal,
                            ArcLeaderTarget = arc.Mid
                        });

                        slotIndex++;
                        usedArcs.Add(arc.Id);
                        usedLines.Add(candidates[i].Id);
                        usedLines.Add(candidates[j].Id);
                        i = candidates.Count;
                        break;
                    }
                }
            }
        }

        private static IEnumerable<ObjectId> GetSlotSourceIds(SlotFeature slot)
        {
            if (!slot.FirstArcId.IsNull) yield return slot.FirstArcId;
            if (!slot.SecondArcId.IsNull) yield return slot.SecondArcId;
            if (!slot.FirstLineId.IsNull) yield return slot.FirstLineId;
            if (!slot.SecondLineId.IsNull) yield return slot.SecondLineId;
        }

        private static HoleFeature CreateSlotPoint(SlotFeature slot, Point2d center, ObjectId sourceId)
        {
            return new HoleFeature
            {
                Center = new Point3d(center.X, center.Y, 0.0),
                Diameter = slot.Radius * 2.0,
                SourceId = sourceId,
                CircleId = ObjectId.Null,
                HoleKind = HoleKind.Slot,
                FitTolerance = string.Empty,
                ThreadCallout = string.Empty
            };
        }

        private static void WriteSlotDiagnostics(Autodesk.AutoCAD.EditorInput.Editor editor, IEnumerable<SlotFeature> slots)
        {
            var slotList = slots == null ? new List<SlotFeature>() : slots.ToList();
            editor.WriteMessage("\n[diagnostic] U slots ({0}):", slotList.Count);
            foreach (var slot in slotList)
            {
                editor.WriteMessage(
                    " radius={0:0.###} centerDistance={1:0.###} centers=({2:0.###},{3:0.###})/({4:0.###},{5:0.###});",
                    slot.Radius,
                    slot.CenterDistance,
                    slot.FirstCenter.X,
                    slot.FirstCenter.Y,
                    slot.SecondCenter.X,
                    slot.SecondCenter.Y);
            }
        }

        private bool IsHalfArc(Arc arc)
        {
            var sweep = GetArcSweep(arc);
            return Math.Abs(sweep - Math.PI) <= Math.PI / 12.0;
        }

        private bool IsHalfArc(OutlineArc arc)
        {
            var chord = arc.Start.GetDistanceTo(arc.End);
            var diameter = arc.Radius * 2.0;
            var tolerance = Math.Max(_config.GeometryTolerance, Math.Max(arc.Radius, 1.0) * 0.05);
            return arc.Radius > _config.GeometryTolerance
                && Math.Abs(chord - diameter) <= tolerance;
        }

        private SlotArcCandidate ToSlotArcCandidate(OutlineArc arc)
        {
            return new SlotArcCandidate
            {
                Id = arc.SourceId,
                Center = arc.Center,
                Start = arc.Start,
                End = arc.End,
                Mid = GetOutlineArcMidPoint(arc),
                Radius = arc.Radius
            };
        }

        private bool CanPairSlotArcs(SlotArcCandidate first, SlotArcCandidate second, out bool horizontal)
        {
            horizontal = false;
            var radiusTolerance = Math.Max(_config.GeometryTolerance, Math.Max(first.Radius, second.Radius) * 0.02);
            if (Math.Abs(first.Radius - second.Radius) > radiusTolerance)
            {
                return false;
            }

            var alignmentTolerance = Math.Max(_config.GeometryTolerance, Math.Max(first.Radius, second.Radius) * 0.05);
            var sameY = Math.Abs(first.Center.Y - second.Center.Y) <= alignmentTolerance;
            var sameX = Math.Abs(first.Center.X - second.Center.X) <= alignmentTolerance;
            if (!sameY && !sameX)
            {
                return false;
            }

            var centerDistance = first.Center.GetDistanceTo(second.Center);
            if (centerDistance <= Math.Max(_config.GeometryTolerance, first.Radius * 0.5))
            {
                return false;
            }

            horizontal = sameY;
            return true;
        }

        private bool CanPairSingleArcSlotLines(
            SlotArcCandidate arc,
            SlotLineCandidate first,
            SlotLineCandidate second,
            out bool horizontal)
        {
            horizontal = false;
            int firstArcEndpoint;
            int secondArcEndpoint;
            Point2d firstFreeEndpoint;
            Point2d secondFreeEndpoint;
            if (!TryGetSingleArcSlotLineEndpoints(first, arc, out firstArcEndpoint, out firstFreeEndpoint)
                || !TryGetSingleArcSlotLineEndpoints(second, arc, out secondArcEndpoint, out secondFreeEndpoint))
            {
                return false;
            }

            if (firstArcEndpoint == secondArcEndpoint)
            {
                return false;
            }

            var firstHorizontal = IsLineParallelToSlot(first, horizontal: true);
            var secondHorizontal = IsLineParallelToSlot(second, horizontal: true);
            var firstVertical = IsLineParallelToSlot(first, horizontal: false);
            var secondVertical = IsLineParallelToSlot(second, horizontal: false);
            if (firstHorizontal && secondHorizontal)
            {
                horizontal = true;
            }
            else if (firstVertical && secondVertical)
            {
                horizontal = false;
            }
            else
            {
                return false;
            }

            var tolerance = Math.Max(_config.GeometryTolerance, arc.Radius * 0.1);
            if (horizontal)
            {
                return Math.Abs(firstFreeEndpoint.X - secondFreeEndpoint.X) <= tolerance
                    && first.Length > Math.Max(_config.GeometryTolerance, arc.Radius * 0.25)
                    && second.Length > Math.Max(_config.GeometryTolerance, arc.Radius * 0.25);
            }

            return Math.Abs(firstFreeEndpoint.Y - secondFreeEndpoint.Y) <= tolerance
                && first.Length > Math.Max(_config.GeometryTolerance, arc.Radius * 0.25)
                && second.Length > Math.Max(_config.GeometryTolerance, arc.Radius * 0.25);
        }

        private bool IsLineParallelToSlot(SlotLineCandidate line, bool horizontal)
        {
            return horizontal
                ? Math.Abs(line.Start.Y - line.End.Y) <= Math.Max(_config.GeometryTolerance, line.Length * 0.01)
                : Math.Abs(line.Start.X - line.End.X) <= Math.Max(_config.GeometryTolerance, line.Length * 0.01);
        }

        private bool ConnectsSlotArcs(SlotLineCandidate line, SlotArcCandidate first, SlotArcCandidate second)
        {
            var tolerance = Math.Max(_config.GeometryTolerance, Math.Max(first.Radius, second.Radius) * 0.08);
            return (PointMatchesArcEndpoint(line.Start, first, tolerance) && PointMatchesArcEndpoint(line.End, second, tolerance))
                || (PointMatchesArcEndpoint(line.End, first, tolerance) && PointMatchesArcEndpoint(line.Start, second, tolerance));
        }

        private bool LineTouchesSlotArc(SlotLineCandidate line, SlotArcCandidate arc)
        {
            int endpoint;
            Point2d freeEndpoint;
            return TryGetSingleArcSlotLineEndpoints(line, arc, out endpoint, out freeEndpoint);
        }

        private bool TryGetSingleArcSlotLineEndpoints(
            SlotLineCandidate line,
            SlotArcCandidate arc,
            out int arcEndpoint,
            out Point2d freeEndpoint)
        {
            var tolerance = Math.Max(_config.GeometryTolerance, arc.Radius * 0.08);
            if (line.Start.GetDistanceTo(arc.Start) <= tolerance)
            {
                arcEndpoint = 1;
                freeEndpoint = line.End;
                return true;
            }

            if (line.End.GetDistanceTo(arc.Start) <= tolerance)
            {
                arcEndpoint = 1;
                freeEndpoint = line.Start;
                return true;
            }

            if (line.Start.GetDistanceTo(arc.End) <= tolerance)
            {
                arcEndpoint = 2;
                freeEndpoint = line.End;
                return true;
            }

            if (line.End.GetDistanceTo(arc.End) <= tolerance)
            {
                arcEndpoint = 2;
                freeEndpoint = line.Start;
                return true;
            }

            arcEndpoint = 0;
            freeEndpoint = Point2d.Origin;
            return false;
        }

        private static bool PointMatchesArcEndpoint(Point2d point, SlotArcCandidate arc, double tolerance)
        {
            return point.GetDistanceTo(arc.Start) <= tolerance || point.GetDistanceTo(arc.End) <= tolerance;
        }

        private static Point2d GetArcMidPoint(Arc arc)
        {
            var sweep = GetArcSweep(arc);
            var angle = arc.StartAngle + sweep / 2.0;
            return new Point2d(
                arc.Center.X + Math.Cos(angle) * arc.Radius,
                arc.Center.Y + Math.Sin(angle) * arc.Radius);
        }

        private static Point2d GetOutlineArcMidPoint(OutlineArc arc)
        {
            if (Math.Abs(arc.Bulge) > 1e-9)
            {
                var startAngle = Math.Atan2(arc.Start.Y - arc.Center.Y, arc.Start.X - arc.Center.X);
                var endAngle = Math.Atan2(arc.End.Y - arc.Center.Y, arc.End.X - arc.Center.X);
                var sweep = arc.Bulge >= 0.0
                    ? NormalizePositive(endAngle - startAngle)
                    : -NormalizePositive(startAngle - endAngle);
                var angle = startAngle + sweep / 2.0;
                return new Point2d(
                    arc.Center.X + Math.Cos(angle) * arc.Radius,
                    arc.Center.Y + Math.Sin(angle) * arc.Radius);
            }

            var chordX = arc.End.X - arc.Start.X;
            var chordY = arc.End.Y - arc.Start.Y;
            var chordLength = Math.Sqrt(chordX * chordX + chordY * chordY);
            if (chordLength <= 1e-9)
            {
                return new Point2d(arc.Center.X + arc.Radius, arc.Center.Y);
            }

            return new Point2d(
                arc.Center.X - chordY / chordLength * arc.Radius,
                arc.Center.Y + chordX / chordLength * arc.Radius);
        }

        private static double GetArcSweep(Arc arc)
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

            return sweep;
        }

        private OutlineFeature RecognizeLightweightOutline(Polyline polyline)
        {
            var outline = CreateEmptyOutline();
            AddEntityExtents(outline, polyline);
            var vertices = new List<Point2d>();

            for (var i = 0; i < polyline.NumberOfVertices; i++)
            {
                Point2d vertex = polyline.GetPoint2dAt(i);
                vertices.Add(vertex);
                AddVertex(outline, vertex);
            }

            for (var i = 0; i < vertices.Count; i++)
            {
                var next = (i + 1) % vertices.Count;
                var bulge = polyline.GetBulgeAt(i);
                if (Math.Abs(bulge) > _config.GeometryTolerance)
                {
                    AddArcChord(outline, vertices[i], vertices[next], ObjectId.Null, bulge);
                }
                else
                {
                    AddSegment(outline, vertices[i], vertices[next], ObjectId.Null);
                }
            }

            return outline;
        }

        private OutlineFeature RecognizePolyline2dOutline(Polyline2d polyline, Transaction tr)
        {
            var outline = CreateEmptyOutline();
            AddEntityExtents(outline, polyline);
            var vertices = new List<Point2d>();

            foreach (ObjectId vertexId in polyline)
            {
                var vertex = tr.GetObject(vertexId, OpenMode.ForRead) as Vertex2d;
                if (vertex == null)
                {
                    continue;
                }

                var point = new Point2d(vertex.Position.X, vertex.Position.Y);
                vertices.Add(point);
                AddVertex(outline, point);
            }

            for (var i = 0; i < vertices.Count; i++)
            {
                AddSegment(outline, vertices[i], vertices[(i + 1) % vertices.Count], ObjectId.Null);
            }

            if (outline.Vertices.Count == 0)
            {
                throw new InvalidOperationException("Outline Polyline has no valid vertices.");
            }

            return outline;
        }

        private static OutlineFeature CreateEmptyOutline()
        {
            return new OutlineFeature
            {
                MinX = double.MaxValue,
                MinY = double.MaxValue,
                MaxX = double.MinValue,
                MaxY = double.MinValue
            };
        }

        private static void AddEntityExtents(OutlineFeature outline, Entity entity)
        {
            try
            {
                Extents3d extents = entity.GeometricExtents;
                outline.MinX = Math.Min(outline.MinX, extents.MinPoint.X);
                outline.MaxX = Math.Max(outline.MaxX, extents.MaxPoint.X);
                outline.MinY = Math.Min(outline.MinY, extents.MinPoint.Y);
                outline.MaxY = Math.Max(outline.MaxY, extents.MaxPoint.Y);
            }
            catch
            {
                // Some proxy or invalid entities may not expose extents; ignore them.
            }
        }

        private void AddEntityKeyPoints(OutlineFeature outline, Entity entity, Transaction tr)
        {
            var line = entity as Line;
            if (line != null)
            {
                var start = new Point2d(line.StartPoint.X, line.StartPoint.Y);
                var end = new Point2d(line.EndPoint.X, line.EndPoint.Y);
                AddVertex(outline, start);
                AddVertex(outline, end);
                AddSegment(outline, start, end, entity.ObjectId);
                return;
            }

            var arc = entity as Arc;
            if (arc != null)
            {
                var start = new Point2d(arc.StartPoint.X, arc.StartPoint.Y);
                var end = new Point2d(arc.EndPoint.X, arc.EndPoint.Y);
                AddVertex(outline, start);
                AddVertex(outline, end);
                outline.Arcs.Add(new OutlineArc
                {
                    Start = start,
                    End = end,
                    Center = new Point2d(arc.Center.X, arc.Center.Y),
                    Radius = arc.Radius,
                    SourceId = entity.ObjectId,
                    Bulge = Math.Tan(GetArcSweep(arc) / 4.0)
                });
                AddArcEnvelopePoints(outline, start, end, new Point2d(arc.Center.X, arc.Center.Y), arc.Radius, arc.StartAngle, arc.EndAngle, counterClockwise: true);
                return;
            }

            var circle = entity as Circle;
            if (circle != null)
            {
                AddVertex(outline, new Point2d(circle.Center.X - circle.Radius, circle.Center.Y));
                AddVertex(outline, new Point2d(circle.Center.X + circle.Radius, circle.Center.Y));
                AddVertex(outline, new Point2d(circle.Center.X, circle.Center.Y - circle.Radius));
                AddVertex(outline, new Point2d(circle.Center.X, circle.Center.Y + circle.Radius));
                return;
            }

            var lightweight = entity as Polyline;
            if (lightweight != null)
            {
                var vertices = new List<Point2d>();
                for (var i = 0; i < lightweight.NumberOfVertices; i++)
                {
                    var vertex = lightweight.GetPoint2dAt(i);
                    vertices.Add(vertex);
                    AddVertex(outline, vertex);
                }

                for (var i = 0; i < vertices.Count - 1; i++)
                {
                    var bulge = lightweight.GetBulgeAt(i);
                    if (Math.Abs(bulge) > _config.GeometryTolerance)
                    {
                        AddArcChord(outline, vertices[i], vertices[i + 1], entity.ObjectId, bulge);
                    }
                    else
                    {
                        AddSegment(outline, vertices[i], vertices[i + 1], entity.ObjectId);
                    }
                }

                if (lightweight.Closed && vertices.Count > 1)
                {
                    var bulge = lightweight.GetBulgeAt(lightweight.NumberOfVertices - 1);
                    if (Math.Abs(bulge) > _config.GeometryTolerance)
                    {
                        AddArcChord(outline, vertices[vertices.Count - 1], vertices[0], entity.ObjectId, bulge);
                    }
                    else
                    {
                        AddSegment(outline, vertices[vertices.Count - 1], vertices[0], entity.ObjectId);
                    }
                }

                return;
            }

            var polyline2d = entity as Polyline2d;
            if (polyline2d != null)
            {
                var vertices = new List<Point2d>();
                foreach (ObjectId vertexId in polyline2d)
                {
                    var vertex = tr.GetObject(vertexId, OpenMode.ForRead) as Vertex2d;
                    if (vertex == null)
                    {
                        continue;
                    }

                    var point = new Point2d(vertex.Position.X, vertex.Position.Y);
                    vertices.Add(point);
                    AddVertex(outline, point);
                }

                for (var i = 0; i < vertices.Count - 1; i++)
                {
                    AddSegment(outline, vertices[i], vertices[i + 1], entity.ObjectId);
                }

                if (polyline2d.Closed && vertices.Count > 1)
                {
                    AddSegment(outline, vertices[vertices.Count - 1], vertices[0], entity.ObjectId);
                }
            }
        }

        private static void AddVertex(OutlineFeature outline, Point2d point)
        {
            outline.Vertices.Add(point);
            outline.MinX = Math.Min(outline.MinX, point.X);
            outline.MaxX = Math.Max(outline.MaxX, point.X);
            outline.MinY = Math.Min(outline.MinY, point.Y);
            outline.MaxY = Math.Max(outline.MaxY, point.Y);
        }

        private static void AddSegment(OutlineFeature outline, Point2d start, Point2d end, ObjectId sourceId, bool isArcChord = false)
        {
            outline.Segments.Add(new OutlineSegment
            {
                Start = start,
                End = end,
                SourceId = sourceId,
                IsArcChord = isArcChord
            });
        }

        private void AddArcChord(OutlineFeature outline, Point2d start, Point2d end, ObjectId sourceId, double bulge)
        {
            AddSegment(outline, start, end, sourceId, isArcChord: true);

            var chord = start.GetDistanceTo(end);
            if (chord <= _config.GeometryTolerance)
            {
                return;
            }

            var radius = chord * (1.0 + bulge * bulge) / (4.0 * Math.Abs(bulge));
            var midpoint = new Point2d((start.X + end.X) / 2.0, (start.Y + end.Y) / 2.0);
            var ux = (end.X - start.X) / chord;
            var uy = (end.Y - start.Y) / chord;
            var normalX = -uy;
            var normalY = ux;
            var sagitta = bulge * chord / 2.0;
            var centerDistance = radius - Math.Abs(sagitta);
            var sign = bulge >= 0.0 ? 1.0 : -1.0;
            var center = new Point2d(midpoint.X + normalX * centerDistance * sign, midpoint.Y + normalY * centerDistance * sign);

            outline.Arcs.Add(new OutlineArc
            {
                Start = start,
                End = end,
                Center = center,
                Radius = radius,
                SourceId = sourceId,
                Bulge = bulge
            });
            AddArcEnvelopePoints(outline, start, end, center, radius, bulge);
        }

        private void AddArcEnvelopePoints(OutlineFeature outline, Point2d start, Point2d end, Point2d center, double radius, double bulge)
        {
            var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
            var endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);
            AddArcEnvelopePoints(outline, start, end, center, radius, startAngle, endAngle, bulge >= 0.0);
        }

        private void AddArcEnvelopePoints(
            OutlineFeature outline,
            Point2d start,
            Point2d end,
            Point2d center,
            double radius,
            double startAngle,
            double endAngle,
            bool counterClockwise)
        {
            AddVertex(outline, start);
            AddVertex(outline, end);

            double[] cardinalAngles = { 0.0, Math.PI / 2.0, Math.PI, Math.PI * 1.5 };
            foreach (var angle in cardinalAngles)
            {
                if (!ArcContainsAngle(startAngle, endAngle, angle, counterClockwise))
                {
                    continue;
                }

                AddVertex(
                    outline,
                    new Point2d(
                        center.X + Math.Cos(angle) * radius,
                        center.Y + Math.Sin(angle) * radius));
            }
        }

        private static bool ArcContainsAngle(double startAngle, double endAngle, double angle, bool counterClockwise)
        {
            var start = NormalizeAngle(startAngle);
            var end = NormalizeAngle(endAngle);
            var candidate = NormalizeAngle(angle);
            if (counterClockwise)
            {
                var sweep = NormalizePositive(end - start);
                var offset = NormalizePositive(candidate - start);
                return offset <= sweep + 1e-9;
            }

            var clockwiseSweep = NormalizePositive(start - end);
            var clockwiseOffset = NormalizePositive(start - candidate);
            return clockwiseOffset <= clockwiseSweep + 1e-9;
        }

        private static double NormalizeAngle(double angle)
        {
            var twoPi = Math.PI * 2.0;
            angle = angle % twoPi;
            return angle < 0.0 ? angle + twoPi : angle;
        }

        private static double NormalizePositive(double angle)
        {
            var twoPi = Math.PI * 2.0;
            angle = angle % twoPi;
            return angle < 0.0 ? angle + twoPi : angle;
        }

        public void RecognizeOutlineCornerFeatures(OutlineFeature outline)
        {
            outline.Chamfers.Clear();
            outline.Fillets.Clear();
            RecognizeChamfers(outline);
            RecognizeFillets(outline);
            RecognizeInnerGrooveChamfers(outline);
            WriteCornerFeatureDiagnostics(outline);
        }

        private static void WriteCornerFeatureDiagnostics(OutlineFeature outline)
        {
            if (!DiagnosticsEnabled)
            {
                return;
            }

            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            ed.WriteMessage("\n[diagnostic] Chamfers ({0}):", outline.Chamfers.Count);
            foreach (var chamfer in outline.Chamfers)
            {
                ed.WriteMessage(
                    " start=({0:0.###},{1:0.###}) end=({2:0.###},{3:0.###}) text={4};",
                    chamfer.StartPoint.X,
                    chamfer.StartPoint.Y,
                    chamfer.EndPoint.X,
                    chamfer.EndPoint.Y,
                    chamfer.Text);
            }

            ed.WriteMessage("\n[diagnostic] Fillets ({0}):", outline.Fillets.Count);
            foreach (var fillet in outline.Fillets)
            {
                ed.WriteMessage(
                    " center=({0:0.###},{1:0.###}) radius={2:0.###} text={3};",
                    fillet.Center.X,
                    fillet.Center.Y,
                    fillet.Radius,
                    fillet.Text);
            }
        }

        public void RecognizeChamfers(OutlineFeature outline)
        {
            var maxChamferLeg = Math.Max(outline.Width, outline.Height) * 0.25;
            foreach (var segment in outline.Segments)
            {
                if (segment.IsArcChord || segment.IsHorizontal(_config.GeometryTolerance) || segment.IsVertical(_config.GeometryTolerance))
                {
                    continue;
                }

                var dx = Math.Abs(segment.End.X - segment.Start.X);
                var dy = Math.Abs(segment.End.Y - segment.Start.Y);
                var chamferLeg = GetChamferDimensionLeg(outline, segment, dx, dy);
                if (segment.Length <= _config.GeometryTolerance || chamferLeg > maxChamferLeg)
                {
                    continue;
                }

                if (Math.Abs(dx - dy) > Math.Max(_config.GeometryTolerance, Math.Max(dx, dy) * 0.05))
                {
                    continue;
                }

                OutlineSegment firstNeighbor;
                OutlineSegment secondNeighbor;
                if (!TryGetChamferAxisNeighbors(outline, segment, out firstNeighbor, out secondNeighbor))
                {
                    continue;
                }

                var adjacentMainLength = Math.Max(firstNeighbor.Length, secondNeighbor.Length);
                if (adjacentMainLength <= _config.GeometryTolerance
                    || chamferLeg > adjacentMainLength * 0.5 + _config.GeometryTolerance)
                {
                    if (!IsLocalStepChamferCallout(segment, firstNeighbor, secondNeighbor, outline))
                    {
                        continue;
                    }

                    outline.Chamfers.Add(CreateChamferFeature(segment, dx, dy, chamferLeg));
                    continue;
                }

                outline.Chamfers.Add(CreateChamferFeature(segment, dx, dy, chamferLeg));
            }
        }

        private void RecognizeInnerGrooveChamfers(OutlineFeature outline)
        {
            var maxChamferLeg = Math.Max(outline.Width, outline.Height) * 0.25;
            foreach (var segment in outline.Segments)
            {
                if (segment.IsArcChord
                    || segment.IsHorizontal(_config.GeometryTolerance)
                    || segment.IsVertical(_config.GeometryTolerance)
                    || !IsFortyFiveDegreeSegment(segment)
                    || IsKnownChamferSegment(segment, outline)
                    || !IsInnerGrooveChamferSegment(segment, outline))
                {
                    continue;
                }

                var dx = Math.Abs(segment.End.X - segment.Start.X);
                var dy = Math.Abs(segment.End.Y - segment.Start.Y);
                var chamferLeg = GetChamferDimensionLeg(outline, segment, dx, dy);
                if (segment.Length <= _config.GeometryTolerance || chamferLeg > maxChamferLeg)
                {
                    continue;
                }

                outline.Chamfers.Add(CreateChamferFeature(segment, dx, dy, chamferLeg));
            }
        }

        private ChamferFeature CreateChamferFeature(
            OutlineSegment segment,
            double dx,
            double dy,
            double chamferLeg)
        {
            return new ChamferFeature
            {
                StartPoint = segment.Start,
                EndPoint = segment.End,
                Length = segment.Length,
                DeltaX = dx,
                DeltaY = dy,
                Value = chamferLeg,
                Text = "C" + _config.FormatNumber(chamferLeg),
                SourceSegment = segment,
                Confidence = 0.9
            };
        }

        private bool IsLocalStepChamferCallout(
            OutlineSegment chamfer,
            OutlineSegment firstNeighbor,
            OutlineSegment secondNeighbor,
            OutlineFeature outline)
        {
            return IsAtOuterLocalStep(chamfer.Start, firstNeighbor, outline)
                || IsAtOuterLocalStep(chamfer.Start, secondNeighbor, outline)
                || IsAtOuterLocalStep(chamfer.End, firstNeighbor, outline)
                || IsAtOuterLocalStep(chamfer.End, secondNeighbor, outline);
        }

        private bool IsAtOuterLocalStep(Point2d point, OutlineSegment neighbor, OutlineFeature outline)
        {
            if (neighbor == null)
            {
                return false;
            }

            var tolerance = Math.Max(_config.GeometryTolerance, 0.2);
            var touchesEnvelope =
                Math.Abs(neighbor.MinX - outline.MinX) <= tolerance
                || Math.Abs(neighbor.MaxX - outline.MaxX) <= tolerance
                || Math.Abs(neighbor.MinY - outline.MinY) <= tolerance
                || Math.Abs(neighbor.MaxY - outline.MaxY) <= tolerance
                || Math.Abs(point.X - outline.MinX) <= tolerance
                || Math.Abs(point.X - outline.MaxX) <= tolerance
                || Math.Abs(point.Y - outline.MinY) <= tolerance
                || Math.Abs(point.Y - outline.MaxY) <= tolerance;
            if (!touchesEnvelope)
            {
                return false;
            }

            return neighbor.Length <= Math.Max(outline.Width, outline.Height) * 0.35 + tolerance;
        }

        private double GetChamferDimensionLeg(OutlineFeature outline, OutlineSegment chamfer, double dx, double dy)
        {
            var rawLeg = Math.Max(dx, dy);
            var points = new List<Point2d> { chamfer.Start, chamfer.End };

            foreach (var segment in outline.Segments)
            {
                if (ReferenceEquals(segment, chamfer) || segment.IsArcChord)
                {
                    continue;
                }

                if (segment.IsHorizontal(_config.GeometryTolerance) || segment.IsVertical(_config.GeometryTolerance))
                {
                    continue;
                }

                var otherDx = Math.Abs(segment.End.X - segment.Start.X);
                var otherDy = Math.Abs(segment.End.Y - segment.Start.Y);
                if (Math.Abs(otherDx - otherDy) > Math.Max(_config.GeometryTolerance, Math.Max(otherDx, otherDy) * 0.05))
                {
                    continue;
                }

                if (IsChamferExtensionSegment(chamfer, segment, rawLeg))
                {
                    points.Add(segment.Start);
                    points.Add(segment.End);
                }
            }

            var projectionX = points.Max(p => p.X) - points.Min(p => p.X);
            var projectionY = points.Max(p => p.Y) - points.Min(p => p.Y);
            return Math.Max(rawLeg, Math.Max(projectionX, projectionY));
        }

        private bool IsChamferExtensionSegment(OutlineSegment chamfer, OutlineSegment candidate, double chamferLeg)
        {
            var direction = chamfer.End - chamfer.Start;
            if (direction.Length <= _config.GeometryTolerance)
            {
                return false;
            }

            direction = direction.GetNormal();
            var candidateDirection = candidate.End - candidate.Start;
            if (candidateDirection.Length <= _config.GeometryTolerance)
            {
                return false;
            }

            candidateDirection = candidateDirection.GetNormal();
            var parallel = Math.Abs(Math.Abs(direction.DotProduct(candidateDirection)) - 1.0) <= 0.02;
            if (!parallel)
            {
                return false;
            }

            var maxPerpendicularDistance = Math.Max(_config.GeometryTolerance, 0.2);
            if (DistanceFromPointToLine(candidate.Start, chamfer.Start, direction) > maxPerpendicularDistance
                || DistanceFromPointToLine(candidate.End, chamfer.Start, direction) > maxPerpendicularDistance)
            {
                return false;
            }

            var chamferA = 0.0;
            var chamferB = (chamfer.End - chamfer.Start).DotProduct(direction);
            if (chamferB < chamferA)
            {
                var temp = chamferA;
                chamferA = chamferB;
                chamferB = temp;
            }

            var candidateA = (candidate.Start - chamfer.Start).DotProduct(direction);
            var candidateB = (candidate.End - chamfer.Start).DotProduct(direction);
            if (candidateB < candidateA)
            {
                var temp = candidateA;
                candidateA = candidateB;
                candidateB = temp;
            }

            var gap = Math.Max(candidateA - chamferB, chamferA - candidateB);
            var maxGap = Math.Max(chamferLeg * 0.75, Math.Max(_config.GeometryTolerance, 2.0));
            return gap <= maxGap + _config.GeometryTolerance;
        }

        private bool IsInnerGrooveChamferSegment(OutlineSegment chamfer, OutlineFeature outline)
        {
            return IsHorizontalInnerGrooveChamferSegment(chamfer, outline, isTopSide: true)
                || IsHorizontalInnerGrooveChamferSegment(chamfer, outline, isTopSide: false)
                || IsVerticalInnerGrooveChamferSegment(chamfer, outline, leftSide: true)
                || IsVerticalInnerGrooveChamferSegment(chamfer, outline, leftSide: false);
        }

        private bool IsHorizontalInnerGrooveChamferSegment(
            OutlineSegment chamfer,
            OutlineFeature outline,
            bool isTopSide)
        {
            var tolerance = _config.GeometryTolerance;
            var chamferTopY = Math.Max(chamfer.Start.Y, chamfer.End.Y);
            var chamferBottomY = Math.Min(chamfer.Start.Y, chamfer.End.Y);
            foreach (var horizontal in outline.Segments.Where(s => s.IsHorizontal(tolerance) && !s.IsArcChord))
            {
                if (Math.Abs(horizontal.MinY - outline.MaxY) <= tolerance
                    || Math.Abs(horizontal.MinY - outline.MinY) <= tolerance
                    || (isTopSide && horizontal.MinY >= chamferTopY - tolerance)
                    || (!isTopSide && horizontal.MinY <= chamferBottomY + tolerance))
                {
                    continue;
                }

                if (SegmentTouchesPoint(horizontal, chamfer.Start)
                    && HorizontalOtherEndConnectsInnerGroove(horizontal, chamfer.Start, chamfer, outline))
                {
                    return true;
                }

                if (SegmentTouchesPoint(horizontal, chamfer.End)
                    && HorizontalOtherEndConnectsInnerGroove(horizontal, chamfer.End, chamfer, outline))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsVerticalInnerGrooveChamferSegment(
            OutlineSegment chamfer,
            OutlineFeature outline,
            bool leftSide)
        {
            var tolerance = _config.GeometryTolerance;
            var chamferLeftX = Math.Min(chamfer.Start.X, chamfer.End.X);
            var chamferRightX = Math.Max(chamfer.Start.X, chamfer.End.X);
            foreach (var vertical in outline.Segments.Where(s => s.IsVertical(tolerance) && !s.IsArcChord))
            {
                if (Math.Abs(vertical.MinX - outline.MinX) <= tolerance
                    || Math.Abs(vertical.MinX - outline.MaxX) <= tolerance
                    || (leftSide && vertical.MinX <= chamferLeftX + tolerance)
                    || (!leftSide && vertical.MinX >= chamferRightX - tolerance))
                {
                    continue;
                }

                if (SegmentTouchesPoint(vertical, chamfer.Start)
                    && IsInternalSideGrooveVertical(vertical, chamfer, leftSide, outline)
                    && VerticalOtherEndConnectsInnerGroove(vertical, chamfer.Start, chamfer, outline))
                {
                    return true;
                }

                if (SegmentTouchesPoint(vertical, chamfer.End)
                    && IsInternalSideGrooveVertical(vertical, chamfer, leftSide, outline)
                    && VerticalOtherEndConnectsInnerGroove(vertical, chamfer.End, chamfer, outline))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsInternalSideGrooveVertical(
            OutlineSegment vertical,
            OutlineSegment chamfer,
            bool leftSide,
            OutlineFeature outline)
        {
            var tolerance = _config.GeometryTolerance;
            if (!vertical.IsVertical(tolerance)
                || Math.Abs(vertical.MinX - outline.MinX) <= tolerance
                || Math.Abs(vertical.MinX - outline.MaxX) <= tolerance)
            {
                return false;
            }

            var chamferLeftX = Math.Min(chamfer.Start.X, chamfer.End.X);
            var chamferRightX = Math.Max(chamfer.Start.X, chamfer.End.X);
            return leftSide
                ? vertical.MinX > chamferLeftX + tolerance
                : vertical.MinX < chamferRightX - tolerance;
        }

        private bool HorizontalOtherEndConnectsInnerGroove(
            OutlineSegment horizontal,
            Point2d sharedPoint,
            OutlineSegment currentChamfer,
            OutlineFeature outline)
        {
            var otherEnd = PointsEqual(horizontal.Start, sharedPoint) ? horizontal.End : horizontal.Start;
            return outline.Segments.Any(segment =>
                    !ReferenceEquals(segment, currentChamfer)
                    && !segment.IsHorizontal(_config.GeometryTolerance)
                    && !segment.IsVertical(_config.GeometryTolerance)
                    && IsFortyFiveDegreeSegment(segment)
                    && (PointsEqual(segment.Start, otherEnd) || PointsEqual(segment.End, otherEnd)))
                || outline.Chamfers.Any(chamfer =>
                    (PointsEqual(chamfer.StartPoint, otherEnd) || PointsEqual(chamfer.EndPoint, otherEnd)))
                || outline.Fillets.Any(fillet =>
                    PointsEqual(fillet.StartPoint, otherEnd) || PointsEqual(fillet.EndPoint, otherEnd));
        }

        private bool VerticalOtherEndConnectsInnerGroove(
            OutlineSegment vertical,
            Point2d sharedPoint,
            OutlineSegment currentChamfer,
            OutlineFeature outline)
        {
            var otherEnd = PointsEqual(vertical.Start, sharedPoint) ? vertical.End : vertical.Start;
            return outline.Segments.Any(segment =>
                    !ReferenceEquals(segment, currentChamfer)
                    && !segment.IsHorizontal(_config.GeometryTolerance)
                    && !segment.IsVertical(_config.GeometryTolerance)
                    && IsFortyFiveDegreeSegment(segment)
                    && (PointsEqual(segment.Start, otherEnd) || PointsEqual(segment.End, otherEnd)))
                || outline.Chamfers.Any(chamfer =>
                    PointsEqual(chamfer.StartPoint, otherEnd) || PointsEqual(chamfer.EndPoint, otherEnd))
                || outline.Fillets.Any(fillet =>
                    PointsEqual(fillet.StartPoint, otherEnd) || PointsEqual(fillet.EndPoint, otherEnd));
        }

        private bool IsKnownChamferSegment(OutlineSegment segment, OutlineFeature outline)
        {
            return outline.Chamfers.Any(chamfer =>
                (PointsEqual(chamfer.StartPoint, segment.Start) && PointsEqual(chamfer.EndPoint, segment.End))
                || (PointsEqual(chamfer.StartPoint, segment.End) && PointsEqual(chamfer.EndPoint, segment.Start)));
        }

        private bool IsFortyFiveDegreeSegment(OutlineSegment segment)
        {
            var dx = Math.Abs(segment.End.X - segment.Start.X);
            var dy = Math.Abs(segment.End.Y - segment.Start.Y);
            if (dx <= _config.GeometryTolerance || dy <= _config.GeometryTolerance)
            {
                return false;
            }

            return Math.Abs(dx - dy) <= Math.Max(_config.GeometryTolerance, Math.Max(dx, dy) * 0.05);
        }

        private bool SegmentTouchesPoint(OutlineSegment segment, Point2d point)
        {
            return PointsEqual(segment.Start, point) || PointsEqual(segment.End, point);
        }

        private static double DistanceFromPointToLine(Point2d point, Point2d linePoint, Vector2d direction)
        {
            var vector = point - linePoint;
            return Math.Abs(vector.X * direction.Y - vector.Y * direction.X);
        }

        public void RecognizeFillets(OutlineFeature outline)
        {
            var maxRadius = Math.Min(outline.Width, outline.Height) * 0.2;
            foreach (var arc in outline.Arcs)
            {
                if (arc.Radius <= _config.GeometryTolerance || arc.Radius > maxRadius)
                {
                    continue;
                }

                if (!HasConnectedOutlineSegment(outline, arc.Start) || !HasConnectedOutlineSegment(outline, arc.End))
                {
                    continue;
                }

                outline.Fillets.Add(new FilletFeature
                {
                    Center = arc.Center,
                    Radius = arc.Radius,
                    StartPoint = arc.Start,
                    EndPoint = arc.End,
                    Text = "R" + _config.FormatNumber(arc.Radius),
                    SourceArc = arc,
                    Confidence = 0.9
                });
            }
        }

        private bool HasAxisNeighborAtPoint(OutlineFeature outline, OutlineSegment source, Point2d point, bool horizontal)
        {
            return outline.Segments.Any(s =>
                !ReferenceEquals(s, source)
                && !s.IsArcChord
                && (PointsEqual(s.Start, point) || PointsEqual(s.End, point))
                && (horizontal ? s.IsHorizontal(_config.GeometryTolerance) : s.IsVertical(_config.GeometryTolerance)));
        }

        private bool TryGetChamferAxisNeighbors(
            OutlineFeature outline,
            OutlineSegment chamfer,
            out OutlineSegment firstNeighbor,
            out OutlineSegment secondNeighbor)
        {
            firstNeighbor = null;
            secondNeighbor = null;

            var startHorizontal = GetAxisNeighborAtPoint(outline, chamfer, chamfer.Start, horizontal: true);
            var startVertical = GetAxisNeighborAtPoint(outline, chamfer, chamfer.Start, horizontal: false);
            var endHorizontal = GetAxisNeighborAtPoint(outline, chamfer, chamfer.End, horizontal: true);
            var endVertical = GetAxisNeighborAtPoint(outline, chamfer, chamfer.End, horizontal: false);

            if (startHorizontal != null && endVertical != null)
            {
                firstNeighbor = startHorizontal;
                secondNeighbor = endVertical;
                return true;
            }

            if (startVertical != null && endHorizontal != null)
            {
                firstNeighbor = startVertical;
                secondNeighbor = endHorizontal;
                return true;
            }

            return false;
        }

        private OutlineSegment GetAxisNeighborAtPoint(OutlineFeature outline, OutlineSegment source, Point2d point, bool horizontal)
        {
            return outline.Segments
                .Where(s =>
                    !ReferenceEquals(s, source)
                    && !s.IsArcChord
                    && (PointsEqual(s.Start, point) || PointsEqual(s.End, point))
                    && (horizontal ? s.IsHorizontal(_config.GeometryTolerance) : s.IsVertical(_config.GeometryTolerance)))
                .OrderByDescending(s => s.Length)
                .FirstOrDefault();
        }

        private bool HasConnectedOutlineSegment(OutlineFeature outline, Point2d point)
        {
            return outline.Segments.Any(s =>
                !s.IsArcChord
                && (PointsEqual(s.Start, point) || PointsEqual(s.End, point)));
        }

        private bool PointsEqual(Point2d a, Point2d b)
        {
            return a.GetDistanceTo(b) <= Math.Max(_config.GeometryTolerance, 0.2);
        }

        private static double GetAverageY(IEnumerable<HoleFeature> row)
        {
            return row.Average(h => h.Center.Y);
        }

        private static double GetAverageDiameter(IEnumerable<HoleFeature> group)
        {
            return group.Average(h => h.Diameter);
        }

        private IList<Point3d> CollectPinMarkers(IEnumerable<ObjectId> contextIds, IEnumerable<ObjectId> circleIds, Transaction tr)
        {
            var circleSet = new HashSet<ObjectId>(circleIds);
            var markers = new List<Point3d>();
            if (contextIds == null)
            {
                return markers;
            }

            int blockRefCount = 0;
            int pinMarkerCount = 0;
            var nonMatchingBlockNames = new List<string>();

            foreach (ObjectId id in contextIds)
            {
                if (circleSet.Contains(id))
                {
                    continue;
                }

                var entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
                var blockReference = entity as BlockReference;
                if (blockReference == null)
                {
                    continue;
                }

                blockRefCount++;
                string blockName = GetBlockName(blockReference, tr);
                if (IsPinMarkerBlock(blockReference, tr))
                {
                    pinMarkerCount++;
                    markers.Add(blockReference.Position);
                }
                else if (!string.IsNullOrEmpty(blockName))
                {
                    if (!nonMatchingBlockNames.Contains(blockName))
                    {
                        nonMatchingBlockNames.Add(blockName);
                    }
                }
            }

            var doc = Autodesk.AutoCAD.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc != null && DiagnosticsEnabled)
            {
                var ed = doc.Editor;
                ed.WriteMessage("\n[璇婃柇] 妗嗛€変腑 BlockReference 鏁伴噺={0}, 閿€瀛旀爣璁版暟閲?{1}", blockRefCount, pinMarkerCount);
                if (nonMatchingBlockNames.Count > 0 && pinMarkerCount == 0)
                {
                    ed.WriteMessage("\n[璇婃柇] 鏈尮閰嶇殑鍧楀悕: {0}", string.Join(", ", nonMatchingBlockNames));
                }
            }

            return markers;
        }

        private bool HasPinMarker(Point3d center, double radius, IEnumerable<Point3d> markerPoints)
        {
            foreach (var markerPoint in markerPoints)
            {
                if (center.DistanceTo(markerPoint) <= radius + _config.GeometryTolerance)
                {
                    return true;
                }
            }

            // Prefer concentric matching. Keep the fallback radius very small so
            // nearby non-pin circles are not misclassified as pin holes.
            var searchRadius = Math.Max(radius * 0.0, 1.0);
            foreach (var markerPoint in markerPoints)
            {
                if (center.DistanceTo(markerPoint) <= searchRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsThreadArc(Arc arc)
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
            return sweep >= minimum - _config.GeometryTolerance;
        }

        private bool IsConfirmedThreadArc(Arc arc, IEnumerable<ObjectId> sourceIds, Transaction tr)
        {
            if (!IsDrawingLayer(arc.Layer) || !IsThreadArc(arc))
            {
                return false;
            }

            // A real thread hole must have a concentric circle (minor diameter) in the selection.
            foreach (ObjectId id in sourceIds)
            {
                var circle = tr.GetObject(id, OpenMode.ForRead) as Circle;
                if (circle == null)
                {
                    continue;
                }

                if (arc.Center.DistanceTo(circle.Center) > _config.GeometryTolerance)
                {
                    continue;
                }

                // Concentric circle found 鈥?must be smaller than the arc (minor < major).
                if (circle.Radius < arc.Radius - _config.GeometryTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private IList<ThreadArcInfo> CollectThreadArcInfos(IEnumerable<ObjectId> sourceIds, Transaction tr)
        {
            var infos = new List<ThreadArcInfo>();
            if (sourceIds == null)
            {
                return infos;
            }

            foreach (ObjectId id in sourceIds)
            {
                var arc = tr.GetObject(id, OpenMode.ForRead) as Arc;
                if (arc != null && IsConfirmedThreadArc(arc, sourceIds, tr))
                {
                    infos.Add(new ThreadArcInfo
                    {
                        Center = arc.Center,
                        Radius = arc.Radius
                    });
                }
            }

            return infos;
        }

        private bool IsSuppressedByThreadArc(Circle circle, IEnumerable<ThreadArcInfo> threadArcs)
        {
            foreach (var threadArc in threadArcs)
            {
                if (circle.Center.DistanceTo(threadArc.Center) <= _config.GeometryTolerance
                    && circle.Radius <= threadArc.Radius + _config.GeometryTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private IList<ThreadMinorCircleInfo> CollectThreadMinorCircles(IEnumerable<ObjectId> sourceIds, Transaction tr, IEnumerable<ThreadArcInfo> threadArcs)
        {
            var infos = new List<ThreadMinorCircleInfo>();
            if (sourceIds == null)
            {
                return infos;
            }

            foreach (ObjectId id in sourceIds)
            {
                var circle = tr.GetObject(id, OpenMode.ForRead) as Circle;
                if (circle == null || !HasThreadArcAtCenter(circle.Center, threadArcs))
                {
                    continue;
                }

                var callout = _config.GetThreadCalloutForMinorDiameter(circle.Radius * 2.0);
                if (!string.IsNullOrEmpty(callout))
                {
                    infos.Add(new ThreadMinorCircleInfo
                    {
                        Center = circle.Center,
                        Diameter = circle.Radius * 2.0,
                        Callout = callout
                    });
                }
            }

            return infos;
        }

        private bool HasThreadArcAtCenter(Point3d center, IEnumerable<ThreadArcInfo> threadArcs)
        {
            foreach (var threadArc in threadArcs)
            {
                if (center.DistanceTo(threadArc.Center) <= _config.GeometryTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private string FindThreadMinorCallout(Point3d center, IEnumerable<ThreadMinorCircleInfo> minorCircles)
        {
            foreach (var minorCircle in minorCircles)
            {
                if (center.DistanceTo(minorCircle.Center) <= _config.GeometryTolerance)
                {
                    return minorCircle.Callout;
                }
            }

            return string.Empty;
        }

        private static bool IsDrawingLayer(string layerName)
        {
            return string.Equals((layerName ?? string.Empty).Trim(), "DRAWING", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPinMarkerBlock(BlockReference blockReference, Transaction tr)
        {
            string blockName = GetBlockName(blockReference, tr);
            return string.Equals(blockName, "CadAider_\u9500\u5B54\u6807\u8BB0", StringComparison.OrdinalIgnoreCase)
                || string.Equals(blockName, "CadAider_\u9500\u5B54\u6807\u8BB0\u80CC\u9762", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetBlockName(BlockReference blockReference, Transaction tr)
        {
            ObjectId blockTableRecordId = blockReference.IsDynamicBlock
                ? blockReference.DynamicBlockTableRecord
                : blockReference.BlockTableRecord;

            var record = tr.GetObject(blockTableRecordId, OpenMode.ForRead) as BlockTableRecord;
            if (record != null)
            {
                return record.Name;
            }

            return string.Empty;
        }

        private sealed class SlotArcCandidate
        {
            public ObjectId Id { get; set; }
            public Point2d Center { get; set; }
            public Point2d Start { get; set; }
            public Point2d End { get; set; }
            public Point2d Mid { get; set; }
            public double Radius { get; set; }
        }

        private sealed class SlotLineCandidate
        {
            public ObjectId Id { get; set; }
            public Point2d Start { get; set; }
            public Point2d End { get; set; }
            public double Length
            {
                get { return Start.GetDistanceTo(End); }
            }
        }

        private sealed class ThreadArcInfo
        {
            public Point3d Center { get; set; }
            public double Radius { get; set; }
        }

        private sealed class ThreadMinorCircleInfo
        {
            public Point3d Center { get; set; }
            public double Diameter { get; set; }
            public string Callout { get; set; }
        }
    }
}
