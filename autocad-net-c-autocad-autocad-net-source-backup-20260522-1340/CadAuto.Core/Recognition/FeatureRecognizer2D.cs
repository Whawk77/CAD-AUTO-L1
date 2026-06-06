using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Recognition
{
    public sealed class FeatureRecognizer2D
    {
        private readonly DimensionRuleConfig _config;

        public FeatureRecognizer2D(DimensionRuleConfig config)
        {
            _config = config;
        }

        public void RecognizeOutlineCornerFeatures(OutlineFeature2D outline)
        {
            if (outline == null)
            {
                throw new ArgumentNullException("outline");
            }

            outline.Chamfers.Clear();
            outline.Fillets.Clear();
            RecognizeChamfers(outline);
            RecognizeFillets(outline);

            // TODO: Port the inner-groove chamfer branch after the adapter can preserve
            // the same segment connectivity metadata currently implicit in AutoCAD entities.
        }

        public IList<SlotFeature2D> RecognizeSlotFeatures(DrawingGeometry geometry)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException("geometry");
            }

            return RecognizeSlotFeatures(geometry.Arcs, geometry.Segments);
        }

        public IList<SlotFeature2D> RecognizeOutlineSlotFeatures(OutlineFeature2D outline)
        {
            if (outline == null)
            {
                throw new ArgumentNullException("outline");
            }

            return RecognizeSlotFeatures(outline.Arcs, outline.Segments);
        }

        public IList<SlotFeature2D> RecognizeSlotFeatures(IEnumerable<Arc2D> arcs, IEnumerable<Segment2D> segments)
        {
            var arcCandidates = (arcs ?? new Arc2D[0])
                .Where(a => a != null && IsHalfArc(a))
                .Select(ToSlotArcCandidate)
                .ToList();
            var lineCandidates = (segments ?? new Segment2D[0])
                .Where(s => s != null && !s.IsArcChord)
                .Select(ToSlotLineCandidate)
                .ToList();

            var slots = new List<SlotFeature2D>();
            var usedArcs = new HashSet<string>();
            var usedLines = new HashSet<string>();
            var slotIndex = 1;

            for (int i = 0; i < arcCandidates.Count; i++)
            {
                if (usedArcs.Contains(arcCandidates[i].Id))
                {
                    continue;
                }

                for (int j = i + 1; j < arcCandidates.Count; j++)
                {
                    if (usedArcs.Contains(arcCandidates[j].Id))
                    {
                        continue;
                    }

                    bool horizontal;
                    if (!CanPairSlotArcs(arcCandidates[i], arcCandidates[j], out horizontal))
                    {
                        continue;
                    }

                    var connectingLines = lineCandidates
                        .Where(l => !usedLines.Contains(l.Id)
                            && IsLineParallelToSlot(l, horizontal)
                            && ConnectsSlotArcs(l, arcCandidates[i], arcCandidates[j]))
                        .ToList();
                    if (connectingLines.Count != 2)
                    {
                        continue;
                    }

                    slots.Add(new SlotFeature2D
                    {
                        GroupId = CreateSlotGroupId(slotIndex),
                        FirstCenter = arcCandidates[i].Center,
                        SecondCenter = arcCandidates[j].Center,
                        Radius = (arcCandidates[i].Radius + arcCandidates[j].Radius) / 2.0,
                        CenterDistance = arcCandidates[i].Center.DistanceTo(arcCandidates[j].Center),
                        IsVertical = !horizontal
                    });

                    usedArcs.Add(arcCandidates[i].Id);
                    usedArcs.Add(arcCandidates[j].Id);
                    usedLines.Add(connectingLines[0].Id);
                    usedLines.Add(connectingLines[1].Id);
                    slotIndex++;
                    break;
                }
            }

            RecognizeSingleArcSlots(arcCandidates, lineCandidates, usedArcs, usedLines, slots, ref slotIndex);
            return slots;
        }

        public OutlineFeature2D RecognizeOutlineFromClosedPath(IEnumerable<Point2D> vertices)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException("vertices");
            }

            var points = vertices.ToList();
            if (points.Count < 3)
            {
                throw new InvalidOperationException("Outline needs at least three vertices.");
            }

            if (PointsEqual(points[0], points[points.Count - 1]))
            {
                points.RemoveAt(points.Count - 1);
            }

            var outline = new OutlineFeature2D
            {
                MinX = points.Min(p => p.X),
                MaxX = points.Max(p => p.X),
                MinY = points.Min(p => p.Y),
                MaxY = points.Max(p => p.Y)
            };

            foreach (var point in points)
            {
                outline.Vertices.Add(point);
            }

            for (int i = 0; i < points.Count; i++)
            {
                var next = (i + 1) % points.Count;
                outline.Segments.Add(new Segment2D(points[i], points[next])
                {
                    SourceKey = "path:" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });
            }

            RecognizeOutlineCornerFeatures(outline);
            return outline;
        }

        public OutlineFeature2D RecognizeOutlineFromSegments(IEnumerable<Segment2D> segments, IEnumerable<Arc2D> arcs)
        {
            if (segments == null)
            {
                throw new ArgumentNullException("segments");
            }

            var segmentList = segments.Where(s => s != null).ToList();
            if (segmentList.Count == 0)
            {
                throw new InvalidOperationException("Outline needs at least one segment.");
            }

            var points = segmentList.SelectMany(s => new[] { s.Start, s.End }).ToList();
            if (arcs != null)
            {
                points.AddRange(arcs.Where(a => a != null).SelectMany(a => new[] { a.Start, a.End, a.Center }));
            }

            var outline = new OutlineFeature2D
            {
                MinX = points.Min(p => p.X),
                MaxX = points.Max(p => p.X),
                MinY = points.Min(p => p.Y),
                MaxY = points.Max(p => p.Y)
            };

            foreach (var segment in segmentList)
            {
                outline.Segments.Add(segment);
                AddUniqueVertex(outline, segment.Start);
                AddUniqueVertex(outline, segment.End);
            }

            if (arcs != null)
            {
                foreach (var arc in arcs.Where(a => a != null))
                {
                    outline.Arcs.Add(arc);
                }
            }

            RecognizeOutlineCornerFeatures(outline);
            return outline;
        }

        public void RecognizeChamfers(OutlineFeature2D outline)
        {
            var maxChamferLeg = Math.Max(outline.Width, outline.Height) * 0.25;

            foreach (var segment in outline.Segments.Where(s => !s.IsArcChord))
            {
                if (segment.IsHorizontal(_config.GeometryTolerance) || segment.IsVertical(_config.GeometryTolerance))
                {
                    continue;
                }

                var dx = Math.Abs(segment.Start.X - segment.End.X);
                var dy = Math.Abs(segment.Start.Y - segment.End.Y);
                var chamferLeg = Math.Max(dx, dy);
                if (segment.Length <= _config.GeometryTolerance || chamferLeg > maxChamferLeg)
                {
                    continue;
                }

                if (Math.Abs(dx - dy) > _config.GeometryTolerance)
                {
                    continue;
                }

                Segment2D firstNeighbor;
                Segment2D secondNeighbor;
                if (!TryGetChamferAxisNeighbors(outline, segment, out firstNeighbor, out secondNeighbor))
                {
                    continue;
                }

                outline.Chamfers.Add(CreateChamferFeature(segment, dx, dy, chamferLeg));
            }
        }

        public void RecognizeFillets(OutlineFeature2D outline)
        {
            var maxRadius = Math.Max(outline.Width, outline.Height) * 0.25;

            foreach (var arc in outline.Arcs)
            {
                if (arc.Radius <= _config.GeometryTolerance || arc.Radius > maxRadius)
                {
                    continue;
                }

                outline.Fillets.Add(new FilletFeature2D
                {
                    Center = arc.Center,
                    Radius = arc.Radius,
                    StartPoint = arc.Start,
                    EndPoint = arc.End,
                    SourceArc = arc,
                    Text = "R" + _config.FormatNumber(arc.Radius),
                    Confidence = 0.8
                });
            }
        }

        private void RecognizeSingleArcSlots(
            IList<SlotArcCandidate> arcs,
            IList<SlotLineCandidate> lines,
            ISet<string> usedArcs,
            ISet<string> usedLines,
            IList<SlotFeature2D> slots,
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

                        slots.Add(new SlotFeature2D
                        {
                            GroupId = CreateSlotGroupId(slotIndex),
                            FirstCenter = arc.Center,
                            SecondCenter = arc.Center,
                            Radius = arc.Radius,
                            CenterDistance = 0.0,
                            IsSingleArcSlot = true,
                            IsVertical = !horizontal,
                            ArcLeaderTarget = arc.Mid
                        });

                        usedArcs.Add(arc.Id);
                        usedLines.Add(candidates[i].Id);
                        usedLines.Add(candidates[j].Id);
                        slotIndex++;
                        i = candidates.Count;
                        break;
                    }
                }
            }
        }

        private SlotArcCandidate ToSlotArcCandidate(Arc2D arc)
        {
            return new SlotArcCandidate
            {
                Id = arc.SourceKey ?? string.Empty,
                Center = arc.Center,
                Start = arc.Start,
                End = arc.End,
                Mid = GetArcMidPoint(arc),
                Radius = arc.Radius
            };
        }

        private SlotLineCandidate ToSlotLineCandidate(Segment2D segment)
        {
            return new SlotLineCandidate
            {
                Id = segment.SourceKey ?? string.Empty,
                Start = segment.Start,
                End = segment.End
            };
        }

        private bool IsHalfArc(Arc2D arc)
        {
            var chord = arc.Start.DistanceTo(arc.End);
            var diameter = arc.Radius * 2.0;
            var tolerance = Math.Max(_config.GeometryTolerance, Math.Max(arc.Radius, 1.0) * 0.05);
            return arc.Radius > _config.GeometryTolerance
                && Math.Abs(chord - diameter) <= tolerance;
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

            var centerDistance = first.Center.DistanceTo(second.Center);
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
            Point2D firstFreeEndpoint;
            Point2D secondFreeEndpoint;
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
            Point2D freeEndpoint;
            return TryGetSingleArcSlotLineEndpoints(line, arc, out endpoint, out freeEndpoint);
        }

        private bool TryGetSingleArcSlotLineEndpoints(
            SlotLineCandidate line,
            SlotArcCandidate arc,
            out int arcEndpoint,
            out Point2D freeEndpoint)
        {
            var tolerance = Math.Max(_config.GeometryTolerance, arc.Radius * 0.08);
            if (line.Start.DistanceTo(arc.Start) <= tolerance)
            {
                arcEndpoint = 1;
                freeEndpoint = line.End;
                return true;
            }

            if (line.End.DistanceTo(arc.Start) <= tolerance)
            {
                arcEndpoint = 1;
                freeEndpoint = line.Start;
                return true;
            }

            if (line.Start.DistanceTo(arc.End) <= tolerance)
            {
                arcEndpoint = 2;
                freeEndpoint = line.End;
                return true;
            }

            if (line.End.DistanceTo(arc.End) <= tolerance)
            {
                arcEndpoint = 2;
                freeEndpoint = line.Start;
                return true;
            }

            arcEndpoint = 0;
            freeEndpoint = new Point2D();
            return false;
        }

        private static bool PointMatchesArcEndpoint(Point2D point, SlotArcCandidate arc, double tolerance)
        {
            return point.DistanceTo(arc.Start) <= tolerance || point.DistanceTo(arc.End) <= tolerance;
        }

        private Point2D GetArcMidPoint(Arc2D arc)
        {
            if (Math.Abs(arc.Bulge) > 1e-9)
            {
                var startAngle = Math.Atan2(arc.Start.Y - arc.Center.Y, arc.Start.X - arc.Center.X);
                var endAngle = Math.Atan2(arc.End.Y - arc.Center.Y, arc.End.X - arc.Center.X);
                var sweep = endAngle - startAngle;
                if (arc.Bulge > 0.0 && sweep < 0.0)
                {
                    sweep += Math.PI * 2.0;
                }
                else if (arc.Bulge < 0.0 && sweep > 0.0)
                {
                    sweep -= Math.PI * 2.0;
                }

                var angle = startAngle + sweep / 2.0;
                return new Point2D(
                    arc.Center.X + Math.Cos(angle) * arc.Radius,
                    arc.Center.Y + Math.Sin(angle) * arc.Radius);
            }

            return new Point2D(
                (arc.Start.X + arc.End.X) / 2.0,
                (arc.Start.Y + arc.End.Y) / 2.0);
        }

        private static string CreateSlotGroupId(int slotIndex)
        {
            return "SLOT" + slotIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private ChamferFeature2D CreateChamferFeature(Segment2D segment, double dx, double dy, double chamferLeg)
        {
            return new ChamferFeature2D
            {
                StartPoint = segment.Start,
                EndPoint = segment.End,
                Length = segment.Length,
                DeltaX = dx,
                DeltaY = dy,
                Value = chamferLeg,
                Text = _config.FormatNumber(chamferLeg) + "x45%%d",
                SourceSegment = segment,
                Confidence = 0.9
            };
        }

        private bool TryGetChamferAxisNeighbors(
            OutlineFeature2D outline,
            Segment2D chamfer,
            out Segment2D firstNeighbor,
            out Segment2D secondNeighbor)
        {
            firstNeighbor = FindAxisNeighborAtPoint(outline, chamfer, chamfer.Start);
            secondNeighbor = FindAxisNeighborAtPoint(outline, chamfer, chamfer.End);
            return firstNeighbor != null
                && secondNeighbor != null
                && !ReferenceEquals(firstNeighbor, secondNeighbor)
                && ArePerpendicularAxisSegments(firstNeighbor, secondNeighbor);
        }

        private Segment2D FindAxisNeighborAtPoint(OutlineFeature2D outline, Segment2D chamfer, Point2D point)
        {
            return outline.Segments.FirstOrDefault(segment =>
                !ReferenceEquals(segment, chamfer)
                && !segment.IsArcChord
                && (segment.IsHorizontal(_config.GeometryTolerance) || segment.IsVertical(_config.GeometryTolerance))
                && (PointsEqual(segment.Start, point) || PointsEqual(segment.End, point)));
        }

        private bool ArePerpendicularAxisSegments(Segment2D first, Segment2D second)
        {
            return (first.IsHorizontal(_config.GeometryTolerance) && second.IsVertical(_config.GeometryTolerance))
                || (first.IsVertical(_config.GeometryTolerance) && second.IsHorizontal(_config.GeometryTolerance));
        }

        private bool PointsEqual(Point2D a, Point2D b)
        {
            return Math.Abs(a.X - b.X) <= _config.GeometryTolerance
                && Math.Abs(a.Y - b.Y) <= _config.GeometryTolerance;
        }

        private void AddUniqueVertex(OutlineFeature2D outline, Point2D point)
        {
            if (!outline.Vertices.Any(existing => PointsEqual(existing, point)))
            {
                outline.Vertices.Add(point);
            }
        }

        private sealed class SlotArcCandidate
        {
            public string Id { get; set; }
            public Point2D Center { get; set; }
            public Point2D Start { get; set; }
            public Point2D End { get; set; }
            public Point2D Mid { get; set; }
            public double Radius { get; set; }
        }

        private sealed class SlotLineCandidate
        {
            public string Id { get; set; }
            public Point2D Start { get; set; }
            public Point2D End { get; set; }
            public double Length
            {
                get { return Start.DistanceTo(End); }
            }
        }
    }
}
