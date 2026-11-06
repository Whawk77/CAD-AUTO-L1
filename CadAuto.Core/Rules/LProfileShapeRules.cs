using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public enum LProfileShapeKind
{
	NotLProfile,
	Plain,
	LocalStep
}

public sealed class LProfileShape
{
	public LProfileShapeKind Kind { get; set; }

	// 诊断用：Plain、LocalStep 记录面数与内凹角数，NotLProfile 记录失败原因。
	public string Evidence { get; set; }

	// 内凹角两侧的内侧臂面（补成虚拟尖角后）的长度：从臂的外端量到另一条内侧臂面所在的线。
	public double InnerHorizontalLength { get; set; }

	public double InnerVerticalLength { get; set; }

	public bool IsLProfile => Kind != LProfileShapeKind.NotLProfile;
}

// L 型判定的唯一入口，L 型规则和基准基线规则都用它（用户于 2026-11-02 确认统一）。
// 做法：把外轮廓补成虚拟 L 型再判断，只看几何，不依赖倒角、圆角是否已被识别。
// 1. 同方向、同一条线上相邻的面合成一个面（例如被内部线截断的边）。
// 2. 夹在水平面和竖直面之间的斜边、圆弧是转角处理，补成尖角；夹在两个平行面之间的斜边、圆弧是台阶立面，保留。
// 3. 补完后 6 个正交面、1 个内凹角：标准 L 型。
// 4. 补完后 8 个正交面、2 个内凹角，且两个内凹角之间只隔一个台阶（两个面），台阶较浅方向的深度
//    不超过较薄臂厚的 maxStepDepthRatio：内角处带一个局部小台阶的 L 型。
public static class LProfileShapeRules
{
	private enum Kind { Horizontal, Vertical, Oblique }

	private sealed class Face
	{
		public Kind Kind;
		public Point2D Start;
		public Point2D End;

		// 由平行面之间的倒角、圆弧换来的立面。
		public bool Riser;

		public double Coordinate => Kind == Kind.Horizontal ? (Start.Y + End.Y) / 2 : (Start.X + End.X) / 2;
	}

	public static LProfileShape Classify(OutlineTopologyDiagnostic graph, double tolerance, double maxStepDepthRatio)
	{
		if (graph == null || graph.Status != "UniqueExteriorWalk" || !graph.IsClosedWalk || graph.HasRepeatedBoundaryEdges
			|| graph.BoundaryPath.Count < 3 || !(tolerance > 0))
		{
			return NotL("NoUniqueClosedBoundary");
		}
		var edges = graph.Edges.ToDictionary(e => e.Id);
		var walk = new List<Face>();
		foreach (var step in graph.BoundaryPath.OrderBy(s => s.Order))
		{
			if (!edges.TryGetValue(step.EdgeId, out var edge)) { return NotL("MissingBoundaryEdge"); }
			var start = step.Forward ? edge.Start : edge.End;
			var end = step.Forward ? edge.End : edge.Start;
			Kind kind = edge.Kind != "Line" ? Kind.Oblique
				: Math.Abs(end.Y - start.Y) <= tolerance ? Kind.Horizontal
				: Math.Abs(end.X - start.X) <= tolerance ? Kind.Vertical
				: Kind.Oblique;
			walk.Add(new Face { Kind = kind, Start = start, End = end });
		}
		double orientation = SignedArea(walk);
		if (Math.Abs(orientation) <= tolerance) { return NotL("DegenerateBoundary"); }

		var faces = MergeCollinear(walk, tolerance);
		// 斜边、圆弧：两侧是一横一竖就补成尖角；两侧平行且错开就换成一个台阶立面；其余情况不判 L 型。
		var normalized = new List<Face>();
		for (int i = 0; i < faces.Count; i++)
		{
			var face = faces[i];
			if (face.Kind != Kind.Oblique) { normalized.Add(face); continue; }
			var before = faces[(i + faces.Count - 1) % faces.Count];
			var after = faces[(i + 1) % faces.Count];
			if (before.Kind == Kind.Oblique || after.Kind == Kind.Oblique) { return NotL("ConsecutiveCornerTreatments"); }
			if (before.Kind != after.Kind) { continue; }
			if (Math.Abs(before.Coordinate - after.Coordinate) <= tolerance) { return NotL("CurveBetweenCollinearFaces"); }
			// 夹在两个平行面之间的倒角、圆弧是台阶立面（用户确认的倒角规则），换成一个立面参与计数。
			normalized.Add(new Face { Kind = before.Kind == Kind.Horizontal ? Kind.Vertical : Kind.Horizontal, Start = face.Start, End = face.End, Riser = true });
		}
		faces = MergeCollinear(normalized, tolerance);
		int count = faces.Count;
		for (int i = 0; i < count; i++)
		{
			if (faces[i].Kind == faces[(i + 1) % count].Kind) { return NotL("NonAlternatingFaces|Faces=" + count); }
		}
		// 内凹角：相邻两面的转向与外轮廓的绕向相反。
		var concave = new List<int>();
		for (int i = 0; i < count; i++)
		{
			var a = Direction(faces[i]);
			var b = Direction(faces[(i + 1) % count]);
			if (Math.Sign(a.X * b.Y - a.Y * b.X) != Math.Sign(orientation)) { concave.Add(i); }
		}
		string counts = "Faces=" + count + "|Concave=" + concave.Count;
		if (count == 6 && concave.Count == 1)
		{
			return WithInnerArms(new LProfileShape { Kind = LProfileShapeKind.Plain, Evidence = "LProfileShape=Plain|" + counts }, faces, concave[0], (concave[0] + 1) % count);
		}
		if (count != 8 || concave.Count != 2) { return NotL(counts); }

		// 两个内凹角之间应只隔台阶的两个面：armA → s1 → s2 → armB。
		int first = concave[0], second = concave[1];
		if ((second - first + count) % count != 2) { (first, second) = (second, first); }
		if ((second - first + count) % count != 2) { return NotL(counts + "|ConcaveTurnsNotAdjacent"); }
		var armA = faces[first];
		var s1 = faces[(first + 1) % count];
		var s2 = faces[(first + 2) % count];
		var armB = faces[(first + 3) % count];
		if (OnEnvelope(s1, graph, tolerance) || OnEnvelope(s2, graph, tolerance)) { return NotL(counts + "|StepOnEnvelope"); }
		// 由倒角构成的台阶是真实的台阶层（例如两级 C5 台阶），不是内角处的局部小台阶。
		if (armA.Riser || s1.Riser || s2.Riser || armB.Riser) { return NotL(counts + "|StepFormedByChamferRiser"); }
		double depth = Math.Min(Math.Abs(s2.Coordinate - armA.Coordinate), Math.Abs(s1.Coordinate - armB.Coordinate));
		double arm = Math.Min(ArmThickness(armA, orientation, graph), ArmThickness(armB, orientation, graph));
		string stepEvidence = counts + "|StepDepth=" + Format(depth) + "|ArmThickness=" + Format(arm) + "|MaxRatio=" + Format(maxStepDepthRatio);
		if (!(arm > tolerance) || depth > maxStepDepthRatio * arm + tolerance) { return NotL(stepEvidence + "|StepTooDeep"); }
		return WithInnerArms(new LProfileShape { Kind = LProfileShapeKind.LocalStep, Evidence = "LProfileShape=LocalStep|" + stepEvidence }, faces, first, (first + 3) % count);
	}

