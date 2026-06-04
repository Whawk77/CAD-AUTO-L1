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
    }
}
