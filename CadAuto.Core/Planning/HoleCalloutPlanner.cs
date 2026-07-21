using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning;

public sealed class HoleCalloutPlanner
{
	private sealed class HoleCalloutCluster
	{
		public HoleFeature2D BasePin { get; set; }

		public List<HoleFeature2D> Pins { get; private set; }

		public List<HoleFeature2D> Members { get; private set; }

		public HoleCalloutCluster()
		{
			Pins = new List<HoleFeature2D>();
			Members = new List<HoleFeature2D>();
		}
	}

	private readonly DimensionRuleConfig _config;

	public HoleCalloutPlanner(DimensionRuleConfig config)
	{
		_config = config ?? DimensionRuleConfig.CreateDefault();
	}

	public IList<HoleCalloutPlan> CreatePlans(IEnumerable<HoleFeature2D> holes, HoleFeature2D datumPin)
	{
		List<HoleFeature2D> list = ((holes == null) ? new List<HoleFeature2D>() : holes.Where((HoleFeature2D h) => h != null && !h.IsSlotPoint).ToList());
		List<HoleFeature2D> list2 = list.Where((HoleFeature2D h) => h.IsPinHole).ToList();
		if (list2.Count == 0)
		{
			return BuildPlansBySpatialRows(list);
		}
		List<HoleCalloutCluster> list3 = BuildPinCalloutClusters(list2, datumPin);
		AssignNonPinHolesToCalloutClusters(list, list3);
		List<HoleCalloutPlan> list4 = new List<HoleCalloutPlan>();
		int num = 1;
		foreach (HoleCalloutCluster item in list3)
		{
			foreach (IList<HoleFeature2D> item2 in GroupCalloutClusterMembers(item.Members))
			{
				list4.Add(CreatePlan(item2, "PG" + num.ToString(CultureInfo.InvariantCulture)));
			}
			num++;
		}
		return list4;
	}

	private IList<HoleCalloutPlan> BuildPlansBySpatialRows(IList<HoleFeature2D> holes)
	{
		List<HoleCalloutPlan> list = new List<HoleCalloutPlan>();
		foreach (IList<HoleFeature2D> item in GroupCalloutClusterMembers(holes))
		{
			list.Add(CreatePlan(item, string.Empty));
		}
		return list;
	}

	private List<HoleCalloutCluster> BuildPinCalloutClusters(IList<HoleFeature2D> pinHoles, HoleFeature2D datumPin)
	{
		List<HoleFeature2D> list = (from h in pinHoles
			orderby h.Center.X, h.Center.Y
			select h).ToList();
		List<HoleCalloutCluster> list2 = new List<HoleCalloutCluster>();
		HoleFeature2D seed = ((datumPin != null && datumPin.IsPinHole) ? (list.FirstOrDefault((HoleFeature2D h) => IsSameHoleForCallout(h, datumPin)) ?? datumPin) : list.First());
		while (list.Count > 0)
		{
			HoleFeature2D reference = ((list2.Count == 0) ? null : list2[list2.Count - 1].BasePin);
			if (list2.Count > 0)
			{
				seed = (from h in list
					orderby DistanceSquared(h.Center, reference.Center), h.Center.X, h.Center.Y
					select h).First();
			}
			HoleCalloutCluster holeCalloutCluster = CreatePinCalloutCluster(seed, list, (list2.Count == 0) ? seed : null, reference);
			list2.Add(holeCalloutCluster);
			foreach (HoleFeature2D item in holeCalloutCluster.Pins.ToList())
			{
				for (int num = list.Count - 1; num >= 0; num--)
				{
					if (IsSameHoleForCallout(list[num], item))
					{
						list.RemoveAt(num);
					}
				}
			}
			if (list.Count > 0 && !list.Any((HoleFeature2D h) => IsSameHoleForCallout(h, seed)))
			{
				seed = list[0];
			}
		}
		return list2;
	}

	private HoleCalloutCluster CreatePinCalloutCluster(HoleFeature2D seed, IList<HoleFeature2D> candidates, HoleFeature2D forcedBasePin, HoleFeature2D referenceBasePin)
	{
		List<HoleFeature2D> list = new List<HoleFeature2D> { seed };
		HoleFeature2D holeFeature2D = (from h in candidates
			where !IsSameHoleForCallout(h, seed) && Math.Abs(h.Diameter - seed.Diameter) <= _config.GeometryTolerance
			orderby DistanceSquared(h.Center, seed.Center), h.Center.X, h.Center.Y
			select h).FirstOrDefault();
		if (holeFeature2D != null)
		{
			list.Add(holeFeature2D);
		}
		HoleFeature2D basePin = ChooseCalloutBasePin(list, forcedBasePin, referenceBasePin, seed);
		HoleCalloutCluster holeCalloutCluster = new HoleCalloutCluster
		{
			BasePin = basePin
		};
		foreach (HoleFeature2D item in list.OrderBy((HoleFeature2D h) => DistanceSquared(h.Center, basePin.Center)))
		{
			holeCalloutCluster.Pins.Add(item);
			holeCalloutCluster.Members.Add(item);
		}
		return holeCalloutCluster;
	}

