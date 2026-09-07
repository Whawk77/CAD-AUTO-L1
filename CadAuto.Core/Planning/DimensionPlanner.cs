using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning;

// Partial layout (navigational only, compiler-identical):
//   DimensionPlanner.cs                   - DTOs, fields, entry points, emission API, shared leaf utilities
//   DimensionPlanner.Suppression.cs       - 23-pass suppression pipeline + structure overall-partition
//   DimensionPlanner.Structure.cs         - overall/step/four-side structure candidate generation
//   DimensionPlanner.StructureGeometry.cs - extension crossings, inclined endpoints, intersection math
//   DimensionPlanner.Features.cs          - hole rows, pin groups, functional/loose holes, slots
public sealed partial class DimensionPlanner
{
	private sealed class StructurePoint
	{
		public Point2D Point { get; set; }

		public string Source { get; set; }
	}

	private sealed class IgnoredPoint
	{
		public Point2D Point { get; set; }

		public string Reason { get; set; }
	}

	private sealed class CenterlineEndpointMatch
	{
		public CenterlineEndpoint2D Endpoint { get; set; }

		public double? Distance { get; set; }

		public string Status { get; set; }

		public string MatchMode { get; set; }

		public bool IsMatched => Endpoint != null && string.Equals(Status, "Matched", StringComparison.Ordinal);
	}

	// Every MarkSuppressed reason emitted by this planner, in one place.
	private static class SuppressReason
	{
		public const string LocalGeometryOnOverallEnvelope = "LocalGeometryOnOverallEnvelope";

		public const string BottomProtrusionInnerRemainder = "BottomProtrusionInnerRemainder";

		public const string ComplementaryOutlineRemainder = "ComplementaryOutlineRemainder";

		public const string OuterContourStepOverallRemainder = "OuterContourStepOverallRemainder";

		public const string OutlineSegmentOverallPartition = "OutlineSegmentOverallPartition";

		public const string StructureDuplicateOfEnvelopeOutlineSegment = "StructureDuplicateOfEnvelopeOutlineSegment";

		public const string OutlineSegmentOnOverallEnvelope = "OutlineSegmentOnOverallEnvelope";

		public const string OutlineSegmentSameIntervalAsEnvelopeFragment = "OutlineSegmentSameIntervalAsEnvelopeFragment";

		public const string RightStructureHeightDuplicatesOverallHeight = "RightStructureHeightDuplicatesOverallHeight";

		public const string LeftStructureHeightDuplicatesOverallHeight = "LeftStructureHeightDuplicatesOverallHeight";

		/// <summary>Inset structure span ≥80% overall on that axis (e.g. CAD 171.5 vs H=201.5).</summary>
		public const string NearOverallInsetStructureBody = "NearOverallInsetStructureBody";


		public const string LeftStructureHeightCoveredByRight = "LeftStructureHeightCoveredByRight";

		public const string OrphanOuterVerticalStructureHeightTip = "OrphanOuterVerticalStructureHeightTip";

		public const string SecondaryOutlineSegmentDuplicatesPrimaryStructureHeight = "SecondaryOutlineSegmentDuplicatesPrimaryStructureHeight";

		public const string SecondaryOutlineSegmentOverallResidual = "SecondaryOutlineSegmentOverallResidual";

		public const string SecondaryOutlineSegmentOverallPartitionWithPrimaryStructure = "SecondaryOutlineSegmentOverallPartitionWithPrimaryStructure";

		public const string MirroredDuplicate = "MirroredDuplicate";

		public const string DuplicateMeasuredDimension = "DuplicateMeasuredDimension";

		public const string LeftStructureHeightCoveredByDatumRootedOuterStep = "LeftStructureHeightCoveredByDatumRootedOuterStep";

		public const string StructureOverallPartition = "StructureOverallPartition";

		public const string ProjectedStructureOverallPartition = "ProjectedStructureOverallPartition";

		public const string NonProfileBackedSideStructureHeight = "NonProfileBackedSideStructureHeight";

		public const string TopClosedChainRedundantPositioning = "TopClosedChainRedundantPositioning";

		/// <summary>
		/// Multi-piece structure chain (n≥3) covers overall: drop the unique longest body
		/// remainder (e.g. 177.45 when 73+87.55+177.45=338), keep real steps.
		/// </summary>
		public const string MultiPieceStructureOverallRemainder = "MultiPieceStructureOverallRemainder";

		/// <summary>
		/// Same-side structure: a major piece (≥45% overall) coexists with a smaller piece that
		/// does not share an endpoint — drop the disconnected secondary (e.g. top 87 next to 120).
		/// </summary>
		public const string DisconnectedSecondaryStructure = "DisconnectedSecondaryStructure";

		/// <summary>
		/// Single side-height residual (25–40% overall) when the opposite side already carries a
		/// major structure (≥50% overall) — e.g. right 72 with left 120 on rotated 215 part.
		/// </summary>
		public const string OppositeMajorResidualSideHeight = "OppositeMajorResidualSideHeight";
	}

	private sealed class FunctionalHoleGroupPlan
	{
		public PinGroupPlan PinGroup { get; set; }

		public List<HoleFeature2D> Holes { get; private set; }

		public FunctionalHoleGroupPlan()
		{
			Holes = new List<HoleFeature2D>();
		}
	}

	private sealed class LooseHoleLineGroup
	{
		public bool Horizontal { get; set; }

		public string SpecKey { get; set; }

		public List<HoleFeature2D> Holes { get; private set; }

		public LooseHoleLineGroup()
		{
			Holes = new List<HoleFeature2D>();
		}
	}

