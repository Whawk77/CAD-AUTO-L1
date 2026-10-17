using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning.Modules;

public interface IOutlineFacts
{
	OutlineFeature2D Outline { get; }

	double MinX { get; }

	double MaxX { get; }

	double MinY { get; }

	double MaxY { get; }

	double Width { get; }

	double Height { get; }

	bool TryGetEnvelope(out OutlineEnvelope2D envelope);

	bool IsPointOnBoundary(Point2D point);

	bool TryFindHorizontalBoundaryPoint(double y, double preferredX, out Point2D point);

	bool TryFindVerticalBoundaryPoint(double x, double preferredY, out Point2D point);
}
