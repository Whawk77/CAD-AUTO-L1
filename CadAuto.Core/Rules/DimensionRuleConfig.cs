using System;
using System.Collections.Generic;
using System.Globalization;

namespace CadAuto.Core.Rules;

public sealed class DimensionRuleConfig
{
	public Dictionary<double, string> HoleFitTolerance { get; private set; }

	public Dictionary<double, string> CenterDistanceTolerance { get; private set; }

	public Dictionary<double, string> ThreadMinorDiameterCallout { get; private set; }

	public double GeometryTolerance { get; set; }

	public double TextHeight { get; set; }

	public double ArrowSize { get; set; }

	public double FirstDimOffset { get; set; }

	public double DimTextClearance { get; set; }

	public double LeaderOffset { get; set; }

	public double ThreadArcAngleToleranceDegrees { get; set; }

	public double ThreadMinorDiameterTolerance { get; set; }

	public double ToleranceTextHeightScale { get; set; }

	public string DatumHoleLocationToleranceText { get; set; }

	public bool DatumHoleLocationDefaultUseTolerance { get; set; }

	public string PinCenterDistanceToleranceText { get; set; }

	public string PinGroupDistanceToleranceText { get; set; }

	public string PinHoleFitToleranceText { get; set; }

	public DimensionRuleConfig()
	{
		HoleFitTolerance = new Dictionary<double, string>();
		CenterDistanceTolerance = new Dictionary<double, string>();
		ThreadMinorDiameterCallout = new Dictionary<double, string>();
	}

	public static DimensionRuleConfig CreateDefault()
	{
		DimensionRuleConfig dimensionRuleConfig = new DimensionRuleConfig
		{
			GeometryTolerance = 0.001,
			TextHeight = 2.5,
			ArrowSize = 2.5,
			FirstDimOffset = 10.0,
			DimTextClearance = 3.0,
			LeaderOffset = 12.0,
			ThreadArcAngleToleranceDegrees = 2.0,
			ThreadMinorDiameterTolerance = 0.25,
			ToleranceTextHeightScale = 0.8,
			DatumHoleLocationToleranceText = "<>\\H0.8x;±0.05\\H1x;",
			DatumHoleLocationDefaultUseTolerance = false,
			PinCenterDistanceToleranceText = "±0.02",
			PinGroupDistanceToleranceText = "±0.05",
			PinHoleFitToleranceText = "H7"
		};
		dimensionRuleConfig.CenterDistanceTolerance[30.0] = "±0.02";
		dimensionRuleConfig.ThreadMinorDiameterCallout[4.917] = "M6";
		dimensionRuleConfig.ThreadMinorDiameterCallout[5.0] = "M6";
		dimensionRuleConfig.ThreadMinorDiameterCallout[6.647] = "M8";
		dimensionRuleConfig.ThreadMinorDiameterCallout[6.8] = "M8";
		dimensionRuleConfig.ThreadMinorDiameterCallout[8.376] = "M10";
		dimensionRuleConfig.ThreadMinorDiameterCallout[8.5] = "M10";
		dimensionRuleConfig.ThreadMinorDiameterCallout[10.106] = "M12";
		dimensionRuleConfig.ThreadMinorDiameterCallout[10.2] = "M12";
		dimensionRuleConfig.ThreadMinorDiameterCallout[13.835] = "M16";
		dimensionRuleConfig.ThreadMinorDiameterCallout[14.0] = "M16";
		return dimensionRuleConfig;
	}

	public string GetHoleFitTolerance(double diameter)
	{
		string value;
		return TryFindByTolerance(HoleFitTolerance, diameter, out value) ? value : string.Empty;
	}

	public string GetCenterDistanceTolerance(double distance)
	{
		string value;
		return TryFindByTolerance(CenterDistanceTolerance, distance, out value) ? value : string.Empty;
	}

	public string FormatHoleCallout(double diameter, int count)
	{
		return FormatHoleCallout(diameter, count, GetHoleFitTolerance(diameter));
	}

	public string FormatHoleCallout(double diameter, int count, string fitTolerance)
	{
		string text = "%%c" + FormatNumber(diameter) + (fitTolerance ?? string.Empty);
		return (count > 1) ? (count.ToString(CultureInfo.InvariantCulture) + "-" + text) : text;
	}

	public string FormatCenterDistanceOverride(double distance)
	{
		string centerDistanceTolerance = GetCenterDistanceTolerance(distance);
		return string.IsNullOrEmpty(centerDistanceTolerance) ? string.Empty : (FormatNumber(distance) + FormatToleranceSuffix(centerDistanceTolerance));
	}

	public string FormatPinCenterDistanceOverride(double distance)
	{
		return FormatNumber(distance) + FormatToleranceSuffix(PinCenterDistanceToleranceText);
	}

	public string FormatPinGroupDistanceOverride(double distance)
	{
		return FormatNumber(distance) + FormatToleranceSuffix(PinGroupDistanceToleranceText);
	}

	public string FormatThreadCallout(double diameter)
	{
		return "M" + FormatNumber(diameter);
	}

	public string GetThreadCalloutForMinorDiameter(double diameter)
	{
		foreach (KeyValuePair<double, string> item in ThreadMinorDiameterCallout)
		{
			if (Math.Abs(item.Key - diameter) <= ThreadMinorDiameterTolerance)
			{
				return item.Value;
			}
		}
		return string.Empty;
	}

	public string FormatThreadCallout(double diameter, int count)
	{
		string text = FormatThreadCallout(diameter);
		return (count > 1) ? (count.ToString(CultureInfo.InvariantCulture) + "-" + text) : text;
	}

	public string FormatThreadCallout(double diameter, int count, string explicitCallout)
	{
		string text = (string.IsNullOrEmpty(explicitCallout) ? FormatThreadCallout(diameter) : explicitCallout);
		return (count > 1) ? (count.ToString(CultureInfo.InvariantCulture) + "-" + text) : text;
	}

	public string FormatNumber(double value)
	{
		return value.ToString("0.###", CultureInfo.InvariantCulture);
	}

	private string FormatToleranceSuffix(string toleranceText)
	{
		if (string.IsNullOrEmpty(toleranceText))
		{
			return string.Empty;
		}
		return string.Format(CultureInfo.InvariantCulture, "\\H{0:0.###}x;{1}\\H1x;", (ToleranceTextHeightScale <= 0.0) ? 0.8 : ToleranceTextHeightScale, toleranceText);
	}

	private bool TryFindByTolerance(Dictionary<double, string> rules, double actual, out string value)
	{
		foreach (KeyValuePair<double, string> rule in rules)
		{
			if (Math.Abs(rule.Key - actual) <= GeometryTolerance)
			{
				value = rule.Value;
				return true;
			}
		}
		value = string.Empty;
		return false;
	}
}
