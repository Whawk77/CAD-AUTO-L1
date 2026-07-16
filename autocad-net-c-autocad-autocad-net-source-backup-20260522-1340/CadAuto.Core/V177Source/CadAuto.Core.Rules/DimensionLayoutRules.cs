using System;
using System.Collections.Generic;
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
		List<DimensionStackingPlacement> list = new List<DimensionStackingPlacement>();
		if (dimensions == null || dimensions.Count == 0)
		{
			return list;
		}
		List<List<StackingLayerItem>> list2 = new List<List<StackingLayerItem>>();
		Dictionary<int, int> dictionary = new Dictionary<int, int>();
		int? num = null;
		for (int i = 0; i < dimensions.Count; i++)
		{
			DimensionLayoutItem dimensionLayoutItem = dimensions[i];
			Tuple<double, double> tuple = ComputeTextInterval(dimensionLayoutItem, isHorizontal, textHeight);
			Tuple<double, double> tuple2 = ComputeArrowInterval(dimensionLayoutItem, isHorizontal);
			int num2 = 0;
			for (int j = 0; j < list2.Count; j++)
			{
				foreach (StackingLayerItem item in list2[j])
				{
					if (HasStrictArrowConflict(tuple2.Item1, tuple2.Item2, item.ArrA, item.ArrB) && j + 1 > num2)
					{
						num2 = j + 1;
					}
				}
			}
			int num3 = (dimensionLayoutItem.ForceOuterLevel ? Math.Max(num2, list2.Count) : num2);
			if (isHorizontal && !dimensionLayoutItem.ForceOuterLevel && dimensionLayoutItem.LooseChainId != 0 && dictionary.TryGetValue(dimensionLayoutItem.LooseChainId, out var value))
			{
				num3 = Math.Max(value, num2);
			}
			if (isHorizontal && !dimensionLayoutItem.ForceOuterLevel && dimensionLayoutItem.LooseChainId != 0 && num.HasValue)
			{
				num3 = Math.Max(num.Value, num2);
			}
			while (true)
			{
				double offset = firstOffset + (double)num3 * perLevelSpacing;
				if (TextCoversOutline(dimensionLayoutItem, side, offset, textHeight, outline, isHorizontal))
				{
					num3++;
					continue;
				}
				if (num3 >= list2.Count)
				{
					break;
				}
				bool flag = true;
				foreach (StackingLayerItem item2 in list2[num3])
				{
					if ((!isHorizontal || dimensionLayoutItem.LooseChainId == 0 || item2.Dimension.LooseChainId == 0) && (!isHorizontal || !CanIgnoreTextConflictWithLooseChain(dimensionLayoutItem, item2.Dimension) || HasStrictArrowConflict(tuple2.Item1, tuple2.Item2, item2.ArrA, item2.ArrB)) && !AreCompatible(item2.TxtA, item2.TxtB, tuple.Item1, tuple.Item2, gap))
					{
						flag = false;
						break;
					}
				}
				if (!flag)
				{
					num3++;
					continue;
				}
				break;
			}
			while (list2.Count <= num3)
			{
				list2.Add(new List<StackingLayerItem>());
			}
			if (isHorizontal && dimensionLayoutItem.LooseChainId != 0)
			{
				PromoteLooseSideLevel(list2, num3);
				dictionary[dimensionLayoutItem.LooseChainId] = num3;
				num = num3;
			}
			list2[num3].Add(new StackingLayerItem
			{
				Index = i,
				Dimension = dimensionLayoutItem,
				TxtA = tuple.Item1,
				TxtB = tuple.Item2,
				ArrA = tuple2.Item1,
				ArrB = tuple2.Item2
			});
		}
		AlignDimensionsBySharedExtensionLines(list2, side, outline, textHeight, gap, firstOffset, perLevelSpacing, isHorizontal);
		MoveAlignmentGroupsTogether(list2, side, outline, textHeight, gap, firstOffset, perLevelSpacing, isHorizontal);
		PromoteOverallDimensionsToOutermostLayer(list2);
		for (int k = 0; k < list2.Count; k++)
		{
			double offset2 = firstOffset + (double)k * perLevelSpacing;
			foreach (StackingLayerItem item3 in list2[k])
			{
				list.Add(new DimensionStackingPlacement
				{
					Index = item3.Index,
					Level = k,
					Offset = offset2
				});
			}
		}
		EnsureOverallPhysicalOutermostOffset(list, dimensions, side, outline, perLevelSpacing);
		ApplyAlignmentCoordinateOverrides(list, dimensions, side, outline);
		return list;
	}

	private void MoveAlignmentGroupsTogether(List<List<StackingLayerItem>> layers, DimensionSide side, OutlineFeature2D outline, double textHeight, double gap, double firstOffset, double perLevelSpacing, bool isHorizontal)
	{
		var groups = layers.SelectMany((layer, level) => layer.Select(item => new
		{
			Item = item,
			Level = level
		}))
			.Where(entry => !string.IsNullOrEmpty(entry.Item.Dimension.AlignmentKey))
			.GroupBy(entry => entry.Item.Dimension.AlignmentKey, StringComparer.Ordinal)
			.Where(group => group.Count() > 1)
			.OrderBy(group => group.Min(entry => entry.Item.Index))
			.ToList();

		foreach (var group in groups)
		{
			List<StackingLayerItem> members = group.Select(entry => entry.Item).OrderBy(item => item.Index).ToList();
			StackingLayerItem anchor = members
				.OrderByDescending(item => item.Dimension.AlignmentPriority)
				.ThenBy(item => item.Dimension.Span)
				.ThenBy(item => item.Index)
				.First();
			int targetLevel = group.Max(entry => entry.Level);
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
		if (string.IsNullOrEmpty(item.Dimension.AlignmentKey))
		{
			return GetDimLineCoordinate(item.Dimension, side, outline, firstOffset + (double)itemLevel * perLevelSpacing);
		}
		var alignedItems = layers.SelectMany((layer, level) => layer
			.Where(candidate => string.Equals(candidate.Dimension.AlignmentKey, item.Dimension.AlignmentKey, StringComparison.Ordinal))
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

	private void ApplyAlignmentCoordinateOverrides(IList<DimensionStackingPlacement> placements, IList<DimensionLayoutItem> dimensions, DimensionSide side, OutlineFeature2D outline)
	{
		var groups = placements
			.Where(placement => placement.Index >= 0 && placement.Index < dimensions.Count && !string.IsNullOrEmpty(dimensions[placement.Index].AlignmentKey))
			.GroupBy(placement => dimensions[placement.Index].AlignmentKey, StringComparer.Ordinal);

		foreach (var group in groups)
		{
			DimensionStackingPlacement anchor = group
				.OrderByDescending(placement => dimensions[placement.Index].AlignmentPriority)
				.ThenBy(placement => dimensions[placement.Index].Span)
				.ThenBy(placement => placement.Index)
				.First();
			double coordinate = GetDimLineCoordinate(dimensions[anchor.Index], side, outline, anchor.Offset);
			foreach (DimensionStackingPlacement placement in group)
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
					double dimLineCoordinate = GetDimLineCoordinate(dimensions[item.Index], side, outline, item.Offset);
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
			placement.Offset = Math.Max(placement.Offset, val);
		}
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
		if (dim.LooseChainId != 0)
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
		string dimensionText = GetDimensionText(dim);
		double num2 = (double)Math.Max(dimensionText.Length, 2) * _config.TextHeight * dimScale * 0.75;
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
		return dim.Kind == DimensionKind.HoleLocation && IsShortVerticalDimension(dim, dimScale) && !dim.ForceOuterLevel;
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

	public List<DimensionTextSlidePlacement> SelectVerticalHoleLocationTextSlides(IList<DimensionTextPlacementItem> placedDimensions, IEnumerable<TextBounds2D> textObstacles, double textHeight, double gap, double dimScale)
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
			if (!CanSlideVerticalHoleLocationText(placed, dimScale))
			{
				continue;
			}
			int num = ScoreTextBoundsAgainstPlaced(currentBounds[i], currentBounds, obstacles, i, gap, dimScale);
			bool flag = VerticalDimensionTextFitsInsideOwnLines(placed.Dimension, textHeight);
			bool flag2 = TextBoundsHasHardOverlap(currentBounds[i], currentBounds, obstacles, i, gap);
			if (!flag || flag2)
			{
				TextSlideCandidate textSlideCandidate = (from candidate in GetVerticalTextSlideCandidates(placed, textHeight, gap)
					select new TextSlideCandidate
					{
						Position = candidate.Position,
						Bounds = candidate.Bounds,
						Score = ScoreTextBoundsAgainstPlaced(candidate.Bounds, currentBounds, obstacles, i, gap, dimScale)
					} into candidate
					orderby candidate.Score, Math.Abs(candidate.Position.Y - placed.DimLinePoint.Y)
					select candidate).FirstOrDefault();
				if (textSlideCandidate != null && (!flag || textSlideCandidate.Score < num) && (flag || textSlideCandidate.Score <= num))
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
		}
		return list;
	}

	public bool VerticalDimensionTextFitsInsideOwnLines(DimensionLayoutItem dim, double textHeight)
	{
		Tuple<double, double> verticalInterval = GetVerticalInterval(dim);
		string dimensionText = GetDimensionText(dim);
		double num = (double)Math.Max(dimensionText.Length, 1) * textHeight * 1.6;
		double num2 = Math.Max(_config.GeometryTolerance, textHeight * 0.5);
		return num + num2 * 2.0 <= verticalInterval.Item2 - verticalInterval.Item1;
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

	private IEnumerable<TextSlideCandidate> GetVerticalTextSlideCandidates(DimensionTextPlacementItem placed, double textHeight, double gap)
	{
		double textLength = GetDimensionTextLength(placed.Dimension, textHeight);
		Tuple<double, double> arrow = GetVerticalInterval(placed.Dimension);
		double lowerCenter = arrow.Item1 - textLength / 2.0 - gap;
		double upperCenter = arrow.Item2 + textLength / 2.0 + gap;
		double x = placed.DimLinePoint.X;
		yield return CreateVerticalCustomTextCandidate(x, lowerCenter, textLength, textHeight);
		yield return CreateVerticalCustomTextCandidate(x, upperCenter, textLength, textHeight);
	}

	private static TextSlideCandidate CreateVerticalCustomTextCandidate(double x, double centerY, double textLength, double textHeight)
	{
		double num = textHeight * 0.65;
		return new TextSlideCandidate
		{
			Position = new Point2D(x, centerY),
			Bounds = new TextBounds2D
			{
				MinX = x - num,
				MaxX = x + num,
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
		double value;
		if (isHorizontal)
		{
			num = (dim.FirstPoint.X + dim.SecondPoint.X) / 2.0;
			value = Math.Abs(dim.SecondPoint.X - dim.FirstPoint.X);
		}
		else
		{
			num = (dim.FirstPoint.Y + dim.SecondPoint.Y) / 2.0;
			value = Math.Abs(dim.SecondPoint.Y - dim.FirstPoint.Y);
		}
		string text = (string.IsNullOrEmpty(dim.OverrideText) ? _config.FormatNumber(value) : dim.OverrideText);
		double num2 = (double)Math.Max(text.Length, 2) * textHeight * 0.7;
		return Tuple.Create(num - num2 / 2.0, num + num2 / 2.0);
	}

	public double GetDimensionTextLength(DimensionLayoutItem dim, double textHeight)
	{
		string dimensionText = GetDimensionText(dim);
		return (double)Math.Max(dimensionText.Length, 2) * textHeight * 0.7;
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
				if (stackingLayerItem.Dimension.Kind != DimensionKind.HoleLocation && !HasArrowEndpointTouch(stackingLayerItem, layers, i, num, side, outline, firstOffset, perLevelSpacing, isHorizontal))
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
				if (HasStrictArrowConflict(candidate.ArrA, candidate.ArrB, item.ArrA, item.ArrB) || !AreCompatible(item.TxtA, item.TxtB, candidate.TxtA, candidate.TxtB, gap))
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
		List<int> list = (from item in source
			where item.Dimension.LooseChainId != 0 && item.Dimension.Kind == DimensionKind.HoleLocation
			select item.Dimension.LooseChainId).Distinct().ToList();
		foreach (int chainId in list)
		{
			List<IndexedLayoutItem> list2 = source.Where((IndexedLayoutItem item) => item.Dimension.LooseChainId == chainId).ToList();
			if (list2.Count == 0 || list2.Any((IndexedLayoutItem item) => !CanRebalanceVerticalLooseChainDimension(item.Dimension, dimScale)))
			{
				continue;
			}
			List<DimensionLayoutItem> sourceWithoutChain = (from item in source
				where item.Dimension.LooseChainId != chainId
				select item.Dimension).ToList();
			int num = list2.Sum((IndexedLayoutItem item) => ScoreVerticalSideCrowding(item.Dimension, sourceWithoutChain, dimScale));
			int num2 = list2.Sum((IndexedLayoutItem item) => ScoreVerticalSideCrowding(item.Dimension, target.Select((IndexedLayoutItem existing) => existing.Dimension), dimScale));
			int num3 = Math.Max(2, list2.Count);
			if (num - num2 < num3)
			{
				continue;
			}
			for (int num4 = source.Count - 1; num4 >= 0; num4--)
			{
				if (source[num4].Dimension.LooseChainId == chainId)
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
