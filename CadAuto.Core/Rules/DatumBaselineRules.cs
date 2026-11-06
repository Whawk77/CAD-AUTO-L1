using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public sealed class DatumBaselineTarget
{
	public double Distance { get; set; }
	public string Reason { get; set; }
	public List<Point2D> Points { get; } = new List<Point2D>();
	public List<string> SourceGeometryIds { get; } = new List<string>();
}

public sealed class DatumBaselineAxis
{
	public bool Minimum { get; set; }
	public double Datum { get; set; }
	public double Span { get; set; }
	public List<Point2D> DatumPoints { get; } = new List<Point2D>();
	public List<string> DatumSources { get; } = new List<string>();
	public List<DatumBaselineTarget> Targets { get; } = new List<DatumBaselineTarget>();
	public List<double> Derived { get; } = new List<double>();
}

public sealed class DatumBaselineLayout
{
	public DatumBaselineAxis AlongX { get; } = new DatumBaselineAxis();
	public DatumBaselineAxis AlongY { get; } = new DatumBaselineAxis();
}

// Faces are measured from the datum edges; chamfer and fillet callouts supply everything else they imply.
// Targets exclude the far envelope edge, which the overall dimension already measures.
public static class DatumBaselineRules
{
	private enum PieceKind { Horizontal, Vertical, Chamfer, Fillet }

	private sealed class Piece
	{
		public Point2D Start;
		public Point2D End;
		public PieceKind Kind;
		public List<string> Sources;
	}

	public static bool Supports(OutlineTopologyDiagnostic graph, IEnumerable<ChamferFeature2D> chamfers, IEnumerable<FilletFeature2D> fillets,
		double tolerance, out string reason) => TryWalkSteppedBoundary(graph, chamfers, fillets, tolerance, out _, out reason);

	public static bool TryPlan(OutlineTopologyDiagnostic graph, IEnumerable<ChamferFeature2D> chamfers, IEnumerable<FilletFeature2D> fillets,
		bool minimumX, bool minimumY, double tolerance, out DatumBaselineLayout layout, out string reason)
	{
		layout = null;
		if (!TryWalkSteppedBoundary(graph, chamfers, fillets, tolerance, out var pieces, out reason)) { return false; }
		var result = new DatumBaselineLayout();
		if (!TryPlanAxis(pieces, true, minimumX, minimumX ? graph.MinX : graph.MaxX, graph.MaxX - graph.MinX, tolerance, result.AlongX, out reason)
			|| !TryPlanAxis(pieces, false, minimumY, minimumY ? graph.MinY : graph.MaxY, graph.MaxY - graph.MinY, tolerance, result.AlongY, out reason)) { return false; }
		layout = result;
		return true;
	}

