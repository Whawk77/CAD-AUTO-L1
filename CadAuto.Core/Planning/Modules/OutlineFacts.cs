using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning.Modules;

public sealed class OutlineFacts : IOutlineFacts
{
	private readonly double _tolerance;

	public OutlineFeature2D Outline { get; }

	public double MinX => Outline.MinX;

	public double MaxX => Outline.MaxX;

	public double MinY => Outline.MinY;

	public double MaxY => Outline.MaxY;

	public double Width => Outline.Width;

	public double Height => Outline.Height;

	public OutlineFacts(OutlineFeature2D outline, double tolerance)
	{
		Outline = outline;
		_tolerance = tolerance;
	}

	public static OutlineFacts From(OutlineFeature2D outline, double tolerance)
	{
		return outline == null ? null : new OutlineFacts(outline, tolerance);
	}

	public bool TryGetEnvelope(out OutlineEnvelope2D envelope)
	{
		return OutlineGeometryQuery.TryGetEnvelope(Outline, _tolerance, out envelope);
	}

	public bool IsPointOnBoundary(Point2D point)
	{
		return OutlineGeometryQuery.IsPointOnBoundary(point, Outline, _tolerance);
	}

	public bool TryFindHorizontalBoundaryPoint(double y, double preferredX, out Point2D point)
	{
		return OutlineGeometryQuery.TryFindHorizontalBoundaryPoint(Outline, y, preferredX, _tolerance, out point);
	}

	public bool TryFindVerticalBoundaryPoint(double x, double preferredY, out Point2D point)
	{
		return OutlineGeometryQuery.TryFindVerticalBoundaryPoint(Outline, x, preferredY, _tolerance, out point);
	}
}
