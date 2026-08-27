using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning;

public sealed partial class DimensionPlanner
{
	private const string LProfileVerticalArmWidthRuleId = "LProfile.VerticalArmWidth";

	private const string LProfileHorizontalArmHeightRuleId = "LProfile.HorizontalArmHeight";

	private const string LProfileDerivedInnerEdgeRuleId = "LProfile.DerivedInnerEdge";

	private const string LProfileInternalChordEndpointRuleId = "LProfile.InternalChordEndpoint";

	private const string LProfileDerivedOuterContourSegmentRuleId = "LProfile.DerivedOuterContourSegment";

	private const string LProfileOverallWidthRuleId = "LProfile.OverallWidth";

	private const string LProfileOverallHeightRuleId = "LProfile.OverallHeight";

	private const string LProfileOverallSplitByFilletRuleId = "LProfile.OverallSplitByFillet";

	private void ApplyLProfileAnnotationRule(
		DimensionPlan plan,
		OutlineFeature2D outline,
		LProfileOuterContour preclassifiedOuterContour,
		string preclassifiedFailure)
	{
		if (plan == null || outline == null)
		{
			return;
		}

		string outerContourFailure = preclassifiedFailure ?? string.Empty;
		LProfileOuterContour outerContour = preclassifiedOuterContour;
		if (outerContour == null && string.IsNullOrEmpty(outerContourFailure))
		{
			outerContour = LProfileOuterContour.TryCreate(
				outline,
				_config.GeometryTolerance,
				out outerContourFailure);
		}
		if (outerContour == null)
		{
			string legacyDirectFailure;
			LProfileMatch.TryCreate(outline, _config.GeometryTolerance, out legacyDirectFailure);
			string legacyTransposedFailure;
			LProfileMatch.TryCreate(TransposeLProfileOutline(outline), _config.GeometryTolerance, out legacyTransposedFailure);
			plan.Diagnostics.Warnings.Add("LProfile.NotApplied|Stage=OuterContour|Reason=" + outerContourFailure
				+ "|Direct=" + legacyDirectFailure
				+ "|Transposed=" + legacyTransposedFailure);
			return;
		}
		plan.Diagnostics.Warnings.Add("LProfile.Recognized|" + outerContour.TopologyEvidence);

		List<LProfileMatch> matches = new List<LProfileMatch>();
		string directFailure;
		LProfileMatch direct = LProfileMatch.TryCreate(outline, _config.GeometryTolerance, out directFailure);
		if (direct != null)
		{
			direct.Transposed = false;
			matches.Add(direct);
		}
		string transposedFailure;
		LProfileMatch transposed = LProfileMatch.TryCreate(TransposeLProfileOutline(outline), _config.GeometryTolerance, out transposedFailure);
		if (transposed != null)
		{
			transposed.Transposed = true;
			matches.Add(transposed);
		}
		string newFailure = string.Empty;
		if (matches.Count == 0)
		{
			LProfileMatch orthogonalInnerCorner = LProfileMatch.TryCreateOrthogonalInnerCorner(
				outline,
				outerContour,
				_config.GeometryTolerance,
				out newFailure);
			if (orthogonalInnerCorner != null)
			{
				matches.Add(orthogonalInnerCorner);
			}
		}
		if (matches.Count > 1)
		{
			List<Tuple<LProfileMatch, int>> ranked = matches
				.Select(match => Tuple.Create(match, ScoreLProfileMatch(plan, match)))
				.OrderByDescending(item => item.Item2)
				.ToList();
			if (ranked[0].Item2 > ranked[1].Item2
				|| matches.All(match => string.Equals(match.AnnotationTopology, ranked[0].Item1.AnnotationTopology, StringComparison.Ordinal)))
			{
				matches = new List<LProfileMatch> { ranked[0].Item1 };
			}
		}
		if (matches.Count != 1)
		{
			plan.Diagnostics.Warnings.Add("LProfile.NotApplied|MatchCount=" + matches.Count
				+ "|Direct=" + directFailure
				+ "|Transposed=" + transposedFailure
				+ "|New=" + newFailure);
			return;
		}

		LProfileMatch match = matches[0];
		PlannedDimension verticalArmWidth = EnsureLProfileDimension(
			plan,
			match,
			match.VerticalArmWidthFirstPoint,
			match.VerticalArmWidthSecondPoint,
			DimensionOrientation.Horizontal,
			match.OuterHorizontalSide,
			LProfileVerticalArmWidthRuleId);
		if (verticalArmWidth == null)
		{
			plan.Diagnostics.Warnings.Add("LProfile.NotApplied|Reason=VerticalArmWidthCandidateAmbiguous");
			return;
		}
		SuppressReplacedVerticalArmWidth(plan, match, verticalArmWidth);

		PlannedDimension horizontalArmHeight = EnsureLProfileDimension(
			plan,
			match,
			match.HorizontalArmHeightFirstPoint,
			match.HorizontalArmHeightSecondPoint,
			DimensionOrientation.Vertical,
			match.ArmVerticalSide,
			LProfileHorizontalArmHeightRuleId);
		if (horizontalArmHeight == null)
		{
			plan.Diagnostics.Warnings.Add("LProfile.NotApplied|Reason=HorizontalArmHeightCandidateAmbiguous");
			return;
		}
		plan.Diagnostics.Warnings.Add("LProfile.Applied|Strategy=" + match.AnnotationTopology
			+ "|" + match.TopologyEvidence);

		if (match.FormulaClosed && horizontalArmHeight != null)
		{
			SuppressDerivedInnerEdge(plan, match);
			StampDerivedInnerEdgeDiagnostics(plan, match);
		}
		SuppressInternalChordEndpointCandidates(plan, match);
		SuppressDerivedOuterContourSegments(plan, match);
	}

	private void ApplyLProfileOverallDimensionRules(
		DimensionPlan plan,
		OutlineFeature2D outline,
		LProfileOuterContour outerContour)
	{
		if (plan == null || outline == null || outerContour?.Graph?.BoundaryOutline == null)
		{
			return;
		}

		OutlineFeature2D boundary = outerContour.Graph.BoundaryOutline;
		double tolerance = _config.GeometryTolerance;
		if (!OutlineGeometryQuery.TryGetEnvelope(boundary, tolerance, out OutlineEnvelope2D envelope))
		{
			return;
		}

		ApplyLProfileOverallDimensionRule(plan, boundary, outerContour, envelope, horizontal: true);
		ApplyLProfileOverallDimensionRule(plan, boundary, outerContour, envelope, horizontal: false);
	}

	private void ApplyLProfileOverallDimensionRule(
		DimensionPlan plan,
		OutlineFeature2D boundary,
		LProfileOuterContour outerContour,
		OutlineEnvelope2D envelope,
		bool horizontal)
	{
		double tolerance = _config.GeometryTolerance;
		DimensionKind kind = horizontal ? DimensionKind.OverallWidth : DimensionKind.OverallHeight;
		DimensionOrientation orientation = horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical;
		string ruleId = horizontal ? LProfileOverallWidthRuleId : LProfileOverallHeightRuleId;
		double overallSpan = horizontal ? envelope.MaxX - envelope.MinX : envelope.MaxY - envelope.MinY;
		if (overallSpan <= tolerance)
		{
			return;
		}

		PlannedDimension existingOverall = plan.Dimensions.SingleOrDefault(d =>
			d != null && d.Kind == kind && d.Orientation == orientation);
		if (existingOverall == null)
		{
			return;
		}

		LProfileOverallSideSelection sideSelection = SelectLProfileOverallSide(
			outerContour,
			envelope,
			horizontal,
			tolerance);
		Point2D firstAnchor = default(Point2D);
		Point2D secondAnchor = default(Point2D);
		bool anchorsResolved = sideSelection != null
			&& (horizontal
				? TryGetLProfileOverallWidthAnchors(boundary, envelope, sideSelection.Side, tolerance, out firstAnchor, out secondAnchor)
				: TryGetLProfileOverallHeightAnchors(boundary, envelope, sideSelection.Side, tolerance, out firstAnchor, out secondAnchor));
		List<string> sourceGeometryIds = GetBoundarySourceGeometryIds(boundary);
		string failureReason = sideSelection == null
			? "NoValidOuterSide"
			: (anchorsResolved ? string.Empty : "NoRealAnchorPair");
		string baseEvidence = BuildLProfileOverallEvidence(
			ruleId,
			envelope,
			horizontal,
			anchorsResolved ? (Point2D?)firstAnchor : null,
			anchorsResolved ? (Point2D?)secondAnchor : null,
			anchorsResolved ? "Generation" : "GenerationRejected",
			anchorsResolved ? "Retained" : "Rejected",
			failureReason,
			sideSelection);

		List<PlannedDimension> duplicateCandidates = plan.Dimensions
			.Where(d => IsLProfileOverallDuplicate(d, kind, overallSpan, tolerance))
			.ToList();
		foreach (PlannedDimension candidate in duplicateCandidates)
		{
			bool endpointsOnBoundary = OutlineGeometryQuery.IsPointOnBoundary(candidate.FirstPoint, boundary, tolerance)
				&& OutlineGeometryQuery.IsPointOnBoundary(candidate.SecondPoint, boundary, tolerance);
			string candidateEvidence = endpointsOnBoundary
				? baseEvidence
				: BuildLProfileOverallEvidence(
					ruleId,
					envelope,
					horizontal,
					anchorsResolved ? (Point2D?)firstAnchor : null,
					anchorsResolved ? (Point2D?)secondAnchor : null,
					"GenerationRejected",
					"Rejected",
					"VirtualProjection",
					sideSelection);
			if (!endpointsOnBoundary)
			{
				plan.MarkRejected(
					candidate,
					"GenerationRejected|VirtualProjection|" + ruleId,
					null,
					sourceGeometryIds,
					candidateEvidence
						+ "|Candidate=" + (candidate.DebugRole ?? string.Empty)
						+ "|RequestedFirst=" + candidate.FirstPoint
						+ "|RequestedSecond=" + candidate.SecondPoint);
			}
			else
			{
				plan.MarkSuppressed(
					candidate,
					ruleId + ".DuplicateStructure",
					ruleId,
					sourceGeometryIds,
					candidateEvidence
						+ "|Stage=PostGeneration|Decision=Suppressed|Reason=DuplicateOverallSpan");
			}
			plan.Dimensions.Remove(candidate);
		}

		if (sideSelection == null || !anchorsResolved)
		{
			plan.RecordRuleEvidence(
				existingOverall,
				null,
				sourceGeometryIds,
				baseEvidence + "|Stage=Fallback|Decision=Retained|Reason=" + failureReason);
			plan.Diagnostics.Warnings.Add(baseEvidence + "|Stage=Fallback|Decision=Retained|Reason=" + failureReason);
			return;
		}

		plan.MarkSuppressed(
			existingOverall,
			ruleId + ".DynamicSide",
			ruleId,
			sourceGeometryIds,
			baseEvidence + "|Stage=PostGeneration|Decision=Suppressed|Reason=ReplaceGenericOverall");
		plan.Dimensions.Remove(existingOverall);

		PlannedDimension replacement = new PlannedDimension
		{
			Kind = kind,
			Orientation = orientation,
			Side = sideSelection.Side,
			FirstPoint = firstAnchor,
			SecondPoint = secondAnchor,
			ForceOuterLevel = true,
			ReadingLevel = DimensionReadingLevel.Overall,
			DebugOwner = "LProfile",
			DebugRole = ruleId,
			SourceKey = ruleId,
			Role = DimensionCandidateRole.Overall,
			OwnerKind = DimensionCandidateOwnerKind.Outline,
			SourceGeometryIds = sourceGeometryIds,
			TopologyEvidence = baseEvidence + "|Stage=Generation|Decision=Retained|LayoutSide=" + sideSelection.Side,
			RuleId = ruleId
		};
		plan.Add(replacement);
	}

	private static bool IsLProfileOverallDuplicate(
		PlannedDimension dimension,
		DimensionKind kind,
		double overallSpan,
		double tolerance)
	{
		bool horizontal = kind == DimensionKind.OverallWidth;
		return dimension != null
			&& dimension.Kind == DimensionKind.Normal
			&& dimension.Orientation == (horizontal ? DimensionOrientation.Horizontal : DimensionOrientation.Vertical)
			&& (horizontal
				? (dimension.Side == DimensionSide.Top || dimension.Side == DimensionSide.Bottom)
				: (dimension.Side == DimensionSide.Left || dimension.Side == DimensionSide.Right))
			&& (dimension.Role == DimensionCandidateRole.Structure
				|| dimension.Role == DimensionCandidateRole.OutlineSegment)
			&& Math.Abs((horizontal
				? Math.Abs(dimension.SecondPoint.X - dimension.FirstPoint.X)
				: Math.Abs(dimension.SecondPoint.Y - dimension.FirstPoint.Y)) - overallSpan) <= tolerance;
	}

	private static LProfileOverallSideSelection SelectLProfileOverallSide(
		LProfileOuterContour outerContour,
		OutlineEnvelope2D envelope,
		bool horizontal,
		double tolerance)
	{
		DimensionSide defaultSide = horizontal ? DimensionSide.Top : DimensionSide.Left;
		DimensionSide alternativeSide = horizontal ? DimensionSide.Bottom : DimensionSide.Right;
		LProfileOverallSideScore defaultScore = ScoreLProfileOverallSide(
			outerContour,
			envelope,
			defaultSide,
			horizontal,
			tolerance);
		LProfileOverallSideScore alternativeScore = ScoreLProfileOverallSide(
			outerContour,
			envelope,
			alternativeSide,
			horizontal,
			tolerance);
		if (!defaultScore.HasRealSupport && !alternativeScore.HasRealSupport)
		{
			return null;
		}

		LProfileOverallSideScore selected = CompareLProfileOverallSides(defaultScore, alternativeScore, defaultSide, tolerance) >= 0
			? defaultScore
			: alternativeScore;
		return new LProfileOverallSideSelection
		{
			Side = selected.Side,
			DefaultScore = defaultScore,
			AlternativeScore = alternativeScore
		};
	}

	private static int CompareLProfileOverallSides(
		LProfileOverallSideScore first,
		LProfileOverallSideScore second,
		DimensionSide defaultSide,
		double tolerance)
	{
		if (first.HasRealSupport != second.HasRealSupport)
		{
			return first.HasRealSupport ? 1 : -1;
		}
		if (Math.Abs(first.Coverage - second.Coverage) > tolerance)
		{
			return first.Coverage > second.Coverage ? 1 : -1;
		}
		if (Math.Abs(first.LongestContinuousSpan - second.LongestContinuousSpan) > tolerance)
		{
			return first.LongestContinuousSpan > second.LongestContinuousSpan ? 1 : -1;
		}
		if (first.BreakCount != second.BreakCount)
		{
			return first.BreakCount < second.BreakCount ? 1 : -1;
		}
		return first.Side == defaultSide ? 1 : -1;
	}