	private sealed class LooseHoleMacroGroup
	{
		public List<LooseHoleLineGroup> LineGroups { get; private set; }

		public List<HoleFeature2D> Holes { get; private set; }

		public LooseHoleMacroGroup()
		{
			LineGroups = new List<LooseHoleLineGroup>();
			Holes = new List<HoleFeature2D>();
		}
	}

	private sealed class LooseHoleLocationPlan
	{
		public LooseHoleMacroGroup MacroGroup { get; set; }

		public PinGroupPlan ReferencePinGroup { get; set; }

		public HoleFeature2D AnchorHole { get; set; }
	}

	private readonly DimensionRuleConfig _config;

	private readonly StructureSuppressionRules _structureSuppressionRules;

	private readonly StructureEndpointRules _structureEndpointRules;

	private readonly DimensionDeduplicationRules _dimensionDeduplicationRules;

	private readonly DimensionPlanPostValidator _postValidator;

	private int _nextLooseChainId = 1;

	public DimensionPlanner(DimensionRuleConfig config)
	{
		_config = config;
		_structureSuppressionRules = new StructureSuppressionRules(config);
		_structureEndpointRules = new StructureEndpointRules(config);
		_dimensionDeduplicationRules = new DimensionDeduplicationRules(config);
		_postValidator = new DimensionPlanPostValidator(config);
	}

	public DimensionPlan CreateOutlinePlan(OutlineFeature2D outline)
	{
		if (outline == null)
		{
			throw new ArgumentNullException("outline");
		}
		CoordinateFrame2D frame = CoordinateFrame2D.InferFromOutline(outline, _config.GeometryTolerance);
		OutlineFeature2D localOutline = frame.IsIdentity
			? outline
			: CoordinateFrameModelTransform.ToLocal(outline, frame, _config.GeometryTolerance);
		DimensionPlan dimensionPlan = CreateOutlinePlanLocal(localOutline);
		dimensionPlan.CoordinateFrame = frame;
		return dimensionPlan;
	}

	public DimensionPlan CreateOutlinePlan(OutlineFeature2D outline, CoordinateFrame2D coordinateFrame)
	{
		if (outline == null)
		{
			throw new ArgumentNullException("outline");
		}
		CoordinateFrame2D frame = coordinateFrame ?? CoordinateFrame2D.InferFromOutline(outline, _config.GeometryTolerance);
		OutlineFeature2D localOutline = frame.IsIdentity
			? outline
			: CoordinateFrameModelTransform.ToLocal(outline, frame, _config.GeometryTolerance);
		DimensionPlan dimensionPlan = CreateOutlinePlanLocal(localOutline);
		dimensionPlan.CoordinateFrame = frame;
		return dimensionPlan;
	}

	private DimensionPlan CreateOutlinePlanLocal(OutlineFeature2D outline)
	{
		_nextLooseChainId = 1;
		DimensionPlan dimensionPlan = new DimensionPlan();
		LProfileOuterContour lProfileOuterContour = null;
		string lProfileOuterContourFailure = string.Empty;
		lProfileOuterContour = LProfileOuterContour.TryCreate(
			outline,
			_config.GeometryTolerance,
			out lProfileOuterContourFailure);
		AddOverallWidth(dimensionPlan, outline);
		AddOverallHeight(dimensionPlan, outline);
		if (_config.UseFeatureFirstStructurePipeline)
		{
			AddFeatureFirstStructureDimensions(dimensionPlan, outline);
		}
		else
		{
			AddStepOutlineDimensions(dimensionPlan, outline);
		}
		AddLinearSegmentDimensions(dimensionPlan, outline);
		if (_config.UseFeatureFirstStructurePipeline)
		{
			ApplyLProfileAnnotationRule(
				dimensionPlan,
				outline,
				lProfileOuterContour,
				lProfileOuterContourFailure);
		}
		ApplyLProfileOverallDimensionRules(dimensionPlan, outline, lProfileOuterContour);
		ApplyLProfileOverallSplitByFilletRule(dimensionPlan, outline, lProfileOuterContour);
		SuppressDuplicateDimensions(dimensionPlan, outline);
		_postValidator.Validate(dimensionPlan, outline);
		dimensionPlan.CaptureFinalDimensions();
		return dimensionPlan;
	}

	/// <summary>
	/// Phase B structure path: extract → select → place (structure only; Overall already added).
	/// </summary>
	private void AddFeatureFirstStructureDimensions(DimensionPlan plan, OutlineFeature2D outline)
	{
		var extractor = new StructureFeatureExtractor(_config);
		var selector = new StructureMeasurementSelector(_config);
		var adapter = new StructurePlacementAdapter(_config);
		IList<StructureFeature> features = extractor.Extract(outline);
		selector.Select(features, outline);
		AnnotationCaseRuntime.LastFeatures = features;
		AnnotationCaseRuntime.LastOutline = outline;
		if (!string.IsNullOrEmpty(_config.AnnotationCaseStorePath))
		{
			AnnotationCaseStore store = AnnotationCaseStore.Load(_config.AnnotationCaseStorePath);
			new AnnotationCaseOverlay(store).Apply(features, outline);
		}
		IList<StructureFeature> kept = features
			.Where(f => f != null && f.Keep && f.Kind != StructureFeatureKind.Overall)
			.ToList();
		foreach (PlannedDimension dim in adapter.Place(kept, outline))
		{
			if (dim == null || dim.Kind == DimensionKind.OverallWidth || dim.Kind == DimensionKind.OverallHeight)
			{
				continue;
			}
			dim.TopologyEvidence = string.IsNullOrEmpty(dim.TopologyEvidence)
				? "FeatureFirstStructure"
				: dim.TopologyEvidence + ";FeatureFirstStructure";
			plan.Add(dim);
		}
	}

