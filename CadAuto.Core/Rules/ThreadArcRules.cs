using System;

namespace CadAuto.Core.Rules;

public static class ThreadArcRules
{
	public static bool IsThreadSweep(double sweepRadians, DimensionRuleConfig config)
	{
		if (config == null || double.IsNaN(sweepRadians) || double.IsInfinity(sweepRadians))
		{
			return false;
		}
		double normalizedSweep = NormalizePositiveSweep(sweepRadians);
		double toleranceRadians = Math.Max(0.0, config.ThreadArcAngleToleranceDegrees) * Math.PI / 180.0;
		return normalizedSweep >= 3.0 * Math.PI / 2.0 - toleranceRadians;
	}

	private static double NormalizePositiveSweep(double sweepRadians)
	{
		double fullTurn = Math.PI * 2.0;
		double normalizedSweep = sweepRadians;
		while (normalizedSweep < 0.0)
		{
			normalizedSweep += fullTurn;
		}
		while (normalizedSweep > fullTurn)
		{
			normalizedSweep -= fullTurn;
		}
		return normalizedSweep;
	}
}
