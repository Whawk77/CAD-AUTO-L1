using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public sealed partial class DimensionLayoutRules
{
	// Only explicitly owned parallel stacks participate. Legacy lanes and selected sides stay intact.
	private void CompactParallelDatumStacks(IList<DimensionStackingPlacement> placements,
		IList<DimensionLayoutItem> dimensions, IList<LayoutBlock> blocks, DimensionSide side,
		OutlineFeature2D outline, double textHeight, double gap, double firstOffset, double spacing, bool horizontal)
	{
		if (_config.DisabledExperimentRule == DimensionExperimentRule.ParallelDatumCompaction) { return; }
		if (outline == null || spacing <= _config.GeometryTolerance) { return; }
		var byBlock = placements.GroupBy(p => p.LayoutBlockId).ToDictionary(g => g.Key, g => g.ToList());
		var eligible = new HashSet<string>(dimensions.Where(d => !string.IsNullOrEmpty(d.DatumFamilyKey) && !string.IsNullOrEmpty(d.ParallelStackKey))
			.GroupBy(d => d.ParallelStackKey).Where(g => g.Count() > 1 && g.Select(d => d.DatumFamilyKey).Distinct().Count() == 1)
			.Select(g => g.Key), StringComparer.Ordinal);
		double clearance = Math.Max(Math.Abs(spacing), textHeight + gap);
		for (int index = 0; index < blocks.Count; index++)
		{
			var block = blocks[index];
			if (!byBlock.TryGetValue(block.Id, out var members) || members.Count != 1 || block.ForceOuterLevel) { continue; }
			var placement = members[0];
			var dimension = dimensions[placement.Index];
			if (!eligible.Contains(dimension.ParallelStackKey ?? "") || placement.AlignmentLaneMemberCount > 1) { continue; }
			double original = GetPlacementPhysicalOutwardRank(placement, dimensions, side, outline);
			double minimum = GetPhysicalOutwardRank(side, GetDimLineCoordinate(dimension, side, outline, firstOffset));
			for (int inner = 0; inner < index; inner++)
			{
				if (RequiresPhysicalOutwardOrder(blocks[inner], block, horizontal) && byBlock.TryGetValue(blocks[inner].Id, out var peers))
				{
					minimum = Math.Max(minimum, peers.Max(p => GetPlacementPhysicalOutwardRank(p, dimensions, side, outline)) + clearance);
				}
			}
			placement.CompactionDecision = "RetainedForPhysicalClearance";
			for (double rank = minimum; rank < original - _config.GeometryTolerance; rank += clearance)
			{
				double coordinate = side == DimensionSide.Left || side == DimensionSide.Bottom ? -rank : rank;
				if (DimensionLineEntersOutlineInterior(dimension, side, coordinate, outline)) { continue; }
				var text = ComputeTextInterval(dimension, horizontal, textHeight);
				var arrows = ComputeArrowInterval(dimension, horizontal);
				bool conflict = placements.Where(p => p != placement).Any(p =>
				{
					if (Math.Abs(rank - GetPlacementPhysicalOutwardRank(p, dimensions, side, outline)) >= clearance - _config.GeometryTolerance) { return false; }
					var other = dimensions[p.Index];
					var otherArrows = ComputeArrowInterval(other, horizontal);
					var otherText = ComputeTextInterval(other, horizontal, textHeight);
					return HasStrictArrowConflict(arrows.Item1, arrows.Item2, otherArrows.Item1, otherArrows.Item2)
						|| !AreCompatible(text.Item1, text.Item2, otherText.Item1, otherText.Item2, gap);
				});
				if (conflict) { continue; }
				placement.DimLineCoordinateOverride = coordinate;
				placement.CompactionDecision = "CompactedToSafePhysicalCoordinate";
				break;
			}
		}
	}
}
