using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

// 托压块规则（用户 2026-11-04、11-05 确认）：托压块形状不固定，目标是"结构和整体尺寸要标，细碎无效尺寸不标"。
// 做法：规划前把外轮廓整理成只有横竖边的虚拟轮廓，再交给现有的结构、整体尺寸规则。
// 1. 同一条线上被 T 字连接切开的几段合成一条边；内部线不进虚拟轮廓。
// 2. 首尾相接的一串斜边、倒角、圆弧合成一段，再看它夹在哪两个面之间：
//    一横一竖：补成尖角；两个平行面错开：换成一条立面；同一条线上：去掉这个凸起，两侧合并。
// 3. 立面位置：贴外包络就放在外包络上（总尺寸不变）；否则取一个整数坐标的端点；都不是取中点。
public static class SupportBlockRules
{
	private enum Kind { Horizontal, Vertical, Oblique }

	private sealed class Face
	{
		public Kind Kind;
		public Point2D Start;
		public Point2D End;
		// 一串斜边经过的所有端点（判断是否贴外包络用；斜边中间的端点可能才是最外侧）。
		public List<Point2D> Points = new List<Point2D>();
		public double Coordinate => Kind == Kind.Horizontal ? (Start.Y + End.Y) / 2 : (Start.X + End.X) / 2;
	}

	public static OutlineFeature2D BuildOrthogonalOutline(OutlineTopologyDiagnostic graph, double tolerance, out string evidence)
	{
		if (graph == null || graph.Status != "UniqueExteriorWalk" || !graph.IsClosedWalk || graph.HasRepeatedBoundaryEdges
			|| graph.BoundaryPath.Count < 3 || !(tolerance > 0))
		{
			evidence = "SupportBlock.VirtualOutline|Failed|Reason=NoUniqueClosedBoundary";
			return null;
		}
		var edges = graph.Edges.ToDictionary(e => e.Id);
		var walk = new List<Face>();
		foreach (var step in graph.BoundaryPath.OrderBy(s => s.Order))
		{
			if (!edges.TryGetValue(step.EdgeId, out var edge))
			{
				evidence = "SupportBlock.VirtualOutline|Failed|Reason=MissingBoundaryEdge";
				return null;
			}
			var start = step.Forward ? edge.Start : edge.End;
			var end = step.Forward ? edge.End : edge.Start;
			Kind kind = edge.Kind != "Line" ? Kind.Oblique
				: Math.Abs(end.Y - start.Y) <= tolerance ? Kind.Horizontal
				: Math.Abs(end.X - start.X) <= tolerance ? Kind.Vertical
				: Kind.Oblique;
			walk.Add(new Face { Kind = kind, Start = start, End = end, Points = new List<Point2D> { start, end } });
		}

		var faces = Merge(walk, tolerance);
		int first = faces.FindIndex(f => f.Kind != Kind.Oblique);
		if (first < 0)
		{
			evidence = "SupportBlock.VirtualOutline|Failed|Reason=NoOrthogonalFace";
			return null;
		}
		// 从一个横竖面开始，这样每一串斜边的前后都是横竖面。
		faces = faces.Skip(first).Concat(faces.Take(first)).ToList();
		int corners = 0, risers = 0, bumps = 0;
		var result = new List<Face>();
		for (int i = 0; i < faces.Count; i++)
		{
			var face = faces[i];
			if (face.Kind != Kind.Oblique) { result.Add(face); continue; }
			var before = result[result.Count - 1];
			var after = faces[(i + 1) % faces.Count];
			if (before.Kind != after.Kind) { corners++; continue; }
			if (Math.Abs(before.Coordinate - after.Coordinate) <= tolerance) { bumps++; continue; }
			risers++;
			bool vertical = before.Kind == Kind.Horizontal;
			double a = vertical ? face.Start.X : face.Start.Y, b = vertical ? face.End.X : face.End.Y;
			double min = vertical ? graph.MinX : graph.MinY, max = vertical ? graph.MaxX : graph.MaxY;
			var along = face.Points.Select(q => vertical ? q.X : q.Y).ToList();
			double c = RiserCoordinate(a, b, along, min, max, tolerance);
			result.Add(vertical
				? new Face { Kind = Kind.Vertical, Start = new Point2D(c, before.Coordinate), End = new Point2D(c, after.Coordinate) }
				: new Face { Kind = Kind.Horizontal, Start = new Point2D(before.Coordinate, c), End = new Point2D(after.Coordinate, c) });
		}
		result = Merge(result, tolerance);
		int count = result.Count;
		if (count < 4 || Enumerable.Range(0, count).Any(i => result[i].Kind == result[(i + 1) % count].Kind))
		{
			evidence = "SupportBlock.VirtualOutline|Failed|Reason=NonAlternatingFaces|Faces=" + count;
			return null;
		}

		// 相邻横竖面所在直线的交点就是虚拟轮廓的顶点。
		var vertices = new List<Point2D>();
		for (int i = 0; i < count; i++)
		{
			var f = result[i];
			var g = result[(i + 1) % count];
			vertices.Add(f.Kind == Kind.Horizontal ? new Point2D(g.Coordinate, f.Coordinate) : new Point2D(f.Coordinate, g.Coordinate));
		}
		var outline = new OutlineFeature2D
		{
			MinX = vertices.Min(p => p.X), MaxX = vertices.Max(p => p.X),
			MinY = vertices.Min(p => p.Y), MaxY = vertices.Max(p => p.Y)
		};
		for (int i = 0; i < count; i++)
		{
			var s = vertices[i];
			var e = vertices[(i + 1) % count];
			outline.Vertices.Add(s);
			outline.Segments.Add(new Segment2D(s, e) { SourceKey = "SupportBlock.Virtual:" + i });
		}
		evidence = "SupportBlock.VirtualOutline|Faces=" + count + "|CornersCompleted=" + corners + "|Risers=" + risers + "|BumpsRemoved=" + bumps
			+ "|Envelope=" + Format(outline.MinX) + "," + Format(outline.MinY) + "," + Format(outline.MaxX) + "," + Format(outline.MaxY);
		return outline;
	}

