using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;

namespace CadAuto.Core.Planning;

public sealed class OrthographicViewPlan
{
	public OrthographicViewFrame Frame { get; set; }
	public DimensionPlan Plan { get; set; }
}

public sealed class OrthographicDimensionCoverage
{
	public OrthographicViewPlan TargetView { get; internal set; }
	public PlannedDimension Target { get; internal set; }
	public OrthographicViewPlan ProviderView { get; internal set; }
	public PlannedDimension Provider { get; internal set; }
	public PartExtentKind Extent { get; internal set; }
	public string Evidence { get; internal set; }
	public bool Applied { get; internal set; }
}

public static partial class OrthographicViewSetAssigner
{
	public const string ProjectionRuleId = "CrossView.ProjectedInterval";

	public static IList<OrthographicDimensionCoverage> FindProjectedCoverage(IReadOnlyList<OrthographicViewPlan> views, double tolerance)
	{
		if (views == null) { throw new ArgumentNullException(nameof(views)); }
		if (double.IsNaN(tolerance) || double.IsInfinity(tolerance) || tolerance <= 0) { throw new ArgumentOutOfRangeException(nameof(tolerance)); }
		if (views.Any(v => v == null || v.Frame == null || v.Plan == null))
		{ throw new ArgumentException("Projection coverage requires complete plans.", nameof(views)); }
		var ordered = views.OrderBy(v => RoleRank(v.Frame.Role)).ToList();
		if (ordered.Count < 2 || ordered.Count > 3
			|| ordered.Any(v => v.Frame.Role == OrthographicViewRole.Unassigned)
			|| ordered.Select(v => v.Frame.Role).Distinct().Count() != ordered.Count
			|| ordered.Any(v => string.IsNullOrEmpty(v.Frame.Id))
			|| ordered.Select(v => v.Frame.Id).Distinct(StringComparer.Ordinal).Count() != ordered.Count)
		{ throw new ArgumentException("Projection coverage requires distinct assigned views and complete plans.", nameof(views)); }
		foreach (var view in ordered.Where(v => (v.Plan.CoordinateFrame != null && !v.Plan.CoordinateFrame.IsIdentity)
			|| !new StructureBoundaryEvidence(v.Plan.Diagnostics.OutlineTopology, tolerance).IsAvailable))
		{ ProjectionWarning(view.Plan, "UnverifiedCoordinateFrameOrBoundary"); }
		var result = new List<OrthographicDimensionCoverage>();
		for (int index = 1; index < ordered.Count; index++)
		{
			var target = ordered[index];
			foreach (var provider in ordered.Take(index))
			{
				if (!TryProjectionAxes(provider.Frame, target.Frame, ordered, tolerance, out bool providerHorizontal,
					out bool targetHorizontal, out PartExtentKind extent))
				{
					ProjectionWarning(target.Plan, "UnverifiedAlignmentOrScale|OtherView=" + provider.Frame.Id);
					continue;
				}
				foreach (var candidate in target.Plan.Dimensions)
				{
					if (!CanProject(target, candidate, targetHorizontal, tolerance)) { continue; }
					var matches = provider.Plan.Dimensions.Where(d => CanProject(provider, d, providerHorizontal, tolerance)
						&& EquivalentInterval(provider.Frame, d, target.Frame, candidate, extent, tolerance)).ToList();
					if (matches.Count == 0) { continue; }
					int targets = target.Plan.Dimensions.Count(d => CanProject(target, d, targetHorizontal, tolerance)
						&& EquivalentInterval(provider.Frame, matches[0], target.Frame, d, extent, tolerance));
					if (matches.Count != 1 || targets != 1)
					{
						ProjectionWarning(target.Plan, "AmbiguousFeatureInterval|CandidateId=" + candidate.DiagnosticId);
						continue;
					}
					var owner = matches[0];
					result.Add(new OrthographicDimensionCoverage { TargetView = target, Target = candidate,
						ProviderView = provider, Provider = owner, Extent = extent,
						Evidence = ProjectionRuleId + "|Extent=" + extent + "|View=" + target.Frame.Id
							+ "|CandidateId=" + candidate.DiagnosticId + "|CoveredByView=" + provider.Frame.Id
							+ "|CoveredById=" + owner.DiagnosticId + "|RealBoundaryPlanes=True|Interval="
							+ ProjectCoordinate(target.Frame, candidate.FirstPoint, targetHorizontal, extent).ToString("R", CultureInfo.InvariantCulture)
							+ "," + ProjectCoordinate(target.Frame, candidate.SecondPoint, targetHorizontal, extent).ToString("R", CultureInfo.InvariantCulture) });
				}
			}
		}
		return result;
	}

