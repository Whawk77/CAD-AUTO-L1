using System;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text;
using CadAuto.Core.Geometry;

namespace CadAuto.Core.Planning;

/// <summary>Read-only views of the captured graph. Never closes gaps or substitutes a convex hull.</summary>
public static class OutlineTopologyExporter
{
	private static string Number(double value) => double.IsNaN(value) || double.IsInfinity(value)
		? "null" : value.ToString("R", CultureInfo.InvariantCulture);

	private static string Quote(string value)
	{
		var result = new StringBuilder("\"");
		foreach (char c in value ?? string.Empty)
		{
			if (c == '"' || c == '\\') { result.Append('\\').Append(c); }
			else if (c < 32) { result.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); }
			else { result.Append(c); }
		}
		return result.Append('"').ToString();
	}

	private static string Point(Point2D p) => "[" + Number(p.X) + "," + Number(p.Y) + "]";
	private static string Bool(bool value) => value ? "true" : "false";

	private static bool HasBoundaryClassification(OutlineTopologyDiagnostic graph) => graph.Status == "UniqueExteriorWalk"
		&& graph.IsClosedWalk && !graph.HasRepeatedBoundaryEdges;

	private static string Membership(OutlineTopologyDiagnostic graph, OutlineTopologyEdge edge) => !HasBoundaryClassification(graph)
		? "Unknown" : edge.IsBoundary ? "Boundary" : "NonBoundary";

	public static string ToJson(OutlineTopologyDiagnostic graph)
	{
		if (graph == null) { return "null"; }
		var b = new StringBuilder("{");
		b.Append("\"status\":").Append(Quote(graph.Status))
			.Append(",\"reason\":").Append(Quote(graph.Reason))
			.Append(",\"coordinateSystem\":").Append(Quote(graph.CoordinateSystem))
			.Append(",\"intersectionHandling\":").Append(Quote(graph.IntersectionHandling))
			.Append(",\"tolerance\":").Append(Number(graph.Tolerance))
			.Append(",\"bounds\":[").Append(Number(graph.MinX)).Append(',').Append(Number(graph.MinY))
			.Append(',').Append(Number(graph.MaxX)).Append(',').Append(Number(graph.MaxY)).Append(']')
			.Append(",\"exteriorFaceCount\":").Append(graph.ExteriorFaceCount)
			.Append(",\"isClosedWalk\":").Append(Bool(graph.IsClosedWalk))
			.Append(",\"hasRepeatedBoundaryEdges\":").Append(Bool(graph.HasRepeatedBoundaryEdges))
			.Append(",\"ignoredGeometryCount\":").Append(graph.IgnoredGeometryCount)
			.Append(",\"nodes\":[");
		b.Append(string.Join(",", graph.Nodes.Select(n => "{\"id\":" + n.Id + ",\"point\":" + Point(n.Point)
			+ ",\"degree\":" + n.Degree + ",\"kind\":" + Quote(n.Kind) + ",\"isBoundary\":" + Bool(n.IsBoundary) + "}")));
		b.Append("],\"edges\":[");
		b.Append(string.Join(",", graph.Edges.Select(e => "{\"id\":" + e.Id + ",\"startNode\":" + e.StartNode
			+ ",\"endNode\":" + e.EndNode + ",\"kind\":" + Quote(e.Kind) + ",\"sourceKey\":" + Quote(e.SourceKey)
			+ ",\"sourceHandle\":" + Quote(e.SourceHandle) + ",\"start\":" + Point(e.Start) + ",\"end\":" + Point(e.End)
			+ ",\"center\":" + (e.Kind == "Arc" ? Point(e.Center) : "null") + ",\"radius\":" + Number(e.Radius)
			+ ",\"bulge\":" + Number(e.Bulge) + ",\"isBoundary\":" + Bool(e.IsBoundary)
			+ ",\"boundaryMembership\":" + Quote(Membership(graph, e)) + "}")));
		b.Append("],\"boundaryPath\":[");
		b.Append(string.Join(",", graph.BoundaryPath.Select(s => "{\"order\":" + s.Order + ",\"edgeId\":" + s.EdgeId
			+ ",\"fromNode\":" + s.FromNode + ",\"toNode\":" + s.ToNode + ",\"forward\":" + Bool(s.Forward) + "}")));
		return b.Append("]}").ToString();
	}

	public static string ToSvg(OutlineTopologyDiagnostic graph, string runId)
	{
		if (graph == null) { throw new ArgumentNullException(nameof(graph)); }
		var points = graph.Edges.SelectMany(e => e.Kind == "Arc"
			? new[] { e.Start, e.End, new Point2D(e.Center.X - e.Radius, e.Center.Y - e.Radius),
				new Point2D(e.Center.X + e.Radius, e.Center.Y + e.Radius) }
			: new[] { e.Start, e.End }).ToList();
		double minX = points.Count == 0 ? 0 : points.Min(p => p.X);
		double minY = points.Count == 0 ? 0 : points.Min(p => p.Y);
		double maxX = points.Count == 0 ? 1 : points.Max(p => p.X);
		double maxY = points.Count == 0 ? 1 : points.Max(p => p.Y);
		double scale = Math.Min(1020.0 / Math.Max(maxX - minX, 1E-6), 420.0 / Math.Max(maxY - minY, 1E-6));
		Func<Point2D, Point2D> screen = p => new Point2D(80 + (p.X - minX) * scale, 530 - (p.Y - minY) * scale);
		Func<OutlineTopologyEdge, bool, string> path = (e, forward) =>
		{
			Point2D start = screen(forward ? e.Start : e.End), end = screen(forward ? e.End : e.Start);
			string d = "M " + Number(start.X) + " " + Number(start.Y);
			if (e.Kind == "Arc")
			{
				double sweep = 4 * Math.Atan(e.Bulge) * (forward ? 1 : -1);
				d += " A " + Number(e.Radius * scale) + " " + Number(e.Radius * scale)
					+ " 0 " + (Math.Abs(sweep) > Math.PI ? "1" : "0") + " " + (sweep > 0 ? "0" : "1");
			}
			else { d += " L"; }
			return d + " " + Number(end.X) + " " + Number(end.Y);
		};
		int height = 750 + (graph.Nodes.Count + graph.Edges.Count + graph.BoundaryPath.Count) * 22;
		var b = new StringBuilder();
		b.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"").Append(height)
			.Append("\" viewBox=\"0 0 1200 ").Append(height).Append("\"><rect width=\"100%\" height=\"100%\" fill=\"white\"/>")
			.Append("<defs><marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"8\" refY=\"5\" markerWidth=\"5\" markerHeight=\"5\" orient=\"auto-start-reverse\"><path d=\"M 0 0 L 10 5 L 0 10 z\" fill=\"#16834b\"/></marker><marker id=\"nonExteriorArrow\" viewBox=\"0 0 10 10\" refX=\"8\" refY=\"5\" markerWidth=\"5\" markerHeight=\"5\" orient=\"auto-start-reverse\"><path d=\"M 0 0 L 10 5 L 0 10 z\" fill=\"#e6b800\"/></marker></defs>");
		Action<double, double, string> text = (x, y, value) => b.Append("<text x=\"").Append(Number(x))
			.Append("\" y=\"").Append(Number(y)).Append("\" font-family=\"monospace\" font-size=\"13\">")
			.Append(SecurityElement.Escape(value ?? string.Empty)).Append("</text>");
		text(25, 25, "代码识别的外轮廓 / Planner outline evidence | run=" + runId);
		text(25, 47, graph.Status + " | " + graph.Reason + " | frame=" + graph.CoordinateSystem);
		text(25, 69, "绿色箭头：外轮廓及遍历方向；黄色箭头：非外轮廓实体及源方向；灰色虚线：归属未知；红点：悬空点；橙点：分支点。");
		text(25, 91, "Endpoint graph only: intersections are NOT split; a closed walk is NOT proof of a simple valid contour.");
		foreach (OutlineTopologyEdge edge in graph.Edges)
		{
			string membership = Membership(graph, edge);
			bool nonExterior = membership == "NonBoundary";
			b.Append("<path data-membership=\"").Append(membership).Append("\" d=\"").Append(path(edge, true))
				.Append(nonExterior ? "\" fill=\"none\" stroke=\"#e6b800\" stroke-width=\"2.5\" marker-end=\"url(#nonExteriorArrow)\"><title>"
					: "\" fill=\"none\" stroke=\"#999\" stroke-width=\"1.5\" stroke-dasharray=\"5 3\"><title>")
				.Append(SecurityElement.Escape("E" + edge.Id + " handle=" + edge.SourceHandle + " source=" + edge.SourceKey
					+ " membership=" + membership + (nonExterior ? " 非外轮廓实体" : ""))).Append("</title></path>");
		}
		foreach (OutlineTopologyStep step in graph.BoundaryPath.Where(s => HasBoundaryClassification(graph)))
		{
			OutlineTopologyEdge edge = graph.Edges.Single(e => e.Id == step.EdgeId);
			b.Append("<path d=\"").Append(path(edge, step.Forward)).Append("\" fill=\"none\" stroke=\"#16834b\" stroke-width=\"2.5\" marker-end=\"url(#arrow)\"/>");
		}
		foreach (OutlineTopologyEdge edge in graph.Edges)
		{
			Point2D mid = new Point2D((edge.Start.X + edge.End.X) / 2, (edge.Start.Y + edge.End.Y) / 2);
			if (edge.Kind == "Arc")
			{
				double a = Math.Atan2(edge.Start.Y - edge.Center.Y, edge.Start.X - edge.Center.X) + 2 * Math.Atan(edge.Bulge);
				mid = new Point2D(edge.Center.X + edge.Radius * Math.Cos(a), edge.Center.Y + edge.Radius * Math.Sin(a));
			}
			Point2D p = screen(mid);
			text(p.X + 5, p.Y - 6, "E" + edge.Id);
		}
		foreach (OutlineTopologyNode node in graph.Nodes)
		{
			Point2D p = screen(node.Point);
			string color = node.Kind == "Dangling" ? "#d22" : node.Kind == "Branch" ? "#e78a00" : "#2266bb";
			b.Append("<circle cx=\"").Append(Number(p.X)).Append("\" cy=\"").Append(Number(p.Y)).Append("\" r=\"4\" fill=\"").Append(color).Append("\"/>");
			text(p.X + 6, p.Y + 16, "N" + node.Id);
		}
		text(25, 565, "Closed walk=" + graph.IsClosedWalk + "; repeated boundary edges=" + graph.HasRepeatedBoundaryEdges
			+ "; tolerance=" + Number(graph.Tolerance) + "; ignored geometry=" + graph.IgnoredGeometryCount);
		int row = 590;
		text(25, row, "Boundary walk (actual traversal order, not a sorted point list):");
		foreach (OutlineTopologyStep step in graph.BoundaryPath)
		{
			text(25, row += 22, step.Order + ": N" + step.FromNode + " -> N" + step.ToNode + " via E" + step.EdgeId);
		}
		text(25, row += 30, "Nodes (coordinates in the report's planning frame):");
		foreach (OutlineTopologyNode node in graph.Nodes)
		{
			text(25, row += 22, "N" + node.Id + " " + Point(node.Point) + " degree=" + node.Degree + " " + node.Kind);
		}
		text(25, row += 30, "Edges (非外轮廓实体 does not imply a recognized internal functional feature; unknown remains unknown):");
		foreach (OutlineTopologyEdge edge in graph.Edges)
		{
			text(25, row += 22, "E" + edge.Id + " N" + edge.StartNode + " -> N" + edge.EndNode + " " + edge.Kind
				+ " handle=" + edge.SourceHandle + " " + (Membership(graph, edge) == "NonBoundary" ? "NON_EXTERIOR_ENTITY 非外轮廓实体"
					: Membership(graph, edge) == "Boundary" ? "BOUNDARY" : "UNKNOWN"));
		}
		return b.Append("</svg>").ToString();
	}
}
