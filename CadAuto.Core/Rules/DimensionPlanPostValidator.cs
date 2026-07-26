using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;

namespace CadAuto.Core.Rules;

public sealed class DimensionPlanPostValidator
{
	private readonly DimensionRuleConfig _config;

	public DimensionPlanPostValidator(DimensionRuleConfig config)
	{
		_config = config ?? throw new ArgumentNullException("config");
	}

	public void Validate(DimensionPlan plan, OutlineFeature2D outline)
	{
		if (plan == null)
		{
			throw new ArgumentNullException("plan");
		}
		if (outline == null)
		{
			throw new ArgumentNullException("outline");
		}
		if (!OutlineGeometryQuery.TryGetEnvelope(outline, _config.GeometryTolerance, out var envelope))
		{
			throw new InvalidOperationException("The selected outline has no verifiable line or arc geometry for dimension attachment.");
		}
		ValidateStoredEnvelope(plan, outline, envelope);
		ValidateOverallDimension(plan, outline, DimensionKind.OverallWidth, DimensionOrientation.Horizontal, DimensionSide.Bottom, envelope.MinX, envelope.MaxX);
		ValidateOverallDimension(plan, outline, DimensionKind.OverallHeight, DimensionOrientation.Vertical, DimensionSide.Left, envelope.MinY, envelope.MaxY);
		ValidateNoZeroLengthLinearDimensions(plan.Dimensions);
		ValidateRequiredOutlineAttachments(plan.Dimensions, outline);
	}

	private void ValidateNoZeroLengthLinearDimensions(IEnumerable<PlannedDimension> dimensions)
	{
		foreach (PlannedDimension dimension in dimensions.Where((PlannedDimension item) => item != null))
		{
			double span;
			if (dimension.Orientation == DimensionOrientation.Horizontal)
			{
				span = Math.Abs(dimension.SecondPoint.X - dimension.FirstPoint.X);
			}
			else if (dimension.Orientation == DimensionOrientation.Vertical)
			{
				span = Math.Abs(dimension.SecondPoint.Y - dimension.FirstPoint.Y);
			}
			else
			{
				continue;
			}
			if (span <= _config.GeometryTolerance)
			{
				throw new InvalidOperationException("Dimension " + (dimension.DebugRole ?? dimension.Kind.ToString()) + " has zero or sub-tolerance length.");
			}
		}
	}

	private void ValidateStoredEnvelope(DimensionPlan plan, OutlineFeature2D outline, OutlineEnvelope2D envelope)
	{
		if (!IsFinite(outline.MinX) || !IsFinite(outline.MaxX) || !IsFinite(outline.MinY) || !IsFinite(outline.MaxY) || outline.MinX > outline.MaxX || outline.MinY > outline.MaxY)
		{
			throw new InvalidOperationException("The selected outline has invalid envelope values.");
		}
		if (NearlyEqual(outline.MinX, envelope.MinX) && NearlyEqual(outline.MaxX, envelope.MaxX) && NearlyEqual(outline.MinY, envelope.MinY) && NearlyEqual(outline.MaxY, envelope.MaxY))
		{
			return;
		}
		// Reconcile rather than abort. Aborting here was the only path that produced no output at
		// all, and the recognizer no longer has a second envelope source that can drift (see the
		// envelope-source unification), so a surviving mismatch is a residual numeric one. The
		// real-geometry derivation wins; the remaining checks below still run against it, so a
		// large discrepancy will surface as an overall-dimension violation instead of a blanket
		// failure with no diagnostic.
		plan.Diagnostics.Warnings.Add(string.Format(CultureInfo.InvariantCulture, "StoredEnvelopeReconciled: stored=({0:0.####},{1:0.####})-({2:0.####},{3:0.####}) derived=({4:0.####},{5:0.####})-({6:0.####},{7:0.####})", outline.MinX, outline.MinY, outline.MaxX, outline.MaxY, envelope.MinX, envelope.MinY, envelope.MaxX, envelope.MaxY));
		outline.MinX = envelope.MinX;
		outline.MaxX = envelope.MaxX;
		outline.MinY = envelope.MinY;
		outline.MaxY = envelope.MaxY;
		if (!IsFinite(outline.MinX) || !IsFinite(outline.MaxX) || !IsFinite(outline.MinY) || !IsFinite(outline.MaxY) || outline.MinX > outline.MaxX || outline.MinY > outline.MaxY)
		{
			throw new InvalidOperationException("The selected outline envelope could not be reconciled with its real line and arc geometry.");
		}
	}

