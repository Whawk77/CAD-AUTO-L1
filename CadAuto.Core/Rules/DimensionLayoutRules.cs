using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public sealed class DimensionLayoutRules
{
	private sealed class StackingLayerItem
	{
		public int Index { get; set; }

		public DimensionLayoutItem Dimension { get; set; }

		public double TxtA { get; set; }

		public double TxtB { get; set; }

		public double ArrA { get; set; }

		public double ArrB { get; set; }

		public string AlignmentLaneKey { get; set; }

		public string LayoutBlockId { get; set; }

		public string LayoutBlockType { get; set; }

		public int LayoutBlockMemberCount { get; set; }
	}

	private sealed class LayoutBlock
	{
		public string Id { get; set; }

		public string Type { get; set; }

		public List<IndexedLayoutItem> Members { get; } = new List<IndexedLayoutItem>();

		public double EffectiveSpan { get; set; }

		public DimensionReadingLevel ReadingLevel { get; set; }

		public bool ForceOuterLevel { get; set; }

		public int FirstSourceIndex { get; set; }

		public int EffectiveOrder { get; set; }

		public string OrderingReason { get; set; }

		public string PromotedByConflictWith { get; set; }
	}

	private sealed class TextSlideCandidate
	{
		public Point2D Position { get; set; }

		public TextBounds2D Bounds { get; set; }

		public int Score { get; set; }
	}

	private sealed class ExtensionLineBreakCandidate
	{
		public double A { get; set; }

		public double B { get; set; }

		public double DistanceToFeature { get; set; }

		public double Length { get; set; }
	}

	private sealed class IndexedLayoutItem
	{
		public int SourceIndex { get; set; }

		public DimensionLayoutItem Dimension { get; set; }
	}

	private readonly DimensionRuleConfig _config;

	private readonly StructureSuppressionRules _structureRules;

	public DimensionLayoutRules(DimensionRuleConfig config)
	{
		if (config == null)
		{
			throw new ArgumentNullException("config");
		}
		_config = config;
		_structureRules = new StructureSuppressionRules(config);
	}

	public List<DimensionStackingPlacement> CreateStackingPlan(IList<DimensionLayoutItem> dimensions, DimensionSide side, OutlineFeature2D outline, double textHeight, double gap, double firstOffset, double perLevelSpacing, bool isHorizontal)
	{
		List<DimensionStackingPlacement> placements = new List<DimensionStackingPlacement>();
		if (dimensions == null || dimensions.Count == 0)
		{
			return placements;
		}
		List<LayoutBlock> layoutBlocks = GetOrderedLayoutBlocks(dimensions, isHorizontal);
		if (isHorizontal && side == DimensionSide.Top)
		{
			ApplyTopHorizontalFixedBlockOrder(layoutBlocks);
		}
		Dictionary<string, LayoutBlock> blockById = layoutBlocks.ToDictionary(block => block.Id, StringComparer.Ordinal);
		List<List<StackingLayerItem>> layers = new List<List<StackingLayerItem>>();
		foreach (LayoutBlock block in layoutBlocks)
		{
			List<StackingLayerItem> blockItems = block.Members
				.OrderBy(member => member.SourceIndex)
				.Select(member => CreateStackingLayerItem(member, block, isHorizontal, textHeight))
				.ToList();
			int targetLevel = 0;
			for (int level = 0; level < layers.Count; level++)
			{
				foreach (StackingLayerItem existing in layers[level])
				{
					bool hasStrictArrowConflict = blockItems.Any(member => HasStrictArrowConflict(member.ArrA, member.ArrB, existing.ArrA, existing.ArrB));
					bool requiresPhysicalOutwardOrder = blockById.TryGetValue(existing.LayoutBlockId ?? string.Empty, out var existingBlock)
						&& RequiresPhysicalOutwardOrder(existingBlock, block, isHorizontal);
					if ((hasStrictArrowConflict || requiresPhysicalOutwardOrder) && level + 1 > targetLevel)
					{
						targetLevel = level + 1;
						block.PromotedByConflictWith = existing.LayoutBlockId ?? string.Empty;
					}
				}
			}
			if (block.ForceOuterLevel)
			{
				targetLevel = Math.Max(targetLevel, layers.Count);
			}
			while (true)
			{
				double offset = firstOffset + (double)targetLevel * perLevelSpacing;
				if (blockItems.Any(member => TextCoversOutline(member.Dimension, side, offset, textHeight, outline, isHorizontal)))
				{
					targetLevel++;
					continue;
				}
				StackingLayerItem conflict = targetLevel < layers.Count
					? layers[targetLevel].FirstOrDefault(existing => blockItems.Any(member => HasStackingLayerConflict(member, existing, isHorizontal, gap)))
					: null;
				if (conflict != null)
				{
					block.PromotedByConflictWith = conflict.LayoutBlockId ?? string.Empty;
					targetLevel++;
					continue;
				}
				StackingLayerItem anchor = blockItems
					.OrderByDescending(item => item.Dimension.AlignmentPriority)
					.ThenBy(item => item.Dimension.Span)
					.ThenBy(item => item.Index)
					.First();
				if (HasPhysicalAlignmentGroupConflict(blockItems, anchor, targetLevel, layers, side, outline, textHeight, gap, firstOffset, perLevelSpacing))
				{
					targetLevel++;
					continue;
				}
				break;
			}
			while (layers.Count <= targetLevel)
			{
				layers.Add(new List<StackingLayerItem>());
			}
			layers[targetLevel].AddRange(blockItems);
		}
		AlignDimensionsBySharedExtensionLines(layers, side, outline, textHeight, gap, firstOffset, perLevelSpacing, isHorizontal);
		List<List<StackingLayerItem>> alignmentLanes = BuildAlignmentLanes(layers, gap);
		MoveAlignmentGroupsTogether(layers, alignmentLanes, side, outline, textHeight, gap, firstOffset, perLevelSpacing, isHorizontal);
		PromoteOverallDimensionsToOutermostLayer(layers);
		for (int level = 0; level < layers.Count; level++)
		{
			double offset = firstOffset + (double)level * perLevelSpacing;
			foreach (StackingLayerItem item in layers[level])
			{
				LayoutBlock block = blockById[item.LayoutBlockId];
				placements.Add(new DimensionStackingPlacement
				{
					Index = item.Index,
					Level = level,
					Offset = offset,
					LayoutBlockId = block.Id,
					LayoutBlockType = block.Type,
					EffectiveSpan = block.EffectiveSpan,
					EffectiveOrder = block.EffectiveOrder,
					OrderingReason = block.OrderingReason,
					PromotedByConflictWith = block.PromotedByConflictWith ?? string.Empty
				});
			}
		}
		ApplyAlignmentCoordinateOverrides(placements, dimensions, alignmentLanes, side, outline);
		ApplyRootedLayoutBlockCoordinateOverrides(placements, dimensions, side, outline);
		HashSet<string> unifiedLaneKeys = CaptureUnifiedAlignmentLaneKeys(placements, dimensions, side, outline);
		EnsureLayoutBlockPhysicalOutwardOrder(placements, dimensions, layoutBlocks, side, outline, perLevelSpacing, isHorizontal);
		RestoreSplitAlignmentLanes(placements, dimensions, side, outline, unifiedLaneKeys);
		EnsureOverallPhysicalOutermostOffset(placements, dimensions, side, outline, perLevelSpacing);
		UpdatePhysicalOrderDiagnostics(placements, dimensions, layoutBlocks, side, outline, isHorizontal);
		return placements;
	}

	public List<int> GetStackingOrder(IList<DimensionLayoutItem> dimensions, bool isHorizontal)
	{
		if (dimensions == null)
		{
			return new List<int>();
		}
		return GetOrderedLayoutBlocks(dimensions, isHorizontal)
			.SelectMany(block => block.Members.OrderBy(member => member.SourceIndex))
			.Select(member => member.SourceIndex)
			.ToList();
	}

	private List<LayoutBlock> GetOrderedLayoutBlocks(IList<DimensionLayoutItem> dimensions, bool isHorizontal)
	{
		List<LayoutBlock> blocks = BuildLayoutBlocks(dimensions, isHorizontal);
		blocks.Sort((first, second) => CompareLayoutBlocks(first, second));
		for (int index = 0; index < blocks.Count; index++)
		{
			LayoutBlock block = blocks[index];
			block.EffectiveOrder = index;
			block.OrderingReason = GetOrderingReason(block, blocks);
		}
		return blocks;
	}

	private void ApplyTopHorizontalFixedBlockOrder(IList<LayoutBlock> blocks)
	{
		List<LayoutBlock> datumChains = blocks.Where(IsRootedDatumOrPinChainBlock).ToList();
		List<LayoutBlock> functionalHoles = blocks
			.Where(IsPreserveLevelFunctionalHoleBlock)
			.Where(functional => datumChains.Any(chain =>
				SharesPinGroupSource(functional, chain)
				&& FunctionalHoleOrdersBeyondChain(functional, chain)))
			.ToList();
		datumChains = datumChains
			.Where(chain => functionalHoles.Any(functional => SharesPinGroupSource(functional, chain)))
			.ToList();
		List<LayoutBlock> topStructures = blocks.Where(IsTopStructureWidthRootBlock).ToList();
		if (datumChains.Count == 0 || functionalHoles.Count == 0 || topStructures.Count == 0)
		{
			return;
		}

		HashSet<LayoutBlock> fixedBlocks = new HashSet<LayoutBlock>(
			datumChains.Concat(functionalHoles).Concat(topStructures));
		List<int> fixedPositions = blocks
			.Select((block, index) => new { Block = block, Index = index })
			.Where(item => fixedBlocks.Contains(item.Block))
			.Select(item => item.Index)
			.ToList();
		List<LayoutBlock> fixedOrder = datumChains
			.Concat(functionalHoles)
			.Concat(topStructures)
			.ToList();
		for (int index = 0; index < fixedPositions.Count; index++)
		{
			blocks[fixedPositions[index]] = fixedOrder[index];
		}
		for (int index = 0; index < blocks.Count; index++)
		{
			blocks[index].EffectiveOrder = index;
			if (fixedBlocks.Contains(blocks[index]))
			{
				blocks[index].OrderingReason = "TopDatumFunctionalStructureFixedOrder";
			}
		}
	}

	private static bool IsTopStructureWidthRootBlock(LayoutBlock block)
	{
		return block != null
			&& string.Equals(block.Type, "RootedAlignmentLane", StringComparison.Ordinal)
			&& block.Members.Count > 0
			&& block.Members.All(member => member.Dimension != null
				&& member.Dimension.Kind == DimensionKind.Normal
				&& string.Equals(member.Dimension.SourceFeatureId, "TopStructWidth", StringComparison.Ordinal));
	}

	private List<LayoutBlock> BuildLayoutBlocks(IList<DimensionLayoutItem> dimensions, bool isHorizontal)
	{
		List<IndexedLayoutItem> indexedItems = dimensions
			.Select((dimension, index) => new IndexedLayoutItem { SourceIndex = index, Dimension = dimension })
			.Where(item => item.Dimension != null)
			.ToList();
		HashSet<int> assigned = new HashSet<int>();
		List<LayoutBlock> blocks = new List<LayoutBlock>();
		foreach (List<IndexedLayoutItem> looseChain in BuildLooseLayoutChains(
			indexedItems.Where(item => item.Dimension.LooseChainId != 0),
			isHorizontal))
		{
			string chainKey = string.Join("+", looseChain
				.Select(item => item.Dimension.LooseChainId)
				.Distinct()
				.OrderBy(chainId => chainId)
				.Select(chainId => chainId.ToString(CultureInfo.InvariantCulture)));
			LayoutBlock block = CreateLayoutBlock("LooseChain:" + chainKey, "LooseChain", looseChain, isHorizontal);
			blocks.Add(block);
			foreach (IndexedLayoutItem member in block.Members)
			{
				assigned.Add(member.SourceIndex);
			}
		}
		foreach (IGrouping<string, IndexedLayoutItem> alignmentGroup in indexedItems
			.Where(item => !assigned.Contains(item.SourceIndex)
				&& !item.Dimension.PreserveAlignmentLevel
				&& !string.IsNullOrEmpty(item.Dimension.AlignmentKey))
			.GroupBy(item => item.Dimension.AlignmentKey, StringComparer.Ordinal)
			.OrderBy(group => group.Min(item => item.SourceIndex)))
		{
			List<List<IndexedLayoutItem>> lanes = BuildRootedLayoutLanes(alignmentGroup, isHorizontal);
			int laneNumber = 1;
			foreach (List<IndexedLayoutItem> lane in lanes)
			{
				LayoutBlock block = CreateLayoutBlock("AlignmentLane:" + alignmentGroup.Key + "#" + laneNumber.ToString(CultureInfo.InvariantCulture), "RootedAlignmentLane", lane, isHorizontal);
				blocks.Add(block);
				foreach (IndexedLayoutItem laneMember in lane)
				{
					assigned.Add(laneMember.SourceIndex);
				}
				laneNumber++;
			}
		}
		foreach (IndexedLayoutItem item in indexedItems.Where(item => !assigned.Contains(item.SourceIndex)).OrderBy(item => item.SourceIndex))
		{
			blocks.Add(CreateLayoutBlock("Dimension:" + item.SourceIndex.ToString(CultureInfo.InvariantCulture), "SingleDimension", new[] { item }, isHorizontal));
		}
		return blocks;
	}

	private List<List<IndexedLayoutItem>> BuildLooseLayoutChains(IEnumerable<IndexedLayoutItem> source, bool isHorizontal)
	{
		List<IndexedLayoutItem> remaining = source.OrderBy(item => item.SourceIndex).ToList();
		List<List<IndexedLayoutItem>> chains = new List<List<IndexedLayoutItem>>();
		while (remaining.Count > 0)
		{
			List<IndexedLayoutItem> chain = new List<IndexedLayoutItem> { remaining[0] };
			remaining.RemoveAt(0);
			bool added;
			do
			{
				added = false;
				foreach (IndexedLayoutItem candidate in remaining.ToList())
				{
					if (!chain.Any(member => member.Dimension.LooseChainId == candidate.Dimension.LooseChainId
						|| SharesArrowEndpoint(member.Dimension, candidate.Dimension, isHorizontal)))
					{
						continue;
					}
					chain.Add(candidate);
					remaining.Remove(candidate);
					added = true;
				}
			}
			while (added);
			chains.Add(chain.OrderBy(item => item.SourceIndex).ToList());
		}
		return chains.OrderBy(chain => chain.Min(item => item.SourceIndex)).ToList();
	}

	private List<List<IndexedLayoutItem>> BuildRootedLayoutLanes(IEnumerable<IndexedLayoutItem> source, bool isHorizontal)
	{
		List<IndexedLayoutItem> remaining = source.OrderBy(item => item.SourceIndex).ToList();
		List<List<IndexedLayoutItem>> lanes = new List<List<IndexedLayoutItem>>();
		foreach (IndexedLayoutItem transfer in remaining.Where(item => item.Dimension.Kind == DimensionKind.PinGroupDistance).ToList())
		{
			IndexedLayoutItem pin = remaining
				.Where(item => item != transfer
					&& item.Dimension.Kind == DimensionKind.PinDistance
					&& SharesArrowEndpoint(transfer.Dimension, item.Dimension, isHorizontal)
					&& !HasStrictArrowConflict(transfer.Dimension, item.Dimension, isHorizontal))
				.OrderByDescending(item => HasSameSourceFeature(transfer.Dimension, item.Dimension))
				.ThenBy(item => item.SourceIndex)
				.FirstOrDefault();
			if (pin == null)
			{
				continue;
			}
			lanes.Add(new List<IndexedLayoutItem> { transfer, pin });
			remaining.Remove(transfer);
			remaining.Remove(pin);
		}
		foreach (IndexedLayoutItem datum in remaining.Where(item => item.Dimension.Kind == DimensionKind.DatumHoleLocationX || item.Dimension.Kind == DimensionKind.DatumHoleLocationY).ToList())
		{
			List<IndexedLayoutItem> lane = lanes.FirstOrDefault(candidate => CanJoinRootedLayoutLane(datum, candidate, isHorizontal));
			if (lane != null)
			{
				lane.Add(datum);
				remaining.Remove(datum);
				continue;
			}
			IndexedLayoutItem pin = remaining
				.Where(item => item != datum
					&& item.Dimension.Kind == DimensionKind.PinDistance
					&& SharesArrowEndpoint(datum.Dimension, item.Dimension, isHorizontal)
					&& !HasStrictArrowConflict(datum.Dimension, item.Dimension, isHorizontal))
				.OrderByDescending(item => HasSameSourceFeature(datum.Dimension, item.Dimension))
				.ThenBy(item => item.SourceIndex)
				.FirstOrDefault();
			if (pin != null)
			{
				lanes.Add(new List<IndexedLayoutItem> { datum, pin });
				remaining.Remove(datum);
				remaining.Remove(pin);
			}
		}
		foreach (IndexedLayoutItem item in remaining.OrderBy(candidate => candidate.SourceIndex).ToList())
		{
			List<IndexedLayoutItem> lane = lanes.FirstOrDefault(candidate => CanJoinRootedLayoutLane(item, candidate, isHorizontal));
			if (lane == null)
			{
				lane = new List<IndexedLayoutItem>();
				lanes.Add(lane);
			}
			lane.Add(item);
			remaining.Remove(item);
		}
		return lanes
			.OrderByDescending(lane => lane.Count)
			.ThenBy(lane => lane.Min(item => item.SourceIndex))
			.ToList();
	}

	private bool CanJoinRootedLayoutLane(IndexedLayoutItem item, IList<IndexedLayoutItem> lane, bool isHorizontal)
	{
		return lane.Any(member => SharesArrowEndpoint(item.Dimension, member.Dimension, isHorizontal))
			&& lane.All(member => !HasStrictArrowConflict(item.Dimension, member.Dimension, isHorizontal));
	}

	private bool HasStrictArrowConflict(DimensionLayoutItem first, DimensionLayoutItem second, bool isHorizontal)
	{
		Tuple<double, double> firstInterval = ComputeArrowInterval(first, isHorizontal);
		Tuple<double, double> secondInterval = ComputeArrowInterval(second, isHorizontal);
		return HasStrictArrowConflict(firstInterval.Item1, firstInterval.Item2, secondInterval.Item1, secondInterval.Item2);
	}

	private bool SharesArrowEndpoint(DimensionLayoutItem first, DimensionLayoutItem second, bool isHorizontal)
	{
		Tuple<double, double> firstInterval = ComputeArrowInterval(first, isHorizontal);
		Tuple<double, double> secondInterval = ComputeArrowInterval(second, isHorizontal);
		return SharesArrowEndpoint(firstInterval.Item1, firstInterval.Item2, secondInterval.Item1, secondInterval.Item2);
	}

	private static bool HasSameSourceFeature(DimensionLayoutItem first, DimensionLayoutItem second)
	{
		return !string.IsNullOrEmpty(first.SourceFeatureId)
			&& string.Equals(first.SourceFeatureId, second.SourceFeatureId, StringComparison.Ordinal);
	}

	private LayoutBlock CreateLayoutBlock(string id, string type, IEnumerable<IndexedLayoutItem> members, bool isHorizontal)
	{
		LayoutBlock block = new LayoutBlock { Id = id, Type = type, PromotedByConflictWith = string.Empty };
		block.Members.AddRange(members.OrderBy(member => member.SourceIndex));
		List<Tuple<double, double>> intervals = block.Members.Select(member => ComputeArrowInterval(member.Dimension, isHorizontal)).ToList();
		block.EffectiveSpan = intervals.Count == 0 ? 0.0 : intervals.Max(interval => interval.Item2) - intervals.Min(interval => interval.Item1);
		block.ReadingLevel = block.Members.Count == 0
			? DimensionReadingLevel.LocalSpacing
			: block.Members.Select(member => member.Dimension.ReadingLevel).OrderByDescending(level => level).First();
		block.ForceOuterLevel = block.Members.Any(member => member.Dimension.ForceOuterLevel);
		block.FirstSourceIndex = block.Members.Count == 0 ? int.MaxValue : block.Members.Min(member => member.SourceIndex);
		return block;
	}

	private int CompareLayoutBlocks(LayoutBlock first, LayoutBlock second)
	{
		if (first.ForceOuterLevel != second.ForceOuterLevel)
		{
			return first.ForceOuterLevel ? 1 : -1;
		}
		// Same pin-group: functional holes that reach past the rooted 41-30 (datum/pin) chain
		// must stack outside that chain even when their raw span is shorter (e.g. 45 outside 71).
		int functionalBeyondChain = CompareFunctionalHoleBeyondDatumChain(first, second);
		if (functionalBeyondChain != 0)
		{
			return functionalBeyondChain;
		}
		double spanDifference = first.EffectiveSpan - second.EffectiveSpan;
		if (Math.Abs(spanDifference) > _config.GeometryTolerance)
		{
			return spanDifference < 0.0 ? -1 : 1;
		}
		int readingLevelComparison = first.ReadingLevel.CompareTo(second.ReadingLevel);
		return readingLevelComparison != 0 ? readingLevelComparison : first.FirstSourceIndex.CompareTo(second.FirstSourceIndex);
	}

	private int CompareFunctionalHoleBeyondDatumChain(LayoutBlock first, LayoutBlock second)
	{
		bool firstFunc = IsPreserveLevelFunctionalHoleBlock(first);
		bool secondFunc = IsPreserveLevelFunctionalHoleBlock(second);
		bool firstChain = IsRootedDatumOrPinChainBlock(first);
		bool secondChain = IsRootedDatumOrPinChainBlock(second);
		if (firstFunc && secondChain && SharesPinGroupSource(first, second))
		{
			return FunctionalHoleOrdersBeyondChain(first, second) ? 1 : -1;
		}
		if (secondFunc && firstChain && SharesPinGroupSource(first, second))
		{
			return FunctionalHoleOrdersBeyondChain(second, first) ? -1 : 1;
		}
		return 0;
	}

	private static bool IsPreserveLevelFunctionalHoleBlock(LayoutBlock block)
	{
		return block != null
			&& block.Members.Count > 0
			&& block.Members.All(member => member.Dimension != null
				&& member.Dimension.Kind == DimensionKind.HoleLocation
				&& member.Dimension.PreserveAlignmentLevel);
	}

	private static bool IsRootedDatumOrPinChainBlock(LayoutBlock block)
	{
		if (block == null || block.Members.Count == 0)
		{
			return false;
		}
		if (string.Equals(block.Type, "RootedAlignmentLane", StringComparison.Ordinal))
		{
			return true;
		}
		return block.Members.Any(member => member.Dimension != null
			&& (member.Dimension.Kind == DimensionKind.DatumHoleLocationX
				|| member.Dimension.Kind == DimensionKind.DatumHoleLocationY
				|| member.Dimension.Kind == DimensionKind.PinDistance
				|| member.Dimension.Kind == DimensionKind.PinGroupDistance));
	}

	private static bool SharesPinGroupSource(LayoutBlock first, LayoutBlock second)
	{
		HashSet<string> firstIds = new HashSet<string>(
			first.Members
				.Select(member => member.Dimension?.SourceFeatureId)
				.Where(id => !string.IsNullOrEmpty(id)),
			StringComparer.Ordinal);
		if (firstIds.Count == 0)
		{
			return false;
		}
		return second.Members.Any(member => !string.IsNullOrEmpty(member.Dimension?.SourceFeatureId)
			&& firstIds.Contains(member.Dimension.SourceFeatureId));
	}

	private bool FunctionalHoleOrdersBeyondChain(LayoutBlock functionalHole, LayoutBlock chain)
	{
		// Horizontal dims: beyond on X. Vertical dims: beyond on Y. Mixed blocks fall back to both.
		bool horizontal = functionalHole.Members.Any(member =>
			member.Dimension.Kind == DimensionKind.HoleLocation
			&& Math.Abs(member.Dimension.FirstPoint.Y - member.Dimension.SecondPoint.Y) <= _config.GeometryTolerance);
		bool vertical = functionalHole.Members.Any(member =>
			member.Dimension.Kind == DimensionKind.HoleLocation
			&& Math.Abs(member.Dimension.FirstPoint.X - member.Dimension.SecondPoint.X) <= _config.GeometryTolerance);
		if (horizontal || !vertical)
		{
			double funcMin = functionalHole.Members.Min(member => Math.Min(member.Dimension.FirstPoint.X, member.Dimension.SecondPoint.X));
			double funcMax = functionalHole.Members.Max(member => Math.Max(member.Dimension.FirstPoint.X, member.Dimension.SecondPoint.X));
			double chainMin = chain.Members.Min(member => Math.Min(member.Dimension.FirstPoint.X, member.Dimension.SecondPoint.X));
			double chainMax = chain.Members.Max(member => Math.Max(member.Dimension.FirstPoint.X, member.Dimension.SecondPoint.X));
			if (funcMax > chainMax + _config.GeometryTolerance || funcMin < chainMin - _config.GeometryTolerance)
			{
				return true;
			}
		}
		if (vertical)
		{
			double funcMin = functionalHole.Members.Min(member => Math.Min(member.Dimension.FirstPoint.Y, member.Dimension.SecondPoint.Y));
			double funcMax = functionalHole.Members.Max(member => Math.Max(member.Dimension.FirstPoint.Y, member.Dimension.SecondPoint.Y));
			double chainMin = chain.Members.Min(member => Math.Min(member.Dimension.FirstPoint.Y, member.Dimension.SecondPoint.Y));
			double chainMax = chain.Members.Max(member => Math.Max(member.Dimension.FirstPoint.Y, member.Dimension.SecondPoint.Y));
			if (funcMax > chainMax + _config.GeometryTolerance || funcMin < chainMin - _config.GeometryTolerance)
			{
				return true;
			}
		}
		return false;
	}

	private string GetOrderingReason(LayoutBlock block, IList<LayoutBlock> orderedBlocks)
	{
		if (block.ForceOuterLevel)
		{
			return "ForceOutermost";
		}
		bool usedSemanticTieBreak = orderedBlocks.Any(candidate => candidate != block
			&& Math.Abs(candidate.EffectiveSpan - block.EffectiveSpan) <= _config.GeometryTolerance
			&& candidate.ReadingLevel != block.ReadingLevel);
		return usedSemanticTieBreak ? "NearEqualSpanSemanticTieBreak" : "EffectiveSpanAscending";
	}

	private StackingLayerItem CreateStackingLayerItem(IndexedLayoutItem member, LayoutBlock block, bool isHorizontal, double textHeight)
	{
		Tuple<double, double> textInterval = ComputeTextInterval(member.Dimension, isHorizontal, textHeight);
		Tuple<double, double> arrowInterval = ComputeArrowInterval(member.Dimension, isHorizontal);
		return new StackingLayerItem
		{
			Index = member.SourceIndex,
			Dimension = member.Dimension,
			TxtA = textInterval.Item1,
			TxtB = textInterval.Item2,
			ArrA = arrowInterval.Item1,
			ArrB = arrowInterval.Item2,
			LayoutBlockId = block.Id,
			LayoutBlockType = block.Type,
			LayoutBlockMemberCount = block.Members.Count
		};
	}

	private bool HasStackingLayerConflict(StackingLayerItem candidate, StackingLayerItem existing, bool isHorizontal, double gap)
	{
		bool strictArrowConflict = HasStrictArrowConflict(candidate.ArrA, candidate.ArrB, existing.ArrA, existing.ArrB);
		return (!isHorizontal || candidate.Dimension.LooseChainId == 0 || existing.Dimension.LooseChainId == 0)
			&& (!isHorizontal || !CanIgnoreTextConflictWithLooseChain(candidate.Dimension, existing.Dimension) || strictArrowConflict)
			&& !AreCompatible(existing.TxtA, existing.TxtB, candidate.TxtA, candidate.TxtB, gap);
	}

	private List<List<StackingLayerItem>> BuildAlignmentLanes(IList<List<StackingLayerItem>> layers, double gap)
	{
		List<List<StackingLayerItem>> result = new List<List<StackingLayerItem>>();
		Dictionary<StackingLayerItem, int> levelByItem = layers.SelectMany((layer, level) => layer.Select(item => new
		{
			Item = item,
			Level = level
		})).ToDictionary(entry => entry.Item, entry => entry.Level);
		var groups = layers.SelectMany(layer => layer)
			.Where(item => !string.IsNullOrEmpty(item.Dimension.AlignmentKey)
				&& !string.Equals(item.LayoutBlockType, "RootedAlignmentLane", StringComparison.Ordinal))
			.GroupBy(item => item.Dimension.AlignmentKey, StringComparer.Ordinal)
			.OrderBy(group => group.Min(item => item.Index))
			.ToList();

		foreach (var group in groups)
		{
			List<List<StackingLayerItem>> lanes = new List<List<StackingLayerItem>>();
			foreach (StackingLayerItem item in group.OrderBy(candidate => candidate.Index))
			{
				List<List<StackingLayerItem>> list = lanes.Where((List<StackingLayerItem> candidate) => CanJoinAlignmentLane(item, candidate, levelByItem, gap)).ToList();
				List<StackingLayerItem> lane;
				if (list.Count == 0)
				{
					lane = new List<StackingLayerItem>();
					lanes.Add(lane);
				}
				else
				{
					lane = list[0];
					foreach (List<StackingLayerItem> item2 in list.Skip(1).Where((List<StackingLayerItem> candidate) => CanMergeAlignmentLanes(lane, candidate, levelByItem, gap)).ToList())
					{
						lane.AddRange(item2);
						lanes.Remove(item2);
					}
				}
				lane.Add(item);
			}
			lanes = lanes
				.OrderByDescending(lane => lane.Count)
				.ThenBy(lane => lane.Min(item => item.Index))
				.ToList();
			for (int i = 0; i < lanes.Count; i++)
			{
				foreach (StackingLayerItem item3 in lanes[i])
				{
					item3.AlignmentLaneKey = group.Key + "#" + (i + 1).ToString();
				}
			}
			result.AddRange(lanes);
		}
		return result;
	}

	private bool CanJoinAlignmentLane(StackingLayerItem item, IList<StackingLayerItem> lane, IDictionary<StackingLayerItem, int> levelByItem, double gap)
	{
		if (lane.Any(existing => existing.Dimension.PreserveAlignmentLevel != item.Dimension.PreserveAlignmentLevel))
		{
			return false;
		}
		if (lane.Any(existing => HasStrictArrowConflict(item.ArrA, item.ArrB, existing.ArrA, existing.ArrB)))
		{
			return false;
		}
		// PreserveAlignmentLevel lanes (functional holes) are intentionally excluded from
		// MoveAlignmentGroupsTogether so V198 stacking levels stay put. A member can still be
		// bumped outward alone by an arrow conflict; the shared AlignmentKey must keep the lane
		// together across those level differences and without requiring a shared arrow endpoint.
		if (item.Dimension.PreserveAlignmentLevel)
		{
			return lane.All(existing => AreCompatible(item.TxtA, item.TxtB, existing.TxtA, existing.TxtB, gap));
		}
		return lane.Any(existing => SharesArrowEndpoint(item, existing));
	}

	private bool CanMergeAlignmentLanes(IList<StackingLayerItem> first, IList<StackingLayerItem> second, IDictionary<StackingLayerItem, int> levelByItem, double gap)
	{
		if (first.Any(item => item.Dimension.PreserveAlignmentLevel != second[0].Dimension.PreserveAlignmentLevel))
		{
			return false;
		}
		if (first.Any((StackingLayerItem item) => second.Any((StackingLayerItem candidate) => HasStrictArrowConflict(item.ArrA, item.ArrB, candidate.ArrA, candidate.ArrB))))
		{
			return false;
		}
		if (first[0].Dimension.PreserveAlignmentLevel)
		{
			return first.All(item => second.All(candidate => AreCompatible(item.TxtA, item.TxtB, candidate.TxtA, candidate.TxtB, gap)));
		}
		return true;
	}

	private bool SharesArrowEndpoint(StackingLayerItem first, StackingLayerItem second)
	{
		return SharesArrowEndpoint(first.ArrA, first.ArrB, second.ArrA, second.ArrB);
	}

	private bool SharesArrowEndpoint(double firstA, double firstB, double secondA, double secondB)
	{
		return Math.Abs(firstA - secondA) <= _config.GeometryTolerance
			|| Math.Abs(firstA - secondB) <= _config.GeometryTolerance
			|| Math.Abs(firstB - secondA) <= _config.GeometryTolerance
			|| Math.Abs(firstB - secondB) <= _config.GeometryTolerance;
	}

	private void MoveAlignmentGroupsTogether(List<List<StackingLayerItem>> layers, IList<List<StackingLayerItem>> alignmentLanes, DimensionSide side, OutlineFeature2D outline, double textHeight, double gap, double firstOffset, double perLevelSpacing, bool isHorizontal)
	{
		foreach (List<StackingLayerItem> members in alignmentLanes.OrderBy(lane => lane.Min(item => item.Index)))
		{
			bool alreadyPlacedAsLayoutBlock = members.Count > 1
				&& members[0].LayoutBlockMemberCount > 1
				&& members.All(item => string.Equals(item.LayoutBlockId, members[0].LayoutBlockId, StringComparison.Ordinal));
			if (members.Count < 2 || members.All(item => item.Dimension.PreserveAlignmentLevel) || alreadyPlacedAsLayoutBlock)
			{
				continue;
			}
			StackingLayerItem anchor = members
				.OrderByDescending(item => item.Dimension.AlignmentPriority)
				.ThenBy(item => item.Dimension.Span)
				.ThenBy(item => item.Index)
				.First();
			int targetLevel = layers.SelectMany((layer, level) => layer
				.Where(item => members.Contains(item))
				.Select(item => level))
				.DefaultIfEmpty(0)
				.Max();
			foreach (List<StackingLayerItem> layer in layers)
			{
				layer.RemoveAll(item => members.Contains(item));
			}

			for (int level = 0; level < layers.Count; level++)
			{
				foreach (StackingLayerItem existing in layers[level])
				{
					if (members.Any(member => HasStrictArrowConflict(member.ArrA, member.ArrB, existing.ArrA, existing.ArrB)))
					{
						targetLevel = Math.Max(targetLevel, level + 1);
					}
				}
			}

			while (true)
			{
				while (layers.Count <= targetLevel)
				{
					layers.Add(new List<StackingLayerItem>());
				}
				double offset = firstOffset + (double)targetLevel * perLevelSpacing;
				bool coversOutline = members.Any(member => TextCoversOutline(member.Dimension, side, offset, textHeight, outline, isHorizontal));
				bool conflictsAtLevel = layers[targetLevel].Any(existing => members.Any(member =>
					HasStrictArrowConflict(member.ArrA, member.ArrB, existing.ArrA, existing.ArrB)
					|| !AreCompatible(existing.TxtA, existing.TxtB, member.TxtA, member.TxtB, gap)));
				bool physicalConflict = HasPhysicalAlignmentGroupConflict(members, anchor, targetLevel, layers, side, outline, textHeight, gap, firstOffset, perLevelSpacing);
				if (!coversOutline && !conflictsAtLevel && !physicalConflict)
				{
					break;
				}
				targetLevel++;
			}

			layers[targetLevel].AddRange(members);
		}
	}

	private bool HasPhysicalAlignmentGroupConflict(IList<StackingLayerItem> members, StackingLayerItem anchor, int targetLevel, IList<List<StackingLayerItem>> layers, DimensionSide side, OutlineFeature2D outline, double textHeight, double gap, double firstOffset, double perLevelSpacing)
	{
		double targetOffset = firstOffset + (double)targetLevel * perLevelSpacing;
		double targetCoordinate = GetDimLineCoordinate(anchor.Dimension, side, outline, targetOffset);
		double minimumSeparation = Math.Max(Math.Abs(perLevelSpacing), textHeight + gap);
		for (int level = 0; level < layers.Count; level++)
		{
			foreach (StackingLayerItem existing in layers[level])
			{
				double existingCoordinate = GetResolvedStackingCoordinate(existing, level, layers, side, outline, firstOffset, perLevelSpacing);
				if (Math.Abs(targetCoordinate - existingCoordinate) >= minimumSeparation - _config.GeometryTolerance)
				{
					continue;
				}
				if (members.Any(member => HasStrictArrowConflict(member.ArrA, member.ArrB, existing.ArrA, existing.ArrB)
					|| !AreCompatible(existing.TxtA, existing.TxtB, member.TxtA, member.TxtB, gap)))
				{
					return true;
				}
			}
		}
		return false;
	}

	private double GetResolvedStackingCoordinate(StackingLayerItem item, int itemLevel, IList<List<StackingLayerItem>> layers, DimensionSide side, OutlineFeature2D outline, double firstOffset, double perLevelSpacing)
	{
		if (string.IsNullOrEmpty(item.AlignmentLaneKey))
		{
			return GetDimLineCoordinate(item.Dimension, side, outline, firstOffset + (double)itemLevel * perLevelSpacing);
		}
		var alignedItems = layers.SelectMany((layer, level) => layer
			.Where(candidate => string.Equals(candidate.AlignmentLaneKey, item.AlignmentLaneKey, StringComparison.Ordinal))
			.Select(candidate => new { Item = candidate, Level = level }))
			.ToList();
		if (alignedItems.Count == 0)
		{
			return GetDimLineCoordinate(item.Dimension, side, outline, firstOffset + (double)itemLevel * perLevelSpacing);
		}
		var anchor = alignedItems
			.OrderByDescending(entry => entry.Item.Dimension.AlignmentPriority)
			.ThenBy(entry => entry.Item.Dimension.Span)
			.ThenBy(entry => entry.Item.Index)
			.First();
		return GetDimLineCoordinate(anchor.Item.Dimension, side, outline, firstOffset + (double)anchor.Level * perLevelSpacing);
	}

	private void ApplyAlignmentCoordinateOverrides(IList<DimensionStackingPlacement> placements, IList<DimensionLayoutItem> dimensions, IList<List<StackingLayerItem>> alignmentLanes, DimensionSide side, OutlineFeature2D outline)
	{
		Dictionary<int, DimensionStackingPlacement> placementByIndex = placements
			.Where(placement => placement.Index >= 0 && placement.Index < dimensions.Count)
			.ToDictionary(placement => placement.Index);
		foreach (List<StackingLayerItem> lane in alignmentLanes)
		{
			List<DimensionStackingPlacement> group = lane
				.Where(item => placementByIndex.ContainsKey(item.Index))
				.Select(item => placementByIndex[item.Index])
				.ToList();
			if (group.Count == 0)
			{
				continue;
			}
			string alignmentLaneKey = lane.Select(item => item.AlignmentLaneKey).FirstOrDefault(key => !string.IsNullOrEmpty(key)) ?? string.Empty;
			foreach (DimensionStackingPlacement placement in group)
			{
				placement.AlignmentLaneKey = alignmentLaneKey;
				placement.AlignmentLaneMemberCount = group.Count;
			}
			bool preservesLevel = lane.All(item => item.Dimension.PreserveAlignmentLevel);
			if (preservesLevel && group.Count < 2)
			{
				continue;
			}
			// Preserve-level members may sit on different stacking levels after an asymmetric
			// conflict bump. Anchor on the OUTERMOST member so unifying the lane never pulls a
			// promoted dimension back inward on top of the conflict.
			DimensionStackingPlacement anchor = preservesLevel
				? group
					.OrderByDescending(placement => GetPlacementPhysicalOutwardRank(placement, dimensions, side, outline))
					.ThenByDescending(placement => dimensions[placement.Index].AlignmentPriority)
					.ThenBy(placement => dimensions[placement.Index].Span)
					.ThenBy(placement => placement.Index)
					.First()
				: group
					.OrderByDescending(placement => dimensions[placement.Index].AlignmentPriority)
					.ThenBy(placement => dimensions[placement.Index].Span)
					.ThenBy(placement => placement.Index)
					.First();
			double coordinate = GetDimLineCoordinate(dimensions[anchor.Index], side, outline, anchor.Offset);
			if (preservesLevel && lane.Any(item => DimensionLineEntersOutlineInterior(item.Dimension, side, coordinate, outline)))
			{
				continue;
			}
			foreach (DimensionStackingPlacement placement in group)
			{
				placement.DimLineCoordinateOverride = coordinate;
			}
		}
	}

	private void ApplyRootedLayoutBlockCoordinateOverrides(IList<DimensionStackingPlacement> placements, IList<DimensionLayoutItem> dimensions, DimensionSide side, OutlineFeature2D outline)
	{
		foreach (IGrouping<string, DimensionStackingPlacement> block in placements
			.Where(placement => string.Equals(placement.LayoutBlockType, "RootedAlignmentLane", StringComparison.Ordinal))
			.GroupBy(placement => placement.LayoutBlockId, StringComparer.Ordinal))
		{
			List<DimensionStackingPlacement> members = block.Where(placement => placement.Index >= 0 && placement.Index < dimensions.Count).ToList();
			if (members.Count == 0)
			{
				continue;
			}
			string alignmentLaneKey = block.Key.StartsWith("AlignmentLane:", StringComparison.Ordinal)
				? block.Key.Substring("AlignmentLane:".Length)
				: (dimensions[members[0].Index].AlignmentKey ?? string.Empty);
			foreach (DimensionStackingPlacement placement in members)
			{
				placement.AlignmentLaneKey = alignmentLaneKey;
				placement.AlignmentLaneMemberCount = members.Count;
			}
			DimensionStackingPlacement anchor = members
				.OrderByDescending(placement => dimensions[placement.Index].AlignmentPriority)
				.ThenBy(placement => dimensions[placement.Index].Span)
				.ThenBy(placement => placement.Index)
				.First();
			double coordinate = GetDimLineCoordinate(dimensions[anchor.Index], side, outline, anchor.Offset);
			if (members.Any(placement => DimensionLineEntersOutlineInterior(dimensions[placement.Index], side, coordinate, outline)))
			{
				continue;
			}
			foreach (DimensionStackingPlacement placement in members)
			{
				placement.DimLineCoordinateOverride = coordinate;
			}
		}
	}

	private void EnsureOverallPhysicalOutermostOffset(IList<DimensionStackingPlacement> placements, IList<DimensionLayoutItem> dimensions, DimensionSide side, OutlineFeature2D outline, double perLevelSpacing)
	{
		if (outline == null || placements == null || placements.Count < 2)
		{
			return;
		}
		double clearance = Math.Max(Math.Abs(perLevelSpacing), _config.GeometryTolerance);
		foreach (DimensionStackingPlacement placement in placements)
		{
			if (placement.Index < 0 || placement.Index >= dimensions.Count)
			{
				continue;
			}
			DimensionLayoutItem dimensionLayoutItem = dimensions[placement.Index];
			if (!dimensionLayoutItem.ForceOuterLevel || (dimensionLayoutItem.Kind != DimensionKind.OverallWidth && dimensionLayoutItem.Kind != DimensionKind.OverallHeight))
			{
				continue;
			}
			List<double> list = new List<double>();
			foreach (DimensionStackingPlacement item in placements)
			{
				if (item != placement && item.Index >= 0 && item.Index < dimensions.Count)
				{
					double dimLineCoordinate = item.DimLineCoordinateOverride ?? GetDimLineCoordinate(dimensions[item.Index], side, outline, item.Offset);
					if (!double.IsNaN(dimLineCoordinate) && !double.IsInfinity(dimLineCoordinate))
					{
						list.Add(dimLineCoordinate);
					}
				}
			}
			if (list.Count == 0)
			{
				continue;
			}
			double val = side switch
			{
				DimensionSide.Bottom => outline.MinY - list.Min() + clearance,
				DimensionSide.Top => list.Max() - outline.MaxY + clearance,
				DimensionSide.Left => outline.MinX - list.Min() + clearance,
				DimensionSide.Right => list.Max() - outline.MaxX + clearance,
				_ => placement.Offset,
			};
			placement.Offset = Math.Max(clearance, val);
		}
	}

	private void EnsureLayoutBlockPhysicalOutwardOrder(IList<DimensionStackingPlacement> placements, IList<DimensionLayoutItem> dimensions, IList<LayoutBlock> layoutBlocks, DimensionSide side, OutlineFeature2D outline, double perLevelSpacing, bool isHorizontal)
	{
		if (placements == null || dimensions == null || layoutBlocks == null || placements.Count < 2)
		{
			return;
		}
		Dictionary<string, List<DimensionStackingPlacement>> placementsByBlock = placements
			.GroupBy(placement => placement.LayoutBlockId, StringComparer.Ordinal)
			.ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
		double clearance = Math.Max(Math.Abs(perLevelSpacing), _config.GeometryTolerance);
		for (int outerIndex = 0; outerIndex < layoutBlocks.Count; outerIndex++)
		{
			LayoutBlock outer = layoutBlocks[outerIndex];
			if (outer.ForceOuterLevel || !placementsByBlock.TryGetValue(outer.Id, out var outerPlacements))
			{
				continue;
			}
			double requiredMinimumRank = double.NegativeInfinity;
			string promotedBy = string.Empty;
			for (int innerIndex = 0; innerIndex < outerIndex; innerIndex++)
			{
				LayoutBlock inner = layoutBlocks[innerIndex];
				if (!placementsByBlock.TryGetValue(inner.Id, out var innerPlacements) || !RequiresPhysicalOutwardOrder(inner, outer, isHorizontal))
				{
					continue;
				}
				double innerMaximumRank = innerPlacements.Max(placement => GetPlacementPhysicalOutwardRank(placement, dimensions, side, outline));
				double requiredRank = innerMaximumRank + clearance;
				if (requiredRank > requiredMinimumRank)
				{
					requiredMinimumRank = requiredRank;
					promotedBy = inner.Id;
				}
			}
			if (double.IsNegativeInfinity(requiredMinimumRank))
			{
				continue;
			}
			double outerMinimumRank = outerPlacements.Min(placement => GetPlacementPhysicalOutwardRank(placement, dimensions, side, outline));
			if (outerMinimumRank >= requiredMinimumRank - _config.GeometryTolerance)
			{
				continue;
			}
			double outwardShift = requiredMinimumRank - outerMinimumRank;
			foreach (DimensionStackingPlacement placement in outerPlacements)
			{
				double adjustedRank = GetPlacementPhysicalOutwardRank(placement, dimensions, side, outline) + outwardShift;
				double coordinate = (side == DimensionSide.Bottom || side == DimensionSide.Left) ? (0.0 - adjustedRank) : adjustedRank;
				placement.DimLineCoordinateOverride = coordinate;
			}
			outer.PromotedByConflictWith = promotedBy;
		}
	}

	/// <summary>
	/// Records which alignment lanes actually share one dimension-line coordinate BEFORE the
	/// outward-order pass runs. Lanes that were deliberately left unaligned - a preserve-level
	/// lane with a single member, or one whose shared coordinate would enter the outline
	/// interior - must not be forced together afterwards.
	/// </summary>
	private HashSet<string> CaptureUnifiedAlignmentLaneKeys(IList<DimensionStackingPlacement> placements, IList<DimensionLayoutItem> dimensions, DimensionSide side, OutlineFeature2D outline)
	{
		HashSet<string> unifiedLaneKeys = new HashSet<string>(StringComparer.Ordinal);
		foreach (IGrouping<string, DimensionStackingPlacement> lane in placements
			.Where(placement => !string.IsNullOrEmpty(placement.AlignmentLaneKey))
			.GroupBy(placement => placement.AlignmentLaneKey, StringComparer.Ordinal))
		{
			List<DimensionStackingPlacement> members = lane.ToList();
			if (members.Count < 2)
			{
				continue;
			}
			double firstRank = GetPlacementPhysicalOutwardRank(members[0], dimensions, side, outline);
			if (members.All(placement => Math.Abs(GetPlacementPhysicalOutwardRank(placement, dimensions, side, outline) - firstRank) <= _config.GeometryTolerance))
			{
				unifiedLaneKeys.Add(lane.Key);
			}
		}
		return unifiedLaneKeys;
	}

	/// <summary>
	/// EnsureLayoutBlockPhysicalOutwardOrder moves whole LAYOUT BLOCKS, but an alignment lane can
	/// span several blocks (PreserveAlignmentLevel members become individual SingleDimension
	/// blocks). Promoting one member therefore breaks the lane, and no later pass restores it.
	/// Lanes that were unified beforehand are re-unified at their outermost member, which keeps
	/// the promotion that caused the split while honouring the AGENTS.md rule that
	/// functional-hole dimensions stay with their owning pin group.
	/// </summary>
	private void RestoreSplitAlignmentLanes(IList<DimensionStackingPlacement> placements, IList<DimensionLayoutItem> dimensions, DimensionSide side, OutlineFeature2D outline, HashSet<string> unifiedLaneKeys)
	{
		if (unifiedLaneKeys == null || unifiedLaneKeys.Count == 0)
		{
			return;
		}
		foreach (IGrouping<string, DimensionStackingPlacement> lane in placements
			.Where(placement => !string.IsNullOrEmpty(placement.AlignmentLaneKey) && unifiedLaneKeys.Contains(placement.AlignmentLaneKey))
			.GroupBy(placement => placement.AlignmentLaneKey, StringComparer.Ordinal))
		{
			List<DimensionStackingPlacement> members = lane.ToList();
			if (members.Count < 2)
			{
				continue;
			}
			double maximumRank = members.Max(placement => GetPlacementPhysicalOutwardRank(placement, dimensions, side, outline));
			double minimumRank = members.Min(placement => GetPlacementPhysicalOutwardRank(placement, dimensions, side, outline));
			if (maximumRank - minimumRank <= _config.GeometryTolerance)
			{
				continue;
			}
			double coordinate = (side == DimensionSide.Bottom || side == DimensionSide.Left) ? (0.0 - maximumRank) : maximumRank;
			string promotedBy = members
				.Select(placement => placement.PromotedByConflictWith)
				.FirstOrDefault(value => !string.IsNullOrEmpty(value)) ?? string.Empty;
			foreach (DimensionStackingPlacement placement in members)
			{
				placement.DimLineCoordinateOverride = coordinate;
				if (string.IsNullOrEmpty(placement.PromotedByConflictWith))
				{
					placement.PromotedByConflictWith = promotedBy;
				}
			}
		}
	}

	private double GetPlacementPhysicalOutwardRank(DimensionStackingPlacement placement, IList<DimensionLayoutItem> dimensions, DimensionSide side, OutlineFeature2D outline)
	{
		DimensionLayoutItem dimension = dimensions[placement.Index];
		double coordinate = placement.DimLineCoordinateOverride ?? GetDimLineCoordinate(dimension, side, outline, placement.Offset);
		return GetPhysicalOutwardRank(side, coordinate);
	}

	private static void PromoteOverallDimensionsToOutermostLayer(List<List<StackingLayerItem>> layers)
	{
		List<StackingLayerItem> list = new List<StackingLayerItem>();
		for (int i = 0; i < layers.Count; i++)
		{
			for (int num = layers[i].Count - 1; num >= 0; num--)
			{
				StackingLayerItem stackingLayerItem = layers[i][num];
				if (stackingLayerItem.Dimension.ForceOuterLevel && (stackingLayerItem.Dimension.Kind == DimensionKind.OverallWidth || stackingLayerItem.Dimension.Kind == DimensionKind.OverallHeight))
				{
					layers[i].RemoveAt(num);
					list.Add(stackingLayerItem);
				}
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		layers.RemoveAll((List<StackingLayerItem> layer) => layer.Count == 0);
		layers.Add(list);
	}

	private void UpdatePhysicalOrderDiagnostics(IList<DimensionStackingPlacement> placements, IList<DimensionLayoutItem> dimensions, IList<LayoutBlock> layoutBlocks, DimensionSide side, OutlineFeature2D outline, bool isHorizontal)
	{
		Dictionary<string, List<DimensionStackingPlacement>> placementsByBlock = placements
			.GroupBy(placement => placement.LayoutBlockId, StringComparer.Ordinal)
			.ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
		Dictionary<string, Tuple<double, double>> physicalRanks = new Dictionary<string, Tuple<double, double>>(StringComparer.Ordinal);
		bool physicalOrderValidated = true;
		foreach (DimensionStackingPlacement placement in placements)
		{
			if (placement.Index < 0 || placement.Index >= dimensions.Count)
			{
				physicalOrderValidated = false;
				continue;
			}
			DimensionLayoutItem dimension = dimensions[placement.Index];
			double coordinate = placement.DimLineCoordinateOverride ?? GetDimLineCoordinate(dimension, side, outline, placement.Offset);
			placement.PhysicalOutwardDistance = GetPhysicalOutwardDistance(dimension, side, outline, coordinate, placement.Offset);
			if (double.IsNaN(coordinate) || double.IsInfinity(coordinate) || placement.PhysicalOutwardDistance < -_config.GeometryTolerance)
			{
				physicalOrderValidated = false;
			}
		}
		foreach (IGrouping<string, DimensionStackingPlacement> group in placements.GroupBy(placement => placement.LayoutBlockId, StringComparer.Ordinal))
		{
			List<double> ranks = group.Select(placement =>
			{
				DimensionLayoutItem dimension = dimensions[placement.Index];
				double coordinate = placement.DimLineCoordinateOverride ?? GetDimLineCoordinate(dimension, side, outline, placement.Offset);
				return GetPhysicalOutwardRank(side, coordinate);
			}).ToList();
			physicalRanks[group.Key] = Tuple.Create(ranks.Min(), ranks.Max());
		}
		foreach (LayoutBlock block in layoutBlocks.Where(candidate => candidate.Members.Count > 1))
		{
			if (!placementsByBlock.TryGetValue(block.Id, out var blockPlacements))
			{
				continue;
			}
			if (blockPlacements.Select(placement => placement.Level).Distinct().Count() != 1
				|| (string.Equals(block.Type, "RootedAlignmentLane", StringComparison.Ordinal)
					&& physicalRanks[block.Id].Item2 - physicalRanks[block.Id].Item1 > _config.GeometryTolerance))
			{
				physicalOrderValidated = false;
			}
		}
		for (int outerIndex = 0; outerIndex < layoutBlocks.Count; outerIndex++)
		{
			LayoutBlock outer = layoutBlocks[outerIndex];
			if (!placementsByBlock.TryGetValue(outer.Id, out var outerPlacements))
			{
				continue;
			}
			for (int innerIndex = 0; innerIndex < outerIndex; innerIndex++)
			{
				LayoutBlock inner = layoutBlocks[innerIndex];
				if (!placementsByBlock.TryGetValue(inner.Id, out var innerPlacements) || !RequiresPhysicalOutwardOrder(inner, outer, isHorizontal))
				{
					continue;
				}
				if (physicalRanks[inner.Id].Item2 >= physicalRanks[outer.Id].Item1 - _config.GeometryTolerance)
				{
					physicalOrderValidated = false;
				}
				if (string.IsNullOrEmpty(outer.PromotedByConflictWith)
					&& outerPlacements.Min(placement => placement.Level) > innerPlacements.Max(placement => placement.Level))
				{
					outer.PromotedByConflictWith = inner.Id;
				}
			}
		}
		foreach (DimensionStackingPlacement placement in placements)
		{
			placement.PromotedByConflictWith = layoutBlocks.First(block => string.Equals(block.Id, placement.LayoutBlockId, StringComparison.Ordinal)).PromotedByConflictWith ?? string.Empty;
			placement.PhysicalOrderValidated = physicalOrderValidated;
		}
	}

	private bool RequiresPhysicalOutwardOrder(LayoutBlock inner, LayoutBlock outer, bool isHorizontal)
	{
		if (outer.ForceOuterLevel)
		{
			return true;
		}
		if (Math.Abs(inner.EffectiveSpan - outer.EffectiveSpan) <= _config.GeometryTolerance)
		{
			return false;
		}
		foreach (IndexedLayoutItem innerMember in inner.Members)
		{
			Tuple<double, double> innerInterval = ComputeArrowInterval(innerMember.Dimension, isHorizontal);
			foreach (IndexedLayoutItem outerMember in outer.Members)
			{
				Tuple<double, double> outerInterval = ComputeArrowInterval(outerMember.Dimension, isHorizontal);
				if (HasStrictArrowConflict(innerInterval.Item1, innerInterval.Item2, outerInterval.Item1, outerInterval.Item2)
					|| (HasSameSourceFeature(innerMember.Dimension, outerMember.Dimension)
						&& SharesArrowEndpoint(innerInterval.Item1, innerInterval.Item2, outerInterval.Item1, outerInterval.Item2)))
				{
					return true;
				}
			}
		}
		return false;
	}

	private double GetPhysicalOutwardDistance(DimensionLayoutItem dimension, DimensionSide side, OutlineFeature2D outline, double coordinate, double fallbackOffset)
	{
		double boundary;
		if (!TryGetDimensionLocalBoundary(dimension, side, outline, out boundary))
		{
			if (outline == null)
			{
				return Math.Max(0.0, fallbackOffset);
			}
			boundary = side switch
			{
				DimensionSide.Bottom => outline.MinY,
				DimensionSide.Top => outline.MaxY,
				DimensionSide.Left => outline.MinX,
				DimensionSide.Right => outline.MaxX,
				_ => coordinate,
			};
		}
		return side switch
		{
			DimensionSide.Bottom => boundary - coordinate,
			DimensionSide.Top => coordinate - boundary,
			DimensionSide.Left => boundary - coordinate,
			DimensionSide.Right => coordinate - boundary,
			_ => 0.0,
		};
	}

	private static double GetPhysicalOutwardRank(DimensionSide side, double coordinate)
	{
		return side == DimensionSide.Bottom || side == DimensionSide.Left ? -coordinate : coordinate;
	}

	public bool TryGetLocalDimLineCoordinate(DimensionLayoutItem dim, DimensionSide side, OutlineFeature2D outline, double offset, out double coordinate)
	{
		coordinate = 0.0;
		if (TryGetDimensionLocalBoundary(dim, side, outline, out var boundary))
		{
			switch (side)
			{
			case DimensionSide.Bottom:
			case DimensionSide.Left:
				coordinate = boundary - offset;
				if (!DimensionLineEntersOutlineInterior(dim, side, coordinate, outline))
				{
					return true;
				}
				break;
			case DimensionSide.Top:
			case DimensionSide.Right:
				coordinate = boundary + offset;
				if (!DimensionLineEntersOutlineInterior(dim, side, coordinate, outline))
				{
					return true;
				}
				break;
			}
		}
		return false;
	}

	public bool TryGetDimensionLocalBoundary(DimensionLayoutItem dim, DimensionSide side, OutlineFeature2D outline, out double boundary)
	{
		boundary = 0.0;
		if (!CanUseLocalDimensionBoundary(dim))
		{
			return false;
		}
		if (dim.LooseChainId != 0 && !dim.PreferLocalBoundary)
		{
			return false;
		}
		return TryGetLocalHoleLocationBoundary(dim, side, outline, out boundary);
	}

	public bool DimensionLineEntersOutlineInterior(DimensionLayoutItem dim, DimensionSide side, double coordinate, OutlineFeature2D outline)
	{
		if (outline == null)
		{
			return false;
		}
		bool flag = side == DimensionSide.Bottom || side == DimensionSide.Top;
		double num = (flag ? Math.Min(dim.FirstPoint.X, dim.SecondPoint.X) : Math.Min(dim.FirstPoint.Y, dim.SecondPoint.Y));
		double num2 = (flag ? Math.Max(dim.FirstPoint.X, dim.SecondPoint.X) : Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y));
		if (num2 - num <= _config.GeometryTolerance)
		{
			return false;
		}
		double[] array = new double[3]
		{
			num,
			(num + num2) / 2.0,
			num2
		};
		double[] array2 = array;
		foreach (double num3 in array2)
		{
			double x = (flag ? num3 : coordinate);
			double y = (flag ? coordinate : num3);
			if (_structureRules.IsPointInsideOutlineByRayCast(x, y, outline) && !IsPointOnAnyOutlineSegment(new Point2D(x, y), outline))
			{
				return true;
			}
		}
		return false;
	}

	public bool TryGetLocalHoleLocationBoundary(DimensionLayoutItem dim, DimensionSide side, OutlineFeature2D outline, out double boundary)
	{
		boundary = 0.0;
		if (outline == null || outline.Segments.Count == 0)
		{
			return false;
		}
		double tolerance = Math.Max(_config.GeometryTolerance, 0.05);
		double midX = (dim.FirstPoint.X + dim.SecondPoint.X) / 2.0;
		double midY = (dim.FirstPoint.Y + dim.SecondPoint.Y) / 2.0;
		double minX = Math.Min(dim.FirstPoint.X, dim.SecondPoint.X);
		double maxX = Math.Max(dim.FirstPoint.X, dim.SecondPoint.X);
		double minY = Math.Min(dim.FirstPoint.Y, dim.SecondPoint.Y);
		double maxY = Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y);
		if (side == DimensionSide.Left || side == DimensionSide.Right)
		{
			var anon = (from s in outline.Segments
				where s.IsVertical(tolerance)
				where s.LengthY > tolerance
				where (side == DimensionSide.Left) ? (s.MinX <= minX + tolerance) : (s.MinX >= maxX - tolerance)
				select new
				{
					Segment = s,
					Coordinate = (s.Start.X + s.End.X) / 2.0,
					Overlap = IntervalOverlap(minY, maxY, s.MinY, s.MaxY),
					CoversMid = (midY >= s.MinY - tolerance && midY <= s.MaxY + tolerance)
				} into c
				where c.Overlap > tolerance || c.CoversMid
				orderby Math.Abs(c.Coordinate - midX), c.Overlap descending, c.Segment.LengthY descending
				select c).FirstOrDefault();
			if (anon == null)
			{
				return false;
			}
			boundary = anon.Coordinate;
			return true;
		}
		if (side == DimensionSide.Bottom || side == DimensionSide.Top)
		{
			var anon2 = (from s in outline.Segments
				where s.IsHorizontal(tolerance)
				where s.LengthX > tolerance
				where (side == DimensionSide.Bottom) ? (s.MinY <= minY + tolerance) : (s.MinY >= maxY - tolerance)
				select new
				{
					Segment = s,
					Coordinate = (s.Start.Y + s.End.Y) / 2.0,
					Overlap = IntervalOverlap(minX, maxX, s.MinX, s.MaxX),
					CoversMid = (midX >= s.MinX - tolerance && midX <= s.MaxX + tolerance)
				} into c
				where c.Overlap > tolerance || c.CoversMid
				orderby Math.Abs(c.Coordinate - midY), c.Overlap descending, c.Segment.LengthX descending
				select c).FirstOrDefault();
			if (anon2 == null)
			{
				return false;
			}
			boundary = anon2.Coordinate;
			return true;
		}
		return false;
	}

	public bool CanUseLocalDimensionBoundary(DimensionLayoutItem dim)
	{
		return dim.PreferLocalBoundary || dim.Kind == DimensionKind.PinDistance || dim.Kind == DimensionKind.PinGroupDistance;
	}

	public bool CanIgnoreTextConflictWithLooseChain(DimensionLayoutItem candidate, DimensionLayoutItem existing)
	{
		return candidate.LooseChainId != 0 || existing.LooseChainId != 0;
	}

	public double IntervalOverlap(double firstMin, double firstMax, double secondMin, double secondMax)
	{
		return Math.Min(firstMax, secondMax) - Math.Max(firstMin, secondMin);
	}

	public bool AreCompatible(double firstA, double firstB, double secondA, double secondB, double gap)
	{
		return firstB + gap <= secondA || secondB + gap <= firstA;
	}

	public bool HasStrictArrowConflict(double firstA, double firstB, double secondA, double secondB)
	{
		double geometryTolerance = _config.GeometryTolerance;
		bool flag = (secondA + geometryTolerance < firstA && firstA < secondB - geometryTolerance) || (secondA + geometryTolerance < firstB && firstB < secondB - geometryTolerance);
		bool flag2 = (firstA + geometryTolerance < secondA && secondA < firstB - geometryTolerance) || (firstA + geometryTolerance < secondB && secondB < firstB - geometryTolerance);
		return flag || flag2;
	}

	public bool ExtensionLineOverlapsAtCoordinate(double coordinateA, double featureA, double lineA, double coordinateB, double featureB, double lineB)
	{
		if (Math.Abs(coordinateA - coordinateB) > _config.GeometryTolerance)
		{
			return false;
		}
		double num = Math.Min(featureA, lineA);
		double num2 = Math.Max(featureA, lineA);
		double num3 = Math.Min(featureB, lineB);
		double num4 = Math.Max(featureB, lineB);
		return num <= num4 + _config.GeometryTolerance && num3 <= num2 + _config.GeometryTolerance;
	}

	public bool HasSharedExtensionLine(DimensionLayoutItem first, DimensionLayoutItem second, DimensionSide side, OutlineFeature2D outline, double targetOffset)
	{
		if (outline == null)
		{
			return false;
		}
		if (side == DimensionSide.Bottom || side == DimensionSide.Top)
		{
			double num = ((side == DimensionSide.Bottom) ? (outline.MinY - targetOffset) : (outline.MaxY + targetOffset));
			return ExtensionLineOverlapsAtCoordinate(first.FirstPoint.X, first.FirstPoint.Y, num, second.FirstPoint.X, second.FirstPoint.Y, num) || ExtensionLineOverlapsAtCoordinate(first.FirstPoint.X, first.FirstPoint.Y, num, second.SecondPoint.X, second.SecondPoint.Y, num) || ExtensionLineOverlapsAtCoordinate(first.SecondPoint.X, first.SecondPoint.Y, num, second.FirstPoint.X, second.FirstPoint.Y, num) || ExtensionLineOverlapsAtCoordinate(first.SecondPoint.X, first.SecondPoint.Y, num, second.SecondPoint.X, second.SecondPoint.Y, num);
		}
		double num2 = ((side == DimensionSide.Left) ? (outline.MinX - targetOffset) : (outline.MaxX + targetOffset));
		return ExtensionLineOverlapsAtCoordinate(first.FirstPoint.Y, first.FirstPoint.X, num2, second.FirstPoint.Y, second.FirstPoint.X, num2) || ExtensionLineOverlapsAtCoordinate(first.FirstPoint.Y, first.FirstPoint.X, num2, second.SecondPoint.Y, second.SecondPoint.X, num2) || ExtensionLineOverlapsAtCoordinate(first.SecondPoint.Y, first.SecondPoint.X, num2, second.FirstPoint.Y, second.FirstPoint.X, num2) || ExtensionLineOverlapsAtCoordinate(first.SecondPoint.Y, first.SecondPoint.X, num2, second.SecondPoint.Y, second.SecondPoint.X, num2);
	}

	public bool ArrowEndpointTouches(Point2D featureA, Point2D dimLineA, Point2D featureB, Point2D dimLineB, bool isHorizontal)
	{
		Point2D point2D = (isHorizontal ? new Point2D(featureA.X, dimLineA.Y) : new Point2D(dimLineA.X, featureA.Y));
		Point2D other = (isHorizontal ? new Point2D(featureB.X, dimLineB.Y) : new Point2D(dimLineB.X, featureB.Y));
		return point2D.DistanceTo(other) <= _config.GeometryTolerance;
	}

	public Tuple<double, double> GetVerticalInterval(DimensionLayoutItem dim)
	{
		return Tuple.Create(Math.Min(dim.FirstPoint.Y, dim.SecondPoint.Y), Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y));
	}

	public Tuple<double, double> EstimateVerticalTextInterval(DimensionLayoutItem dim, double dimScale)
	{
		Tuple<double, double> verticalInterval = GetVerticalInterval(dim);
		double num = (verticalInterval.Item1 + verticalInterval.Item2) / 2.0;
		double num2 = GetDimensionTextLength(dim, _config.TextHeight * dimScale);
		return Tuple.Create(num - num2 / 2.0, num + num2 / 2.0);
	}

	public bool IsShortVerticalDimension(DimensionLayoutItem dim, double dimScale)
	{
		return dim.Span <= _config.TextHeight * 3.0 * dimScale;
	}

	public bool IntervalsOverlap(Tuple<double, double> first, Tuple<double, double> second, double tolerance)
	{
		return first.Item1 <= second.Item2 + tolerance && second.Item1 <= first.Item2 + tolerance;
	}

	public int ScoreVerticalSideCrowding(DimensionLayoutItem candidate, IEnumerable<DimensionLayoutItem> existingDims, double dimScale)
	{
		int num = 0;
		Tuple<double, double> verticalInterval = GetVerticalInterval(candidate);
		Tuple<double, double> first = EstimateVerticalTextInterval(candidate, dimScale);
		double num2 = (verticalInterval.Item1 + verticalInterval.Item2) / 2.0;
		bool flag = IsShortVerticalDimension(candidate, dimScale);
		foreach (DimensionLayoutItem existingDim in existingDims)
		{
			Tuple<double, double> verticalInterval2 = GetVerticalInterval(existingDim);
			Tuple<double, double> second = EstimateVerticalTextInterval(existingDim, dimScale);
			double num3 = (verticalInterval2.Item1 + verticalInterval2.Item2) / 2.0;
			int num4 = ((existingDim.Kind == DimensionKind.Normal || existingDim.Kind == DimensionKind.HoleLocation) ? 1 : 2);
			if (IntervalsOverlap(verticalInterval, verticalInterval2, _config.GeometryTolerance))
			{
				num += ((!flag && !IsShortVerticalDimension(existingDim, dimScale)) ? 1 : 2) * num4;
			}
			if (IntervalsOverlap(first, second, _config.TextHeight * dimScale * 0.8))
			{
				num += ((flag || IsShortVerticalDimension(existingDim, dimScale)) ? 4 : 2) * num4;
			}
			if (Math.Abs(num2 - num3) <= _config.TextHeight * dimScale * 2.5)
			{
				num += ((!flag) ? 1 : 2) * num4;
			}
		}
		return num;
	}

	public bool CanRebalanceVerticalLooseChainDimension(DimensionLayoutItem dim, double dimScale)
	{
		return dim.Kind == DimensionKind.HoleLocation && !dim.ForceOuterLevel;
	}

	public bool CanRebalanceVerticalHoleLocation(DimensionLayoutItem dim, double dimScale)
	{
		return dim.Kind == DimensionKind.HoleLocation && dim.LooseChainId == 0 && IsShortVerticalDimension(dim, dimScale) && !dim.ForceOuterLevel && !dim.PreferFeatureLocalPlacement && !dim.PreservePreferredSide;
	}

	public DimensionSide ChooseVerticalHoleLocationSide(DimensionLayoutItem dim, DimensionSide preferredSide, IEnumerable<DimensionLayoutItem> preferredDims, IEnumerable<DimensionLayoutItem> oppositeDims, double dimScale)
	{
		if (dim.Kind != DimensionKind.HoleLocation)
		{
			return preferredSide;
		}
		if (dim.PreferFeatureLocalPlacement || dim.PreservePreferredSide)
		{
			return preferredSide;
		}
		if (preferredSide != DimensionSide.Left && preferredSide != DimensionSide.Right)
		{
			return preferredSide;
		}
		DimensionSide result = ((preferredSide == DimensionSide.Left) ? DimensionSide.Right : DimensionSide.Left);
		int num = ScoreVerticalSideCrowding(dim, preferredDims ?? Enumerable.Empty<DimensionLayoutItem>(), dimScale);
		int num2 = ScoreVerticalSideCrowding(dim, oppositeDims ?? Enumerable.Empty<DimensionLayoutItem>(), dimScale);
		int num3 = (IsShortVerticalDimension(dim, dimScale) ? 1 : 3);
		if (num - num2 >= num3)
		{
			return result;
		}
		return preferredSide;
	}

	public List<VerticalRebalanceMove> SelectVerticalHoleLocationRebalanceMoves(IList<DimensionLayoutItem> sourceDims, IList<DimensionLayoutItem> targetDims, double dimScale)
	{
		List<VerticalRebalanceMove> list = new List<VerticalRebalanceMove>();
		if (sourceDims == null || sourceDims.Count == 0)
		{
			return list;
		}
		List<IndexedLayoutItem> list2 = sourceDims.Select((DimensionLayoutItem dim, int index) => new IndexedLayoutItem
		{
			SourceIndex = index,
			Dimension = dim
		}).ToList();
		List<IndexedLayoutItem> list3 = (targetDims ?? new DimensionLayoutItem[0]).Select((DimensionLayoutItem dim, int index) => new IndexedLayoutItem
		{
			SourceIndex = index,
			Dimension = dim
		}).ToList();
		SelectVerticalLooseChainRebalanceMoves(list2, list3, dimScale, list);
		int i;
		for (i = list2.Count - 1; i >= 0; i--)
		{
			IndexedLayoutItem indexedLayoutItem = list2[i];
			if (CanRebalanceVerticalHoleLocation(indexedLayoutItem.Dimension, dimScale))
			{
				List<DimensionLayoutItem> existingDims = (from existing in list2.Where((IndexedLayoutItem existing, int index) => index != i)
					select existing.Dimension).ToList();
				int num = ScoreVerticalSideCrowding(indexedLayoutItem.Dimension, existingDims, dimScale);
				int num2 = ScoreVerticalSideCrowding(indexedLayoutItem.Dimension, list3.Select((IndexedLayoutItem existing) => existing.Dimension), dimScale);
				int num3 = (IsShortVerticalDimension(indexedLayoutItem.Dimension, dimScale) ? 1 : 3);
				if (num - num2 >= num3)
				{
					list2.RemoveAt(i);
					list3.Add(indexedLayoutItem);
					list.Add(new VerticalRebalanceMove
					{
						SourceIndex = indexedLayoutItem.SourceIndex
					});
				}
			}
		}
		return list.OrderByDescending((VerticalRebalanceMove move) => move.SourceIndex).ToList();
	}

	public TextBounds2D ComputePlacedTextBounds(DimensionLayoutItem dim, Point2D dimLinePoint, bool isHorizontal, double textHeight)
	{
		Tuple<double, double> tuple = ComputeTextInterval(dim, isHorizontal, textHeight);
		double num = textHeight * 0.65;
		if (isHorizontal)
		{
			return new TextBounds2D
			{
				MinX = tuple.Item1,
				MaxX = tuple.Item2,
				MinY = dimLinePoint.Y - num,
				MaxY = dimLinePoint.Y + num
			};
		}
		return new TextBounds2D
		{
			MinX = dimLinePoint.X - num,
			MaxX = dimLinePoint.X + num,
			MinY = tuple.Item1,
			MaxY = tuple.Item2
		};
	}

	public List<DimensionTextSlidePlacement> SelectShortLocalDimensionTextSlides(IList<DimensionTextPlacementItem> placedDimensions, IEnumerable<TextBounds2D> textObstacles, double textHeight, double arrowSize, double clearance)
	{
		List<DimensionTextSlidePlacement> list = new List<DimensionTextSlidePlacement>();
		if (placedDimensions == null || placedDimensions.Count == 0)
		{
			return list;
		}
		List<TextBounds2D> currentBounds = placedDimensions.Select((DimensionTextPlacementItem item) => CloneTextBounds(item.TextBounds)).ToList();
		List<TextBounds2D> obstacles = (textObstacles ?? Enumerable.Empty<TextBounds2D>()).Select(CloneTextBounds).ToList();
		int i;
		for (i = 0; i < placedDimensions.Count; i++)
		{
			DimensionTextPlacementItem placed = placedDimensions[i];
			if (!CanSlideShortLocalDimensionText(placed))
			{
				continue;
			}
			double safeArrowSize = Math.Max(0.0, arrowSize);
			double safeClearance = Math.Max(_config.GeometryTolerance, clearance);
			if (DimensionTextFitsBetweenOwnExtensionLines(placed.Dimension, textHeight))
			{
				list.Add(new DimensionTextSlidePlacement
				{
					Index = i,
					TextPosition = GetCenteredDimensionTextPosition(placed),
					TextBounds = currentBounds[i]
				});
				continue;
			}
			double effectiveDimScale = (_config.TextHeight > _config.GeometryTolerance) ? (textHeight / _config.TextHeight) : 1.0;
			TextSlideCandidate textSlideCandidate = (from candidate in GetShortDimensionTextSlideCandidates(placed, textHeight, safeArrowSize, safeClearance)
				select new TextSlideCandidate
				{
					Position = candidate.Position,
					Bounds = candidate.Bounds,
					Score = ScoreTextBoundsAgainstPlaced(candidate.Bounds, currentBounds, obstacles, i, safeClearance, effectiveDimScale)
						+ ScoreTextBoundsAgainstArrows(candidate.Bounds, placedDimensions, safeArrowSize, safeClearance)
				} into candidate
				orderby candidate.Score, IsTopTenPlusMinusDatumHoleLocation(placed) && candidate.Position.X > (placed.Dimension.FirstPoint.X + placed.Dimension.SecondPoint.X) / 2.0 ? 1 : 0, GetTextSlideDistance(candidate.Position, placed)
				select candidate).FirstOrDefault();
			if (textSlideCandidate != null)
			{
				currentBounds[i] = textSlideCandidate.Bounds;
				list.Add(new DimensionTextSlidePlacement
				{
					Index = i,
					TextPosition = textSlideCandidate.Position,
					TextBounds = textSlideCandidate.Bounds
				});
			}
		}
		return list;
	}

	private static Point2D GetCenteredDimensionTextPosition(DimensionTextPlacementItem placed)
	{
		bool horizontal = placed.Side == DimensionSide.Bottom || placed.Side == DimensionSide.Top;
		return horizontal
			? new Point2D((placed.Dimension.FirstPoint.X + placed.Dimension.SecondPoint.X) / 2.0, placed.DimLinePoint.Y)
			: new Point2D(placed.DimLinePoint.X, (placed.Dimension.FirstPoint.Y + placed.Dimension.SecondPoint.Y) / 2.0);
	}

	public List<DimensionTextSlidePlacement> SelectVerticalHoleLocationTextSlides(IList<DimensionTextPlacementItem> placedDimensions, IEnumerable<TextBounds2D> textObstacles, double textHeight, double gap, double dimScale)
	{
		return SelectShortLocalDimensionTextSlides(placedDimensions, textObstacles, textHeight, 0.0, gap);
	}

	public bool CanSlideShortLocalDimensionText(DimensionTextPlacementItem placed)
	{
		if (placed == null || placed.Dimension == null || placed.Dimension.ForceOuterLevel)
		{
			return false;
		}
		if (placed.Side != DimensionSide.Bottom && placed.Side != DimensionSide.Top && placed.Side != DimensionSide.Left && placed.Side != DimensionSide.Right)
		{
			return false;
		}
		DimensionKind kind = placed.Dimension.Kind;
		return IsTopTenPlusMinusDatumHoleLocation(placed)
			|| kind == DimensionKind.HoleLocation || kind == DimensionKind.PinDistance || kind == DimensionKind.PinGroupDistance;
	}

	private bool IsTopTenPlusMinusDatumHoleLocation(DimensionTextPlacementItem placed)
	{
		DimensionLayoutItem dimension = placed.Dimension;
		return placed.Side == DimensionSide.Top
			&& dimension.Kind == DimensionKind.DatumHoleLocationX
			&& Math.Abs(dimension.Span - 10.0) <= _config.GeometryTolerance
			&& (dimension.OverrideText ?? string.Empty).IndexOf("±0.05", StringComparison.Ordinal) >= 0;
	}

	public bool DimensionTextFitsBetweenOwnExtensionLines(DimensionLayoutItem dim, double textHeight)
	{
		if (dim == null)
		{
			return true;
		}
		double extensionLineClearance = Math.Max(_config.GeometryTolerance, textHeight * 0.15);
		double requiredLength = GetDimensionTextLength(dim, textHeight) + extensionLineClearance * 2.0;
		return requiredLength <= Math.Max(0.0, dim.Span) + _config.GeometryTolerance;
	}

	public bool VerticalDimensionTextFitsInsideOwnLines(DimensionLayoutItem dim, double textHeight)
	{
		return DimensionTextFitsBetweenOwnExtensionLines(dim, textHeight);
	}

	public bool CanSlideVerticalHoleLocationText(DimensionTextPlacementItem placed, double dimScale)
	{
		return placed != null && (placed.Side == DimensionSide.Left || placed.Side == DimensionSide.Right) && placed.Dimension != null && placed.Dimension.Kind == DimensionKind.HoleLocation && IsShortVerticalDimension(placed.Dimension, dimScale);
	}

	public int ScoreTextBoundsAgainstPlaced(TextBounds2D candidate, IList<TextBounds2D> placedBounds, IEnumerable<TextBounds2D> textObstacles, int selfIndex, double gap, double dimScale)
	{
		int num = 0;
		if (placedBounds != null)
		{
			for (int i = 0; i < placedBounds.Count; i++)
			{
				if (i != selfIndex)
				{
					if (TextBoundsOverlap(candidate, placedBounds[i], gap))
					{
						num += 4;
					}
					if (TextBoundsAreTooClose(candidate, placedBounds[i], gap, dimScale))
					{
						num += 2;
					}
				}
			}
		}
		foreach (TextBounds2D item in textObstacles ?? Enumerable.Empty<TextBounds2D>())
		{
			if (TextBoundsOverlap(candidate, item, gap))
			{
				num += 4;
			}
			if (TextBoundsAreTooClose(candidate, item, gap, dimScale))
			{
				num += 2;
			}
		}
		return num;
	}

	private int ScoreTextBoundsAgainstArrows(TextBounds2D candidate, IEnumerable<DimensionTextPlacementItem> placedDimensions, double arrowSize, double clearance)
	{
		int score = 0;
		foreach (DimensionTextPlacementItem placed in placedDimensions ?? Enumerable.Empty<DimensionTextPlacementItem>())
		{
			foreach (TextBounds2D arrowBounds in GetArrowBounds(placed, arrowSize))
			{
				if (TextBoundsOverlap(candidate, arrowBounds, clearance))
				{
					score += 8;
				}
			}
		}
		return score;
	}

	private IEnumerable<TextBounds2D> GetArrowBounds(DimensionTextPlacementItem placed, double arrowSize)
	{
		if (placed == null || placed.Dimension == null || arrowSize <= _config.GeometryTolerance)
		{
			yield break;
		}
		double halfThickness = Math.Max(_config.GeometryTolerance, arrowSize * 0.5);
		bool horizontal = placed.Side == DimensionSide.Bottom || placed.Side == DimensionSide.Top;
		Point2D[] endpoints = { placed.Dimension.FirstPoint, placed.Dimension.SecondPoint };
		foreach (Point2D endpoint in endpoints)
		{
			if (horizontal)
			{
				yield return new TextBounds2D
				{
					MinX = endpoint.X - arrowSize,
					MaxX = endpoint.X + arrowSize,
					MinY = placed.DimLinePoint.Y - halfThickness,
					MaxY = placed.DimLinePoint.Y + halfThickness
				};
			}
			else
			{
				yield return new TextBounds2D
				{
					MinX = placed.DimLinePoint.X - halfThickness,
					MaxX = placed.DimLinePoint.X + halfThickness,
					MinY = endpoint.Y - arrowSize,
					MaxY = endpoint.Y + arrowSize
				};
			}
		}
	}

	public bool TextBoundsHasHardOverlap(TextBounds2D candidate, IList<TextBounds2D> placedBounds, IEnumerable<TextBounds2D> textObstacles, int selfIndex, double gap)
	{
		if (placedBounds != null)
		{
			for (int i = 0; i < placedBounds.Count; i++)
			{
				if (i != selfIndex && TextBoundsOverlap(candidate, placedBounds[i], gap))
				{
					return true;
				}
			}
		}
		return (textObstacles ?? Enumerable.Empty<TextBounds2D>()).Any((TextBounds2D obstacle) => TextBoundsOverlap(candidate, obstacle, gap));
	}

	public bool TextBoundsOverlap(TextBounds2D first, TextBounds2D second, double gap)
	{
		return first.MinX <= second.MaxX + gap && second.MinX <= first.MaxX + gap && first.MinY <= second.MaxY + gap && second.MinY <= first.MaxY + gap;
	}

	public bool TextBoundsAreTooClose(TextBounds2D first, TextBounds2D second, double gap, double dimScale)
	{
		if (!(first.MinY <= second.MaxY + gap) || !(second.MinY <= first.MaxY + gap))
		{
			return false;
		}
		double num = ((first.MaxX < second.MinX) ? (second.MinX - first.MaxX) : ((!(second.MaxX < first.MinX)) ? 0.0 : (first.MinX - second.MaxX)));
		return num <= Math.Max(gap, _config.TextHeight * dimScale * 1.4);
	}

	public bool HasExtensionLineTextConflict(DimensionTextPlacementItem placed, IEnumerable<TextBounds2D> textBounds)
	{
		if (placed == null || placed.Dimension == null)
		{
			return false;
		}
		foreach (TextBounds2D item in textBounds ?? Enumerable.Empty<TextBounds2D>())
		{
			if (ExtensionLineCrossesText(placed, placed.Dimension.FirstPoint, item) || ExtensionLineCrossesText(placed, placed.Dimension.SecondPoint, item))
			{
				return true;
			}
		}
		return false;
	}

	public bool ExtensionLineCrossesText(DimensionTextPlacementItem placed, Point2D featurePoint, TextBounds2D text)
	{
		double geometryTolerance = _config.GeometryTolerance;
		if (placed.Side == DimensionSide.Bottom || placed.Side == DimensionSide.Top)
		{
			double x = featurePoint.X;
			double num = Math.Min(featurePoint.Y, placed.DimLinePoint.Y);
			double num2 = Math.Max(featurePoint.Y, placed.DimLinePoint.Y);
			return x >= text.MinX - geometryTolerance && x <= text.MaxX + geometryTolerance && num <= text.MaxY + geometryTolerance && num2 >= text.MinY - geometryTolerance;
		}
		double y = featurePoint.Y;
		double num3 = Math.Min(featurePoint.X, placed.DimLinePoint.X);
		double num4 = Math.Max(featurePoint.X, placed.DimLinePoint.X);
		return y >= text.MinY - geometryTolerance && y <= text.MaxY + geometryTolerance && num3 <= text.MaxX + geometryTolerance && num4 >= text.MinX - geometryTolerance;
	}

	public List<ExtensionLineBreakRange> GetBreakRangesForExtensionLine(DimensionTextPlacementItem placed, Point2D featurePoint, IEnumerable<TextBounds2D> textBounds, double breakLength)
	{
		if (placed == null)
		{
			return new List<ExtensionLineBreakRange>();
		}
		Point2D dimPoint = GetExtensionLineEndPoint(placed, featurePoint);
		List<ExtensionLineBreakCandidate> list = (from text in textBounds ?? Enumerable.Empty<TextBounds2D>()
			where ExtensionLineCrossesText(placed, featurePoint, text)
			select ToBreakRangeCandidate(placed, featurePoint, dimPoint, text, breakLength) into range
			where range.B > range.A + _config.GeometryTolerance
			orderby range.DistanceToFeature, range.Length
			select range).ToList();
		if (list.Count == 0)
		{
			return new List<ExtensionLineBreakRange>();
		}
		ExtensionLineBreakCandidate extensionLineBreakCandidate = list[0];
		return new List<ExtensionLineBreakRange>
		{
			new ExtensionLineBreakRange
			{
				A = extensionLineBreakCandidate.A,
				B = extensionLineBreakCandidate.B
			}
		};
	}

	public Point2D GetExtensionLineEndPoint(DimensionTextPlacementItem placed, Point2D featurePoint)
	{
		if (placed.Side == DimensionSide.Bottom || placed.Side == DimensionSide.Top)
		{
			return new Point2D(featurePoint.X, placed.DimLinePoint.Y);
		}
		return new Point2D(placed.DimLinePoint.X, featurePoint.Y);
	}

	private IEnumerable<TextSlideCandidate> GetShortDimensionTextSlideCandidates(DimensionTextPlacementItem placed, double textHeight, double arrowSize, double clearance)
	{
		double textLength = GetDimensionTextLength(placed.Dimension, textHeight);
		bool horizontal = placed.Side == DimensionSide.Bottom || placed.Side == DimensionSide.Top;
		Tuple<double, double> arrow = ComputeArrowInterval(placed.Dimension, horizontal);
		double extraClearance = clearance + _config.GeometryTolerance * 2.0;
		double lowerCenter = arrow.Item1 - arrowSize - extraClearance - textLength / 2.0;
		double upperCenter = arrow.Item2 + arrowSize + extraClearance + textLength / 2.0;
		if (horizontal)
		{
			double y = placed.DimLinePoint.Y;
			yield return CreateHorizontalCustomTextCandidate(lowerCenter, y, textLength, textHeight);
			yield return CreateHorizontalCustomTextCandidate(upperCenter, y, textLength, textHeight);
			yield break;
		}
		double x = placed.DimLinePoint.X;
		yield return CreateVerticalCustomTextCandidate(x, lowerCenter, textLength, textHeight);
		yield return CreateVerticalCustomTextCandidate(x, upperCenter, textLength, textHeight);
	}

	private static double GetTextSlideDistance(Point2D position, DimensionTextPlacementItem placed)
	{
		bool horizontal = placed.Side == DimensionSide.Bottom || placed.Side == DimensionSide.Top;
		return horizontal ? Math.Abs(position.X - placed.DimLinePoint.X) : Math.Abs(position.Y - placed.DimLinePoint.Y);
	}

	private static TextSlideCandidate CreateHorizontalCustomTextCandidate(double centerX, double y, double textLength, double textHeight)
	{
		double halfHeight = textHeight * 0.65;
		return new TextSlideCandidate
		{
			Position = new Point2D(centerX, y),
			Bounds = new TextBounds2D
			{
				MinX = centerX - textLength / 2.0,
				MaxX = centerX + textLength / 2.0,
				MinY = y - halfHeight,
				MaxY = y + halfHeight
			}
		};
	}

	private static TextSlideCandidate CreateVerticalCustomTextCandidate(double x, double centerY, double textLength, double textHeight)
	{
		double halfHeight = textHeight * 0.65;
		return new TextSlideCandidate
		{
			Position = new Point2D(x, centerY),
			Bounds = new TextBounds2D
			{
				MinX = x - halfHeight,
				MaxX = x + halfHeight,
				MinY = centerY - textLength / 2.0,
				MaxY = centerY + textLength / 2.0
			}
		};
	}

	private static ExtensionLineBreakCandidate ToBreakRangeCandidate(DimensionTextPlacementItem placed, Point2D featurePoint, Point2D dimPoint, TextBounds2D text, double breakLength)
	{
		double num;
		double val;
		double val2;
		if (placed.Side == DimensionSide.Bottom || placed.Side == DimensionSide.Top)
		{
			num = featurePoint.Y;
			val = dimPoint.Y;
			val2 = (text.MinY + text.MaxY) / 2.0;
		}
		else
		{
			num = featurePoint.X;
			val = dimPoint.X;
			val2 = (text.MinX + text.MaxX) / 2.0;
		}
		double val3 = Math.Min(num, val);
		double val4 = Math.Max(num, val);
		double num2 = Math.Max(val3, Math.Min(val4, val2));
		double num3 = breakLength / 2.0;
		double num4 = Math.Max(val3, num2 - num3);
		double num5 = Math.Min(val4, num2 + num3);
		return new ExtensionLineBreakCandidate
		{
			A = num4,
			B = num5,
			DistanceToFeature = Math.Abs(num2 - num),
			Length = num5 - num4
		};
	}

	public Tuple<double, double> ComputeTextInterval(DimensionLayoutItem dim, bool isHorizontal, double textHeight)
	{
		double num;
		if (isHorizontal)
		{
			num = (dim.FirstPoint.X + dim.SecondPoint.X) / 2.0;
		}
		else
		{
			num = (dim.FirstPoint.Y + dim.SecondPoint.Y) / 2.0;
		}
		double num2 = GetDimensionTextLength(dim, textHeight);
		return Tuple.Create(num - num2 / 2.0, num + num2 / 2.0);
	}

	public double GetDimensionTextLength(DimensionLayoutItem dim, double textHeight)
	{
		string dimensionText = GetDimensionText(dim).Replace("<>", _config.FormatNumber(dim.Span));
		double baseCharacterWidth = textHeight * 0.7;
		double heightScale = 1.0;
		double currentLineWidth = 0.0;
		double maximumLineWidth = 0.0;
		for (int i = 0; i < dimensionText.Length; i++)
		{
			if (dimensionText[i] == '\\' && i + 1 < dimensionText.Length)
			{
				char control = char.ToUpperInvariant(dimensionText[i + 1]);
				if (control == 'H')
				{
					int terminator = dimensionText.IndexOf(';', i + 2);
					if (terminator >= 0)
					{
						string scaleText = dimensionText.Substring(i + 2, terminator - i - 2).TrimEnd('x', 'X');
						if (double.TryParse(scaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedScale) && parsedScale > 0.0)
						{
							heightScale = parsedScale;
						}
						i = terminator;
						continue;
					}
				}
				if (control == 'P')
				{
					maximumLineWidth = Math.Max(maximumLineWidth, currentLineWidth);
					currentLineWidth = 0.0;
					i++;
					continue;
				}
				continue;
			}
			if (dimensionText[i] == '%' && i + 2 < dimensionText.Length && dimensionText[i + 1] == '%')
			{
				currentLineWidth += baseCharacterWidth * heightScale;
				i += 2;
				continue;
			}
			if (dimensionText[i] != '{' && dimensionText[i] != '}')
			{
				currentLineWidth += baseCharacterWidth * heightScale;
			}
		}
		maximumLineWidth = Math.Max(maximumLineWidth, currentLineWidth);
		return Math.Max(baseCharacterWidth * 2.0, maximumLineWidth);
	}

	public string GetDimensionText(DimensionLayoutItem dim)
	{
		return string.IsNullOrEmpty(dim.OverrideText) ? _config.FormatNumber(dim.Span) : dim.OverrideText;
	}

	public bool TextCoversOutline(DimensionLayoutItem dim, DimensionSide side, double offset, double textHeight, OutlineFeature2D outline, bool isHorizontal)
	{
		if (outline == null)
		{
			return false;
		}
		if (TryGetDimensionLocalBoundary(dim, side, outline, out var boundary) && !IsGlobalBoundaryCoordinate(boundary, side, outline))
		{
			double coordinate = ((side == DimensionSide.Bottom || side == DimensionSide.Left) ? (boundary - offset) : (boundary + offset));
			if (!DimensionLineEntersOutlineInterior(dim, side, coordinate, outline))
			{
				return false;
			}
		}
		Tuple<double, double> tuple = ComputeTextInterval(dim, isHorizontal, textHeight);
		double num = textHeight / 2.0;
		switch (side)
		{
		case DimensionSide.Bottom:
		{
			double num3 = outline.MinY - offset;
			if (num3 + num < outline.MinY - _config.GeometryTolerance)
			{
				return false;
			}
			return tuple.Item1 < outline.MaxX + _config.GeometryTolerance && tuple.Item2 > outline.MinX - _config.GeometryTolerance;
		}
		case DimensionSide.Top:
		{
			double num5 = outline.MaxY + offset;
			if (num5 - num > outline.MaxY + _config.GeometryTolerance)
			{
				return false;
			}
			return tuple.Item1 < outline.MaxX + _config.GeometryTolerance && tuple.Item2 > outline.MinX - _config.GeometryTolerance;
		}
		case DimensionSide.Left:
		{
			double num4 = outline.MinX - offset;
			if (num4 + num < outline.MinX - _config.GeometryTolerance)
			{
				return false;
			}
			return tuple.Item1 < outline.MaxY + _config.GeometryTolerance && tuple.Item2 > outline.MinY - _config.GeometryTolerance;
		}
		case DimensionSide.Right:
		{
			double num2 = outline.MaxX + offset;
			if (num2 - num > outline.MaxX + _config.GeometryTolerance)
			{
				return false;
			}
			return tuple.Item1 < outline.MaxY + _config.GeometryTolerance && tuple.Item2 > outline.MinY - _config.GeometryTolerance;
		}
		default:
			return false;
		}
	}

	public bool IsGlobalBoundaryCoordinate(double coordinate, DimensionSide side, OutlineFeature2D outline)
	{
		if (outline == null)
		{
			return false;
		}
		return side switch
		{
			DimensionSide.Bottom => Math.Abs(coordinate - outline.MinY) <= _config.GeometryTolerance,
			DimensionSide.Top => Math.Abs(coordinate - outline.MaxY) <= _config.GeometryTolerance,
			DimensionSide.Left => Math.Abs(coordinate - outline.MinX) <= _config.GeometryTolerance,
			DimensionSide.Right => Math.Abs(coordinate - outline.MaxX) <= _config.GeometryTolerance,
			_ => false,
		};
	}

	public Point2D GetDimLinePoint(DimensionLayoutItem dim, DimensionSide side, OutlineFeature2D outline, double offset)
	{
		switch (side)
		{
		case DimensionSide.Bottom:
		case DimensionSide.Top:
			return new Point2D((dim.FirstPoint.X + dim.SecondPoint.X) / 2.0, GetDimLineCoordinate(dim, side, outline, offset));
		case DimensionSide.Left:
		case DimensionSide.Right:
			return new Point2D(GetDimLineCoordinate(dim, side, outline, offset), (dim.FirstPoint.Y + dim.SecondPoint.Y) / 2.0);
		default:
			throw new ArgumentOutOfRangeException("side");
		}
	}

	public double GetDimLineCoordinate(DimensionLayoutItem dim, DimensionSide side, OutlineFeature2D outline, double offset)
	{
		if (dim != null && dim.PreferFeatureLocalPlacement)
		{
			double featureLocalDimLineCoordinate = GetFeatureLocalDimLineCoordinate(dim, side, offset);
			if (!double.IsNaN(featureLocalDimLineCoordinate))
			{
				return featureLocalDimLineCoordinate;
			}
		}
		if (TryGetLocalDimLineCoordinate(dim, side, outline, offset, out var coordinate))
		{
			return coordinate;
		}
		if (outline == null)
		{
			return (side == DimensionSide.Bottom || side == DimensionSide.Left) ? (0.0 - offset) : offset;
		}
		return side switch
		{
			DimensionSide.Bottom => outline.MinY - offset,
			DimensionSide.Top => outline.MaxY + offset,
			DimensionSide.Left => outline.MinX - offset,
			DimensionSide.Right => outline.MaxX + offset,
			_ => throw new ArgumentOutOfRangeException("side"),
		};
	}

	private static double GetFeatureLocalDimLineCoordinate(DimensionLayoutItem dim, DimensionSide side, double offset)
	{
		return side switch
		{
			DimensionSide.Bottom => Math.Min(dim.FirstPoint.Y, dim.SecondPoint.Y) - offset,
			DimensionSide.Top => Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y) + offset,
			DimensionSide.Left => Math.Min(dim.FirstPoint.X, dim.SecondPoint.X) - offset,
			DimensionSide.Right => Math.Max(dim.FirstPoint.X, dim.SecondPoint.X) + offset,
			_ => double.NaN,
		};
	}

	private static Tuple<double, double> ComputeArrowInterval(DimensionLayoutItem dim, bool isHorizontal)
	{
		if (isHorizontal)
		{
			return Tuple.Create(Math.Min(dim.FirstPoint.X, dim.SecondPoint.X), Math.Max(dim.FirstPoint.X, dim.SecondPoint.X));
		}
		return Tuple.Create(Math.Min(dim.FirstPoint.Y, dim.SecondPoint.Y), Math.Max(dim.FirstPoint.Y, dim.SecondPoint.Y));
	}

	private static void PromoteLooseSideLevel(List<List<StackingLayerItem>> layers, int targetLevel)
	{
		while (layers.Count <= targetLevel)
		{
			layers.Add(new List<StackingLayerItem>());
		}
		for (int i = 0; i < layers.Count; i++)
		{
			if (i == targetLevel)
			{
				continue;
			}
			for (int num = layers[i].Count - 1; num >= 0; num--)
			{
				if (layers[i][num].Dimension.LooseChainId != 0)
				{
					StackingLayerItem item = layers[i][num];
					layers[i].RemoveAt(num);
					layers[targetLevel].Add(item);
				}
			}
		}
	}

	private void AlignDimensionsBySharedExtensionLines(List<List<StackingLayerItem>> layers, DimensionSide side, OutlineFeature2D outline, double textHeight, double gap, double firstOffset, double perLevelSpacing, bool isHorizontal)
	{
		for (int i = 0; i < layers.Count - 1; i++)
		{
			for (int num = layers[i].Count - 1; num >= 0; num--)
			{
				StackingLayerItem stackingLayerItem = layers[i][num];
				if (stackingLayerItem.LayoutBlockMemberCount <= 1 && stackingLayerItem.Dimension.Kind != DimensionKind.HoleLocation && !HasArrowEndpointTouch(stackingLayerItem, layers, i, num, side, outline, firstOffset, perLevelSpacing, isHorizontal))
				{
					int num2 = FindSharedExtensionAlignmentLevel(stackingLayerItem, layers, i, side, outline, textHeight, gap, firstOffset, perLevelSpacing, isHorizontal);
					if (num2 > i)
					{
						layers[i].RemoveAt(num);
						layers[num2].Add(stackingLayerItem);
					}
				}
			}
		}
	}

	private bool HasArrowEndpointTouch(StackingLayerItem candidate, List<List<StackingLayerItem>> layers, int candidateLevel, int candidateIndex, DimensionSide side, OutlineFeature2D outline, double firstOffset, double perLevelSpacing, bool isHorizontal)
	{
		double offset = firstOffset + (double)candidateLevel * perLevelSpacing;
		Point2D dimLinePoint = GetDimLinePoint(candidate.Dimension, side, outline, offset);
		for (int i = 0; i < layers.Count; i++)
		{
			double offset2 = firstOffset + (double)i * perLevelSpacing;
			for (int j = 0; j < layers[i].Count; j++)
			{
				if (i != candidateLevel || j != candidateIndex)
				{
					StackingLayerItem stackingLayerItem = layers[i][j];
					Point2D dimLinePoint2 = GetDimLinePoint(stackingLayerItem.Dimension, side, outline, offset2);
					if (ArrowEndpointTouches(candidate.Dimension.FirstPoint, dimLinePoint, stackingLayerItem.Dimension.FirstPoint, dimLinePoint2, isHorizontal) || ArrowEndpointTouches(candidate.Dimension.FirstPoint, dimLinePoint, stackingLayerItem.Dimension.SecondPoint, dimLinePoint2, isHorizontal) || ArrowEndpointTouches(candidate.Dimension.SecondPoint, dimLinePoint, stackingLayerItem.Dimension.FirstPoint, dimLinePoint2, isHorizontal) || ArrowEndpointTouches(candidate.Dimension.SecondPoint, dimLinePoint, stackingLayerItem.Dimension.SecondPoint, dimLinePoint2, isHorizontal))
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	private int FindSharedExtensionAlignmentLevel(StackingLayerItem candidate, List<List<StackingLayerItem>> layers, int sourceLevel, DimensionSide side, OutlineFeature2D outline, double textHeight, double gap, double firstOffset, double perLevelSpacing, bool isHorizontal)
	{
		for (int i = sourceLevel + 1; i < layers.Count; i++)
		{
			double num = firstOffset + (double)i * perLevelSpacing;
			if (TextCoversOutline(candidate.Dimension, side, num, textHeight, outline, isHorizontal))
			{
				continue;
			}
			bool flag = false;
			bool flag2 = true;
			foreach (StackingLayerItem item in layers[i])
			{
				if (HasSharedExtensionLine(candidate.Dimension, item.Dimension, side, outline, num))
				{
					flag = true;
				}
				if (SharesArrowEndpoint(candidate, item)
					|| HasStrictArrowConflict(candidate.ArrA, candidate.ArrB, item.ArrA, item.ArrB)
					|| !AreCompatible(item.TxtA, item.TxtB, candidate.TxtA, candidate.TxtB, gap))
				{
					flag2 = false;
					break;
				}
			}
			if (flag && flag2)
			{
				return i;
			}
		}
		return sourceLevel;
	}

	private static TextBounds2D CloneTextBounds(TextBounds2D bounds)
	{
		return new TextBounds2D
		{
			MinX = bounds.MinX,
			MaxX = bounds.MaxX,
			MinY = bounds.MinY,
			MaxY = bounds.MaxY
		};
	}

	private void SelectVerticalLooseChainRebalanceMoves(List<IndexedLayoutItem> source, List<IndexedLayoutItem> target, double dimScale, List<VerticalRebalanceMove> moves)
	{
		List<List<IndexedLayoutItem>> chains = BuildLooseLayoutChains(
			source.Where(item => item.Dimension.LooseChainId != 0 && item.Dimension.Kind == DimensionKind.HoleLocation),
			isHorizontal: false);
		foreach (List<IndexedLayoutItem> chain in chains)
		{
			if (chain.Count == 0 || chain.Any(item => !CanRebalanceVerticalLooseChainDimension(item.Dimension, dimScale)))
			{
				continue;
			}
			HashSet<int> chainSourceIndexes = new HashSet<int>(chain.Select(item => item.SourceIndex));
			List<DimensionLayoutItem> sourceWithoutChain = source
				.Where(item => !chainSourceIndexes.Contains(item.SourceIndex))
				.Select(item => item.Dimension)
				.ToList();
			int num = chain.Sum(item => ScoreVerticalSideCrowding(item.Dimension, sourceWithoutChain, dimScale));
			int num2 = chain.Sum(item => ScoreVerticalSideCrowding(item.Dimension, target.Select(existing => existing.Dimension), dimScale));
			int num3 = Math.Max(2, chain.Count);
			if (num - num2 < num3)
			{
				continue;
			}
			for (int num4 = source.Count - 1; num4 >= 0; num4--)
			{
				if (chainSourceIndexes.Contains(source[num4].SourceIndex))
				{
					IndexedLayoutItem indexedLayoutItem = source[num4];
					source.RemoveAt(num4);
					target.Add(indexedLayoutItem);
					moves.Add(new VerticalRebalanceMove
					{
						SourceIndex = indexedLayoutItem.SourceIndex
					});
				}
			}
		}
	}

	public bool IsPointOnAnyOutlineSegment(Point2D point, OutlineFeature2D outline)
	{
		return outline?.Segments.Any((Segment2D segment) => _structureRules.IsPointOnSegment(point, segment)) ?? false;
	}
}
