using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;
using CadAuto.Core.Rules;

namespace CadAuto.CadAdapter.Recognition;

public sealed class FeatureRecognizer
{
	private sealed class SlotArcCandidate
	{
		public ObjectId Id { get; set; }

		public Point2d Center { get; set; }

		public Point2d Start { get; set; }

		public Point2d End { get; set; }

		public Point2d Mid { get; set; }

		public double Radius { get; set; }
	}

	private sealed class SlotLineCandidate
	{
		public ObjectId Id { get; set; }

		public Point2d Start { get; set; }

		public Point2d End { get; set; }

		public double Length => Start.GetDistanceTo(End);
	}

	private sealed class ThreadArcInfo
	{
		public Point3d Center { get; set; }

		public double Radius { get; set; }
	}

	private sealed class ThreadMinorCircleInfo
	{
		public Point3d Center { get; set; }

		public double Diameter { get; set; }

		public string Callout { get; set; }
	}

	private const double BulgeEpsilon = 1E-12;

	private static readonly bool DiagnosticsEnabled;

	private readonly DimensionRuleConfig _config;

	public IList<SlotFeature> LastRecognizedSlots { get; private set; } = new List<SlotFeature>();

	public FeatureRecognizer(DimensionRuleConfig config)
	{
		_config = config;
	}

	public OutlineFeature RecognizeOutline(Entity entity, Transaction tr)
	{
		Polyline polyline = entity as Polyline;
		if (polyline != null)
		{
			OutlineFeature outlineFeature = RecognizeLightweightOutline(polyline);
			RecognizeOutlineCornerFeatures(outlineFeature);
			return outlineFeature;
		}
		Polyline2d polyline2d = entity as Polyline2d;
		if (polyline2d != null)
		{
			OutlineFeature outlineFeature = RecognizePolyline2dOutline(polyline2d, tr);
			RecognizeOutlineCornerFeatures(outlineFeature);
			return outlineFeature;
		}
		throw new InvalidOperationException("Outline must be a closed Polyline or DRAWING outline component.");
	}

	public OutlineFeature RecognizeOutline(IEnumerable<ObjectId> outlineEntityIds, Transaction tr)
	{
		OutlineFeature outlineFeature = CreateEmptyOutline();
		foreach (ObjectId outlineEntityId in outlineEntityIds)
		{
			Entity entity = tr.GetObject(outlineEntityId, OpenMode.ForRead) as Entity;
			if (!(entity == null))
			{
				AddEntityExtents(outlineFeature, entity);
				AddEntityKeyPoints(outlineFeature, entity, tr);
			}
		}
		if (outlineFeature.MinX == double.MaxValue)
		{
			throw new InvalidOperationException("Unable to calculate outline extents from DRAWING objects.");
		}
		RecognizeOutlineCornerFeatures(outlineFeature);
		return outlineFeature;
	}

	public IList<HoleFeature> RecognizeHoles(IEnumerable<ObjectId> sourceIds, Transaction tr, IEnumerable<ObjectId> contextIds)
	{
		List<ObjectId> list = ((sourceIds == null) ? new List<ObjectId>() : sourceIds.ToList());
		LastRecognizedSlots = RecognizeSlotFeatures(list, tr);
		HashSet<ObjectId> hashSet = new HashSet<ObjectId>(LastRecognizedSlots.SelectMany(GetSlotSourceIds));
		IList<Point3d> list2 = CollectPinMarkers(contextIds, list, tr);
		IList<ThreadArcInfo> list3 = CollectThreadArcInfos(list, tr);
		IList<ThreadMinorCircleInfo> minorCircles = CollectThreadMinorCircles(list, tr, list3);
		List<HoleFeature> list4 = new List<HoleFeature>();
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		List<string> list5 = new List<string>();
		List<string> list6 = new List<string>();
		foreach (ThreadArcInfo item in list3)
		{
			list6.Add($"(涓\ue15e績={item.Center.X:0.###},{item.Center.Y:0.###} R={item.Radius:0.###})");
		}
		foreach (SlotFeature lastRecognizedSlot in LastRecognizedSlots)
		{
			list4.Add(CreateSlotPoint(lastRecognizedSlot, lastRecognizedSlot.FirstCenter, lastRecognizedSlot.FirstArcId));
			if (!lastRecognizedSlot.IsSingleArcSlot)
			{
				list4.Add(CreateSlotPoint(lastRecognizedSlot, lastRecognizedSlot.SecondCenter, lastRecognizedSlot.SecondArcId));
			}
		}
		foreach (ObjectId item2 in list)
		{
			if (hashSet.Contains(item2))
			{
				continue;
			}
			Entity entity = tr.GetObject(item2, OpenMode.ForRead) as Entity;
			Circle circle = entity as Circle;
			if (circle != null)
			{
				num++;
				double diameter = circle.Radius * 2.0;
				string threadCalloutForMinorDiameter = _config.GetThreadCalloutForMinorDiameter(diameter);
				bool flag = HasThreadArcAtCenter(circle.Center, list3);
				if (!string.IsNullOrEmpty(threadCalloutForMinorDiameter) && flag)
				{
					num2++;
					num3++;
					list5.Add(FormatCircleSuppression(circle, "thread minor " + threadCalloutForMinorDiameter));
					continue;
				}
				if (IsSuppressedByThreadArc(circle, list3))
				{
					num2++;
					list5.Add($"(涓\ue15e績={circle.Center.X:0.###},{circle.Center.Y:0.###} R={circle.Radius:0.###})");
					continue;
				}
				bool flag2 = HasPinMarker(circle.Center, circle.Radius, list2);
				if (flag2)
				{
					num4++;
				}
				else
				{
					num5++;
				}
				list4.Add(new HoleFeature
				{
					Center = circle.Center,
					Diameter = diameter,
					SourceId = item2,
					CircleId = item2,
					HoleKind = (flag2 ? HoleKind.Pin : HoleKind.Normal),
					FitTolerance = (flag2 ? _config.PinHoleFitToleranceText : string.Empty),
					ThreadCallout = string.Empty
				});
				continue;
			}
			Arc arc = entity as Arc;
			if (arc != null && IsConfirmedThreadArc(arc, list, tr))
			{
				string text = FindThreadMinorCallout(arc.Center, minorCircles);
				if (string.IsNullOrEmpty(text))
				{
					text = _config.GetThreadCalloutForMinorDiameter(arc.Radius * 2.0);
				}
				list4.Add(new HoleFeature
				{
					Center = arc.Center,
					Diameter = arc.Radius * 2.0,
					SourceId = item2,
					CircleId = ObjectId.Null,
					HoleKind = HoleKind.Thread,
					FitTolerance = string.Empty,
					ThreadCallout = text
				});
			}
		}
		Document mdiActiveDocument = Application.DocumentManager.MdiActiveDocument;
		if (mdiActiveDocument != null && DiagnosticsEnabled)
		{
			Editor editor = mdiActiveDocument.Editor;
			editor.WriteMessage("\n[璇婃柇] Circle鎬绘暟={0}, 琚\ue0a5灪绾瑰姬鎶戝埗={1}, 琚\ue0a5灪绾瑰皬寰勫尮閰?{2}, 閿€瀛旀爣璁板尮閰?{3}, 閿€瀛旀爣璁版湭鍖归厤={4}", num, num2, num3, num4, num5);
			editor.WriteMessage("\n[diagnostic] Hole recognition: circles={0}, suppressed={1}, threadMinorSuppressed={2}, pinMatched={3}, normalCircles={4}", num, num2, num3, num4, num5);
			WriteHoleDiagnostics(editor, list4, list5);
			WriteSlotDiagnostics(editor, LastRecognizedSlots);
			if (num5 > 0 && list2.Count > 0)
			{
				editor.WriteMessage("\n[璇婃柇] 閿€瀛旀爣璁颁綅缃?");
				foreach (Point3d item3 in list2)
				{
					editor.WriteMessage(" ({0:0.###},{1:0.###})", item3.X, item3.Y);
				}
				editor.WriteMessage("\n[璇婃柇] 鏈\ue044尮閰嶉攢瀛旂殑Circle:");
				foreach (HoleFeature item4 in list4)
				{
					if (item4.HoleKind == HoleKind.Normal)
					{
						editor.WriteMessage(" (涓\ue15e績={0:0.###},{1:0.###} R={2:0.###})", item4.Center.X, item4.Center.Y, item4.Diameter / 2.0);
					}
				}
			}
			if (list6.Count > 0)
			{
				editor.WriteMessage("\n[璇婃柇] 纭\ue1bf\ue17b铻虹汗寮? {0}", string.Join(", ", list6));
			}
			if (list5.Count > 0)
			{
				editor.WriteMessage("\n[璇婃柇] 琚\ue0a3姂鍒剁殑Circle: {0}", string.Join(", ", list5));
			}
		}
		return list4;
	}

