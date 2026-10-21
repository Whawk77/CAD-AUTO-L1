using System.Collections.Generic;
using CadAuto.Core.Geometry;

namespace CadAuto.Core.Planning;

/// <summary>A snapshot of the actual endpoint graph used by the planner, not a reconstructed hull.</summary>
public sealed class OutlineTopologyDiagnostic
{
	public string Status { get; set; } = "Unavailable";
	public string Reason { get; set; } = "NotCaptured";
	public string CoordinateSystem => "PlanningFrame";
	public string IntersectionHandling => "ExistingEndpointsOnly_SplitLineOverlaps_NoNewIntersections";
	public double Tolerance { get; set; }
	public double MinX { get; set; }
	public double MinY { get; set; }
	public double MaxX { get; set; }
	public double MaxY { get; set; }
	public int ExteriorFaceCount { get; set; }
	public bool IsClosedWalk { get; set; }
	public bool HasRepeatedBoundaryEdges { get; set; }
	public int IgnoredGeometryCount { get; set; }
	public List<OutlineTopologyNode> Nodes { get; } = new List<OutlineTopologyNode>();
	public List<OutlineTopologyEdge> Edges { get; } = new List<OutlineTopologyEdge>();
	public List<OutlineTopologyStep> BoundaryPath { get; } = new List<OutlineTopologyStep>();
}

public sealed class OutlineTopologyNode
{
	public int Id { get; set; }
	public Point2D Point { get; set; }
	public int Degree { get; set; }
	public string Kind { get; set; }
	public bool IsBoundary { get; set; }
}

public sealed class OutlineTopologyEdge
{
	public int Id { get; set; }
	public int StartNode { get; set; }
	public int EndNode { get; set; }
	public string Kind { get; set; }
	public string SourceKey { get; set; }
	public List<string> SourceKeys { get; set; } = new List<string>();
	public string SourceHandle { get; set; }
	public Point2D Start { get; set; }
	public Point2D End { get; set; }
	public Point2D Center { get; set; }
	public double Radius { get; set; }
	public double Bulge { get; set; }
	public bool IsBoundary { get; set; }
}

public sealed class OutlineTopologyStep
{
	public int Order { get; set; }
	public int EdgeId { get; set; }
	public int FromNode { get; set; }
	public int ToNode { get; set; }
	public bool Forward { get; set; }
}
