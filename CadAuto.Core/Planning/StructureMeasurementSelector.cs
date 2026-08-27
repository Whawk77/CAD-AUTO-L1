using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning;

/// <summary>
/// Axis-independent keep/drop rules (R1–R7). Does not use WCS side as a decision key.
/// </summary>
public sealed partial class StructureMeasurementSelector
{
	private readonly DimensionRuleConfig _config;

	public StructureMeasurementSelector(DimensionRuleConfig config)
	{
		_config = config ?? DimensionRuleConfig.CreateDefault();
	}

	public IList<StructureFeature> Select(IList<StructureFeature> features, OutlineFeature2D outline)
	{
		if (features == null || features.Count == 0 || outline == null)
		{
			return new List<StructureFeature>();
		}
		double tol = _config.GeometryTolerance;
		double microGap = Math.Max(5.0, Math.Max(outline.Width, outline.Height) * 0.025);
		List<StructureFeature> list = features.ToList();
		HashSet<StructureFeature> balancedSteps = CollectBalancedStepChains(list, outline, microGap, tol);
		HashSet<StructureFeature> stairOpenRings = TakeStairOpenRings(balancedSteps, outline, microGap, tol);

		ResetSelection(list);
		SuppressNoiseAndLedgeResiduals(list, outline, tol);
		ApplyPerAxisChainSelection(list, outline, microGap, tol, balancedSteps);
		EnsureSingleOverallPerAxis(list);
		SuppressInsetMeasurements(list, outline, microGap, tol, balancedSteps);
		RestoreProtectedMeasurements(list, outline, tol, balancedSteps);
		ApplyOpenChainAndNestedRules(list, outline, tol, balancedSteps);
		RemoveDuplicateMeasurements(list, outline, tol, balancedSteps);
		FinalizeBodyRemainders(list, outline, microGap, tol, balancedSteps);
		RestoreStairTreadsAndOpenRings(list, balancedSteps, stairOpenRings);
		ApplyEnvelopeEndAndCoveringRules(list, outline, microGap, tol);

		return list.Where(f => f.Keep).ToList();
	}

	private static bool IsStepGroove(StructureFeature f)
	{
		return f != null
			&& f.SourceKey != null
			&& f.SourceKey.StartsWith("StepGroove", StringComparison.Ordinal);
	}

	private static bool IsOuterTip(StructureFeature f)
	{
		return f != null
			&& f.SourceKey != null
			&& f.SourceKey.StartsWith("OuterTip", StringComparison.Ordinal);
	}

	private static bool IsInnerBoss(StructureFeature f)
	{
		return f != null
			&& f.SourceKey != null
			&& f.SourceKey.StartsWith("InnerBoss", StringComparison.Ordinal);
	}

	private static bool IsFootLedge(StructureFeature f)
	{
		return f != null
			&& f.SourceKey != null
			&& f.SourceKey.StartsWith("FootLedge", StringComparison.Ordinal);
	}

	private static bool IsStepRiser(StructureFeature f)
	{
		return f != null
			&& f.SourceKey != null
			&& f.SourceKey.StartsWith("StepRiser", StringComparison.Ordinal);
	}