	public IList<SlotFeature> RecognizeOutlineSlotFeatures(OutlineFeature outline)
	{
		List<SlotFeature> list = new List<SlotFeature>();
		if (outline == null || outline.Arcs.Count == 0 || outline.Segments.Count < 2)
		{
			return list;
		}
		int num = 1;
		foreach (OutlineArc arc in outline.Arcs)
		{
			if (!IsHalfArc(arc))
			{
				continue;
			}
			List<SlotLineCandidate> list2 = (from s in outline.Segments
				where !s.IsArcChord
				select new SlotLineCandidate
				{
					Id = s.SourceId,
					Start = s.Start,
					End = s.End
				} into l
				where LineTouchesSlotArc(l, ToSlotArcCandidate(arc))
				select l).ToList();
			for (int num2 = 0; num2 < list2.Count; num2++)
			{
				for (int num3 = num2 + 1; num3 < list2.Count; num3++)
				{
					SlotArcCandidate arc2 = ToSlotArcCandidate(arc);
					if (CanPairSingleArcSlotLines(arc2, list2[num2], list2[num3], out var horizontal))
					{
						list.Add(new SlotFeature
						{
							GroupId = "OUTLINE_SLOT" + num.ToString(CultureInfo.InvariantCulture),
							FirstCenter = arc.Center,
							SecondCenter = arc.Center,
							Radius = arc.Radius,
							CenterDistance = 0.0,
							FirstArcId = arc.SourceId,
							FirstLineId = list2[num2].Id,
							SecondLineId = list2[num3].Id,
							IsSingleArcSlot = true,
							IsVertical = !horizontal,
							ArcLeaderTarget = GetOutlineArcMidPoint(arc)
						});
						num++;
						num2 = list2.Count;
						break;
					}
				}
			}
		}
		return list;
	}

	private static void WriteHoleDiagnostics(Editor editor, IEnumerable<HoleFeature> holes, IEnumerable<string> suppressedCircles)
	{
		List<HoleFeature> source = ((holes == null) ? new List<HoleFeature>() : holes.ToList());
		List<HoleFeature> list = source.Where((HoleFeature h) => h.HoleKind == HoleKind.Pin).ToList();
		List<HoleFeature> list2 = source.Where((HoleFeature h) => h.HoleKind == HoleKind.Normal).ToList();
		List<HoleFeature> list3 = source.Where((HoleFeature h) => h.HoleKind == HoleKind.Thread).ToList();
		List<HoleFeature> list4 = source.Where((HoleFeature h) => h.HoleKind == HoleKind.Slot).ToList();
		editor.WriteMessage("\n[diagnostic] Pin holes ({0}):", list.Count);
		foreach (HoleFeature item in list)
		{
			editor.WriteMessage(" center=({0:0.###},{1:0.###}) diameter={2:0.###} fit={3};", item.Center.X, item.Center.Y, item.Diameter, string.IsNullOrEmpty(item.FitTolerance) ? "none" : item.FitTolerance);
		}
		editor.WriteMessage("\n[diagnostic] Normal holes ({0}):", list2.Count);
		foreach (HoleFeature item2 in list2)
		{
			editor.WriteMessage(" center=({0:0.###},{1:0.###}) diameter={2:0.###};", item2.Center.X, item2.Center.Y, item2.Diameter);
		}
		editor.WriteMessage("\n[diagnostic] Thread holes ({0}):", list3.Count);
		foreach (HoleFeature item3 in list3)
		{
			editor.WriteMessage(" center=({0:0.###},{1:0.###}) callout={2};", item3.Center.X, item3.Center.Y, string.IsNullOrEmpty(item3.ThreadCallout) ? "unknown" : item3.ThreadCallout);
		}
		editor.WriteMessage("\n[diagnostic] Slot points ({0}):", list4.Count);
		foreach (HoleFeature item4 in list4)
		{
			editor.WriteMessage(" center=({0:0.###},{1:0.###}) radius={2:0.###};", item4.Center.X, item4.Center.Y, item4.Diameter / 2.0);
		}
		List<string> list5 = ((suppressedCircles == null) ? new List<string>() : suppressedCircles.Select((string s) => (s.IndexOf("reason=", StringComparison.OrdinalIgnoreCase) >= 0) ? s : (s + " reason=inside thread arc")).ToList());
		if (list5.Count > 0)
		{
			editor.WriteMessage("\n[diagnostic] Suppressed circles with reason: {0}", string.Join(", ", list5));
		}
	}

	private static string FormatCircleSuppression(Circle circle, string reason)
	{
		return $"center=({circle.Center.X:0.###},{circle.Center.Y:0.###}) radius={circle.Radius:0.###} reason={reason}";
	}

	public IList<HoleFeature> RecognizeHoles(IEnumerable<ObjectId> circleIds, Transaction tr)
	{
		return RecognizeHoles(circleIds, tr, new ObjectId[0]);
	}

	public IList<IList<HoleFeature>> GroupHolesByHorizontalRow(IEnumerable<HoleFeature> holes)
	{
		List<HoleFeature> list = (from h in holes
			orderby h.Center.Y, h.Center.X
			select h).ToList();
		List<IList<HoleFeature>> list2 = new List<IList<HoleFeature>>();
		foreach (HoleFeature hole in list)
		{
			IList<HoleFeature> list3 = list2.FirstOrDefault((IList<HoleFeature> r) => Math.Abs(GetAverageY(r) - hole.Center.Y) <= _config.GeometryTolerance);
			if (list3 == null)
			{
				list2.Add(new List<HoleFeature> { hole });
			}
			else
			{
				list3.Add(hole);
			}
		}
		foreach (IList<HoleFeature> item in list2)
		{
			List<HoleFeature> list4 = item.OrderBy((HoleFeature h) => h.Center.X).ToList();
			item.Clear();
			foreach (HoleFeature item2 in list4)
			{
				item.Add(item2);
			}
		}
		return list2.OrderBy((IList<HoleFeature> r) => GetAverageY(r)).ToList();
	}

	public IList<IList<HoleFeature>> GroupHolesByDiameter(IEnumerable<HoleFeature> holes)
	{
		List<HoleFeature> list = (from h in holes
			where !h.IsSlotPoint
			orderby h.HoleKind, h.Diameter, h.FitTolerance
			select h).ToList();
		List<IList<HoleFeature>> list2 = new List<IList<HoleFeature>>();
		foreach (HoleFeature hole in list)
		{
			IList<HoleFeature> list3 = list2.FirstOrDefault((IList<HoleFeature> g) => Math.Abs(GetAverageDiameter(g) - hole.Diameter) <= _config.GeometryTolerance && g[0].HoleKind == hole.HoleKind && string.Equals(g[0].FitTolerance ?? string.Empty, hole.FitTolerance ?? string.Empty, StringComparison.OrdinalIgnoreCase) && string.Equals(g[0].ThreadCallout ?? string.Empty, hole.ThreadCallout ?? string.Empty, StringComparison.OrdinalIgnoreCase));
			if (list3 == null)
			{
				list2.Add(new List<HoleFeature> { hole });
			}
			else
			{
				list3.Add(hole);
			}
		}
		return list2;
	}