	private HoleFeature2D ChooseCalloutBasePin(IList<HoleFeature2D> pins, HoleFeature2D forcedBasePin, HoleFeature2D referenceBasePin, HoleFeature2D fallbackPin)
	{
		if (forcedBasePin != null)
		{
			return pins.FirstOrDefault((HoleFeature2D h) => IsSameHoleForCallout(h, forcedBasePin)) ?? forcedBasePin;
		}
		if (referenceBasePin != null)
		{
			return (from h in pins
				orderby DistanceSquared(h.Center, referenceBasePin.Center), h.Center.X, h.Center.Y
				select h).First();
		}
		return (from h in pins
			orderby h.Center.X, h.Center.Y
			select h).FirstOrDefault() ?? fallbackPin;
	}

	private void AssignNonPinHolesToCalloutClusters(IList<HoleFeature2D> holes, IList<HoleCalloutCluster> clusters)
	{
		foreach (HoleFeature2D hole in holes.Where((HoleFeature2D h) => !h.IsPinHole && !h.IsSlotPoint))
		{
			(from g in clusters
				orderby DistanceToCalloutCluster(hole, g), DistanceSquared(hole.Center, g.BasePin.Center)
				select g).FirstOrDefault()?.Members.Add(hole);
		}
	}

	private double DistanceToCalloutCluster(HoleFeature2D hole, HoleCalloutCluster cluster)
	{
		if (cluster == null || cluster.Pins.Count == 0)
		{
			return double.MaxValue;
		}
		if (cluster.Pins.Count == 1)
		{
			return Math.Sqrt(DistanceSquared(hole.Center, cluster.Pins[0].Center));
		}
		return cluster.Pins.Take(2).Sum((HoleFeature2D pin) => Math.Sqrt(DistanceSquared(hole.Center, pin.Center)));
	}

	private IEnumerable<IList<HoleFeature2D>> GroupCalloutClusterMembers(IEnumerable<HoleFeature2D> members)
	{
		foreach (IGrouping<int, HoleFeature2D> typeGroup in from g in members.Where((HoleFeature2D h) => h != null && !h.IsSlotPoint).GroupBy(GetHoleCalloutTypeRank)
			orderby g.Key
			select g)
		{
			List<List<HoleFeature2D>> groups = new List<List<HoleFeature2D>>();
			foreach (HoleFeature2D hole in from h in typeGroup
				orderby h.Diameter, h.Center.Y, h.Center.X
				select h)
			{
				List<HoleFeature2D> group = groups.FirstOrDefault((List<HoleFeature2D> g) => Math.Abs(g.Average((HoleFeature2D h) => h.Diameter) - hole.Diameter) <= _config.GeometryTolerance && g[0].Kind == hole.Kind && string.Equals(g[0].FitTolerance ?? string.Empty, hole.FitTolerance ?? string.Empty, StringComparison.OrdinalIgnoreCase) && string.Equals(g[0].ThreadCallout ?? string.Empty, hole.ThreadCallout ?? string.Empty, StringComparison.OrdinalIgnoreCase));
				if (group == null)
				{
					group = new List<HoleFeature2D>();
					groups.Add(group);
				}
				group.Add(hole);
			}
			foreach (List<HoleFeature2D> item in groups)
			{
				yield return item;
			}
		}
	}

	private HoleCalloutPlan CreatePlan(IList<HoleFeature2D> group, string debugOwner)
	{
		List<HoleFeature2D> list = (from h in @group
			orderby h.Center.Y, h.Center.X
			select h).ToList();
		HoleFeature2D holeFeature2D = list[0];
		HoleCalloutPlan holeCalloutPlan = new HoleCalloutPlan
		{
			Kind = GetHoleCalloutKind(holeFeature2D),
			AnchorPoint = holeFeature2D.Center,
			DebugOwner = (debugOwner ?? string.Empty),
			Text = (holeFeature2D.IsThreadHole ? _config.FormatThreadCallout(holeFeature2D.Diameter, list.Count, holeFeature2D.ThreadCallout) : _config.FormatHoleCallout(holeFeature2D.Diameter, list.Count, holeFeature2D.FitTolerance))
		};
		foreach (HoleFeature2D item in list)
		{
			holeCalloutPlan.Holes.Add(item);
		}
		return holeCalloutPlan;
	}

	private static HoleCalloutKind GetHoleCalloutKind(HoleFeature2D hole)
	{
		if (hole.IsPinHole)
		{
			return HoleCalloutKind.Pin;
		}
		if (hole.IsThreadHole)
		{
			return HoleCalloutKind.Thread;
		}
		return HoleCalloutKind.Normal;
	}

	private static int GetHoleCalloutTypeRank(HoleFeature2D hole)
	{
		if (hole.IsPinHole)
		{
			return 0;
		}
		if (hole.IsThreadHole)
		{
			return 1;
		}
		return 2;
	}

	private bool IsSameHoleForCallout(HoleFeature2D a, HoleFeature2D b)
	{
		if (a == null || b == null)
		{
			return false;
		}
		return DistanceSquared(a.Center, b.Center) <= _config.GeometryTolerance * _config.GeometryTolerance && Math.Abs(a.Diameter - b.Diameter) <= _config.GeometryTolerance;
	}

	private static double DistanceSquared(Point2D a, Point2D b)
	{
		double num = a.X - b.X;
		double num2 = a.Y - b.Y;
		return num * num + num2 * num2;
	}
}
