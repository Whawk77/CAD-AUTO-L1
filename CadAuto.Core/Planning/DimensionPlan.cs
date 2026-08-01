using System;
using System.Collections.Generic;
using CadAuto.Core.Geometry;

namespace CadAuto.Core.Planning;

public sealed class DimensionPlan
{
	private int _nextDiagnosticId;

	private readonly Dictionary<PlannedDimension, DimensionCandidateDiagnostic> _diagnosticByDimension;

	public List<PlannedDimension> Dimensions { get; private set; }

	public List<PinGroupPlan> PinGroups { get; private set; }

	public DimensionDiagnosticReport Diagnostics { get; private set; }

	public DimensionPlan()
	{
		_nextDiagnosticId = 1;
		_diagnosticByDimension = new Dictionary<PlannedDimension, DimensionCandidateDiagnostic>();
		Dimensions = new List<PlannedDimension>();
		PinGroups = new List<PinGroupPlan>();
		Diagnostics = new DimensionDiagnosticReport();
	}

	public void Add(PlannedDimension dimension)
	{
		if (dimension != null)
		{
			DimensionCandidateSemantics.ApplyLegacyMappings(dimension);
			Dimensions.Add(dimension);
			AddDiagnosticCandidate(dimension);
		}
	}

	public void MarkSuppressed(PlannedDimension dimension, string reason)
	{
		MarkSuppressed(dimension, reason, null, null, null);
	}

	public void MarkSuppressed(PlannedDimension dimension, string reason, string ruleId, IEnumerable<string> sourceGeometryIds, string topologyEvidence)
	{
		if (dimension != null)
		{
			if (!_diagnosticByDimension.TryGetValue(dimension, out var value))
			{
				value = AddDiagnosticCandidate(dimension);
			}
			RecordRuleEvidence(dimension, value, ruleId, sourceGeometryIds, topologyEvidence);
			value.IsSuppressed = true;
			value.SuppressedReason = reason ?? string.Empty;
			value.IsSelected = false;
			value.DecisionStatus = "Suppressed";
			value.DecisionReason = reason ?? string.Empty;
			value.Decision = DimensionCandidateDecision.Suppressed;
		}
	}

	public void RecordRuleEvidence(PlannedDimension dimension, string ruleId, IEnumerable<string> sourceGeometryIds = null, string topologyEvidence = null)
	{
		if (dimension == null)
		{
			return;
		}
		if (!_diagnosticByDimension.TryGetValue(dimension, out var diagnostic))
		{
			diagnostic = AddDiagnosticCandidate(dimension);
		}
		RecordRuleEvidence(dimension, diagnostic, ruleId, sourceGeometryIds, topologyEvidence);
	}

	public void AddDiscardedCandidate(PlannedDimension dimension, string reason)
	{
		if (dimension != null)
		{
			DimensionCandidateSemantics.ApplyLegacyMappings(dimension);
			AddSkippedDimension(
				dimension.Kind,
				dimension.Orientation,
				dimension.Side,
				dimension.FirstPoint,
				dimension.SecondPoint,
				reason,
				dimension.DebugRole,
				dimension.DebugOwner,
				dimension.Role,
				dimension.OwnerKind,
				dimension.SourceGeometryIds,
				dimension.TopologyEvidence,
				dimension.RuleId);
		}
	}

