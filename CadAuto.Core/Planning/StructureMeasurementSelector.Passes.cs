using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Model;

namespace CadAuto.Core.Planning;

/// <summary>
/// Ordered, behavior-preserving selection passes for <see cref="StructureMeasurementSelector"/>.
/// The method order is intentionally explicit: later passes may revive or suppress features
/// changed by earlier passes.
/// </summary>
public sealed partial class StructureMeasurementSelector
{
	private static void ResetSelection(List<StructureFeature> list)
	{
		// Reset keep flags
		foreach (StructureFeature f in list)
		{
			f.Keep = true;
			f.SuppressReason = null;
		}
	}

	private static void SuppressNoiseAndLedgeResiduals(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double tol)
	{
		// R6: noise tips / micro shelf (F338 right-arm 33).
		// G1: outer-envelope short steps (foot tip 20) are RealStep, not noise — keep them.
		double noiseFloor = Math.Max(outline.Width, outline.Height) * 0.12;
		foreach (StructureFeature f in list.Where(f => f.Kind == StructureFeatureKind.NoiseTip
			|| (f.Kind != StructureFeatureKind.Overall && f.Span + tol < noiseFloor
				&& f.Kind != StructureFeatureKind.NotchOpening
				&& f.IsInset
				&& !IsOuterTip(f))).ToList())
		{
			f.Keep = false;
			f.SuppressReason = "NoiseTip";
			if (f.Kind != StructureFeatureKind.NoiseTip)
			{
				f.Kind = StructureFeatureKind.NoiseTip;
			}
		}
		// Ledge residuals that restate overall complement (F338 265 = overall−73) are body noise.
		// Do NOT use "closes with any foot" here — F215 arm 42 + platform riser 58 also sum to
		// overall height, but 42 is a required golden structure dimension.
		foreach (StructureFeature f in list.ToList())
		{
			if (!f.Keep || f.SourceKey == null
				|| !f.SourceKey.StartsWith("LedgeResidual", StringComparison.Ordinal))
			{
				continue;
			}
			double overallOnAxis = f.Axis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
			bool largeBody = f.Span + tol >= Math.Max(outline.Width, outline.Height) * 0.5
				|| f.Span + tol >= overallOnAxis * 0.55;
			if (largeBody && !IsThinStepRiserComplement(f, list, outline, tol))
			{
				f.Keep = false;
				f.SuppressReason = "LedgeResidualBody";
				f.Kind = StructureFeatureKind.BodyRemainder;
			}
		}
	}

	private void ApplyPerAxisChainSelection(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double microGap,
		double tol,
		HashSet<StructureFeature> balancedSteps)
	{
		// R3/R4: per-axis chain selection
		foreach (StructureFeatureAxis axis in new[] { StructureFeatureAxis.Horizontal, StructureFeatureAxis.Vertical })
		{
			ApplyChainRules(list, outline, axis, microGap, tol, balancedSteps);
		}
	}

	private static void EnsureSingleOverallPerAxis(List<StructureFeature> list)
	{
		// R1 overall uniqueness
		foreach (StructureFeatureAxis axis in new[] { StructureFeatureAxis.Horizontal, StructureFeatureAxis.Vertical })
		{
			List<StructureFeature> overalls = list
				.Where(f => f.Axis == axis && f.Kind == StructureFeatureKind.Overall)
				.OrderByDescending(f => f.Confidence)
				.ToList();
			for (int i = 0; i < overalls.Count; i++)
			{
				overalls[i].Keep = i == 0;
				if (i > 0)
				{
					overalls[i].SuppressReason = "DuplicateOverall";
				}
			}
		}
	}