	private static LProfileOverallSideScore ScoreLProfileOverallSide(
		LProfileOuterContour outerContour,
		OutlineEnvelope2D envelope,
		DimensionSide side,
		bool horizontal,
		double tolerance)
	{
		double sideCoordinate = GetLProfileSideCoordinate(envelope, side);
		List<Tuple<double, double>> intervals = new List<Tuple<double, double>>();
		IEnumerable<LProfileAxisChain> chains = horizontal ? outerContour.HorizontalChains : outerContour.VerticalChains;
		foreach (LProfileAxisChain chain in chains ?? new List<LProfileAxisChain>())
		{
			if (chain == null || chain.Span <= tolerance || Math.Abs(chain.Cross - sideCoordinate) > tolerance)
			{
				continue;
			}
			intervals.Add(Tuple.Create(chain.MinT, chain.MaxT));
		}

		foreach (LProfileEdge edge in outerContour.Graph.BoundaryEdges)
		{
			if (edge?.Arc == null
				|| LProfileArcAttachment.TryCreate(
					edge,
					outerContour.Graph,
					outerContour.HorizontalChains,
					outerContour.VerticalChains,
					tolerance) == null
				|| !IsLProfileTangentArc(
					edge,
					outerContour.Graph,
					outerContour.HorizontalChains,
					outerContour.VerticalChains,
					tolerance))
			{
				continue;
			}
			if (!TryGetArcProjection(edge.Arc, tolerance, out double minX, out double maxX, out double minY, out double maxY))
			{
				continue;
			}
			double actualSideCoordinate = horizontal
				? (side == DimensionSide.Top ? maxY : minY)
				: (side == DimensionSide.Right ? maxX : minX);
			if (Math.Abs(actualSideCoordinate - sideCoordinate) > tolerance)
			{
				continue;
			}
			double minimum = horizontal ? minX : minY;
			double maximum = horizontal ? maxX : maxY;
			if (maximum - minimum > tolerance)
			{
				intervals.Add(Tuple.Create(minimum, maximum));
			}
		}

		List<Tuple<double, double>> merged = MergeLProfileProjectionIntervals(intervals, tolerance);
		double coverage = merged.Sum(interval => interval.Item2 - interval.Item1);
		return new LProfileOverallSideScore
		{
			Side = side,
			Coverage = coverage,
			LongestContinuousSpan = merged.Count == 0 ? 0.0 : merged.Max(interval => interval.Item2 - interval.Item1),
			BreakCount = Math.Max(0, merged.Count - 1),
			ChainCount = merged.Count,
			HasRealSupport = merged.Count != 0
		};
	}

	private static List<Tuple<double, double>> MergeLProfileProjectionIntervals(
		IEnumerable<Tuple<double, double>> intervals,
		double tolerance)
	{
		List<Tuple<double, double>> ordered = (intervals ?? new List<Tuple<double, double>>())
			.Where(interval => interval != null && interval.Item2 - interval.Item1 > tolerance)
			.OrderBy(interval => interval.Item1)
			.ThenBy(interval => interval.Item2)
			.ToList();
		List<Tuple<double, double>> merged = new List<Tuple<double, double>>();
		foreach (Tuple<double, double> interval in ordered)
		{
			if (merged.Count == 0 || interval.Item1 > merged[merged.Count - 1].Item2 + tolerance)
			{
				merged.Add(Tuple.Create(interval.Item1, interval.Item2));
			}
			else if (interval.Item2 > merged[merged.Count - 1].Item2)
			{
				Tuple<double, double> previous = merged[merged.Count - 1];
				merged[merged.Count - 1] = Tuple.Create(previous.Item1, interval.Item2);
			}
		}
		return merged;
	}

	private static double GetLProfileSideCoordinate(OutlineEnvelope2D envelope, DimensionSide side)
	{
		return side switch
		{
			DimensionSide.Top => envelope.MaxY,
			DimensionSide.Bottom => envelope.MinY,
			DimensionSide.Right => envelope.MaxX,
			_ => envelope.MinX
		};
	}

	private static bool TryGetLProfileOverallWidthAnchors(
		OutlineFeature2D boundary,
		OutlineEnvelope2D envelope,
		DimensionSide side,
		double tolerance,
		out Point2D leftAnchor,
		out Point2D rightAnchor)
	{
		leftAnchor = default(Point2D);
		rightAnchor = default(Point2D);
		List<Point2D> leftCandidates = OutlineGeometryQuery.GetVerticalBoundaryIntersections(boundary, envelope.MinX, tolerance)
			.Where(point => Math.Abs(point.X - envelope.MinX) <= tolerance
				&& OutlineGeometryQuery.IsPointOnBoundary(point, boundary, tolerance))
			.OrderBy(point => side == DimensionSide.Bottom ? point.Y : -point.Y)
			.ThenBy(point => point.X)
			.ToList();
		List<Point2D> rightCandidates = OutlineGeometryQuery.GetVerticalBoundaryIntersections(boundary, envelope.MaxX, tolerance)
			.Where(point => Math.Abs(point.X - envelope.MaxX) <= tolerance
				&& OutlineGeometryQuery.IsPointOnBoundary(point, boundary, tolerance))
			.OrderBy(point => side == DimensionSide.Bottom ? point.Y : -point.Y)
			.ThenBy(point => point.X)
			.ToList();
		if (leftCandidates.Count == 0 || rightCandidates.Count == 0)
		{
			return false;
		}
		leftAnchor = leftCandidates[0];
		rightAnchor = rightCandidates[0];
		return Math.Abs(leftAnchor.X - envelope.MinX) <= tolerance
			&& Math.Abs(rightAnchor.X - envelope.MaxX) <= tolerance
			&& OutlineGeometryQuery.IsPointOnBoundary(leftAnchor, boundary, tolerance)
			&& OutlineGeometryQuery.IsPointOnBoundary(rightAnchor, boundary, tolerance);
	}

	private static bool TryGetLProfileOverallHeightAnchors(
		OutlineFeature2D boundary,
		OutlineEnvelope2D envelope,
		DimensionSide side,
		double tolerance,
		out Point2D bottomAnchor,
		out Point2D topAnchor)
	{
		bottomAnchor = default(Point2D);
		topAnchor = default(Point2D);
		List<Point2D> bottomCandidates = OutlineGeometryQuery.GetHorizontalBoundaryIntersections(boundary, envelope.MinY, tolerance)
			.Where(point => Math.Abs(point.Y - envelope.MinY) <= tolerance
				&& OutlineGeometryQuery.IsPointOnBoundary(point, boundary, tolerance))
			.OrderBy(point => side == DimensionSide.Right ? -point.X : point.X)
			.ThenBy(point => point.Y)
			.ToList();
		List<Point2D> topCandidates = OutlineGeometryQuery.GetHorizontalBoundaryIntersections(boundary, envelope.MaxY, tolerance)
			.Where(point => Math.Abs(point.Y - envelope.MaxY) <= tolerance
				&& OutlineGeometryQuery.IsPointOnBoundary(point, boundary, tolerance))
			.OrderBy(point => side == DimensionSide.Right ? -point.X : point.X)
			.ThenBy(point => point.Y)
			.ToList();
		if (bottomCandidates.Count == 0 || topCandidates.Count == 0)
		{
			return false;
		}
		bottomAnchor = bottomCandidates[0];
		topAnchor = topCandidates[0];
		return Math.Abs(bottomAnchor.Y - envelope.MinY) <= tolerance
			&& Math.Abs(topAnchor.Y - envelope.MaxY) <= tolerance
			&& OutlineGeometryQuery.IsPointOnBoundary(bottomAnchor, boundary, tolerance)
			&& OutlineGeometryQuery.IsPointOnBoundary(topAnchor, boundary, tolerance);
	}

	private static bool IsLProfileTangentArc(
		LProfileEdge edge,
		LProfileBoundaryGraph graph,
		IList<LProfileAxisChain> horizontalChains,
		IList<LProfileAxisChain> verticalChains,
		double tolerance)
	{
		if (edge?.Arc == null || graph == null)
		{
			return false;
		}
		LProfileArcAttachment attachment = LProfileArcAttachment.TryCreate(
			edge,
			graph,
			horizontalChains,
			verticalChains,
			tolerance);
		if (attachment == null)
		{
			return false;
		}
		Segment2D horizontal = graph.BoundaryEdges
			.Where(item => item?.Segment != null
				&& item.Segment.IsHorizontal(tolerance)
				&& item.Touches(attachment.HorizontalPoint, tolerance))
			.Select(item => item.Segment)
			.SingleOrDefault();
		Segment2D vertical = graph.BoundaryEdges
			.Where(item => item?.Segment != null
				&& item.Segment.IsVertical(tolerance)
				&& item.Touches(attachment.VerticalPoint, tolerance))
			.Select(item => item.Segment)
			.SingleOrDefault();
		return horizontal != null
			&& vertical != null
			&& IsArcTangentToSegment(edge.Arc, horizontal, attachment.HorizontalPoint, tolerance)
			&& IsArcTangentToSegment(edge.Arc, vertical, attachment.VerticalPoint, tolerance);
	}

	private static bool IsArcTangentToSegment(Arc2D arc, Segment2D segment, Point2D endpoint, double tolerance)
	{
		double radialX = endpoint.X - arc.Center.X;
		double radialY = endpoint.Y - arc.Center.Y;
		double lineX = segment.End.X - segment.Start.X;
		double lineY = segment.End.Y - segment.Start.Y;
		double radialLength = Math.Sqrt(radialX * radialX + radialY * radialY);
		double lineLength = Math.Sqrt(lineX * lineX + lineY * lineY);
		if (radialLength <= tolerance || lineLength <= tolerance)
		{
			return false;
		}
		double sweep = 4.0 * Math.Atan(arc.Bulge);
		double tangentX = sweep >= 0.0 ? -radialY : radialY;
		double tangentY = sweep >= 0.0 ? radialX : -radialX;
		double sine = Math.Abs(tangentX * lineY - tangentY * lineX) / (radialLength * lineLength);
		return sine <= Math.Max(1E-9, tolerance / radialLength);
	}

	private static bool TryGetArcProjection(
		Arc2D arc,
		double tolerance,
		out double minX,
		out double maxX,
		out double minY,
		out double maxY)
	{
		minX = maxX = minY = maxY = 0.0;
		if (arc == null || arc.Radius <= tolerance || Math.Abs(arc.Bulge) <= 1E-12)
		{
			return false;
		}
		List<Point2D> points = new List<Point2D> { arc.Start, arc.End };
		foreach (double angle in new[] { 0.0, Math.PI / 2.0, Math.PI, Math.PI * 1.5 })
		{
			if (IsArcAngleOnArc(angle, arc, tolerance))
			{
				points.Add(new Point2D(
					arc.Center.X + Math.Cos(angle) * arc.Radius,
					arc.Center.Y + Math.Sin(angle) * arc.Radius));
			}
		}
		minX = points.Min(point => point.X);
		maxX = points.Max(point => point.X);
		minY = points.Min(point => point.Y);
		maxY = points.Max(point => point.Y);
		return maxX - minX > tolerance || maxY - minY > tolerance;
	}

	private static bool IsArcAngleOnArc(double angle, Arc2D arc, double tolerance)
	{
		double startAngle = Math.Atan2(arc.Start.Y - arc.Center.Y, arc.Start.X - arc.Center.X);
		double sweep = 4.0 * Math.Atan(arc.Bulge);
		double delta = sweep >= 0.0
			? NormalizeLProfileAngle(angle - startAngle)
			: NormalizeLProfileAngle(startAngle - angle);
		return delta <= Math.Abs(sweep) + Math.Max(1E-12, tolerance / arc.Radius);
	}

	private static double NormalizeLProfileAngle(double angle)
	{
		double fullTurn = Math.PI * 2.0;
		angle %= fullTurn;
		return angle < 0.0 ? angle + fullTurn : angle;
	}

	private static List<string> GetBoundarySourceGeometryIds(OutlineFeature2D boundary)
	{
		return (boundary.Segments ?? new List<Segment2D>())
			.Select(segment => segment?.SourceKey)
			.Concat((boundary.Arcs ?? new List<Arc2D>()).Select(arc => arc?.SourceKey))
			.Where(source => !string.IsNullOrEmpty(source))
			.Distinct(StringComparer.Ordinal)
			.ToList();
	}

	private static string BuildLProfileOverallEvidence(
		string ruleId,
		OutlineEnvelope2D envelope,
		bool horizontal,
		Point2D? firstAnchor,
		Point2D? secondAnchor,
		string stage,
		string decision,
		string reason,
		LProfileOverallSideSelection sideSelection)
	{
		DimensionSide defaultSide = horizontal ? DimensionSide.Top : DimensionSide.Left;
		LProfileOverallSideScore first = sideSelection?.DefaultScore;
		LProfileOverallSideScore second = sideSelection?.AlternativeScore;
		double overallSpan = horizontal ? envelope.MaxX - envelope.MinX : envelope.MaxY - envelope.MinY;
		return ruleId
			+ "|Stage=" + stage
			+ "|Decision=" + decision
			+ "|Axis=" + (horizontal ? "Horizontal" : "Vertical")
			+ "|OuterContourComponent=1"
			+ "|DefaultSide=" + defaultSide
			+ "|SelectedSide=" + (sideSelection == null ? "Unavailable" : sideSelection.Side.ToString())
			+ "|CandidateA=" + FormatLProfileSideScore(first, overallSpan)
			+ "|CandidateB=" + FormatLProfileSideScore(second, overallSpan)
			+ "|EnvelopeMinX=" + envelope.MinX.ToString("0.########", CultureInfo.InvariantCulture)
			+ "|EnvelopeMaxX=" + envelope.MaxX.ToString("0.########", CultureInfo.InvariantCulture)
			+ "|EnvelopeMinY=" + envelope.MinY.ToString("0.########", CultureInfo.InvariantCulture)
			+ "|EnvelopeMaxY=" + envelope.MaxY.ToString("0.########", CultureInfo.InvariantCulture)
			+ "|EnvelopeSpan=" + (horizontal ? envelope.MaxX - envelope.MinX : envelope.MaxY - envelope.MinY).ToString("0.########", CultureInfo.InvariantCulture)
			+ "|FirstAnchor=" + FormatLProfilePoint(firstAnchor)
			+ "|SecondAnchor=" + FormatLProfilePoint(secondAnchor)
			+ (string.IsNullOrEmpty(reason) ? string.Empty : "|Reason=" + reason);
	}

	private static string FormatLProfileSideScore(LProfileOverallSideScore score, double overallSpan)
	{
		if (score == null)
		{
			return "Unavailable";
		}
		return score.Side
			+ ":Coverage=" + score.Coverage.ToString("0.########", CultureInfo.InvariantCulture)
			+ ":CoverageRatio=" + (overallSpan <= 0.0 ? 0.0 : score.Coverage / overallSpan).ToString("0.########", CultureInfo.InvariantCulture)
			+ ":Longest=" + score.LongestContinuousSpan.ToString("0.########", CultureInfo.InvariantCulture)
			+ ":Chains=" + score.ChainCount
			+ ":Breaks=" + score.BreakCount
			+ ":Real=" + score.HasRealSupport;
	}

	private static string FormatLProfilePoint(Point2D? point)
	{
		return point.HasValue
			? point.Value.X.ToString("0.########", CultureInfo.InvariantCulture) + "," + point.Value.Y.ToString("0.########", CultureInfo.InvariantCulture)
			: "Unavailable";
	}