	public DimensionPlan CreateDimensionPlan(OutlineFeature2D outline, Datum2D datum, IEnumerable<HoleFeature2D> holes)
	{
		return CreateDimensionPlan(outline, datum, holes, new SlotFeature2D[0]);
	}

	public DimensionPlan CreateDimensionPlan(OutlineFeature2D outline, Datum2D datum, IEnumerable<HoleFeature2D> holes, IEnumerable<SlotFeature2D> slots)
	{
		return CreateDimensionPlan(outline, datum, holes, slots, null);
	}

	public DimensionPlan CreateDimensionPlan(OutlineFeature2D outline, Datum2D datum, IEnumerable<HoleFeature2D> holes, IEnumerable<SlotFeature2D> slots, CoordinateFrame2D coordinateFrame)
	{
		if (outline == null)
		{
			throw new ArgumentNullException("outline");
		}
		CoordinateFrame2D frame = coordinateFrame ?? CoordinateFrame2D.InferFromOutline(outline, _config.GeometryTolerance);
		OutlineFeature2D localOutline = frame.IsIdentity
			? outline
			: CoordinateFrameModelTransform.ToLocal(outline, frame, _config.GeometryTolerance);
		Datum2D localDatum = datum == null
			? null
			: (frame.IsIdentity ? datum : CoordinateFrameModelTransform.ToLocal(datum, frame));
		List<HoleFeature2D> localHoles = frame.IsIdentity
			? (holes ?? new HoleFeature2D[0]).Where((HoleFeature2D h) => h != null).ToList()
			: CoordinateFrameModelTransform.ToLocal(holes, frame);
		List<SlotFeature2D> localSlots = frame.IsIdentity
			? (slots ?? new SlotFeature2D[0]).Where((SlotFeature2D s) => s != null).ToList()
			: CoordinateFrameModelTransform.ToLocal(slots, frame);
		DimensionPlan dimensionPlan = CreateOutlinePlanLocal(localOutline);
		Datum2D datum2 = localDatum ?? Datum2D.FromOutline(localOutline);
		AddHolePositionDimensions(dimensionPlan, localOutline, datum2, localHoles);
		AddSlotDimensions(dimensionPlan, localOutline, datum2, localHoles, localSlots);
		ApplyCenterlineEndpointAttachments(dimensionPlan, datum2);
		SuppressDuplicateDimensions(dimensionPlan, localOutline);
		_postValidator.Validate(dimensionPlan, localOutline);
		dimensionPlan.CaptureFinalDimensions();
		dimensionPlan.CoordinateFrame = frame;
		return dimensionPlan;
	}

	private void ApplyCenterlineEndpointAttachments(DimensionPlan plan, Datum2D datum)
	{
		if (plan == null || datum == null || datum.HoleCenterlineEndpoints == null || datum.HoleCenterlineEndpoints.Count == 0)
		{
			return;
		}
		foreach (PlannedDimension dimension in plan.Dimensions.ToList())
		{
			if (!AllowsCenterlineEndpoint(dimension))
			{
				continue;
			}
			List<string> evidence = new List<string>();
			List<string> sourceGeometryIds = new List<string>();
			bool changed = false;
			bool linkedSlotCenterDistance = IsLinkedSlotCenterDistance(plan, dimension);
			bool linkedSlotDimension = linkedSlotCenterDistance
				|| string.Equals(dimension.RuleId, "LinkedSlotDatumSide", StringComparison.Ordinal);
			if (!dimension.FirstPointMustLieOnOutline)
			{
				Point2D beforePoint = dimension.FirstPoint;
				CenterlineEndpointMatch firstMatch = linkedSlotDimension
					? FindSingleArcSlotCenterlineEndpoint(datum, beforePoint, dimension.Side)
					: FindCenterlineEndpoint(datum, beforePoint);
				if (firstMatch.IsMatched && HasSpan(dimension, firstMatch.Endpoint.Point, dimension.SecondPoint))
				{
					dimension.FirstPoint = firstMatch.Endpoint.Point;
					changed = true;
					if (!string.IsNullOrEmpty(firstMatch.Endpoint.SourceGeometryId))
					{
						sourceGeometryIds.Add(firstMatch.Endpoint.SourceGeometryId);
					}
					evidence.Add(BuildAppliedCenterlineEvidence("First", firstMatch, beforePoint, dimension.FirstPoint));
				}
				else
				{
					evidence.Add(BuildCenterlineCheckEvidence("First", firstMatch, firstMatch.IsMatched ? "InvalidSpan" : null));
				}
			}
			Point2D beforeSecondPoint = dimension.SecondPoint;
			bool arcSlotDatum = string.Equals(dimension.DebugRole, "SingleArcSlotDatum", StringComparison.Ordinal)
				|| string.Equals(dimension.DebugRole, "SingleArcSlotVerticalDatum", StringComparison.Ordinal)
				|| string.Equals(dimension.DebugRole, "DoubleArcSlotHorizontalDatum", StringComparison.Ordinal)
				|| string.Equals(dimension.DebugRole, "DoubleArcSlotVerticalDatum", StringComparison.Ordinal)
				|| linkedSlotDimension;
			CenterlineEndpointMatch secondMatch = arcSlotDatum
				? FindSingleArcSlotCenterlineEndpoint(datum, beforeSecondPoint, dimension.Side)
				: FindCenterlineEndpoint(datum, beforeSecondPoint);
			if (secondMatch.IsMatched && HasSpan(dimension, dimension.FirstPoint, secondMatch.Endpoint.Point))
			{
				dimension.SecondPoint = secondMatch.Endpoint.Point;
				changed = true;
				if (!string.IsNullOrEmpty(secondMatch.Endpoint.SourceGeometryId))
				{
					sourceGeometryIds.Add(secondMatch.Endpoint.SourceGeometryId);
				}
				evidence.Add(BuildAppliedCenterlineEvidence("Second", secondMatch, beforeSecondPoint, dimension.SecondPoint));
			}
			else
			{
				evidence.Add(BuildCenterlineCheckEvidence("Second", secondMatch, secondMatch.IsMatched ? "InvalidSpan" : null));
			}
			if (changed)
			{
				dimension.AttachmentKind = "SelectedCenterlineEndpoint";
				plan.SetAttachmentValidity(dimension, true);
			}
			plan.RecordRuleEvidence(
				dimension,
				null,
				sourceGeometryIds,
				AppendCenterlineEndpointEvidence(dimension.TopologyEvidence, evidence));
		}
	}

