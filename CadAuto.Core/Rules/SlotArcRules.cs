using System;

namespace CadAuto.Core.Rules;

/// <summary>
/// Half-arc detection shared by slot recognition on both the production and the
/// test recognizer. Previously each recognizer decided "is this a slot end arc"
/// with a different criterion: swept angle for real Arc entities, chord-versus-
/// diameter for outline arcs. The chord form silently widened the accept band as
/// the radius shrank, so the same physical arc could be a slot end on one path
/// and not on the other.
/// </summary>
public static class SlotArcRules
{
	/// <summary>Swept angle of a bulge-encoded arc: bulge = tan(sweep / 4).</summary>
	public static double SweepFromBulge(double bulge)
	{
		return 4.0 * Math.Atan(Math.Abs(bulge));
	}

	/// <summary>
	/// True when <paramref name="sweepRadians"/> is a half turn within
	/// <see cref="DimensionRuleConfig.HalfArcSweepToleranceDegrees"/>.
	/// </summary>
	public static bool IsHalfSweep(double sweepRadians, DimensionRuleConfig config)
	{
		double degrees = (config == null) ? 0.0 : config.HalfArcSweepToleranceDegrees;
		if (degrees <= 0.0)
		{
			return false;
		}
		return Math.Abs(sweepRadians - Math.PI) <= degrees * Math.PI / 180.0;
	}
}
