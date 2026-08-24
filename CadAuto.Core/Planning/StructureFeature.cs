using System.Collections.Generic;
using CadAuto.Core.Geometry;

namespace CadAuto.Core.Planning;

/// <summary>
/// Axis-independent structure measurement (Phase B). Placement side is assigned later.
/// </summary>
public enum StructureFeatureKind
{
	Overall,
	RealStep,
	NotchOpening,
	BodyRemainder,
	NoiseTip
}

/// <summary>
/// Measurement axis in outline envelope coordinates.
/// Horizontal = measure ΔX (width-like); Vertical = measure ΔY (height-like).
/// </summary>
public enum StructureFeatureAxis
{
	Horizontal,
	Vertical
}

public sealed class StructureFeature
{
	public StructureFeatureAxis Axis { get; set; }

	public StructureFeatureKind Kind { get; set; }

	/// <summary>Interval start on the measurement axis (X if Horizontal, Y if Vertical).</summary>
	public double T0 { get; set; }

	/// <summary>Interval end on the measurement axis.</summary>
	public double T1 { get; set; }

	public double Span => System.Math.Abs(T1 - T0);

	/// <summary>Cross-axis position of the supporting edge (Y for horizontal edges, X for vertical).</summary>
	public double CrossPosition { get; set; }

	/// <summary>Preferred outer placement after facing resolution.</summary>
	public DimensionSide PreferredSide { get; set; }

	public int ChainId { get; set; }

	public double Confidence { get; set; }

	public bool IsInset { get; set; }

	public bool TouchesOverallMin { get; set; }

	public bool TouchesOverallMax { get; set; }

	public Point2D FirstPoint { get; set; }

	public Point2D SecondPoint { get; set; }

	public string SourceKey { get; set; }

	public bool Keep { get; set; } = true;

	public string SuppressReason { get; set; }

	/// <summary>Place beside the supporting edge (inner boss), not on the AABB outer side.</summary>
	public bool PreferLocalPlacement { get; set; }

	public List<string> SupportSourceKeys { get; private set; } = new List<string>();
}
