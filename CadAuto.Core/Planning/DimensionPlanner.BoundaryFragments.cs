using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;

namespace CadAuto.Core.Planning;

public sealed partial class DimensionPlanner
{
	private const string CollinearBranchFragmentRule = "Structure.CollinearBoundaryBranchFragment";

	private void SuppressCollinearExteriorBranchFragments(DimensionPlan plan, OutlineFeature2D outline)
	{
		var graph = plan.Diagnostics.OutlineTopology;
		var ownership = new StructureBoundaryEvidence(graph, _config.GeometryTolerance);
		if (!ownership.IsAvailable) { return; }
		double tol = Math.Max(Math.Abs(_config.GeometryTolerance), 1E-9);
		foreach (var candidate in plan.Dimensions.ToList())
		{
			if (!candidate.RequiresOuterBoundarySupport || StructureCoveragePolicy.GetIneligibilityReason(candidate) != null) { continue; }
			bool horizontal = candidate.Orientation == DimensionOrientation.Horizontal;
			if (horizontal ? candidate.Side != DimensionSide.Top && candidate.Side != DimensionSide.Bottom
				: candidate.Side != DimensionSide.Left && candidate.Side != DimensionSide.Right) { continue; }
			Func<Point2D, double> axis = p => horizontal ? p.X : p.Y;
			Func<Point2D, double> cross = p => horizontal ? p.Y : p.X;
			double crossPosition = cross(candidate.FirstPoint);
			if (Math.Abs(cross(candidate.SecondPoint) - crossPosition) > tol) { continue; }
			double crossMin = horizontal ? outline.MinY : outline.MinX;
			double crossMax = horizontal ? outline.MaxY : outline.MaxX;
			bool onMin = Math.Abs(crossPosition - crossMin) <= tol;
			bool onMax = Math.Abs(crossPosition - crossMax) <= tol;
			if (onMin == onMax) { continue; }
			Func<OutlineTopologyEdge, bool> straight = e => e.Kind == "Line" && e.StartNode != e.EndNode
				&& Math.Abs(cross(e.Start) - crossPosition) <= tol && Math.Abs(cross(e.End) - crossPosition) <= tol
				&& Math.Abs(axis(e.End) - axis(e.Start)) > tol;
			Func<Point2D, Point2D, bool> same = (a, b) => a.DistanceTo(b) <= tol;
			var seeds = graph.Edges.Where(e => e.IsBoundary && straight(e)
				&& candidate.SourceGeometryIds.Contains(e.SourceKey)
				&& ((same(candidate.FirstPoint, e.Start) && same(candidate.SecondPoint, e.End))
					|| (same(candidate.FirstPoint, e.End) && same(candidate.SecondPoint, e.Start)))).ToList();
			if (seeds.Count != 1) { continue; }
			// Only the raw edge / its outer-tip promotion is redundant here. An independently
			// recovered groove, boss or ledge may carry additional feature intent.
			if (candidate.SourceKey != seeds[0].SourceKey && candidate.SourceKey != "OuterTip:" + seeds[0].SourceKey) { continue; }

			var chain = new List<OutlineTopologyEdge> { seeds[0] };
			var queue = new Queue<OutlineTopologyEdge>(chain);
			var junctions = new HashSet<int>();
			var branches = new List<OutlineTopologyEdge>();
			bool ambiguous = false;
			while (queue.Count > 0 && !ambiguous)
			{
				var edge = queue.Dequeue();
				foreach (int node in new[] { edge.StartNode, edge.EndNode })
				{
					var incident = graph.Edges.Where(e => e.IsBoundary && (e.StartNode == node || e.EndNode == node)).ToList();
					if (incident.Count != 2) { ambiguous = true; break; }
					var next = incident.Single(e => e.Id != edge.Id);
					if (!straight(next)) { continue; } // A true corner/curve ends the straight chain.
					Point2D at = edge.StartNode == node ? edge.Start : edge.End;
					Point2D far = edge.StartNode == node ? edge.End : edge.Start;
					Point2D nextFar = next.StartNode == node ? next.End : next.Start;
					if ((axis(far) - axis(at)) * (axis(nextFar) - axis(at)) >= -tol * tol)
					{ ambiguous = true; break; } // Overlap/backtracking is not a continuous side.
					var internalEdges = graph.Edges.Where(e => !e.IsBoundary && (e.StartNode == node || e.EndNode == node)).ToList();
					if (internalEdges.Count > 0)
					{
						// Only a real line leaving the envelope inward proves this kind of T-junction.
						if (internalEdges.Any(e => e.Kind != "Line" || (onMin
							? cross(e.StartNode == node ? e.End : e.Start) <= crossPosition + tol
							: cross(e.StartNode == node ? e.End : e.Start) >= crossPosition - tol)))
						{ ambiguous = true; break; }
						junctions.Add(node);
						branches.AddRange(internalEdges);
					}
					if (chain.All(e => e.Id != next.Id)) { chain.Add(next); queue.Enqueue(next); }
				}
			}
			if (ambiguous || chain.Count < 2
				|| !junctions.Any(n => n == seeds[0].StartNode || n == seeds[0].EndNode)) { continue; }
			double minimum = chain.Min(e => Math.Min(axis(e.Start), axis(e.End)));
			double maximum = chain.Max(e => Math.Max(axis(e.Start), axis(e.End)));
			if (Math.Abs(minimum - (horizontal ? outline.MinX : outline.MinY)) > tol
				|| Math.Abs(maximum - (horizontal ? outline.MaxX : outline.MaxY)) > tol) { continue; }
			var provider = plan.Dimensions.FirstOrDefault(d => d.Kind == (horizontal ? DimensionKind.OverallWidth : DimensionKind.OverallHeight)
				&& d.Orientation == candidate.Orientation
				&& Math.Abs(Math.Min(axis(d.FirstPoint), axis(d.SecondPoint)) - minimum) <= tol
				&& Math.Abs(Math.Max(axis(d.FirstPoint), axis(d.SecondPoint)) - maximum) <= tol
				&& ownership.ValidateBoundaryAttachments(d.FirstPoint, d.SecondPoint) == true);
			if (provider == null) { continue; }
			var supporting = chain.Concat(branches).Select(e => e.SourceKey).Where(k => !string.IsNullOrEmpty(k)).Distinct();
			string proof = CollinearBranchFragmentRule + "|Relation=InternalBranchOnContinuousExterior"
				+ "|BoundaryEdges=" + string.Join(",", chain.Select(e => e.Id).OrderBy(id => id))
				+ "|InternalBranches=" + string.Join(",", branches.Select(e => e.Id).Distinct().OrderBy(id => id))
				+ "|StraightJunctionNodes=" + string.Join(",", junctions.OrderBy(id => id))
				+ "|Axis=" + candidate.Orientation + "|CompleteEnvelopeSide=True|RealBoundary=True";
			plan.TrySuppressCoveredStructure(candidate, CollinearBranchFragmentRule, new[] { provider }, supporting, proof);
		}
	}
}
