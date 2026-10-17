using System.Collections.Generic;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Planning.Modules;

public sealed class LaneOccupancy
{
	private readonly Dictionary<DimensionSide, List<PlannedDimension>> _bySide = new Dictionary<DimensionSide, List<PlannedDimension>>();

	public void Record(PlannedDimension dimension)
	{
		if (dimension == null)
		{
			return;
		}
		if (!_bySide.TryGetValue(dimension.Side, out List<PlannedDimension> list))
		{
			list = new List<PlannedDimension>();
			_bySide[dimension.Side] = list;
		}
		list.Add(dimension);
	}

	public void RecordAll(IEnumerable<PlannedDimension> dimensions)
	{
		if (dimensions == null)
		{
			return;
		}
		foreach (PlannedDimension dimension in dimensions)
		{
			Record(dimension);
		}
	}

	public IList<PlannedDimension> GetSide(DimensionSide side)
	{
		if (_bySide.TryGetValue(side, out List<PlannedDimension> list))
		{
			return list;
		}
		return new List<PlannedDimension>();
	}

	public int Count(DimensionSide side)
	{
		return _bySide.TryGetValue(side, out List<PlannedDimension> list) ? list.Count : 0;
	}
}