	public void AddSkippedDimension(
		DimensionKind kind,
		DimensionOrientation orientation,
		DimensionSide side,
		Point2D requestedFirstPoint,
		Point2D secondPoint,
		string reason,
		string debugRole,
		string debugOwner = null,
		DimensionCandidateRole role = DimensionCandidateRole.Unknown,
		DimensionCandidateOwnerKind ownerKind = DimensionCandidateOwnerKind.Unknown,
		IEnumerable<string> sourceGeometryIds = null,
		string topologyEvidence = null,
		string ruleId = null)
	{
		string text = reason ?? string.Empty;
		if (role == DimensionCandidateRole.Unknown)
		{
			role = DimensionCandidateSemantics.GetRole(kind, debugRole);
		}
		if (ownerKind == DimensionCandidateOwnerKind.Unknown)
		{
			ownerKind = DimensionCandidateSemantics.GetOwnerKind(role, debugOwner);
		}
		Diagnostics.DimensionCandidates.Add(new DimensionCandidateDiagnostic
		{
			Id = _nextDiagnosticId++,
			Kind = kind.ToString(),
			SourceFeatureId = debugOwner ?? debugRole ?? string.Empty,
			Value = GetDiagnosticValue(orientation, requestedFirstPoint, secondPoint),
			FirstPointX = requestedFirstPoint.X,
			FirstPointY = requestedFirstPoint.Y,
			SecondPointX = secondPoint.X,
			SecondPointY = secondPoint.Y,
			MeasurementMinimum = GetMeasurementMinimum(orientation, requestedFirstPoint, secondPoint),
			MeasurementMaximum = GetMeasurementMaximum(orientation, requestedFirstPoint, secondPoint),
			PlacementSide = side.ToString(),
			RequestedPlacementSide = side.ToString(),
			Priority = GetDiagnosticPriority(kind),
			IsSuppressed = false,
			IsSelected = false,
			IsAttachmentValid = false,
			DecisionStatus = "Skipped",
			DecisionReason = text,
			SuppressedReason = string.Empty,
			Orientation = orientation.ToString(),
			DebugRole = debugRole ?? string.Empty,
			OverrideText = string.Empty,
			Role = role,
			OwnerKind = ownerKind,
			SourceGeometryIds = DimensionCandidateSemantics.MergeSourceGeometryIds(null, sourceGeometryIds),
			TopologyEvidence = topologyEvidence ?? string.Empty,
			RuleId = ruleId ?? string.Empty,
			Decision = DimensionCandidateDecision.Skipped
		});
	}

	public void CaptureFinalDimensions()
	{
		Diagnostics.FinalDimensions.Clear();
		foreach (DimensionCandidateDiagnostic candidate in Diagnostics.DimensionCandidates)
		{
			if (candidate.IsSuppressed)
			{
				candidate.Decision = DimensionCandidateDecision.Suppressed;
			}
			else if (string.Equals(candidate.DecisionStatus, "Skipped", StringComparison.Ordinal))
			{
				candidate.Decision = DimensionCandidateDecision.Skipped;
			}
			else
			{
				candidate.IsSelected = false;
				candidate.DecisionStatus = "NotSelected";
				candidate.DecisionReason = "NotPresentInFinalPlan";
				candidate.Decision = DimensionCandidateDecision.NotSelected;
			}
		}
		foreach (PlannedDimension dimension in Dimensions)
		{
			if (!_diagnosticByDimension.TryGetValue(dimension, out var value))
			{
				value = AddDiagnosticCandidate(dimension);
			}
			value.IsSelected = true;
			value.DecisionStatus = "Selected";
			value.DecisionReason = IsOverall(dimension.Kind) ? "RequiredOverallDimension" : "RetainedAfterSuppression";
			value.Decision = DimensionCandidateDecision.Selected;
			Diagnostics.FinalDimensions.Add(CloneDiagnostic(value));
		}
	}

	public void SynchronizeFinalPlacementSides()
	{
		foreach (KeyValuePair<PlannedDimension, DimensionCandidateDiagnostic> item in _diagnosticByDimension)
		{
			if (item.Value.HasFinalPlacement && Enum.TryParse(item.Value.PlacementSide, out DimensionSide side))
			{
				item.Key.Side = side;
			}
		}
	}

