using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;

namespace CadAuto.CadAdapter
{
    public static class AutoCadGeometryConverter
    {
        public static Point2D ToCore(Point2d point)
        {
            return new Point2D(point.X, point.Y);
        }

        public static Point2D ToCore(Point3d point)
        {
            return new Point2D(point.X, point.Y);
        }

        public static Point3d ToCad(Point2D point)
        {
            return new Point3d(point.X, point.Y, 0.0);
        }

        public static Circle2D ToCore(Circle circle)
        {
            if (circle == null)
            {
                throw new ArgumentNullException("circle");
            }

            return new Circle2D
            {
                Center = ToCore(circle.Center),
                Radius = circle.Radius,
                SourceKey = circle.ObjectId.ToString()
            };
        }

        public static Arc2D ToCore(Arc arc)
        {
            if (arc == null)
            {
                throw new ArgumentNullException("arc");
            }

            return new Arc2D
            {
                Start = ToCore(arc.StartPoint),
                End = ToCore(arc.EndPoint),
                Center = ToCore(arc.Center),
                Radius = arc.Radius,
                SourceKey = arc.ObjectId.ToString()
            };
        }

        public static Segment2D ToCore(Line line)
        {
            if (line == null)
            {
                throw new ArgumentNullException("line");
            }

            return new Segment2D(ToCore(line.StartPoint), ToCore(line.EndPoint))
            {
                SourceKey = line.ObjectId.ToString()
            };
        }

        public static OutlineFeature2D ToCoreOutline(Polyline polyline)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException("polyline");
            }

            var outline = new OutlineFeature2D
            {
                MinX = double.MaxValue,
                MinY = double.MaxValue,
                MaxX = double.MinValue,
                MaxY = double.MinValue
            };

            var count = polyline.NumberOfVertices;
            for (int i = 0; i < count; i++)
            {
                var point = ToCore(polyline.GetPoint2dAt(i));
                outline.Vertices.Add(point);
                Include(outline, point);
            }

            for (int i = 0; i < count; i++)
            {
                var next = (i + 1) % count;
                var start = ToCore(polyline.GetPoint2dAt(i));
                var end = ToCore(polyline.GetPoint2dAt(next));
                var bulge = polyline.GetBulgeAt(i);

                if (Math.Abs(bulge) <= 1e-9)
                {
                    outline.Segments.Add(new Segment2D(start, end)
                    {
                        SourceKey = polyline.ObjectId.ToString()
                    });
                }
                else
                {
                    var arc = CreateBulgeArc(start, end, bulge, polyline.ObjectId.ToString());
                    outline.Arcs.Add(arc);
                    outline.Segments.Add(new Segment2D(start, end)
                    {
                        SourceKey = polyline.ObjectId.ToString(),
                        IsArcChord = true
                    });
                }
            }

            return outline;
        }

        private static Arc2D CreateBulgeArc(Point2D start, Point2D end, double bulge, string sourceKey)
        {
            var chord = start.DistanceTo(end);
            var theta = 4.0 * Math.Atan(bulge);
            var radius = Math.Abs(chord / (2.0 * Math.Sin(theta / 2.0)));
            var midX = (start.X + end.X) * 0.5;
            var midY = (start.Y + end.Y) * 0.5;
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var h = Math.Sqrt(Math.Max(radius * radius - (chord * chord * 0.25), 0.0));
            var sign = bulge >= 0.0 ? 1.0 : -1.0;
            var center = new Point2D(midX - sign * dy / length * h, midY + sign * dx / length * h);

            return new Arc2D
            {
                Start = start,
                End = end,
                Center = center,
                Radius = radius,
                Bulge = bulge,
                SourceKey = sourceKey
            };
        }

        private static void Include(OutlineFeature2D outline, Point2D point)
        {
            outline.MinX = Math.Min(outline.MinX, point.X);
            outline.MaxX = Math.Max(outline.MaxX, point.X);
            outline.MinY = Math.Min(outline.MinY, point.Y);
            outline.MaxY = Math.Max(outline.MaxY, point.Y);
        }
    }
}