	private static void SuppressInsetMeasurements(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double microGap,
		double tol,
		HashSet<StructureFeature> balancedSteps)
	{
		// Drop inset platform risers / connectors (axis-independent — must survive 90°).
		// Never drop NotchOpening / StepGroove 槽宽 (geometry span, not a fixed 87.55/92.55).
		foreach (StructureFeature f in list.Where(f =>
			f.Keep
			&& f.Kind != StructureFeatureKind.Overall
			&& f.Kind != StructureFeatureKind.NotchOpening
			&& f.IsInset).ToList())
		{
			if (IsStepGroove(f) || IsInnerBoss(f) || IsFootLedge(f) || IsStepRiser(f)
				|| f.PreferLocalPlacement
				|| balancedSteps.Contains(f))
			{
				continue;
			}
			bool onEnvelopeCross = f.Axis == StructureFeatureAxis.Vertical
				? (Math.Abs(f.CrossPosition - outline.MinX) <= tol
					|| Math.Abs(f.CrossPosition - outline.MaxX) <= tol)
				: (Math.Abs(f.CrossPosition - outline.MinY) <= tol
					|| Math.Abs(f.CrossPosition - outline.MaxY) <= tol);
			if (onEnvelopeCross)
			{
				continue;
			}
			double overallOnAxis = f.Axis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
			double shortSide = Math.Min(outline.Width, outline.Height);
			// Partial walls from envelope end (F215 pr=58, F338 riser=50): drop short inset.
			// Fully interior connectors (riser-body ~50): drop only clearly short risers.
			// Multi-level treads / 槽宽 floors must NOT be over-suppressed.
			bool partialFromEnd = f.TouchesOverallMin || f.TouchesOverallMax;
			double filletGap = Math.Max(microGap, Math.Max(12.0, shortSide * 0.08));
			if (!partialFromEnd
				&& IsDistantAbutToOuterTip(f, list, outline, filletGap, tol))
			{
				f.Keep = false;
				f.SuppressReason = "InteriorRiser";
				continue;
			}
			bool multiLevelTread = f.Axis == StructureFeatureAxis.Horizontal
				&& !partialFromEnd
				&& f.Span + tol >= Math.Max(overallOnAxis * 0.18, shortSide * 0.30)
				&& f.Span + tol < overallOnAxis * 0.50;
			if (multiLevelTread)
			{
				continue; // keep mid-step shelves / 槽宽
			}
			bool drop = partialFromEnd
				? f.Span + tol < overallOnAxis * 0.65
				: f.Span + tol < shortSide * 0.28;
			if (drop)
			{
				f.Keep = false;
				f.SuppressReason = "InteriorRiser";
			}
		}

		// Near-overall inset body (CAD filleted interior wall ~171.5 vs overall 201.5).
		// Outer-envelope long arms (F338 305) stay; only inset near-overall is dropped.
		// Axis-independent so 0°/180° height bodies and 90°/270° width bodies normalize.
		foreach (StructureFeature f in list.Where(f =>
			f.Keep
			&& f.IsInset
			&& f.Kind != StructureFeatureKind.Overall
			&& f.Kind != StructureFeatureKind.NotchOpening
			&& !IsStepGroove(f)).ToList())
		{
			double overallOnAxis = f.Axis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
			if (overallOnAxis <= tol)
			{
				continue;
			}
			if (f.Span + tol >= overallOnAxis * 0.80)
			{
				f.Keep = false;
				f.SuppressReason = "NearOverallInsetBody";
			}
		}
	}

	private static void RestoreProtectedMeasurements(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double tol,
		HashSet<StructureFeature> balancedSteps)
	{
		// Force-keep 槽宽 (StepGroove / multi-level floor) and outer tips on every orientation.
		foreach (StructureFeature f in list.Where(f =>
			IsStepGrooveOrMidShelf(f) || IsOuterTip(f) || IsInnerBoss(f) || IsFootLedge(f)
			|| IsStepRiser(f)
			|| IsThinStepRiserComplement(f, list, outline, tol)
			|| IsMaxEnvelopePartialStep(f, outline, tol)
			|| f.PreferLocalPlacement
			|| balancedSteps.Contains(f)))
		{
			f.Keep = true;
			f.SuppressReason = null;
			if (f.Kind == StructureFeatureKind.BodyRemainder || f.Kind == StructureFeatureKind.NoiseTip)
			{
				f.Kind = StructureFeatureKind.RealStep;
			}
		}
	}

	private static void ApplyOpenChainAndNestedRules(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double tol,
		HashSet<StructureFeature> balancedSteps)
	{
		// Incomplete notch band: drop co-side real steps that are neither the notch
		// nor an overall-end piece (F215 top/rotated 80 next to 50+75). Run AFTER
		// InteriorRiser so the notch is still present as Keep when we evaluate.
		ApplyOpenChainBodyFragmentDrop(list, tol, balancedSteps);

		// Nested residual: if two vertical features share PreferredSide and one interval
		// strictly contains the other, keep the outer residual (F215 42 over 32).
		ApplyNestedIntervalPreference(list, StructureFeatureAxis.Vertical, outline, tol, balancedSteps);
		ApplyNestedIntervalPreference(list, StructureFeatureAxis.Horizontal, outline, tol, balancedSteps);
	}

