using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Model;

namespace CadAuto.Core.Planning;

/// <summary>
/// After FeatureFirst Select, overlay keep/drop from the nearest confirmed case.
/// No match or a token conflict → leave Select unchanged.
/// </summary>
public sealed class AnnotationCaseOverlay
{
	private readonly AnnotationCaseStore _store;

	public AnnotationCaseOverlay(AnnotationCaseStore store)
	{
		_store = store ?? new AnnotationCaseStore();
	}

	public string Apply(IList<StructureFeature> features, OutlineFeature2D outline)
	{
		AnnotationCaseRuntime.LastMatchedCaseId = string.Empty;
		if (features == null || outline == null || _store.Cases.Count == 0)
		{
			return string.Empty;
		}
		HashSet<string> present = new HashSet<string>(StringComparer.Ordinal);
		foreach (StructureFeature feature in features)
		{
			string token = StructureSchemaEncoder.Encode(feature, outline);
			if (!string.IsNullOrEmpty(token))
			{
				present.Add(token);
			}
		}
		AnnotationCase best = null;
		int bestHits = 0;
		foreach (AnnotationCase annotationCase in _store.Cases)
		{
			if (annotationCase.Decisions == null || annotationCase.Decisions.Count == 0)
			{
				continue;
			}
			int hits = annotationCase.Decisions.Count(d => present.Contains(d.Token));
			if (hits > bestHits)
			{
				bestHits = hits;
				best = annotationCase;
			}
		}
		if (best == null || bestHits * 2 < best.Decisions.Count)
		{
			return string.Empty;
		}
		if (HasConflictingDecisions(best))
		{
			return string.Empty;
		}
		foreach (AnnotationCaseDecision decision in best.Decisions)
		{
			foreach (StructureFeature feature in features)
			{
				if (!string.Equals(StructureSchemaEncoder.Encode(feature, outline), decision.Token, StringComparison.Ordinal))
				{
					continue;
				}
				feature.Keep = decision.Keep;
				if (!decision.Keep)
				{
					feature.SuppressReason = "AnnotationCase:" + best.Id;
				}
				else if (string.Equals(feature.SuppressReason, "AnnotationCase:" + best.Id, StringComparison.Ordinal)
					|| feature.SuppressReason == null)
				{
					feature.SuppressReason = null;
				}
			}
		}
		AnnotationCaseRuntime.LastMatchedCaseId = best.Id;
		return best.Id;
	}

	public static AnnotationCase Capture(IList<StructureFeature> features, OutlineFeature2D outline, string id)
	{
		AnnotationCase annotationCase = new AnnotationCase
		{
			Id = string.IsNullOrEmpty(id) ? DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture) : id
		};
		if (features == null || outline == null)
		{
			return annotationCase;
		}
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (StructureFeature feature in features)
		{
			if (feature == null || feature.Kind == StructureFeatureKind.Overall)
			{
				continue;
			}
			string token = StructureSchemaEncoder.Encode(feature, outline);
			if (string.IsNullOrEmpty(token) || !seen.Add(token))
			{
				continue;
			}
			annotationCase.Decisions.Add(new AnnotationCaseDecision { Token = token, Keep = feature.Keep });
		}
		return annotationCase;
	}

	private static bool HasConflictingDecisions(AnnotationCase annotationCase)
	{
		Dictionary<string, bool> keepByToken = new Dictionary<string, bool>(StringComparer.Ordinal);
		foreach (AnnotationCaseDecision decision in annotationCase.Decisions)
		{
			if (string.IsNullOrEmpty(decision.Token))
			{
				continue;
			}
			if (keepByToken.TryGetValue(decision.Token, out bool keep) && keep != decision.Keep)
			{
				return true;
			}
			keepByToken[decision.Token] = decision.Keep;
		}
		return false;
	}
}
