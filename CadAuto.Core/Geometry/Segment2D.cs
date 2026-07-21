using System;

namespace CadAuto.Core.Geometry;

public sealed class Segment2D
{
	public Point2D Start { get; set; }

	public Point2D End { get; set; }

	public string SourceKey { get; set; }

	public bool IsArcChord { get; set; }

	public double MinX => Math.Min(Start.X, End.X);

	public double MaxX => Math.Max(Start.X, End.X);

	public double MinY => Math.Min(Start.Y, End.Y);

	public double MaxY => Math.Max(Start.Y, End.Y);

	public double LengthX => MaxX - MinX;

	public double LengthY => MaxY - MinY;

	public double Length => Start.DistanceTo(End);

	public Segment2D()
	{
	}

	public Segment2D(Point2D start, Point2D end)
	{
		Start = start;
		End = end;
	}

	public bool IsHorizontal(double tolerance)
	{
		return Math.Abs(Start.Y - End.Y) <= tolerance;
	}

	public bool IsVertical(double tolerance)
	{
		return Math.Abs(Start.X - End.X) <= tolerance;
	}
}
