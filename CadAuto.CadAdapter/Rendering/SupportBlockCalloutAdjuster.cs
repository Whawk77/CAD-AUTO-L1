using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;
using CadAuto.Core.Rules;

namespace CadAuto.CadAdapter.Rendering;

// 托压块 TY 标注避让（用户 2026-11-05 确认）：ASD 画完尺寸后，同侧的 TY 引线文字、粗糙度块和 ASD 尺寸重叠时，
// 把一组 TY 标注（同一 GroupId 的文字、引线转折点、粗糙度块）一起移到 ASD 尺寸外面。
// 引线箭头和偏移虚线不动；和 ASD 标注在同一事务里，一次 UNDO 撤回。
public static class SupportBlockCalloutAdjuster
{
	public static List<string> Adjust(Transaction transaction, BlockTableRecord space, IEnumerable<ObjectId> supportBlockIds, IEnumerable<ObjectId> asdDimensionIds,
		OutlineFeature outline, double gap, double tolerance)
	{
		var records = new List<string>();
		if (outline == null) { return records; }
		var obstacles = new List<TextBounds2D>();
		foreach (ObjectId id in asdDimensionIds)
		{
			if (TryOpen(transaction, id, out Entity entity) && entity is RotatedDimension dimension) { obstacles.Add(DimensionBounds(dimension)); }
		}
		int groupCount = 0, entityCount = 0;
		if (obstacles.Count == 0)
		{
			records.Add("SupportBlock.CalloutCheck|Obstacles=0");
			return records;
		}

		// 框选通常只框到偏移虚线和引线，TY 的文字和粗糙度块可能在框外，所以按 GroupId 到当前空间里找全一组。
		var groupIds = new HashSet<string>();
		foreach (ObjectId id in supportBlockIds)
		{
			if (TryOpen(transaction, id, out Entity entity))
			{
				string group = AnnotationMetadata.GetGroupId(entity);
				if (!string.IsNullOrEmpty(group)) { groupIds.Add(group); }
			}
		}
		var groups = new Dictionary<string, List<Entity>>();
		if (groupIds.Count == 0)
		{
			records.Add("SupportBlock.CalloutCheck|Obstacles=" + obstacles.Count + "|Groups=0");
			return records;
		}
		foreach (ObjectId id in space)
		{
			if (!id.ObjectClass.DxfName.Equals("MTEXT", StringComparison.OrdinalIgnoreCase)
				&& !id.ObjectClass.DxfName.Equals("INSERT", StringComparison.OrdinalIgnoreCase)
				&& !id.ObjectClass.DxfName.Equals("LEADER", StringComparison.OrdinalIgnoreCase)) { continue; }
			if (!TryOpen(transaction, id, out Entity entity) || !AnnotationMetadata.IsMarked(entity)
				|| AnnotationMetadata.GetKind(entity) != AnnotationMetadata.KindSupportBlock) { continue; }
			string group = AnnotationMetadata.GetGroupId(entity);
			if (!groupIds.Contains(group)) { continue; }
			if (!groups.TryGetValue(group, out var list)) { groups[group] = list = new List<Entity>(); }
			list.Add(entity);
		}
		var shifts = new List<string>();
		foreach (var pair in groups)
		{
			groupCount++;
			entityCount += pair.Value.Count;
			var movable = pair.Value.Where(e => e is MText || e is BlockReference).ToList();
			var boxes = new List<TextBounds2D>();
			foreach (var entity in movable) { if (TryBounds(entity, out var box)) { boxes.Add(box); } }
			if (boxes.Count == 0) { continue; }
			var shift = SupportBlockCalloutLayout.ComputeShift(boxes, obstacles, outline.MinX, outline.MinY, outline.MaxX, outline.MaxY, gap, tolerance);
			if (shift == null) { continue; }
			var delta = new Vector3d(shift.DeltaX, shift.DeltaY, 0.0);
			var move = Matrix3d.Displacement(delta);
			foreach (var entity in movable)
			{
				entity.UpgradeOpen();
				entity.TransformBy(move);
			}
			foreach (var leader in pair.Value.OfType<Leader>())
			{
				leader.UpgradeOpen();
				// 箭头（第 0 点）留在虚线上，其余转折点跟文字一起移动。
				for (int i = 1; i < leader.NumVertices; i++) { leader.SetVertexAt(i, leader.VertexAt(i) + delta); }
				leader.EvaluateLeader();
			}
			shifts.Add(shift.Evidence + "|Group=" + pair.Key + "|Entities=" + pair.Value.Count);
		}
		records.Add("SupportBlock.CalloutCheck|Obstacles=" + obstacles.Count + "|Groups=" + groupCount + "|Entities=" + entityCount + "|Shifted=" + shifts.Count);
		records.AddRange(shifts);
		return records;
	}