	private IList<SlotFeature> RecognizeSlotFeatures(IList<ObjectId> sourceIds, Transaction tr)
	{
		List<SlotArcCandidate> arcs = new List<SlotArcCandidate>();
		List<SlotLineCandidate> list = new List<SlotLineCandidate>();
		foreach (ObjectId sourceId in sourceIds)
		{
			Entity entity = tr.GetObject(sourceId, OpenMode.ForRead) as Entity;
			Arc arc = entity as Arc;
			if (arc != null && IsDrawingLayer(arc.Layer) && !IsConfirmedThreadArc(arc, sourceIds, tr) && IsHalfArc(arc))
			{
				arcs.Add(new SlotArcCandidate
				{
					Id = sourceId,
					Center = new Point2d(arc.Center.X, arc.Center.Y),
					Start = new Point2d(arc.StartPoint.X, arc.StartPoint.Y),
					End = new Point2d(arc.EndPoint.X, arc.EndPoint.Y),
					Mid = GetArcMidPoint(arc),
					Radius = arc.Radius
				});
			}
			else
			{
				Line line = entity as Line;
				if (line != null && IsDrawingLayer(line.Layer))
				{
					list.Add(new SlotLineCandidate
					{
						Id = sourceId,
						Start = new Point2d(line.StartPoint.X, line.StartPoint.Y),
						End = new Point2d(line.EndPoint.X, line.EndPoint.Y)
					});
				}
			}
		}
		List<SlotFeature> list2 = new List<SlotFeature>();
		HashSet<ObjectId> hashSet = new HashSet<ObjectId>();
		HashSet<ObjectId> usedLines = new HashSet<ObjectId>();
		int slotIndex = 1;
		for (int i = 0; i < arcs.Count; i++)
		{
			if (hashSet.Contains(arcs[i].Id))
			{
				continue;
			}
			for (int j = i + 1; j < arcs.Count; j++)
			{
				if (!hashSet.Contains(arcs[j].Id) && CanPairSlotArcs(arcs[i], arcs[j], out var horizontal))
				{
					List<SlotLineCandidate> list3 = list.Where((SlotLineCandidate l) => !usedLines.Contains(l.Id) && IsLineParallelToSlot(l, horizontal) && ConnectsSlotArcs(l, arcs[i], arcs[j])).ToList();
					if (list3.Count == 2)
					{
						list2.Add(new SlotFeature
						{
							GroupId = "SLOT" + slotIndex.ToString(CultureInfo.InvariantCulture),
							FirstCenter = arcs[i].Center,
							SecondCenter = arcs[j].Center,
							Radius = (arcs[i].Radius + arcs[j].Radius) / 2.0,
							CenterDistance = arcs[i].Center.GetDistanceTo(arcs[j].Center),
							FirstArcId = arcs[i].Id,
							SecondArcId = arcs[j].Id,
							FirstLineId = list3[0].Id,
							SecondLineId = list3[1].Id,
							IsVertical = !horizontal
						});
						slotIndex++;
						hashSet.Add(arcs[i].Id);
						hashSet.Add(arcs[j].Id);
						usedLines.Add(list3[0].Id);
						usedLines.Add(list3[1].Id);
						break;
					}
				}
			}
		}
		RecognizeSingleArcSlots(arcs, list, hashSet, usedLines, list2, ref slotIndex);
		return list2;
	}

	private void RecognizeSingleArcSlots(IList<SlotArcCandidate> arcs, IList<SlotLineCandidate> lines, ISet<ObjectId> usedArcs, ISet<ObjectId> usedLines, IList<SlotFeature> slots, ref int slotIndex)
	{
		foreach (SlotArcCandidate arc in arcs)
		{
			if (usedArcs.Contains(arc.Id))
			{
				continue;
			}
			List<SlotLineCandidate> list = (from l in lines
				where !usedLines.Contains(l.Id)
				where LineTouchesSlotArc(l, arc)
				select l).ToList();
			for (int num = 0; num < list.Count; num++)
			{
				for (int num2 = num + 1; num2 < list.Count; num2++)
				{
					if (CanPairSingleArcSlotLines(arc, list[num], list[num2], out var horizontal))
					{
						slots.Add(new SlotFeature
						{
							GroupId = "SLOT" + slotIndex.ToString(CultureInfo.InvariantCulture),
							FirstCenter = arc.Center,
							SecondCenter = arc.Center,
							Radius = arc.Radius,
							CenterDistance = 0.0,
							FirstArcId = arc.Id,
							FirstLineId = list[num].Id,
							SecondLineId = list[num2].Id,
							IsSingleArcSlot = true,
							IsVertical = !horizontal,
							ArcLeaderTarget = arc.Mid
						});
						slotIndex++;
						usedArcs.Add(arc.Id);
						usedLines.Add(list[num].Id);
						usedLines.Add(list[num2].Id);
						num = list.Count;
						break;
					}
				}
			}
		}
	}

	private static IEnumerable<ObjectId> GetSlotSourceIds(SlotFeature slot)
	{
		if (!slot.FirstArcId.IsNull)
		{
			yield return slot.FirstArcId;
		}
		if (!slot.SecondArcId.IsNull)
		{
			yield return slot.SecondArcId;
		}
		if (!slot.FirstLineId.IsNull)
		{
			yield return slot.FirstLineId;
		}
		if (!slot.SecondLineId.IsNull)
		{
			yield return slot.SecondLineId;
		}
	}

	private static HoleFeature CreateSlotPoint(SlotFeature slot, Point2d center, ObjectId sourceId)
	{
		return new HoleFeature
		{
			Center = new Point3d(center.X, center.Y, 0.0),
			Diameter = slot.Radius * 2.0,
			SourceId = sourceId,
			CircleId = ObjectId.Null,
			HoleKind = HoleKind.Slot,
			FitTolerance = string.Empty,
			ThreadCallout = string.Empty
		};
	}

	private static void WriteSlotDiagnostics(Editor editor, IEnumerable<SlotFeature> slots)
	{
		List<SlotFeature> list = ((slots == null) ? new List<SlotFeature>() : slots.ToList());
		editor.WriteMessage("\n[diagnostic] U slots ({0}):", list.Count);
		foreach (SlotFeature item in list)
		{
			editor.WriteMessage(" radius={0:0.###} centerDistance={1:0.###} centers=({2:0.###},{3:0.###})/({4:0.###},{5:0.###});", item.Radius, item.CenterDistance, item.FirstCenter.X, item.FirstCenter.Y, item.SecondCenter.X, item.SecondCenter.Y);
		}
	}

	private bool IsHalfArc(Arc arc)
	{
		double arcSweep = GetArcSweep(arc);
		return Math.Abs(arcSweep - Math.PI) <= Math.PI / 12.0;
	}

	private bool IsHalfArc(OutlineArc arc)
	{
		double distanceTo = arc.Start.GetDistanceTo(arc.End);
		double num = arc.Radius * 2.0;
		double num2 = Math.Max(_config.GeometryTolerance, Math.Max(arc.Radius, 1.0) * 0.05);
		return arc.Radius > _config.GeometryTolerance && Math.Abs(distanceTo - num) <= num2;
	}

	private SlotArcCandidate ToSlotArcCandidate(OutlineArc arc)
	{
		return new SlotArcCandidate
		{
			Id = arc.SourceId,
			Center = arc.Center,
			Start = arc.Start,
			End = arc.End,
			Mid = GetOutlineArcMidPoint(arc),
			Radius = arc.Radius
		};
	}

	private bool CanPairSlotArcs(SlotArcCandidate first, SlotArcCandidate second, out bool horizontal)
	{
		horizontal = false;
		double num = Math.Max(_config.GeometryTolerance, Math.Max(first.Radius, second.Radius) * 0.02);
		if (Math.Abs(first.Radius - second.Radius) > num)
		{
			return false;
		}
		double num2 = Math.Max(_config.GeometryTolerance, Math.Max(first.Radius, second.Radius) * 0.05);
		bool flag = Math.Abs(first.Center.Y - second.Center.Y) <= num2;
		bool flag2 = Math.Abs(first.Center.X - second.Center.X) <= num2;
		if (!flag && !flag2)
		{
			return false;
		}
		double distanceTo = first.Center.GetDistanceTo(second.Center);
		if (distanceTo <= Math.Max(_config.GeometryTolerance, first.Radius * 0.5))
		{
			return false;
		}
		horizontal = flag;
		return true;
	}

	private bool CanPairSingleArcSlotLines(SlotArcCandidate arc, SlotLineCandidate first, SlotLineCandidate second, out bool horizontal)
	{
		horizontal = false;
		if (!TryGetSingleArcSlotLineEndpoints(first, arc, out var arcEndpoint, out var freeEndpoint) || !TryGetSingleArcSlotLineEndpoints(second, arc, out var arcEndpoint2, out var freeEndpoint2))
		{
			return false;
		}
		if (arcEndpoint == arcEndpoint2)
		{
			return false;
		}
		bool flag = IsLineParallelToSlot(first, horizontal: true);
		bool flag2 = IsLineParallelToSlot(second, horizontal: true);
		bool flag3 = IsLineParallelToSlot(first, horizontal: false);
		bool flag4 = IsLineParallelToSlot(second, horizontal: false);
		if (flag && flag2)
		{
			horizontal = true;
		}
		else
		{
			if (!(flag3 && flag4))
			{
				return false;
			}
			horizontal = false;
		}
		double num = Math.Max(_config.GeometryTolerance, arc.Radius * 0.1);
		if (horizontal)
		{
			return Math.Abs(freeEndpoint.X - freeEndpoint2.X) <= num && first.Length > Math.Max(_config.GeometryTolerance, arc.Radius * 0.25) && second.Length > Math.Max(_config.GeometryTolerance, arc.Radius * 0.25);
		}
		return Math.Abs(freeEndpoint.Y - freeEndpoint2.Y) <= num && first.Length > Math.Max(_config.GeometryTolerance, arc.Radius * 0.25) && second.Length > Math.Max(_config.GeometryTolerance, arc.Radius * 0.25);
	}