	private DimensionCandidateDiagnostic AddDiagnosticCandidate(PlannedDimension dimension)
	{
		DimensionCandidateSemantics.ApplyLegacyMappings(dimension);
		int diagnosticId = _nextDiagnosticId++;
		dimension.DiagnosticId = diagnosticId;
		DimensionCandidateDiagnostic dimensionCandidateDiagnostic = new DimensionCandidateDiagnostic
		{
			Id = diagnosticId,
			Kind = dimension.Kind.ToString(),
			SourceFeatureId = (dimension.SourceKey ?? dimension.DebugOwner ?? dimension.DebugRole ?? string.Empty),
			Value = GetDiagnosticValue(dimension),
			FirstPointX = dimension.FirstPoint.X,
			FirstPointY = dimension.FirstPoint.Y,
			SecondPointX = dimension.SecondPoint.X,
			SecondPointY = dimension.SecondPoint.Y,
			MeasurementMinimum = GetMeasurementMinimum(dimension),
			MeasurementMaximum = GetMeasurementMaximum(dimension),
			PlacementSide = dimension.Side.ToString(),
			RequestedPlacementSide = dimension.Side.ToString(),
			Priority = GetDiagnosticPriority(dimension),
			ReadingLevel = dimension.ReadingLevel.ToString(),
			AlignmentKey = dimension.AlignmentKey ?? string.Empty,
			AlignmentPriority = dimension.AlignmentPriority,
			PreserveAlignmentLevel = dimension.PreserveAlignmentLevel,
			AlignmentLaneKey = string.Empty,
			AlignmentDecision = string.Empty,
			LayoutBlockId = string.Empty,
			LayoutBlockType = string.Empty,
			OrderingReason = string.Empty,
			PromotedByConflictWith = string.Empty,
			IsSuppressed = false,
			IsSelected = false,
			IsAttachmentValid = true,
			DecisionStatus = "Candidate",
			DecisionReason = "Generated",
			SuppressedReason = string.Empty,
			Orientation = dimension.Orientation.ToString(),
			DebugRole = (dimension.DebugRole ?? string.Empty),
			OverrideText = (dimension.OverrideText ?? string.Empty),
			Role = dimension.Role,
			OwnerKind = dimension.OwnerKind,
			SourceGeometryIds = DimensionCandidateSemantics.MergeSourceGeometryIds(null, dimension.SourceGeometryIds),
			TopologyEvidence = dimension.TopologyEvidence,
			RuleId = dimension.RuleId,
			Decision = DimensionCandidateDecision.Candidate
		};
		_diagnosticByDimension[dimension] = dimensionCandidateDiagnostic;
		Diagnostics.DimensionCandidates.Add(dimensionCandidateDiagnostic);
		return dimensionCandidateDiagnostic;
	}

	private static DimensionCandidateDiagnostic CloneDiagnostic(DimensionCandidateDiagnostic source)
	{
		return new DimensionCandidateDiagnostic
		{
			Id = source.Id,
			Kind = source.Kind,
			SourceFeatureId = source.SourceFeatureId,
			Value = source.Value,
			FirstPointX = source.FirstPointX,
			FirstPointY = source.FirstPointY,
			SecondPointX = source.SecondPointX,
			SecondPointY = source.SecondPointY,
			MeasurementMinimum = source.MeasurementMinimum,
			MeasurementMaximum = source.MeasurementMaximum,
			PlacementSide = source.PlacementSide,
			RequestedPlacementSide = source.RequestedPlacementSide,
			Priority = source.Priority,
			ReadingLevel = source.ReadingLevel,
			AlignmentKey = source.AlignmentKey,
			AlignmentPriority = source.AlignmentPriority,
			PreserveAlignmentLevel = source.PreserveAlignmentLevel,
			HasFinalPlacement = source.HasFinalPlacement,
			StackingLevel = source.StackingLevel,
			StackingOffset = source.StackingOffset,
			ResolvedDimLineCoordinate = source.ResolvedDimLineCoordinate,
			UsesLocalBoundary = source.UsesLocalBoundary,
			HasAlignmentCoordinateOverride = source.HasAlignmentCoordinateOverride,
			AlignmentLaneKey = source.AlignmentLaneKey,
			AlignmentLaneMemberCount = source.AlignmentLaneMemberCount,
			AlignmentDecision = source.AlignmentDecision,
			LayoutBlockId = source.LayoutBlockId,
			LayoutBlockType = source.LayoutBlockType,
			EffectiveSpan = source.EffectiveSpan,
			EffectiveOrder = source.EffectiveOrder,
			OrderingReason = source.OrderingReason,
			PromotedByConflictWith = source.PromotedByConflictWith,
			PhysicalOutwardDistance = source.PhysicalOutwardDistance,
			PhysicalOrderValidated = source.PhysicalOrderValidated,
			IsSuppressed = source.IsSuppressed,
			IsSelected = source.IsSelected,
			IsAttachmentValid = source.IsAttachmentValid,
			DecisionStatus = source.DecisionStatus,
			DecisionReason = source.DecisionReason,
			SuppressedReason = source.SuppressedReason,
			Orientation = source.Orientation,
			DebugRole = source.DebugRole,
			OverrideText = source.OverrideText,
			Role = source.Role,
			OwnerKind = source.OwnerKind,
			SourceGeometryIds = DimensionCandidateSemantics.MergeSourceGeometryIds(null, source.SourceGeometryIds),
			TopologyEvidence = source.TopologyEvidence,
			RuleId = source.RuleId,
			Decision = source.Decision
		};
	}