	public static int SuppressRenderedDuplicates(OrthographicViewPlan view, IEnumerable<OrthographicDimensionCoverage> coverage,
		Func<OrthographicViewPlan, PlannedDimension, bool> wasRendered)
	{
		if (view == null || coverage == null || wasRendered == null) { throw new ArgumentNullException(); }
		int removed = 0;
		foreach (var relation in coverage.Where(c => ReferenceEquals(c.TargetView, view) && !c.Applied))
		{
			if (!view.Plan.Dimensions.Contains(relation.Target)) { continue; }
			if (!relation.ProviderView.Plan.Dimensions.Contains(relation.Provider)
				|| relation.Provider.AttachmentValidity == false || !wasRendered(relation.ProviderView, relation.Provider))
			{
				ProjectionWarning(view.Plan, "ProviderNotRendered|CandidateId=" + relation.Target.DiagnosticId
					+ "|OtherView=" + relation.ProviderView.Frame.Id);
				continue;
			}
			// Keep the generation RuleId; cross-view evidence must not replace a feature's rule identity.
			view.Plan.MarkSuppressed(relation.Target, ProjectionRuleId, null, null,
				(relation.Target.TopologyEvidence ?? "") + "|" + relation.Evidence);
			view.Plan.Dimensions.Remove(relation.Target);
			view.Plan.Diagnostics.FinalDimensions.RemoveAll(d => d.Id == relation.Target.DiagnosticId);
			relation.Applied = true;
			removed++;
		}
		return removed;
	}

	private static bool TryProjectionAxes(OrthographicViewFrame first, OrthographicViewFrame second,
		IReadOnlyList<OrthographicViewPlan> views, double tol, out bool firstHorizontal, out bool secondHorizontal, out PartExtentKind extent)
	{
		firstHorizontal = secondHorizontal = false;
		extent = PartExtentKind.Height;
		if (first.Role == OrthographicViewRole.Front && second.Role == OrthographicViewRole.Top)
		{
			firstHorizontal = secondHorizontal = true; extent = PartExtentKind.Length;
			return Near(first.MinX, second.MinX, tol) && Near(first.MaxX, second.MaxX, tol) && second.MaxY < first.MinY - tol;
		}
		if (first.Role == OrthographicViewRole.Front && second.Role == OrthographicViewRole.Left)
		{
			return Near(first.MinY, second.MinY, tol) && Near(first.MaxY, second.MaxY, tol) && second.MinX > first.MaxX + tol;
		}
		if (first.Role != OrthographicViewRole.Top || second.Role != OrthographicViewRole.Left) { return false; }
		secondHorizontal = true; extent = PartExtentKind.Width;
		var front = views.FirstOrDefault(v => v.Frame.Role == OrthographicViewRole.Front)?.Frame;
		return front != null && Near(first.Height, second.Width, tol)
			&& Near(first.MinX, front.MinX, tol) && Near(first.MaxX, front.MaxX, tol)
			&& Near(second.MinY, front.MinY, tol) && Near(second.MaxY, front.MaxY, tol)
			&& first.MaxY < front.MinY - tol && second.MinX > front.MaxX + tol;
	}