	// 新建尺寸在事务提交前 GeometricExtents 不可靠（尺寸块还没生成），按尺寸自身的几何算外框：
	// 两个尺寸界线原点、尺寸线两端、文字位置（按字高和文字长度估算）。
	private static TextBounds2D DimensionBounds(RotatedDimension dimension)
	{
		var d = new Vector3d(Math.Cos(dimension.Rotation), Math.Sin(dimension.Rotation), 0.0);
		Point3d line = dimension.DimLinePoint;
		Func<Point3d, Point3d> project = p => line + d * ((p - line).DotProduct(d));
		var points = new List<Point3d> { dimension.XLine1Point, dimension.XLine2Point, project(dimension.XLine1Point), project(dimension.XLine2Point) };
		double scale = dimension.Dimscale > 0.0 ? dimension.Dimscale : 1.0;
		double height = Math.Max(dimension.Dimtxt * scale, 1e-6);
		double arrow = dimension.Dimasz * scale;
		string text = string.IsNullOrEmpty(dimension.DimensionText) || dimension.DimensionText == "<>"
			? Math.Round(dimension.Measurement, 2).ToString(System.Globalization.CultureInfo.InvariantCulture)
			: dimension.DimensionText;
		double halfLength = Math.Max(1, text.Length) * height * 0.45;
		// 默认文字位置在尺寸块生成前未必有效，取尺寸线中点（外框上下各留一个字高，覆盖文字在线上方的情况）。
		Point3d mid = project(dimension.XLine1Point) + (project(dimension.XLine2Point) - project(dimension.XLine1Point)) * 0.5;
		Point3d t = dimension.UsingDefaultTextPosition ? mid : dimension.TextPosition;
		bool horizontalText = Math.Abs(d.X) >= Math.Abs(d.Y);
		double hx = horizontalText ? halfLength : height, hy = horizontalText ? height : halfLength;
		points.Add(new Point3d(t.X - hx, t.Y - hy, 0)); points.Add(new Point3d(t.X + hx, t.Y + hy, 0));
		var box = new TextBounds2D { MinX = points.Min(p => p.X), MinY = points.Min(p => p.Y), MaxX = points.Max(p => p.X), MaxY = points.Max(p => p.Y) };
		// 箭头可能翻到尺寸界线外侧（短尺寸），沿尺寸线方向外扩一个箭头长度。
		if (horizontalText) { box.MinX -= arrow; box.MaxX += arrow; } else { box.MinY -= arrow; box.MaxY += arrow; }
		return box;
	}

	private static bool TryOpen(Transaction transaction, ObjectId id, out Entity entity)
	{
		entity = null;
		if (id.IsNull || !id.IsValid || id.IsErased) { return false; }
		entity = transaction.GetObject(id, OpenMode.ForRead, openErased: false) as Entity;
		return entity != null;
	}

	private static bool TryBounds(Entity entity, out TextBounds2D box)
	{
		box = null;
		try
		{
			Extents3d e = entity.GeometricExtents;
			box = new TextBounds2D { MinX = e.MinPoint.X, MinY = e.MinPoint.Y, MaxX = e.MaxPoint.X, MaxY = e.MaxPoint.Y };
			return true;
		}
		catch (Autodesk.AutoCAD.Runtime.Exception)
		{
			return false;
		}
	}
}
