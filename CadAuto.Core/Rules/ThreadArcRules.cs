using System;

namespace CadAuto.Core.Rules;

/// <summary>
/// Thread-arc geometry predicates shared by the production recognizer and the
/// selection collector. Both used to carry private copies of this test, which
/// meant the same arc could be classified differently by hole-source collection
/// and by thread minor-circle suppression.
/// </summary>
public static class ThreadArcRules
{
	private const double ThreeQuarterTurnRadians = 3.0 * Math.PI / 2.0;

	/// <summary>
	/// A thread is drawn as a 3/4 circle. <paramref name="sweepRadians"/> must already be
	/// normalized to [0, 2*PI]. The tolerance is an ANGLE
	/// (<see cref="DimensionRuleConfig.ThreadArcAngleToleranceDegrees"/>) - the earlier
	/// implementations subtracted the LENGTH tolerance GeometryTolerance (0.001 mm) from a
	/// radian value, which silently demanded a sweep of at least 269.943 degrees.
	/// </summary>
	public static bool IsThreadSweep(double sweepRadians, DimensionRuleConfig config)
	{
		double toleranceRadians = GetToleranceRadians(config);
		return sweepRadians >= ThreeQuarterTurnRadians - toleranceRadians;
	}

	private static double GetToleranceRadians(DimensionRuleConfig config)
	{
		double degrees = (config == null) ? 0.0 : config.ThreadArcAngleToleranceDegrees;
		if (degrees <= 0.0)
		{
			return 0.0;
		}
		return degrees * Math.PI / 180.0;
	}
}
