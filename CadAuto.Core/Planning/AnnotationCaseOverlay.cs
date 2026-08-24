using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Model;

namespace CadAuto.Core.Planning;

/// <summary>
/// After FeatureFirst Select, apply confirmed annotation <em>strategies</em> when the
/// live covering-chain schema matches a stored case. Empty store / no schema match
/// leaves Select unchanged. Token decisions are archive-only.
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
		double tol = 0.001;
		double microGap = Math.Max(5.0, Math.Max(outline.Width, outline.Height) * 0.025);
		HashSet<string> liveSchemas = new HashSet<string>(
			StructureMeasurementSelector.DetectCoveringChainSchemas(features, outline, microGap, tol),
			StringComparer.Ordinal);
		AnnotationCase best = null;
		List<string> bestStrategies = null;
		foreach (AnnotationCase annotationCase in _store.Cases)
		{
			List<string> strategies = EffectiveStrategies(annotationCase);
			if (strategies.Count == 0)
			{
				continue;
			}
			if (!SchemaMatches(annotationCase, liveSchemas, strategies))
			{
				continue;
			}
			best = annotationCase;
			bestStrategies = strategies;
			break;
		}
		if (best == null || bestStrategies == null)
		{
			return string.Empty;
		}
		foreach (string strategy in bestStrategies.Distinct(StringComparer.Ordinal))
		{
			if (string.Equals(strategy, AnnotationStrategyIds.OpenCoveringDropResidualMax, StringComparison.Ordinal))
			{
				StructureMeasurementSelector.ApplyOpenCoveringDropResidualMax(features, outline, microGap, tol);
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
		double tol = 0.001;
		double microGap = Math.Max(5.0, Math.Max(outline.Width, outline.Height) * 0.025);
		foreach (string schema in StructureMeasurementSelector.DetectCoveringChainSchemas(features, outline, microGap, tol))
		{
			annotationCase.Schemas.Add(schema);
		}
		if (annotationCase.Schemas.Count > 0)
		{
			annotationCase.Strategies.Add(AnnotationStrategyIds.OpenCoveringDropResidualMax);
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

	private static List<string> EffectiveStrategies(AnnotationCase annotationCase)
	{
		List<string> strategies = new List<string>();
		if (annotationCase.Strategies != null)
		{
			foreach (string strategy in annotationCase.Strategies)
			{
				if (!string.IsNullOrEmpty(strategy))
				{
					strategies.Add(strategy);
				}
			}
		}
		if (strategies.Count > 0)
		{
			return strategies;
		}
		if (annotationCase.Decisions == null)
		{
			return strategies;
		}
		bool dropMax = annotationCase.Decisions.Any(d =>
			d != null && !d.Keep && d.Token != null && d.Token.IndexOf("ResidualMax", StringComparison.Ordinal) >= 0);
		bool keepMin = annotationCase.Decisions.Any(d =>
			d != null && d.Keep && d.Token != null && d.Token.IndexOf("ResidualMin", StringComparison.Ordinal) >= 0);
		if (dropMax && keepMin)
		{
			strategies.Add(AnnotationStrategyIds.OpenCoveringDropResidualMax);
		}
		return strategies;
	}

	private static bool SchemaMatches(AnnotationCase annotationCase, HashSet<string> liveSchemas, List<string> strategies)
	{
		if (liveSchemas.Count == 0)
		{
			return false;
		}
		if (annotationCase.Schemas != null && annotationCase.Schemas.Count > 0)
		{
			return annotationCase.Schemas.Any(liveSchemas.Contains);
		}
		return strategies.Contains(AnnotationStrategyIds.OpenCoveringDropResidualMax)
			&& liveSchemas.Any(s => s.StartsWith("CoveringChain|", StringComparison.Ordinal));
	}
}
