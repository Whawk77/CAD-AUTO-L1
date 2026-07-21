using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace CadAuto.CadAdapter.Model;

public sealed class OutlineSegment
{
	public Point2d Start { get; set; }

	public Point2d End { get; set; }

	public ObjectId SourceId { get; set; } = ObjectId.Null;

	public bool IsArcChord { get; set; }

	public double MinX => Math.Min(Start.X, End.X);

	public double MaxX => Math.Max(Start.X, End.X);

	public double MinY => Math.Min(Start.Y, End.Y);

	public double MaxY => Math.Max(Start.Y, End.Y);

	public double LengthX => MaxX - MinX;

	public double LengthY => MaxY - MinY;

	public double Length => Start.GetDistanceTo(End);

	public bool IsHorizontal(double tolerance)
	{
		return Math.Abs(Start.Y - End.Y) <= tolerance;
	}

	public bool IsVertical(double tolerance)
	{
		return Math.Abs(Start.X - End.X) <= tolerance;
	}
}