	private sealed class LProfileOverallSideSelection
	{
		public DimensionSide Side { get; set; }

		public LProfileOverallSideScore DefaultScore { get; set; }

		public LProfileOverallSideScore AlternativeScore { get; set; }
	}

	private sealed class LProfileOverallSideScore
	{
		public DimensionSide Side { get; set; }

		public double Coverage { get; set; }

		public double LongestContinuousSpan { get; set; }

		public int ChainCount { get; set; }

		public int BreakCount { get; set; }

		public bool HasRealSupport { get; set; }
	}

	private static int ScoreLProfileMatch(DimensionPlan plan, LProfileMatch match)
	{
		return plan.Dimensions.Any(d => IsLProfileLinearCandidate(d)
			&& CanonicalOrientation(d, match) == DimensionOrientation.Vertical
			&& IsCanonicalCrossCoordinate(d, match, match.CommonVertical.Cross, horizontal: false)
			&& IsCanonicalInterval(d, match, DimensionOrientation.Vertical, match.CommonVertical.MinT, match.CommonVertical.MaxT))
			? 1
			: 0;
	}

	private void ApplyLProfileOverallSplitByFilletRule(
		DimensionPlan plan,
		OutlineFeature2D outline,
		LProfileOuterContour outerContour)
	{
		if (plan == null || outline == null || outerContour?.Graph?.BoundaryOutline == null)
		{
			return;
		}

		OutlineEnvelope2D envelope;
		if (!OutlineGeometryQuery.TryGetEnvelope(
			outerContour.Graph.BoundaryOutline,
			_config.GeometryTolerance,
			out envelope))
		{
			return;
		}

		ApplyLProfileOverallSplitByFilletAxis(
			plan,
			outerContour,
			envelope,
			horizontal: true);
		ApplyLProfileOverallSplitByFilletAxis(
			plan,
			outerContour,
			envelope,
			horizontal: false);
	}

	private void ApplyLProfileOverallSplitByFilletAxis(
		DimensionPlan plan,
		LProfileOuterContour outerContour,
		OutlineEnvelope2D envelope,
		bool horizontal)
	{
		double tolerance = _config.GeometryTolerance;
		OutlineFeature2D boundary = outerContour.Graph.BoundaryOutline;
		PlannedDimension overall = plan.Dimensions.SingleOrDefault(d =>
			horizontal
				? d.Kind == DimensionKind.OverallWidth
				: d.Kind == DimensionKind.OverallHeight);
		if (!IsValidOverallForLProfileSplit(overall, boundary, envelope, horizontal, tolerance))
		{
			return;
		}

		List<LProfileAxisChain> chains = horizontal
			? outerContour.HorizontalChains
			: outerContour.VerticalChains;
		List<LProfileAxisChain> oppositeChains = horizontal
			? outerContour.VerticalChains
			: outerContour.HorizontalChains;
		foreach (PlannedDimension candidate in plan.Dimensions.ToList())
		{
			if (!IsLProfileLinearCandidate(candidate)
				|| IsLProfileOwnedDimension(candidate)
				|| (horizontal
					? candidate.Orientation != DimensionOrientation.Horizontal
					: candidate.Orientation != DimensionOrientation.Vertical))
			{
				continue;
			}

			LProfileAxisChain chain = FindExactCandidateAxisChain(
				candidate,
				chains,
				boundary,
				tolerance);
			if (chain == null)
			{
				continue;
			}

			LProfileOverallSplitEvidence evidence = TryBuildLProfileOverallSplitEvidence(
				outerContour.Graph,
				chain,
				oppositeChains,
				envelope,
				horizontal,
				tolerance);
			if (evidence == null)
			{
				continue;
			}

			if (evidence.ClosureResidual > tolerance)
			{
				plan.RecordRuleEvidence(
					candidate,
					null,
					evidence.SourceGeometryIds,
					evidence.TopologyEvidence + "|Decision=Retained");
				continue;
			}

			plan.SetAttachmentValidity(candidate, true);
			plan.MarkSuppressed(
				candidate,
				LProfileOverallSplitByFilletRuleId,
				LProfileOverallSplitByFilletRuleId,
				evidence.SourceGeometryIds,
				evidence.TopologyEvidence + "|Decision=Suppressed");
			plan.Dimensions.Remove(candidate);
		}
	}

	private static bool IsValidOverallForLProfileSplit(
		PlannedDimension overall,
		OutlineFeature2D boundary,
		OutlineEnvelope2D envelope,
		bool horizontal,
		double tolerance)
	{
		if (overall == null || overall.AttachmentValidity == false
			|| !OutlineGeometryQuery.IsPointOnBoundary(overall.FirstPoint, boundary, tolerance)
			|| !OutlineGeometryQuery.IsPointOnBoundary(overall.SecondPoint, boundary, tolerance))
		{
			return false;
		}
		double first = horizontal ? overall.FirstPoint.X : overall.FirstPoint.Y;
		double second = horizontal ? overall.SecondPoint.X : overall.SecondPoint.Y;
		double minimum = horizontal ? envelope.MinX : envelope.MinY;
		double maximum = horizontal ? envelope.MaxX : envelope.MaxY;
		return Math.Abs(Math.Min(first, second) - minimum) <= tolerance
			&& Math.Abs(Math.Max(first, second) - maximum) <= tolerance;
	}

	private static LProfileAxisChain FindExactCandidateAxisChain(
		PlannedDimension candidate,
		IEnumerable<LProfileAxisChain> chains,
		OutlineFeature2D boundary,
		double tolerance)
	{
		if (candidate == null || boundary == null)
		{
			return null;
		}
		List<LProfileAxisChain> matches = (chains ?? new LProfileAxisChain[0])
			.Where(chain => chain != null && IsContinuousAxisChain(chain, tolerance))
			.Where(chain =>
				OutlineGeometryQuery.IsPointOnBoundary(chain.GetEndpoint(minimum: true), boundary, tolerance)
				&& OutlineGeometryQuery.IsPointOnBoundary(chain.GetEndpoint(minimum: false), boundary, tolerance))
			.Where(chain =>
				(IsSamePoint(candidate.FirstPoint, chain.GetEndpoint(minimum: true), tolerance)
					&& IsSamePoint(candidate.SecondPoint, chain.GetEndpoint(minimum: false), tolerance))
				|| (IsSamePoint(candidate.FirstPoint, chain.GetEndpoint(minimum: false), tolerance)
					&& IsSamePoint(candidate.SecondPoint, chain.GetEndpoint(minimum: true), tolerance)))
			.ToList();
		return matches.Count == 1 ? matches[0] : null;
	}

	private static bool IsContinuousAxisChain(LProfileAxisChain chain, double tolerance)
	{
		if (chain == null || chain.Edges == null || chain.Edges.Count == 0)
		{
			return false;
		}
		List<LProfileEdge> ordered = chain.Edges
			.Where(edge => edge?.Segment != null)
			.OrderBy(edge => chain.Horizontal ? edge.Segment.MinX : edge.Segment.MinY)
			.ToList();
		if (ordered.Count == 0)
		{
			return false;
		}
		LProfileEdge previous = ordered[0];
		for (int i = 1; i < ordered.Count; i++)
		{
			LProfileEdge current = ordered[i];
			double previousMaximum = chain.Horizontal ? previous.Segment.MaxX : previous.Segment.MaxY;
			double currentMinimum = chain.Horizontal ? current.Segment.MinX : current.Segment.MinY;
			if (currentMinimum > previousMaximum + tolerance
				|| !AreAxisSegmentEndpointsConnected(previous.Segment, current.Segment, tolerance))
			{
				return false;
			}
			previous = current;
		}
		return true;
	}

	private static bool AreAxisSegmentEndpointsConnected(
		Segment2D first,
		Segment2D second,
		double tolerance)
	{
		return first.Start.DistanceTo(second.Start) <= tolerance
			|| first.Start.DistanceTo(second.End) <= tolerance
			|| first.End.DistanceTo(second.Start) <= tolerance
			|| first.End.DistanceTo(second.End) <= tolerance;
	}

	private static LProfileOverallSplitEvidence TryBuildLProfileOverallSplitEvidence(
		LProfileBoundaryGraph graph,
		LProfileAxisChain chain,
		IList<LProfileAxisChain> oppositeChains,
		OutlineEnvelope2D envelope,
		bool horizontal,
		double tolerance)
	{
		if (graph == null || chain == null || envelope == null || chain.Span <= tolerance)
		{
			return null;
		}
		double minimum = horizontal ? envelope.MinX : envelope.MinY;
		double maximum = horizontal ? envelope.MaxX : envelope.MaxY;
		List<LProfileFilletProjection> projections = new List<LProfileFilletProjection>();
		foreach (Point2D endpoint in new[]
		{
			chain.GetEndpoint(minimum: true),
			chain.GetEndpoint(minimum: false)
		})
		{
			double axis = horizontal ? endpoint.X : endpoint.Y;
			if (Math.Abs(axis - minimum) <= tolerance || Math.Abs(axis - maximum) <= tolerance)
			{
				continue;
			}
			LProfileFilletProjection projection;
			if (!TryGetOuterFilletProjection(
				graph,
				chain,
				oppositeChains,
				endpoint,
				horizontal,
				minimum,
				maximum,
				tolerance,
				out projection))
			{
				return null;
			}
			projections.Add(projection);
		}
		if (projections.Count == 0)
		{
			return null;
		}

		double overallSpan = maximum - minimum;
		double closureActual = chain.Span + projections.Sum(projection => projection.ComplementProjection);
		double closureResidual = Math.Abs(closureActual - overallSpan);
		List<LProfileEdge> evidenceEdges = chain.Edges
			.Concat(projections.SelectMany(projection => new[] { projection.ArcEdge, projection.OrthogonalEdge }))
			.Where(edge => edge != null)
			.Distinct()
			.ToList();
		List<string> sourceGeometryIds = evidenceEdges
			.Select(edge => edge.SourceKey)
			.Where(source => !string.IsNullOrEmpty(source))
			.Distinct(StringComparer.Ordinal)
			.ToList();
		string projectionEvidence = string.Join(";", projections.Select(projection =>
			"ArcEdge=" + projection.ArcEdge.Id
			+ ",Source=" + (projection.ArcEdge.SourceKey ?? string.Empty)
			+ ",Radius=" + projection.ArcEdge.Arc.Radius.ToString("0.########")
			+ ",Bulge=" + projection.ArcEdge.Arc.Bulge.ToString("0.########")
			+ ",Tangent=" + projection.LineTangentPoint
			+ ",EnvelopePoint=" + projection.EnvelopePoint
			+ ",Complement=" + projection.ComplementProjection.ToString("0.########")));
		return new LProfileOverallSplitEvidence
		{
			SourceGeometryIds = sourceGeometryIds,
			ClosureResidual = closureResidual,
			TopologyEvidence = LProfileOverallSplitByFilletRuleId
				+ "|Axis=" + (horizontal ? "Horizontal" : "Vertical")
				+ "|LineChain=" + string.Join(",", chain.Edges.Select(edge => edge.Id))
				+ "|LineStart=" + chain.GetEndpoint(minimum: true)
				+ "|LineEnd=" + chain.GetEndpoint(minimum: false)
				+ "|Projections=" + projectionEvidence
				+ "|StraightSpan=" + chain.Span.ToString("0.########")
				+ "|ClosureExpected=" + overallSpan.ToString("0.########")
				+ "|ClosureActual=" + closureActual.ToString("0.########")
				+ "|ClosureResidual=" + closureResidual.ToString("0.########")
				+ (closureResidual <= tolerance ? "|Closed" : "|Open")
		};
	}

	private static bool TryGetOuterFilletProjection(
		LProfileBoundaryGraph graph,
		LProfileAxisChain lineChain,
		IList<LProfileAxisChain> oppositeChains,
		Point2D lineEndpoint,
		bool horizontal,
		double minimum,
		double maximum,
		double tolerance,
		out LProfileFilletProjection projection)
	{
		projection = null;
		List<LProfileEdge> arcs = graph.BoundaryEdges
			.Where(edge => edge?.Arc != null && edge.Touches(lineEndpoint, tolerance))
			.ToList();
		if (arcs.Count != 1)
		{
			return false;
		}
		LProfileEdge arcEdge = arcs[0];
		Arc2D arc = arcEdge.Arc;
		bool startsAtLine = arc.Start.DistanceTo(lineEndpoint) <= tolerance;
		bool endsAtLine = arc.End.DistanceTo(lineEndpoint) <= tolerance;
		if (startsAtLine == endsAtLine || !IsValidArcForLProfileSplit(arc, tolerance))
		{
			return false;
		}
		Point2D tangentPoint = startsAtLine ? arc.Start : arc.End;
		Point2D envelopePoint = startsAtLine ? arc.End : arc.Start;
		List<LProfileEdge> lineEdges = lineChain.Edges
			.Where(edge => edge?.Segment != null && edge.Touches(tangentPoint, tolerance))
			.ToList();
		if (lineEdges.Count != 1
			|| !IsArcTangentToLine(arc, tangentPoint, lineEdges[0].Segment, tolerance))
		{
			return false;
		}

		List<LProfileEdge> orthogonalEdges = graph.BoundaryEdges
			.Where(edge => edge?.Segment != null
				&& !lineChain.Edges.Contains(edge)
				&& (horizontal ? edge.Segment.IsVertical(tolerance) : edge.Segment.IsHorizontal(tolerance))
				&& edge.Touches(envelopePoint, tolerance))
			.ToList();
		if (orthogonalEdges.Count != 1
			|| !IsArcTangentToLine(arc, envelopePoint, orthogonalEdges[0].Segment, tolerance)
			|| !IsInsideBoundary(arc.Center, graph.BoundaryOutline, tolerance))
		{
			return false;
		}
		LProfileAxisChain orthogonalChain = (oppositeChains ?? new LProfileAxisChain[0])
			.Where(chain => chain != null && chain.Edges.Contains(orthogonalEdges[0]))
			.SingleOrDefault();
		if (orthogonalChain == null || !IsContinuousAxisChain(orthogonalChain, tolerance))
		{
			return false;
		}
		double envelopeAxis = horizontal ? envelopePoint.X : envelopePoint.Y;
		bool atMinimum = Math.Abs(envelopeAxis - minimum) <= tolerance;
		bool atMaximum = Math.Abs(envelopeAxis - maximum) <= tolerance;
		if (atMinimum == atMaximum)
		{
			return false;
		}
		double lineAxis = horizontal ? lineEndpoint.X : lineEndpoint.Y;
		double complement = Math.Abs(envelopeAxis - lineAxis);
		if (complement <= tolerance)
		{
			return false;
		}
		projection = new LProfileFilletProjection
		{
			ArcEdge = arcEdge,
			OrthogonalEdge = orthogonalEdges[0],
			LineTangentPoint = tangentPoint,
			EnvelopePoint = envelopePoint,
			ComplementProjection = complement
		};
		return true;
	}

