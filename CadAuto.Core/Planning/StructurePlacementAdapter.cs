using System;
using System.Collections.Generic;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning;

/// <summary>
/// Maps kept <see cref="StructureFeature"/> to <see cref="PlannedDimension"/> with WCS sides.
/// </summary>
public sealed class StructurePlacementAdapter
{
	private readonly DimensionRuleConfig _config;

	public StructurePlacementAdapter(DimensionRuleConfig config)
	{
		_config = config ?? DimensionRuleConfig.CreateDefault();
	}

	public IList<PlannedDimension> Place(IList<StructureFeature> kept, OutlineFeature2D outline)
	{
		List<PlannedDimension> dims = new List<PlannedDimension>();
		if (kept == null)
		{
			return dims;
		}
		foreach (StructureFeature f in kept)
		{
			if (f == null || !f.Keep)
			{
				continue;
			}
			if (f.Kind == StructureFeatureKind.Overall)
			{
				dims.Add(PlaceOverall(f, outline));
				continue;
			}
			dims.Add(PlaceStructure(f));
		}
		return dims;
	}

	private PlannedDimension PlaceOverall(StructureFeature f, OutlineFeature2D outline)
	{
		if (f.Axis == StructureFeatureAxis.Horizontal)
		{
			return new PlannedDimension
			{
				Kind = DimensionKind.OverallWidth,
				Orientation = DimensionOrientation.Horizontal,
				Side = DimensionSide.Bottom,
				FirstPoint = new Point2D(outline.MinX, outline.MinY),
				SecondPoint = new Point2D(outline.MaxX, outline.MinY),
				ForceOuterLevel = true,
				DebugRole = "OverallWidth",
				Role = DimensionCandidateRole.Overall,
				ReadingLevel = DimensionReadingLevel.Overall,
				AlignmentPriority = 100
			};
		}
		return new PlannedDimension
		{
			Kind = DimensionKind.OverallHeight,
			Orientation = DimensionOrientation.Vertical,
			Side = DimensionSide.Left,
			FirstPoint = new Point2D(outline.MinX, outline.MinY),
			SecondPoint = new Point2D(outline.MinX, outline.MaxY),
			ForceOuterLevel = true,
			DebugRole = "OverallHeight",
			Role = DimensionCandidateRole.Overall,
			ReadingLevel = DimensionReadingLevel.Overall,
			AlignmentPriority = 100
		};
	}

	private PlannedDimension PlaceStructure(StructureFeature f)
	{
		bool horizontal = f.Axis == StructureFeatureAxis.Horizontal;
		string role = horizontal
			? (f.PreferredSide == DimensionSide.Top ? "TopStructWidth" : "BottomStructWidth")
			: (f.PreferredSide == DimensionSide.Right ? "RightStructHeight" : "LeftStructHeight");
		// Prefer extractor side; fall back by axis.
		DimensionSide side = f.PreferredSide;
		if (horizontal && side != DimensionSide.Top && side != DimensionSide.Bottom)
		{
			side = DimensionSide.Bottom;
			role = "BottomStructWidth";
		}
		if (!horizontal && side != DimensionSide.Left && side != DimensionSide.Right)
		{
			side = DimensionSide.Left;
			role = "LeftStructHeight";
		}
		string align = f.PreferLocalPlacement
			? "Structure:" + (horizontal ? "H" : "V") + ":Local:" + side
			: "Structure:" + (horizontal ? "H" : "V") + ":" + side;
		return new PlannedDimension
		{
			Kind = DimensionKind.Normal,
			Orientation = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical,
			Side = side,
			FirstPoint = f.FirstPoint,
			SecondPoint = f.SecondPoint,
			DebugRole = role,
			SourceKey = f.SourceKey,
			Role = DimensionCandidateRole.Structure,
			ReadingLevel = DimensionReadingLevel.LocalSpacing,
			AlignmentKey = align,
			AlignmentPriority = f.PreferLocalPlacement ? 40 : 70,
			PreferLocalBoundary = f.PreferLocalPlacement,
			PreferFeatureLocalPlacement = f.PreferLocalPlacement,
			PreservePreferredSide = f.PreferLocalPlacement
		};
	}
}