	private static bool IsDistantAbutToOuterTip(
		StructureFeature f,
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double microGap,
		double tol)
	{
		if (f == null || list == null || outline == null)
		{
			return false;
		}
		double perp = f.Axis == StructureFeatureAxis.Horizontal ? outline.Height : outline.Width;
		double a0 = Math.Min(f.T0, f.T1);
		double a1 = Math.Max(f.T0, f.T1);
		foreach (StructureFeature tip in list)
		{
			if (!IsOuterTip(tip) || tip.Axis != f.Axis)
			{
				continue;
			}
			double b0 = Math.Min(tip.T0, tip.T1);
			double b1 = Math.Max(tip.T0, tip.T1);
			bool abut = Math.Abs(a0 - b1) <= microGap + tol || Math.Abs(b0 - a1) <= microGap + tol;
			if (!abut)
			{
				continue;
			}
			if (Math.Abs(f.CrossPosition - tip.CrossPosition) > perp * 0.50)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// n≥3 abutting real treads that cover overall, each in [12%, 45%), and whose
	/// CrossPosition is strictly monotonic (a staircase). Long LedgeResidual bodies
	/// (Stair3 304 / 266) must not join the chain — they would trip the 45% cap
	/// and the stair would be treated as a closed body+tips (F338 177).
	/// Axis-only abut, no side/half split, so 0°/90° keep the same physical edges.
	/// </summary>
	private static HashSet<StructureFeature> CollectBalancedStepChains(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double microGap,
		double tol)
	{
		HashSet<StructureFeature> result = new HashSet<StructureFeature>();
		if (list == null || outline == null)
		{
			return result;
		}
		foreach (StructureFeatureAxis axis in new[] { StructureFeatureAxis.Horizontal, StructureFeatureAxis.Vertical })
		{
			double overallMin = axis == StructureFeatureAxis.Horizontal ? outline.MinX : outline.MinY;
			double overallMax = axis == StructureFeatureAxis.Horizontal ? outline.MaxX : outline.MaxY;
			double overall = overallMax - overallMin;
			if (overall <= tol)
			{
				continue;
			}
			double perp = axis == StructureFeatureAxis.Horizontal ? outline.Height : outline.Width;
			double minRise = Math.Max(12.0, perp * 0.08);
			List<StructureFeature> members = list
				.Where(f => f.Axis == axis
					&& f.Kind != StructureFeatureKind.Overall
					&& !IsLedgeResidual(f)
					&& !IsStepGroove(f)
					&& f.Span + tol >= overall * 0.12
					&& f.Span + tol < overall * 0.45)
				.OrderBy(f => Math.Min(f.T0, f.T1))
				.ToList();
			foreach (List<StructureFeature> chain in BuildChains(members, microGap, tol))
			{
				if (chain.Count < 3)
				{
					continue;
				}
				double c0 = chain.Min(f => Math.Min(f.T0, f.T1));
				double c1 = chain.Max(f => Math.Max(f.T0, f.T1));
				bool covers = Math.Abs(c0 - overallMin) <= microGap + tol
					&& Math.Abs(c1 - overallMax) <= microGap + tol;
				if (!covers)
				{
					continue;
				}
				if (!IsMonotonicStair(chain, minRise, tol))
				{
					continue;
				}
				foreach (StructureFeature f in chain)
				{
					result.Add(f);
				}
			}
		}
		return result;
	}

	private static bool IsLedgeResidual(StructureFeature f)
	{
		return f != null
			&& f.SourceKey != null
			&& f.SourceKey.StartsWith("LedgeResidual", StringComparison.Ordinal);
	}

	/// <summary>
	/// Partial outer-envelope edge on the max cross (horizontal → MaxY / Top,
	/// vertical → MaxX / Right). RF110 top pad 50 and C-plate top body 120.
	/// Not inset, not overall. Does not require an inner complement pair.
	/// </summary>
	private static bool IsMaxEnvelopePartialStep(
		StructureFeature f,
		OutlineFeature2D outline,
		double tol)
	{
		if (f == null || outline == null || f.Kind == StructureFeatureKind.Overall || f.IsInset
			|| IsLedgeResidual(f) || IsOuterTip(f))
		{
			return false;
		}
		double overall = f.Axis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
		if (overall <= tol || f.Span + tol >= overall * 0.80 || f.Span + tol < overall * 0.45)
		{
			return false;
		}
		if (!f.TouchesOverallMax || f.TouchesOverallMin)
		{
			return false;
		}
		double band = Math.Max(tol * 8.0, 1.0);
		if (f.Axis == StructureFeatureAxis.Horizontal)
		{
			if (Math.Abs(f.CrossPosition - outline.MaxY) > band)
			{
				return false;
			}
			return !MaxEnvelopeEdgeIsFullLength(outline, horizontal: true, outline.MaxY, overall, band, tol);
		}
		if (Math.Abs(f.CrossPosition - outline.MaxX) > band)
		{
			return false;
		}
		return !MaxEnvelopeEdgeIsFullLength(outline, horizontal: false, outline.MaxX, overall, band, tol);
	}

	private static bool MaxEnvelopeEdgeIsFullLength(
		OutlineFeature2D outline,
		bool horizontal,
		double edge,
		double overall,
		double band,
		double tol)
	{
		if (outline?.Segments == null || overall <= tol)
		{
			return false;
		}
		double t0 = double.PositiveInfinity;
		double t1 = double.NegativeInfinity;
		foreach (Segment2D s in outline.Segments)
		{
			if (s == null || s.IsArcChord)
			{
				continue;
			}
			if (horizontal)
			{
				if (!s.IsHorizontal(tol))
				{
					continue;
				}
				double y = (s.Start.Y + s.End.Y) * 0.5;
				if (Math.Abs(y - edge) > band)
				{
					continue;
				}
				t0 = Math.Min(t0, Math.Min(s.Start.X, s.End.X));
				t1 = Math.Max(t1, Math.Max(s.Start.X, s.End.X));
			}
			else
			{
				if (!s.IsVertical(tol))
				{
					continue;
				}
				double x = (s.Start.X + s.End.X) * 0.5;
				if (Math.Abs(x - edge) > band)
				{
					continue;
				}
				t0 = Math.Min(t0, Math.Min(s.Start.Y, s.End.Y));
				t1 = Math.Max(t1, Math.Max(s.Start.Y, s.End.Y));
			}
		}
		return t1 - t0 + tol >= overall;
	}

	/// <summary>
	/// Ledge residual whose leftover to overall is a thin StepRiser (RF110 53 = 55−2).
	/// Keep it instead of treating it as overall-complement body noise.
	/// </summary>
	private static bool IsThinStepRiserComplement(
		StructureFeature f,
		IList<StructureFeature> list,
		OutlineFeature2D outline,
		double tol)
	{
		if (!IsLedgeResidual(f) || list == null || outline == null)
		{
			return false;
		}
		double overall = f.Axis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
		double leftover = overall - f.Span;
		double maxRiser = Math.Min(outline.Width, outline.Height) * 0.12;
		if (leftover + tol < 1.0 || leftover > maxRiser + tol)
		{
			return false;
		}
		double spanTol = Math.Max(tol * 10, 0.05);
		return list.Any(r => IsStepRiser(r)
			&& r.Axis == f.Axis
			&& Math.Abs(r.Span - leftover) <= spanTol);
	}

	private static void ReplaceOverallEndStepRisersWithComplement(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double tol)
	{
		if (list == null || outline == null)
		{
			return;
		}
		foreach (StructureFeatureAxis axis in new[] { StructureFeatureAxis.Horizontal, StructureFeatureAxis.Vertical })
		{
			if (!list.Any(f => f.Keep && f.Axis == axis && IsThinStepRiserComplement(f, list, outline, tol)))
			{
				continue;
			}
			foreach (StructureFeature riser in list.Where(f =>
				f.Keep
				&& f.Axis == axis
				&& IsStepRiser(f)
				&& (f.TouchesOverallMin || f.TouchesOverallMax)))
			{
				riser.Keep = false;
				riser.SuppressReason = "ReplacedByStepRiserComplement";
			}
		}
	}

	/// <summary>
	/// A stair that covers overall must not also keep every tread — that plus Overall
	/// is a closed dimension chain. Pull one end out of the keep-set: prefer a
	/// unique-span inset end (Stair3 169.21; V 140.44) so the envelope locating
	/// end stays (163.37). Else the longer overall-end. Rotation-stable.
	/// </summary>
	private static HashSet<StructureFeature> TakeStairOpenRings(
		HashSet<StructureFeature> balancedSteps,
		OutlineFeature2D outline,
		double microGap,
		double tol)
	{
		HashSet<StructureFeature> rings = new HashSet<StructureFeature>();
		if (balancedSteps == null || balancedSteps.Count == 0 || outline == null)
		{
			return rings;
		}
		foreach (StructureFeatureAxis axis in new[] { StructureFeatureAxis.Horizontal, StructureFeatureAxis.Vertical })
		{
			List<StructureFeature> members = balancedSteps
				.Where(f => f.Axis == axis)
				.OrderBy(f => Math.Min(f.T0, f.T1))
				.ToList();
			foreach (List<StructureFeature> chain in BuildChains(members, microGap, tol))
			{
				if (chain.Count < 3)
				{
					continue;
				}
				StructureFeature ring = ChooseStairOpenRing(chain, tol);
				if (ring == null)
				{
					continue;
				}
				rings.Add(ring);
				balancedSteps.Remove(ring);
			}
		}
		return rings;
	}

	private static StructureFeature ChooseStairOpenRing(List<StructureFeature> chain, double tol)
	{
		if (chain == null || chain.Count < 3)
		{
			return null;
		}
		List<StructureFeature> ends = chain
			.Where(f => f.TouchesOverallMin || f.TouchesOverallMax)
			.ToList();
		if (ends.Count == 0)
		{
			return chain.OrderByDescending(f => f.Span).First();
		}
		double spanTol = Math.Max(tol, 0.05);
		List<StructureFeature> uniqueEnds = ends
			.Where(end => chain.Count(f => Math.Abs(f.Span - end.Span) <= spanTol) == 1)
			.ToList();
		if (uniqueEnds.Count > 0)
		{
			// Keep envelope locating end (163.37); open the inset opposite end (140.44).
			return uniqueEnds
				.OrderByDescending(f => f.IsInset)
				.ThenByDescending(f => f.Span)
				.First();
		}
		return ends
			.OrderByDescending(f => f.IsInset)
			.ThenByDescending(f => f.Span)
			.First();
	}

	/// <summary>
	/// True when CrossPosition steps the same way along T (up-stair or down-stair).
	/// A notch (F215 90→50→75) goes down then up and is not a staircase.
	/// </summary>
	private static bool IsMonotonicStair(List<StructureFeature> chain, double minRise, double tol)
	{
		if (chain == null || chain.Count < 3)
		{
			return false;
		}
		List<StructureFeature> ordered = chain.OrderBy(f => Math.Min(f.T0, f.T1)).ToList();
		int sign = 0;
		for (int i = 0; i < ordered.Count - 1; i++)
		{
			double delta = ordered[i + 1].CrossPosition - ordered[i].CrossPosition;
			if (Math.Abs(delta) < minRise - tol)
			{
				return false;
			}
			int s = delta > 0.0 ? 1 : -1;
			if (sign == 0)
			{
				sign = s;
			}
			else if (s != sign)
			{
				return false;
			}
		}
		return sign != 0;
	}

	private static bool InteriorsAreFragmentsOf(
		StructureFeature end,
		List<StructureFeature> chain,
		double tol)
	{
		if (end == null || chain == null)
		{
			return false;
		}
		double e0 = Math.Min(end.T0, end.T1);
		double e1 = Math.Max(end.T0, end.T1);
		List<StructureFeature> interiors = chain
			.Where(f => f != end && !f.TouchesOverallMin && !f.TouchesOverallMax)
			.ToList();
		if (interiors.Count == 0)
		{
			return false;
		}
		return interiors.All(f =>
			e0 <= Math.Min(f.T0, f.T1) + tol
			&& e1 >= Math.Max(f.T0, f.T1) - tol
			&& end.Span > f.Span + tol);
	}

	/// <summary>
	/// 槽宽: explicit StepGroove, or inset mid-shelf not touching overall ends
	/// with span in the multi-level tread band (geometric, not fixed 87.55/92.55).
	/// </summary>
	private static bool IsStepGrooveOrMidShelf(StructureFeature f)
	{
		if (f == null || f.Kind == StructureFeatureKind.Overall)
		{
			return false;
		}
		return IsStepGroove(f);
	}

	/// <summary>
	/// When two co-axis structure spans nearly partition overall, drop the longer body
	/// if the shorter is a real step (≥12% overall) and the longer is not a long arm (≥85%).
	/// </summary>
	private static void DropComplementaryLongerBody(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double microGap,
		double tol,
		HashSet<StructureFeature> balancedSteps)
	{
		foreach (StructureFeatureAxis axis in new[] { StructureFeatureAxis.Horizontal, StructureFeatureAxis.Vertical })
		{
			double overall = axis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
			List<StructureFeature> kept = list
				.Where(f => f.Keep && f.Axis == axis && f.Kind != StructureFeatureKind.Overall)
				.ToList();
			for (int i = 0; i < kept.Count; i++)
			{
				for (int j = i + 1; j < kept.Count; j++)
				{
					StructureFeature a = kept[i];
					StructureFeature b = kept[j];
					double sum = a.Span + b.Span;
					if (Math.Abs(sum - overall) > microGap + tol)
					{
						continue;
					}
					StructureFeature longer = a.Span >= b.Span ? a : b;
					StructureFeature shorter = a.Span >= b.Span ? b : a;
					if (balancedSteps != null && (balancedSteps.Contains(a) || balancedSteps.Contains(b)))
					{
						continue;
					}
					if (IsThinStepRiserComplement(a, list, outline, tol)
						|| IsThinStepRiserComplement(b, list, outline, tol))
					{
						continue;
					}
					bool shortTip = IsOuterTip(shorter);
					if (!shortTip && shorter.Span + tol < overall * 0.12)
					{
						continue;
					}
					// Long arm + leftover ear (305+33): keep long arm.
					// OuterTip + opposite complement that equals overall (25+232=257) drops the arm.
					double residual = overall - longer.Span;
					if (!shortTip
						&& longer.Span + tol >= overall * 0.85
						&& residual + tol >= 12.0
						&& residual <= overall * 0.12 + tol)
					{
						continue;
					}
					longer.Keep = false;
					longer.SuppressReason = "ComplementaryLongerBody";
					longer.Kind = StructureFeatureKind.BodyRemainder;
				}
			}
		}
	}

	private void ApplyChainRules(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		StructureFeatureAxis axis,
		double microGap,
		double tol,
		HashSet<StructureFeature> balancedSteps)
	{
		double overallMin = axis == StructureFeatureAxis.Horizontal ? outline.MinX : outline.MinY;
		double overallMax = axis == StructureFeatureAxis.Horizontal ? outline.MaxX : outline.MaxY;
		double overall = overallMax - overallMin;

		// Group by PreferredSide so top/bottom multi-level chains stay separate.
		// Within a side, multi-level ledges (different CrossPosition) still form one X-chain.
		var members = list.Where(f => f.Axis == axis && f.Kind != StructureFeatureKind.Overall && f.Keep).ToList();
		List<List<StructureFeature>> chainSets = members
			.GroupBy(f => ChainGroupKey(f, outline, axis, microGap, tol))
			.Select(g => g.OrderBy(f => Math.Min(f.T0, f.T1)).ToList())
			.SelectMany(ordered => axis == StructureFeatureAxis.Horizontal
				? BuildOverlapChains(ordered, microGap, tol)
				: BuildChains(ordered, microGap, tol))
			.ToList();
		foreach (List<StructureFeature> chain in chainSets)
		{
				if (chain.Count < 2)
				{
					continue;
				}
				double c0 = chain.Min(f => Math.Min(f.T0, f.T1));
				double c1 = chain.Max(f => Math.Max(f.T0, f.T1));
				bool covers = Math.Abs(c0 - overallMin) <= microGap + tol
					&& Math.Abs(c1 - overallMax) <= microGap + tol;
				if (!covers)
				{
					continue;
				}

				bool hasInterior = chain.Any(f =>
					!f.TouchesOverallMin && !f.TouchesOverallMax
					&& f.Kind != StructureFeatureKind.NoiseTip);

				List<StructureFeature> minEnds = chain.Where(f => f.TouchesOverallMin).ToList();
				List<StructureFeature> maxEnds = chain.Where(f => f.TouchesOverallMax).ToList();
				StructureFeature minEnd = minEnds.OrderByDescending(f => f.Span).FirstOrDefault();
				StructureFeature maxEnd = maxEnds.OrderByDescending(f => f.Span).FirstOrDefault();

				if (hasInterior)
				{
					// R3/R4: drop overall-closing ends longer than every interior step and ≥30% overall
					// (F338: drop 177.45 and residual 265; keep 73+87.55).
					// Prefer real contour ends over LedgeResidual synthetic ends when choosing
					// which overall-touching piece to keep (F215: keep 42 residual arm, not only 32).
					double interiorMax = chain
						.Where(f => !f.TouchesOverallMin && !f.TouchesOverallMax)
						.Select(f => f.Span)
						.DefaultIfEmpty(0.0)
						.Max();
					foreach (StructureFeature end in minEnds.Concat(maxEnds).Distinct())
					{
						if (IsOuterTip(end) || (balancedSteps != null && balancedSteps.Contains(end)))
						{
							continue;
						}
						// F215 42 residual contains the C10 stub 32 — 32 is not a real interior step.
						if (InteriorsAreFragmentsOf(end, chain, tol))
						{
							continue;
						}
						if (end.Span > interiorMax + tol && end.Span + tol >= overall * 0.30)
						{
							// Keep short location end (75/73/42) even if it touches overall.
							StructureFeature opposite = end.TouchesOverallMin ? maxEnd : minEnd;
							if (opposite != null && end.Span <= opposite.Span + tol)
							{
								continue;
							}
							// Also keep the shorter side of a two-end residual pair when this end
							// is a LedgeResidual arm shorter than the opposite body.
							if (opposite != null
								&& end.SourceKey != null
								&& end.SourceKey.StartsWith("LedgeResidual", StringComparison.Ordinal)
								&& end.Span + tol < opposite.Span)
							{
								continue;
							}
							end.Kind = StructureFeatureKind.BodyRemainder;
							end.Keep = false;
							end.SuppressReason = "ClosedChainLongerEnd";
						}
					}
				}
				else if (minEnd != null && maxEnd != null && minEnd != maxEnd)
				{
					// Non-collinear pair (RF110 top 50 @ MaxY vs inner 52.5 @ MaxY-2):
					// not a split of one edge. Keep the outer-envelope end, drop the inner.
					if (!AreCollinearFeatures(minEnd, maxEnd, tol)
						&& TryKeepMaxEnvelopeStepDropInner(minEnds, maxEnds, axis, outline, tol))
					{
						continue;
					}
					// Two-end only (no interior): keep longer foot, drop ALL shorter-end pieces
					// (F215 bottom 120 over 95 / platform 95). Never drop envelope tip 20.
					if (minEnd.Span > maxEnd.Span + tol)
					{
						foreach (StructureFeature e in maxEnds)
						{
							if (IsOuterTip(e))
							{
								continue;
							}
							e.Keep = false;
							e.SuppressReason = "ShorterComplementEnd";
							e.Kind = StructureFeatureKind.BodyRemainder;
						}
						foreach (StructureFeature e in minEnds)
						{
							e.Keep = true;
							e.SuppressReason = null;
						}
					}
					else if (maxEnd.Span > minEnd.Span + tol)
					{
						foreach (StructureFeature e in minEnds)
						{
							if (IsOuterTip(e))
							{
								continue;
							}
							e.Keep = false;
							e.SuppressReason = "ShorterComplementEnd";
							e.Kind = StructureFeatureKind.BodyRemainder;
						}
						foreach (StructureFeature e in maxEnds)
						{
							e.Keep = true;
							e.SuppressReason = null;
						}
					}
				}
		}

	}

	/// <summary>
	/// Incomplete notch band: drop co-side real steps that are neither the notch
	/// nor an overall-end piece (F215 top 80 / rotated left 80 next to 50+75).
	/// Groups by PreferredSide and also by envelope half-band so rotation is stable.
	/// </summary>
	private static void ApplyOpenChainBodyFragmentDrop(
		List<StructureFeature> list,
		double tol,
		HashSet<StructureFeature> balancedSteps)
	{
		foreach (StructureFeatureAxis ax in new[] { StructureFeatureAxis.Horizontal, StructureFeatureAxis.Vertical })
		{
			foreach (var band in list.Where(f => f.Keep && f.Axis == ax && f.Kind != StructureFeatureKind.Overall)
				.GroupBy(f => f.PreferredSide))
			{
				DropOpenChainBodyFragments(band.ToList(), balancedSteps);
			}
		}
	}

	private static void DropOpenChainBodyFragments(
		List<StructureFeature> sideFeatures,
		HashSet<StructureFeature> balancedSteps)
	{
		if (!sideFeatures.Any(f => f.Kind == StructureFeatureKind.NotchOpening))
		{
			return;
		}
		bool hasEnd = sideFeatures.Any(f => f.TouchesOverallMin || f.TouchesOverallMax);
		if (!hasEnd)
		{
			return;
		}
		foreach (StructureFeature f in sideFeatures)
		{
			if (f.Kind == StructureFeatureKind.NotchOpening)
			{
				continue;
			}
			if (f.TouchesOverallMin || f.TouchesOverallMax)
			{
				continue;
			}
			if (balancedSteps != null && balancedSteps.Contains(f))
			{
				continue;
			}
			f.Keep = false;
			f.SuppressReason = "OpenChainBodyFragment";
		}
	}

	/// <summary>
	/// Collinear = same supporting line. A 2mm step (RF110 MaxY vs MaxY-2) is not collinear;
	/// band is tighter than envelopeBand so a real riser is not glued into one edge.
	/// </summary>
	private static bool AreCollinearFeatures(StructureFeature a, StructureFeature b, double tol)
	{
		if (a == null || b == null)
		{
			return false;
		}
		double band = Math.Max(tol * 8.0, 1.0);
		return Math.Abs(a.CrossPosition - b.CrossPosition) <= band;
	}

	/// <summary>
	/// When two overall-end pieces cover overall but sit on different levels, keep the
	/// member on the max envelope of the cross axis (horizontal → MaxY / Top; vertical
	/// → MaxX / Right). Drops the inner shelf. Returns false when neither end is on
	/// that envelope, so the caller can fall back to keep-longer.
	/// </summary>
	private static bool TryKeepMaxEnvelopeStepDropInner(
		List<StructureFeature> minEnds,
		List<StructureFeature> maxEnds,
		StructureFeatureAxis axis,
		OutlineFeature2D outline,
		double tol)
	{
		if (minEnds == null || maxEnds == null || outline == null)
		{
			return false;
		}
		double outerCross = axis == StructureFeatureAxis.Horizontal ? outline.MaxY : outline.MaxX;
		double band = Math.Max(tol * 8.0, 1.0);
		List<StructureFeature> allEnds = minEnds.Concat(maxEnds).Distinct().ToList();
		List<StructureFeature> outerEnds = allEnds
			.Where(e => Math.Abs(e.CrossPosition - outerCross) <= band)
			.ToList();
		if (outerEnds.Count == 0)
		{
			return false;
		}
		foreach (StructureFeature e in allEnds)
		{
			if (outerEnds.Contains(e) || IsOuterTip(e))
			{
				e.Keep = true;
				e.SuppressReason = null;
				continue;
			}
			e.Keep = false;
			e.SuppressReason = "InnerLevelComplementEnd";
			e.Kind = StructureFeatureKind.BodyRemainder;
		}
		return true;
	}

	/// <summary>
	/// Same-half, same-axis abutting structure intervals that cover overall (n≥2)
	/// plus overall is a closed chain. Drop max-end LedgeResidual first, keeping at
	/// least one residual as location of the real step (C-notch: drop 35, keep 15+50).
	/// A lone residual that does not cover overall (F215 42) is left alone.
	/// </summary>
	private static void OpenCoveringLedgeResidualChains(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double microGap,
		double tol)
	{
		ForEachAxisChain(list, outline, microGap, tol, includeDropped: false,
			(chain, overallMin, overallMax, overall) =>
				OpenOneCoveringChain(chain, overallMin, overallMax, overall, outline, tol));
	}

	/// <summary>
	/// Overlay executor for <see cref="AnnotationStrategyIds.OpenCoveringDropResidualMax"/>.
	/// Same grouping as Select; only drops the Max-end residual on a covering chain.
	/// </summary>
	public static void ApplyOpenCoveringDropResidualMax(
		IList<StructureFeature> features,
		OutlineFeature2D outline,
		double microGap,
		double tol)
	{
		if (features == null || outline == null)
		{
			return;
		}
		ForEachAxisChain(features.ToList(), outline, microGap, tol, includeDropped: false,
			(chain, overallMin, overallMax, overall) =>
				DropCoveringMaxEndResiduals(chain, overallMin, overallMax, tol, "ClosedChainMaxEndResidual"));
	}

	/// <summary>
	/// Qualitative covering-chain schema: ResidualMin + real step + ResidualMax on one
	/// axis/half, union covering overall. Includes already-dropped members so Capture
	/// still sees the chain after Select opened it. No millimetre / decile fields.
	/// </summary>
	public static IList<string> DetectCoveringChainSchemas(
		IList<StructureFeature> features,
		OutlineFeature2D outline,
		double microGap,
		double tol)
	{
		List<string> schemas = new List<string>();
		if (features == null || outline == null)
		{
			return schemas;
		}
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		ForEachAxisChain(features.ToList(), outline, microGap, tol, includeDropped: true,
			(chain, overallMin, overallMax, overall) =>
			{
				if (chain == null || chain.Count < 2)
				{
					return;
				}
				if (!CoversOverallInterval(chain, overallMin, overallMax, tol))
				{
					return;
				}
				bool hasMin = false;
				bool hasMax = false;
				bool hasReal = false;
				foreach (StructureFeature f in chain)
				{
					string role = StructureSchemaEncoder.Role(f);
					if (role == "ResidualMin")
					{
						hasMin = true;
					}
					else if (role == "ResidualMax")
					{
						hasMax = true;
					}
					else if (role != "Residual")
					{
						hasReal = true;
					}
				}
				if (!hasMin || !hasMax || !hasReal)
				{
					return;
				}
				string axisTag = chain[0].Axis == StructureFeatureAxis.Horizontal ? "H" : "V";
				string schema = "CoveringChain|" + axisTag + "|ResidualMin|RealStep|ResidualMax";
				if (seen.Add(schema))
				{
					schemas.Add(schema);
				}
			});
		return schemas;
	}

	private static void ForEachAxisChain(
		List<StructureFeature> list,
		OutlineFeature2D outline,
		double microGap,
		double tol,
		bool includeDropped,
		Action<List<StructureFeature>, double, double, double> visit)
	{
		if (list == null || outline == null || visit == null)
		{
			return;
		}
		foreach (StructureFeatureAxis axis in new[] { StructureFeatureAxis.Horizontal, StructureFeatureAxis.Vertical })
		{
			double overallMin = axis == StructureFeatureAxis.Horizontal ? outline.MinX : outline.MinY;
			double overallMax = axis == StructureFeatureAxis.Horizontal ? outline.MaxX : outline.MaxY;
			double overall = overallMax - overallMin;
			if (overall <= tol)
			{
				continue;
			}
			IEnumerable<IGrouping<string, StructureFeature>> groups = list
				.Where(f => f != null
					&& f.Axis == axis
					&& f.Kind != StructureFeatureKind.Overall
					&& (includeDropped || f.Keep))
				.GroupBy(f => ChainGroupKey(f, outline, axis, microGap, tol));
			foreach (IGrouping<string, StructureFeature> group in groups)
			{
				List<StructureFeature> ordered = group.OrderBy(f => Math.Min(f.T0, f.T1)).ToList();
				foreach (List<StructureFeature> chain in BuildChains(ordered, microGap, tol))
				{
					visit(chain, overallMin, overallMax, overall);
				}
			}
		}
	}

	private static void OpenOneCoveringChain(
		List<StructureFeature> chain,
		double overallMin,
		double overallMax,
		double overall,
		OutlineFeature2D outline,
		double tol)
	{
		if (chain == null || chain.Count < 2)
		{
			return;
		}
		DropCoveringMaxEndResiduals(chain, overallMin, overallMax, tol, "ClosedChainMaxEndResidual");
		List<StructureFeature> live = chain.Where(f => f.Keep).OrderBy(f => Math.Min(f.T0, f.T1)).ToList();
		if (!CoversOverallInterval(live, overallMin, overallMax, tol) || live.Count < 2)
		{
			return;
		}
		StructureFeature drop = live
			.Where(f => !IsProtectedClosedChainMember(f, outline, tol))
			.OrderByDescending(f => f.Span)
			.FirstOrDefault();
		if (drop == null)
		{
			return;
		}
		drop.Keep = false;
		drop.SuppressReason = "ClosedChainLongestUnprotected";
		drop.Kind = StructureFeatureKind.BodyRemainder;
	}

	private static void DropCoveringMaxEndResiduals(
		List<StructureFeature> chain,
		double overallMin,
		double overallMax,
		double tol,
		string reason)
	{
		List<StructureFeature> live = chain.Where(f => f.Keep).OrderBy(f => Math.Min(f.T0, f.T1)).ToList();
		if (!CoversOverallInterval(live, overallMin, overallMax, tol))
		{
			return;
		}
		List<StructureFeature> residuals = live.Where(IsLedgeResidual).ToList();
		List<StructureFeature> maxEndResiduals = residuals.Where(r => r.TouchesOverallMax).ToList();
		List<StructureFeature> otherResiduals = residuals.Where(r => !r.TouchesOverallMax).ToList();
		if (maxEndResiduals.Count == 0 || otherResiduals.Count == 0)
		{
			return;
		}
		foreach (StructureFeature r in maxEndResiduals)
		{
			r.Keep = false;
			r.SuppressReason = reason;
			r.Kind = StructureFeatureKind.BodyRemainder;
		}
	}

	private static bool CoversOverallInterval(
		List<StructureFeature> members,
		double overallMin,
		double overallMax,
		double tol)
	{
		if (members == null || members.Count < 2)
		{
			return false;
		}
		double c0 = members.Min(f => Math.Min(f.T0, f.T1));
		double c1 = members.Max(f => Math.Max(f.T0, f.T1));
		return Math.Abs(c0 - overallMin) <= tol
			&& Math.Abs(c1 - overallMax) <= tol;
	}

	private static bool IsProtectedClosedChainMember(
		StructureFeature f,
		OutlineFeature2D outline,
		double tol)
	{
		return IsOuterTip(f) || IsStepGroove(f) || IsInnerBoss(f) || IsFootLedge(f)
			|| IsMaxEnvelopePartialStep(f, outline, tol);
	}

	private static List<List<StructureFeature>> BuildChains(
		List<StructureFeature> ordered,
		double microGap,
		double tol)
	{
		List<List<StructureFeature>> chains = new List<List<StructureFeature>>();
		List<StructureFeature> current = new List<StructureFeature>();
		double curMax = double.NegativeInfinity;
		foreach (StructureFeature f in ordered)
		{
			double a0 = Math.Min(f.T0, f.T1);
			double a1 = Math.Max(f.T0, f.T1);
			if (current.Count == 0)
			{
				current.Add(f);
				curMax = a1;
				continue;
			}
			if (a0 <= curMax + microGap + tol)
			{
				current.Add(f);
				curMax = Math.Max(curMax, a1);
			}
			else
			{
				chains.Add(current);
				current = new List<StructureFeature> { f };
				curMax = a1;
			}
		}
		if (current.Count > 0)
		{
			chains.Add(current);
		}
		return chains;
	}

	/// <summary>
	/// Multi-level bottom/top ledges: union intervals that abut or overlap in X into one chain.
	/// </summary>
	private static List<List<StructureFeature>> BuildOverlapChains(
		List<StructureFeature> ordered,
		double microGap,
		double tol)
	{
		// Single chain of all members that participate in covering overall when sorted by T0 —
		// multi-level steps at different Y still partition the same X overall.
		if (ordered.Count == 0)
		{
			return new List<List<StructureFeature>>();
		}
		return new List<List<StructureFeature>> { ordered.ToList() };
	}

	private static void ApplyNestedIntervalPreference(
		List<StructureFeature> list,
		StructureFeatureAxis axis,
		OutlineFeature2D outline,
		double tol,
		HashSet<StructureFeature> balancedSteps)
	{
		List<StructureFeature> kept = list
			.Where(f => f.Keep && f.Axis == axis && f.Kind != StructureFeatureKind.Overall)
			.ToList();
		foreach (StructureFeature inner in kept)
		{
			if (!inner.Keep)
			{
				continue;
			}
			// Outer envelope short tips (foot 20) must survive even when nested in a taller foot residual.
			// Keep threshold tight so F215 lu=32 still loses to residual 42.
			// 槽宽 is an interior clear width and must not lose to a merged body that contains it.
			if (IsStepGroove(inner) || IsOuterTip(inner) || IsInnerBoss(inner) || IsFootLedge(inner)
				|| IsStepRiser(inner)
				|| inner.PreferLocalPlacement
				|| (balancedSteps != null && balancedSteps.Contains(inner)))
			{
				continue;
			}
			if (!inner.IsInset && inner.Span + tol < 25.0)
			{
				continue;
			}
			double i0 = Math.Min(inner.T0, inner.T1);
			double i1 = Math.Max(inner.T0, inner.T1);
			foreach (StructureFeature outer in kept)
			{
				if (outer == inner || !outer.Keep)
				{
					continue;
				}
				if (outer.PreferredSide != inner.PreferredSide)
				{
					continue;
				}
				double o0 = Math.Min(outer.T0, outer.T1);
				double o1 = Math.Max(outer.T0, outer.T1);
				// outer strictly contains inner
				if (o0 <= i0 + tol && o1 >= i1 - tol && outer.Span > inner.Span + tol)
				{
					if (IsInnerBoss(outer)
						|| IsThinStepRiserComplement(outer, list, outline, tol))
					{
						continue;
					}
					// Prefer outer residual when both on envelope band
					if (Math.Abs(outer.CrossPosition - inner.CrossPosition) <= Math.Max(tol * 20, outer.Span * 0.01)
						|| outer.PreferredSide == inner.PreferredSide)
					{
						inner.Keep = false;
						inner.SuppressReason = "NestedInLargerResidual";
					}
				}
			}
		}
	}

	private bool IsProtectedLongArm(
		StructureFeature f,
		List<StructureFeature> all,
		OutlineFeature2D outline,
		double tol)
	{
		if (outline == null || f == null)
		{
			return false;
		}
		double overall = f.Axis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
		if (f.Span + tol < overall * 0.7 || Math.Abs(f.Span - overall) <= tol)
		{
			return false;
		}
		List<StructureFeature> chain = all.Where(x => x.ChainId == f.ChainId && x != f && x.Keep).ToList();
		if (chain.Count >= 2
			&& chain.Sum(x => x.Span) + f.Span + tol >= overall * 0.95)
		{
			return false;
		}
		return !f.IsInset;
	}

	private static int EnvelopeScore(StructureFeature f, OutlineFeature2D outline, double tol)
	{
		if (outline == null || f == null)
		{
			return 0;
		}
		if (f.Axis == StructureFeatureAxis.Vertical)
		{
			if (Math.Abs(f.CrossPosition - outline.MinX) <= tol || Math.Abs(f.CrossPosition - outline.MaxX) <= tol)
			{
				return 2;
			}
		}
		else
		{
			if (Math.Abs(f.CrossPosition - outline.MinY) <= tol || Math.Abs(f.CrossPosition - outline.MaxY) <= tol)
			{
				return 2;
			}
		}
		return f.SourceKey != null && f.SourceKey.StartsWith("LedgeResidual", StringComparison.Ordinal) ? 0 : 1;
	}

	private static string IntervalKey(StructureFeature f, double tol)
	{
		double a0 = Math.Min(f.T0, f.T1);
		double a1 = Math.Max(f.T0, f.T1);
		long b0 = (long)Math.Round(a0 / Math.Max(tol, 1e-6));
		long b1 = (long)Math.Round(a1 / Math.Max(tol, 1e-6));
		return f.Axis + ":" + b0 + ":" + b1;
	}

	private static string BandKey(StructureFeature f, double microGap, double tol)
	{
		double bucket = Math.Round(f.CrossPosition / Math.Max(microGap, tol * 10.0));
		return f.PreferredSide + ":" + bucket.ToString(System.Globalization.CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Multi-level steps: group by envelope half (bottom vs top / left vs right band), not ray-cast
	/// facing alone — mid ledges may face "Top" in a pocket yet belong to the bottom step chain.
	/// </summary>
	private static string ChainGroupKey(
		StructureFeature f,
		OutlineFeature2D outline,
		StructureFeatureAxis axis,
		double microGap,
		double tol)
	{
		if (axis == StructureFeatureAxis.Horizontal)
		{
			double midY = (outline.MinY + outline.MaxY) * 0.5;
			return f.CrossPosition <= midY + tol ? "H-BottomHalf" : "H-TopHalf";
		}
		double midX = (outline.MinX + outline.MaxX) * 0.5;
		return f.CrossPosition <= midX + tol ? "V-LeftHalf" : "V-RightHalf";
	}
}
