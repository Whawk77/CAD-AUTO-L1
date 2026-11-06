using System;
using System.Collections.Generic;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Model;

/// <summary>
/// Deep-copy model features into a planning frame. The source WCS model remains
/// untouched so the adapter can use it again for rendering and diagnostics.
/// </summary>
public static class CoordinateFrameModelTransform
{
	public static OutlineFeature2D ToLocal(OutlineFeature2D source, CoordinateFrame2D frame, double tolerance = 1E-9)
	{
		if (source == null)
		{
			return null;
		}
		CoordinateFrame2D effectiveFrame = frame ?? CoordinateFrame2D.Identity;
		OutlineFeature2D target = new OutlineFeature2D();
		foreach (Point2D vertex in source.Vertices)
		{
			target.Vertices.Add(effectiveFrame.ToLocal(vertex));
		}

		Dictionary<Segment2D, Segment2D> segmentMap = new Dictionary<Segment2D, Segment2D>();
		foreach (Segment2D segment in source.Segments)
		{
			if (segment == null)
			{
				target.Segments.Add(null);
				continue;
			}
			Segment2D transformed = new Segment2D(
				effectiveFrame.ToLocal(segment.Start),
				effectiveFrame.ToLocal(segment.End))
			{
				SourceKey = segment.SourceKey,
				IsArcChord = segment.IsArcChord
			};
			target.Segments.Add(transformed);
			segmentMap[segment] = transformed;
		}

		Dictionary<Arc2D, Arc2D> arcMap = new Dictionary<Arc2D, Arc2D>();
		foreach (Arc2D arc in source.Arcs)
		{
			if (arc == null)
			{
				target.Arcs.Add(null);
				continue;
			}
			Arc2D transformed = new Arc2D
			{
				Start = effectiveFrame.ToLocal(arc.Start),
				End = effectiveFrame.ToLocal(arc.End),
				Center = effectiveFrame.ToLocal(arc.Center),
				Radius = arc.Radius,
				Bulge = arc.Bulge,
				SourceKey = arc.SourceKey
			};
			target.Arcs.Add(transformed);
			arcMap[arc] = transformed;
		}

		foreach (ChamferFeature2D chamfer in source.Chamfers)
		{
			if (chamfer == null)
			{
				target.Chamfers.Add(null);
				continue;
			}
			Point2D start = effectiveFrame.ToLocal(chamfer.StartPoint);
			Point2D end = effectiveFrame.ToLocal(chamfer.EndPoint);
			target.Chamfers.Add(new ChamferFeature2D
			{
				StartPoint = start,
				EndPoint = end,
				Length = start.DistanceTo(end),
				DeltaX = Math.Abs(end.X - start.X),
				DeltaY = Math.Abs(end.Y - start.Y),
				Value = chamfer.Value,
				SourceGeometryIds = new List<string>(chamfer.SourceGeometryIds),
				Text = chamfer.Text,
				SourceSegment = chamfer.SourceSegment != null && segmentMap.TryGetValue(chamfer.SourceSegment, out Segment2D transformedSegment)
					? transformedSegment
					: null,
				Confidence = chamfer.Confidence
			});
		}

		foreach (FilletFeature2D fillet in source.Fillets)
		{
			if (fillet == null)
			{
				target.Fillets.Add(null);
				continue;
			}
			target.Fillets.Add(new FilletFeature2D
			{
				Center = effectiveFrame.ToLocal(fillet.Center),
				Radius = fillet.Radius,
				StartPoint = effectiveFrame.ToLocal(fillet.StartPoint),
				EndPoint = effectiveFrame.ToLocal(fillet.EndPoint),
				Text = fillet.Text,
				SourceArc = fillet.SourceArc != null && arcMap.TryGetValue(fillet.SourceArc, out Arc2D transformedArc)
					? transformedArc
					: null,
				Confidence = fillet.Confidence
			});
		}

		if (OutlineGeometryQuery.TryGetEnvelope(target, Math.Max(Math.Abs(tolerance), 1E-9), out OutlineEnvelope2D envelope))
		{
			target.MinX = envelope.MinX;
			target.MaxX = envelope.MaxX;
			target.MinY = envelope.MinY;
			target.MaxY = envelope.MaxY;
		}
		else
		{
			List<Point2D> points = target.Vertices
				.Concat(target.Segments.Where(segment => segment != null).SelectMany(segment => new[] { segment.Start, segment.End }))
				.Concat(target.Arcs.Where(arc => arc != null).SelectMany(arc => new[] { arc.Start, arc.End, arc.Center }))
				.ToList();
			if (points.Count == 0)
			{
				Point2D[] corners =
				{
					effectiveFrame.ToLocal(new Point2D(source.MinX, source.MinY)),
					effectiveFrame.ToLocal(new Point2D(source.MinX, source.MaxY)),
					effectiveFrame.ToLocal(new Point2D(source.MaxX, source.MinY)),
					effectiveFrame.ToLocal(new Point2D(source.MaxX, source.MaxY))
				};
				points.AddRange(corners);
			}
			target.MinX = points.Min(point => point.X);
			target.MaxX = points.Max(point => point.X);
			target.MinY = points.Min(point => point.Y);
			target.MaxY = points.Max(point => point.Y);
		}
		return target;
	}