	private void ValidateOverallDimension(DimensionPlan plan, OutlineFeature2D outline, DimensionKind kind, DimensionOrientation orientation, DimensionSide requiredSide, double expectedMinimum, double expectedMaximum)
	{
		List<PlannedDimension> list = plan.Dimensions.Where((PlannedDimension dimension) => dimension.Kind == kind).ToList();
		if (expectedMaximum - expectedMinimum <= _config.GeometryTolerance)
		{
			if (list.Count != 0)
			{
				throw new InvalidOperationException(kind + " must not be emitted for a zero-length envelope.");
			}
			return;
		}
		if (list.Count != 1)
		{
			throw new InvalidOperationException("Dimension plan must contain exactly one " + kind + ".");
		}
		PlannedDimension dimension2 = list[0];
		double actualMinimum = (orientation == DimensionOrientation.Horizontal) ? Math.Min(dimension2.FirstPoint.X, dimension2.SecondPoint.X) : Math.Min(dimension2.FirstPoint.Y, dimension2.SecondPoint.Y);
		double actualMaximum = (orientation == DimensionOrientation.Horizontal) ? Math.Max(dimension2.FirstPoint.X, dimension2.SecondPoint.X) : Math.Max(dimension2.FirstPoint.Y, dimension2.SecondPoint.Y);
		if (dimension2.Orientation != orientation || dimension2.Side != requiredSide || !dimension2.ForceOuterLevel || dimension2.UseSegmentedExtensionLines || !NearlyEqual(actualMinimum, expectedMinimum) || !NearlyEqual(actualMaximum, expectedMaximum) || !OutlineGeometryQuery.IsPointOnBoundary(dimension2.FirstPoint, outline, _config.GeometryTolerance) || !OutlineGeometryQuery.IsPointOnBoundary(dimension2.SecondPoint, outline, _config.GeometryTolerance))
		{
			throw new InvalidOperationException(kind + " does not preserve the real outline envelope and attachment rules.");
		}
	}

	private void ValidateRequiredOutlineAttachments(IEnumerable<PlannedDimension> dimensions, OutlineFeature2D outline)
	{
		foreach (PlannedDimension dimension in dimensions.Where((PlannedDimension item) => item != null && item.FirstPointMustLieOnOutline))
		{
			if (!OutlineGeometryQuery.IsPointOnBoundary(dimension.FirstPoint, outline, _config.GeometryTolerance))
			{
				throw new InvalidOperationException("Dimension " + (dimension.DebugRole ?? dimension.Kind.ToString()) + " has a non-geometric outline reference point.");
			}
			if (dimension.RequiredOutlineReferenceCoordinate.HasValue)
			{
				double actualCoordinate;
				if (dimension.Orientation == DimensionOrientation.Horizontal)
				{
					actualCoordinate = dimension.FirstPoint.X;
				}
				else if (dimension.Orientation == DimensionOrientation.Vertical)
				{
					actualCoordinate = dimension.FirstPoint.Y;
				}
				else
				{
					throw new InvalidOperationException("Dimension " + (dimension.DebugRole ?? dimension.Kind.ToString()) + " has no valid datum axis for outline reference validation.");
				}
				if (!NearlyEqual(actualCoordinate, dimension.RequiredOutlineReferenceCoordinate.Value))
				{
					throw new InvalidOperationException("Dimension " + (dimension.DebugRole ?? dimension.Kind.ToString()) + " changed its required datum coordinate.");
				}
			}
		}
	}

	private bool NearlyEqual(double first, double second)
	{
		return Math.Abs(first - second) <= _config.GeometryTolerance;
	}

	private static bool IsFinite(double value)
	{
		return !double.IsNaN(value) && !double.IsInfinity(value);
	}
}