	private static bool IsLinkedSlotCenterDistance(DimensionPlan plan, PlannedDimension dimension)
	{
		if (dimension == null
			|| (dimension.DebugRole != "SlotChainH" && dimension.DebugRole != "SlotChainV")
			|| string.IsNullOrEmpty(dimension.AlignmentKey))
		{
			return false;
		}
		return plan.Dimensions.Any(candidate => candidate != null
			&& candidate.AlignmentKey == dimension.AlignmentKey
			&& (candidate.DebugRole == "SingleArcSlotDatum"
				|| candidate.DebugRole == "SingleArcSlotVerticalDatum"
				|| candidate.DebugRole == "DoubleArcSlotHorizontalDatum"
				|| candidate.DebugRole == "DoubleArcSlotVerticalDatum"));
	}

	private bool AllowsCenterlineEndpoint(PlannedDimension dimension)
	{
		if (dimension == null)
		{
			return false;
		}
		if (string.Equals(dimension.DebugRole, "SlotCenter", StringComparison.Ordinal))
		{
			return string.Equals(dimension.RuleId, "LinkedSlotDatumSide", StringComparison.Ordinal);
		}
		return dimension.Role == DimensionCandidateRole.Hole
			|| dimension.Role == DimensionCandidateRole.Slot;
	}

	private CenterlineEndpointMatch FindCenterlineEndpoint(Datum2D datum, Point2D target)
	{
		double tolerance = Math.Max(Math.Abs(_config.GeometryTolerance), 1E-9);
		List<CenterlineEndpointMatch> matches = datum.HoleCenterlineEndpoints
			.Where(candidate => candidate != null)
			.Select(candidate => new CenterlineEndpointMatch
			{
				Endpoint = candidate,
				Distance = candidate.Point.DistanceTo(target),
				Status = "Matched"
			})
			.Where(match => match.Distance.HasValue && match.Distance.Value <= tolerance)
			.OrderBy(match => match.Distance.Value)
			.ToList();
		if (matches.Count == 0)
		{
			double? nearestDistance = datum.HoleCenterlineEndpoints
			.Where(candidate => candidate != null)
			.Select(candidate => (double?)candidate.Point.DistanceTo(target))
			.OrderBy(distance => distance)
			.FirstOrDefault();
			return new CenterlineEndpointMatch
			{
				Distance = nearestDistance,
				Status = nearestDistance.HasValue ? "NoMatch" : "NoEndpoint"
			};
		}
		if (matches.Count > 1
			&& Math.Abs(matches[1].Distance.Value - matches[0].Distance.Value) <= _config.GeometryTolerance)
		{
			return new CenterlineEndpointMatch
			{
				Distance = matches[0].Distance,
				Status = "Ambiguous"
			};
		}
		return matches[0];
	}

	private CenterlineEndpointMatch FindSingleArcSlotCenterlineEndpoint(Datum2D datum, Point2D target, DimensionSide side)
	{
		bool verticalAxis = side == DimensionSide.Bottom || side == DimensionSide.Top;
		bool horizontalAxis = side == DimensionSide.Left || side == DimensionSide.Right;
		string matchMode = verticalAxis ? "VerticalAxisEndpointByPlacementSide" : "HorizontalAxisEndpointByPlacementSide";
		if (!verticalAxis && !horizontalAxis)
		{
			return new CenterlineEndpointMatch { Status = "UnsupportedSide", MatchMode = matchMode };
		}
		double tolerance = Math.Max(Math.Abs(_config.GeometryTolerance), 1E-9);
		List<CenterlineEndpointMatch> matches = datum.HoleCenterlineEndpoints
			.Where(endpoint => endpoint != null && !string.IsNullOrEmpty(endpoint.SourceGeometryId))
			.GroupBy(endpoint => endpoint.SourceGeometryId)
			.Where(group => group.Count() >= 2
				&& (verticalAxis
					? group.Max(endpoint => endpoint.Point.X) - group.Min(endpoint => endpoint.Point.X) <= tolerance
						&& Math.Abs(group.First().Point.X - target.X) <= tolerance
					: group.Max(endpoint => endpoint.Point.Y) - group.Min(endpoint => endpoint.Point.Y) <= tolerance
						&& Math.Abs(group.First().Point.Y - target.Y) <= tolerance))
			.Select(group => new CenterlineEndpointMatch
			{
				Endpoint = side == DimensionSide.Bottom ? group.OrderBy(endpoint => endpoint.Point.Y).First()
					: side == DimensionSide.Top ? group.OrderByDescending(endpoint => endpoint.Point.Y).First()
					: side == DimensionSide.Left ? group.OrderBy(endpoint => endpoint.Point.X).First()
					: group.OrderByDescending(endpoint => endpoint.Point.X).First(),
				Status = "Matched",
				MatchMode = matchMode
			})
			.Select(match =>
			{
				match.Distance = match.Endpoint.Point.DistanceTo(target);
				return match;
			})
			.OrderBy(match => match.Distance.Value)
			.ToList();
		if (matches.Count == 0)
		{
			return new CenterlineEndpointMatch { Status = verticalAxis ? "NoMatchingVerticalAxis" : "NoMatchingHorizontalAxis", MatchMode = matchMode };
		}
		if (matches.Count > 1 && Math.Abs(matches[1].Distance.Value - matches[0].Distance.Value) <= tolerance)
		{
			return new CenterlineEndpointMatch
			{
				Distance = matches[0].Distance,
				Status = "Ambiguous",
				MatchMode = matchMode
			};
		}
		return matches[0];
	}

