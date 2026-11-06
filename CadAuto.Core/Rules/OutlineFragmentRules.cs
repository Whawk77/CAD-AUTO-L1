using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;

namespace CadAuto.Core.Rules;

public sealed class OutlineFragmentMergeResult
{
	public OutlineFeature2D Outline { get; set; }

	// 每条记录形如 OutlineFragment.Merged|Kind=Line|Sources=A+B|Join=(x,y)
	public List<string> Records { get; } = new List<string>();
}

// 断开但端点重合的碎段，在规划时视为一条边。只改插件内存中的模型，不改 CAD 图纸。
// 合并条件：
// - 相接点只连着这两段，没有第三条线的端点或穿过的线（无分支）；
// - 直线：方向相同，相接点和此前吸收的所有断点都在合并后直线的公差内（真实转折、缓慢弯曲不合并）；
// - 圆弧：圆心、半径在公差内相同，转向相同，合并后不超过整圆。
// 直线与圆弧相接、端点之间有缝隙的情况都不合并。
public static class OutlineFragmentRules
{
	public static OutlineFragmentMergeResult Merge(OutlineFeature2D outline, double tolerance, Func<double, string> formatNumber = null)
	{
		var result = new OutlineFragmentMergeResult { Outline = outline };
		if (outline == null || !(tolerance > 0)) { return result; }
		var segments = outline.Segments.Where(s => s != null).ToList();
		var arcs = outline.Arcs.Where(a => a != null).ToList();
		var absorbed = new Dictionary<object, List<Point2D>>();
		var pieces = new Dictionary<object, List<object>>();
		var joins = new List<Point2D>();
		for (bool changed = true; changed;)
		{
			changed = false;
			foreach (var point in CandidateJoins(segments, arcs, tolerance))
			{
				if (TryMergeAt(point, segments, arcs, absorbed, pieces, tolerance, out string record))
				{
					result.Records.Add(record);
					joins.Add(point);
					changed = true;
					break;
				}
			}
		}
		if (result.Records.Count == 0) { return result; }
		result.Outline = Rebuild(outline, segments, arcs, pieces, joins, tolerance, formatNumber);
		return result;
	}

	private static IEnumerable<Point2D> CandidateJoins(IList<Segment2D> segments, IList<Arc2D> arcs, double tol)
	{
		var points = new List<Point2D>();
		foreach (var p in segments.Where(IsLine).SelectMany(s => new[] { s.Start, s.End }).Concat(arcs.Where(IsArc).SelectMany(a => new[] { a.Start, a.End })))
		{
			if (!points.Any(q => q.DistanceTo(p) <= tol)) { points.Add(p); }
		}
		return points;
	}

