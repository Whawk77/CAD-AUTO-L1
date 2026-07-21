using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;
using CadAuto.Core.Planning;

namespace CadAuto.CadAdapter.Rendering;

internal sealed class DimensionPlanCadMapper
{
	public IEnumerable<DimensionPlanCadItem> Map(DimensionPlan plan)
	{
		if (plan == null)
		{
			yield break;
		}
		foreach (PlannedDimension dimension in plan.Dimensions)
		{
			DimensionPlanCadItem item = Map(dimension);
			if (item != null)
			{
				yield return item;
			}
		}
	}

	private static DimensionPlanCadItem Map(PlannedDimension dimension)
	{
		if (dimension == null)
		{
			return null;
		}
		if (dimension.Orientation != DimensionOrientation.Horizontal && dimension.Orientation != DimensionOrientation.Vertical)
		{
			return null;
		}
		return new DimensionPlanCadItem
		{
			DiagnosticId = dimension.DiagnosticId,
			Rotation = ((dimension.Orientation == DimensionOrientation.Horizontal) ? 0.0 : (Math.PI / 2.0)),
			FirstPoint = new Point3d(dimension.FirstPoint.X, dimension.FirstPoint.Y, 0.0),
			SecondPoint = new Point3d(dimension.SecondPoint.X, dimension.SecondPoint.Y, 0.0),
			OverrideText = (dimension.OverrideText ?? string.Empty),
			Span = GetSpan(dimension),
			DimensionType = ToDimensionType(dimension.Kind),
			ForceOuterLevel = dimension.ForceOuterLevel,
			UseSegmentedExtensionLines = dimension.UseSegmentedExtensionLines,
			ChainId = dimension.ChainId,
			AlignmentKey = dimension.AlignmentKey,
			AlignmentPriority = dimension.AlignmentPriority,
			PreserveAlignmentLevel = dimension.PreserveAlignmentLevel,
			ReadingLevel = dimension.ReadingLevel,
			PreferLocalBoundary = dimension.PreferLocalBoundary,
			PreferFeatureLocalPlacement = dimension.PreferFeatureLocalPlacement,
			PreservePreferredSide = dimension.PreservePreferredSide,
			DebugOwner = dimension.DebugOwner,
			DebugRole = dimension.DebugRole,
			Side = dimension.Side
		};
	}

	private static double GetSpan(PlannedDimension dimension)
	{
		return (dimension.Orientation == DimensionOrientation.Horizontal) ? Math.Abs(dimension.SecondPoint.X - dimension.FirstPoint.X) : Math.Abs(dimension.SecondPoint.Y - dimension.FirstPoint.Y);
	}

	private static DimensionType ToDimensionType(DimensionKind kind)
	{
		return kind switch
		{
			DimensionKind.OverallWidth => DimensionType.OverallWidth,
			DimensionKind.OverallHeight => DimensionType.OverallHeight,
			DimensionKind.HoleDiameter => DimensionType.HoleDiameter,
			DimensionKind.PinDistance => DimensionType.PinDistance,
			DimensionKind.PinGroupDistance => DimensionType.PinGroupDistance,
			DimensionKind.HoleLocation => DimensionType.HoleLocation,
			DimensionKind.DatumHoleLocationX => DimensionType.DatumHoleLocationX,
			DimensionKind.DatumHoleLocationY => DimensionType.DatumHoleLocationY,
			_ => DimensionType.Normal,
		};
	}
}
