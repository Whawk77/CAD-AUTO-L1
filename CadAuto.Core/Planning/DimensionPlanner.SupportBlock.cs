using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning;

// 托压块规则（用户 2026-11-04、11-05 确认）：框选集合里有 TY/TY1/TY2 的标注时，外轮廓先整理成只有横竖边的
// 虚拟轮廓（SupportBlockRules），再照常跑结构、整体尺寸规则；斜边、倒角、碎段因此不生成尺寸。
// 几个尺寸能互相推算时优先保留整数：去掉非整数的一个；都是整数时去掉最大的一个。总尺寸不去。孔照常标。
public sealed partial class DimensionPlanner
{
	public const string SupportBlockDerivableRuleId = "SupportBlock.DerivableDimension";

	// 返回虚拟轮廓；整理失败返回原轮廓（照常规划，诊断里记录原因）。
	private OutlineFeature2D PrepareSupportBlockOutline(OutlineFeature2D outline, out string evidence)
	{
		double tol = _config.GeometryTolerance;
		var graph = InspectOutlineTopology(outline, tol);
		OutlineFeature2D virtualOutline = SupportBlockRules.BuildOrthogonalOutline(graph, tol, out evidence);
		return virtualOutline ?? outline;
	}

	// 一条横向（或竖向）的尺寸链：从外包络的一端连到另一端、每段相邻首尾相接的一组结构尺寸。
	// 链里所有段之和等于总尺寸，可以去掉一段。也处理两段之和等于第三段的情况（同轴、区间首尾相接）。
	private void SuppressSupportBlockDerivableDimensions(DimensionPlan plan, OutlineFeature2D outline)
	{
		double tol = _config.GeometryTolerance;
		foreach (bool horizontal in new[] { true, false })
		{
			for (int guard = 0; guard < 16; guard++)
			{
				var intervals = plan.Dimensions
					.Where(d => (d.Kind == DimensionKind.Normal || d.Kind == DimensionKind.OverallWidth || d.Kind == DimensionKind.OverallHeight)
						&& d.Role != DimensionCandidateRole.Hole && d.Role != DimensionCandidateRole.Pin && d.Role != DimensionCandidateRole.Slot
						&& d.Role != DimensionCandidateRole.Datum
						&& d.Orientation == (horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical))
					.Select(d => new { Dimension = d, Low = Math.Min(Axis(d.FirstPoint, horizontal), Axis(d.SecondPoint, horizontal)),
						High = Math.Max(Axis(d.FirstPoint, horizontal), Axis(d.SecondPoint, horizontal)) })
					.ToList();
				PlannedDimension victim = null;
				string relation = null;
				// 找一个区间能被另外两个首尾相接的区间拼出来：a.Low=whole.Low，a.High=b.Low，b.High=whole.High。
				foreach (var whole in intervals)
				{
					foreach (var a in intervals.Where(x => x != whole && Math.Abs(x.Low - whole.Low) <= tol))
					{
						var b = intervals.FirstOrDefault(x => x != whole && x != a && Math.Abs(x.Low - a.High) <= tol && Math.Abs(x.High - whole.High) <= tol);
						if (b == null) { continue; }
						var group = new[] { whole.Dimension, a.Dimension, b.Dimension };
						victim = ChooseDerivableVictim(group);
						if (victim != null) { relation = string.Join("+", new[] { a, b }.Select(x => _config.FormatNumber(x.High - x.Low))) + "=" + _config.FormatNumber(whole.High - whole.Low); break; }
					}
					if (victim != null) { break; }
				}
				if (victim == null) { break; }
				plan.MarkSuppressed(victim, SupportBlockDerivableRuleId, SupportBlockDerivableRuleId, null,
					SupportBlockDerivableRuleId + "|Relation=" + relation + "|PreferInteger");
				plan.Dimensions.Remove(victim);
			}
		}
	}

	// 优先去掉非整数的非总尺寸；都是整数时去掉最大的非总尺寸。
	private static PlannedDimension ChooseDerivableVictim(IEnumerable<PlannedDimension> group)
	{
		var removable = group.Where(d => d.Kind != DimensionKind.OverallWidth && d.Kind != DimensionKind.OverallHeight).ToList();
		if (removable.Count == 0) { return null; }
		var nonInteger = removable.Where(d => !SupportBlockRules.IsInteger(Span(d))).OrderByDescending(Span).FirstOrDefault();
		return nonInteger ?? removable.OrderByDescending(Span).First();
	}

	public const string SupportBlockTRulePrefix = "SupportBlock.T.";
	public const string SupportBlockTOverallLengthRuleId = "SupportBlock.T.OverallLength";
	public const string SupportBlockTOverallDepthRuleId = "SupportBlock.T.OverallDepth";

