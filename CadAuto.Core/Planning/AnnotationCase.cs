using System.Collections.Generic;
using CadAuto.Core.Model;

namespace CadAuto.Core.Planning;

/// <summary>
/// One confirmed drawing: qualitative structure tokens and keep/drop decisions.
/// Tokens are rotation-stable (axis + envelope ends), not millimetre values.
/// </summary>
public sealed class AnnotationCase
{
	public string Id { get; set; }

	public List<AnnotationCaseDecision> Decisions { get; set; } = new List<AnnotationCaseDecision>();
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