	private bool HasSpan(PlannedDimension dimension, Point2D firstPoint, Point2D secondPoint)
	{
		double span = dimension.Orientation == DimensionOrientation.Horizontal
			? Math.Abs(secondPoint.X - firstPoint.X)
			: Math.Abs(secondPoint.Y - firstPoint.Y);
		return span > _config.GeometryTolerance;
	}

	private static string BuildAppliedCenterlineEvidence(string pointName, CenterlineEndpointMatch match, Point2D beforePoint, Point2D afterPoint)
	{
		return string.Format(
			CultureInfo.InvariantCulture,
			"CenterlineEndpoint|Status=Applied|Mode={0}|Point={1}|Source={2}|Distance={3:0.########}|Before={4:0.########},{5:0.########}|After={6:0.########},{7:0.########}",
			match.MatchMode ?? "CoincidentEndpoint",
			pointName,
			match.Endpoint.SourceGeometryId ?? string.Empty,
			match.Distance ?? 0.0,
			beforePoint.X,
			beforePoint.Y,
			afterPoint.X,
			afterPoint.Y);
	}

	private static string BuildCenterlineCheckEvidence(string pointName, CenterlineEndpointMatch match, string overrideStatus)
	{
		return string.Format(
			CultureInfo.InvariantCulture,
			"CenterlineEndpointCheck|Point={0}|Status={1}|Distance={2}",
			pointName,
			overrideStatus ?? match.Status,
			match.Distance.HasValue ? match.Distance.Value.ToString("0.########", CultureInfo.InvariantCulture) : "none");
	}

	private static string AppendCenterlineEndpointEvidence(string evidence, IEnumerable<string> additions)
	{
		List<string> entries = new List<string>();
		if (!string.IsNullOrEmpty(evidence))
		{
			entries.Add(evidence);
		}
		foreach (string addition in additions ?? Enumerable.Empty<string>())
		{
			if (!string.IsNullOrEmpty(addition) && !entries.Contains(addition))
			{
				entries.Add(addition);
			}
		}
		return string.Join(";", entries);
	}

	private bool ContainsStructurePoint(IEnumerable<StructurePoint> points, Point2D point)
	{
		return points.Any((StructurePoint p) => PointsEqual(p.Point, point));
	}

	private bool ContainsIgnoredPoint(IEnumerable<IgnoredPoint> points, Point2D point)
	{
		return points.Any((IgnoredPoint p) => PointsEqual(p.Point, point));
	}

	private bool IsTooSmallStructureSpan(double span)
	{
		return _structureEndpointRules.IsTooSmallStructureSpan(span);
	}

	private bool IsCrossAxisStructureSpanTooLarge(Point2D first, Point2D second, bool horizontal)
	{
		return _structureEndpointRules.IsCrossAxisStructureSpanTooLarge(first, second, horizontal);
	}

	private Point2D GetLeftBoundaryPoint(OutlineFeature2D outline)
	{
		return _structureEndpointRules.GetBoundaryPoint(outline, DimensionSide.Left);
	}

	private Point2D GetRightBoundaryPoint(OutlineFeature2D outline)
	{
		return _structureEndpointRules.GetBoundaryPoint(outline, DimensionSide.Right);
	}

	private Point2D GetBottomBoundaryPoint(OutlineFeature2D outline)
	{
		return _structureEndpointRules.GetBoundaryPoint(outline, DimensionSide.Bottom);
	}

	private Point2D GetTopBoundaryPoint(OutlineFeature2D outline)
	{
		return _structureEndpointRules.GetBoundaryPoint(outline, DimensionSide.Top);
	}

	private bool AddHorizontalOutlineReferenceDimension(DimensionPlan plan, OutlineFeature2D outline, double preferredX, Point2D target, DimensionKind kind, DimensionSide side, string overrideText, string debugRole, string debugOwner = null, bool preferFeatureLocalPlacement = false, string alignmentKey = null, int alignmentPriority = 0, DimensionReadingLevel readingLevel = DimensionReadingLevel.LocalSpacing, string skippedDebugRole = null)
	{
		if (!OutlineGeometryQuery.TryFindVerticalBoundaryPoint(outline, preferredX, target.Y, _config.GeometryTolerance, out var point))
		{
			plan.AddSkippedDimension(kind, DimensionOrientation.Horizontal, side, new Point2D(preferredX, target.Y), target, "NoRealOutlineAttachment:XDatum=" + preferredX.ToString("0.########", CultureInfo.InvariantCulture), skippedDebugRole ?? debugRole, debugOwner);
			return false;
		}
		if (string.Equals(debugRole, "DoubleArcSlotHorizontalDatum", StringComparison.Ordinal))
		{
			point = GetSideFacingOutlineSegmentEndpoint(outline, point, DimensionOrientation.Horizontal, side);
		}
		AddDimension(plan, kind, DimensionOrientation.Horizontal, side, point, target, overrideText, debugRole, debugOwner, preferFeatureLocalPlacement, firstPointMustLieOnOutline: true, requiredOutlineReferenceCoordinate: preferredX, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority, readingLevel: readingLevel);
		return true;
	}

