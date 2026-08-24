using System.Collections.Generic;
using CadAuto.Core.Model;

namespace CadAuto.Core.Planning;

/// <summary>
/// Confirmed drawing: annotation <em>logic</em> (strategy ids + covering-chain schemas),
/// not millimetre values. Token decisions are an archive only.
/// </summary>
public sealed class AnnotationCase
{
	public string Id { get; set; }

	public List<string> Strategies { get; set; } = new List<string>();

	public List<string> Schemas { get; set; } = new List<string>();

	public List<AnnotationCaseDecision> Decisions { get; set; } = new List<AnnotationCaseDecision>();
}

public static class AnnotationStrategyIds
{
	/// <summary>
	/// Same-side ResidualMin + real step + ResidualMax cover overall → drop the
	/// Max-end residual (no process meaning) and keep the locating residual.
	/// </summary>
	public const string OpenCoveringDropResidualMax = "OpenCoveringDropResidualMax";
}

public sealed class AnnotationCaseDecision
{
	public string Token { get; set; }

	public bool Keep { get; set; }
}

/// <summary>Last FeatureFirst snapshot so ASDCASE can save a confirmed case.</summary>
public static class AnnotationCaseRuntime
{
	public static IList<StructureFeature> LastFeatures { get; set; }

	public static OutlineFeature2D LastOutline { get; set; }

	public static string LastMatchedCaseId { get; set; }
}