	private static void RecordRuleEvidence(
		PlannedDimension dimension,
		DimensionCandidateDiagnostic diagnostic,
		string ruleId,
		IEnumerable<string> sourceGeometryIds,
		string topologyEvidence)
	{
		DimensionCandidateSemantics.ApplyLegacyMappings(dimension);
		if (!string.IsNullOrEmpty(ruleId))
		{
			EnsureCompatibleRuleId(dimension.RuleId, ruleId, diagnostic.Id);
			EnsureCompatibleRuleId(diagnostic.RuleId, ruleId, diagnostic.Id);
			dimension.RuleId = ruleId;
			diagnostic.RuleId = ruleId;
		}
		if (topologyEvidence != null)
		{
			dimension.TopologyEvidence = topologyEvidence;
			diagnostic.TopologyEvidence = topologyEvidence;
		}
		dimension.SourceGeometryIds = DimensionCandidateSemantics.MergeSourceGeometryIds(
			dimension.SourceGeometryIds,
			sourceGeometryIds);
		diagnostic.SourceGeometryIds = DimensionCandidateSemantics.MergeSourceGeometryIds(
			diagnostic.SourceGeometryIds,
			dimension.SourceGeometryIds);
		diagnostic.Role = dimension.Role;
		diagnostic.OwnerKind = dimension.OwnerKind;
	}

	private static void EnsureCompatibleRuleId(string existingRuleId, string ruleId, int diagnosticId)
	{
		if (!string.IsNullOrEmpty(existingRuleId)
			&& !string.Equals(existingRuleId, ruleId, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				"Dimension candidate "
				+ diagnosticId
				+ " already has RuleId '"
				+ existingRuleId
				+ "' and cannot be reassigned to '"
				+ ruleId
				+ "'.");
		}
	}

	private static double GetMeasurementMinimum(PlannedDimension dimension)
	{
		return GetMeasurementMinimum(dimension.Orientation, dimension.FirstPoint, dimension.SecondPoint);
	}

	private static double GetMeasurementMaximum(PlannedDimension dimension)
	{
		return GetMeasurementMaximum(dimension.Orientation, dimension.FirstPoint, dimension.SecondPoint);
	}

	private static double GetMeasurementMinimum(DimensionOrientation orientation, Point2D firstPoint, Point2D secondPoint)
	{
		return orientation == DimensionOrientation.Vertical ? Math.Min(firstPoint.Y, secondPoint.Y) : Math.Min(firstPoint.X, secondPoint.X);
	}

	private static double GetMeasurementMaximum(DimensionOrientation orientation, Point2D firstPoint, Point2D secondPoint)
	{
		return orientation == DimensionOrientation.Vertical ? Math.Max(firstPoint.Y, secondPoint.Y) : Math.Max(firstPoint.X, secondPoint.X);
	}

	private static bool IsOverall(DimensionKind kind)
	{
		return kind == DimensionKind.OverallWidth || kind == DimensionKind.OverallHeight;
	}

	private static double GetDiagnosticValue(PlannedDimension dimension)
	{
		return GetDiagnosticValue(dimension.Orientation, dimension.FirstPoint, dimension.SecondPoint);
	}

	private static double GetDiagnosticValue(DimensionOrientation orientation, Point2D firstPoint, Point2D secondPoint)
	{
		if (orientation == DimensionOrientation.Vertical)
		{
			return Math.Abs(secondPoint.Y - firstPoint.Y);
		}
		if (orientation == DimensionOrientation.Horizontal)
		{
			return Math.Abs(secondPoint.X - firstPoint.X);
		}
		return firstPoint.DistanceTo(secondPoint);
	}

	private static int GetDiagnosticPriority(PlannedDimension dimension)
	{
		return GetDiagnosticPriority(dimension.Kind);
	}

	private static int GetDiagnosticPriority(DimensionKind kind)
	{
		switch (kind)
		{
		case DimensionKind.OverallWidth:
		case DimensionKind.OverallHeight:
			return 1000;
		case DimensionKind.DatumHoleLocationX:
		case DimensionKind.DatumHoleLocationY:
			return 900;
		case DimensionKind.PinGroupDistance:
			return 800;
		case DimensionKind.PinDistance:
			return 700;
		case DimensionKind.HoleLocation:
			return 600;
		case DimensionKind.Normal:
			return 500;
		case DimensionKind.HoleDiameter:
			return 400;
		case DimensionKind.Chamfer:
		case DimensionKind.Fillet:
			return 300;
		default:
			return 0;
		}
	}
}