	private static bool IsValidArcForLProfileSplit(Arc2D arc, double tolerance)
	{
		if (arc == null || arc.Radius <= tolerance || Math.Abs(arc.Bulge) <= 1E-12)
		{
			return false;
		}
		double startRadius = arc.Center.DistanceTo(arc.Start);
		double endRadius = arc.Center.DistanceTo(arc.End);
		return Math.Abs(startRadius - arc.Radius) <= tolerance
			&& Math.Abs(endRadius - arc.Radius) <= tolerance
			&& arc.Start.DistanceTo(arc.End) > tolerance;
	}

	private static bool IsArcTangentToLine(
		Arc2D arc,
		Point2D arcPoint,
		Segment2D line,
		double tolerance)
	{
		if (arc == null || line == null || line.Length <= tolerance)
		{
			return false;
		}
		Point2D radius = new Point2D(arcPoint.X - arc.Center.X, arcPoint.Y - arc.Center.Y);
		Point2D direction = new Point2D(line.End.X - line.Start.X, line.End.Y - line.Start.Y);
		double radiusLength = Math.Sqrt(radius.X * radius.X + radius.Y * radius.Y);
		double directionLength = Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
		if (radiusLength <= tolerance || directionLength <= tolerance)
		{
			return false;
		}
		double tangentX = -radius.Y / radiusLength;
		double tangentY = radius.X / radiusLength;
		double lineX = direction.X / directionLength;
		double lineY = direction.Y / directionLength;
		double parallelError = Math.Abs(lineX * tangentY - lineY * tangentX);
		double directionTolerance = tolerance / Math.Max(radiusLength, tolerance);
		return parallelError <= Math.Max(1E-12, directionTolerance);
	}

	private static bool IsSamePoint(Point2D first, Point2D second, double tolerance)
	{
		return first.DistanceTo(second) <= tolerance;
	}

	private PlannedDimension EnsureLProfileDimension(
		DimensionPlan plan,
		LProfileMatch match,
		Point2D canonicalFirst,
		Point2D canonicalSecond,
		DimensionOrientation canonicalOrientation,
		DimensionSide canonicalSide,
		string ruleId)
	{
		Tuple<double, double> interval = GetCanonicalInterval(canonicalFirst, canonicalSecond, canonicalOrientation);
		List<PlannedDimension> existing = plan.Dimensions
			.Where(IsStructureDimension)
			.Where(d => CanonicalOrientation(d, match) == canonicalOrientation)
			.Where(d => CanonicalSide(d, match) == canonicalSide)
			.Where(d => IsCanonicalInterval(d, match, canonicalOrientation, interval.Item1, interval.Item2))
			.ToList();
		if (existing.Count > 1)
		{
			return null;
		}

		PlannedDimension dimension = existing.SingleOrDefault();
		if (dimension == null)
		{
			PlannedDimension canonical = new PlannedDimension
			{
				Kind = DimensionKind.Normal,
				Orientation = canonicalOrientation,
				Side = canonicalSide,
				FirstPoint = canonicalFirst,
				SecondPoint = canonicalSecond,
				SourceKey = "LProfile:" + ruleId + ":" + string.Join(",", match.SourceGeometryIds),
				DebugOwner = "LProfile",
				DebugRole = GetDebugRole(canonicalOrientation, canonicalSide, match),
				Role = DimensionCandidateRole.Structure,
				ReadingLevel = DimensionReadingLevel.LocalSpacing,
				AlignmentKey = GetAlignmentKey(canonicalOrientation, canonicalSide, match),
				AlignmentPriority = 80,
				TopologyEvidence = match.TopologyEvidence,
				RuleId = ruleId
			};
			dimension = ToWorldLProfileDimension(canonical, match);
			plan.Add(dimension);
		}
		else
		{
			UpdateExistingLProfileDimensionGeometry(plan, dimension, match, canonicalFirst, canonicalSecond);
		}

		plan.RecordRuleEvidence(dimension, ruleId, match.SourceGeometryIds, match.TopologyEvidence);
		return dimension;
	}

	private static void UpdateExistingLProfileDimensionGeometry(
		DimensionPlan plan,
		PlannedDimension dimension,
		LProfileMatch match,
		Point2D canonicalFirst,
		Point2D canonicalSecond)
	{
		Point2D first = FromCanonicalPoint(canonicalFirst, match);
		Point2D second = FromCanonicalPoint(canonicalSecond, match);
		dimension.FirstPoint = first;
		dimension.SecondPoint = second;

		DimensionCandidateDiagnostic diagnostic = plan.Diagnostics.DimensionCandidates
			.SingleOrDefault(candidate => candidate.Id == dimension.DiagnosticId);
		if (diagnostic == null)
		{
			return;
		}
		diagnostic.Value = dimension.Orientation == DimensionOrientation.Vertical
			? Math.Abs(second.Y - first.Y)
			: Math.Abs(second.X - first.X);
		diagnostic.FirstPointX = first.X;
		diagnostic.FirstPointY = first.Y;
		diagnostic.SecondPointX = second.X;
		diagnostic.SecondPointY = second.Y;
		diagnostic.MeasurementMinimum = dimension.Orientation == DimensionOrientation.Vertical
			? Math.Min(first.Y, second.Y)
			: Math.Min(first.X, second.X);
		diagnostic.MeasurementMaximum = dimension.Orientation == DimensionOrientation.Vertical
			? Math.Max(first.Y, second.Y)
			: Math.Max(first.X, second.X);
	}

	private void SuppressDerivedInnerEdge(DimensionPlan plan, LProfileMatch match)
	{
		List<PlannedDimension> inner = plan.Dimensions
			.Where(IsLProfileLinearCandidate)
			.Where(d => CanonicalOrientation(d, match) == DimensionOrientation.Vertical)
			.Where(d => IsCanonicalCrossCoordinate(d, match, match.CommonVertical.Cross, horizontal: false))
			.Where(d => IsCanonicalInterval(d, match, DimensionOrientation.Vertical, match.CommonVertical.MinT, match.CommonVertical.MaxT))
			.ToList();
		foreach (PlannedDimension dimension in inner)
		{
			SuppressLProfileDimension(plan, dimension, LProfileDerivedInnerEdgeRuleId, match);
		}
	}

	private void StampDerivedInnerEdgeDiagnostics(DimensionPlan plan, LProfileMatch match)
	{
		foreach (DimensionCandidateDiagnostic diagnostic in plan.Diagnostics.DimensionCandidates)
		{
			if (!string.Equals(diagnostic.Kind, DimensionKind.Normal.ToString(), StringComparison.Ordinal)
				|| !Enum.TryParse(diagnostic.Orientation, out DimensionOrientation orientation))
			{
				continue;
			}
			PlannedDimension probe = new PlannedDimension
			{
				Orientation = orientation,
				FirstPoint = new Point2D(diagnostic.FirstPointX, diagnostic.FirstPointY),
				SecondPoint = new Point2D(diagnostic.SecondPointX, diagnostic.SecondPointY)
			};
			if (CanonicalOrientation(probe, match) != DimensionOrientation.Vertical
				|| !IsCanonicalCrossCoordinate(probe, match, match.CommonVertical.Cross, horizontal: false)
				|| !IsCanonicalInterval(probe, match, DimensionOrientation.Vertical, match.CommonVertical.MinT, match.CommonVertical.MaxT))
			{
				continue;
			}
			diagnostic.IsSuppressed = true;
			diagnostic.IsSelected = false;
			diagnostic.DecisionStatus = "Suppressed";
			diagnostic.DecisionReason = LProfileDerivedInnerEdgeRuleId;
			diagnostic.SuppressedReason = LProfileDerivedInnerEdgeRuleId;
			diagnostic.RuleId = LProfileDerivedInnerEdgeRuleId;
			diagnostic.TopologyEvidence = match.TopologyEvidence;
			diagnostic.SourceGeometryIds = diagnostic.SourceGeometryIds
				.Concat(match.SourceGeometryIds)
				.Where(source => !string.IsNullOrEmpty(source))
				.Distinct(StringComparer.Ordinal)
				.ToList();
			diagnostic.Decision = DimensionCandidateDecision.Suppressed;
		}
	}

	private void SuppressReplacedVerticalArmWidth(DimensionPlan plan, LProfileMatch match, PlannedDimension replacement)
	{
		List<PlannedDimension> replaced = plan.Dimensions
			.Where(IsLProfileLinearCandidate)
			.Where(d => !ReferenceEquals(d, replacement))
			.Where(d => CanonicalOrientation(d, match) == DimensionOrientation.Horizontal)
			.Where(d => CanonicalSide(d, match) == match.OuterHorizontalSide)
			.Where(d => IsCanonicalInterval(d, match, DimensionOrientation.Horizontal,
				match.ReplacedVerticalArmWidthMinimum,
				match.ReplacedVerticalArmWidthMaximum))
			.ToList();
		foreach (PlannedDimension dimension in replaced)
		{
			SuppressLProfileDimension(plan, dimension, LProfileVerticalArmWidthRuleId, match);
		}
	}

	private void SuppressInternalChordEndpointCandidates(DimensionPlan plan, LProfileMatch match)
	{
		List<LProfileEdge> nonBoundaryEdges = match.Graph.AllEdges
			.Where(edge => !match.Graph.IsBoundaryEdge(edge))
			.ToList();
		List<LProfileAxisChain> openInteriorChords = LProfileAxisChain.Build(nonBoundaryEdges, horizontal: true, tolerance: _config.GeometryTolerance)
			.Concat(LProfileAxisChain.Build(nonBoundaryEdges, horizontal: false, tolerance: _config.GeometryTolerance))
			.Where(chain => IsOpenInteriorChord(chain, match.Graph.BoundaryOutline, _config.GeometryTolerance))
			.ToList();

		foreach (PlannedDimension dimension in plan.Dimensions.ToList())
		{
			if (!IsLProfileLinearCandidate(dimension) || IsLProfileOwnedDimension(dimension))
			{
				continue;
			}
			DimensionOrientation orientation = CanonicalOrientation(dimension, match);
			Point2D first = ToCanonicalPoint(dimension.FirstPoint, match);
			Point2D second = ToCanonicalPoint(dimension.SecondPoint, match);
			bool isStepGroove = IsLProfileStepGroove(dimension);
			bool endpointsOnOuterContour = !isStepGroove
				|| (OutlineGeometryQuery.IsPointOnBoundary(first, match.Graph.BoundaryOutline, _config.GeometryTolerance)
					&& OutlineGeometryQuery.IsPointOnBoundary(second, match.Graph.BoundaryOutline, _config.GeometryTolerance));
			if (isStepGroove)
			{
				plan.SetAttachmentValidity(dimension, endpointsOnOuterContour);
				if (!endpointsOnOuterContour)
				{
					SuppressLProfileDimension(plan, dimension, LProfileInternalChordEndpointRuleId, match);
					continue;
				}
			}
			LProfileAxisChain stepGrooveChord = FindLProfileDerivedStepGrooveChord(
				dimension,
				match,
				openInteriorChords);
			if (IsOpenInteriorChord(first, second, match.Graph.BoundaryOutline, _config.GeometryTolerance)
				|| stepGrooveChord != null)
			{
				SuppressLProfileDimension(
					plan,
					dimension,
					LProfileInternalChordEndpointRuleId,
					match,
					stepGrooveChord == null ? null : stepGrooveChord.Edges);
				continue;
			}
			foreach (LProfileAxisChain chord in openInteriorChords.Where(chain => chain.Horizontal == (orientation == DimensionOrientation.Vertical)))
			{
				if (!IsPointOnAxisChain(first, chord, _config.GeometryTolerance)
					&& !IsPointOnAxisChain(second, chord, _config.GeometryTolerance))
				{
					continue;
				}
				SuppressLProfileDimension(plan, dimension, LProfileInternalChordEndpointRuleId, match, chord.Edges);
				break;
			}
		}
	}

	private LProfileAxisChain FindLProfileDerivedStepGrooveChord(
		PlannedDimension dimension,
		LProfileMatch match,
		IEnumerable<LProfileAxisChain> openInteriorChords)
	{
		if (dimension == null || match == null || match.InnerHorizontal == null
			|| !IsLProfileStepGroove(dimension))
		{
			return null;
		}
		if (CanonicalOrientation(dimension, match) != DimensionOrientation.Horizontal)
		{
			return null;
		}
		Point2D first = ToCanonicalPoint(dimension.FirstPoint, match);
		Point2D second = ToCanonicalPoint(dimension.SecondPoint, match);
		double transitionBand = GetLProfileTransitionBand(match);
		double cross = (first.Y + second.Y) * 0.5;
		double minimum = Math.Min(first.X, second.X);
		double maximum = Math.Max(first.X, second.X);
		foreach (LProfileAxisChain chord in (openInteriorChords ?? new LProfileAxisChain[0])
			.Where(chain => chain.Horizontal))
		{
			if (Math.Abs(cross - chord.Cross) > transitionBand + match.Tolerance
				|| minimum > chord.MinT + match.Tolerance
				|| maximum < chord.MaxT - match.Tolerance)
			{
				continue;
			}
			double endpointBand = Math.Max(transitionBand * 2.0, match.Tolerance * 20.0);
			if (chord.MinT - minimum > endpointBand + match.Tolerance
				|| maximum - chord.MaxT > endpointBand + match.Tolerance)
			{
				continue;
			}
			return chord;
		}
		return null;
	}

	private static bool IsLProfileStepGroove(PlannedDimension dimension)
	{
		return dimension != null
			&& !string.IsNullOrEmpty(dimension.SourceKey)
			&& dimension.SourceKey.StartsWith("StepGroove", StringComparison.Ordinal);
	}

	private static double GetLProfileTransitionBand(LProfileMatch match)
	{
		double maximumRadius = match.Graph.BoundaryEdges
			.Where(edge => edge != null && edge.Arc != null)
			.Select(edge => edge.Arc.Radius)
			.DefaultIfEmpty(0.0)
			.Max();
		return Math.Max(match.Tolerance * 20.0, maximumRadius * 0.5);
	}

	private void SuppressDerivedOuterContourSegments(DimensionPlan plan, LProfileMatch match)
	{
		if (plan == null || match == null)
		{
			return;
		}
		foreach (PlannedDimension dimension in plan.Dimensions.ToList())
		{
			if (!IsLProfileLinearCandidate(dimension) || IsLProfileOwnedDimension(dimension))
			{
				continue;
			}
			if (!IsCanonicalChainSpan(dimension, match, match.InnerHorizontal)
				&& !IsCanonicalChainSpan(dimension, match, match.OppositeOuterVertical))
			{
				continue;
			}
			SuppressLProfileDimension(plan, dimension, LProfileDerivedOuterContourSegmentRuleId, match);
		}
	}

	private static bool IsLProfileOwnedDimension(PlannedDimension dimension)
	{
		return dimension != null
			&& (string.Equals(dimension.RuleId, LProfileVerticalArmWidthRuleId, StringComparison.Ordinal)
				|| string.Equals(dimension.RuleId, LProfileHorizontalArmHeightRuleId, StringComparison.Ordinal));
	}

