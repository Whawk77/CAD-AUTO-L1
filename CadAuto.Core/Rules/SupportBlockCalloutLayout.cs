using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

// 托压块 TY 标注避让（用户 2026-11-05 确认）：ASD 尺寸和 TY 的引线文字、粗糙度块重叠时，把 TY 的文字、
// 引线转折点、粗糙度块一起沿外法向移到这一侧 ASD 尺寸外面，再留 gap。引线箭头和偏移虚线不动。
// 目标位置按"ASD 最外层 + gap"计算，重复运行 ASD 结果不累加。不重叠时不动。
public sealed class SupportBlockCalloutShift
{
	public DimensionSide Side { get; set; }
	public double DeltaX { get; set; }
	public double DeltaY { get; set; }
	public string Evidence { get; set; }
}

public static class SupportBlockCalloutLayout
{
	// movable：一组 TY 标注里会移动的对象外框（文字、粗糙度块）；obstacles：ASD 尺寸各部分的外框。
	// 返回 null 表示不用移动。
	public static SupportBlockCalloutShift ComputeShift(IList<TextBounds2D> movable, IList<TextBounds2D> obstacles,
		double envMinX, double envMinY, double envMaxX, double envMaxY, double gap, double tol)
	{
		if (movable == null || movable.Count == 0 || obstacles == null || obstacles.Count == 0) { return null; }
		var group = Union(movable);
		double cx = (group.MinX + group.MaxX) / 2, cy = (group.MinY + group.MaxY) / 2;
		var outside = new[]
		{
			Tuple.Create(DimensionSide.Bottom, envMinY - cy), Tuple.Create(DimensionSide.Top, cy - envMaxY),
			Tuple.Create(DimensionSide.Left, envMinX - cx), Tuple.Create(DimensionSide.Right, cx - envMaxX)
		}.OrderByDescending(t => t.Item2).First();
		if (outside.Item2 <= tol) { return null; }
		DimensionSide side = outside.Item1;
		if (!movable.Any(m => obstacles.Any(o => Overlaps(m, o, tol)))) { return null; }

		bool vertical = side == DimensionSide.Bottom || side == DimensionSide.Top;
		double margin = Math.Max(tol, gap * 0.25);
		// 只看沿边方向和这组标注有交叠、并且伸到包络外这一侧的尺寸部件。
		var relevant = obstacles.Where(o => vertical
				? o.MaxX >= group.MinX - gap && o.MinX <= group.MaxX + gap
				: o.MaxY >= group.MinY - gap && o.MinY <= group.MaxY + gap)
			// 另一方向的尺寸贴着包络边起止，外框可能略超出包络；要求明显伸到这一侧外面。
			.Where(o => side == DimensionSide.Bottom ? o.MinY < envMinY - margin
				: side == DimensionSide.Top ? o.MaxY > envMaxY + margin
				: side == DimensionSide.Left ? o.MinX < envMinX - margin
				: o.MaxX > envMaxX + margin)
			.ToList();
		if (relevant.Count == 0) { return null; }
		double dx = 0, dy = 0;
		switch (side)
		{
			case DimensionSide.Bottom: dy = relevant.Min(o => o.MinY) - gap - group.MaxY; if (dy >= -tol) { return null; } break;
			case DimensionSide.Top: dy = relevant.Max(o => o.MaxY) + gap - group.MinY; if (dy <= tol) { return null; } break;
			case DimensionSide.Left: dx = relevant.Min(o => o.MinX) - gap - group.MaxX; if (dx >= -tol) { return null; } break;
			default: dx = relevant.Max(o => o.MaxX) + gap - group.MinX; if (dx <= tol) { return null; } break;
		}
		return new SupportBlockCalloutShift
		{
			Side = side, DeltaX = dx, DeltaY = dy,
			Evidence = "SupportBlock.CalloutShift|Side=" + side + "|Distance=" + Math.Round(Math.Abs(dx + dy), 3)
		};
	}

	public static TextBounds2D Union(IEnumerable<TextBounds2D> boxes)
	{
		var list = boxes.ToList();
		return new TextBounds2D { MinX = list.Min(b => b.MinX), MinY = list.Min(b => b.MinY), MaxX = list.Max(b => b.MaxX), MaxY = list.Max(b => b.MaxY) };
	}

	private static bool Overlaps(TextBounds2D a, TextBounds2D b, double tol)
		=> a.MinX < b.MaxX - tol && b.MinX < a.MaxX - tol && a.MinY < b.MaxY - tol && b.MinY < a.MaxY - tol;
}
