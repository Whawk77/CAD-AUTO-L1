using System;
using System.Linq;

namespace CadAuto.Core.Planning;

/// <summary>
/// Eligibility only: a caller must still prove the real boundary/topology relation.
/// A source label or equal numeric value is never a coverage proof.
/// </summary>
public static class StructureCoveragePolicy
{
	public static string GetIneligibilityReason(PlannedDimension dimension, string replaceableRuleId = null)
	{
		if (dimension == null)
		{
			return "MissingCandidate";
		}
		if (dimension.Kind != DimensionKind.Normal
			|| dimension.Role != DimensionCandidateRole.Structure
			|| dimension.OwnerKind != DimensionCandidateOwnerKind.Outline)
		{
			return "ProtectedSemanticIdentity";
		}
		// Explicit text may carry tolerance or functional intent. Do not infer
		// redundancy from geometry when that information would be lost.
		if (!string.IsNullOrWhiteSpace(dimension.OverrideText) || dimension.ForceOuterLevel)
		{
			return "IndependentAnnotationRequirement";
		}
		if (!string.IsNullOrEmpty(dimension.RuleId) && dimension.RuleId != replaceableRuleId)
		{
			return "ExistingRuleRequiresReview";
		}
		if (dimension.AttachmentValidity == false)
		{
			return "InvalidAttachment";
		}
		if (dimension.SourceGeometryIds == null
			|| !dimension.SourceGeometryIds.Any(id => !string.IsNullOrWhiteSpace(id)))
		{
			return "MissingSourceGeometry";
		}
		return null;
	}
}