	private bool IsLineParallelToSlot(SlotLineCandidate line, bool horizontal)
	{
		return horizontal ? (Math.Abs(line.Start.Y - line.End.Y) <= Math.Max(_config.GeometryTolerance, line.Length * 0.01)) : (Math.Abs(line.Start.X - line.End.X) <= Math.Max(_config.GeometryTolerance, line.Length * 0.01));
	}

	private bool ConnectsSlotArcs(SlotLineCandidate line, SlotArcCandidate first, SlotArcCandidate second)
	{
		double tolerance = Math.Max(_config.GeometryTolerance, Math.Max(first.Radius, second.Radius) * 0.08);
		return (PointMatchesArcEndpoint(line.Start, first, tolerance) && PointMatchesArcEndpoint(line.End, second, tolerance)) || (PointMatchesArcEndpoint(line.End, first, tolerance) && PointMatchesArcEndpoint(line.Start, second, tolerance));
	}

	private bool LineTouchesSlotArc(SlotLineCandidate line, SlotArcCandidate arc)
	{
		int arcEndpoint;
		Point2d freeEndpoint;
		return TryGetSingleArcSlotLineEndpoints(line, arc, out arcEndpoint, out freeEndpoint);
	}

	private bool TryGetSingleArcSlotLineEndpoints(SlotLineCandidate line, SlotArcCandidate arc, out int arcEndpoint, out Point2d freeEndpoint)
	{
		double num = Math.Max(_config.GeometryTolerance, arc.Radius * 0.08);
		if (line.Start.GetDistanceTo(arc.Start) <= num)
		{
			arcEndpoint = 1;
			freeEndpoint = line.End;
			return true;
		}
		if (line.End.GetDistanceTo(arc.Start) <= num)
		{
			arcEndpoint = 1;
			freeEndpoint = line.Start;
			return true;
		}
		if (line.Start.GetDistanceTo(arc.End) <= num)
		{
			arcEndpoint = 2;
			freeEndpoint = line.End;
			return true;
		}
		if (line.End.GetDistanceTo(arc.End) <= num)
		{
			arcEndpoint = 2;
			freeEndpoint = line.Start;
			return true;
		}
		arcEndpoint = 0;
		freeEndpoint = Point2d.Origin;
		return false;
	}

	private static bool PointMatchesArcEndpoint(Point2d point, SlotArcCandidate arc, double tolerance)
	{
		return point.GetDistanceTo(arc.Start) <= tolerance || point.GetDistanceTo(arc.End) <= tolerance;
	}

	private static Point2d GetArcMidPoint(Arc arc)
	{
		double arcSweep = GetArcSweep(arc);
		double num = arc.StartAngle + arcSweep / 2.0;
		return new Point2d(arc.Center.X + Math.Cos(num) * arc.Radius, arc.Center.Y + Math.Sin(num) * arc.Radius);
	}

	private static Point2d GetOutlineArcMidPoint(OutlineArc arc)
	{
		if (Math.Abs(arc.Bulge) > 1E-09)
		{
			double num = Math.Atan2(arc.Start.Y - arc.Center.Y, arc.Start.X - arc.Center.X);
			double num2 = Math.Atan2(arc.End.Y - arc.Center.Y, arc.End.X - arc.Center.X);
			double num3 = ((arc.Bulge >= 0.0) ? NormalizePositive(num2 - num) : (0.0 - NormalizePositive(num - num2)));
			double num4 = num + num3 / 2.0;
			return new Point2d(arc.Center.X + Math.Cos(num4) * arc.Radius, arc.Center.Y + Math.Sin(num4) * arc.Radius);
		}
		double num5 = arc.End.X - arc.Start.X;
		double num6 = arc.End.Y - arc.Start.Y;
		double num7 = Math.Sqrt(num5 * num5 + num6 * num6);
		if (num7 <= 1E-09)
		{
			return new Point2d(arc.Center.X + arc.Radius, arc.Center.Y);
		}
		return new Point2d(arc.Center.X - num6 / num7 * arc.Radius, arc.Center.Y + num5 / num7 * arc.Radius);
	}

	private static double GetArcSweep(Arc arc)
	{
		double num;
		for (num = arc.EndAngle - arc.StartAngle; num < 0.0; num += Math.PI * 2.0)
		{
		}
		while (num > Math.PI * 2.0)
		{
			num -= Math.PI * 2.0;
		}
		return num;
	}

	private OutlineFeature RecognizeLightweightOutline(Polyline polyline)
	{
		OutlineFeature outlineFeature = CreateEmptyOutline();
		AddEntityExtents(outlineFeature, polyline);
		List<Point2d> list = new List<Point2d>();
		for (int i = 0; i < polyline.NumberOfVertices; i++)
		{
			Point2d point2dAt = polyline.GetPoint2dAt(i);
			list.Add(point2dAt);
			AddVertex(outlineFeature, point2dAt);
		}
		for (int j = 0; j < list.Count; j++)
		{
			int index = (j + 1) % list.Count;
			double bulgeAt = polyline.GetBulgeAt(j);
			if (Math.Abs(bulgeAt) > BulgeEpsilon)
			{
				AddArcChord(outlineFeature, list[j], list[index], ObjectId.Null, bulgeAt);
			}
			else
			{
				AddSegment(outlineFeature, list[j], list[index], ObjectId.Null);
			}
		}
		return outlineFeature;
	}

	private OutlineFeature RecognizePolyline2dOutline(Polyline2d polyline, Transaction tr)
	{
		OutlineFeature outlineFeature = CreateEmptyOutline();
		AddEntityExtents(outlineFeature, polyline);
		List<Point2d> list = new List<Point2d>();
		List<double> list2 = new List<double>();
		foreach (ObjectId item in polyline)
		{
			Vertex2d vertex2d = tr.GetObject(item, OpenMode.ForRead) as Vertex2d;
			if (!(vertex2d == null))
			{
				Point2d point2d = new Point2d(vertex2d.Position.X, vertex2d.Position.Y);
				list.Add(point2d);
				list2.Add(vertex2d.Bulge);
				AddVertex(outlineFeature, point2d);
			}
		}
		for (int i = 0; i < list.Count; i++)
		{
			Point2d start = list[i];
			Point2d end = list[(i + 1) % list.Count];
			double bulge = list2[i];
			if (Math.Abs(bulge) > BulgeEpsilon)
			{
				AddArcChord(outlineFeature, start, end, ObjectId.Null, bulge);
			}
			else
			{
				AddSegment(outlineFeature, start, end, ObjectId.Null);
			}
		}
		if (outlineFeature.Vertices.Count == 0)
		{
			throw new InvalidOperationException("Outline Polyline has no valid vertices.");
		}
		return outlineFeature;
	}

	private static OutlineFeature CreateEmptyOutline()
	{
		return new OutlineFeature
		{
			MinX = double.MaxValue,
			MinY = double.MaxValue,
			MaxX = double.MinValue,
			MaxY = double.MinValue
		};
	}

	private static void AddEntityExtents(OutlineFeature outline, Entity entity)
	{
		try
		{
			Extents3d geometricExtents = entity.GeometricExtents;
			outline.MinX = Math.Min(outline.MinX, geometricExtents.MinPoint.X);
			outline.MaxX = Math.Max(outline.MaxX, geometricExtents.MaxPoint.X);
			outline.MinY = Math.Min(outline.MinY, geometricExtents.MinPoint.Y);
			outline.MaxY = Math.Max(outline.MaxY, geometricExtents.MaxPoint.Y);
		}
		catch
		{
		}
	}

