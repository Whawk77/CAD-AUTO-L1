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
		Datum2D target = new Datum2D
		{
			BaseX = localBase.X,
			BaseY = localBase.Y,
			DatumHole = ToLocal(source.DatumHole, effectiveFrame),
			DatumHoleLocationUseToleranceX = source.DatumHoleLocationUseToleranceX,
			DatumHoleLocationUseToleranceY = source.DatumHoleLocationUseToleranceY
		};
		if (source.DatumHoleLocationBaseX.HasValue || source.DatumHoleLocationBaseY.HasValue)
		{
			Point2D location = effectiveFrame.ToLocal(new Point2D(
				source.DatumHoleLocationBaseX ?? source.BaseX,
				source.DatumHoleLocationBaseY ?? source.BaseY));
			target.DatumHoleLocationBaseX = source.DatumHoleLocationBaseX.HasValue ? location.X : (double?)null;
			target.DatumHoleLocationBaseY = source.DatumHoleLocationBaseY.HasValue ? location.Y : (double?)null;
		}
		return target;
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
