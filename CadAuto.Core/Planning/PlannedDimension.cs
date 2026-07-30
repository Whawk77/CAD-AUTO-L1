using System.Collections.Generic;
using CadAuto.Core.Geometry;

namespace CadAuto.Core.Planning;

public sealed class PlannedDimension
{
	public int DiagnosticId { get; set; }

	public DimensionKind Kind { get; set; }

	public DimensionOrientation Orientation { get; set; }

	public DimensionSide Side { get; set; }

	public Point2D FirstPoint { get; set; }

	public Point2D SecondPoint { get; set; }

	public string OverrideText { get; set; }

	public string SourceKey { get; set; }

	public bool ForceOuterLevel { get; set; }

	public bool FirstPointMustLieOnOutline { get; set; }

	public double? RequiredOutlineReferenceCoordinate { get; set; }

	public bool UseSegmentedExtensionLines { get; set; }

	public bool PreferLocalBoundary { get; set; }

	public bool PreferFeatureLocalPlacement { get; set; }

	public bool PreservePreferredSide { get; set; }

	public int ChainId { get; set; }

	public string AlignmentKey { get; set; }

	public int AlignmentPriority { get; set; }

	public bool PreserveAlignmentLevel { get; set; }

	public DimensionReadingLevel ReadingLevel { get; set; }

	public string DebugOwner { get; set; }

	public string DebugRole { get; set; }

	public DimensionCandidateRole Role { get; set; }

	public DimensionCandidateOwnerKind OwnerKind { get; set; }

	public List<string> SourceGeometryIds { get; set; } = new List<string>();

	public string TopologyEvidence { get; set; }

	public string RuleId { get; set; }
}
