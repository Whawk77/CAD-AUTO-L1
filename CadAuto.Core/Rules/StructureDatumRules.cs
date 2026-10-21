using System;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;

namespace CadAuto.Core.Rules;

public static class StructureDatumRules
{
	public const string VerticalCommonDatumRule = "StepDatum.VerticalCommonDatum";
	public const string HorizontalCommonDatumRule = "StepDatum.HorizontalCommonDatum";
	public static bool IsCommonDatumRule(string rule) => rule == VerticalCommonDatumRule || rule == HorizontalCommonDatumRule;

	public static bool IsDatumBlock(string name) =>
		string.Equals(name, "CadAider_日标2个角", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(name, "CadAider_国标粗糙度32下", StringComparison.OrdinalIgnoreCase)
		|| string.Equals(name, "CadAider_国标粗糙度32", StringComparison.OrdinalIgnoreCase);

	public static StructureDatumMarker2D CreateInsertionMarker(string name, string sourceId, Point2D insertion,
		Point2D transformedX, Point2D transformedY, OutlineFeature2D outline, double tolerance, bool dynamicBlock = false)
	{
		string failure = null;
		bool horizontalSurface = Math.Abs(transformedX.X) > 1E-12 && Math.Abs(transformedY.Y) > 1E-12
			&& Math.Abs(transformedX.Y) <= Math.Abs(transformedX.X) * 1E-6
			&& Math.Abs(transformedY.X) <= Math.Abs(transformedY.Y) * 1E-6;
		bool verticalSurface = Math.Abs(transformedX.Y) > 1E-12 && Math.Abs(transformedY.X) > 1E-12
			&& Math.Abs(transformedX.X) <= Math.Abs(transformedX.Y) * 1E-6
			&& Math.Abs(transformedY.Y) <= Math.Abs(transformedY.X) * 1E-6;
		if (!IsDatumBlock(name)) { failure = "UnknownBlockName"; }
		else if (dynamicBlock) { failure = "DynamicBlockContactUnverified"; }
		else if (!Finite(insertion) || !Finite(transformedX) || !Finite(transformedY)) { failure = "InvalidBlockTransform"; }
		else if (!horizontalSurface && !verticalSurface) { failure = "BlockNotAlignedToAxisDatum"; }
		else if (outline == null || !outline.Segments.Any(e => e != null && !e.IsArcChord
				&& (horizontalSurface ? e.IsHorizontal(tolerance) && Math.Abs(insertion.Y - e.Start.Y) <= tolerance
					&& insertion.X >= e.MinX - tolerance && insertion.X <= e.MaxX + tolerance
				: e.IsVertical(tolerance) && Math.Abs(insertion.X - e.Start.X) <= tolerance
					&& insertion.Y >= e.MinY - tolerance && insertion.Y <= e.MaxY + tolerance)))
		{ failure = horizontalSurface ? "InsertionNotOnHorizontalBoundary" : "InsertionNotOnVerticalBoundary"; }
		return new StructureDatumMarker2D { BlockName = name, SourceGeometryId = sourceId, ContactPoint = insertion,
			ContactKind = verticalSurface ? "BlockInsertionVertical" : "BlockInsertion", ValidationFailure = failure };
	}

	private static bool Finite(Point2D point) => !double.IsNaN(point.X) && !double.IsInfinity(point.X)
		&& !double.IsNaN(point.Y) && !double.IsInfinity(point.Y);
}