	private static bool IsCanonicalChainSpan(
		PlannedDimension dimension,
		LProfileMatch match,
		LProfileAxisChain chain)
	{
		if (dimension == null || match == null || chain == null)
		{
			return false;
		}
		DimensionOrientation expected = chain.Horizontal
			? DimensionOrientation.Horizontal
			: DimensionOrientation.Vertical;
		if (CanonicalOrientation(dimension, match) != expected)
		{
			return false;
		}
		Point2D first = ToCanonicalPoint(dimension.FirstPoint, match);
		Point2D second = ToCanonicalPoint(dimension.SecondPoint, match);
		return IsPointOnAxisChain(first, chain, match.Tolerance)
			&& IsPointOnAxisChain(second, chain, match.Tolerance)
			&& IsCanonicalInterval(dimension, match, expected, chain.MinT, chain.MaxT);
	}

	private void SuppressLProfileDimension(
		DimensionPlan plan,
		PlannedDimension dimension,
		string ruleId,
		LProfileMatch match,
		IEnumerable<LProfileEdge> extraEdges = null)
	{
		IEnumerable<string> sourceIds = match.SourceGeometryIds
			.Concat((extraEdges ?? new LProfileEdge[0]).Select(edge => edge.SourceKey))
			.Where(source => !string.IsNullOrEmpty(source))
			.Distinct(StringComparer.Ordinal);
		plan.MarkSuppressed(dimension, ruleId, ruleId, sourceIds, match.TopologyEvidence);
		plan.Dimensions.Remove(dimension);
	}

	private static bool IsStructureDimension(PlannedDimension dimension)
	{
		return dimension != null
			&& dimension.Kind == DimensionKind.Normal
			&& dimension.Role == DimensionCandidateRole.Structure;
	}

	private static bool IsLProfileLinearCandidate(PlannedDimension dimension)
	{
		return IsStructureDimension(dimension)
			|| (dimension != null
				&& dimension.Kind == DimensionKind.Normal
				&& dimension.Role == DimensionCandidateRole.OutlineSegment);
	}

	private static bool IsCanonicalInterval(
		PlannedDimension dimension,
		LProfileMatch match,
		DimensionOrientation orientation,
		double minimum,
		double maximum)
	{
		Tuple<double, double> interval = GetCanonicalInterval(
			ToCanonicalPoint(dimension.FirstPoint, match),
			ToCanonicalPoint(dimension.SecondPoint, match),
			orientation);
		return Math.Abs(interval.Item1 - minimum) <= match.Tolerance
			&& Math.Abs(interval.Item2 - maximum) <= match.Tolerance;
	}

	private static bool IsCanonicalCrossCoordinate(PlannedDimension dimension, LProfileMatch match, double coordinate, bool horizontal)
	{
		Point2D first = ToCanonicalPoint(dimension.FirstPoint, match);
		Point2D second = ToCanonicalPoint(dimension.SecondPoint, match);
		double actual = horizontal ? (first.Y + second.Y) * 0.5 : (first.X + second.X) * 0.5;
		return Math.Abs(actual - coordinate) <= match.Tolerance;
	}

	private static Tuple<double, double> GetCanonicalInterval(Point2D first, Point2D second, DimensionOrientation orientation)
	{
		double firstAxis = orientation == DimensionOrientation.Horizontal ? first.X : first.Y;
		double secondAxis = orientation == DimensionOrientation.Horizontal ? second.X : second.Y;
		return Tuple.Create(Math.Min(firstAxis, secondAxis), Math.Max(firstAxis, secondAxis));
	}

	private static DimensionOrientation CanonicalOrientation(PlannedDimension dimension, LProfileMatch match)
	{
		if (!match.Transposed)
		{
			return dimension.Orientation;
		}
		return dimension.Orientation == DimensionOrientation.Horizontal
			? DimensionOrientation.Vertical
			: DimensionOrientation.Horizontal;
	}

	private static DimensionSide CanonicalSide(PlannedDimension dimension, LProfileMatch match)
	{
		return match.Transposed ? SwapDimensionSide(dimension.Side) : dimension.Side;
	}

	private static Point2D ToCanonicalPoint(Point2D point, LProfileMatch match)
	{
		return match.Transposed ? new Point2D(point.Y, point.X) : point;
	}

	private static PlannedDimension ToWorldLProfileDimension(PlannedDimension canonical, LProfileMatch match)
	{
		canonical.FirstPoint = FromCanonicalPoint(canonical.FirstPoint, match);
		canonical.SecondPoint = FromCanonicalPoint(canonical.SecondPoint, match);
		if (match.Transposed)
		{
			canonical.Orientation = canonical.Orientation == DimensionOrientation.Horizontal
				? DimensionOrientation.Vertical
				: DimensionOrientation.Horizontal;
			canonical.Side = SwapDimensionSide(canonical.Side);
			canonical.DebugRole = GetDebugRole(canonical.Orientation, canonical.Side, match);
			canonical.AlignmentKey = GetAlignmentKey(canonical.Orientation, canonical.Side, match);
		}
		return canonical;
	}

	private static Point2D FromCanonicalPoint(Point2D point, LProfileMatch match)
	{
		return match.Transposed ? new Point2D(point.Y, point.X) : point;
	}

	private static DimensionSide SwapDimensionSide(DimensionSide side)
	{
		return side switch
		{
			DimensionSide.Top => DimensionSide.Right,
			DimensionSide.Bottom => DimensionSide.Left,
			DimensionSide.Left => DimensionSide.Bottom,
			DimensionSide.Right => DimensionSide.Top,
			_ => side
		};
	}

	private static string GetDebugRole(DimensionOrientation orientation, DimensionSide side, LProfileMatch match)
	{
		if (orientation == DimensionOrientation.Horizontal)
		{
			return side == DimensionSide.Top ? "TopStructWidth" : "BottomStructWidth";
		}
		return side == DimensionSide.Right ? "RightStructHeight" : "LeftStructHeight";
	}

	private static string GetAlignmentKey(DimensionOrientation orientation, DimensionSide side, LProfileMatch match)
	{
		return "Structure:" + (orientation == DimensionOrientation.Horizontal ? "H" : "V") + ":" + side;
	}

	private static bool IsOpenInteriorChord(LProfileAxisChain chain, OutlineFeature2D boundary, double tolerance)
	{
		if (chain == null || boundary == null || chain.Span <= tolerance)
		{
			return false;
		}
		return IsOpenInteriorChord(
			chain.GetEndpoint(minimum: true),
			chain.GetEndpoint(minimum: false),
			boundary,
			tolerance);
	}

	private static bool IsOpenInteriorChord(
		Point2D first,
		Point2D second,
		OutlineFeature2D boundary,
		double tolerance)
	{
		if (boundary == null)
		{
			return false;
		}
		bool horizontal = Math.Abs(first.Y - second.Y) <= tolerance;
		bool vertical = Math.Abs(first.X - second.X) <= tolerance;
		if (horizontal == vertical)
		{
			return false;
		}
		if (!OutlineGeometryQuery.IsPointOnBoundary(first, boundary, tolerance)
			|| !OutlineGeometryQuery.IsPointOnBoundary(second, boundary, tolerance))
		{
			return false;
		}
		double minimum = horizontal ? Math.Min(first.X, second.X) : Math.Min(first.Y, second.Y);
		double maximum = horizontal ? Math.Max(first.X, second.X) : Math.Max(first.Y, second.Y);
		double cross = horizontal ? (first.Y + second.Y) * 0.5 : (first.X + second.X) * 0.5;
		IList<Point2D> crossings = horizontal
			? OutlineGeometryQuery.GetHorizontalBoundaryIntersections(boundary, cross, tolerance)
			: OutlineGeometryQuery.GetVerticalBoundaryIntersections(boundary, cross, tolerance);
		foreach (Point2D crossing in crossings)
		{
			double axis = horizontal ? crossing.X : crossing.Y;
			if (axis > minimum + tolerance && axis < maximum - tolerance)
			{
				return false;
			}
		}
		foreach (double fraction in new[] { 0.2, 0.5, 0.8 })
		{
			Point2D sample = new Point2D(
				first.X + (second.X - first.X) * fraction,
				first.Y + (second.Y - first.Y) * fraction);
			if (OutlineGeometryQuery.IsPointOnBoundary(sample, boundary, tolerance)
				|| !IsInsideBoundary(sample, boundary, tolerance))
			{
				return false;
			}
		}
		return true;
	}

	private static bool IsInsideBoundary(Point2D point, OutlineFeature2D boundary, double tolerance)
	{
		if (OutlineGeometryQuery.IsPointOnBoundary(point, boundary, tolerance))
		{
			return false;
		}
		return OutlineGeometryQuery.GetHorizontalBoundaryIntersections(boundary, point.Y, tolerance)
			.Count(candidate => candidate.X > point.X + tolerance) % 2 == 1;
	}

	private static bool IsPointOnAxisChain(Point2D point, LProfileAxisChain chain, double tolerance)
	{
		double axis = chain.Horizontal ? point.X : point.Y;
		double cross = chain.Horizontal ? point.Y : point.X;
		return Math.Abs(cross - chain.Cross) <= tolerance
			&& axis >= chain.MinT - tolerance
			&& axis <= chain.MaxT + tolerance;
	}

	private static OutlineFeature2D TransposeLProfileOutline(OutlineFeature2D source)
	{
		OutlineFeature2D target = new OutlineFeature2D
		{
			MinX = source.MinY,
			MaxX = source.MaxY,
			MinY = source.MinX,
			MaxY = source.MaxX
		};
		foreach (Point2D vertex in source.Vertices)
		{
			target.Vertices.Add(new Point2D(vertex.Y, vertex.X));
		}
		foreach (Segment2D segment in source.Segments)
		{
			if (segment == null)
			{
				target.Segments.Add(null);
				continue;
			}
			target.Segments.Add(new Segment2D(
				new Point2D(segment.Start.Y, segment.Start.X),
				new Point2D(segment.End.Y, segment.End.X))
			{
				SourceKey = segment.SourceKey,
				IsArcChord = segment.IsArcChord
			});
		}
		foreach (Arc2D arc in source.Arcs)
		{
			target.Arcs.Add(new Arc2D
			{
				Start = new Point2D(arc.Start.Y, arc.Start.X),
				End = new Point2D(arc.End.Y, arc.End.X),
				Center = new Point2D(arc.Center.Y, arc.Center.X),
				Radius = arc.Radius,
				Bulge = -arc.Bulge,
				SourceKey = arc.SourceKey
			});
		}
		return target;
	}

	private sealed class LProfileOuterContour
	{
		public LProfileBoundaryGraph Graph { get; private set; }

		public List<LProfileAxisChain> HorizontalChains { get; private set; }

		public List<LProfileAxisChain> VerticalChains { get; private set; }

		public LProfileConcaveTurn Turn { get; private set; }

		public string TopologyEvidence { get; private set; }

		private LProfileOuterContour()
		{
		}

		public static LProfileOuterContour TryCreate(OutlineFeature2D outline, double tolerance, out string failureReason)
		{
			failureReason = string.Empty;
			if (outline == null)
			{
				failureReason = "NullOutline";
				return null;
			}
			LProfileBoundaryGraph graph = LProfileBoundaryGraph.TryCreate(outline, tolerance);
			if (graph == null)
			{
				failureReason = "BoundaryGraphUnavailable";
				return null;
			}
			List<LProfileAxisChain> horizontalChains = LProfileAxisChain.Build(graph.BoundaryEdges, horizontal: true, tolerance);
			List<LProfileAxisChain> verticalChains = LProfileAxisChain.Build(graph.BoundaryEdges, horizontal: false, tolerance);
			List<LProfileConcaveTurn> turns = graph.FindConcaveTurns(horizontalChains, verticalChains, tolerance);
			if (turns.Count != 1)
			{
				failureReason = "EffectiveConcaveTurnCount=" + turns.Count;
				return null;
			}
			return new LProfileOuterContour
			{
				Graph = graph,
				HorizontalChains = horizontalChains,
				VerticalChains = verticalChains,
				Turn = turns[0],
				TopologyEvidence = "OuterContour|EffectiveConcaveTurnCount=1|HorizontalChain="
					+ turns[0].HorizontalChain.Cross.ToString("0.########")
					+ "|VerticalChain=" + turns[0].VerticalChain.Cross.ToString("0.########")
			};
		}

		public bool TryGetInteriorPositiveSide(LProfileAxisChain chain, bool horizontal, double tolerance, out bool positive)
		{
			positive = false;
			if (chain == null || chain.Span <= tolerance || Graph?.BoundaryOutline == null)
			{
				return false;
			}
			double scale = Math.Max(Graph.BoundaryOutline.Width, Graph.BoundaryOutline.Height);
			double probe = Math.Max(tolerance * 10.0, scale * 1E-6);
			Point2D center = chain.GetPointAt((chain.MinT + chain.MaxT) * 0.5);
			for (int attempt = 0; attempt < 3; attempt++)
			{
				Point2D plus = horizontal
					? new Point2D(center.X, center.Y + probe)
					: new Point2D(center.X + probe, center.Y);
				Point2D minus = horizontal
					? new Point2D(center.X, center.Y - probe)
					: new Point2D(center.X - probe, center.Y);
				bool plusInside = DimensionPlanner.IsInsideBoundary(plus, Graph.BoundaryOutline, tolerance);
				bool minusInside = DimensionPlanner.IsInsideBoundary(minus, Graph.BoundaryOutline, tolerance);
				if (plusInside != minusInside)
				{
					positive = plusInside;
					return true;
				}
				probe *= 0.25;
			}
			return false;
		}

		public bool TryGetVerticalClosureProjections(LProfileAxisChain chain, double tolerance, out List<double> projections)
		{
			projections = new List<double>();
			if (chain == null || chain.Horizontal || chain.Span <= tolerance)
			{
				return false;
			}
			foreach (Point2D endpoint in new[] { chain.GetEndpoint(minimum: true), chain.GetEndpoint(minimum: false) })
			{
				double horizontalCross;
				if (!TryFindAdjacentHorizontalCross(chain, endpoint, tolerance, out horizontalCross))
				{
					return false;
				}
				projections.Add(Math.Abs(endpoint.Y - horizontalCross));
			}
			return projections.Count == 2;
		}

		private bool TryFindAdjacentHorizontalCross(LProfileAxisChain chain, Point2D endpoint, double tolerance, out double cross)
		{
			cross = 0.0;
			for (int i = 0; i < Graph.BoundaryPath.Count; i++)
			{
				LProfileHalfEdge current = Graph.BoundaryPath[i];
				if (!chain.Edges.Contains(current.Edge))
				{
					continue;
				}
				int step;
				if (current.ToPoint.DistanceTo(endpoint) <= tolerance)
				{
					step = 1;
				}
				else if (current.FromPoint.DistanceTo(endpoint) <= tolerance)
				{
					step = -1;
				}
				else
				{
					continue;
				}
				for (int offset = 1; offset < Graph.BoundaryPath.Count; offset++)
				{
					int nextIndex = (i + step * offset) % Graph.BoundaryPath.Count;
					if (nextIndex < 0)
					{
						nextIndex += Graph.BoundaryPath.Count;
					}
					LProfileHalfEdge next = Graph.BoundaryPath[nextIndex];
					if (next.Edge.Segment == null)
					{
						continue;
					}
					if (next.Edge.Segment.IsHorizontal(tolerance))
					{
						cross = next.Edge.Segment.Start.Y;
						return true;
					}
					if (next.Edge.Segment.IsVertical(tolerance))
					{
						break;
					}
				}
			}
			return false;
		}
	}

