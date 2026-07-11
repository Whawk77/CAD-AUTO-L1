using System;
using System.Globalization;

namespace CadAuto.Core.Geometry;

public struct Point2D : IEquatable<Point2D>
{
	public double X { get; }

	public double Y { get; }

	public Point2D(double x, double y)
	{
		X = x;
		Y = y;
	}

	public double DistanceTo(Point2D other)
	{
		double num = X - other.X;
		double num2 = Y - other.Y;
		return Math.Sqrt(num * num + num2 * num2);
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
		return (X.GetHashCode() * 397) ^ Y.GetHashCode();
	}

	public override string ToString()
	{
		return string.Format(CultureInfo.InvariantCulture, "({0:0.###},{1:0.###})", X, Y);
	}
}
