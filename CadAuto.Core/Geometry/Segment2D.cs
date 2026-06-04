using System;

namespace CadAuto.Core.Geometry
{
    public sealed class Segment2D
    {
        public Segment2D()
        {
        }

        public Segment2D(Point2D start, Point2D end)
        {
            Start = start;
            End = end;
        }

        public Point2D Start { get; set; }
        public Point2D End { get; set; }
        public string SourceKey { get; set; }
        public bool IsArcChord { get; set; }

        public double MinX { get { return Math.Min(Start.X, End.X); } }
        public double MaxX { get { return Math.Max(Start.X, End.X); } }
        public double MinY { get { return Math.Min(Start.Y, End.Y); } }
        public double MaxY { get { return Math.Max(Start.Y, End.Y); } }
        public double LengthX { get { return MaxX - MinX; } }
        public double LengthY { get { return MaxY - MinY; } }
        public double Length { get { return Start.DistanceTo(End); } }

        public bool IsHorizontal(double tolerance)
        {
            return Math.Abs(Start.Y - End.Y) <= tolerance;
        }

        public bool IsVertical(double tolerance)
        {
            return Math.Abs(Start.X - End.X) <= tolerance;
        }
    }
}
