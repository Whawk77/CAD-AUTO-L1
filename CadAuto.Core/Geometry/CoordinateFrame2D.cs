using System;
using CadAuto.Core.Model;

namespace CadAuto.Core.Geometry;

/// <summary>
/// A right-handed 2D rigid frame used to keep dimension planning in the part's
/// local coordinates while preserving the original WCS geometry for rendering.
/// </summary>
public sealed class CoordinateFrame2D
{
	private const double IdentityTolerance = 1E-12;

	public static CoordinateFrame2D Identity { get; } = new CoordinateFrame2D(
		new Point2D(0.0, 0.0),
		new Point2D(1.0, 0.0),
		isNormalized: true);

	public Point2D Origin { get; }

	public Point2D UnitX { get; }

	public Point2D UnitY { get; }

	public double Angle => Math.Atan2(UnitX.Y, UnitX.X);

	public bool IsIdentity => NearlyEqual(Origin.X, 0.0)
		&& NearlyEqual(Origin.Y, 0.0)
		&& NearlyEqual(UnitX.X, 1.0)
		&& NearlyEqual(UnitX.Y, 0.0)
		&& NearlyEqual(UnitY.X, 0.0)
		&& NearlyEqual(UnitY.Y, 1.0);

	public CoordinateFrame2D(Point2D origin, Point2D unitX)
		: this(origin, unitX, isNormalized: false)
	{
	}

	private CoordinateFrame2D(Point2D origin, Point2D unitX, bool isNormalized)
	{
		if (!IsFinite(origin) || !IsFinite(unitX))
		{
			throw new ArgumentException("Coordinate frame vectors must be finite.");
		}
		double length = Math.Sqrt(unitX.X * unitX.X + unitX.Y * unitX.Y);
		if (!isNormalized && length <= IdentityTolerance)
		{
			throw new ArgumentException("Coordinate frame UnitX must have non-zero length.", nameof(unitX));
		}
		if (isNormalized)
		{
			length = 1.0;
		}
		Origin = origin;
		UnitX = new Point2D(unitX.X / length, unitX.Y / length);
		// The perpendicular is intentionally not inferred from the outline winding;
		// this keeps the frame right-handed for both clockwise and counter-clockwise
		// source paths.
		UnitY = new Point2D(-UnitX.Y, UnitX.X);
	}

	public Point2D ToLocal(Point2D world)
	{
		Point2D delta = new Point2D(world.X - Origin.X, world.Y - Origin.Y);
		return new Point2D(
			delta.X * UnitX.X + delta.Y * UnitX.Y,
			delta.X * UnitY.X + delta.Y * UnitY.Y);
	}

	public Point2D ToWorld(Point2D local)
	{
		return new Point2D(
			Origin.X + local.X * UnitX.X + local.Y * UnitY.X,
			Origin.Y + local.X * UnitX.Y + local.Y * UnitY.Y);
	}

	/// <summary>
	/// Infers a stable local frame from the longest straight outline segment.
	/// Fully axis-aligned outlines intentionally return Identity to preserve all
	/// existing planner coordinates. A square or a 90-degree-symmetric outline
	/// has no unique principal edge; the first longest segment is the deterministic
	/// tie-break and callers should treat the resulting side labels as frame-local.
	/// </summary>
	public static CoordinateFrame2D InferFromOutline(OutlineFeature2D outline, double tolerance)
	{
		if (outline == null)
		{
			throw new ArgumentNullException(nameof(outline));
		}
		double tol = Math.Max(Math.Abs(tolerance), IdentityTolerance);
		Segment2D longest = null;
		double longestLength = 0.0;
		bool hasNonAxisSegment = false;
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment == null || segment.IsArcChord || segment.Length <= tol)
			{
				continue;
			}
			if (!segment.IsHorizontal(tol) && !segment.IsVertical(tol))
			{
				hasNonAxisSegment = true;
			}
			if (longest == null || segment.Length > longestLength + tol)
			{
				longest = segment;
				longestLength = segment.Length;
			}
		}
		if (!hasNonAxisSegment || longest == null)
		{
			return Identity;
		}

		double dx = longest.End.X - longest.Start.X;
		double dy = longest.End.Y - longest.Start.Y;
		double length = Math.Sqrt(dx * dx + dy * dy);
		if (length <= tol)
		{
			return Identity;
		}
		dx /= length;
		dy /= length;
		// A mostly axis-aligned profile can contain a short diagonal chamfer. Keep
		// that legacy profile in WCS when the selected principal edge itself is
		// axis-aligned; the frame is intended for genuinely rotated contours.
		if (Math.Abs(dx) <= tol || Math.Abs(dy) <= tol)
		{
			return Identity;
		}
		// Segment order is not a semantic direction. Normalize the principal axis
		// so reversing a source segment cannot silently swap local left/right.
		if (dx < -tol || (Math.Abs(dx) <= tol && dy < 0.0))
		{
			dx = -dx;
			dy = -dy;
		}
		return new CoordinateFrame2D(longest.Start, new Point2D(dx, dy));
	}

	private static bool IsFinite(Point2D point)
	{
		return !double.IsNaN(point.X) && !double.IsInfinity(point.X)
			&& !double.IsNaN(point.Y) && !double.IsInfinity(point.Y);
	}

	private static bool NearlyEqual(double first, double second)
	{
		return Math.Abs(first - second) <= IdentityTolerance;
	}
}