	private void AddEntityKeyPoints(OutlineFeature outline, Entity entity, Transaction tr)
	{
		Line line = entity as Line;
		if (line != null)
		{
			Point2d point2d = new Point2d(line.StartPoint.X, line.StartPoint.Y);
			Point2d point2d2 = new Point2d(line.EndPoint.X, line.EndPoint.Y);
			AddVertex(outline, point2d);
			AddVertex(outline, point2d2);
			AddSegment(outline, point2d, point2d2, entity.ObjectId);
			return;
		}
		Arc arc = entity as Arc;
		if (arc != null)
		{
			Point2d point2d3 = new Point2d(arc.StartPoint.X, arc.StartPoint.Y);
			Point2d point2d4 = new Point2d(arc.EndPoint.X, arc.EndPoint.Y);
			AddVertex(outline, point2d3);
			AddVertex(outline, point2d4);
			outline.Arcs.Add(new OutlineArc
			{
				Start = point2d3,
				End = point2d4,
				Center = new Point2d(arc.Center.X, arc.Center.Y),
				Radius = arc.Radius,
				SourceId = entity.ObjectId,
				Bulge = Math.Tan(GetArcSweep(arc) / 4.0)
			});
			AddArcEnvelopePoints(outline, point2d3, point2d4, new Point2d(arc.Center.X, arc.Center.Y), arc.Radius, arc.StartAngle, arc.EndAngle, counterClockwise: true);
			return;
		}
		Circle circle = entity as Circle;
		if (circle != null)
		{
			AddVertex(outline, new Point2d(circle.Center.X - circle.Radius, circle.Center.Y));
			AddVertex(outline, new Point2d(circle.Center.X + circle.Radius, circle.Center.Y));
			AddVertex(outline, new Point2d(circle.Center.X, circle.Center.Y - circle.Radius));
			AddVertex(outline, new Point2d(circle.Center.X, circle.Center.Y + circle.Radius));
			return;
		}
		Polyline polyline = entity as Polyline;
		if (polyline != null)
		{
			List<Point2d> list = new List<Point2d>();
			for (int i = 0; i < polyline.NumberOfVertices; i++)
			{
				Point2d point2dAt = polyline.GetPoint2dAt(i);
				list.Add(point2dAt);
				AddVertex(outline, point2dAt);
			}
			for (int j = 0; j < list.Count - 1; j++)
			{
				double bulgeAt = polyline.GetBulgeAt(j);
				if (Math.Abs(bulgeAt) > BulgeEpsilon)
				{
					AddArcChord(outline, list[j], list[j + 1], entity.ObjectId, bulgeAt);
				}
				else
				{
					AddSegment(outline, list[j], list[j + 1], entity.ObjectId);
				}
			}
			if (polyline.Closed && list.Count > 1)
			{
				double bulgeAt2 = polyline.GetBulgeAt(polyline.NumberOfVertices - 1);
				if (Math.Abs(bulgeAt2) > BulgeEpsilon)
				{
					AddArcChord(outline, list[list.Count - 1], list[0], entity.ObjectId, bulgeAt2);
				}
				else
				{
					AddSegment(outline, list[list.Count - 1], list[0], entity.ObjectId);
				}
			}
			return;
		}
		Polyline2d polyline2d = entity as Polyline2d;
		if (!(polyline2d != null))
		{
			return;
		}
		List<Point2d> list3 = new List<Point2d>();
		List<double> list4 = new List<double>();
		foreach (ObjectId item in polyline2d)
		{
			Vertex2d vertex2d = tr.GetObject(item, OpenMode.ForRead) as Vertex2d;
			if (!(vertex2d == null))
			{
				Point2d point2d5 = new Point2d(vertex2d.Position.X, vertex2d.Position.Y);
				list3.Add(point2d5);
				list4.Add(vertex2d.Bulge);
				AddVertex(outline, point2d5);
			}
		}
		for (int k = 0; k < list3.Count - 1; k++)
		{
			if (Math.Abs(list4[k]) > BulgeEpsilon)
			{
				AddArcChord(outline, list3[k], list3[k + 1], entity.ObjectId, list4[k]);
			}
			else
			{
				AddSegment(outline, list3[k], list3[k + 1], entity.ObjectId);
			}
		}
		if (polyline2d.Closed && list3.Count > 1)
		{
			double bulge2 = list4[list4.Count - 1];
			if (Math.Abs(bulge2) > BulgeEpsilon)
			{
				AddArcChord(outline, list3[list3.Count - 1], list3[0], entity.ObjectId, bulge2);
			}
			else
			{
				AddSegment(outline, list3[list3.Count - 1], list3[0], entity.ObjectId);
			}
		}
	}

	private static void AddVertex(OutlineFeature outline, Point2d point)
	{
		outline.Vertices.Add(point);
		outline.MinX = Math.Min(outline.MinX, point.X);
		outline.MaxX = Math.Max(outline.MaxX, point.X);
		outline.MinY = Math.Min(outline.MinY, point.Y);
		outline.MaxY = Math.Max(outline.MaxY, point.Y);
	}

	private static void AddSegment(OutlineFeature outline, Point2d start, Point2d end, ObjectId sourceId, bool isArcChord = false)
	{
		outline.Segments.Add(new OutlineSegment
		{
			Start = start,
			End = end,
			SourceId = sourceId,
			IsArcChord = isArcChord
		});
	}

	private void AddArcChord(OutlineFeature outline, Point2d start, Point2d end, ObjectId sourceId, double bulge)
	{
		double distanceTo = start.GetDistanceTo(end);
		double num = Math.Abs(bulge);
		double num2 = num * distanceTo / 2.0;
		if (!IsFinite(distanceTo) || !IsFinite(bulge) || !IsFinite(num2) || distanceTo <= BulgeEpsilon || num2 <= Math.Max(_config.GeometryTolerance, BulgeEpsilon))
		{
			AddSegment(outline, start, end, sourceId);
			return;
		}
		double num3 = distanceTo * (num + 1.0 / num) / 4.0;
		Point2d point2d = new Point2d((start.X + end.X) / 2.0, (start.Y + end.Y) / 2.0);
		double num4 = (end.X - start.X) / distanceTo;
		double num5 = (end.Y - start.Y) / distanceTo;
		double num6 = 0.0 - num5;
		double num7 = num4;
		double num8 = num3 - num2;
		double num9 = ((bulge >= 0.0) ? 1.0 : (-1.0));
		Point2d center = new Point2d(point2d.X + num6 * num8 * num9, point2d.Y + num7 * num8 * num9);
		if (!IsFinite(num3) || !IsFinite(center.X) || !IsFinite(center.Y))
		{
			AddSegment(outline, start, end, sourceId);
			return;
		}
		AddSegment(outline, start, end, sourceId, isArcChord: true);
		outline.Arcs.Add(new OutlineArc
		{
			Start = start,
			End = end,
			Center = center,
			Radius = num3,
			SourceId = sourceId,
			Bulge = bulge
		});
		AddArcEnvelopePoints(outline, start, end, center, num3, bulge);
	}

	private static bool IsFinite(double value)
	{
		return !double.IsNaN(value) && !double.IsInfinity(value);
	}