	private sealed class LProfileConcaveTurn
	{
		public LProfileAxisChain HorizontalChain { get; set; }

		public LProfileAxisChain VerticalChain { get; set; }

		public Point2D HorizontalPoint { get; set; }

		public Point2D VerticalPoint { get; set; }

		public List<LProfileEdge> TransitionEdges { get; set; }
	}

	private sealed class LProfileMatch
	{
		public OutlineFeature2D Outline { get; private set; }

		public LProfileBoundaryGraph Graph { get; private set; }

		public LProfileAxisChain CommonVertical { get; private set; }

		public LProfileAxisChain InnerHorizontal { get; private set; }

		public LProfileAxisChain OppositeOuterVertical { get; private set; }

		public DimensionSide OuterHorizontalSide { get; private set; }

		public DimensionSide ArmVerticalSide { get; private set; }

		public Point2D VerticalArmWidthFirstPoint { get; private set; }

		public Point2D VerticalArmWidthSecondPoint { get; private set; }

		public Point2D HorizontalArmHeightFirstPoint { get; private set; }

		public Point2D HorizontalArmHeightSecondPoint { get; private set; }

		public List<string> SourceGeometryIds { get; private set; }

		public string TopologyEvidence { get; private set; }

		public bool FormulaClosed { get; private set; }

		public double ReplacedVerticalArmWidthMinimum { get; private set; }

		public double ReplacedVerticalArmWidthMaximum { get; private set; }

		public bool Transposed { get; set; }

		public string AnnotationTopology { get; private set; }

		public double Tolerance { get; private set; }

		private LProfileMatch()
		{
		}

		public static LProfileMatch TryCreateOrthogonalInnerCorner(
			OutlineFeature2D outline,
			LProfileOuterContour outerContour,
			double tolerance,
			out string failureReason)
		{
			failureReason = string.Empty;
			if (outline == null || outerContour == null || outerContour.Turn == null)
			{
				failureReason = "OuterContourUnavailable";
				return null;
			}
			LProfileAxisChain innerHorizontal = outerContour.Turn.HorizontalChain;
			LProfileAxisChain innerVertical = outerContour.Turn.VerticalChain;
			bool innerHorizontalPositive;
			if (!outerContour.TryGetInteriorPositiveSide(innerHorizontal, true, tolerance, out innerHorizontalPositive))
			{
				failureReason = "InnerHorizontalInteriorSideAmbiguous";
				return null;
			}
			bool innerVerticalPositive;
			if (!outerContour.TryGetInteriorPositiveSide(innerVertical, false, tolerance, out innerVerticalPositive))
			{
				failureReason = "InnerVerticalInteriorSideAmbiguous";
				return null;
			}

			LProfileAxisChain outerHorizontal = FindUniqueEnvelopeChain(
				outerContour.HorizontalChains,
				innerHorizontalPositive ? outline.MinY : outline.MaxY,
				tolerance);
			LProfileAxisChain outerVertical = FindUniqueEnvelopeChain(
				outerContour.VerticalChains,
				innerVerticalPositive ? outline.MaxX : outline.MinX,
				tolerance);
			if (outerHorizontal == null || outerVertical == null
				|| ReferenceEquals(outerHorizontal, innerHorizontal)
				|| ReferenceEquals(outerVertical, innerVertical))
			{
				failureReason = "OuterParallelChainAmbiguous";
				return null;
			}

			Point2D outerVerticalPoint;
			if (!TryGetAxisChainPoint(outerVertical, innerVertical.GetAxis(outerContour.Turn.VerticalPoint), outerContour.Graph.BoundaryOutline, tolerance, out outerVerticalPoint))
			{
				failureReason = "OuterVerticalAttachmentUnavailable";
				return null;
			}

			Point2D innerFarPoint;
			if (!innerHorizontal.TryGetOppositeEndpoint(outerContour.Turn.HorizontalPoint, tolerance, out innerFarPoint))
			{
				failureReason = "InnerHorizontalOppositeEndpointUnavailable";
				return null;
			}
			LProfileAxisChain armVertical = TryGetUniqueVerticalChainAt(
				outerContour.Graph,
				outerContour.VerticalChains,
				innerFarPoint,
				tolerance);
			if (armVertical == null)
			{
				failureReason = "HorizontalArmVerticalChainUnavailable";
				return null;
			}
			armVertical = ExtendVerticalChainToEnvelope(
				outerContour.Graph,
				armVertical,
				innerFarPoint,
				outline,
				tolerance);
			if (armVertical == null || armVertical.Span <= tolerance
				|| !IsEnvelopeCoordinate(armVertical.Cross, outline.MinX, outline.MaxX, tolerance))
			{
				failureReason = "HorizontalArmVerticalChainNotEnvelopeBacked";
				return null;
			}
			LProfileAxisChain oppositeOuterVertical = FindUniqueEnvelopeChain(
				outerContour.VerticalChains,
				Math.Abs(armVertical.Cross - outline.MaxX) <= tolerance ? outline.MinX : outline.MaxX,
				tolerance);

			Point2D armFirstPoint = armVertical.GetEndpoint(minimum: true);
			Point2D armSecondPoint = armVertical.GetEndpoint(minimum: false);
			if (!OutlineGeometryQuery.IsPointOnBoundary(outerVerticalPoint, outerContour.Graph.BoundaryOutline, tolerance)
				|| !OutlineGeometryQuery.IsPointOnBoundary(outerContour.Turn.VerticalPoint, outerContour.Graph.BoundaryOutline, tolerance)
				|| !OutlineGeometryQuery.IsPointOnBoundary(outerContour.Turn.HorizontalPoint, outerContour.Graph.BoundaryOutline, tolerance)
				|| !OutlineGeometryQuery.IsPointOnBoundary(armFirstPoint, outerContour.Graph.BoundaryOutline, tolerance)
				|| !OutlineGeometryQuery.IsPointOnBoundary(armSecondPoint, outerContour.Graph.BoundaryOutline, tolerance))
			{
				failureReason = "LProfileAttachmentPointNotOnBoundary";
				return null;
			}

			List<double> closureProjections;
			bool closureAvailable = outerContour.TryGetVerticalClosureProjections(innerVertical, tolerance, out closureProjections);
			double expected = outline.Height - armVertical.Span;
			double actual = innerVertical.Span + (closureAvailable ? closureProjections.Sum() : double.NaN);
			bool formulaClosed = closureAvailable && Math.Abs(actual - expected) <= tolerance;
			LProfileMatch match = new LProfileMatch
			{
				Outline = outline,
				Graph = outerContour.Graph,
				CommonVertical = innerVertical,
				InnerHorizontal = innerHorizontal,
				OppositeOuterVertical = oppositeOuterVertical,
				OuterHorizontalSide = innerHorizontalPositive ? DimensionSide.Bottom : DimensionSide.Top,
				ArmVerticalSide = armVertical.Cross >= outline.MaxX - tolerance ? DimensionSide.Right : DimensionSide.Left,
				VerticalArmWidthFirstPoint = outerVerticalPoint,
				VerticalArmWidthSecondPoint = outerContour.Turn.VerticalPoint,
				HorizontalArmHeightFirstPoint = armFirstPoint,
				HorizontalArmHeightSecondPoint = armSecondPoint,
				SourceGeometryIds = GetSourceGeometryIds(outerContour, outerHorizontal, outerVertical, armVertical),
				TopologyEvidence = outerContour.TopologyEvidence
					+ "|Annotation=ConnectedInnerHorizontalVerticalCorner"
					+ "|ClosureExpected=" + expected.ToString("0.########")
					+ "|ClosureActual=" + (closureAvailable ? actual.ToString("0.########") : "Unavailable")
					+ (formulaClosed ? "|Closed" : "|Open"),
				FormulaClosed = formulaClosed,
				ReplacedVerticalArmWidthMinimum = Math.Min(outerVertical.Cross, innerVertical.Cross),
				ReplacedVerticalArmWidthMaximum = Math.Max(outerVertical.Cross, innerVertical.Cross),
				AnnotationTopology = "ConcaveCornerInnerChains",
				Tolerance = tolerance
			};
			return match;
		}

		private static LProfileAxisChain FindUniqueEnvelopeChain(
			IEnumerable<LProfileAxisChain> chains,
			double coordinate,
			double tolerance)
		{
			List<LProfileAxisChain> matches = chains
				.Where(chain => chain.Span > tolerance && Math.Abs(chain.Cross - coordinate) <= tolerance)
				.ToList();
			return matches.Count == 1 ? matches[0] : null;
		}

		private static bool TryGetAxisChainPoint(
			LProfileAxisChain chain,
			double axis,
			OutlineFeature2D boundary,
			double tolerance,
			out Point2D point)
		{
			point = default(Point2D);
			if (chain == null || axis < chain.MinT - tolerance || axis > chain.MaxT + tolerance)
			{
				return false;
			}
			axis = Math.Max(chain.MinT, Math.Min(chain.MaxT, axis));
			point = chain.GetPointAt(axis);
			return OutlineGeometryQuery.IsPointOnBoundary(point, boundary, tolerance);
		}

		private static LProfileAxisChain ExtendVerticalChainToEnvelope(
			LProfileBoundaryGraph graph,
			LProfileAxisChain chain,
			Point2D seed,
			OutlineFeature2D outline,
			double tolerance)
		{
			for (int attempt = 0; attempt < graph.BoundaryEdges.Count; attempt++)
			{
				if (chain.IsConnectedToEnvelope(outline.MinY, outline.MaxY, tolerance))
				{
					return chain;
				}
				Point2D farPoint;
				if (!chain.TryGetOppositeEndpoint(seed, tolerance, out farPoint))
				{
					return null;
				}
				List<Tuple<LProfileEdge, Point2D?>> transitions = graph.BoundaryEdges
					.Where(edge => !chain.Edges.Contains(edge)
						&& IsTransitionEdge(edge, tolerance)
						&& edge.Touches(farPoint, tolerance))
					.Select(edge => Tuple.Create(edge, GetOtherEndpoint(edge, farPoint)))
					.Where(item => item.Item2.HasValue)
					.ToList();
				if (transitions.Count != 1)
				{
					return null;
				}
				Point2D nextPoint = transitions[0].Item2.Value;
				chain = chain.ExtendTo(nextPoint, transitions[0].Item1, tolerance);
				seed = nextPoint;
			}
			return null;
		}

		private static bool IsTransitionEdge(LProfileEdge edge, double tolerance)
		{
			return edge != null
				&& (edge.Arc != null
					|| (edge.Segment != null
						&& !edge.Segment.IsHorizontal(tolerance)
						&& !edge.Segment.IsVertical(tolerance)));
		}

		private static Point2D? GetOtherEndpoint(LProfileEdge edge, Point2D point)
		{
			if (edge == null)
			{
				return null;
			}
			Point2D first = edge.Segment != null ? edge.Segment.Start : edge.Arc.Start;
			Point2D second = edge.Segment != null ? edge.Segment.End : edge.Arc.End;
			if (first.DistanceTo(point) <= second.DistanceTo(point))
			{
				return second;
			}
			return first;
		}

		public static LProfileMatch TryCreate(OutlineFeature2D outline, double tolerance, out string failureReason)
		{
			failureReason = string.Empty;
			if (outline == null)
			{
				failureReason = "NullOutline";
				return null;
			}
			LProfileBoundaryGraph graph = LProfileBoundaryGraph.TryCreate(outline, tolerance);
			if (graph == null)
			{
				failureReason = "BoundaryGraphUnavailable";
				return null;
			}
			List<LProfileAxisChain> horizontalChains = LProfileAxisChain.Build(graph.BoundaryEdges, horizontal: true, tolerance);
			List<LProfileAxisChain> verticalChains = LProfileAxisChain.Build(graph.BoundaryEdges, horizontal: false, tolerance);
			List<LProfileArcAttachment> attachments = graph.BoundaryEdges
				.Where(edge => edge.Arc != null)
				.Select(edge => LProfileArcAttachment.TryCreate(edge, graph, horizontalChains, verticalChains, tolerance))
				.Where(attachment => attachment != null)
				.ToList();
			List<Tuple<LProfileArcAttachment, LProfileArcAttachment>> pairs = new List<Tuple<LProfileArcAttachment, LProfileArcAttachment>>();
			for (int i = 0; i < attachments.Count; i++)
			{
				for (int j = i + 1; j < attachments.Count; j++)
				{
					if (ReferenceEquals(attachments[i].VerticalChain, attachments[j].VerticalChain))
					{
						pairs.Add(Tuple.Create(attachments[i], attachments[j]));
					}
				}
			}
			if (pairs.Count != 1)
			{
				failureReason = "ConnectedCornerPairCount=" + pairs.Count;
				return null;
			}

			LProfileArcAttachment first = pairs[0].Item1;
			LProfileArcAttachment second = pairs[0].Item2;
			LProfileAxisChain common = first.VerticalChain;
			if (!IsDistinctCommonChainEndpoints(first, second, common, tolerance))
			{
				failureReason = "CommonInnerChainEndpointsInvalid";
				return null;
			}
			LProfileArcAttachment outer = new[] { first, second }
				.SingleOrDefault(attachment => IsEnvelopeCoordinate(attachment.HorizontalChain.Cross, outline.MinY, outline.MaxY, tolerance));
			LProfileArcAttachment inner = new[] { first, second }
				.SingleOrDefault(attachment => !IsEnvelopeCoordinate(attachment.HorizontalChain.Cross, outline.MinY, outline.MaxY, tolerance));
			if (outer == null || inner == null)
			{
				failureReason = "ArmHorizontalPlacementInvalid";
				return null;
			}
			Point2D outerFarPoint;
			Point2D innerFarPoint;
			if (!outer.HorizontalChain.TryGetOppositeEndpoint(outer.HorizontalPoint, tolerance, out outerFarPoint)
				|| !inner.HorizontalChain.TryGetOppositeEndpoint(inner.HorizontalPoint, tolerance, out innerFarPoint))
			{
				failureReason = "ArmHorizontalEndpointInvalid";
				return null;
			}
			LProfileAxisChain outerFarVertical = TryGetUniqueVerticalChainAt(graph, verticalChains, outerFarPoint, tolerance);
			LProfileAxisChain armVertical = TryGetUniqueVerticalChainAt(graph, verticalChains, innerFarPoint, tolerance);
			if (outerFarVertical == null || armVertical == null)
			{
				failureReason = "ArmVerticalChainUnavailable";
				return null;
			}
			if (ReferenceEquals(outerFarVertical, armVertical))
			{
				failureReason = "ArmVerticalChainsNotDistinct";
				return null;
			}
			if (!IsEnvelopeCoordinate(outerFarVertical.Cross, outline.MinX, outline.MaxX, tolerance)
				|| !IsEnvelopeCoordinate(armVertical.Cross, outline.MinX, outline.MaxX, tolerance))
			{
				failureReason = "ArmVerticalChainNotOnOuterEnvelope";
				return null;
			}
			if (!armVertical.IsConnectedToEnvelope(outline.MinY, outline.MaxY, tolerance)
				|| !outerFarVertical.IsConnectedToEnvelope(outline.MinY, outline.MaxY, tolerance))
			{
				failureReason = "ArmVerticalChainNotEnvelopeConnected";
				return null;
			}

			LProfileMatch match = new LProfileMatch
			{
				Outline = outline,
				Graph = graph,
				CommonVertical = common,
				InnerHorizontal = inner.HorizontalChain,
				OppositeOuterVertical = outerFarVertical,
				OuterHorizontalSide = outer.HorizontalChain.Cross >= outline.MaxY - tolerance ? DimensionSide.Top : DimensionSide.Bottom,
				ArmVerticalSide = armVertical.Cross >= outline.MaxX - tolerance ? DimensionSide.Right : DimensionSide.Left,
				VerticalArmWidthFirstPoint = outerFarPoint,
				VerticalArmWidthSecondPoint = outer.VerticalPoint,
				HorizontalArmHeightFirstPoint = armVertical.GetEndpoint(minimum: true),
				HorizontalArmHeightSecondPoint = armVertical.GetEndpoint(minimum: false),
				ReplacedVerticalArmWidthMinimum = outer.HorizontalChain.MinT,
				ReplacedVerticalArmWidthMaximum = outer.HorizontalChain.MaxT,
				SourceGeometryIds = GetSourceGeometryIds(graph, first, second, outerFarVertical, armVertical),
				TopologyEvidence = "LProfile|TwoConnectedCornerFeatures|OrthogonalArms|OuterContourComponent",
				AnnotationTopology = "SharedInnerVertical",
				Tolerance = tolerance
			};
			double projections = Math.Abs(first.VerticalPoint.Y - first.HorizontalChain.Cross)
				+ Math.Abs(second.VerticalPoint.Y - second.HorizontalChain.Cross);
			double expected = outline.Height - armVertical.Span;
			match.FormulaClosed = Math.Abs(common.Span + projections - expected) <= tolerance;
			match.TopologyEvidence += "|DerivedClosure=" + common.Span.ToString("0.########")
				+ "+" + projections.ToString("0.########")
				+ "=" + expected.ToString("0.########")
				+ "|Inner=" + common.MinT.ToString("0.########") + "," + common.MaxT.ToString("0.########")
				+ "|InnerCross=" + common.Cross.ToString("0.########")
				+ (match.FormulaClosed ? "|Closed" : "|Open");
			return match;
		}