	private static bool TryPlanAxis(IList<Piece> pieces, bool alongX, bool minimum, double datum, double span, double tol,
		DatumBaselineAxis axis, out string reason)
	{
		reason = null;
		axis.Minimum = minimum; axis.Datum = datum; axis.Span = span;
		var faceKind = alongX ? PieceKind.Vertical : PieceKind.Horizontal;
		var crossKind = alongX ? PieceKind.Horizontal : PieceKind.Vertical;
		Func<Point2D, double> distance = p => Math.Abs((alongX ? p.X : p.Y) - datum);
		Func<Piece, double> layer = p => Math.Abs(Coordinate(p) - datum);
		Func<double, bool> envelope = d => d <= tol || d >= span - tol;
		foreach (var face in pieces.Where(p => p.Kind == faceKind && layer(p) <= tol))
		{
			axis.DatumPoints.Add(face.Start); axis.DatumPoints.Add(face.End);
			axis.DatumSources.AddRange(face.Sources.Where(s => !axis.DatumSources.Contains(s)));
		}
		if (axis.DatumPoints.Count == 0)
		{
			reason = "NoDatumFace|Axis=" + (alongX ? "X" : "Y");
			return false;
		}
		var risers = new List<Piece>();
		var crossRisers = new List<Piece>();
		for (int i = 0; i < pieces.Count; i++)
		{
			if (pieces[i].Kind != PieceKind.Chamfer) { continue; }
			var before = pieces[(i + pieces.Count - 1) % pieces.Count];
			var after = pieces[(i + 1) % pieces.Count];
			// A chamfer that replaces a whole riser fixes the far layer as near layer + C.
			if (before.Kind == faceKind && after.Kind == faceKind)
			{
				double far = Math.Max(layer(before), layer(after));
				if (!axis.Derived.Any(d => Math.Abs(d - far) <= tol)) { axis.Derived.Add(far); }
			}
			else if (before.Kind == crossKind && after.Kind == crossKind) { crossRisers.Add(pieces[i]); }
		}
		foreach (var face in pieces.Where(p => p.Kind == faceKind).OrderBy(layer))
		{
			double d = layer(face);
			if (envelope(d) || axis.Derived.Any(v => Math.Abs(v - d) <= tol)) { continue; }
			var target = axis.Targets.FirstOrDefault(t => Math.Abs(t.Distance - d) <= tol);
			if (target == null)
			{
				target = new DatumBaselineTarget { Distance = d, Reason = "Face" };
				axis.Targets.Add(target);
			}
			target.Points.Add(face.Start); target.Points.Add(face.End);
			target.SourceGeometryIds.AddRange(face.Sources.Where(s => !target.SourceGeometryIds.Contains(s)));
		}
		// A chamfer between two parallel faces is located along those faces by its end farther from the datum.
		foreach (var chamfer in crossRisers)
		{
			Point2D far = distance(chamfer.Start) >= distance(chamfer.End) ? chamfer.Start : chamfer.End;
			double d = distance(far);
			if (envelope(d) || axis.Targets.Any(t => Math.Abs(t.Distance - d) <= tol) || axis.Derived.Any(v => Math.Abs(v - d) <= tol)) { continue; }
			var target = new DatumBaselineTarget { Distance = d, Reason = "ChamferFarEnd" };
			target.Points.Add(far);
			target.SourceGeometryIds.AddRange(chamfer.Sources);
			axis.Targets.Add(target);
		}
		axis.Targets.Sort((a, b) => a.Distance.CompareTo(b.Distance));
		return true;
	}

	private static bool TryWalkSteppedBoundary(OutlineTopologyDiagnostic graph, IEnumerable<ChamferFeature2D> chamfers,
		IEnumerable<FilletFeature2D> fillets, double tol, out List<Piece> pieces, out string reason)
	{
		if (!TryWalkBoundary(graph, (chamfers ?? Enumerable.Empty<ChamferFeature2D>()).Where(c => c != null).ToList(),
			(fillets ?? Enumerable.Empty<FilletFeature2D>()).Where(f => f != null).ToList(), tol, out pieces, out reason)) { return false; }
		if (!pieces.Any(p => p.Kind == PieceKind.Horizontal
				? Coordinate(p) > graph.MinY + tol && Coordinate(p) < graph.MaxY - tol
				: p.Kind == PieceKind.Vertical && Coordinate(p) > graph.MinX + tol && Coordinate(p) < graph.MaxX - tol))
		{
			reason = "NoInteriorLayer";
			return false;
		}
		return true;
	}