	// armA 在前、armB 在后，二者互相垂直；虚拟尖角在两条臂面所在直线的交点。
	private static LProfileShape WithInnerArms(LProfileShape shape, IList<Face> faces, int armA, int armB)
	{
		int count = faces.Count;
		double lengthA = Math.Abs(faces[(armA + count - 1) % count].Coordinate - faces[armB].Coordinate);
		double lengthB = Math.Abs(faces[(armB + 1) % count].Coordinate - faces[armA].Coordinate);
		bool aHorizontal = faces[armA].Kind == Kind.Horizontal;
		shape.InnerHorizontalLength = aHorizontal ? lengthA : lengthB;
		shape.InnerVerticalLength = aHorizontal ? lengthB : lengthA;
		shape.Evidence += "|InnerH=" + Format(shape.InnerHorizontalLength) + "|InnerV=" + Format(shape.InnerVerticalLength);
		return shape;
	}

	private static LProfileShape NotL(string reason)
		=> new LProfileShape { Kind = LProfileShapeKind.NotLProfile, Evidence = "LProfileShape=NotLProfile|" + reason };

	private static List<Face> MergeCollinear(IList<Face> faces, double tol)
	{
		var result = new List<Face>();
		foreach (var face in faces)
		{
			var last = result.LastOrDefault();
			if (last != null && SameLine(last, face, tol)) { last.End = face.End; last.Riser |= face.Riser; }
			else { result.Add(new Face { Kind = face.Kind, Start = face.Start, End = face.End, Riser = face.Riser }); }
		}
		// 外轮廓是一个环：首尾两段在同一条线上时也要合并。
		if (result.Count > 1 && SameLine(result[result.Count - 1], result[0], tol))
		{
			result[0].Start = result[result.Count - 1].Start;
			result.RemoveAt(result.Count - 1);
		}
		return result;
	}

	private static bool SameLine(Face a, Face b, double tol)
		=> a.Kind == b.Kind && a.Kind != Kind.Oblique && Math.Abs(a.Coordinate - b.Coordinate) <= tol;

	private static Point2D Direction(Face face)
		=> face.Kind == Kind.Horizontal ? new Point2D(Math.Sign(face.End.X - face.Start.X), 0) : new Point2D(0, Math.Sign(face.End.Y - face.Start.Y));

	// 臂厚：从内侧面沿实体一侧量到外包络框的距离。逆时针时实体在前进方向左侧。
	private static double ArmThickness(Face face, double orientation, OutlineTopologyDiagnostic graph)
	{
		var d = Direction(face);
		double nx = orientation > 0 ? -d.Y : d.Y, ny = orientation > 0 ? d.X : -d.X;
		return face.Kind == Kind.Horizontal
			? (ny > 0 ? graph.MaxY - face.Coordinate : face.Coordinate - graph.MinY)
			: (nx > 0 ? graph.MaxX - face.Coordinate : face.Coordinate - graph.MinX);
	}

	private static bool OnEnvelope(Face face, OutlineTopologyDiagnostic graph, double tol)
		=> face.Kind == Kind.Horizontal
			? Math.Abs(face.Coordinate - graph.MinY) <= tol || Math.Abs(face.Coordinate - graph.MaxY) <= tol
			: Math.Abs(face.Coordinate - graph.MinX) <= tol || Math.Abs(face.Coordinate - graph.MaxX) <= tol;

	private static double SignedArea(IList<Face> walk)
	{
		double area = 0;
		foreach (var f in walk) { area += f.Start.X * f.End.Y - f.End.X * f.Start.Y; }
		return area / 2;
	}

	private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