		private static bool IsDistinctCommonChainEndpoints(LProfileArcAttachment first, LProfileArcAttachment second, LProfileAxisChain common, double tolerance)
		{
			double firstT = common.GetAxis(first.VerticalPoint);
			double secondT = common.GetAxis(second.VerticalPoint);
			return Math.Abs(firstT - secondT) > tolerance
				&& ((Math.Abs(firstT - common.MinT) <= tolerance && Math.Abs(secondT - common.MaxT) <= tolerance)
					|| (Math.Abs(secondT - common.MinT) <= tolerance && Math.Abs(firstT - common.MaxT) <= tolerance));
		}

		private static bool IsEnvelopeCoordinate(double value, double minimum, double maximum, double tolerance)
		{
			return Math.Abs(value - minimum) <= tolerance || Math.Abs(value - maximum) <= tolerance;
		}

		private static LProfileAxisChain TryGetUniqueVerticalChainAt(
			LProfileBoundaryGraph graph,
			IList<LProfileAxisChain> verticalChains,
			Point2D point,
			double tolerance)
		{
			List<LProfileAxisChain> chains = FindDirectVerticalChainsAt(graph, verticalChains, point, tolerance);
			if (chains.Count == 1)
			{
				return chains[0];
			}
			if (chains.Count != 0)
			{
				return null;
			}

			List<Tuple<LProfileAxisChain, LProfileEdge>> bridged = graph.BoundaryEdges
				.Where(edge => IsTransitionEdge(edge, tolerance) && edge.Touches(point, tolerance))
				.Select(edge => Tuple.Create(edge, GetOtherEndpoint(edge, point)))
				.Where(pair => pair.Item2.HasValue)
				.Select(pair => Tuple.Create(
					FindDirectVerticalChainsAt(graph, verticalChains, pair.Item2.Value, tolerance),
					pair.Item1))
				.Where(pair => pair.Item1.Count == 1)
				.Select(pair => Tuple.Create(pair.Item1[0], pair.Item2))
				.GroupBy(pair => pair.Item1)
				.Select(group => group.First())
				.ToList();
			if (bridged.Count != 1)
			{
				return null;
			}
			return bridged[0].Item1.ExtendTo(point, bridged[0].Item2, tolerance);
		}

		private static List<LProfileAxisChain> FindDirectVerticalChainsAt(
			LProfileBoundaryGraph graph,
			IList<LProfileAxisChain> verticalChains,
			Point2D point,
			double tolerance)
		{
			return graph.BoundaryEdges
				.Where(edge => edge.Segment != null && edge.Segment.IsVertical(tolerance) && edge.Touches(point, tolerance))
				.Select(edge => verticalChains.FirstOrDefault(chain => chain.Edges.Contains(edge)))
				.Where(chain => chain != null)
				.Distinct()
				.ToList();
		}

		private static List<string> GetSourceGeometryIds(
			LProfileBoundaryGraph graph,
			LProfileArcAttachment first,
			LProfileArcAttachment second,
			LProfileAxisChain outer,
			LProfileAxisChain arm)
		{
			return new[] { first.Edge, second.Edge }
				.Concat(first.VerticalChain.Edges)
				.Concat(second.HorizontalChain.Edges)
			.Concat(outer.Edges)
			.Concat(arm.Edges)
			.Select(edge => edge.SourceKey)
			.Where(source => !string.IsNullOrEmpty(source))
			.Distinct(StringComparer.Ordinal)
			.ToList();
		}

		private static List<string> GetSourceGeometryIds(
			LProfileOuterContour outerContour,
			LProfileAxisChain outerHorizontal,
			LProfileAxisChain outerVertical,
			LProfileAxisChain armVertical)
		{
			return outerContour.Turn.HorizontalChain.Edges
				.Concat(outerContour.Turn.VerticalChain.Edges)
				.Concat(outerHorizontal.Edges)
				.Concat(outerVertical.Edges)
				.Concat(armVertical.Edges)
				.Concat(outerContour.Turn.TransitionEdges)
				.Select(edge => edge.SourceKey)
				.Where(source => !string.IsNullOrEmpty(source))
				.Distinct(StringComparer.Ordinal)
				.ToList();
		}
	}

	private sealed class LProfileArcAttachment
	{
		public LProfileEdge Edge { get; private set; }

		public LProfileAxisChain HorizontalChain { get; private set; }

		public LProfileAxisChain VerticalChain { get; private set; }

		public Point2D HorizontalPoint { get; private set; }

		public Point2D VerticalPoint { get; private set; }

		public static LProfileArcAttachment TryCreate(
			LProfileEdge edge,
			LProfileBoundaryGraph graph,
			IList<LProfileAxisChain> horizontalChains,
			IList<LProfileAxisChain> verticalChains,
			double tolerance)
		{
			List<LProfileEdge> firstHorizontal = FindNeighbors(graph, edge.Arc.Start, horizontal: true, tolerance);
			List<LProfileEdge> firstVertical = FindNeighbors(graph, edge.Arc.Start, horizontal: false, tolerance);
			List<LProfileEdge> secondHorizontal = FindNeighbors(graph, edge.Arc.End, horizontal: true, tolerance);
			List<LProfileEdge> secondVertical = FindNeighbors(graph, edge.Arc.End, horizontal: false, tolerance);
			if (firstHorizontal.Count + secondHorizontal.Count != 1 || firstVertical.Count + secondVertical.Count != 1)
			{
				return null;
			}
			LProfileEdge horizontalEdge = firstHorizontal.Concat(secondHorizontal).SingleOrDefault();
			LProfileEdge verticalEdge = firstVertical.Concat(secondVertical).SingleOrDefault();
			if (horizontalEdge == null || verticalEdge == null)
			{
				return null;
			}
			LProfileAxisChain horizontalChain = horizontalChains.FirstOrDefault(chain => chain.Edges.Contains(horizontalEdge));
			LProfileAxisChain verticalChain = verticalChains.FirstOrDefault(chain => chain.Edges.Contains(verticalEdge));
			if (horizontalChain == null || verticalChain == null)
			{
				return null;
			}
			return new LProfileArcAttachment
			{
				Edge = edge,
				HorizontalChain = horizontalChain,
				VerticalChain = verticalChain,
				HorizontalPoint = firstHorizontal.Count == 1 ? edge.Arc.Start : edge.Arc.End,
				VerticalPoint = firstVertical.Count == 1 ? edge.Arc.Start : edge.Arc.End
			};
		}

		private static List<LProfileEdge> FindNeighbors(LProfileBoundaryGraph graph, Point2D point, bool horizontal, double tolerance)
		{
			return graph.BoundaryEdges
				.Where(edge => edge.Segment != null
					&& (horizontal ? edge.Segment.IsHorizontal(tolerance) : edge.Segment.IsVertical(tolerance))
					&& edge.Touches(point, tolerance))
				.ToList();
		}

	}

	private sealed class LProfileOverallSplitEvidence
	{
		public List<string> SourceGeometryIds { get; set; }

		public double ClosureResidual { get; set; }

		public string TopologyEvidence { get; set; }
	}

	private sealed class LProfileFilletProjection
	{
		public LProfileEdge ArcEdge { get; set; }

		public LProfileEdge OrthogonalEdge { get; set; }

		public Point2D LineTangentPoint { get; set; }

		public Point2D EnvelopePoint { get; set; }

		public double ComplementProjection { get; set; }
	}

	private sealed class LProfileAxisChain
	{
		public bool Horizontal { get; private set; }

		public double Cross { get; private set; }

		public double MinT { get; private set; }

		public double MaxT { get; private set; }

		public double Span => MaxT - MinT;

		public List<LProfileEdge> Edges { get; private set; }

		private Point2D? _minimumEndpointOverride;

		private Point2D? _maximumEndpointOverride;

		private LProfileAxisChain(bool horizontal, IList<LProfileEdge> edges, double tolerance)
		{
			Horizontal = horizontal;
			Edges = edges.ToList();
			Cross = horizontal ? Edges[0].Segment.Start.Y : Edges[0].Segment.Start.X;
			MinT = Edges.Min(edge => horizontal ? edge.Segment.MinX : edge.Segment.MinY);
			MaxT = Edges.Max(edge => horizontal ? edge.Segment.MaxX : edge.Segment.MaxY);
		}

		private LProfileAxisChain(LProfileAxisChain source, LProfileEdge bridge, Point2D endpoint, bool minimum)
		{
			Horizontal = source.Horizontal;
			Cross = source.Cross;
			Edges = source.Edges.Concat(new[] { bridge }).Distinct().ToList();
			MinT = source.MinT;
			MaxT = source.MaxT;
			_minimumEndpointOverride = source._minimumEndpointOverride;
			_maximumEndpointOverride = source._maximumEndpointOverride;
			if (minimum)
			{
				MinT = GetAxis(endpoint);
				_minimumEndpointOverride = endpoint;
			}
			else
			{
				MaxT = GetAxis(endpoint);
				_maximumEndpointOverride = endpoint;
			}
		}

		public double GetAxis(Point2D point)
		{
			return Horizontal ? point.X : point.Y;
		}

		public Point2D GetEndpoint(bool minimum)
		{
			Point2D? overridePoint = minimum ? _minimumEndpointOverride : _maximumEndpointOverride;
			if (overridePoint.HasValue)
			{
				return overridePoint.Value;
			}
			double target = minimum ? MinT : MaxT;
			return Edges.Where(edge => edge.Segment != null)
				.SelectMany(edge => new[] { edge.Segment.Start, edge.Segment.End })
				.OrderBy(point => Math.Abs(GetAxis(point) - target))
				.ThenBy(point => point.X)
				.ThenBy(point => point.Y)
				.First();
		}

		public Point2D GetPoint(double axis)
		{
			return GetPointAt(axis);
		}

		public Point2D GetPointAt(double axis)
		{
			return Horizontal ? new Point2D(axis, Cross) : new Point2D(Cross, axis);
		}

		public bool TryGetOppositeEndpoint(Point2D point, double tolerance, out Point2D opposite)
		{
			opposite = default(Point2D);
			double axis = GetAxis(point);
			if (Math.Abs(axis - MinT) <= tolerance)
			{
				opposite = GetEndpoint(minimum: false);
				return true;
			}
			if (Math.Abs(axis - MaxT) <= tolerance)
			{
				opposite = GetEndpoint(minimum: true);
				return true;
			}
			return false;
		}

		public bool IsConnectedToEnvelope(double minimum, double maximum, double tolerance)
		{
			return Math.Abs(MinT - minimum) <= tolerance || Math.Abs(MaxT - maximum) <= tolerance;
		}

		public LProfileAxisChain ExtendTo(Point2D endpoint, LProfileEdge bridge, double tolerance)
		{
			double axis = GetAxis(endpoint);
			if (axis < MinT - tolerance)
			{
				return new LProfileAxisChain(this, bridge, endpoint, minimum: true);
			}
			if (axis > MaxT + tolerance)
			{
				return new LProfileAxisChain(this, bridge, endpoint, minimum: false);
			}
			return this;
		}

		public static List<LProfileAxisChain> Build(IEnumerable<LProfileEdge> edges, bool horizontal, double tolerance)
		{
			List<List<LProfileEdge>> groups = new List<List<LProfileEdge>>();
			foreach (LProfileEdge edge in edges.Where(edge => edge.Segment != null
				&& (horizontal ? edge.Segment.IsHorizontal(tolerance) : edge.Segment.IsVertical(tolerance))))
			{
				List<List<LProfileEdge>> matches = groups.Where(group =>
					Math.Abs((horizontal ? group[0].Segment.Start.Y : group[0].Segment.Start.X)
						- (horizontal ? edge.Segment.Start.Y : edge.Segment.Start.X)) <= tolerance
					&& group.Any(existing => IntervalsTouch(existing.Segment, edge.Segment, horizontal, tolerance)))
					.ToList();
				if (matches.Count == 0)
				{
					groups.Add(new List<LProfileEdge> { edge });
					continue;
				}
				List<LProfileEdge> merged = matches[0];
				merged.Add(edge);
				foreach (List<LProfileEdge> duplicate in matches.Skip(1).ToList())
				{
					merged.AddRange(duplicate);
					groups.Remove(duplicate);
				}
			}
			return groups.Select(group => new LProfileAxisChain(horizontal, group, tolerance)).ToList();
		}

		private static bool IntervalsTouch(Segment2D first, Segment2D second, bool horizontal, double tolerance)
		{
			double firstMin = horizontal ? first.MinX : first.MinY;
			double firstMax = horizontal ? first.MaxX : first.MaxY;
			double secondMin = horizontal ? second.MinX : second.MinY;
			double secondMax = horizontal ? second.MaxX : second.MaxY;
			return firstMax >= secondMin - tolerance && secondMax >= firstMin - tolerance;
		}
	}