	private static bool TryWalkBoundary(OutlineTopologyDiagnostic graph, IList<ChamferFeature2D> chamfers, IList<FilletFeature2D> fillets,
		double tol, out List<Piece> pieces, out string reason)
	{
		pieces = new List<Piece>();
		reason = null;
		if (graph == null || graph.Status != "UniqueExteriorWalk" || !graph.IsClosedWalk || graph.HasRepeatedBoundaryEdges
			|| graph.BoundaryPath.Count < 3 || !(tol > 0))
		{
			reason = "NoUniqueClosedBoundary";
			return false;
		}
		var edges = graph.Edges.ToDictionary(e => e.Id);
		var runOf = new Dictionary<int, BoundaryLineRun>();
		foreach (var run in BoundaryLineRuns.Find(graph, tol)) { foreach (var edge in run.Edges) { runOf[edge.Id] = run; } }
		var emitted = new HashSet<BoundaryLineRun>();
		foreach (var step in graph.BoundaryPath.OrderBy(s => s.Order))
		{
			if (!edges.TryGetValue(step.EdgeId, out var edge))
			{
				reason = "MissingBoundaryEdge";
				return false;
			}
			var piece = new Piece();
			List<OutlineTopologyEdge> members;
			if (runOf.TryGetValue(edge.Id, out var run))
			{
				// A run that wraps past the walk start is emitted once; the cyclic order is unchanged.
				if (!emitted.Add(run)) { continue; }
				members = run.Edges;
				piece.Start = run.Start; piece.End = run.End;
			}
			else
			{
				members = new List<OutlineTopologyEdge> { edge };
				piece.Start = step.Forward ? edge.Start : edge.End;
				piece.End = step.Forward ? edge.End : edge.Start;
			}
			piece.Sources = BoundaryLineRuns.SourceKeys(members);
			if (!TryClassify(piece, edge.Kind, members, chamfers, fillets, tol, out reason)) { return false; }
			pieces.Add(piece);
		}
		for (int i = 0; i < pieces.Count; i++)
		{
			var piece = pieces[i];
			if (piece.Kind != PieceKind.Chamfer && piece.Kind != PieceKind.Fillet) { continue; }
			var before = pieces[(i + pieces.Count - 1) % pieces.Count].Kind;
			var after = pieces[(i + 1) % pieces.Count].Kind;
			// Fillets are corner features only; an arc joining parallel faces is a rounded end, not a step corner.
			if (!IsFace(before) || !IsFace(after) || (piece.Kind == PieceKind.Fillet && before == after))
			{
				reason = "Unsupported" + piece.Kind + "Neighbors|Source=" + string.Join("+", piece.Sources);
				return false;
			}
		}
		return true;
	}

	private static bool TryClassify(Piece piece, string kind, IList<OutlineTopologyEdge> members, IList<ChamferFeature2D> chamfers,
		IList<FilletFeature2D> fillets, double tol, out string reason)
	{
		reason = null;
		string at = "|Source=" + string.Join("+", piece.Sources);
		if (kind == "Arc")
		{
			if (fillets.Any(f => PairMatches(f.StartPoint, f.EndPoint, piece.Start, piece.End, tol)))
			{
				piece.Kind = PieceKind.Fillet;
				return true;
			}
			reason = "UnrecognizedArc" + at;
			return false;
		}
		if (kind != "Line")
		{
			reason = "UnsupportedEdgeKind=" + kind + at;
			return false;
		}
		double dx = Math.Abs(piece.End.X - piece.Start.X), dy = Math.Abs(piece.End.Y - piece.Start.Y);
		if (dy <= tol) { piece.Kind = PieceKind.Horizontal; return true; }
		if (dx <= tol) { piece.Kind = PieceKind.Vertical; return true; }
		if (chamfers.Any(c => PairMatches(c.StartPoint, c.EndPoint, piece.Start, piece.End, tol)
			|| members.All(e => SplitBoundaryChamferRules.ContainsSegment(c, new Segment2D(e.Start, e.End) { SourceKey = e.SourceKey }, tol))))
		{
			piece.Kind = PieceKind.Chamfer;
			return true;
		}
		reason = "UnrecognizedSlope" + at;
		return false;
	}

	private static bool IsFace(PieceKind kind) => kind == PieceKind.Horizontal || kind == PieceKind.Vertical;

	private static double Coordinate(Piece face) => face.Kind == PieceKind.Horizontal
		? (face.Start.Y + face.End.Y) / 2 : (face.Start.X + face.End.X) / 2;

	private static bool PairMatches(Point2D a, Point2D b, Point2D c, Point2D d, double tol)
		=> (a.DistanceTo(c) <= tol && b.DistanceTo(d) <= tol) || (a.DistanceTo(d) <= tol && b.DistanceTo(c) <= tol);
}
