using System;
using System.Globalization;

namespace CadAuto.Core.Geometry
{
    public struct Point2D : IEquatable<Point2D>
    {
        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        public double DistanceTo(Point2D other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public bool Equals(Point2D other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        public override bool Equals(object obj)
        {
            return obj is Point2D && Equals((Point2D)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.###},{1:0.###})", X, Y);
        }
    }
}