	private bool AddVerticalOutlineReferenceDimension(DimensionPlan plan, OutlineFeature2D outline, double preferredY, Point2D target, DimensionKind kind, DimensionSide side, string overrideText, string debugRole, string debugOwner = null, bool preferFeatureLocalPlacement = false, string alignmentKey = null, int alignmentPriority = 0, DimensionReadingLevel readingLevel = DimensionReadingLevel.LocalSpacing, bool preservePreferredSide = false, string skippedDebugRole = null)
	{
		if (!OutlineGeometryQuery.TryFindHorizontalBoundaryPoint(outline, preferredY, target.X, _config.GeometryTolerance, out var point))
		{
			plan.AddSkippedDimension(kind, DimensionOrientation.Vertical, side, new Point2D(target.X, preferredY), target, "NoRealOutlineAttachment:YDatum=" + preferredY.ToString("0.########", CultureInfo.InvariantCulture), skippedDebugRole ?? debugRole, debugOwner);
			return false;
		}
		if (string.Equals(debugRole, "SingleArcSlotVerticalDatum", StringComparison.Ordinal)
			|| string.Equals(debugRole, "DoubleArcSlotVerticalDatum", StringComparison.Ordinal))
		{
			point = GetSideFacingOutlineSegmentEndpoint(outline, point, DimensionOrientation.Vertical, side);
		}
		AddDimension(plan, kind, DimensionOrientation.Vertical, side, point, target, overrideText, debugRole, debugOwner, preferFeatureLocalPlacement, firstPointMustLieOnOutline: true, requiredOutlineReferenceCoordinate: preferredY, preservePreferredSide: preservePreferredSide, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority, readingLevel: readingLevel);
		return true;
	}

	private Point2D GetSideFacingOutlineSegmentEndpoint(OutlineFeature2D outline, Point2D attachment, DimensionOrientation orientation, DimensionSide side)
	{
		double tolerance = Math.Max(Math.Abs(_config.GeometryTolerance), 1E-9);
		List<Segment2D> segments = outline.Segments
			.Where(segment => segment != null && !segment.IsArcChord
				&& (orientation == DimensionOrientation.Horizontal ? segment.IsVertical(tolerance) : segment.IsHorizontal(tolerance))
				&& (orientation == DimensionOrientation.Horizontal
					? Math.Abs(segment.Start.X - attachment.X) <= tolerance
					: Math.Abs(segment.Start.Y - attachment.Y) <= tolerance))
			.ToList();
		if (segments.Count == 0)
		{
			return attachment;
		}
		if (orientation == DimensionOrientation.Horizontal)
		{
			double y = side == DimensionSide.Top ? segments.Max(segment => segment.MaxY) : segments.Min(segment => segment.MinY);
			return new Point2D(attachment.X, y);
		}
		double x = side == DimensionSide.Right ? segments.Max(segment => segment.MaxX) : segments.Min(segment => segment.MinX);
		return new Point2D(x, attachment.Y);
	}

