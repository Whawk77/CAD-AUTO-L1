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
	private readonly DimensionRuleConfig _config;

	public HoleCalloutPlanner(DimensionRuleConfig config)
	{
		_config = config ?? DimensionRuleConfig.CreateDefault();
	}

	public IList<HoleCalloutPlan> CreatePlans(IEnumerable<HoleFeature2D> holes, HoleFeature2D datumPin)
	{
		return CreatePlans(holes, datumPin, null);
	}

	public IList<HoleCalloutPlan> CreatePlans(IEnumerable<HoleFeature2D> holes, HoleFeature2D datumPin, DimensionPlan positionPlan)
	{
		List<HoleFeature2D> list = ((holes == null) ? new List<HoleFeature2D>() : holes.Where((HoleFeature2D h) => h != null && !h.IsSlotPoint).ToList());
		List<HoleFeature2D> list2 = list.Where((HoleFeature2D h) => h.IsPinHole).ToList();
		if (list2.Count == 0)
		{
			return BuildPlansBySpatialRows(list);
		}
		bool usePositionGroups = positionPlan != null && positionPlan.PinGroups.Count > 0;
		IList<PinGroupPlan> list3 = usePositionGroups ? positionPlan.PinGroups : new DimensionPlanner(_config).BuildPinGroupPlan(list, datumPin);
		List<HoleCalloutPlan> list4 = new List<HoleCalloutPlan>();
		foreach (PinGroupPlan item in list3.OrderBy(g => g.GroupIndex))
		{
			// Reuse location ownership, but keep diameter anchors on the original WCS holes.
			IEnumerable<HoleFeature2D> members = usePositionGroups
				? list.Where(h => item.MemberHoles.Any(member => MatchesPositionMember(h, member, positionPlan.CoordinateFrame)))
				: item.MemberHoles;
			foreach (IList<HoleFeature2D> item2 in GroupCalloutClusterMembers(members))
			{
				list4.Add(CreatePlan(item2, "PG" + item.GroupIndex.ToString(CultureInfo.InvariantCulture)));
			}
		}
		return list4;
	}

	private bool MatchesPositionMember(HoleFeature2D source, HoleFeature2D member, CoordinateFrame2D frame)
	{
		return source.Kind == member.Kind
			&& Math.Abs(source.Diameter - member.Diameter) <= _config.GeometryTolerance
			&& (string.IsNullOrEmpty(member.SourceKey) || string.Equals(source.SourceKey, member.SourceKey, StringComparison.Ordinal))
			&& source.Center.DistanceTo((frame ?? CoordinateFrame2D.Identity).ToWorld(member.Center)) <= _config.GeometryTolerance;
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

}