	private sealed class LProfileEdge
	{
		public int Id { get; set; }

		public Segment2D Segment { get; set; }

		public Arc2D Arc { get; set; }

		public int StartNode { get; set; }

		public int EndNode { get; set; }

		public string SourceKey => Segment?.SourceKey ?? Arc?.SourceKey ?? string.Empty;

		public bool IsBoundary { get; set; }

		public bool Touches(Point2D point, double tolerance)
		{
			return (Segment != null && (Segment.Start.DistanceTo(point) <= tolerance || Segment.End.DistanceTo(point) <= tolerance))
				|| (Arc != null && (Arc.Start.DistanceTo(point) <= tolerance || Arc.End.DistanceTo(point) <= tolerance));
		}
	}

	private sealed class LProfileHalfEdge
	{
		public LProfileEdge Edge { get; set; }

		public int FromNode { get; set; }

		public int ToNode { get; set; }

		public bool Forward { get; set; }

		public double Angle { get; set; }

		public LProfileHalfEdge Opposite { get; set; }

		public int Key => Edge.Id * 2 + (Forward ? 0 : 1);

		public Point2D FromPoint => Forward
			? (Edge.Segment != null ? Edge.Segment.Start : Edge.Arc.Start)
			: (Edge.Segment != null ? Edge.Segment.End : Edge.Arc.End);

		public Point2D ToPoint => Forward
			? (Edge.Segment != null ? Edge.Segment.End : Edge.Arc.End)
			: (Edge.Segment != null ? Edge.Segment.Start : Edge.Arc.Start);
	}

	private sealed class LProfileBoundaryGraph
	{
		public List<LProfileEdge> AllEdges { get; private set; }

		public List<LProfileEdge> BoundaryEdges { get; private set; }

		public List<LProfileHalfEdge> BoundaryPath { get; private set; }

		public HashSet<int> ExteriorEdgeIds { get; private set; }

		public OutlineFeature2D BoundaryOutline { get; private set; }

		private readonly List<Point2D> _nodes = new List<Point2D>();

		private readonly Dictionary<Segment2D, LProfileEdge> _segmentEdges = new Dictionary<Segment2D, LProfileEdge>();

		private LProfileBoundaryGraph()
		{
			AllEdges = new List<LProfileEdge>();
			BoundaryEdges = new List<LProfileEdge>();
			BoundaryPath = new List<LProfileHalfEdge>();
			ExteriorEdgeIds = new HashSet<int>();
		}

		public static LProfileBoundaryGraph TryCreate(OutlineFeature2D outline, double tolerance)
		{
			LProfileBoundaryGraph graph = new LProfileBoundaryGraph();
			foreach (Segment2D segment in outline.Segments.Where(segment => segment != null && !segment.IsArcChord && segment.Length > tolerance))
			{
				graph.AddEdge(new LProfileEdge { Segment = segment }, segment.Start, segment.End, tolerance);
			}
			foreach (Arc2D arc in outline.Arcs.Where(arc => IsValidArc(arc, tolerance)))
			{
				graph.AddEdge(new LProfileEdge { Arc = arc }, arc.Start, arc.End, tolerance);
			}
			if (graph.AllEdges.Count == 0)
			{
				return null;
			}
			Dictionary<int, List<LProfileHalfEdge>> adjacency = graph.BuildHalfEdges(tolerance);
			List<List<LProfileHalfEdge>> faces = graph.FindFaces(adjacency, tolerance);
			List<List<LProfileHalfEdge>> exteriorFaces = faces
				.Where(face => graph.SignedArea(face) < -Math.Max(tolerance * tolerance, 1E-12))
				.ToList();
			if (exteriorFaces.Count != 1)
			{
				return null;
			}
			graph.BoundaryPath = exteriorFaces[0];
			foreach (LProfileHalfEdge halfEdge in exteriorFaces[0])
			{
				graph.ExteriorEdgeIds.Add(halfEdge.Edge.Id);
			}
			foreach (LProfileEdge edge in graph.AllEdges)
			{
				edge.IsBoundary = graph.ExteriorEdgeIds.Contains(edge.Id);
				if (edge.IsBoundary)
				{
					graph.BoundaryEdges.Add(edge);
				}
			}
			if (graph.BoundaryEdges.Count < 4)
			{
				return null;
			}
			graph.BoundaryOutline = graph.CreateBoundaryOutline(outline);
			return graph;
		}

		public List<LProfileConcaveTurn> FindConcaveTurns(
			IList<LProfileAxisChain> horizontalChains,
			IList<LProfileAxisChain> verticalChains,
			double tolerance)
		{
			List<LProfileConcaveTurn> turns = new List<LProfileConcaveTurn>();
			if (BoundaryPath.Count < 3)
			{
				return turns;
			}
			for (int i = 0; i < BoundaryPath.Count; i++)
			{
				LProfileHalfEdge first = BoundaryPath[i];
				bool firstHorizontal;
				if (!TryGetAxis(first, tolerance, out firstHorizontal))
				{
					continue;
				}
				int nextIndex = FindNextAxisIndex(i, tolerance);
				if (nextIndex < 0 || nextIndex == i)
				{
					continue;
				}
				LProfileHalfEdge second = BoundaryPath[nextIndex];
				bool secondHorizontal;
				TryGetAxis(second, tolerance, out secondHorizontal);
				if (firstHorizontal == secondHorizontal)
				{
					continue;
				}
				Point2D incoming = new Point2D(first.ToPoint.X - first.FromPoint.X, first.ToPoint.Y - first.FromPoint.Y);
				Point2D outgoing = new Point2D(second.ToPoint.X - second.FromPoint.X, second.ToPoint.Y - second.FromPoint.Y);
				double cross = incoming.X * outgoing.Y - incoming.Y * outgoing.X;
				if (cross <= Math.Max(tolerance * tolerance, 1E-12))
				{
					continue;
				}

				LProfileAxisChain firstChain = FindChain(first.Edge, firstHorizontal ? horizontalChains : verticalChains);
				LProfileAxisChain secondChain = FindChain(second.Edge, secondHorizontal ? horizontalChains : verticalChains);
				if (firstChain == null || secondChain == null)
				{
					continue;
				}
				List<LProfileEdge> transitions = new List<LProfileEdge>();
				for (int offset = 1; offset < BoundaryPath.Count; offset++)
				{
					int index = (i + offset) % BoundaryPath.Count;
					if (index == nextIndex)
					{
						break;
					}
					transitions.Add(BoundaryPath[index].Edge);
				}
				turns.Add(new LProfileConcaveTurn
				{
					HorizontalChain = firstHorizontal ? firstChain : secondChain,
					VerticalChain = firstHorizontal ? secondChain : firstChain,
					HorizontalPoint = firstHorizontal ? first.ToPoint : second.FromPoint,
					VerticalPoint = firstHorizontal ? second.FromPoint : first.ToPoint,
					TransitionEdges = transitions
				});
			}
			return turns;
		}

		private int FindNextAxisIndex(int startIndex, double tolerance)
		{
			for (int offset = 1; offset < BoundaryPath.Count; offset++)
			{
				int index = (startIndex + offset) % BoundaryPath.Count;
				bool horizontal;
				if (TryGetAxis(BoundaryPath[index], tolerance, out horizontal))
				{
					return index;
				}
			}
			return -1;
		}

		private static bool TryGetAxis(LProfileHalfEdge halfEdge, double tolerance, out bool horizontal)
		{
			horizontal = false;
			if (halfEdge?.Edge?.Segment == null || halfEdge.Edge.Segment.IsArcChord)
			{
				return false;
			}
			if (halfEdge.Edge.Segment.IsHorizontal(tolerance))
			{
				horizontal = true;
				return true;
			}
			return halfEdge.Edge.Segment.IsVertical(tolerance);
		}

		private static LProfileAxisChain FindChain(LProfileEdge edge, IEnumerable<LProfileAxisChain> chains)
		{
			return chains.FirstOrDefault(chain => chain.Edges.Contains(edge));
		}

		public bool IsBoundaryEdge(LProfileEdge edge)
		{
			return edge != null && ExteriorEdgeIds.Contains(edge.Id);
		}

		private void AddEdge(LProfileEdge edge, Point2D start, Point2D end, double tolerance)
		{
			edge.Id = AllEdges.Count;
			edge.StartNode = GetNode(start, tolerance);
			edge.EndNode = GetNode(end, tolerance);
			AllEdges.Add(edge);
			if (edge.Segment != null)
			{
				_segmentEdges[edge.Segment] = edge;
			}
		}

		private int GetNode(Point2D point, double tolerance)
		{
			for (int i = 0; i < _nodes.Count; i++)
			{
				if (_nodes[i].DistanceTo(point) <= tolerance)
				{
					return i;
				}
			}
			_nodes.Add(point);
			return _nodes.Count - 1;
		}

		private Dictionary<int, List<LProfileHalfEdge>> BuildHalfEdges(double tolerance)
		{
			Dictionary<int, List<LProfileHalfEdge>> adjacency = new Dictionary<int, List<LProfileHalfEdge>>();
			foreach (LProfileEdge edge in AllEdges)
			{
				LProfileHalfEdge forward = new LProfileHalfEdge
				{
					Edge = edge,
					FromNode = edge.StartNode,
					ToNode = edge.EndNode,
					Forward = true,
					Angle = GetDirectionAngle(edge, forward: true)
				};
				LProfileHalfEdge reverse = new LProfileHalfEdge
				{
					Edge = edge,
					FromNode = edge.EndNode,
					ToNode = edge.StartNode,
					Forward = false,
					Angle = GetDirectionAngle(edge, forward: false),
					Opposite = forward
				};
				forward.Opposite = reverse;
				AddHalfEdge(adjacency, forward);
				AddHalfEdge(adjacency, reverse);
			}
			foreach (List<LProfileHalfEdge> outgoing in adjacency.Values)
			{
				outgoing.Sort((first, second) =>
				{
					int angle = first.Angle.CompareTo(second.Angle);
					return angle != 0 ? angle : first.Key.CompareTo(second.Key);
				});
			}
			return adjacency;
		}

		private static void AddHalfEdge(Dictionary<int, List<LProfileHalfEdge>> adjacency, LProfileHalfEdge halfEdge)
		{
			if (!adjacency.TryGetValue(halfEdge.FromNode, out List<LProfileHalfEdge> outgoing))
			{
				outgoing = new List<LProfileHalfEdge>();
				adjacency[halfEdge.FromNode] = outgoing;
			}
			outgoing.Add(halfEdge);
		}

		private List<List<LProfileHalfEdge>> FindFaces(Dictionary<int, List<LProfileHalfEdge>> adjacency, double tolerance)
		{
			List<List<LProfileHalfEdge>> faces = new List<List<LProfileHalfEdge>>();
			HashSet<int> visited = new HashSet<int>();
			foreach (LProfileHalfEdge start in adjacency.Values.SelectMany(list => list))
			{
				if (visited.Contains(start.Key))
				{
					continue;
				}
				List<LProfileHalfEdge> face = new List<LProfileHalfEdge>();
				LProfileHalfEdge current = start;
				bool closed = false;
				int limit = AllEdges.Count * 2 + 1;
				for (int i = 0; i < limit; i++)
				{
					if (current == null || visited.Contains(current.Key))
					{
						closed = current != null && current.Key == start.Key;
						break;
					}
					visited.Add(current.Key);
					face.Add(current);
					current = GetNextHalfEdge(current, adjacency);
					if (current != null && current.Key == start.Key)
					{
						closed = true;
						break;
					}
				}
				if (closed && face.Count >= 3 && Math.Abs(SignedArea(face)) > Math.Max(tolerance * tolerance, 1E-12))
				{
					faces.Add(face);
				}
			}
			return faces;
		}

		private static LProfileHalfEdge GetNextHalfEdge(LProfileHalfEdge current, Dictionary<int, List<LProfileHalfEdge>> adjacency)
		{
			if (!adjacency.TryGetValue(current.ToNode, out List<LProfileHalfEdge> outgoing) || outgoing.Count == 0)
			{
				return null;
			}
			int reverseIndex = outgoing.IndexOf(current.Opposite);
			if (reverseIndex < 0)
			{
				return null;
			}
			return outgoing[(reverseIndex - 1 + outgoing.Count) % outgoing.Count];
		}

		private double SignedArea(IList<LProfileHalfEdge> face)
		{
			double area = 0.0;
			foreach (LProfileHalfEdge halfEdge in face)
			{
				LProfileEdge edge = halfEdge.Edge;
				if (edge.Segment != null)
				{
					Point2D first = halfEdge.Forward ? edge.Segment.Start : edge.Segment.End;
					Point2D second = halfEdge.Forward ? edge.Segment.End : edge.Segment.Start;
					area += (first.X * second.Y - second.X * first.Y) * 0.5;
					continue;
				}
				double sweep = 4.0 * Math.Atan(edge.Arc.Bulge);
				if (!halfEdge.Forward)
				{
					sweep = -sweep;
				}
				Point2D start = halfEdge.Forward ? edge.Arc.Start : edge.Arc.End;
				double angle = Math.Atan2(start.Y - edge.Arc.Center.Y, start.X - edge.Arc.Center.X);
				double endAngle = angle + sweep;
				area += 0.5 * (edge.Arc.Radius * (edge.Arc.Center.X * (Math.Sin(endAngle) - Math.Sin(angle))
					+ edge.Arc.Center.Y * (Math.Cos(angle) - Math.Cos(endAngle)))
					+ edge.Arc.Radius * edge.Arc.Radius * sweep);
			}
			return area;
		}

		private double GetDirectionAngle(LProfileEdge edge, bool forward)
		{
			if (edge.Segment != null)
			{
				Point2D first = forward ? edge.Segment.Start : edge.Segment.End;
				Point2D second = forward ? edge.Segment.End : edge.Segment.Start;
				return Math.Atan2(second.Y - first.Y, second.X - first.X);
			}
			double sweep = 4.0 * Math.Atan(edge.Arc.Bulge);
			Point2D point = forward ? edge.Arc.Start : edge.Arc.End;
			double radial = Math.Atan2(point.Y - edge.Arc.Center.Y, point.X - edge.Arc.Center.X);
			double direction = Math.Sign(forward ? sweep : -sweep);
			return radial + direction * Math.PI / 2.0;
		}

		private OutlineFeature2D CreateBoundaryOutline(OutlineFeature2D source)
		{
			OutlineFeature2D boundary = new OutlineFeature2D
			{
				MinX = source.MinX,
				MaxX = source.MaxX,
				MinY = source.MinY,
				MaxY = source.MaxY
			};
			foreach (LProfileEdge edge in BoundaryEdges)
			{
				if (edge.Segment != null)
				{
					boundary.Segments.Add(edge.Segment);
				}
				else
				{
					boundary.Arcs.Add(edge.Arc);
				}
			}
			return boundary;
		}

		private static bool IsValidArc(Arc2D arc, double tolerance)
		{
			return arc != null
				&& arc.Radius > tolerance
				&& Math.Abs(arc.Bulge) > 1E-12
				&& arc.Start.DistanceTo(arc.End) > tolerance;
		}
	}
}