	private void AddArcEnvelopePoints(OutlineFeature outline, Point2d start, Point2d end, Point2d center, double radius, double bulge)
	{
		double startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X);
		double endAngle = Math.Atan2(end.Y - center.Y, end.X - center.X);
		AddArcEnvelopePoints(outline, start, end, center, radius, startAngle, endAngle, bulge >= 0.0);
	}

	private void AddArcEnvelopePoints(OutlineFeature outline, Point2d start, Point2d end, Point2d center, double radius, double startAngle, double endAngle, bool counterClockwise)
	{
		AddVertex(outline, start);
		AddVertex(outline, end);
		double[] array = new double[4]
		{
			0.0,
			Math.PI / 2.0,
			Math.PI,
			4.71238898038469
		};
		double[] array2 = array;
		foreach (double num in array2)
		{
			if (ArcContainsAngle(startAngle, endAngle, num, counterClockwise))
			{
				AddVertex(outline, new Point2d(center.X + Math.Cos(num) * radius, center.Y + Math.Sin(num) * radius));
			}
		}
	}

	private static bool ArcContainsAngle(double startAngle, double endAngle, double angle, bool counterClockwise)
	{
		double num = NormalizeAngle(startAngle);
		double num2 = NormalizeAngle(endAngle);
		double num3 = NormalizeAngle(angle);
		if (counterClockwise)
		{
			double num4 = NormalizePositive(num2 - num);
			double num5 = NormalizePositive(num3 - num);
			return num5 <= num4 + 1E-09;
		}
		double num6 = NormalizePositive(num - num2);
		double num7 = NormalizePositive(num - num3);
		return num7 <= num6 + 1E-09;
	}

	private static double NormalizeAngle(double angle)
	{
		double num = Math.PI * 2.0;
		angle %= num;
		return (angle < 0.0) ? (angle + num) : angle;
	}

	private static double NormalizePositive(double angle)
	{
		double num = Math.PI * 2.0;
		angle %= num;
		return (angle < 0.0) ? (angle + num) : angle;
	}

	public void RecognizeOutlineCornerFeatures(OutlineFeature outline)
	{
		outline.Chamfers.Clear();
		outline.Fillets.Clear();
		RecognizeChamfers(outline);
		RecognizeFillets(outline);
		RecognizeInnerGrooveChamfers(outline);
		WriteCornerFeatureDiagnostics(outline);
	}

	private static void WriteCornerFeatureDiagnostics(OutlineFeature outline)
	{
		if (!DiagnosticsEnabled)
		{
			return;
		}
		Document mdiActiveDocument = Application.DocumentManager.MdiActiveDocument;
		if (mdiActiveDocument == null)
		{
			return;
		}
		Editor editor = mdiActiveDocument.Editor;
		editor.WriteMessage("\n[diagnostic] Chamfers ({0}):", outline.Chamfers.Count);
		foreach (ChamferFeature chamfer in outline.Chamfers)
		{
			editor.WriteMessage(" start=({0:0.###},{1:0.###}) end=({2:0.###},{3:0.###}) text={4};", chamfer.StartPoint.X, chamfer.StartPoint.Y, chamfer.EndPoint.X, chamfer.EndPoint.Y, chamfer.Text);
		}
		editor.WriteMessage("\n[diagnostic] Fillets ({0}):", outline.Fillets.Count);
		foreach (FilletFeature fillet in outline.Fillets)
		{
			editor.WriteMessage(" center=({0:0.###},{1:0.###}) radius={2:0.###} text={3};", fillet.Center.X, fillet.Center.Y, fillet.Radius, fillet.Text);
		}
	}

	public void RecognizeChamfers(OutlineFeature outline)
	{
		double num = Math.Max(outline.Width, outline.Height) * 0.25;
		foreach (OutlineSegment segment in outline.Segments)
		{
			if (segment.IsArcChord || segment.IsHorizontal(_config.GeometryTolerance) || segment.IsVertical(_config.GeometryTolerance))
			{
				continue;
			}
			double num2 = Math.Abs(segment.End.X - segment.Start.X);
			double num3 = Math.Abs(segment.End.Y - segment.Start.Y);
			double chamferDimensionLeg = GetChamferDimensionLeg(outline, segment, num2, num3);
			if (segment.Length <= _config.GeometryTolerance || chamferDimensionLeg > num || Math.Abs(num2 - num3) > Math.Max(_config.GeometryTolerance, Math.Max(num2, num3) * 0.05) || !TryGetChamferAxisNeighbors(outline, segment, out var firstNeighbor, out var secondNeighbor))
			{
				continue;
			}
			double num4 = Math.Max(firstNeighbor.Length, secondNeighbor.Length);
			if (num4 <= _config.GeometryTolerance || chamferDimensionLeg > num4 * 0.5 + _config.GeometryTolerance)
			{
				if (IsLocalStepChamferCallout(segment, firstNeighbor, secondNeighbor, outline))
				{
					outline.Chamfers.Add(CreateChamferFeature(segment, num2, num3, chamferDimensionLeg));
				}
			}
			else
			{
				outline.Chamfers.Add(CreateChamferFeature(segment, num2, num3, chamferDimensionLeg));
			}
		}
	}

	private void RecognizeInnerGrooveChamfers(OutlineFeature outline)
	{
		double num = Math.Max(outline.Width, outline.Height) * 0.25;
		foreach (OutlineSegment segment in outline.Segments)
		{
			if (!segment.IsArcChord && !segment.IsHorizontal(_config.GeometryTolerance) && !segment.IsVertical(_config.GeometryTolerance) && IsFortyFiveDegreeSegment(segment) && !IsKnownChamferSegment(segment, outline) && IsInnerGrooveChamferSegment(segment, outline))
			{
				double dx = Math.Abs(segment.End.X - segment.Start.X);
				double dy = Math.Abs(segment.End.Y - segment.Start.Y);
				double chamferDimensionLeg = GetChamferDimensionLeg(outline, segment, dx, dy);
				if (!(segment.Length <= _config.GeometryTolerance) && !(chamferDimensionLeg > num))
				{
					outline.Chamfers.Add(CreateChamferFeature(segment, dx, dy, chamferDimensionLeg));
				}
			}
		}
	}

	private ChamferFeature CreateChamferFeature(OutlineSegment segment, double dx, double dy, double chamferLeg)
	{
		return new ChamferFeature
		{
			StartPoint = segment.Start,
			EndPoint = segment.End,
			Length = segment.Length,
			DeltaX = dx,
			DeltaY = dy,
			Value = chamferLeg,
			Text = "C" + _config.FormatNumber(chamferLeg),
			SourceSegment = segment,
			Confidence = 0.9
		};
	}

	private bool IsLocalStepChamferCallout(OutlineSegment chamfer, OutlineSegment firstNeighbor, OutlineSegment secondNeighbor, OutlineFeature outline)
	{
		return IsAtOuterLocalStep(chamfer.Start, firstNeighbor, outline) || IsAtOuterLocalStep(chamfer.Start, secondNeighbor, outline) || IsAtOuterLocalStep(chamfer.End, firstNeighbor, outline) || IsAtOuterLocalStep(chamfer.End, secondNeighbor, outline);
	}

	private bool IsAtOuterLocalStep(Point2d point, OutlineSegment neighbor, OutlineFeature outline)
	{
		if (neighbor == null)
		{
			return false;
		}
		double num = Math.Max(_config.GeometryTolerance, 0.2);
		if (!(Math.Abs(neighbor.MinX - outline.MinX) <= num) && !(Math.Abs(neighbor.MaxX - outline.MaxX) <= num) && !(Math.Abs(neighbor.MinY - outline.MinY) <= num) && !(Math.Abs(neighbor.MaxY - outline.MaxY) <= num) && !(Math.Abs(point.X - outline.MinX) <= num) && !(Math.Abs(point.X - outline.MaxX) <= num) && !(Math.Abs(point.Y - outline.MinY) <= num) && !(Math.Abs(point.Y - outline.MaxY) <= num))
		{
			return false;
		}
		return neighbor.Length <= Math.Max(outline.Width, outline.Height) * 0.35 + num;
	}

	private double GetChamferDimensionLeg(OutlineFeature outline, OutlineSegment chamfer, double dx, double dy)
	{
		double num = Math.Max(dx, dy);
		List<Point2d> list = new List<Point2d> { chamfer.Start, chamfer.End };
		foreach (OutlineSegment segment in outline.Segments)
		{
			if (segment != chamfer && !segment.IsArcChord && !segment.IsHorizontal(_config.GeometryTolerance) && !segment.IsVertical(_config.GeometryTolerance))
			{
				double num2 = Math.Abs(segment.End.X - segment.Start.X);
				double num3 = Math.Abs(segment.End.Y - segment.Start.Y);
				if (!(Math.Abs(num2 - num3) > Math.Max(_config.GeometryTolerance, Math.Max(num2, num3) * 0.05)) && IsChamferExtensionSegment(chamfer, segment, num))
				{
					list.Add(segment.Start);
					list.Add(segment.End);
				}
			}
		}
		double val = list.Max((Point2d p) => p.X) - list.Min((Point2d p) => p.X);
		double val2 = list.Max((Point2d p) => p.Y) - list.Min((Point2d p) => p.Y);
		return Math.Max(num, Math.Max(val, val2));
	}

	private bool IsChamferExtensionSegment(OutlineSegment chamfer, OutlineSegment candidate, double chamferLeg)
	{
		Vector2d vector2d = chamfer.End - chamfer.Start;
		if (vector2d.Length <= _config.GeometryTolerance)
		{
			return false;
		}
		vector2d = vector2d.GetNormal();
		Vector2d vector2d2 = candidate.End - candidate.Start;
		if (vector2d2.Length <= _config.GeometryTolerance)
		{
			return false;
		}
		vector2d2 = vector2d2.GetNormal();
		if (!(Math.Abs(Math.Abs(vector2d.DotProduct(vector2d2)) - 1.0) <= 0.02))
		{
			return false;
		}
		double num = Math.Max(_config.GeometryTolerance, 0.2);
		if (DistanceFromPointToLine(candidate.Start, chamfer.Start, vector2d) > num || DistanceFromPointToLine(candidate.End, chamfer.Start, vector2d) > num)
		{
			return false;
		}
		double num2 = 0.0;
		double num3 = (chamfer.End - chamfer.Start).DotProduct(vector2d);
		if (num3 < num2)
		{
			double num4 = num2;
			num2 = num3;
			num3 = num4;
		}
		double num5 = (candidate.Start - chamfer.Start).DotProduct(vector2d);
		double num6 = (candidate.End - chamfer.Start).DotProduct(vector2d);
		if (num6 < num5)
		{
			double num7 = num5;
			num5 = num6;
			num6 = num7;
		}
		double num8 = Math.Max(num5 - num3, num2 - num6);
		double num9 = Math.Max(chamferLeg * 0.75, Math.Max(_config.GeometryTolerance, 2.0));
		return num8 <= num9 + _config.GeometryTolerance;
	}

	private bool IsInnerGrooveChamferSegment(OutlineSegment chamfer, OutlineFeature outline)
	{
		return IsHorizontalInnerGrooveChamferSegment(chamfer, outline, isTopSide: true) || IsHorizontalInnerGrooveChamferSegment(chamfer, outline, isTopSide: false) || IsVerticalInnerGrooveChamferSegment(chamfer, outline, leftSide: true) || IsVerticalInnerGrooveChamferSegment(chamfer, outline, leftSide: false);
	}

	private bool IsHorizontalInnerGrooveChamferSegment(OutlineSegment chamfer, OutlineFeature outline, bool isTopSide)
	{
		double tolerance = _config.GeometryTolerance;
		double num = Math.Max(chamfer.Start.Y, chamfer.End.Y);
		double num2 = Math.Min(chamfer.Start.Y, chamfer.End.Y);
		foreach (OutlineSegment item in outline.Segments.Where((OutlineSegment s) => s.IsHorizontal(tolerance) && !s.IsArcChord))
		{
			if (!(Math.Abs(item.MinY - outline.MaxY) <= tolerance) && !(Math.Abs(item.MinY - outline.MinY) <= tolerance) && (!isTopSide || !(item.MinY >= num - tolerance)) && (isTopSide || !(item.MinY <= num2 + tolerance)))
			{
				if (SegmentTouchesPoint(item, chamfer.Start) && HorizontalOtherEndConnectsInnerGroove(item, chamfer.Start, chamfer, outline))
				{
					return true;
				}
				if (SegmentTouchesPoint(item, chamfer.End) && HorizontalOtherEndConnectsInnerGroove(item, chamfer.End, chamfer, outline))
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool IsVerticalInnerGrooveChamferSegment(OutlineSegment chamfer, OutlineFeature outline, bool leftSide)
	{
		double tolerance = _config.GeometryTolerance;
		double num = Math.Min(chamfer.Start.X, chamfer.End.X);
		double num2 = Math.Max(chamfer.Start.X, chamfer.End.X);
		foreach (OutlineSegment item in outline.Segments.Where((OutlineSegment s) => s.IsVertical(tolerance) && !s.IsArcChord))
		{
			if (!(Math.Abs(item.MinX - outline.MinX) <= tolerance) && !(Math.Abs(item.MinX - outline.MaxX) <= tolerance) && (!leftSide || !(item.MinX <= num + tolerance)) && (leftSide || !(item.MinX >= num2 - tolerance)))
			{
				if (SegmentTouchesPoint(item, chamfer.Start) && IsInternalSideGrooveVertical(item, chamfer, leftSide, outline) && VerticalOtherEndConnectsInnerGroove(item, chamfer.Start, chamfer, outline))
				{
					return true;
				}
				if (SegmentTouchesPoint(item, chamfer.End) && IsInternalSideGrooveVertical(item, chamfer, leftSide, outline) && VerticalOtherEndConnectsInnerGroove(item, chamfer.End, chamfer, outline))
				{
					return true;
				}
			}
		}
		return false;
	}

	private bool IsInternalSideGrooveVertical(OutlineSegment vertical, OutlineSegment chamfer, bool leftSide, OutlineFeature outline)
	{
		double geometryTolerance = _config.GeometryTolerance;
		if (!vertical.IsVertical(geometryTolerance) || Math.Abs(vertical.MinX - outline.MinX) <= geometryTolerance || Math.Abs(vertical.MinX - outline.MaxX) <= geometryTolerance)
		{
			return false;
		}
		double num = Math.Min(chamfer.Start.X, chamfer.End.X);
		double num2 = Math.Max(chamfer.Start.X, chamfer.End.X);
		return leftSide ? (vertical.MinX > num + geometryTolerance) : (vertical.MinX < num2 - geometryTolerance);
	}

	private bool HorizontalOtherEndConnectsInnerGroove(OutlineSegment horizontal, Point2d sharedPoint, OutlineSegment currentChamfer, OutlineFeature outline)
	{
		Point2d otherEnd = (PointsEqual(horizontal.Start, sharedPoint) ? horizontal.End : horizontal.Start);
		return outline.Segments.Any((OutlineSegment segment) => segment != currentChamfer && !segment.IsHorizontal(_config.GeometryTolerance) && !segment.IsVertical(_config.GeometryTolerance) && IsFortyFiveDegreeSegment(segment) && (PointsEqual(segment.Start, otherEnd) || PointsEqual(segment.End, otherEnd))) || outline.Chamfers.Any((ChamferFeature chamfer) => PointsEqual(chamfer.StartPoint, otherEnd) || PointsEqual(chamfer.EndPoint, otherEnd)) || outline.Fillets.Any((FilletFeature fillet) => PointsEqual(fillet.StartPoint, otherEnd) || PointsEqual(fillet.EndPoint, otherEnd));
	}

	private bool VerticalOtherEndConnectsInnerGroove(OutlineSegment vertical, Point2d sharedPoint, OutlineSegment currentChamfer, OutlineFeature outline)
	{
		Point2d otherEnd = (PointsEqual(vertical.Start, sharedPoint) ? vertical.End : vertical.Start);
		return outline.Segments.Any((OutlineSegment segment) => segment != currentChamfer && !segment.IsHorizontal(_config.GeometryTolerance) && !segment.IsVertical(_config.GeometryTolerance) && IsFortyFiveDegreeSegment(segment) && (PointsEqual(segment.Start, otherEnd) || PointsEqual(segment.End, otherEnd))) || outline.Chamfers.Any((ChamferFeature chamfer) => PointsEqual(chamfer.StartPoint, otherEnd) || PointsEqual(chamfer.EndPoint, otherEnd)) || outline.Fillets.Any((FilletFeature fillet) => PointsEqual(fillet.StartPoint, otherEnd) || PointsEqual(fillet.EndPoint, otherEnd));
	}

	private bool IsKnownChamferSegment(OutlineSegment segment, OutlineFeature outline)
	{
		return outline.Chamfers.Any((ChamferFeature chamfer) => (PointsEqual(chamfer.StartPoint, segment.Start) && PointsEqual(chamfer.EndPoint, segment.End)) || (PointsEqual(chamfer.StartPoint, segment.End) && PointsEqual(chamfer.EndPoint, segment.Start)));
	}

	private bool IsFortyFiveDegreeSegment(OutlineSegment segment)
	{
		double num = Math.Abs(segment.End.X - segment.Start.X);
		double num2 = Math.Abs(segment.End.Y - segment.Start.Y);
		if (num <= _config.GeometryTolerance || num2 <= _config.GeometryTolerance)
		{
			return false;
		}
		return Math.Abs(num - num2) <= Math.Max(_config.GeometryTolerance, Math.Max(num, num2) * 0.05);
	}

	private bool SegmentTouchesPoint(OutlineSegment segment, Point2d point)
	{
		return PointsEqual(segment.Start, point) || PointsEqual(segment.End, point);
	}

	private static double DistanceFromPointToLine(Point2d point, Point2d linePoint, Vector2d direction)
	{
		Vector2d vector2d = point - linePoint;
		return Math.Abs(vector2d.X * direction.Y - vector2d.Y * direction.X);
	}

	public void RecognizeFillets(OutlineFeature outline)
	{
		double num = Math.Min(outline.Width, outline.Height) * 0.2;
		foreach (OutlineArc arc in outline.Arcs)
		{
			if (!(arc.Radius <= _config.GeometryTolerance) && !(arc.Radius > num) && HasConnectedOutlineSegment(outline, arc.Start) && HasConnectedOutlineSegment(outline, arc.End))
			{
				outline.Fillets.Add(new FilletFeature
				{
					Center = arc.Center,
					Radius = arc.Radius,
					StartPoint = arc.Start,
					EndPoint = arc.End,
					Text = "R" + _config.FormatNumber(arc.Radius),
					SourceArc = arc,
					Confidence = 0.9
				});
			}
		}
	}

	private bool HasAxisNeighborAtPoint(OutlineFeature outline, OutlineSegment source, Point2d point, bool horizontal)
	{
		return outline.Segments.Any((OutlineSegment s) => s != source && !s.IsArcChord && (PointsEqual(s.Start, point) || PointsEqual(s.End, point)) && (horizontal ? s.IsHorizontal(_config.GeometryTolerance) : s.IsVertical(_config.GeometryTolerance)));
	}

	private bool TryGetChamferAxisNeighbors(OutlineFeature outline, OutlineSegment chamfer, out OutlineSegment firstNeighbor, out OutlineSegment secondNeighbor)
	{
		firstNeighbor = null;
		secondNeighbor = null;
		OutlineSegment axisNeighborAtPoint = GetAxisNeighborAtPoint(outline, chamfer, chamfer.Start, horizontal: true);
		OutlineSegment axisNeighborAtPoint2 = GetAxisNeighborAtPoint(outline, chamfer, chamfer.Start, horizontal: false);
		OutlineSegment axisNeighborAtPoint3 = GetAxisNeighborAtPoint(outline, chamfer, chamfer.End, horizontal: true);
		OutlineSegment axisNeighborAtPoint4 = GetAxisNeighborAtPoint(outline, chamfer, chamfer.End, horizontal: false);
		if (axisNeighborAtPoint != null && axisNeighborAtPoint4 != null)
		{
			firstNeighbor = axisNeighborAtPoint;
			secondNeighbor = axisNeighborAtPoint4;
			return true;
		}
		if (axisNeighborAtPoint2 != null && axisNeighborAtPoint3 != null)
		{
			firstNeighbor = axisNeighborAtPoint2;
			secondNeighbor = axisNeighborAtPoint3;
			return true;
		}
		return false;
	}

	private OutlineSegment GetAxisNeighborAtPoint(OutlineFeature outline, OutlineSegment source, Point2d point, bool horizontal)
	{
		return (from s in outline.Segments
			where s != source && !s.IsArcChord && (PointsEqual(s.Start, point) || PointsEqual(s.End, point)) && (horizontal ? s.IsHorizontal(_config.GeometryTolerance) : s.IsVertical(_config.GeometryTolerance))
			orderby s.Length descending
			select s).FirstOrDefault();
	}

	private bool HasConnectedOutlineSegment(OutlineFeature outline, Point2d point)
	{
		return outline.Segments.Any((OutlineSegment s) => !s.IsArcChord && (PointsEqual(s.Start, point) || PointsEqual(s.End, point)));
	}

	private bool PointsEqual(Point2d a, Point2d b)
	{
		return a.GetDistanceTo(b) <= Math.Max(_config.GeometryTolerance, 0.2);
	}

	private static double GetAverageY(IEnumerable<HoleFeature> row)
	{
		return row.Average((HoleFeature h) => h.Center.Y);
	}

	private static double GetAverageDiameter(IEnumerable<HoleFeature> group)
	{
		return group.Average((HoleFeature h) => h.Diameter);
	}

	private IList<Point3d> CollectPinMarkers(IEnumerable<ObjectId> contextIds, IEnumerable<ObjectId> circleIds, Transaction tr)
	{
		HashSet<ObjectId> hashSet = new HashSet<ObjectId>(circleIds);
		List<Point3d> list = new List<Point3d>();
		if (contextIds == null)
		{
			return list;
		}
		int num = 0;
		int num2 = 0;
		List<string> list2 = new List<string>();
		foreach (ObjectId contextId in contextIds)
		{
			if (hashSet.Contains(contextId))
			{
				continue;
			}
			Entity entity = tr.GetObject(contextId, OpenMode.ForRead) as Entity;
			BlockReference blockReference = entity as BlockReference;
			if (!(blockReference == null))
			{
				num++;
				string blockName = GetBlockName(blockReference, tr);
				if (IsPinMarkerBlock(blockReference, tr))
				{
					num2++;
					list.Add(blockReference.Position);
				}
				else if (!string.IsNullOrEmpty(blockName) && !list2.Contains(blockName))
				{
					list2.Add(blockName);
				}
			}
		}
		Document mdiActiveDocument = Application.DocumentManager.MdiActiveDocument;
		if (mdiActiveDocument != null && DiagnosticsEnabled)
		{
			Editor editor = mdiActiveDocument.Editor;
			editor.WriteMessage("\n[璇婃柇] 妗嗛€変腑 BlockReference 鏁伴噺={0}, 閿€瀛旀爣璁版暟閲?{1}", num, num2);
			if (list2.Count > 0 && num2 == 0)
			{
				editor.WriteMessage("\n[璇婃柇] 鏈\ue044尮閰嶇殑鍧楀悕: {0}", string.Join(", ", list2));
			}
		}
		return list;
	}

	private bool HasPinMarker(Point3d center, double radius, IEnumerable<Point3d> markerPoints)
	{
		foreach (Point3d markerPoint in markerPoints)
		{
			if (center.DistanceTo(markerPoint) <= radius + _config.GeometryTolerance)
			{
				return true;
			}
		}
		double num = Math.Max(radius * 0.0, 1.0);
		foreach (Point3d markerPoint2 in markerPoints)
		{
			if (center.DistanceTo(markerPoint2) <= num)
			{
				return true;
			}
		}
		return false;
	}

	private bool IsThreadArc(Arc arc)
	{
		double num;
		for (num = arc.EndAngle - arc.StartAngle; num < 0.0; num += Math.PI * 2.0)
		{
		}
		while (num > Math.PI * 2.0)
		{
			num -= Math.PI * 2.0;
		}
		double num2 = 4.71238898038469;
		return num >= num2 - _config.GeometryTolerance;
	}

	private bool IsConfirmedThreadArc(Arc arc, IEnumerable<ObjectId> sourceIds, Transaction tr)
	{
		if (!IsDrawingLayer(arc.Layer) || !IsThreadArc(arc))
		{
			return false;
		}
		foreach (ObjectId sourceId in sourceIds)
		{
			Circle circle = tr.GetObject(sourceId, OpenMode.ForRead) as Circle;
			if (circle == null || arc.Center.DistanceTo(circle.Center) > _config.GeometryTolerance || !(circle.Radius < arc.Radius - _config.GeometryTolerance))
			{
				continue;
			}
			return true;
		}
		return false;
	}

	private IList<ThreadArcInfo> CollectThreadArcInfos(IEnumerable<ObjectId> sourceIds, Transaction tr)
	{
		List<ThreadArcInfo> list = new List<ThreadArcInfo>();
		if (sourceIds == null)
		{
			return list;
		}
		foreach (ObjectId sourceId in sourceIds)
		{
			Arc arc = tr.GetObject(sourceId, OpenMode.ForRead) as Arc;
			if (arc != null && IsConfirmedThreadArc(arc, sourceIds, tr))
			{
				list.Add(new ThreadArcInfo
				{
					Center = arc.Center,
					Radius = arc.Radius
				});
			}
		}
		return list;
	}

	private bool IsSuppressedByThreadArc(Circle circle, IEnumerable<ThreadArcInfo> threadArcs)
	{
		foreach (ThreadArcInfo threadArc in threadArcs)
		{
			if (circle.Center.DistanceTo(threadArc.Center) <= _config.GeometryTolerance && circle.Radius <= threadArc.Radius + _config.GeometryTolerance)
			{
				return true;
			}
		}
		return false;
	}

	private IList<ThreadMinorCircleInfo> CollectThreadMinorCircles(IEnumerable<ObjectId> sourceIds, Transaction tr, IEnumerable<ThreadArcInfo> threadArcs)
	{
		List<ThreadMinorCircleInfo> list = new List<ThreadMinorCircleInfo>();
		if (sourceIds == null)
		{
			return list;
		}
		foreach (ObjectId sourceId in sourceIds)
		{
			Circle circle = tr.GetObject(sourceId, OpenMode.ForRead) as Circle;
			if (!(circle == null) && HasThreadArcAtCenter(circle.Center, threadArcs))
			{
				string threadCalloutForMinorDiameter = _config.GetThreadCalloutForMinorDiameter(circle.Radius * 2.0);
				if (!string.IsNullOrEmpty(threadCalloutForMinorDiameter))
				{
					list.Add(new ThreadMinorCircleInfo
					{
						Center = circle.Center,
						Diameter = circle.Radius * 2.0,
						Callout = threadCalloutForMinorDiameter
					});
				}
			}
		}
		return list;
	}

	private bool HasThreadArcAtCenter(Point3d center, IEnumerable<ThreadArcInfo> threadArcs)
	{
		foreach (ThreadArcInfo threadArc in threadArcs)
		{
			if (center.DistanceTo(threadArc.Center) <= _config.GeometryTolerance)
			{
				return true;
			}
		}
		return false;
	}

	private string FindThreadMinorCallout(Point3d center, IEnumerable<ThreadMinorCircleInfo> minorCircles)
	{
		foreach (ThreadMinorCircleInfo minorCircle in minorCircles)
		{
			if (center.DistanceTo(minorCircle.Center) <= _config.GeometryTolerance)
			{
				return minorCircle.Callout;
			}
		}
		return string.Empty;
	}

	private static bool IsDrawingLayer(string layerName)
	{
		return string.Equals((layerName ?? string.Empty).Trim(), "DRAWING", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsPinMarkerBlock(BlockReference blockReference, Transaction tr)
	{
		string blockName = GetBlockName(blockReference, tr);
		return string.Equals(blockName, "CadAider_销孔标记", StringComparison.OrdinalIgnoreCase) || string.Equals(blockName, "CadAider_销孔标记背面", StringComparison.OrdinalIgnoreCase);
	}

	private static string GetBlockName(BlockReference blockReference, Transaction tr)
	{
		ObjectId id = (blockReference.IsDynamicBlock ? blockReference.DynamicBlockTableRecord : blockReference.BlockTableRecord);
		BlockTableRecord blockTableRecord = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord;
		if (blockTableRecord != null)
		{
			return blockTableRecord.Name;
		}
		return string.Empty;
	}
}