	// T 型托压块（用户 2026-11-05）：只出 5 个尺寸。沿横梁：凸台宽、从较近的包络端量到凸台远边的定位放凸台一侧，
	// 总长放横梁一侧。垂直横梁：总高放定位起点那一侧，横梁厚（与凸台高二选一，优先整数，都是整数取短的）放对侧。
	// 返回 false 表示不是 T 型，照常规划。
	private bool TryAddSupportBlockTDimensions(DimensionPlan plan, OutlineFeature2D outline)
	{
		double tol = _config.GeometryTolerance;
		SupportBlockTShape t = SupportBlockRules.ClassifyT(outline, tol);
		plan.Diagnostics.Warnings.Add(t.Evidence);
		if (!t.IsT) { return false; }

		double nearLow = t.BossLow - t.UMin, nearHigh = t.UMax - t.BossHigh;
		bool fromMin = nearLow <= nearHigh + tol;
		double edge = fromMin ? t.UMin : t.UMax;
		double bossNear = fromMin ? t.BossLow : t.BossHigh, bossFar = fromMin ? t.BossHigh : t.BossLow;
		// 远边距离不是整数、近边距离是整数时，改量近边。
		double locateTo = !SupportBlockRules.IsInteger(Math.Abs(bossFar - edge)) && SupportBlockRules.IsInteger(Math.Abs(bossNear - edge)) ? bossNear : bossFar;
		double beam = Math.Abs(t.VFull - t.VInner), bossHeight = Math.Abs(t.VInner - t.VBoss);
		bool beamInteger = SupportBlockRules.IsInteger(beam), bossInteger = SupportBlockRules.IsInteger(bossHeight);
		bool useBeam = beamInteger != bossInteger ? beamInteger : beam <= bossHeight + tol;

		var uOrientation = t.BeamHorizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
		var vOrientation = t.BeamHorizontal ? DimensionOrientation.Vertical : DimensionOrientation.Horizontal;
		bool bossAtLow = t.VBoss < t.VFull;
		DimensionSide uSide = t.BeamHorizontal ? (bossAtLow ? DimensionSide.Bottom : DimensionSide.Top) : (bossAtLow ? DimensionSide.Left : DimensionSide.Right);
		DimensionSide uSideBeam = t.BeamHorizontal ? (bossAtLow ? DimensionSide.Top : DimensionSide.Bottom) : (bossAtLow ? DimensionSide.Right : DimensionSide.Left);
		DimensionSide vSideEdge = t.BeamHorizontal ? (fromMin ? DimensionSide.Left : DimensionSide.Right) : (fromMin ? DimensionSide.Bottom : DimensionSide.Top);
		DimensionSide vSideOpposite = t.BeamHorizontal ? (fromMin ? DimensionSide.Right : DimensionSide.Left) : (fromMin ? DimensionSide.Top : DimensionSide.Bottom);
		double farEdge = fromMin ? t.UMax : t.UMin;

		void Add(DimensionKind kind, DimensionOrientation orientation, DimensionSide side, Point2D a, Point2D b, string ruleId, string role, bool overall)
		{
			plan.Add(new PlannedDimension
			{
				Kind = kind, Orientation = orientation, Side = side, FirstPoint = a, SecondPoint = b,
				Role = overall ? DimensionCandidateRole.Unknown : DimensionCandidateRole.Structure,
				OwnerKind = DimensionCandidateOwnerKind.Outline,
				ForceOuterLevel = overall, PreservePreferredSide = true,
				ReadingLevel = overall ? DimensionReadingLevel.Overall : DimensionReadingLevel.LocalSpacing,
				RuleId = ruleId, DebugRole = role, DebugOwner = "SupportBlock.T", SourceKey = ruleId,
				TopologyEvidence = t.Evidence
			});
		}

		DimensionKind uOverall = t.BeamHorizontal ? DimensionKind.OverallWidth : DimensionKind.OverallHeight;
		DimensionKind vOverall = t.BeamHorizontal ? DimensionKind.OverallHeight : DimensionKind.OverallWidth;
		// 总长放横梁一侧（用户 2026-11-05：凸台一侧有 TY 的 CNC加工/淬火引线和粗糙度，50 放底部会干涉）。
		Add(uOverall, uOrientation, uSideBeam, t.Point(t.UMin, t.VFull), t.Point(t.UMax, t.VFull),
			SupportBlockTOverallLengthRuleId, "SupportBlockTOverallLength", true);
		Add(DimensionKind.Normal, uOrientation, uSide, t.Point(t.BossLow, t.VBoss), t.Point(t.BossHigh, t.VBoss),
			"SupportBlock.T.BossWidth", "SupportBlockTBossWidth", false);
		Add(DimensionKind.Normal, uOrientation, uSide, t.Point(edge, t.VInner), t.Point(locateTo, t.VBoss),
			"SupportBlock.T.BossLocation", "SupportBlockTBossLocation", false);
		Add(vOverall, vOrientation, vSideEdge, t.Point(bossNear, t.VBoss), t.Point(edge, t.VFull),
			SupportBlockTOverallDepthRuleId, "SupportBlockTOverallDepth", true);
		if (useBeam)
		{
			Add(DimensionKind.Normal, vOrientation, vSideOpposite, t.Point(farEdge, t.VFull), t.Point(farEdge, t.VInner),
				"SupportBlock.T.BeamThickness", "SupportBlockTBeamThickness", false);
		}
		else
		{
			Add(DimensionKind.Normal, vOrientation, vSideOpposite, t.Point(bossFar, t.VBoss), t.Point(farEdge, t.VInner),
				"SupportBlock.T.BossHeight", "SupportBlockTBossHeight", false);
		}
		return true;
	}

	private static double Axis(Point2D p, bool horizontal) => horizontal ? p.X : p.Y;

	private static double Span(PlannedDimension d)
		=> d.Orientation == DimensionOrientation.Horizontal ? Math.Abs(d.SecondPoint.X - d.FirstPoint.X) : Math.Abs(d.SecondPoint.Y - d.FirstPoint.Y);
}
