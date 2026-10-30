using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;

namespace CadAuto.Core.Planning;

public sealed class BoundaryLineRun
{
	public Point2D Start { get; set; }
	public Point2D End { get; set; }
	public List<OutlineTopologyEdge> Edges { get; } = new List<OutlineTopologyEdge>();
	public OutlineTopologyEdge Previous { get; set; }
	public OutlineTopologyEdge Next { get; set; }
}

public static class BoundaryLineRuns
{
	public static IDictionary<Segment2D, BoundaryLineRun> MapAxisSegments(OutlineTopologyDiagnostic graph, OutlineFeature2D outline, double tolerance)
	{
		var result = new Dictionary<Segment2D, BoundaryLineRun>();
		foreach (var run in Find(graph, tolerance))
		{
			var full = new Segment2D(run.Start, run.End);
			if (!full.IsHorizontal(tolerance) && !full.IsVertical(tolerance)) { continue; }
			var matches = run.Edges.Select(e => outline.Segments.Where(s => s != null && !s.IsArcChord
				&& e.SourceKeys.Contains(s.SourceKey)
				&& ((s.Start.DistanceTo(e.Start) <= tolerance && s.End.DistanceTo(e.End) <= tolerance)
					|| (s.Start.DistanceTo(e.End) <= tolerance && s.End.DistanceTo(e.Start) <= tolerance))).ToList()).ToList();
			if (matches.Any(m => m.Count != 1) || matches.Select(m => m[0]).Distinct().Count() != run.Edges.Count) { continue; }
			foreach (var match in matches) { result[match[0]] = run; }
		}
		return result;
	}

	public static IList<BoundaryLineRun> Find(OutlineTopologyDiagnostic graph, double tolerance)
	{
		var result = new List<BoundaryLineRun>();
		if (graph == null || graph.Status != "UniqueExteriorWalk" || !graph.IsClosedWalk || graph.HasRepeatedBoundaryEdges
			|| !(tolerance > 0) || graph.BoundaryPath.Count < 3) { return result; }
		var steps = graph.BoundaryPath.OrderBy(s => s.Order).ToList();
		var edges = graph.Edges.ToDictionary(e => e.Id);
		if (steps.Any(s => !edges.ContainsKey(s.EdgeId) || !edges[s.EdgeId].IsBoundary)) { return result; }
		int count = steps.Count;
		int start = Enumerable.Range(0, count).FirstOrDefault(i => !CanJoin(graph, steps[(i + count - 1) % count], steps[i], edges, tolerance));
		var run = new List<OutlineTopologyStep>();
		for (int n = 0; n < count; n++)
		{
			int i = (start + n) % count;
			var step = steps[i];
			if (run.Count > 0 && (!CanJoin(graph, run[run.Count - 1], step, edges, tolerance)
				|| DistanceFromLine(End(step, edges), Start(run[0], edges), End(run[0], edges)) > tolerance))
			{
				AddRun(result, steps, edges, run, start, n - run.Count, tolerance);
				run.Clear();
			}
			run.Add(step);
		}
		AddRun(result, steps, edges, run, start, count - run.Count, tolerance);
		return result;
	}

	private static void AddRun(List<BoundaryLineRun> result, IList<OutlineTopologyStep> steps,
		IDictionary<int, OutlineTopologyEdge> edges, IList<OutlineTopologyStep> run, int start, int offset, double tolerance)
	{
		if (run.Count < 2) { return; }
		var first = Start(run[0], edges); var last = End(run[run.Count - 1], edges);
		if (first.DistanceTo(last) <= tolerance) { return; }
		var item = new BoundaryLineRun { Start = first, End = last,
			Previous = edges[steps[(start + offset + steps.Count - 1) % steps.Count].EdgeId],
			Next = edges[steps[(start + offset + run.Count) % steps.Count].EdgeId] };
		item.Edges.AddRange(run.Select(s => edges[s.EdgeId]));
		result.Add(item);
	}

	private static bool CanJoin(OutlineTopologyDiagnostic graph, OutlineTopologyStep first, OutlineTopologyStep second,
		IDictionary<int, OutlineTopologyEdge> edges, double tolerance)
	{
		var a = edges[first.EdgeId]; var b = edges[second.EdgeId];
		if (a.Kind != "Line" || b.Kind != "Line" || first.ToNode != second.FromNode
			|| graph.Nodes.Count(n => n.Id == first.ToNode && n.Degree == 2 && n.IsBoundary) != 1) { return false; }
		Point2D p = Start(first, edges), q = End(first, edges), r = End(second, edges);
		double ux = q.X - p.X, uy = q.Y - p.Y, vx = r.X - q.X, vy = r.Y - q.Y;
		double u = Math.Sqrt(ux * ux + uy * uy), v = Math.Sqrt(vx * vx + vy * vy);
		return u > tolerance && v > tolerance && ux * vx + uy * vy > 0
			&& Math.Abs(ux * vy - uy * vx) <= tolerance * Math.Min(u, v)
			&& End(first, edges).DistanceTo(Start(second, edges)) <= tolerance;
	}

	private static Point2D Start(OutlineTopologyStep s, IDictionary<int, OutlineTopologyEdge> e) => s.Forward ? e[s.EdgeId].Start : e[s.EdgeId].End;
	private static Point2D End(OutlineTopologyStep s, IDictionary<int, OutlineTopologyEdge> e) => s.Forward ? e[s.EdgeId].End : e[s.EdgeId].Start;
	private static double DistanceFromLine(Point2D p, Point2D a, Point2D b)
	{
		double dx = b.X - a.X, dy = b.Y - a.Y, length = a.DistanceTo(b);
		return length == 0 ? double.PositiveInfinity : Math.Abs((p.X - a.X) * dy - (p.Y - a.Y) * dx) / length;
	}
}