	private static double RiserCoordinate(double a, double b, IEnumerable<double> along, double min, double max, double tol)
	{
		foreach (double env in new[] { min, max })
		{
			if (along.Any(v => Math.Abs(v - env) <= tol)) { return env; }
		}
		if (IsInteger(a - min)) { return a; }
		if (IsInteger(b - min)) { return b; }
		return (a + b) / 2;
	}

	// 同一条线上相邻的横竖面合并；相邻的斜边、圆弧合成一段。外轮廓是环，首尾也要合并。
	private static List<Face> Merge(IList<Face> faces, double tol)
	{
		var result = new List<Face>();
		foreach (var face in faces)
		{
			var last = result.LastOrDefault();
			if (last != null && Joinable(last, face, tol)) { last.End = face.End; last.Points.AddRange(face.Points); }
			else { result.Add(new Face { Kind = face.Kind, Start = face.Start, End = face.End, Points = face.Points.ToList() }); }
		}
		while (result.Count > 1 && Joinable(result[result.Count - 1], result[0], tol))
		{
			result[0].Start = result[result.Count - 1].Start;
			result[0].Points.InsertRange(0, result[result.Count - 1].Points);
			result.RemoveAt(result.Count - 1);
		}
		return result;
	}

	private static bool Joinable(Face a, Face b, double tol)
		=> a.Kind == b.Kind && (a.Kind == Kind.Oblique || Math.Abs(a.Coordinate - b.Coordinate) <= tol);

