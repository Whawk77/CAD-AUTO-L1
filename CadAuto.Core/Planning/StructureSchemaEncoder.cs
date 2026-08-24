using System;
using CadAuto.Core.Model;

namespace CadAuto.Core.Planning;

/// <summary>
/// Maps a structure feature to a qualitative token: axis, role, envelope ends, inset,
/// and span as a tenth of overall. No absolute millimetre values.
/// </summary>
public static class StructureSchemaEncoder
{
	public static string Encode(StructureFeature feature, OutlineFeature2D outline)
	{
		if (feature == null || outline == null)
		{
			return string.Empty;
		}
		double overall = feature.Axis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
		int decile = 0;
		if (overall > 0.0)
		{
			decile = (int)Math.Floor(10.0 * feature.Span / overall);
			if (decile < 0)
			{
				decile = 0;
			}
			if (decile > 9)
			{
				decile = 9;
			}
		}
		return string.Join("|", new[]
		{
			feature.Axis == StructureFeatureAxis.Horizontal ? "H" : "V",
			Role(feature),
			feature.TouchesOverallMin ? "1" : "0",
			feature.TouchesOverallMax ? "1" : "0",
			feature.IsInset ? "1" : "0",
			decile.ToString()
		});
	}

	public static string Role(StructureFeature feature)
	{
		string key = feature.SourceKey ?? string.Empty;
		if (key.StartsWith("LedgeResidual", StringComparison.Ordinal))
		{
			if (feature.TouchesOverallMax && !feature.TouchesOverallMin)
			{
				return "ResidualMax";
			}
			if (feature.TouchesOverallMin && !feature.TouchesOverallMax)
			{
				return "ResidualMin";
			}
			return "Residual";
		}
		if (key.StartsWith("OuterTip", StringComparison.Ordinal))
		{
			return "OuterTip";
		}
		if (key.StartsWith("StepGroove", StringComparison.Ordinal))
		{
			return "StepGroove";
		}
		if (key.StartsWith("InnerBoss", StringComparison.Ordinal))
		{
			return "InnerBoss";
		}
		if (key.StartsWith("FootLedge", StringComparison.Ordinal))
		{
			return "FootLedge";
		}
		return "RealStep";
	}
}