	public static Datum2D ToLocal(Datum2D source, CoordinateFrame2D frame)
	{
		if (source == null)
		{
			return null;
		}
		CoordinateFrame2D effectiveFrame = frame ?? CoordinateFrame2D.Identity;
		Point2D localBase = effectiveFrame.ToLocal(new Point2D(source.BaseX, source.BaseY));
		// 坐标系转 90° 的整数倍时，按轴区分的设置要跟着换轴、换方向；斜坐标系没有对应的轴，保持原值。
		bool quarterTurn = TryGetQuarterTurn(effectiveFrame, out bool swapAxes);
		Datum2D target = new Datum2D
		{
			BaseX = localBase.X,
			BaseY = localBase.Y,
			DatumHole = ToLocal(source.DatumHole, effectiveFrame),
			PinGroupVerticalLayout = source.PinGroupVerticalLayout,
			PinGroupHorizontalLayout = source.PinGroupHorizontalLayout,
			DatumHoleLocationUseToleranceX = swapAxes ? source.DatumHoleLocationUseToleranceY : source.DatumHoleLocationUseToleranceX,
			DatumHoleLocationUseToleranceY = swapAxes ? source.DatumHoleLocationUseToleranceX : source.DatumHoleLocationUseToleranceY
		};
		if (quarterTurn)
		{
			MapPinGroupLayouts(source, target, effectiveFrame);
		}
		if (source.DatumHoleLocationBaseX.HasValue || source.DatumHoleLocationBaseY.HasValue)
		{
			Point2D location = effectiveFrame.ToLocal(new Point2D(
				source.DatumHoleLocationBaseX ?? source.BaseX,
				source.DatumHoleLocationBaseY ?? source.BaseY));
			bool hasLocalX = swapAxes ? source.DatumHoleLocationBaseY.HasValue : source.DatumHoleLocationBaseX.HasValue;
			bool hasLocalY = swapAxes ? source.DatumHoleLocationBaseX.HasValue : source.DatumHoleLocationBaseY.HasValue;
			target.DatumHoleLocationBaseX = hasLocalX ? location.X : (double?)null;
			target.DatumHoleLocationBaseY = hasLocalY ? location.Y : (double?)null;
		}
		foreach (CenterlineEndpoint2D endpoint in source.HoleCenterlineEndpoints ?? Enumerable.Empty<CenterlineEndpoint2D>())
		{
			if (endpoint == null)
			{
				continue;
			}
			target.HoleCenterlineEndpoints.Add(new CenterlineEndpoint2D
			{
				Point = effectiveFrame.ToLocal(endpoint.Point),
				SourceGeometryId = endpoint.SourceGeometryId ?? string.Empty
			});
		}
		foreach (StructureDatumMarker2D marker in source.StructureDatumMarkers)
		{
			target.StructureDatumMarkers.Add(new StructureDatumMarker2D { ContactPoint = effectiveFrame.ToLocal(marker.ContactPoint),
				BlockName = marker.BlockName, SourceGeometryId = marker.SourceGeometryId,
				ContactKind = marker.ContactKind, ValidationFailure = marker.ValidationFailure });
		}
		target.StructureDatumMinimumX = quarterTurn
			? MapMinimumChoice(source.StructureDatumMinimumX, source.StructureDatumMinimumY, effectiveFrame.UnitX)
			: source.StructureDatumMinimumX;
		target.StructureDatumMinimumY = quarterTurn
			? MapMinimumChoice(source.StructureDatumMinimumX, source.StructureDatumMinimumY, effectiveFrame.UnitY)
			: source.StructureDatumMinimumY;
		return target;
	}

	/// <summary>
	/// 坐标系的 X 轴与世界 X 轴或 Y 轴重合时返回 true；swapAxes 表示局部 X 对应世界 Y。
	/// </summary>
	public static bool TryGetQuarterTurn(CoordinateFrame2D frame, out bool swapAxes)
	{
		const double axisTolerance = 1E-9;
		Point2D unitX = (frame ?? CoordinateFrame2D.Identity).UnitX;
		swapAxes = false;
		if (Math.Abs(unitX.Y) <= axisTolerance) { return true; }
		if (Math.Abs(unitX.X) <= axisTolerance) { swapAxes = true; return true; }
		return false;
	}