	private static void RemoveDuplicateMeasurements(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double tol,
		HashSet<StructureFeature> balancedSteps)
	{
		// R7 same interval
		foreach (var g in list.Where(f => f.Keep && f.Kind != StructureFeatureKind.Overall)
			.GroupBy(f => IntervalKey(f, tol)))
		{
			List<StructureFeature> members = g.OrderByDescending(f => f.Confidence).ThenByDescending(f => f.Span).ToList();
			for (int i = 1; i < members.Count; i++)
			{
				if (IsMaxEnvelopePartialStep(members[i], outline, tol)
					&& Math.Abs(members[i].CrossPosition - members[0].CrossPosition) > Math.Max(tol * 8.0, 1.0))
				{
					continue;
				}
				members[i].Keep = false;
				members[i].SuppressReason = "SameIntervalDuplicate";
			}
		}

		// Near-duplicate spans on same axis: keep envelope-backed higher confidence
		// (F338: prefer right foot 100 over left ledge residual 101.5).
		foreach (StructureFeatureAxis axis in new[] { StructureFeatureAxis.Horizontal, StructureFeatureAxis.Vertical })
		{
			List<StructureFeature> kept = list
				.Where(f => f.Keep && f.Axis == axis && f.Kind != StructureFeatureKind.Overall)
				.OrderByDescending(f => EnvelopeScore(f, outline, tol))
				.ThenByDescending(f => f.Confidence)
				.ThenBy(f => f.Span)
				.ToList();
			for (int i = 0; i < kept.Count; i++)
			{
				if (!kept[i].Keep)
				{
					continue;
				}
				for (int j = i + 1; j < kept.Count; j++)
				{
					if (!kept[j].Keep)
					{
						continue;
					}
					if ((balancedSteps.Contains(kept[i]) && balancedSteps.Contains(kept[j]))
						|| IsInnerBoss(kept[i]) || IsInnerBoss(kept[j])
						|| kept[i].PreferLocalPlacement || kept[j].PreferLocalPlacement
						|| IsMaxEnvelopePartialStep(kept[j], outline, tol))
					{
						continue;
					}
					double spanTol = Math.Max(tol * 20, Math.Max(kept[i].Span, kept[j].Span) * 0.03);
					if (Math.Abs(kept[i].Span - kept[j].Span) <= spanTol)
					{
						kept[j].Keep = false;
						kept[j].SuppressReason = "NearDuplicateSpan";
					}
				}
			}
		}
	}

	private void FinalizeBodyRemainders(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double microGap,
		double tol,
		HashSet<StructureFeature> balancedSteps)
	{
		// BodyRemainder off (after protect long arm — never revive LedgeResidual bodies)
		foreach (StructureFeature f in list.Where(f => f.Kind == StructureFeatureKind.BodyRemainder))
		{
			bool isLedgeResidual = f.SourceKey != null
				&& f.SourceKey.StartsWith("LedgeResidual", StringComparison.Ordinal);
			if (!isLedgeResidual && IsProtectedLongArm(f, list, outline, tol))
			{
				f.Kind = StructureFeatureKind.RealStep;
				f.Keep = true;
				f.SuppressReason = null;
				continue;
			}
			f.Keep = false;
			f.SuppressReason = f.SuppressReason ?? "BodyRemainder";
		}

		// Drop complementary body that only restates overall − real step (265+73=338).
		// Prefer the shorter real step; never drop 305-class long arms.
		DropComplementaryLongerBody(list, outline, microGap, tol, balancedSteps);
	}

	private static void RestoreStairTreadsAndOpenRings(
		List<StructureFeature> list,
		HashSet<StructureFeature> balancedSteps,
		HashSet<StructureFeature> stairOpenRings)
	{
		// Revive kept stair treads after later passes (Nested / Complementary).
		foreach (StructureFeature f in list.Where(f => balancedSteps.Contains(f)))
		{
			f.Keep = true;
			f.SuppressReason = null;
			if (f.Kind == StructureFeatureKind.BodyRemainder || f.Kind == StructureFeatureKind.NoiseTip)
			{
				f.Kind = StructureFeatureKind.RealStep;
			}
		}

		// Open the dimension chain: all treads + overall would be a closed loop
		// (Stair3 169+134+134=438 / 163+126+140=430). Drop exactly one ring member.
		foreach (StructureFeature f in list.Where(f => stairOpenRings.Contains(f)))
		{
			f.Keep = false;
			f.SuppressReason = "StairClosedChainOpenRing";
		}
	}

	private static void ApplyEnvelopeEndAndCoveringRules(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double microGap,
		double tol)
	{
		// RF110: keep 53 (overall min → inner top ledge) and drop thin overall-end
		// risers 2/3. 50 stays; 50+53+55 must not eat each other.
		ReplaceOverallEndStepRisersWithComplement(list, outline, tol);

		// n≥2 same-half abutting structure intervals that cover overall: drop the
		// max-end LedgeResidual first (keep at least one residual as location).
		OpenCoveringLedgeResidualChains(list, outline, microGap, tol);
	}
}