	// T 型托压块（用户 2026-11-05 新增分类）：虚拟轮廓 8 个顶点；一侧是通长的横梁边；对侧只有一条边（凸台底面），
	// 不碰包络两端；横梁内侧的两段在同一条线上，各自连到包络一端。坐标用 u（沿横梁）和 v（沿凸台方向）表示。
	public static SupportBlockTShape ClassifyT(OutlineFeature2D outline, double tol)
	{
		var shape = new SupportBlockTShape();
		if (outline == null || outline.Segments.Count != 8 || outline.Arcs.Count != 0
			|| outline.Segments.Any(s => !(Math.Abs(s.Start.X - s.End.X) <= tol || Math.Abs(s.Start.Y - s.End.Y) <= tol)))
		{
			shape.Evidence = "SupportBlock.Shape|NotT|Reason=NotOrthogonalOctagon";
			return shape;
		}
		foreach (bool beamHorizontal in new[] { true, false })
		{
			Func<Point2D, double> u = p => beamHorizontal ? p.X : p.Y;
			Func<Point2D, double> v = p => beamHorizontal ? p.Y : p.X;
			double uMin = beamHorizontal ? outline.MinX : outline.MinY, uMax = beamHorizontal ? outline.MaxX : outline.MaxY;
			double vMin = beamHorizontal ? outline.MinY : outline.MinX, vMax = beamHorizontal ? outline.MaxY : outline.MaxX;
			// 平行于横梁的边：两端 v 相同、u 不同。
			var parallel = outline.Segments.Where(s => Math.Abs(v(s.Start) - v(s.End)) <= tol && Math.Abs(u(s.Start) - u(s.End)) > tol)
				.Select(s => new { V = v(s.Start), Low = Math.Min(u(s.Start), u(s.End)), High = Math.Max(u(s.Start), u(s.End)) }).ToList();
			if (parallel.Count != 4) { continue; }
			foreach (bool fullAtMax in new[] { true, false })
			{
				double vFull = fullAtMax ? vMax : vMin, vBoss = fullAtMax ? vMin : vMax;
				var full = parallel.Where(p => Math.Abs(p.V - vFull) <= tol).ToList();
				var boss = parallel.Where(p => Math.Abs(p.V - vBoss) <= tol).ToList();
				var inner = parallel.Where(p => Math.Abs(p.V - vFull) > tol && Math.Abs(p.V - vBoss) > tol).ToList();
				if (full.Count != 1 || Math.Abs(full[0].Low - uMin) > tol || Math.Abs(full[0].High - uMax) > tol) { continue; }
				if (boss.Count != 1 || boss[0].Low <= uMin + tol || boss[0].High >= uMax - tol) { continue; }
				if (inner.Count != 2 || Math.Abs(inner[0].V - inner[1].V) > tol) { continue; }
				var innerSorted = inner.OrderBy(p => p.Low).ToList();
				if (Math.Abs(innerSorted[0].Low - uMin) > tol || Math.Abs(innerSorted[1].High - uMax) > tol) { continue; }
				shape.IsT = true;
				shape.BeamHorizontal = beamHorizontal;
				shape.UMin = uMin; shape.UMax = uMax;
				shape.VFull = vFull; shape.VBoss = vBoss; shape.VInner = inner[0].V;
				shape.BossLow = boss[0].Low; shape.BossHigh = boss[0].High;
				shape.Evidence = "SupportBlock.Shape=T|Beam=" + (beamHorizontal ? "Horizontal" : "Vertical")
					+ "|BeamThickness=" + Format(Math.Abs(vFull - shape.VInner)) + "|BossHeight=" + Format(Math.Abs(shape.VInner - vBoss))
					+ "|Boss=" + Format(shape.BossLow - uMin) + ".." + Format(shape.BossHigh - uMin) + "|Length=" + Format(uMax - uMin);
				return shape;
			}
		}
		shape.Evidence = "SupportBlock.Shape|NotT|Reason=NoBeamAndSingleBoss";
		return shape;
	}

	// "优先标整数"（用户 2026-11-05）：相对外包络最小值的坐标或尺寸值是否为整数。
	public static bool IsInteger(double value) => Math.Abs(value - Math.Round(value)) <= 0.005;

	private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

// T 型托压块的几何：u 沿横梁，v 沿凸台方向。BeamHorizontal 为 true 时 u=X、v=Y，否则 u=Y、v=X。
public sealed class SupportBlockTShape
{
	public bool IsT { get; set; }
	public bool BeamHorizontal { get; set; }
	public double UMin { get; set; }
	public double UMax { get; set; }
	public double VFull { get; set; }
	public double VInner { get; set; }
	public double VBoss { get; set; }
	public double BossLow { get; set; }
	public double BossHigh { get; set; }
	public string Evidence { get; set; }

	public Point2D Point(double u, double v) => BeamHorizontal ? new Point2D(u, v) : new Point2D(v, u);
}