	private static bool TryMergeAt(Point2D point, List<Segment2D> segments, List<Arc2D> arcs,
		Dictionary<object, List<Point2D>> absorbed, Dictionary<object, List<object>> pieces, double tol, out string record)
	{
		record = null;
		// 统计所有图元（含内部线）在该点的端点数和穿过数，任何第三者都视为分支。
		// 圆弧的弦线（IsArcChord）只是圆弧的辅助表示，不计入。
		var lineEnds = segments.Where(s => IsLine(s) && (s.Start.DistanceTo(point) <= tol || s.End.DistanceTo(point) <= tol)).ToList();
		var arcEnds = arcs.Where(a => a.Start.DistanceTo(point) <= tol || a.End.DistanceTo(point) <= tol).ToList();
		if (lineEnds.Count + arcEnds.Count != 2) { return false; }
		if (segments.Any(s => IsLine(s) && !lineEnds.Contains(s) && DistanceToSegment(point, s) <= tol)) { return false; }
		if (lineEnds.Count == 2)
		{
			var a = lineEnds[0]; var b = lineEnds[1];
			if (!IsLine(a) || !IsLine(b)) { return false; }
			Point2D far0 = a.Start.DistanceTo(point) <= tol ? a.End : a.Start;
			Point2D far1 = b.Start.DistanceTo(point) <= tol ? b.End : b.Start;
			double ux = point.X - far0.X, uy = point.Y - far0.Y, vx = far1.X - point.X, vy = far1.Y - point.Y;
			if (ux * vx + uy * vy <= 0) { return false; }
			var inner = new List<Point2D> { point };
			inner.AddRange(Absorbed(absorbed, a)); inner.AddRange(Absorbed(absorbed, b));
			if (inner.Any(p => DistanceToLine(p, far0, far1) > tol)) { return false; }
			// 合并后线段保持第一段的方向，便于保留原有走向。
			bool keepForward = a.End.DistanceTo(point) <= tol;
			var merged = new Segment2D(keepForward ? far0 : far1, keepForward ? far1 : far0)
			{
				SourceKey = a.SourceKey,
				SourceKeys = Sources(a.SourceKeys, a.SourceKey).Concat(Sources(b.SourceKeys, b.SourceKey)).Distinct().ToList()
			};
			absorbed[merged] = inner;
			pieces[merged] = Pieces(pieces, a).Concat(Pieces(pieces, b)).ToList();
			segments.Remove(a); segments.Remove(b); segments.Add(merged);
			record = Record("Line", merged.SourceKeys, point);
			return true;
		}
		if (arcEnds.Count == 2)
		{
			var a = arcEnds[0]; var b = arcEnds[1];
			if (!IsArc(a) || !IsArc(b) || a.Center.DistanceTo(b.Center) > tol || Math.Abs(a.Radius - b.Radius) > tol) { return false; }
			// 统一成 first 终点接 second 起点；反向圆弧的凸度取负。
			var first = a.End.DistanceTo(point) <= tol ? Oriented(a, false) : Oriented(a, true);
			var second = b.Start.DistanceTo(point) <= tol ? Oriented(b, false) : Oriented(b, true);
			if (Math.Sign(first.Bulge) != Math.Sign(second.Bulge)) { return false; }
			double sweep = 4 * Math.Atan(Math.Abs(first.Bulge)) + 4 * Math.Atan(Math.Abs(second.Bulge));
			if (sweep >= 2 * Math.PI - 1e-6) { return false; }
			var merged = new Arc2D
			{
				Start = first.Start, End = second.End, Center = a.Center, Radius = a.Radius,
				Bulge = Math.Sign(first.Bulge) * Math.Tan(sweep / 4), SourceKey = a.SourceKey,
				SourceKeys = Sources(a.SourceKeys, a.SourceKey).Concat(Sources(b.SourceKeys, b.SourceKey)).Distinct().ToList()
			};
			pieces[merged] = Pieces(pieces, a).Concat(Pieces(pieces, b)).ToList();
			arcs.Remove(a); arcs.Remove(b); arcs.Add(merged);
			// 碎段各自的弦线换成合并后圆弧的一条弦线。
			var chords = segments.Where(s => s != null && s.IsArcChord && (IsChordOf(s, a, tol) || IsChordOf(s, b, tol))).ToList();
			if (chords.Count > 0)
			{
				var chord = new Segment2D(merged.Start, merged.End) { SourceKey = chords[0].SourceKey, SourceKeys = merged.SourceKeys, IsArcChord = true };
				pieces[chord] = chords.SelectMany(c => Pieces(pieces, c)).ToList();
				foreach (var c in chords) { segments.Remove(c); }
				segments.Add(chord);
			}
			record = Record("Arc", merged.SourceKeys, point);
			return true;
		}
		return false;
	}

