using System.Collections.Generic;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;

namespace CadAuto.CadAdapter.Mapping;

public static class CadToCoreModelMapper
{
	public static OutlineFeature2D ToCoreOutline(OutlineFeature source)
	{
		OutlineFeature2D outlineFeature2D = new OutlineFeature2D
		{
			MinX = source.MinX,
			MaxX = source.MaxX,
			MinY = source.MinY,
			MaxY = source.MaxY
		};
		foreach (Point2d vertex in source.Vertices)
		{
			outlineFeature2D.Vertices.Add(new Point2D(vertex.X, vertex.Y));
		}
		foreach (OutlineSegment segment in source.Segments)
		{
			outlineFeature2D.Segments.Add(new Segment2D(new Point2D(segment.Start.X, segment.Start.Y), new Point2D(segment.End.X, segment.End.Y))
			{
				SourceKey = segment.SourceId.ToString(),
				IsArcChord = segment.IsArcChord
			});
		}
		foreach (OutlineArc arc in source.Arcs)
		{
			outlineFeature2D.Arcs.Add(new Arc2D
			{
				Start = new Point2D(arc.Start.X, arc.Start.Y),
				End = new Point2D(arc.End.X, arc.End.Y),
				Center = new Point2D(arc.Center.X, arc.Center.Y),
				Radius = arc.Radius,
				Bulge = arc.Bulge,
				SourceKey = arc.SourceId.ToString()
			});
		}
		foreach (ChamferFeature chamfer in source.Chamfers)
		{
			outlineFeature2D.Chamfers.Add(new ChamferFeature2D
			{
				StartPoint = new Point2D(chamfer.StartPoint.X, chamfer.StartPoint.Y),
				EndPoint = new Point2D(chamfer.EndPoint.X, chamfer.EndPoint.Y),
				Length = chamfer.Length,
				DeltaX = chamfer.DeltaX,
				DeltaY = chamfer.DeltaY,
				Value = chamfer.Value,
				Text = chamfer.Text,
				Confidence = chamfer.Confidence
			});
		}
		foreach (FilletFeature fillet in source.Fillets)
		{
			outlineFeature2D.Fillets.Add(new FilletFeature2D
			{
				Center = new Point2D(fillet.Center.X, fillet.Center.Y),
				Radius = fillet.Radius,
				StartPoint = new Point2D(fillet.StartPoint.X, fillet.StartPoint.Y),
				EndPoint = new Point2D(fillet.EndPoint.X, fillet.EndPoint.Y),
				Text = fillet.Text,
				Confidence = fillet.Confidence
			});
		}
		return outlineFeature2D;
	}

	public static Segment2D ToCoreSegment(OutlineSegment source)
	{
		return new Segment2D(new Point2D(source.Start.X, source.Start.Y), new Point2D(source.End.X, source.End.Y))
		{
			SourceKey = source.SourceId.ToString(),
			IsArcChord = source.IsArcChord
		};
	}

	public static Datum2D ToCoreDatum(DatumDefinition source)
	{
		Datum2D datum = new Datum2D
		{
			BaseX = source.BaseX,
			BaseY = source.BaseY,
			DatumHole = ((source.DatumHole == null) ? null : ToCoreHole(source.DatumHole)),
			DatumHoleLocationBaseX = source.DatumHoleLocationBaseX,
			DatumHoleLocationBaseY = source.DatumHoleLocationBaseY,
			DatumHoleLocationUseToleranceX = source.DatumHoleLocationUseToleranceX,
			DatumHoleLocationUseToleranceY = source.DatumHoleLocationUseToleranceY
		};
		foreach (CenterlineEndpointDefinition endpoint in source.HoleCenterlineEndpoints ?? new List<CenterlineEndpointDefinition>())
		{
			if (endpoint == null)
			{
				continue;
			}
			datum.HoleCenterlineEndpoints.Add(new CenterlineEndpoint2D
			{
				Point = new Point2D(endpoint.Point.X, endpoint.Point.Y),
				SourceGeometryId = endpoint.SourceGeometryId ?? string.Empty
			});
		}
		return datum;
	}

	public static List<HoleFeature2D> ToCoreHoles(IEnumerable<HoleFeature> source)
	{
		List<HoleFeature2D> list = new List<HoleFeature2D>();
		foreach (HoleFeature item in source)
		{
			list.Add(ToCoreHole(item));
		}
		return list;
	}

	public static List<SlotFeature2D> ToCoreSlots(IEnumerable<SlotFeature> source)
	{
		List<SlotFeature2D> list = new List<SlotFeature2D>();
		if (source == null)
		{
			return list;
		}
		foreach (SlotFeature item in source)
		{
			if (item != null)
			{
				list.Add(new SlotFeature2D
				{
					GroupId = item.GroupId,
					FirstCenter = new Point2D(item.FirstCenter.X, item.FirstCenter.Y),
					SecondCenter = new Point2D(item.SecondCenter.X, item.SecondCenter.Y),
					Radius = item.Radius,
					CenterDistance = item.CenterDistance,
					IsSingleArcSlot = item.IsSingleArcSlot,
					IsVertical = item.IsVertical,
					ArcLeaderTarget = new Point2D(item.ArcLeaderTarget.X, item.ArcLeaderTarget.Y)
				});
			}
		}
		return list;
	}

	private static HoleFeature2D ToCoreHole(HoleFeature source)
	{
		return new HoleFeature2D
		{
			Center = new Point2D(source.Center.X, source.Center.Y),
			Diameter = source.Diameter,
			SourceKey = source.SourceId.ToString(),
			FitTolerance = source.FitTolerance,
			ThreadCallout = source.ThreadCallout,
			Kind = ToCoreHoleKind(source.HoleKind)
		};
	}

	private static HoleKind2D ToCoreHoleKind(HoleKind source)
	{
		return source switch
		{
			HoleKind.Pin => HoleKind2D.Pin,
			HoleKind.Thread => HoleKind2D.Thread,
			HoleKind.Slot => HoleKind2D.Slot,
			_ => HoleKind2D.Normal,
		};
	}
}