	private void AddHorizontalChainFromOutlineDatum(DimensionPlan plan, OutlineFeature2D outline, double datumX, IList<Point2D> ordered, string debugRole, string firstDebugRole = null, DimensionSide side = DimensionSide.Bottom)
	{
		if (ordered == null || ordered.Count == 0)
		{
			return;
		}
		// Edge→first hole + inter-hole spans form one continuous locating chain (e.g. U-slot
		// edge location + center distance) and must share a dim-line alignment lane.
		string alignmentKey = GetSlotChainAlignmentKey(side, horizontal: true, ordered[0].Y);
		const int alignmentPriority = 80;
		AddHorizontalOutlineReferenceDimension(plan, outline, datumX, ordered[0], DimensionKind.Normal, side, string.Empty, firstDebugRole ?? debugRole, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority, skippedDebugRole: debugRole);
		for (int i = 1; i < ordered.Count; i++)
		{
			AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Horizontal, side, ordered[i - 1], ordered[i], string.Empty, debugRole, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority);
		}
	}

	private void AddVerticalChainFromOutlineDatum(DimensionPlan plan, OutlineFeature2D outline, double datumY, IList<Point2D> ordered, string debugRole, string firstDebugRole = null, DimensionSide side = DimensionSide.Left)
	{
		if (ordered == null || ordered.Count == 0)
		{
			return;
		}
		// Edge→first U-slot location + slot-to-slot center distance stay on one left-side lane.
		string alignmentKey = GetSlotChainAlignmentKey(side, horizontal: false, ordered[0].X);
		const int alignmentPriority = 80;
		AddVerticalOutlineReferenceDimension(plan, outline, datumY, ordered[0], DimensionKind.Normal, side, string.Empty, firstDebugRole ?? debugRole, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority, skippedDebugRole: debugRole);
		for (int i = 1; i < ordered.Count; i++)
		{
			AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Vertical, side, ordered[i - 1], ordered[i], string.Empty, debugRole, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority);
		}
	}

	private void AddDimension(DimensionPlan plan, DimensionKind kind, DimensionOrientation orientation, DimensionSide side, Point2D firstPoint, Point2D secondPoint, string overrideText, string debugRole, string debugOwner = null, bool preferFeatureLocalPlacement = false, bool firstPointMustLieOnOutline = false, double? requiredOutlineReferenceCoordinate = null, bool preservePreferredSide = false, string alignmentKey = null, int alignmentPriority = 0, bool preferLocalBoundary = false, int chainId = 0, DimensionReadingLevel readingLevel = DimensionReadingLevel.LocalSpacing, bool preserveAlignmentLevel = false)
	{
		if (!(GetSpan(firstPoint, secondPoint, orientation) <= _config.GeometryTolerance))
		{
			plan.Add(new PlannedDimension
			{
				Kind = kind,
				Orientation = orientation,
				Side = side,
				FirstPoint = firstPoint,
				SecondPoint = secondPoint,
				OverrideText = (overrideText ?? string.Empty),
				DebugRole = debugRole,
				DebugOwner = debugOwner,
				AlignmentKey = (alignmentKey ?? string.Empty),
				AlignmentPriority = alignmentPriority,
				PreserveAlignmentLevel = preserveAlignmentLevel,
				ReadingLevel = readingLevel,
				ChainId = chainId,
				PreferLocalBoundary = preferLocalBoundary,
				PreferFeatureLocalPlacement = preferFeatureLocalPlacement,
				PreservePreferredSide = preservePreferredSide,
				FirstPointMustLieOnOutline = firstPointMustLieOnOutline,
				RequiredOutlineReferenceCoordinate = requiredOutlineReferenceCoordinate,
				UseSegmentedExtensionLines = (kind != DimensionKind.OverallWidth && kind != DimensionKind.OverallHeight)
			});
		}
	}

	private static void AddSuppressedDimension(DimensionPlan plan, PlannedDimension dimension, string reason)
	{
		if (plan != null && dimension != null)
		{
			plan.Add(dimension);
			plan.MarkSuppressed(dimension, reason);
			plan.Dimensions.Remove(dimension);
		}
	}

	private void AddHorizontalDim(DimensionPlan plan, Point2D from, Point2D to, string debugRole, string alignmentKey = null, int alignmentPriority = 0)
	{
		AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Horizontal, DimensionSide.Bottom, from, to, string.Empty, debugRole, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority);
	}

	private void AddVerticalDim(DimensionPlan plan, Point2D from, Point2D to, string debugRole, string alignmentKey = null, int alignmentPriority = 0)
	{
		AddDimension(plan, DimensionKind.Normal, DimensionOrientation.Vertical, DimensionSide.Left, from, to, string.Empty, debugRole, alignmentKey: alignmentKey, alignmentPriority: alignmentPriority);
	}

	private double GetSpan(Point2D firstPoint, Point2D secondPoint, DimensionOrientation orientation)
	{
		return (orientation == DimensionOrientation.Horizontal) ? Math.Abs(secondPoint.X - firstPoint.X) : Math.Abs(secondPoint.Y - firstPoint.Y);
	}

	private double GetAverageY(IList<HoleFeature2D> holes)
	{
		return (holes.Count == 0) ? 0.0 : holes.Average((HoleFeature2D h) => h.Center.Y);
	}

	private bool IsSamePinDiameter(HoleFeature2D a, HoleFeature2D b)
	{
		return a != null && b != null && Math.Abs(a.Diameter - b.Diameter) <= _config.GeometryTolerance;
	}

	private bool IsSameHole(HoleFeature2D a, HoleFeature2D b)
	{
		return a != null && b != null && PointsEqual(a.Center, b.Center);
	}

	private void RemoveGroupPins(IList<HoleFeature2D> remaining, PinGroupPlan group)
	{
		foreach (HoleFeature2D item in group.Pins.ToList())
		{
			for (int num = remaining.Count - 1; num >= 0; num--)
			{
				if (IsSameHole(remaining[num], item))
				{
					remaining.RemoveAt(num);
				}
			}
		}
	}

	private double DistanceToPinPair(HoleFeature2D hole, PinGroupPlan group)
	{
		if (hole == null || group == null || group.Pins.Count == 0)
		{
			return double.MaxValue;
		}
		if (group.Pins.Count == 1)
		{
			return Math.Sqrt(DistanceSquared(hole.Center, group.Pins[0].Center));
		}
		return group.Pins.Take(2).Sum((HoleFeature2D pin) => Math.Sqrt(DistanceSquared(hole.Center, pin.Center)));
	}

	/// <summary>
	/// True when the hole projects strictly inside the pin-pair segment and stays near the pin axis.
	/// Used to claim single (or non-grid) holes that sit between the two pin centers as functional holes.
	/// </summary>
	private bool IsHoleBetweenPinPair(HoleFeature2D hole, PinGroupPlan group)
	{
		if (hole == null || group == null || group.Pins.Count < 2)
		{
			return false;
		}
		Point2D pinA = group.Pins[0].Center;
		Point2D pinB = group.Pins[1].Center;
		Point2D center = hole.Center;
		double dx = pinB.X - pinA.X;
		double dy = pinB.Y - pinA.Y;
		double lengthSquared = dx * dx + dy * dy;
		if (lengthSquared <= _config.GeometryTolerance * _config.GeometryTolerance)
		{
			return false;
		}
		double t = ((center.X - pinA.X) * dx + (center.Y - pinA.Y) * dy) / lengthSquared;
		// Strictly between the two pins (exclude endpoints / outside the span).
		double endMargin = 0.05;
		if (t <= endMargin || t >= 1.0 - endMargin)
		{
			return false;
		}
		double projX = pinA.X + t * dx;
		double projY = pinA.Y + t * dy;
		double perpendicular = Math.Sqrt((center.X - projX) * (center.X - projX) + (center.Y - projY) * (center.Y - projY));
		double axisTolerance = Math.Max(GetFunctionalHoleAlignmentTolerance(), Math.Max(group.Pins[0].Diameter, group.Pins[1].Diameter) * 0.5);
		return perpendicular <= axisTolerance;
	}

	private double DistanceSquared(Point2D a, Point2D b)
	{
		double num = a.X - b.X;
		double num2 = a.Y - b.Y;
		return num * num + num2 * num2;
	}

	private DimensionSide ChooseHorizontalHoleSide(OutlineFeature2D outline, Datum2D datum, Point2D point)
	{
		double num = Math.Abs(point.Y - outline.MinY);
		double num2 = Math.Abs(outline.MaxY - point.Y);
		if (Math.Abs(num - num2) <= _config.GeometryTolerance)
		{
			return ChooseOppositeHorizontalDatumSide(outline, datum);
		}
		return (!(num < num2)) ? DimensionSide.Top : DimensionSide.Bottom;
	}

	private DimensionSide ChooseVerticalHoleSide(OutlineFeature2D outline, Datum2D datum, Point2D point)
	{
		double num = Math.Abs(point.X - outline.MinX);
		double num2 = Math.Abs(outline.MaxX - point.X);
		if (Math.Abs(num - num2) <= _config.GeometryTolerance)
		{
			return ChooseOppositeVerticalDatumSide(outline, datum);
		}
		return (num < num2) ? DimensionSide.Left : DimensionSide.Right;
	}

	private DimensionSide ChooseHorizontalHoleSide(OutlineFeature2D outline, Point2D point)
	{
		double num = Math.Abs(point.Y - outline.MinY);
		double num2 = Math.Abs(outline.MaxY - point.Y);
		return (!(num <= num2)) ? DimensionSide.Top : DimensionSide.Bottom;
	}

	private DimensionSide ChooseVerticalHoleSide(OutlineFeature2D outline, Point2D point)
	{
		double num = Math.Abs(point.X - outline.MinX);
		double num2 = Math.Abs(outline.MaxX - point.X);
		return (num <= num2) ? DimensionSide.Left : DimensionSide.Right;
	}

	private DimensionSide ChooseOppositeHorizontalDatumSide(OutlineFeature2D outline, Datum2D datum)
	{
		double num = Math.Abs(datum.BaseY - outline.MinY);
		double num2 = Math.Abs(outline.MaxY - datum.BaseY);
		return (num <= num2) ? DimensionSide.Top : DimensionSide.Bottom;
	}

	private DimensionSide ChooseOppositeVerticalDatumSide(OutlineFeature2D outline, Datum2D datum)
	{
		double num = Math.Abs(datum.BaseX - outline.MinX);
		double num2 = Math.Abs(outline.MaxX - datum.BaseX);
		DimensionSide dimensionSide = ((num <= num2) ? DimensionSide.Left : DimensionSide.Right);
		return (dimensionSide == DimensionSide.Left) ? DimensionSide.Right : DimensionSide.Left;
	}

	private static bool ShouldUseDatumHoleLocationTolerance(Datum2D datum, bool isXDirection)
	{
		if (datum.DatumHole == null)
		{
			return true;
		}
		return isXDirection ? datum.DatumHoleLocationUseToleranceX : datum.DatumHoleLocationUseToleranceY;
	}

	private string GetPinGroupDebugOwner(PinGroupPlan group)
	{
		return (group == null) ? string.Empty : ("PG" + group.GroupIndex.ToString(CultureInfo.InvariantCulture));
	}

	private static string GetPinDatumAlignmentKey(PinGroupPlan firstGroup, bool horizontal)
	{
		if (firstGroup == null)
		{
			return string.Empty;
		}
		return "PG" + firstGroup.GroupIndex.ToString(CultureInfo.InvariantCulture)
			+ ":DatumChain:" + (horizontal ? "H" : "V");
	}

	private static string GetPinFunctionalHoleAlignmentKey(PinGroupPlan group, bool horizontal)
	{
		if (group == null)
		{
			return string.Empty;
		}
		return "PG" + group.GroupIndex.ToString(CultureInfo.InvariantCulture)
			+ ":FunctionalHoles:" + (horizontal ? "H" : "V");
	}

	private static string GetStructureAlignmentKey(DimensionSide side, bool horizontal)
	{
		string sideTag = side switch
		{
			DimensionSide.Top => "T",
			DimensionSide.Bottom => "B",
			DimensionSide.Left => "L",
			DimensionSide.Right => "R",
			_ => side.ToString()
		};
		return "Structure:" + sideTag + ":" + (horizontal ? "H" : "V");
	}

	/// <summary>
	/// Shared alignment lane for one continuous slot locating chain
	/// (edge location + inter-slot center distances on the same column/row).
	/// </summary>
	private static string GetSlotChainAlignmentKey(DimensionSide side, bool horizontal, double sharedCoordinate)
	{
		string sideTag = side switch
		{
			DimensionSide.Top => "T",
			DimensionSide.Bottom => "B",
			DimensionSide.Left => "L",
			DimensionSide.Right => "R",
			_ => side.ToString()
		};
		string coord = sharedCoordinate.ToString("0.###", CultureInfo.InvariantCulture);
		return "SlotChain:" + sideTag + ":" + (horizontal ? "H" : "V") + ":" + coord;
	}
}