	// 局部轴 localAxis 的"最小侧"对应世界上哪一侧：沿局部轴正向就是沿某个世界轴的正向或反向。
	private static bool? MapMinimumChoice(bool? worldMinimumX, bool? worldMinimumY, Point2D localAxis)
	{
		bool alongWorldX = Math.Abs(localAxis.X) >= Math.Abs(localAxis.Y);
		bool? world = alongWorldX ? worldMinimumX : worldMinimumY;
		double sign = alongWorldX ? localAxis.X : localAxis.Y;
		if (!world.HasValue) { return null; }
		return sign > 0 ? world.Value : !world.Value;
	}

	private static void MapPinGroupLayouts(Datum2D source, Datum2D target, CoordinateFrame2D frame)
	{
		target.PinGroupVerticalLayout = PinGroupVerticalLayoutPreference.Automatic;
		target.PinGroupHorizontalLayout = PinGroupHorizontalLayoutPreference.Automatic;
		Point2D? vertical = source.PinGroupVerticalLayout == PinGroupVerticalLayoutPreference.Left ? new Point2D(-1, 0)
			: source.PinGroupVerticalLayout == PinGroupVerticalLayoutPreference.Right ? new Point2D(1, 0) : (Point2D?)null;
		Point2D? horizontal = source.PinGroupHorizontalLayout == PinGroupHorizontalLayoutPreference.Top ? new Point2D(0, 1)
			: source.PinGroupHorizontalLayout == PinGroupHorizontalLayoutPreference.Bottom ? new Point2D(0, -1) : (Point2D?)null;
		foreach (Point2D? worldSide in new[] { vertical, horizontal })
		{
			if (!worldSide.HasValue) { continue; }
			Point2D v = worldSide.Value;
			double x = v.X * frame.UnitX.X + v.Y * frame.UnitX.Y;
			double y = v.X * frame.UnitY.X + v.Y * frame.UnitY.Y;
			if (Math.Abs(x) >= Math.Abs(y))
			{
				target.PinGroupVerticalLayout = x < 0 ? PinGroupVerticalLayoutPreference.Left : PinGroupVerticalLayoutPreference.Right;
			}
			else
			{
				target.PinGroupHorizontalLayout = y > 0 ? PinGroupHorizontalLayoutPreference.Top : PinGroupHorizontalLayoutPreference.Bottom;
			}
		}
	}

	public static List<HoleFeature2D> ToLocal(IEnumerable<HoleFeature2D> source, CoordinateFrame2D frame)
	{
		return (source ?? Enumerable.Empty<HoleFeature2D>())
			.Where(hole => hole != null)
			.Select(hole => ToLocal(hole, frame))
			.ToList();
	}

	public static HoleFeature2D ToLocal(HoleFeature2D source, CoordinateFrame2D frame)
	{
		if (source == null)
		{
			return null;
		}
		return new HoleFeature2D
		{
			Center = (frame ?? CoordinateFrame2D.Identity).ToLocal(source.Center),
			Diameter = source.Diameter,
			SourceKey = source.SourceKey,
			FitTolerance = source.FitTolerance,
			ThreadCallout = source.ThreadCallout,
			Kind = source.Kind
		};
	}

	public static List<SlotFeature2D> ToLocal(IEnumerable<SlotFeature2D> source, CoordinateFrame2D frame)
	{
		return (source ?? Enumerable.Empty<SlotFeature2D>())
			.Where(slot => slot != null)
			.Select(slot => ToLocal(slot, frame))
			.ToList();
	}

	public static SlotFeature2D ToLocal(SlotFeature2D source, CoordinateFrame2D frame)
	{
		if (source == null)
		{
			return null;
		}
		CoordinateFrame2D effectiveFrame = frame ?? CoordinateFrame2D.Identity;
		Point2D firstCenter = effectiveFrame.ToLocal(source.FirstCenter);
		Point2D secondCenter = effectiveFrame.ToLocal(source.SecondCenter);
		double deltaX = Math.Abs(secondCenter.X - firstCenter.X);
		double deltaY = Math.Abs(secondCenter.Y - firstCenter.Y);
		return new SlotFeature2D
		{
			GroupId = source.GroupId,
			FirstCenter = firstCenter,
			SecondCenter = secondCenter,
			Radius = source.Radius,
			CenterDistance = source.CenterDistance,
			IsSingleArcSlot = source.IsSingleArcSlot,
			IsVertical = deltaY >= deltaX,
			ArcLeaderTarget = effectiveFrame.ToLocal(source.ArcLeaderTarget)
		};
	}
}
