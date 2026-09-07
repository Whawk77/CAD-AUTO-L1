using System.Collections.Generic;

namespace CadAuto.Core.Planning;

public sealed class DimensionCandidateDiagnostic
{
	public int Id { get; set; }

	public string Kind { get; set; }

	public string SourceFeatureId { get; set; }

	public double Value { get; set; }

	public double FirstPointX { get; set; }

	public double FirstPointY { get; set; }

	public double SecondPointX { get; set; }

	public double SecondPointY { get; set; }

	public double MeasurementMinimum { get; set; }

	public double MeasurementMaximum { get; set; }

	public string PlacementSide { get; set; }

	public string RequestedPlacementSide { get; set; }

	public int Priority { get; set; }

	public string ReadingLevel { get; set; }

	public string AlignmentKey { get; set; }

	public int AlignmentPriority { get; set; }

	public bool PreserveAlignmentLevel { get; set; }

	public bool HasFinalPlacement { get; set; }

	public int? StackingLevel { get; set; }

	public double? StackingOffset { get; set; }

	public double? ResolvedDimLineCoordinate { get; set; }

	public bool? UsesLocalBoundary { get; set; }

	public bool? HasAlignmentCoordinateOverride { get; set; }

	public string AlignmentLaneKey { get; set; }

	public int? AlignmentLaneMemberCount { get; set; }

	public string AlignmentDecision { get; set; }

	public string LayoutBlockId { get; set; }

	public string LayoutBlockType { get; set; }

	public double EffectiveSpan { get; set; }

	public int EffectiveOrder { get; set; }

	public string OrderingReason { get; set; }

	public string PromotedByConflictWith { get; set; }

	public double PhysicalOutwardDistance { get; set; }

	public bool PhysicalOrderValidated { get; set; }

	public bool IsSuppressed { get; set; }

	public bool IsSelected { get; set; }

	public bool IsAttachmentValid { get; set; }

	public string AttachmentKind { get; set; }

	public string DecisionStatus { get; set; }

	public string DecisionReason { get; set; }

	public string SuppressedReason { get; set; }

	public string Orientation { get; set; }

	public string DebugRole { get; set; }

	public string OverrideText { get; set; }

	public DimensionCandidateRole Role { get; set; }

	public DimensionCandidateOwnerKind OwnerKind { get; set; }

	public List<string> SourceGeometryIds { get; set; } = new List<string>();

	public string TopologyEvidence { get; set; }

	public string RuleId { get; set; }

	public DimensionCandidateDecision Decision { get; set; }
}