	private static bool CanProject(OrthographicViewPlan view, PlannedDimension dimension, bool horizontal, double tol)
	{
		bool overall = dimension != null && IsProjectedOverall(dimension);
		if (dimension == null || (view.Plan.CoordinateFrame != null && !view.Plan.CoordinateFrame.IsIdentity)
			|| dimension.Orientation != (horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical)
			|| dimension.AttachmentValidity == false || !string.IsNullOrWhiteSpace(dimension.OverrideText)
			|| (!overall && dimension.ForceOuterLevel) || !string.IsNullOrEmpty(dimension.DatumFamilyKey)) { return false; }
		if (!overall && (dimension.Kind != DimensionKind.Normal || dimension.OwnerKind != DimensionCandidateOwnerKind.Outline
			|| (dimension.Role != DimensionCandidateRole.Structure && dimension.Role != DimensionCandidateRole.OutlineSegment))) { return false; }
		var graph = view.Plan.Diagnostics.OutlineTopology;
		var evidence = new StructureBoundaryEvidence(graph, tol);
		if (!evidence.IsAvailable || !Near(graph.MinX, view.Frame.MinX, tol) || !Near(graph.MaxX, view.Frame.MaxX, tol)
			|| !Near(graph.MinY, view.Frame.MinY, tol) || !Near(graph.MaxY, view.Frame.MaxY, tol)
			|| evidence.ValidateBoundaryAttachments(dimension.FirstPoint, dimension.SecondPoint) != true) { return false; }
		double a = horizontal ? dimension.FirstPoint.X : dimension.FirstPoint.Y;
		double b = horizontal ? dimension.SecondPoint.X : dimension.SecondPoint.Y;
		if (!Finite(a) || !Finite(b) || Math.Abs(a - b) <= tol) { return false; }
		if (overall)
		{
			return Near(Math.Min(a, b), horizontal ? graph.MinX : graph.MinY, tol)
				&& Near(Math.Max(a, b), horizontal ? graph.MaxX : graph.MaxY, tol);
		}
		if (dimension.SourceGeometryIds == null || !graph.Edges.Any(e => e.IsBoundary
			&& dimension.SourceGeometryIds.Contains(e.SourceKey))) { return false; }
		return HasRealPositionFace(graph, dimension.FirstPoint, horizontal, tol)
			&& HasRealPositionFace(graph, dimension.SecondPoint, horizontal, tol);
	}

	private static bool HasRealPositionFace(OutlineTopologyDiagnostic graph, Point2D point, bool horizontal, double tol)
	{
		return graph.Edges.Any(e => e.IsBoundary && e.Kind == "Line" && !string.IsNullOrEmpty(e.SourceKey)
			&& (horizontal ? Math.Abs(e.Start.X - e.End.X) <= tol : Math.Abs(e.Start.Y - e.End.Y) <= tol)
			&& point.X >= Math.Min(e.Start.X, e.End.X) - tol && point.X <= Math.Max(e.Start.X, e.End.X) + tol
			&& point.Y >= Math.Min(e.Start.Y, e.End.Y) - tol && point.Y <= Math.Max(e.Start.Y, e.End.Y) + tol);
	}

	private static bool EquivalentInterval(OrthographicViewFrame first, PlannedDimension a, OrthographicViewFrame second,
		PlannedDimension b, PartExtentKind extent, double tol)
	{
		if (IsProjectedOverall(a) != IsProjectedOverall(b)) { return false; }
		if (!IsProjectedOverall(a) && (!string.Equals(a.RuleId ?? "", b.RuleId ?? "", StringComparison.Ordinal)
			|| a.Role != b.Role)) { return false; }
		bool ah = a.Orientation == DimensionOrientation.Horizontal, bh = b.Orientation == DimensionOrientation.Horizontal;
		double a0 = ProjectCoordinate(first, a.FirstPoint, ah, extent), a1 = ProjectCoordinate(first, a.SecondPoint, ah, extent);
		double b0 = ProjectCoordinate(second, b.FirstPoint, bh, extent), b1 = ProjectCoordinate(second, b.SecondPoint, bh, extent);
		return Near(Math.Min(a0, a1), Math.Min(b0, b1), tol) && Near(Math.Max(a0, a1), Math.Max(b0, b1), tol);
	}

	private static double ProjectCoordinate(OrthographicViewFrame frame, Point2D point, bool horizontal, PartExtentKind extent)
	{
		// shortcut: standard first-angle, same-scale views only; other arrangements remain unmerged until their transform is confirmed.
		return extent == PartExtentKind.Width && frame.Role == OrthographicViewRole.Top
			? frame.MaxY - point.Y : horizontal ? point.X - frame.MinX : point.Y - frame.MinY;
	}

	private static bool IsProjectedOverall(PlannedDimension dimension) => dimension.Kind == DimensionKind.OverallWidth
		|| dimension.Kind == DimensionKind.OverallHeight;

	private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

	private static void ProjectionWarning(DimensionPlan plan, string reason)
	{
		string warning = "CrossView.PendingHumanConfirmation|" + reason;
		if (!plan.Diagnostics.Warnings.Contains(warning)) { plan.Diagnostics.Warnings.Add(warning); }
	}
}