	private static OutlineFeature2D Rebuild(OutlineFeature2D source, List<Segment2D> segments, List<Arc2D> arcs,
		Dictionary<object, List<object>> pieces, List<Point2D> joins, double tol, Func<double, string> formatNumber)
	{
		var outline = new OutlineFeature2D { MinX = source.MinX, MaxX = source.MaxX, MinY = source.MinY, MaxY = source.MaxY };
		outline.Vertices.AddRange(source.Vertices.Where(v => !joins.Any(j => j.DistanceTo(v) <= tol)));
		// 保持原有顺序：未合并的图元原位保留，合并结果放在其第一个碎段的位置。
		foreach (var s in source.Segments)
		{
			var owner = segments.FirstOrDefault(m => ReferenceEquals(m, s) || (pieces.TryGetValue(m, out var p) && p.Contains(s)));
			if (owner != null && !outline.Segments.Contains(owner)) { outline.Segments.Add(owner); }
		}
		foreach (var a in source.Arcs)
		{
			var owner = arcs.FirstOrDefault(m => ReferenceEquals(m, a) || (pieces.TryGetValue(m, out var p) && p.Contains(a)));
			if (owner != null && !outline.Arcs.Contains(owner)) { outline.Arcs.Add(owner); }
		}
		foreach (var chamfer in source.Chamfers)
		{
			var merged = segments.FirstOrDefault(m => pieces.TryGetValue(m, out var p) && chamfer.SourceSegment != null && p.Contains(chamfer.SourceSegment));
			if (merged == null) { outline.Chamfers.Add(chamfer); continue; }
			bool full = (chamfer.StartPoint.DistanceTo(merged.Start) <= tol && chamfer.EndPoint.DistanceTo(merged.End) <= tol)
				|| (chamfer.StartPoint.DistanceTo(merged.End) <= tol && chamfer.EndPoint.DistanceTo(merged.Start) <= tol);
			var copy = new ChamferFeature2D
			{
				StartPoint = chamfer.StartPoint, EndPoint = chamfer.EndPoint, Length = chamfer.Length, DeltaX = chamfer.DeltaX,
				DeltaY = chamfer.DeltaY, Value = chamfer.Value, Text = chamfer.Text, SourceSegment = merged,
				SourceGeometryIds = chamfer.SourceGeometryIds.Concat(merged.SourceKeys).Distinct().ToList(), Confidence = chamfer.Confidence
			};
			if (!full)
			{
				// 只认出一个碎段的倒角，扩展到合并后的整条斜边。
				copy.StartPoint = merged.Start; copy.EndPoint = merged.End; copy.Length = merged.Length;
				copy.DeltaX = Math.Abs(merged.End.X - merged.Start.X); copy.DeltaY = Math.Abs(merged.End.Y - merged.Start.Y);
				copy.Value = Math.Max(copy.DeltaX, copy.DeltaY);
				if (formatNumber != null) { copy.Text = "C" + formatNumber(copy.Value); }
			}
			if (!outline.Chamfers.Any(c => ReferenceEquals(c.SourceSegment, merged))) { outline.Chamfers.Add(copy); }
		}
		foreach (var fillet in source.Fillets)
		{
			var merged = arcs.FirstOrDefault(m => pieces.TryGetValue(m, out var p) && fillet.SourceArc != null && p.Contains(fillet.SourceArc));
			if (merged == null) { outline.Fillets.Add(fillet); continue; }
			if (outline.Fillets.Any(f => ReferenceEquals(f.SourceArc, merged))) { continue; }
			outline.Fillets.Add(new FilletFeature2D
			{
				Center = merged.Center, Radius = merged.Radius, StartPoint = merged.Start, EndPoint = merged.End,
				Text = fillet.Text, SourceArc = merged, Confidence = fillet.Confidence
			});
		}
		return outline;
	}

	private static bool IsLine(Segment2D s) => s != null && !s.IsArcChord;

	private static bool IsChordOf(Segment2D chord, Arc2D arc, double tol)
		=> (chord.Start.DistanceTo(arc.Start) <= tol && chord.End.DistanceTo(arc.End) <= tol)
			|| (chord.Start.DistanceTo(arc.End) <= tol && chord.End.DistanceTo(arc.Start) <= tol);

	private static bool IsArc(Arc2D a) => a != null && a.Radius > 0 && Math.Abs(a.Bulge) > 1e-9;

	private static Arc2D Oriented(Arc2D arc, bool reverse) => reverse
		? new Arc2D { Start = arc.End, End = arc.Start, Center = arc.Center, Radius = arc.Radius, Bulge = -arc.Bulge }
		: arc;

	private static IEnumerable<Point2D> Absorbed(Dictionary<object, List<Point2D>> absorbed, object item)
		=> absorbed.TryGetValue(item, out var list) ? list : Enumerable.Empty<Point2D>();

	private static IEnumerable<object> Pieces(Dictionary<object, List<object>> pieces, object item)
		=> pieces.TryGetValue(item, out var list) ? list : new List<object> { item };

	private static IEnumerable<string> Sources(List<string> keys, string key)
		=> (keys ?? new List<string> { key }).Where(k => !string.IsNullOrEmpty(k));

	private static string Record(string kind, IEnumerable<string> sources, Point2D join)
		=> "OutlineFragment.Merged|Kind=" + kind + "|Sources=" + string.Join("+", sources)
			+ "|Join=(" + join.X.ToString("0.######", CultureInfo.InvariantCulture) + "," + join.Y.ToString("0.######", CultureInfo.InvariantCulture) + ")";

	private static double DistanceToLine(Point2D p, Point2D a, Point2D b)
	{
		double dx = b.X - a.X, dy = b.Y - a.Y, length = Math.Sqrt(dx * dx + dy * dy);
		return length == 0 ? p.DistanceTo(a) : Math.Abs((p.X - a.X) * dy - (p.Y - a.Y) * dx) / length;
	}

	private static double DistanceToSegment(Point2D p, Segment2D s)
	{
		double dx = s.End.X - s.Start.X, dy = s.End.Y - s.Start.Y, l2 = dx * dx + dy * dy;
		if (l2 == 0) { return p.DistanceTo(s.Start); }
		double t = Math.Max(0, Math.Min(1, ((p.X - s.Start.X) * dx + (p.Y - s.Start.Y) * dy) / l2));
		return p.DistanceTo(new Point2D(s.Start.X + t * dx, s.Start.Y + t * dy));
	}
}
