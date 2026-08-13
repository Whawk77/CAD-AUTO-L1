using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Planning;

/// <summary>
/// Rotation-oriented structure feature extraction: contour edges → 1D features + chains.
/// Side is only a placement hint (facing), not a selection key.
/// </summary>
public sealed class StructureFeatureExtractor
{
	private readonly DimensionRuleConfig _config;
	private readonly StructureSuppressionRules _inside;

	public StructureFeatureExtractor(DimensionRuleConfig config)
	{
		_config = config ?? DimensionRuleConfig.CreateDefault();
		_inside = new StructureSuppressionRules(_config);
	}

	public IList<StructureFeature> Extract(OutlineFeature2D outline)
	{
		List<StructureFeature> features = new List<StructureFeature>();
		if (outline == null)
		{
			return features;
		}
		double tol = _config.GeometryTolerance;
		double microGap = Math.Max(5.0, Math.Max(outline.Width, outline.Height) * 0.025);
		double minSpan = Math.Max(tol * 4.0, _config.TextHeight * 2.0);
		// Fillet/chamfer: outer edges may sit a few mm inside the AABB — still not "inset".
		double envelopeBand = Math.Max(Math.Max(tol * 20.0, 6.0), Math.Min(outline.Width, outline.Height) * 0.04);
		// Tighter than envelopeBand so a 3mm-filleted notch floor (F215 CAD Y=97) is inset.
		double insetBand = Math.Max(tol * 20.0, 2.0);

		// Overall
		features.Add(CreateOverall(outline, StructureFeatureAxis.Horizontal));
		features.Add(CreateOverall(outline, StructureFeatureAxis.Vertical));

		if (outline.Segments == null)
		{
			return features;
		}

		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord)
			{
				continue;
			}
			if (segment.IsHorizontal(tol))
			{
				double span = segment.LengthX;
				// Keep short outer tips (foot 20); still skip full overall and noise micro-spans.
				if (span < minSpan - tol || Math.Abs(span - outline.Width) <= tol)
				{
					continue;
				}
				double y = (segment.Start.Y + segment.End.Y) * 0.5;
				DimensionSide side = ResolveHorizontalFacing(segment, outline, tol);
				bool inset = Math.Abs(y - outline.MinY) > insetBand && Math.Abs(y - outline.MaxY) > insetBand;
				double maxSide = Math.Max(outline.Width, outline.Height);
				bool outerTip = !inset && span + tol >= 12.0 && span <= maxSide * 0.12 + tol;
				features.Add(new StructureFeature
				{
					Axis = StructureFeatureAxis.Horizontal,
					Kind = StructureFeatureKind.RealStep,
					T0 = Math.Min(segment.Start.X, segment.End.X),
					T1 = Math.Max(segment.Start.X, segment.End.X),
					CrossPosition = y,
					PreferredSide = side,
					IsInset = inset,
					TouchesOverallMin = Math.Abs(Math.Min(segment.Start.X, segment.End.X) - outline.MinX) <= tol,
					TouchesOverallMax = Math.Abs(Math.Max(segment.Start.X, segment.End.X) - outline.MaxX) <= tol,
					FirstPoint = new Point2D(Math.Min(segment.Start.X, segment.End.X), y),
					SecondPoint = new Point2D(Math.Max(segment.Start.X, segment.End.X), y),
					SourceKey = outerTip ? "OuterTip:" + (segment.SourceKey ?? string.Empty) : segment.SourceKey,
					Confidence = inset ? 0.85 : 1.0,
					SupportSourceKeys = { segment.SourceKey ?? string.Empty }
				});
			}
			else if (segment.IsVertical(tol))
			{
				double span = segment.LengthY;
				if (span < minSpan - tol || Math.Abs(span - outline.Height) <= tol)
				{
					continue;
				}
				double x = (segment.Start.X + segment.End.X) * 0.5;
				DimensionSide side = ResolveVerticalFacing(segment, outline, tol);
				bool inset = Math.Abs(x - outline.MinX) > insetBand && Math.Abs(x - outline.MaxX) > insetBand;
				double maxSide = Math.Max(outline.Width, outline.Height);
				bool outerTip = !inset && span + tol >= 12.0 && span <= maxSide * 0.12 + tol;
				features.Add(new StructureFeature
				{
					Axis = StructureFeatureAxis.Vertical,
					Kind = StructureFeatureKind.RealStep,
					T0 = Math.Min(segment.Start.Y, segment.End.Y),
					T1 = Math.Max(segment.Start.Y, segment.End.Y),
					CrossPosition = x,
					PreferredSide = side,
					IsInset = inset,
					TouchesOverallMin = Math.Abs(Math.Min(segment.Start.Y, segment.End.Y) - outline.MinY) <= tol,
					TouchesOverallMax = Math.Abs(Math.Max(segment.Start.Y, segment.End.Y) - outline.MaxY) <= tol,
					FirstPoint = new Point2D(x, Math.Min(segment.Start.Y, segment.End.Y)),
					SecondPoint = new Point2D(x, Math.Max(segment.Start.Y, segment.End.Y)),
					SourceKey = outerTip ? "OuterTip:" + (segment.SourceKey ?? string.Empty) : segment.SourceKey,
					Confidence = inset ? 0.9 : 1.0,
					SupportSourceKeys = { segment.SourceKey ?? string.Empty }
				});
			}
		}

		// Real CAD: fillets are arc chords (skipped above) that eat 3–10mm off H/V runs
		// (305→300, 73→63). Walk through short bridges to orthogonal walls / collinear peers
		// and expand each RealStep to the synthetic outer span (grill-me 合成外轮廓边).
		double maxBridge = Math.Max(microGap * 2.0, Math.Min(outline.Width, outline.Height) * 0.08);
		ExpandFeaturesAcrossFilletBridges(features, outline, maxBridge, envelopeBand, tol);

		// Composite outer edges: collinear H/V pieces separated by fillet/chamfer micro-gap
		// (e.g. 150+2gap+155 → 305) merge into one RealStep.
		MergeCollinearRealSteps(features, outline, microGap, envelopeBand, tol);

		// Step groove width (槽宽): geometry span (CAD ~92.55, sharp 87.55), not a fixed constant.
		// 1) riser-pair gaps  2) inset floor edges expanded to walls (last-run: only OS 82.55).
		RecoverStepGrooveWidths(features, outline, envelopeBand, tol);
		RecoverStepGrooveFromInsetFloors(features, outline, maxBridge, envelopeBand, tol);
		AlignStepGroovesToLocatingSteps(features, outline, envelopeBand, tol);

		// Notch openings: inset mid-span edges that cut into the *outer* envelope
		// (F215 n50 near MaxY). Interior multi-level treads (F338 87.55 at Y=50) and
		// platform risers (F338 riser-body) are NOT notches — critical after 90° axis swap.
		double notchOuterTol = Math.Max(microGap, Math.Min(outline.Width, outline.Height) * 0.15);
		foreach (StructureFeature f in features.Where(f => f.Kind == StructureFeatureKind.RealStep && f.IsInset).ToList())
		{
			if (f.TouchesOverallMin || f.TouchesOverallMax)
			{
				continue;
			}
			bool nearOuterEnvelope = f.Axis == StructureFeatureAxis.Horizontal
				? (Math.Abs(f.CrossPosition - outline.MinY) <= notchOuterTol
					|| Math.Abs(f.CrossPosition - outline.MaxY) <= notchOuterTol)
				: (Math.Abs(f.CrossPosition - outline.MinX) <= notchOuterTol
					|| Math.Abs(f.CrossPosition - outline.MaxX) <= notchOuterTol);
			if (!nearOuterEnvelope)
			{
				continue;
			}
			double overall = f.Axis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
			if (f.Span + tol < overall * 0.5 && f.Span + tol >= overall * 0.15)
			{
				f.Kind = StructureFeatureKind.NotchOpening;
				f.Confidence = Math.Max(f.Confidence, 0.95);
			}
		}

		// Outer residual heights/widths from partial envelope ledges (e.g. F215 left tip 42
		// = MaxY − platform Y on MinX-touching ledge).
		AddEnvelopeLedgeResiduals(features, outline, minSpan, tol);
		AddChamferComplementResiduals(features, outline, envelopeBand, minSpan, tol);

		// Outer envelope short tips (CAD foot tip ~20): promote to Structure even when OS
		// would only see OutlineSegmentOnOverallEnvelope.
		AddOuterEnvelopeTips(features, outline, envelopeBand, minSpan, tol);

		AssignChainsAndClassify(features, outline, microGap, tol);
		return features;
	}

	/// <summary>
	/// Short outer-envelope edges (15–12% of max side) as RealStep tips for four-way 20-class dims.
	/// </summary>
	private void AddOuterEnvelopeTips(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		double envelopeBand,
		double minSpan,
		double tol)
	{
		if (outline?.Segments == null)
		{
			return;
		}
		double maxSide = Math.Max(outline.Width, outline.Height);
		double tipMax = maxSide * 0.12;
		double tipMin = Math.Max(minSpan, 12.0);
		double orthoTol = Math.Max(tol * 4.0, 1.0);
		foreach (Segment2D s in outline.Segments)
		{
			if (s == null || s.IsArcChord)
			{
				continue;
			}
			if (s.IsVertical(orthoTol))
			{
				double x = (s.Start.X + s.End.X) * 0.5;
				bool onEnv = Math.Abs(x - outline.MinX) <= envelopeBand
					|| Math.Abs(x - outline.MaxX) <= envelopeBand;
				double span = Math.Abs(s.End.Y - s.Start.Y);
				if (!onEnv || span + tol < tipMin || span > tipMax + tol)
				{
					continue;
				}
				if (features.Any(f => f.Axis == StructureFeatureAxis.Vertical
					&& Math.Abs(f.Span - span) <= tol * 10
					&& Math.Abs(f.CrossPosition - x) <= envelopeBand))
				{
					continue;
				}
				double y0 = Math.Min(s.Start.Y, s.End.Y);
				double y1 = Math.Max(s.Start.Y, s.End.Y);
				features.Add(new StructureFeature
				{
					Axis = StructureFeatureAxis.Vertical,
					Kind = StructureFeatureKind.RealStep,
					T0 = y0,
					T1 = y1,
					CrossPosition = x,
					PreferredSide = Math.Abs(x - outline.MinX) <= envelopeBand
						? DimensionSide.Left
						: DimensionSide.Right,
					IsInset = false,
					TouchesOverallMin = Math.Abs(y0 - outline.MinY) <= envelopeBand,
					TouchesOverallMax = Math.Abs(y1 - outline.MaxY) <= envelopeBand,
					FirstPoint = new Point2D(x, y0),
					SecondPoint = new Point2D(x, y1),
					Confidence = 0.96,
					SourceKey = "OuterTip:" + (s.SourceKey ?? "")
				});
			}
			else if (s.IsHorizontal(orthoTol))
			{
				double y = (s.Start.Y + s.End.Y) * 0.5;
				bool onEnv = Math.Abs(y - outline.MinY) <= envelopeBand
					|| Math.Abs(y - outline.MaxY) <= envelopeBand;
				double span = Math.Abs(s.End.X - s.Start.X);
				if (!onEnv || span + tol < tipMin || span > tipMax + tol)
				{
					continue;
				}
				if (features.Any(f => f.Axis == StructureFeatureAxis.Horizontal
					&& Math.Abs(f.Span - span) <= tol * 10
					&& Math.Abs(f.CrossPosition - y) <= envelopeBand))
				{
					continue;
				}
				double x0 = Math.Min(s.Start.X, s.End.X);
				double x1 = Math.Max(s.Start.X, s.End.X);
				features.Add(new StructureFeature
				{
					Axis = StructureFeatureAxis.Horizontal,
					Kind = StructureFeatureKind.RealStep,
					T0 = x0,
					T1 = x1,
					CrossPosition = y,
					PreferredSide = Math.Abs(y - outline.MinY) <= envelopeBand
						? DimensionSide.Bottom
						: DimensionSide.Top,
					IsInset = false,
					TouchesOverallMin = Math.Abs(x0 - outline.MinX) <= envelopeBand,
					TouchesOverallMax = Math.Abs(x1 - outline.MaxX) <= envelopeBand,
					FirstPoint = new Point2D(x0, y),
					SecondPoint = new Point2D(x1, y),
					Confidence = 0.96,
					SourceKey = "OuterTip:" + (s.SourceKey ?? "")
				});
			}
		}
	}

	private void AddEnvelopeLedgeResiduals(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		double minSpan,
		double tol)
	{
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord || !segment.IsHorizontal(tol))
			{
				continue;
			}
			// Platform/ledge residuals: allow shorter envelope shelves (CAD right-arm ~33)
			// so foot height 100 can be recovered when MaxX is only a tip segment.
			if (segment.LengthX + tol < Math.Max(outline.Width * 0.08, minSpan))
			{
				continue;
			}
			double y = (segment.Start.Y + segment.End.Y) * 0.5;
			if (y <= outline.MinY + tol || y >= outline.MaxY - tol)
			{
				continue;
			}
			bool touchesMinX = Math.Abs(segment.MinX - outline.MinX) <= tol;
			bool touchesMaxX = Math.Abs(segment.MaxX - outline.MaxX) <= tol;
			if (!touchesMinX && !touchesMaxX)
			{
				continue;
			}
			double up = outline.MaxY - y;
			double down = y - outline.MinY;
			DimensionSide side = touchesMinX ? DimensionSide.Left : DimensionSide.Right;
			double x = touchesMinX ? outline.MinX : outline.MaxX;
			if (up >= minSpan - tol && up + tol < outline.Height
				&& !HasVerticalEdgeNear(features, x, y, outline.MaxY, tol))
			{
				features.Add(new StructureFeature
				{
					Axis = StructureFeatureAxis.Vertical,
					Kind = StructureFeatureKind.RealStep,
					T0 = y,
					T1 = outline.MaxY,
					CrossPosition = x,
					PreferredSide = side,
					IsInset = false,
					TouchesOverallMax = true,
					FirstPoint = new Point2D(x, y),
					SecondPoint = new Point2D(x, outline.MaxY),
					Confidence = 0.8,
					SourceKey = "LedgeResidualUp:" + (segment.SourceKey ?? string.Empty)
				});
			}
			if (down >= minSpan - tol && down + tol < outline.Height
				&& !HasVerticalEdgeNear(features, x, outline.MinY, y, tol))
			{
				features.Add(new StructureFeature
				{
					Axis = StructureFeatureAxis.Vertical,
					Kind = StructureFeatureKind.RealStep,
					T0 = outline.MinY,
					T1 = y,
					CrossPosition = x,
					PreferredSide = side,
					IsInset = false,
					TouchesOverallMin = true,
					FirstPoint = new Point2D(x, outline.MinY),
					SecondPoint = new Point2D(x, y),
					Confidence = 0.75,
					SourceKey = "LedgeResidualDown:" + (segment.SourceKey ?? string.Empty)
				});
			}
		}

		// Dual: vertical ledges meeting MinY/MaxY → horizontal residuals (rotation of the above).
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord || !segment.IsVertical(tol))
			{
				continue;
			}
			if (segment.LengthY + tol < Math.Max(minSpan, outline.Height * 0.08))
			{
				continue;
			}
			double x = (segment.Start.X + segment.End.X) * 0.5;
			if (x <= outline.MinX + tol || x >= outline.MaxX - tol)
			{
				continue;
			}
			bool touchesMinY = Math.Abs(segment.MinY - outline.MinY) <= tol;
			bool touchesMaxY = Math.Abs(segment.MaxY - outline.MaxY) <= tol;
			if (!touchesMinY && !touchesMaxY)
			{
				continue;
			}
			double right = outline.MaxX - x;
			double left = x - outline.MinX;
			DimensionSide side = touchesMinY ? DimensionSide.Bottom : DimensionSide.Top;
			double y = touchesMinY ? outline.MinY : outline.MaxY;
			if (right >= minSpan - tol && right + tol < outline.Width
				&& !HasHorizontalEdgeNear(features, y, x, outline.MaxX, tol))
			{
				features.Add(new StructureFeature
				{
					Axis = StructureFeatureAxis.Horizontal,
					Kind = StructureFeatureKind.RealStep,
					T0 = x,
					T1 = outline.MaxX,
					CrossPosition = y,
					PreferredSide = side,
					IsInset = false,
					TouchesOverallMax = true,
					FirstPoint = new Point2D(x, y),
					SecondPoint = new Point2D(outline.MaxX, y),
					Confidence = 0.8,
					SourceKey = "LedgeResidualRight:" + (segment.SourceKey ?? string.Empty)
				});
			}
			if (left >= minSpan - tol && left + tol < outline.Width
				&& !HasHorizontalEdgeNear(features, y, outline.MinX, x, tol))
			{
				features.Add(new StructureFeature
				{
					Axis = StructureFeatureAxis.Horizontal,
					Kind = StructureFeatureKind.RealStep,
					T0 = outline.MinX,
					T1 = x,
					CrossPosition = y,
					PreferredSide = side,
					IsInset = false,
					TouchesOverallMin = true,
					FirstPoint = new Point2D(outline.MinX, y),
					SecondPoint = new Point2D(x, y),
					Confidence = 0.75,
					SourceKey = "LedgeResidualLeft:" + (segment.SourceKey ?? string.Empty)
				});
			}
		}
	}

	private static bool HasHorizontalEdgeNear(
		List<StructureFeature> features,
		double y,
		double x0,
		double x1,
		double tol)
	{
		double a0 = Math.Min(x0, x1);
		double a1 = Math.Max(x0, x1);
		double span = a1 - a0;
		return features.Any(f =>
			f.Axis == StructureFeatureAxis.Horizontal
			&& f.Kind != StructureFeatureKind.Overall
			&& Math.Abs(f.CrossPosition - y) <= tol * 4
			&& Math.Abs(f.Span - span) <= Math.Max(tol * 10, span * 0.05));
	}

	private static bool HasVerticalEdgeNear(
		List<StructureFeature> features,
		double x,
		double y0,
		double y1,
		double tol)
	{
		double a0 = Math.Min(y0, y1);
		double a1 = Math.Max(y0, y1);
		double span = a1 - a0;
		return features.Any(f =>
			f.Axis == StructureFeatureAxis.Vertical
			&& f.Kind != StructureFeatureKind.Overall
			&& Math.Abs(f.CrossPosition - x) <= tol * 4
			&& Math.Abs(f.Span - span) <= Math.Max(tol * 10, span * 0.05));
	}

	private static StructureFeature CreateOverall(OutlineFeature2D outline, StructureFeatureAxis axis)
	{
		if (axis == StructureFeatureAxis.Horizontal)
		{
			return new StructureFeature
			{
				Axis = axis,
				Kind = StructureFeatureKind.Overall,
				T0 = outline.MinX,
				T1 = outline.MaxX,
				CrossPosition = outline.MinY,
				PreferredSide = DimensionSide.Bottom,
				FirstPoint = new Point2D(outline.MinX, outline.MinY),
				SecondPoint = new Point2D(outline.MaxX, outline.MinY),
				Confidence = 1.0,
				TouchesOverallMin = true,
				TouchesOverallMax = true
			};
		}
		return new StructureFeature
		{
			Axis = axis,
			Kind = StructureFeatureKind.Overall,
			T0 = outline.MinY,
			T1 = outline.MaxY,
			CrossPosition = outline.MinX,
			PreferredSide = DimensionSide.Left,
			FirstPoint = new Point2D(outline.MinX, outline.MinY),
			SecondPoint = new Point2D(outline.MinX, outline.MaxY),
			Confidence = 1.0,
			TouchesOverallMin = true,
			TouchesOverallMax = true
		};
	}

	private DimensionSide ResolveHorizontalFacing(Segment2D segment, OutlineFeature2D outline, double tol)
	{
		double y = (segment.Start.Y + segment.End.Y) * 0.5;
		if (Math.Abs(y - outline.MinY) <= tol)
		{
			return DimensionSide.Bottom;
		}
		if (Math.Abs(y - outline.MaxY) <= tol)
		{
			return DimensionSide.Top;
		}
		double midX = (segment.MinX + segment.MaxX) * 0.5;
		double probe = Math.Max(tol * 4.0, Math.Min(outline.Height * 0.05, 1.0));
		bool above = _inside.IsPointInsideOutlineByRayCast(midX, y + probe, outline);
		bool below = _inside.IsPointInsideOutlineByRayCast(midX, y - probe, outline);
		if (above && !below)
		{
			return DimensionSide.Bottom;
		}
		if (below && !above)
		{
			return DimensionSide.Top;
		}
		return Math.Abs(y - outline.MinY) <= Math.Abs(outline.MaxY - y)
			? DimensionSide.Bottom
			: DimensionSide.Top;
	}

	private DimensionSide ResolveVerticalFacing(Segment2D segment, OutlineFeature2D outline, double tol)
	{
		double x = (segment.Start.X + segment.End.X) * 0.5;
		if (Math.Abs(x - outline.MinX) <= tol)
		{
			return DimensionSide.Left;
		}
		if (Math.Abs(x - outline.MaxX) <= tol)
		{
			return DimensionSide.Right;
		}
		double midY = (segment.MinY + segment.MaxY) * 0.5;
		double probe = Math.Max(tol * 4.0, Math.Min(outline.Width * 0.05, 1.0));
		bool right = _inside.IsPointInsideOutlineByRayCast(x + probe, midY, outline);
		bool left = _inside.IsPointInsideOutlineByRayCast(x - probe, midY, outline);
		if (right && !left)
		{
			return DimensionSide.Left;
		}
		if (left && !right)
		{
			return DimensionSide.Right;
		}
		return Math.Abs(x - outline.MinX) <= Math.Abs(outline.MaxX - x)
			? DimensionSide.Left
			: DimensionSide.Right;
	}

	/// <summary>
	/// 槽宽优先与其定位尺寸（邻接的外包络短台阶，F338 的 73）同侧对齐。
	/// Ray-cast / 近包络回退会把 CAD 中位台面（Y=106.5）贴到 305 一侧。
	/// </summary>
	private DimensionSide ResolveGroovePreferredSide(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		bool horizontalGroove,
		double t0,
		double t1,
		double cross,
		double envelopeBand,
		double tol)
	{
		StructureFeature locator = FindGrooveLocatingStep(
			features, outline, horizontalGroove, t0, t1, envelopeBand, tol);
		if (locator != null)
		{
			return locator.PreferredSide;
		}
		if (horizontalGroove)
		{
			var floor = new Segment2D(new Point2D(t0, cross), new Point2D(t1, cross));
			return ResolveHorizontalFacing(floor, outline, tol);
		}
		var wall = new Segment2D(new Point2D(cross, t0), new Point2D(cross, t1));
		return ResolveVerticalFacing(wall, outline, tol);
	}

	private static StructureFeature FindGrooveLocatingStep(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		bool horizontalGroove,
		double t0,
		double t1,
		double envelopeBand,
		double tol)
	{
		if (features == null || outline == null)
		{
			return null;
		}
		StructureFeatureAxis axis = horizontalGroove
			? StructureFeatureAxis.Horizontal
			: StructureFeatureAxis.Vertical;
		double overall = horizontalGroove ? outline.Width : outline.Height;
		double g0 = Math.Min(t0, t1);
		double g1 = Math.Max(t0, t1);
		double abutTol = Math.Max(envelopeBand, 12.0);
		return features
			.Where(f => f.Axis == axis
				&& f.Kind != StructureFeatureKind.Overall
				&& (f.SourceKey == null || !f.SourceKey.StartsWith("StepGroove", StringComparison.Ordinal))
				&& !f.IsInset
				&& (f.TouchesOverallMin || f.TouchesOverallMax)
				&& f.Span + tol >= overall * 0.12
				&& f.Span + tol < overall * 0.40)
			.Select(f =>
			{
				double f0 = Math.Min(f.T0, f.T1);
				double f1 = Math.Max(f.T0, f.T1);
				double gap = Math.Max(0.0, Math.Max(g0 - f1, f0 - g1));
				return new { Feature = f, Gap = gap };
			})
			.Where(x => x.Gap <= abutTol)
			.OrderBy(x => x.Gap)
			.ThenBy(x => x.Feature.Span)
			.Select(x => x.Feature)
			.FirstOrDefault();
	}

	private void AlignStepGroovesToLocatingSteps(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		double envelopeBand,
		double tol)
	{
		if (features == null)
		{
			return;
		}
		foreach (StructureFeature groove in features.Where(f =>
			f.SourceKey != null && f.SourceKey.StartsWith("StepGroove", StringComparison.Ordinal)))
		{
			bool horizontalGroove = groove.Axis == StructureFeatureAxis.Horizontal;
			groove.PreferredSide = ResolveGroovePreferredSide(
				features, outline, horizontalGroove, groove.T0, groove.T1, groove.CrossPosition, envelopeBand, tol);
		}
	}

	private void AssignChainsAndClassify(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		double microGap,
		double tol)
	{
		int nextChain = 1;
		foreach (StructureFeatureAxis axis in new[] { StructureFeatureAxis.Horizontal, StructureFeatureAxis.Vertical })
		{
			double overall = axis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
			// Group by placement band (similar CrossPosition and PreferredSide)
			var bands = features
				.Where(f => f.Axis == axis && f.Kind != StructureFeatureKind.Overall)
				.GroupBy(f => BandKey(f, microGap, tol))
				.ToList();
			foreach (var band in bands)
			{
				List<StructureFeature> ordered = band
					.OrderBy(f => Math.Min(f.T0, f.T1))
					.ToList();
				// Link near-abutting into chains
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

				foreach (List<StructureFeature> chain in chains)
				{
					int chainId = nextChain++;
					foreach (StructureFeature f in chain)
					{
						f.ChainId = chainId;
					}
					double c0 = chain.Min(f => Math.Min(f.T0, f.T1));
					double c1 = chain.Max(f => Math.Max(f.T0, f.T1));
					bool coversOverall = Math.Abs(c0 - (axis == StructureFeatureAxis.Horizontal ? outline.MinX : outline.MinY)) <= microGap + tol
						&& Math.Abs(c1 - (axis == StructureFeatureAxis.Horizontal ? outline.MaxX : outline.MaxY)) <= microGap + tol
						&& Math.Abs((c1 - c0) - overall) <= microGap * 2 + tol;

					// Noise tips — never mark outer-envelope short real steps (foot tip 20) as noise.
					foreach (StructureFeature f in chain)
					{
						if (f.Span + tol < overall * 0.08
							&& f.Kind != StructureFeatureKind.NotchOpening
							&& f.IsInset)
						{
							f.Kind = StructureFeatureKind.NoiseTip;
						}
					}

					if (!coversOverall || chain.Count < 2)
					{
						// Long outer edge that is not full overall (e.g. 305 of 338) stays RealStep.
						continue;
					}

					// Complete / near-complete chain: classify longer overall-closing end as body.
					List<StructureFeature> ends = chain
						.Where(f => f.TouchesOverallMin || f.TouchesOverallMax)
						.OrderByDescending(f => f.Span)
						.ToList();
					if (ends.Count >= 1)
					{
						StructureFeature longestEnd = ends[0];
						StructureFeature secondEnd = ends.Count > 1 ? ends[1] : null;
						bool uniqueLongest = secondEnd == null
							|| longestEnd.Span > secondEnd.Span + tol;
						// Multi-piece: drop unique longest end if it is strictly longer than others
						// and is a substantial body (≥35% overall), unless it is the only long real arm
						// that does not complete with short tips only — 305 is kept as RealStep when
						// it is NOT part of a three-piece 73+87.55+177 chain on the same band.
						if (uniqueLongest && longestEnd.Span + tol >= overall * 0.35
							&& !IsLongArmWithTipResidual(longestEnd, overall, tol))
						{
							// Interior real steps present → longest end is body remainder.
							bool hasInterior = chain.Any(f =>
								!f.TouchesOverallMin && !f.TouchesOverallMax
								&& f.Kind != StructureFeatureKind.NoiseTip);
							if (hasInterior || chain.Count >= 3)
							{
								longestEnd.Kind = StructureFeatureKind.BodyRemainder;
							}
						}
					}
					// n≥3: also mark unique longest overall (any member) as body if covers overall
					if (chain.Count >= 3)
					{
						StructureFeature longest = chain.OrderByDescending(f => f.Span).First();
						StructureFeature second = chain.OrderByDescending(f => f.Span).Skip(1).First();
						if (longest.Span > second.Span + tol
							&& longest.Span + tol >= overall * 0.35
							&& (longest.TouchesOverallMin || longest.TouchesOverallMax)
							&& !IsLongArmWithTipResidual(longest, overall, tol))
						{
							longest.Kind = StructureFeatureKind.BodyRemainder;
						}
					}
				}
			}
		}
	}

	private static string BandKey(StructureFeature f, double microGap, double tol)
	{
		// Bucket cross-position so co-band edges chain together.
		double bucket = Math.Round(f.CrossPosition / Math.Max(microGap, tol * 10.0));
		return f.PreferredSide + ":" + bucket.ToString(System.Globalization.CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// 槽宽 = 台面两端扩到相邻立面站点的净距（真图 82.55 台面 → ~92.55 立面距）。
	/// Only recover along the long envelope axis so a 0° riser height (86.5) is not a groove.
	/// </summary>
	private void RecoverStepGrooveFromInsetFloors(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		double maxBridge,
		double envelopeBand,
		double tol)
	{
		if (outline?.Segments == null)
		{
			return;
		}
		bool horizontalGroove = outline.Width >= outline.Height;
		List<Segment2D> segs = outline.Segments.Where(s => s != null).ToList();
		double orthoTol = Math.Max(tol * 4.0, 1.0);
		double overall = horizontalGroove ? outline.Width : outline.Height;
		foreach (Segment2D s in segs)
		{
			if (s.IsArcChord)
			{
				continue;
			}
			if (horizontalGroove && !s.IsHorizontal(orthoTol))
			{
				continue;
			}
			if (!horizontalGroove && !s.IsVertical(orthoTol))
			{
				continue;
			}
			double cross = horizontalGroove
				? (s.Start.Y + s.End.Y) * 0.5
				: (s.Start.X + s.End.X) * 0.5;
			bool inset = horizontalGroove
				? (Math.Abs(cross - outline.MinY) > envelopeBand && Math.Abs(cross - outline.MaxY) > envelopeBand)
				: (Math.Abs(cross - outline.MinX) > envelopeBand && Math.Abs(cross - outline.MaxX) > envelopeBand);
			if (!inset)
			{
				continue;
			}
			double t0 = horizontalGroove
				? Math.Min(s.Start.X, s.End.X)
				: Math.Min(s.Start.Y, s.End.Y);
			double t1 = horizontalGroove
				? Math.Max(s.Start.X, s.End.X)
				: Math.Max(s.Start.Y, s.End.Y);
			double rawSpan = t1 - t0;
			if (rawSpan + tol < overall * 0.10 || rawSpan + tol >= overall * 0.55)
			{
				continue;
			}
			double e0 = SnapFloorEndToAdjacentFace(
				outline, segs, horizontalGroove, t0, cross, expandMin: true, maxBridge, envelopeBand, tol);
			double e1 = SnapFloorEndToAdjacentFace(
				outline, segs, horizontalGroove, t1, cross, expandMin: false, maxBridge, envelopeBand, tol);
			double gap = e1 - e0;
			if (gap + tol < overall * 0.12 || gap + tol >= overall * 0.50)
			{
				continue; // do not fall back to truncated floor length
			}
			// 槽宽 is an interior clear width (last-run 172.45→265 = 92.55), not an
			// overall-end shelf (F215 platform 0→95) and not a truncated floor (82.55).
			double axisMin = horizontalGroove ? outline.MinX : outline.MinY;
			double axisMax = horizontalGroove ? outline.MaxX : outline.MaxY;
			if (Math.Abs(e0 - axisMin) <= envelopeBand || Math.Abs(e1 - axisMax) <= envelopeBand)
			{
				continue;
			}
			StructureFeatureAxis grooveAxis = horizontalGroove
				? StructureFeatureAxis.Horizontal
				: StructureFeatureAxis.Vertical;
			// Prefer wall-snapped span: drop truncated floor / shorter groove on same band
			// so selector cannot force-keep 82.55 as a mid-shelf next to 92.55.
			foreach (StructureFeature old in features.Where(f =>
				f.Kind != StructureFeatureKind.Overall
				&& f.Axis == grooveAxis
				&& f.IsInset
				&& !f.TouchesOverallMin
				&& !f.TouchesOverallMax
				&& f.Span + tol < gap
				&& f.Span + tol >= overall * 0.10
				&& Math.Abs(f.CrossPosition - cross) <= envelopeBand * 3).ToList())
			{
				features.Remove(old);
			}
			bool exists = features.Any(f =>
				f.Axis == grooveAxis
				&& f.Kind != StructureFeatureKind.Overall
				&& Math.Abs(f.Span - gap) <= Math.Max(tol * 20, gap * 0.06));
			if (exists)
			{
				continue;
			}
			var groove = new StructureFeature
			{
				Axis = horizontalGroove ? StructureFeatureAxis.Horizontal : StructureFeatureAxis.Vertical,
				Kind = StructureFeatureKind.RealStep,
				T0 = e0,
				T1 = e1,
				CrossPosition = cross,
				PreferredSide = ResolveGroovePreferredSide(
					features, outline, horizontalGroove, e0, e1, cross, envelopeBand, tol),
				IsInset = true,
				TouchesOverallMin = false,
				TouchesOverallMax = false,
				Confidence = 0.99,
				SourceKey = "StepGroove:" + Math.Round(gap, 2).ToString(System.Globalization.CultureInfo.InvariantCulture)
			};
			if (horizontalGroove)
			{
				groove.FirstPoint = new Point2D(e0, cross);
				groove.SecondPoint = new Point2D(e1, cross);
			}
			else
			{
				groove.FirstPoint = new Point2D(cross, e0);
				groove.SecondPoint = new Point2D(cross, e1);
			}
			features.Add(groove);
		}
	}

	/// <summary>
	/// Wall / corner stations on the groove axis (X if measuring width, Y if height).
	/// Only orthogonal walls and non-parallel corners (chamfer / diagonal / arc that
	/// actually turns). Parallel floor/arm endpoints (last-run 270 on bottom-73) must
	/// not stretch 槽宽 past the riser.
	/// </summary>
	private static List<double> CollectGrooveStations(OutlineFeature2D outline, bool horizontalGroove, double tol)
	{
		double orthoTol = Math.Max(tol * 4.0, 1.0);
		List<double> stations = new List<double>();
		foreach (Segment2D s in outline.Segments.Where(x => x != null))
		{
			double dx = Math.Abs(s.End.X - s.Start.X);
			double dy = Math.Abs(s.End.Y - s.Start.Y);
			if (horizontalGroove)
			{
				bool wall = s.IsVertical(orthoTol)
					|| (!s.IsArcChord && dx * 3.0 <= dy);
				bool turning = !s.IsHorizontal(orthoTol)
					&& (wall || dy > orthoTol);
				if (wall)
				{
					stations.Add((s.Start.X + s.End.X) * 0.5);
				}
				if (turning)
				{
					stations.Add(s.Start.X);
					stations.Add(s.End.X);
				}
			}
			else
			{
				bool wall = s.IsHorizontal(orthoTol)
					|| (!s.IsArcChord && dy * 3.0 <= dx);
				bool turning = !s.IsVertical(orthoTol)
					&& (wall || dx > orthoTol);
				if (wall)
				{
					stations.Add((s.Start.Y + s.End.Y) * 0.5);
				}
				if (turning)
				{
					stations.Add(s.Start.Y);
					stations.Add(s.End.Y);
				}
			}
		}
		return stations.Distinct().OrderBy(v => v).ToList();
	}

	/// <summary>
	/// Expand a truncated floor end by one adjacent chamfer / riser hop.
	/// Must not sweep all 1D corners in maxBridge (last-run: 260 hopped to 275 on the
	/// bottom-73 fillet instead of the 265 riser → StepGroove 102.55).
	/// </summary>
	private static double SnapFloorEndToAdjacentFace(
		OutlineFeature2D outline,
		List<Segment2D> segs,
		bool horizontalGroove,
		double t,
		double cross,
		bool expandMin,
		double maxBridge,
		double envelopeBand,
		double tol)
	{
		double orthoTol = Math.Max(tol * 4.0, 1.0);
		Point2D endPt = horizontalGroove ? new Point2D(t, cross) : new Point2D(cross, t);
		double best = t;
		bool haveHop = false;
		foreach (Segment2D s in segs)
		{
			if (s == null)
			{
				continue;
			}
			bool touchStart = PointNear(endPt, s.Start, envelopeBand);
			bool touchEnd = PointNear(endPt, s.End, envelopeBand);
			if (!touchStart && !touchEnd)
			{
				continue;
			}
			double dx = Math.Abs(s.End.X - s.Start.X);
			double dy = Math.Abs(s.End.Y - s.Start.Y);
			bool wall = horizontalGroove
				? (s.IsVertical(orthoTol) || (!s.IsArcChord && dx * 3.0 <= dy))
				: (s.IsHorizontal(orthoTol) || (!s.IsArcChord && dy * 3.0 <= dx));
			bool turning = horizontalGroove
				? !s.IsHorizontal(orthoTol) && (wall || dy > orthoTol)
				: !s.IsVertical(orthoTol) && (wall || dx > orthoTol);
			if (!wall && !turning)
			{
				continue;
			}
			Point2D far = touchStart ? s.End : s.Start;
			double farT = horizontalGroove ? far.X : far.Y;
			double hop = Math.Abs(farT - t);
			if (hop <= tol || hop > maxBridge + tol)
			{
				continue;
			}
			if (expandMin && farT < t - tol)
			{
				// Nearest outward face (larger X when going left).
				if (!haveHop || farT > best)
				{
					best = farT;
					haveHop = true;
				}
			}
			else if (!expandMin && farT > t + tol)
			{
				if (!haveHop || farT < best)
				{
					best = farT;
					haveHop = true;
				}
			}
		}
		if (haveHop)
		{
			return best;
		}
		List<double> walls = CollectOrthogonalWallStations(outline, horizontalGroove, tol);
		return SnapStationNearest(walls, t, expandMin, maxBridge, tol);
	}

	private static List<double> CollectOrthogonalWallStations(
		OutlineFeature2D outline,
		bool horizontalGroove,
		double tol)
	{
		double orthoTol = Math.Max(tol * 4.0, 1.0);
		List<double> walls = new List<double>();
		foreach (Segment2D s in outline.Segments.Where(x => x != null))
		{
			double dx = Math.Abs(s.End.X - s.Start.X);
			double dy = Math.Abs(s.End.Y - s.Start.Y);
			if (horizontalGroove)
			{
				if (s.IsVertical(orthoTol) || (!s.IsArcChord && dx * 3.0 <= dy))
				{
					walls.Add((s.Start.X + s.End.X) * 0.5);
				}
			}
			else if (s.IsHorizontal(orthoTol) || (!s.IsArcChord && dy * 3.0 <= dx))
			{
				walls.Add((s.Start.Y + s.End.Y) * 0.5);
			}
		}
		return walls.Distinct().OrderBy(v => v).ToList();
	}

	private static double SnapStationNearest(List<double> stations, double t, bool expandMin, double maxBridge, double tol)
	{
		if (stations == null || stations.Count == 0)
		{
			return t;
		}
		double best = t;
		bool found = false;
		foreach (double s in stations)
		{
			if (expandMin)
			{
				if (s < t - tol && s >= t - maxBridge - tol && (!found || s > best))
				{
					best = s;
					found = true;
				}
			}
			else if (s > t + tol && s <= t + maxBridge + tol && (!found || s < best))
			{
				best = s;
				found = true;
			}
		}
		return best;
	}

	private static double SnapStationOutward(List<double> stations, double t, bool expandMin, double maxBridge, double tol)
	{
		if (stations == null || stations.Count == 0)
		{
			return t;
		}
		if (expandMin)
		{
			// Furthest station in [t - maxBridge, t + tol]
			double best = t;
			foreach (double s in stations)
			{
				if (s <= t + tol && s >= t - maxBridge - tol && s < best - tol)
				{
					best = s;
				}
			}
			return best;
		}
		else
		{
			double best = t;
			foreach (double s in stations)
			{
				if (s >= t - tol && s <= t + maxBridge + tol && s > best + tol)
				{
					best = s;
				}
			}
			return best;
		}
	}

	/// <summary>
	/// Emit 槽宽 as clear gap between adjacent step faces.
	/// Prefer raw outline segments (incl. short risers) so CAD does not depend on
	/// floor edges that CornerFeature/OS often delete (last-run: 82.55 OS only, no Structure).
	/// Only recover along the long envelope axis.
	/// </summary>
	private void RecoverStepGrooveWidths(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		double envelopeBand,
		double tol)
	{
		if (outline?.Segments == null)
		{
			return;
		}
		// Groove partitions the long envelope axis (F338 steps along 338).
		if (outline.Width >= outline.Height)
		{
			RecoverGroovesFromOutlineSegments(features, outline, verticalWalls: true, envelopeBand, tol);
			RecoverGroovesFromFeatureWalls(
				features, outline,
				wallAxis: StructureFeatureAxis.Vertical,
				grooveAxis: StructureFeatureAxis.Horizontal,
				envelopeBand, tol);
		}
		else
		{
			RecoverGroovesFromOutlineSegments(features, outline, verticalWalls: false, envelopeBand, tol);
			RecoverGroovesFromFeatureWalls(
				features, outline,
				wallAxis: StructureFeatureAxis.Horizontal,
				grooveAxis: StructureFeatureAxis.Vertical,
				envelopeBand, tol);
		}
	}

	/// <summary>
	/// Cluster outline walls by cross-position; adjacent clusters define 槽宽.
	/// </summary>
	private void RecoverGroovesFromOutlineSegments(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		bool verticalWalls,
		double envelopeBand,
		double tol)
	{
		double overallGroove = verticalWalls ? outline.Width : outline.Height;
		double overallWall = verticalWalls ? outline.Height : outline.Width;
		StructureFeatureAxis grooveAxis = verticalWalls
			? StructureFeatureAxis.Horizontal
			: StructureFeatureAxis.Vertical;

		// Collect wall intervals: (crossPos, t0, t1). Soft ortho for CAD skew + short filleted risers.
		double orthoTol = Math.Max(tol * 4.0, 1.0);
		double minWall = Math.Max(12.0, overallWall * 0.06); // CAD risers may be ~15–50 after fillet
		List<Tuple<double, double, double>> raw = new List<Tuple<double, double, double>>();
		foreach (Segment2D s in outline.Segments)
		{
			if (s == null)
			{
				continue;
			}
			double dx = Math.Abs(s.End.X - s.Start.X);
			double dy = Math.Abs(s.End.Y - s.Start.Y);
			if (verticalWalls)
			{
				// Prefer vertical; allow mild skew (fillet-adjacent stubs)
				bool mostlyVertical = dy >= dx * 3.0 && dy >= minWall - tol;
				if (!s.IsVertical(orthoTol) && !mostlyVertical)
				{
					continue;
				}
				if (s.IsArcChord && !mostlyVertical)
				{
					continue;
				}
				double x = (s.Start.X + s.End.X) * 0.5;
				double y0 = Math.Min(s.Start.Y, s.End.Y);
				double y1 = Math.Max(s.Start.Y, s.End.Y);
				double wallLen = y1 - y0;
				if (wallLen + tol < minWall)
				{
					continue;
				}
				// Skip full-height outer walls (left/right overall face)
				if (wallLen + tol >= overallWall * 0.85)
				{
					continue;
				}
				// 槽宽 walls must be inset; envelope face + platform riser is F215 95, not a groove.
				if (Math.Abs(x - outline.MinX) <= envelopeBand || Math.Abs(x - outline.MaxX) <= envelopeBand)
				{
					continue;
				}
				raw.Add(Tuple.Create(x, y0, y1));
			}
			else
			{
				bool mostlyHorizontal = dx >= dy * 3.0 && dx >= minWall - tol;
				if (!s.IsHorizontal(orthoTol) && !mostlyHorizontal)
				{
					continue;
				}
				if (s.IsArcChord && !mostlyHorizontal)
				{
					continue;
				}
				double y = (s.Start.Y + s.End.Y) * 0.5;
				double x0 = Math.Min(s.Start.X, s.End.X);
				double x1 = Math.Max(s.Start.X, s.End.X);
				double wallLen = x1 - x0;
				// Rotated risers are moderate length; skip long floors/arms (305, 177).
				if (wallLen + tol < minWall || wallLen + tol >= overallWall * 0.40)
				{
					continue;
				}
				if (Math.Abs(y - outline.MinY) <= envelopeBand || Math.Abs(y - outline.MaxY) <= envelopeBand)
				{
					continue;
				}
				raw.Add(Tuple.Create(y, x0, x1));
			}
		}
		if (raw.Count < 2)
		{
			return;
		}
		// Cluster by cross position
		List<List<Tuple<double, double, double>>> clusters = new List<List<Tuple<double, double, double>>>();
		foreach (var w in raw.OrderBy(t => t.Item1))
		{
			List<Tuple<double, double, double>> c = clusters.FirstOrDefault(cl =>
				Math.Abs(cl.Average(x => x.Item1) - w.Item1) <= envelopeBand);
			if (c == null)
			{
				clusters.Add(new List<Tuple<double, double, double>> { w });
			}
			else
			{
				c.Add(w);
			}
		}
		List<Tuple<double, double, double>> stations = clusters
			.Select(cl =>
			{
				double cross = cl.Average(x => x.Item1);
				double t0 = cl.Min(x => x.Item2);
				double t1 = cl.Max(x => x.Item3);
				return Tuple.Create(cross, t0, t1);
			})
			.OrderBy(t => t.Item1)
			.ToList();

		double envMin = verticalWalls ? outline.MinX : outline.MinY;
		double envMax = verticalWalls ? outline.MaxX : outline.MaxY;
		for (int i = 0; i < stations.Count - 1; i++)
		{
			double x0 = stations[i].Item1;
			double x1 = stations[i + 1].Item1;
			double gap = x1 - x0;
			if (gap + tol < overallGroove * 0.12 || gap + tol >= overallGroove * 0.50)
			{
				continue;
			}
			if (Math.Abs(x0 - envMin) <= envelopeBand || Math.Abs(x0 - envMax) <= envelopeBand
				|| Math.Abs(x1 - envMin) <= envelopeBand || Math.Abs(x1 - envMax) <= envelopeBand)
			{
				continue;
			}
			double a0 = stations[i].Item2;
			double a1 = stations[i].Item3;
			double b0 = stations[i + 1].Item2;
			double b1 = stations[i + 1].Item3;
			double o0 = Math.Max(a0, b0);
			double o1 = Math.Min(a1, b1);
			// Allow near-abutting wall ranges (fillet may shrink overlap)
			if (o1 + envelopeBand < o0)
			{
				// no overlap even with band — try midpoint band if both walls have length
				if (a1 - a0 < tol || b1 - b0 < tol)
				{
					continue;
				}
				o0 = (Math.Min(a0, b0) + Math.Max(a1, b1)) * 0.5 - overallWall * 0.02;
				o1 = o0 + overallWall * 0.04;
			}
			else
			{
				o0 = Math.Max(o0, Math.Min(a0, b0));
				o1 = Math.Min(o1 <= o0 ? o0 + tol : o1, Math.Max(a1, b1));
			}
			bool exists = features.Any(f =>
				f.Axis == grooveAxis
				&& f.Kind != StructureFeatureKind.Overall
				&& Math.Abs(f.Span - gap) <= Math.Max(tol * 20, gap * 0.05));
			if (exists)
			{
				continue;
			}
			double cross = (Math.Max(stations[i].Item2, stations[i + 1].Item2)
				+ Math.Min(stations[i].Item3, stations[i + 1].Item3)) * 0.5;
			if (double.IsNaN(cross) || o1 < o0)
			{
				cross = (stations[i].Item2 + stations[i].Item3
					+ stations[i + 1].Item2 + stations[i + 1].Item3) * 0.25;
			}
			bool inset = verticalWalls
				? (Math.Abs(cross - outline.MinY) > envelopeBand && Math.Abs(cross - outline.MaxY) > envelopeBand)
				: (Math.Abs(cross - outline.MinX) > envelopeBand && Math.Abs(cross - outline.MaxX) > envelopeBand);
			var groove = new StructureFeature
			{
				Axis = grooveAxis,
				Kind = StructureFeatureKind.RealStep,
				T0 = x0,
				T1 = x1,
				CrossPosition = cross,
				PreferredSide = ResolveGroovePreferredSide(
					features, outline, verticalWalls, x0, x1, cross, envelopeBand, tol),
				IsInset = true,
				TouchesOverallMin = false,
				TouchesOverallMax = false,
				Confidence = 0.98,
				SourceKey = "StepGroove:" + Math.Round(gap, 2).ToString(System.Globalization.CultureInfo.InvariantCulture)
			};
			if (verticalWalls)
			{
				groove.FirstPoint = new Point2D(x0, cross);
				groove.SecondPoint = new Point2D(x1, cross);
			}
			else
			{
				groove.FirstPoint = new Point2D(cross, x0);
				groove.SecondPoint = new Point2D(cross, x1);
			}
			features.Add(groove);
		}
	}

	private void RecoverGroovesFromFeatureWalls(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		StructureFeatureAxis wallAxis,
		StructureFeatureAxis grooveAxis,
		double envelopeBand,
		double tol)
	{
		double overallGroove = grooveAxis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
		double overallWall = wallAxis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
		double envMin = wallAxis == StructureFeatureAxis.Vertical ? outline.MinX : outline.MinY;
		double envMax = wallAxis == StructureFeatureAxis.Vertical ? outline.MaxX : outline.MaxY;
		List<StructureFeature> walls = features
			.Where(f => f.Axis == wallAxis
				&& f.Kind != StructureFeatureKind.Overall
				&& f.SourceKey != null
				&& !f.SourceKey.StartsWith("StepGroove", StringComparison.Ordinal)
				&& f.Span + tol < overallWall * 0.75
				&& f.Span + tol >= Math.Max(tol * 8.0, overallWall * 0.05)
				&& Math.Abs(f.CrossPosition - envMin) > envelopeBand
				&& Math.Abs(f.CrossPosition - envMax) > envelopeBand)
			.OrderBy(f => f.CrossPosition)
			.ToList();
		if (walls.Count < 2)
		{
			return;
		}
		for (int i = 0; i < walls.Count - 1; i++)
		{
			StructureFeature a = walls[i];
			StructureFeature b = walls[i + 1];
			double gap = b.CrossPosition - a.CrossPosition;
			if (gap + tol < overallGroove * 0.12 || gap + tol >= overallGroove * 0.50)
			{
				continue;
			}
			double a0 = Math.Min(a.T0, a.T1);
			double a1 = Math.Max(a.T0, a.T1);
			double b0 = Math.Min(b.T0, b.T1);
			double b1 = Math.Max(b.T0, b.T1);
			double o0 = Math.Max(a0, b0);
			double o1 = Math.Min(a1, b1);
			if (o1 - o0 + tol < Math.Max(tol * 8.0, overallWall * 0.04))
			{
				continue;
			}
			bool exists = features.Any(f =>
				f.Axis == grooveAxis
				&& f.Kind != StructureFeatureKind.Overall
				&& Math.Abs(f.Span - gap) <= Math.Max(tol * 20, gap * 0.05));
			if (exists)
			{
				continue;
			}
			double cross = (o0 + o1) * 0.5;
			var groove = new StructureFeature
			{
				Axis = grooveAxis,
				Kind = StructureFeatureKind.RealStep,
				T0 = a.CrossPosition,
				T1 = b.CrossPosition,
				CrossPosition = cross,
				PreferredSide = ResolveGroovePreferredSide(
					features, outline, grooveAxis == StructureFeatureAxis.Horizontal,
					a.CrossPosition, b.CrossPosition, cross, envelopeBand, tol),
				IsInset = true,
				TouchesOverallMin = false,
				TouchesOverallMax = false,
				Confidence = 0.97,
				SourceKey = "StepGroove:" + Math.Round(gap, 2).ToString(System.Globalization.CultureInfo.InvariantCulture)
			};
			if (grooveAxis == StructureFeatureAxis.Horizontal)
			{
				groove.FirstPoint = new Point2D(a.CrossPosition, cross);
				groove.SecondPoint = new Point2D(b.CrossPosition, cross);
			}
			else
			{
				groove.FirstPoint = new Point2D(cross, a.CrossPosition);
				groove.SecondPoint = new Point2D(cross, b.CrossPosition);
			}
			features.Add(groove);
		}
	}

	/// <summary>
	/// 305-class: span ≥85% overall and residual is a short tip (12 … 12% overall).
	/// </summary>
	private static bool IsLongArmWithTipResidual(StructureFeature f, double overall, double tol)
	{
		if (f == null || overall <= tol)
		{
			return false;
		}
		double residual = overall - f.Span;
		return f.Span + tol >= overall * 0.85
			&& residual + tol >= 12.0
			&& residual <= overall * 0.12 + tol;
	}

	/// <summary>
	/// Expand each H/V RealStep through short arc/chamfer bridges to the true station
	/// at the next orthogonal wall or collinear peer (recovers fillet-truncated spans).
	/// </summary>
	private static void ExpandFeaturesAcrossFilletBridges(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		double maxBridge,
		double envelopeBand,
		double tol)
	{
		if (features == null || outline?.Segments == null)
		{
			return;
		}
		List<Segment2D> segs = outline.Segments.Where(s => s != null).ToList();
		if (segs.Count == 0)
		{
			return;
		}
		foreach (StructureFeature f in features.Where(x => x.Kind == StructureFeatureKind.RealStep).ToList())
		{
			double overallOnAxis = f.Axis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
			// Inset floors / notch openings keep raw span; 槽宽 is recovered separately.
			// Expanding F215 notch 50 through 3mm chamfers made 53 and hid the opening.
			if (f.IsInset)
			{
				continue;
			}
			// Outer tips (20) must not be expanded into the full foot wall.
			if (f.Span + tol < overallOnAxis * 0.15)
			{
				continue;
			}
			if (f.Axis == StructureFeatureAxis.Horizontal)
			{
				double y = f.CrossPosition;
				double t0 = Math.Min(f.T0, f.T1);
				double t1 = Math.Max(f.T0, f.T1);
				double e0 = ExpandAlongMeasurementAxis(segs, t0, y, horizontalMeasure: true, expandMin: true, maxBridge, envelopeBand, tol);
				double e1 = ExpandAlongMeasurementAxis(segs, t1, y, horizontalMeasure: true, expandMin: false, maxBridge, envelopeBand, tol);
				double newSpan = e1 - e0;
				// Reject expansion that swallows the full overall (would erase step partitions).
				if (newSpan > f.Span + tol && Math.Abs(newSpan - outline.Width) > maxBridge + tol)
				{
					f.T0 = e0;
					f.T1 = e1;
					f.FirstPoint = new Point2D(e0, y);
					f.SecondPoint = new Point2D(e1, y);
					f.TouchesOverallMin = Math.Abs(e0 - outline.MinX) <= envelopeBand;
					f.TouchesOverallMax = Math.Abs(e1 - outline.MaxX) <= envelopeBand;
					f.SourceKey = (f.SourceKey ?? "") + "+Expanded";
					f.Confidence = Math.Max(f.Confidence, 0.92);
				}
			}
			else
			{
				double x = f.CrossPosition;
				double t0 = Math.Min(f.T0, f.T1);
				double t1 = Math.Max(f.T0, f.T1);
				double e0 = ExpandAlongMeasurementAxis(segs, t0, x, horizontalMeasure: false, expandMin: true, maxBridge, envelopeBand, tol);
				double e1 = ExpandAlongMeasurementAxis(segs, t1, x, horizontalMeasure: false, expandMin: false, maxBridge, envelopeBand, tol);
				double newSpan = e1 - e0;
				if (newSpan > f.Span + tol && Math.Abs(newSpan - outline.Height) > maxBridge + tol)
				{
					f.T0 = e0;
					f.T1 = e1;
					f.FirstPoint = new Point2D(x, e0);
					f.SecondPoint = new Point2D(x, e1);
					f.TouchesOverallMin = Math.Abs(e0 - outline.MinY) <= envelopeBand;
					f.TouchesOverallMax = Math.Abs(e1 - outline.MaxY) <= envelopeBand;
					f.SourceKey = (f.SourceKey ?? "") + "+Expanded";
					f.Confidence = Math.Max(f.Confidence, 0.92);
				}
			}
		}
	}

	/// <summary>
	/// From station (coord on measure axis, cross on the other), walk short bridges
	/// and collinear peers to push the station outward (min or max).
	/// </summary>
	private static double ExpandAlongMeasurementAxis(
		List<Segment2D> segs,
		double station,
		double cross,
		bool horizontalMeasure,
		bool expandMin,
		double maxBridge,
		double envelopeBand,
		double tol)
	{
		double extreme = station;
		// Seed point in 2D
		double seedX = horizontalMeasure ? station : cross;
		double seedY = horizontalMeasure ? cross : station;
		HashSet<int> visited = new HashSet<int>();
		List<Point2D> frontier = new List<Point2D> { new Point2D(seedX, seedY) };
		int guard = 0;
		while (frontier.Count > 0 && guard++ < 48)
		{
			Point2D p = frontier[frontier.Count - 1];
			frontier.RemoveAt(frontier.Count - 1);
			for (int i = 0; i < segs.Count; i++)
			{
				Segment2D s = segs[i];
				bool touchStart = PointNear(p, s.Start, envelopeBand);
				bool touchEnd = PointNear(p, s.End, envelopeBand);
				if (!touchStart && !touchEnd)
				{
					continue;
				}
				int edgeKey = i * 2 + (touchStart ? 0 : 1);
				if (visited.Contains(edgeKey))
				{
					continue;
				}
				visited.Add(edgeKey);
				Point2D far = touchStart ? s.End : s.Start;
				// Tight band: F215 notch floor at Y=97 must not count as collinear with top Y=100.
				double collinearBand = Math.Max(tol * 8.0, 2.0);
				bool collinearPeer = horizontalMeasure
					? s.IsHorizontal(tol) && Math.Abs(((s.Start.Y + s.End.Y) * 0.5) - cross) <= collinearBand
					: s.IsVertical(tol) && Math.Abs(((s.Start.X + s.End.X) * 0.5) - cross) <= collinearBand;
				bool shortBridge = s.IsArcChord
					|| (s.Length <= maxBridge + tol
						&& !((horizontalMeasure && s.IsVertical(tol)) || (!horizontalMeasure && s.IsHorizontal(tol))));
				bool orthogonalStop = horizontalMeasure
					? s.IsVertical(tol)
					: s.IsHorizontal(tol);

				if (orthogonalStop && s.Length > tol * 4)
				{
					// Station at the wall's measure-axis coordinate
					double wallT = horizontalMeasure ? s.Start.X : s.Start.Y;
					if (expandMin && wallT < extreme - tol)
					{
						extreme = wallT;
					}
					if (!expandMin && wallT > extreme + tol)
					{
						extreme = wallT;
					}
					continue;
				}
				if (collinearPeer || shortBridge)
				{
					double farT = horizontalMeasure ? far.X : far.Y;
					if (expandMin && farT < extreme - tol)
					{
						extreme = farT;
						frontier.Add(far);
					}
					else if (!expandMin && farT > extreme + tol)
					{
						extreme = farT;
						frontier.Add(far);
					}
					else if (Math.Abs(farT - extreme) <= envelopeBand)
					{
						frontier.Add(far);
					}
				}
			}
		}
		return extreme;
	}

	private static bool PointNear(Point2D a, Point2D b, double band)
	{
		double dx = a.X - b.X;
		double dy = a.Y - b.Y;
		return dx * dx + dy * dy <= band * band;
	}

	/// <summary>
	/// Merge collinear RealSteps on the same axis whose cross-positions match and intervals
	/// abut within microGap (fillet/chamfer break). Replaces pieces with one synthetic edge.
	/// </summary>
	private static void MergeCollinearRealSteps(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		double microGap,
		double envelopeBand,
		double tol)
	{
		ReplaceWithMergedCollinear(features, outline, StructureFeatureAxis.Horizontal, microGap, envelopeBand, tol);
		ReplaceWithMergedCollinear(features, outline, StructureFeatureAxis.Vertical, microGap, envelopeBand, tol);
	}

	private static void ReplaceWithMergedCollinear(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		StructureFeatureAxis axis,
		double microGap,
		double envelopeBand,
		double tol)
	{
		double overall = axis == StructureFeatureAxis.Horizontal ? outline.Width : outline.Height;
		List<StructureFeature> members = features
			.Where(f => f.Axis == axis && f.Kind == StructureFeatureKind.RealStep)
			.ToList();
		if (members.Count < 2)
		{
			return;
		}
		foreach (StructureFeature m in members)
		{
			features.Remove(m);
		}
		List<List<StructureFeature>> bands = new List<List<StructureFeature>>();
		foreach (StructureFeature f in members.OrderBy(f => f.CrossPosition).ThenBy(f => Math.Min(f.T0, f.T1)))
		{
			// True collinear only. envelopeBand (~8) would glue CAD body Y=101.5 to
			// mid-floor Y=106.5 and swallow 槽宽.
			double collinearBand = Math.Max(tol * 8.0, 2.0);
			List<StructureFeature> band = bands.FirstOrDefault(b =>
				Math.Abs(b[0].CrossPosition - f.CrossPosition) <= collinearBand);
			if (band == null)
			{
				bands.Add(new List<StructureFeature> { f });
			}
			else
			{
				band.Add(f);
			}
		}
		foreach (List<StructureFeature> band in bands)
		{
			List<StructureFeature> ordered = band.OrderBy(f => Math.Min(f.T0, f.T1)).ToList();
			double t0 = Math.Min(ordered[0].T0, ordered[0].T1);
			double t1 = Math.Max(ordered[0].T0, ordered[0].T1);
			double cross = ordered[0].CrossPosition;
			bool tMin = ordered[0].TouchesOverallMin;
			bool tMax = ordered[0].TouchesOverallMax;
			bool inset = ordered[0].IsInset;
			double conf = ordered[0].Confidence;
			DimensionSide side = ordered[0].PreferredSide;
			List<string> keys = new List<string>();
			if (ordered[0].SupportSourceKeys != null)
			{
				keys.AddRange(ordered[0].SupportSourceKeys);
			}
			string src = ordered[0].SourceKey ?? "";
			for (int i = 1; i < ordered.Count; i++)
			{
				StructureFeature next = ordered[i];
				double n0 = Math.Min(next.T0, next.T1);
				double n1 = Math.Max(next.T0, next.T1);
				double curSpan = t1 - t0;
				double nextSpan = n1 - n0;
				// Do not absorb outer short tips (foot 20) into longer collinear walls (80→100).
				double mergedSpan = Math.Max(t1, n1) - Math.Min(t0, n0);
				// Never synthesize a full-overall edge (would delete real step partition e.g. 95+120).
				bool becomesOverall = Math.Abs(mergedSpan - overall) <= microGap + tol;
				// Outer tips may merge with collinear envelope wall to form full foot (20+80→100),
				// while we re-emit the tip piece afterward so Golden338Cad keeps both 20 and 100.
				bool canMerge = n0 <= t1 + microGap + tol && !becomesOverall;
				if (canMerge)
				{
					t0 = Math.Min(t0, n0);
					t1 = Math.Max(t1, n1);
					cross = (cross + next.CrossPosition) * 0.5;
					tMin = tMin || next.TouchesOverallMin;
					tMax = tMax || next.TouchesOverallMax;
					inset = inset && next.IsInset;
					conf = Math.Max(conf, next.Confidence);
					src = "Composite:" + src + "+" + (next.SourceKey ?? "");
					if (next.SupportSourceKeys != null)
					{
						keys.AddRange(next.SupportSourceKeys);
					}
				}
				else
				{
					features.Add(MakeMergedStep(axis, t0, t1, cross, side, inset, tMin, tMax, conf, src, keys));
					t0 = n0;
					t1 = n1;
					cross = next.CrossPosition;
					tMin = next.TouchesOverallMin;
					tMax = next.TouchesOverallMax;
					inset = next.IsInset;
					conf = next.Confidence;
					side = next.PreferredSide;
					src = next.SourceKey ?? "";
					keys = next.SupportSourceKeys != null
						? new List<string>(next.SupportSourceKeys)
						: new List<string>();
				}
			}
			features.Add(MakeMergedStep(axis, t0, t1, cross, side, inset, tMin, tMax, conf, src, keys));
			// Re-emit outer envelope tips that were absorbed into a longer collinear wall.
			foreach (StructureFeature tip in ordered)
			{
				if (tip.IsInset || tip.Span + tol >= overall * 0.15 || tip.Span + tol < 5.0)
				{
					continue;
				}
				bool already = features.Any(f =>
					f.Axis == axis
					&& Math.Abs(f.Span - tip.Span) <= tol
					&& Math.Abs(f.CrossPosition - tip.CrossPosition) <= envelopeBand
					&& Math.Abs(Math.Min(f.T0, f.T1) - Math.Min(tip.T0, tip.T1)) <= tol);
				if (!already)
				{
					features.Add(MakeMergedStep(
						axis,
						Math.Min(tip.T0, tip.T1),
						Math.Max(tip.T0, tip.T1),
						tip.CrossPosition,
						tip.PreferredSide,
						tip.IsInset,
						tip.TouchesOverallMin,
						tip.TouchesOverallMax,
						Math.Max(tip.Confidence, 0.95),
						tip.SourceKey ?? "OuterTip",
						tip.SupportSourceKeys != null ? tip.SupportSourceKeys.ToList() : new List<string>()));
				}
			}
		}
	}

	/// <summary>
	/// C10 leaves a 32 envelope stub (58–90). Emit the complementary residual to the
	/// AABB (58–100 = 42) without mutating the stub, then nested-interval drops 32.
	/// </summary>
	private static void AddChamferComplementResiduals(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		double envelopeBand,
		double minSpan,
		double tol)
	{
		if (features == null || outline == null)
		{
			return;
		}
		double chamferBand = Math.Max(8.0, Math.Min(outline.Width, outline.Height) * 0.12);
		foreach (StructureFeature f in features.Where(x =>
			x.Kind == StructureFeatureKind.RealStep && !x.IsInset).ToList())
		{
			double t0 = Math.Min(f.T0, f.T1);
			double t1 = Math.Max(f.T0, f.T1);
			if (f.Axis == StructureFeatureAxis.Vertical)
			{
				bool onEnv = Math.Abs(f.CrossPosition - outline.MinX) <= envelopeBand
					|| Math.Abs(f.CrossPosition - outline.MaxX) <= envelopeBand;
				if (!onEnv)
				{
					continue;
				}
				double gapMax = outline.MaxY - t1;
				double gapMin = t0 - outline.MinY;
				if (gapMax > tol && gapMax <= chamferBand)
				{
					TryAddResidual(features, outline, StructureFeatureAxis.Vertical,
						f.CrossPosition, t0, outline.MaxY,
						Math.Abs(f.CrossPosition - outline.MinX) <= envelopeBand
							? DimensionSide.Left : DimensionSide.Right,
						minSpan, envelopeBand, tol, "ChamferComplement:");
				}
				if (gapMin > tol && gapMin <= chamferBand)
				{
					TryAddResidual(features, outline, StructureFeatureAxis.Vertical,
						f.CrossPosition, outline.MinY, t1,
						Math.Abs(f.CrossPosition - outline.MinX) <= envelopeBand
							? DimensionSide.Left : DimensionSide.Right,
						minSpan, envelopeBand, tol, "ChamferComplement:");
				}
			}
			else
			{
				bool onEnv = Math.Abs(f.CrossPosition - outline.MinY) <= envelopeBand
					|| Math.Abs(f.CrossPosition - outline.MaxY) <= envelopeBand;
				if (!onEnv)
				{
					continue;
				}
				double gapMax = outline.MaxX - t1;
				double gapMin = t0 - outline.MinX;
				if (gapMax > tol && gapMax <= chamferBand)
				{
					TryAddResidual(features, outline, StructureFeatureAxis.Horizontal,
						f.CrossPosition, t0, outline.MaxX,
						Math.Abs(f.CrossPosition - outline.MinY) <= envelopeBand
							? DimensionSide.Bottom : DimensionSide.Top,
						minSpan, envelopeBand, tol, "ChamferComplement:");
				}
				if (gapMin > tol && gapMin <= chamferBand)
				{
					TryAddResidual(features, outline, StructureFeatureAxis.Horizontal,
						f.CrossPosition, outline.MinX, t1,
						Math.Abs(f.CrossPosition - outline.MinY) <= envelopeBand
							? DimensionSide.Bottom : DimensionSide.Top,
						minSpan, envelopeBand, tol, "ChamferComplement:");
				}
			}
		}
	}

	private static void TryAddResidual(
		List<StructureFeature> features,
		OutlineFeature2D outline,
		StructureFeatureAxis axis,
		double cross,
		double t0,
		double t1,
		DimensionSide side,
		double minSpan,
		double envelopeBand,
		double tol,
		string keyPrefix)
	{
		double a0 = Math.Min(t0, t1);
		double a1 = Math.Max(t0, t1);
		double span = a1 - a0;
		if (span + tol < minSpan)
		{
			return;
		}
		bool exists = axis == StructureFeatureAxis.Vertical
			? HasVerticalEdgeNear(features, cross, a0, a1, tol)
			: HasHorizontalEdgeNear(features, cross, a0, a1, tol);
		if (exists)
		{
			return;
		}
		bool tMin = axis == StructureFeatureAxis.Vertical
			? Math.Abs(a0 - outline.MinY) <= envelopeBand
			: Math.Abs(a0 - outline.MinX) <= envelopeBand;
		bool tMax = axis == StructureFeatureAxis.Vertical
			? Math.Abs(a1 - outline.MaxY) <= envelopeBand
			: Math.Abs(a1 - outline.MaxX) <= envelopeBand;
		var r = new StructureFeature
		{
			Axis = axis,
			Kind = StructureFeatureKind.RealStep,
			T0 = a0,
			T1 = a1,
			CrossPosition = cross,
			PreferredSide = side,
			IsInset = false,
			TouchesOverallMin = tMin,
			TouchesOverallMax = tMax,
			Confidence = 0.94,
			SourceKey = keyPrefix + Math.Round(span, 2).ToString(System.Globalization.CultureInfo.InvariantCulture)
		};
		if (axis == StructureFeatureAxis.Vertical)
		{
			r.FirstPoint = new Point2D(cross, a0);
			r.SecondPoint = new Point2D(cross, a1);
		}
		else
		{
			r.FirstPoint = new Point2D(a0, cross);
			r.SecondPoint = new Point2D(a1, cross);
		}
		features.Add(r);
	}

	private static StructureFeature MakeMergedStep(
		StructureFeatureAxis axis,
		double t0,
		double t1,
		double cross,
		DimensionSide side,
		bool inset,
		bool tMin,
		bool tMax,
		double conf,
		string src,
		List<string> keys)
	{
		var f = new StructureFeature
		{
			Axis = axis,
			Kind = StructureFeatureKind.RealStep,
			T0 = t0,
			T1 = t1,
			CrossPosition = cross,
			PreferredSide = side,
			IsInset = inset,
			TouchesOverallMin = tMin,
			TouchesOverallMax = tMax,
			Confidence = conf,
			SourceKey = src
		};
		if (keys != null)
		{
			foreach (string k in keys.Distinct())
			{
				if (!string.IsNullOrEmpty(k))
				{
					f.SupportSourceKeys.Add(k);
				}
			}
		}
		if (axis == StructureFeatureAxis.Horizontal)
		{
			f.FirstPoint = new Point2D(t0, cross);
			f.SecondPoint = new Point2D(t1, cross);
		}
		else
		{
			f.FirstPoint = new Point2D(cross, t0);
			f.SecondPoint = new Point2D(cross, t1);
		}
		return f;
	}

}
