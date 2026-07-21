using System;

namespace CadAuto.Core.Geometry;

public sealed class BoundingBox2D
{
	public double MinX { get; private set; }

	public double MaxX { get; private set; }

	public double MinY { get; private set; }

	public double MaxY { get; private set; }

	public double Width => IsEmpty ? 0.0 : (MaxX - MinX);

	public double Height => IsEmpty ? 0.0 : (MaxY - MinY);

	public bool IsEmpty => MinX == double.MaxValue;

	public BoundingBox2D()
	{
		MinX = double.MaxValue;
		MinY = double.MaxValue;
		MaxX = double.MinValue;
		MaxY = double.MinValue;
	}

	public void Include(Point2D point)
	{
		MinX = Math.Min(MinX, point.X);
		MaxX = Math.Max(MaxX, point.X);
		MinY = Math.Min(MinY, point.Y);
		MaxY = Math.Max(MaxY, point.Y);
	}
}
