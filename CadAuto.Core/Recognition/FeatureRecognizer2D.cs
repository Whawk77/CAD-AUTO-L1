using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Rules;

namespace CadAuto.Core.Recognition;

// Test-only shadow recognizer. Production commands use CadAuto.CadAdapter.Recognition.FeatureRecognizer.
public sealed class FeatureRecognizer2D
{
	private sealed class SlotArcCandidate
	{
		public string Id { get; set; }

		public Point2D Center { get; set; }

		public Point2D Start { get; set; }

		public Point2D End { get; set; }

		public Point2D Mid { get; set; }

		public double Radius { get; set; }
	}

	private sealed class SlotLineCandidate
	{
		public string Id { get; set; }

		public Point2D Start { get; set; }

		public Point2D End { get; set; }

		public double Length => Start.DistanceTo(End);
	}

	private readonly DimensionRuleConfig _config;

	public FeatureRecognizer2D(DimensionRuleConfig config)
	{
		_config = config;
	}

	public void RecognizeOutlineCornerFeatures(OutlineFeature2D outline)
	{
		if (outline == null)
		{
			throw new ArgumentNullException("outline");
		}
		outline.Chamfers.Clear();
		outline.Fillets.Clear();
		RecognizeChamfers(outline);
		RecognizeFillets(outline);
		RecognizeInnerGrooveChamfers(outline);
	}

	public IList<SlotFeature2D> RecognizeSlotFeatures(DrawingGeometry geometry)
	{
		if (geometry == null)
		{
			throw new ArgumentNullException("geometry");
		}
		return RecognizeSlotFeatures(geometry.Arcs, geometry.Segments);
	}

	public IList<SlotFeature2D> RecognizeOutlineSlotFeatures(OutlineFeature2D outline)
	{
		if (outline == null)
		{
			throw new ArgumentNullException("outline");
		}
		return RecognizeSlotFeatures(outline.Arcs, outline.Segments);
	}

	public IList<SlotFeature2D> RecognizeSlotFeatures(IEnumerable<Arc2D> arcs, IEnumerable<Segment2D> segments)
	{
		List<SlotArcCandidate> arcCandidates = (arcs ?? new Arc2D[0]).Where((Arc2D a) => a != null && IsHalfArc(a)).Select(ToSlotArcCandidate).ToList();
		List<SlotLineCandidate> list = (segments ?? new Segment2D[0]).Where((Segment2D s) => s != null && !s.IsArcChord).Select(ToSlotLineCandidate).ToList();
		List<SlotFeature2D> list2 = new List<SlotFeature2D>();
		HashSet<string> hashSet = new HashSet<string>();
		HashSet<string> usedLines = new HashSet<string>();
		int slotIndex = 1;
		for (int i = 0; i < arcCandidates.Count; i++)
		{
			if (hashSet.Contains(arcCandidates[i].Id))
			{
				continue;
			}
			for (int j = i + 1; j < arcCandidates.Count; j++)
			{
				if (!hashSet.Contains(arcCandidates[j].Id) && CanPairSlotArcs(arcCandidates[i], arcCandidates[j], out var horizontal))
				{
					List<SlotLineCandidate> list3 = list.Where((SlotLineCandidate l) => !usedLines.Contains(l.Id) && IsLineParallelToSlot(l, horizontal) && ConnectsSlotArcs(l, arcCandidates[i], arcCandidates[j])).ToList();
					if (list3.Count == 2)
					{
						list2.Add(new SlotFeature2D
						{
							GroupId = CreateSlotGroupId(slotIndex),
							FirstCenter = arcCandidates[i].Center,
							SecondCenter = arcCandidates[j].Center,
							Radius = (arcCandidates[i].Radius + arcCandidates[j].Radius) / 2.0,
							CenterDistance = arcCandidates[i].Center.DistanceTo(arcCandidates[j].Center),
							IsVertical = !horizontal
						});
						hashSet.Add(arcCandidates[i].Id);
						hashSet.Add(arcCandidates[j].Id);
						usedLines.Add(list3[0].Id);
						usedLines.Add(list3[1].Id);
						slotIndex++;
						break;
					}
				}
			}
		}
		RecognizeSingleArcSlots(arcCandidates, list, hashSet, usedLines, list2, ref slotIndex);
		return list2;
	}

	public OutlineFeature2D RecognizeOutlineFromClosedPath(IEnumerable<Point2D> vertices)
	{
		if (vertices == null)
		{
			throw new ArgumentNullException("vertices");
		}
		List<Point2D> list = vertices.ToList();
		if (list.Count < 3)
		{
			throw new InvalidOperationException("Outline needs at least three vertices.");
		}
		if (PointsEqual(list[0], list[list.Count - 1]))
		{
			list.RemoveAt(list.Count - 1);
		}
		OutlineFeature2D outlineFeature2D = new OutlineFeature2D
		{
			MinX = list.Min((Point2D p) => p.X),
			MaxX = list.Max((Point2D p) => p.X),
			MinY = list.Min((Point2D p) => p.Y),
			MaxY = list.Max((Point2D p) => p.Y)
		};
		foreach (Point2D item in list)
		{
			outlineFeature2D.Vertices.Add(item);
		}
		for (int num = 0; num < list.Count; num++)
		{
			int index = (num + 1) % list.Count;
			outlineFeature2D.Segments.Add(new Segment2D(list[num], list[index])
			{
				SourceKey = "path:" + num.ToString(CultureInfo.InvariantCulture)
			});
		}
		RecognizeOutlineCornerFeatures(outlineFeature2D);
		return outlineFeature2D;
	}

	public OutlineFeature2D RecognizeOutlineFromSegments(IEnumerable<Segment2D> segments, IEnumerable<Arc2D> arcs)
	{
		if (segments == null)
		{
			throw new ArgumentNullException("segments");
		}
		List<Segment2D> list = segments.Where((Segment2D s) => s != null).ToList();
		if (list.Count == 0)
		{
			throw new InvalidOperationException("Outline needs at least one segment.");
		}
		List<Arc2D> list2 = (arcs ?? new Arc2D[0]).Where((Arc2D a) => a != null).ToList();
		OutlineFeature2D outlineFeature2D = new OutlineFeature2D();
		foreach (Segment2D item in list)
		{
			outlineFeature2D.Segments.Add(item);
			AddUniqueVertex(outlineFeature2D, item.Start);
			AddUniqueVertex(outlineFeature2D, item.End);
		}
		foreach (Arc2D item2 in list2)
		{
			outlineFeature2D.Arcs.Add(item2);
			AddUniqueVertex(outlineFeature2D, item2.Start);
			AddUniqueVertex(outlineFeature2D, item2.End);
		}
		if (!OutlineGeometryQuery.TryGetEnvelope(outlineFeature2D, _config.GeometryTolerance, out var envelope))
		{
			throw new InvalidOperationException("Outline needs valid line or arc geometry.");
		}
		outlineFeature2D.MinX = envelope.MinX;
		outlineFeature2D.MaxX = envelope.MaxX;
		outlineFeature2D.MinY = envelope.MinY;
		outlineFeature2D.MaxY = envelope.MaxY;
		RecognizeOutlineCornerFeatures(outlineFeature2D);
		return outlineFeature2D;
	}

	public void RecognizeChamfers(OutlineFeature2D outline)
	{
		double num = Math.Max(outline.Width, outline.Height) * 0.25;
		foreach (Segment2D item in outline.Segments.Where((Segment2D s) => !s.IsArcChord))
		{
			if (item.IsHorizontal(_config.GeometryTolerance) || item.IsVertical(_config.GeometryTolerance))
			{
				continue;
			}
			double dx = Math.Abs(item.Start.X - item.End.X);
			double dy = Math.Abs(item.Start.Y - item.End.Y);
			double chamferDimensionLeg = GetChamferDimensionLeg(outline, item, dx, dy);
			if (item.Length <= _config.GeometryTolerance || chamferDimensionLeg > num || !IsFortyFiveDegreeSegment(item) || !TryGetChamferAxisNeighbors(outline, item, out var firstNeighbor, out var secondNeighbor))
			{
				continue;
			}
			double num2 = Math.Max(firstNeighbor.Length, secondNeighbor.Length);
			if (num2 <= _config.GeometryTolerance || chamferDimensionLeg > num2 * 0.5 + _config.GeometryTolerance)
			{
				if (IsLocalStepChamferCallout(item, firstNeighbor, secondNeighbor, outline))
				{
					outline.Chamfers.Add(CreateChamferFeature(item, dx, dy, chamferDimensionLeg));
				}
			}
			else
			{
				outline.Chamfers.Add(CreateChamferFeature(item, dx, dy, chamferDimensionLeg));
			}
		}
	}

	private void RecognizeInnerGrooveChamfers(OutlineFeature2D outline)
	{
		double num = Math.Max(outline.Width, outline.Height) * 0.25;
		foreach (Segment2D segment in outline.Segments)
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

	public void RecognizeFillets(OutlineFeature2D outline)
	{
		double num = Math.Min(outline.Width, outline.Height) * 0.2;
		foreach (Arc2D arc in outline.Arcs)
		{
			if (!(arc.Radius <= _config.GeometryTolerance) && !(arc.Radius > num) && HasConnectedOutlineSegment(outline, arc.Start) && HasConnectedOutlineSegment(outline, arc.End))
			{
				outline.Fillets.Add(new FilletFeature2D
				{
					Center = arc.Center,
					Radius = arc.Radius,
					StartPoint = arc.Start,
					EndPoint = arc.End,
					SourceArc = arc,
					Text = "R" + _config.FormatNumber(arc.Radius),
					Confidence = 0.9
				});
			}
		}
	}

	private void RecognizeSingleArcSlots(IList<SlotArcCandidate> arcs, IList<SlotLineCandidate> lines, ISet<string> usedArcs, ISet<string> usedLines, IList<SlotFeature2D> slots, ref int slotIndex)
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
						slots.Add(new SlotFeature2D
						{
							GroupId = CreateSlotGroupId(slotIndex),
							FirstCenter = arc.Center,
							SecondCenter = arc.Center,
							Radius = arc.Radius,
							CenterDistance = 0.0,
							IsSingleArcSlot = true,
							IsVertical = !horizontal,
							ArcLeaderTarget = arc.Mid
						});
						usedArcs.Add(arc.Id);
						usedLines.Add(list[num].Id);
						usedLines.Add(list[num2].Id);
						slotIndex++;
						num = list.Count;
						break;
					}
				}
			}
		}
	}

	private SlotArcCandidate ToSlotArcCandidate(Arc2D arc)
	{
		return new SlotArcCandidate
		{
			Id = (arc.SourceKey ?? string.Empty),
			Center = arc.Center,
			Start = arc.Start,
			End = arc.End,
			Mid = GetArcMidPoint(arc),
			Radius = arc.Radius
		};
	}

	private SlotLineCandidate ToSlotLineCandidate(Segment2D segment)
	{
		return new SlotLineCandidate
		{
			Id = (segment.SourceKey ?? string.Empty),
			Start = segment.Start,
			End = segment.End
		};
	}

	private bool IsHalfArc(Arc2D arc)
	{
		double num = arc.Start.DistanceTo(arc.End);
		double num2 = arc.Radius * 2.0;
		double num3 = Math.Max(_config.GeometryTolerance, Math.Max(arc.Radius, 1.0) * 0.05);
		return arc.Radius > _config.GeometryTolerance && Math.Abs(num - num2) <= num3;
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
		double num3 = first.Center.DistanceTo(second.Center);
		if (num3 <= Math.Max(_config.GeometryTolerance, first.Radius * 0.5))
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
		Point2D freeEndpoint;
		return TryGetSingleArcSlotLineEndpoints(line, arc, out arcEndpoint, out freeEndpoint);
	}

	private bool TryGetSingleArcSlotLineEndpoints(SlotLineCandidate line, SlotArcCandidate arc, out int arcEndpoint, out Point2D freeEndpoint)
	{
		double num = Math.Max(_config.GeometryTolerance, arc.Radius * 0.08);
		if (line.Start.DistanceTo(arc.Start) <= num)
		{
			arcEndpoint = 1;
			freeEndpoint = line.End;
			return true;
		}
		if (line.End.DistanceTo(arc.Start) <= num)
		{
			arcEndpoint = 1;
			freeEndpoint = line.Start;
			return true;
		}
		if (line.Start.DistanceTo(arc.End) <= num)
		{
			arcEndpoint = 2;
			freeEndpoint = line.End;
			return true;
		}
		if (line.End.DistanceTo(arc.End) <= num)
		{
			arcEndpoint = 2;
			freeEndpoint = line.Start;
			return true;
		}
		arcEndpoint = 0;
		freeEndpoint = default(Point2D);
		return false;
	}

	private static bool PointMatchesArcEndpoint(Point2D point, SlotArcCandidate arc, double tolerance)
	{
		return point.DistanceTo(arc.Start) <= tolerance || point.DistanceTo(arc.End) <= tolerance;
	}

	private Point2D GetArcMidPoint(Arc2D arc)
	{
		if (Math.Abs(arc.Bulge) > 1E-09)
		{
			double num = Math.Atan2(arc.Start.Y - arc.Center.Y, arc.Start.X - arc.Center.X);
			double num2 = Math.Atan2(arc.End.Y - arc.Center.Y, arc.End.X - arc.Center.X);
			double num3 = num2 - num;
			if (arc.Bulge > 0.0 && num3 < 0.0)
			{
				num3 += Math.PI * 2.0;
			}
			else if (arc.Bulge < 0.0 && num3 > 0.0)
			{
				num3 -= Math.PI * 2.0;
			}
			double num4 = num + num3 / 2.0;
			return new Point2D(arc.Center.X + Math.Cos(num4) * arc.Radius, arc.Center.Y + Math.Sin(num4) * arc.Radius);
		}
		return new Point2D((arc.Start.X + arc.End.X) / 2.0, (arc.Start.Y + arc.End.Y) / 2.0);
	}

	private static string CreateSlotGroupId(int slotIndex)
	{
		return "SLOT" + slotIndex.ToString(CultureInfo.InvariantCulture);
	}

	private ChamferFeature2D CreateChamferFeature(Segment2D segment, double dx, double dy, double chamferLeg)
	{
		return new ChamferFeature2D
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

	private bool IsLocalStepChamferCallout(Segment2D chamfer, Segment2D firstNeighbor, Segment2D secondNeighbor, OutlineFeature2D outline)
	{
		return IsAtOuterLocalStep(chamfer.Start, firstNeighbor, outline) || IsAtOuterLocalStep(chamfer.Start, secondNeighbor, outline) || IsAtOuterLocalStep(chamfer.End, firstNeighbor, outline) || IsAtOuterLocalStep(chamfer.End, secondNeighbor, outline);
	}

	private bool IsAtOuterLocalStep(Point2D point, Segment2D neighbor, OutlineFeature2D outline)
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

	private double GetChamferDimensionLeg(OutlineFeature2D outline, Segment2D chamfer, double dx, double dy)
	{
		double num = Math.Max(dx, dy);
		List<Point2D> list = new List<Point2D> { chamfer.Start, chamfer.End };
		foreach (Segment2D segment in outline.Segments)
		{
			if (segment != chamfer && !segment.IsArcChord && !segment.IsHorizontal(_config.GeometryTolerance) && !segment.IsVertical(_config.GeometryTolerance) && IsFortyFiveDegreeSegment(segment) && IsChamferExtensionSegment(chamfer, segment, num))
			{
				list.Add(segment.Start);
				list.Add(segment.End);
			}
		}
		double val = list.Max((Point2D p) => p.X) - list.Min((Point2D p) => p.X);
		double val2 = list.Max((Point2D p) => p.Y) - list.Min((Point2D p) => p.Y);
		return Math.Max(num, Math.Max(val, val2));
	}

	private bool IsChamferExtensionSegment(Segment2D chamfer, Segment2D candidate, double chamferLeg)
	{
		double num = chamfer.End.X - chamfer.Start.X;
		double num2 = chamfer.End.Y - chamfer.Start.Y;
		double num3 = Math.Sqrt(num * num + num2 * num2);
		if (num3 <= _config.GeometryTolerance)
		{
			return false;
		}
		num /= num3;
		num2 /= num3;
		double num4 = candidate.End.X - candidate.Start.X;
		double num5 = candidate.End.Y - candidate.Start.Y;
		double num6 = Math.Sqrt(num4 * num4 + num5 * num5);
		if (num6 <= _config.GeometryTolerance)
		{
			return false;
		}
		num4 /= num6;
		num5 /= num6;
		double value = num * num4 + num2 * num5;
		if (!(Math.Abs(Math.Abs(value) - 1.0) <= 0.02))
		{
			return false;
		}
		double num7 = Math.Max(_config.GeometryTolerance, 0.2);
		if (DistanceFromPointToLine(candidate.Start, chamfer.Start, num, num2) > num7 || DistanceFromPointToLine(candidate.End, chamfer.Start, num, num2) > num7)
		{
			return false;
		}
		double num8 = 0.0;
		double num9 = ProjectAlong(chamfer.End, chamfer.Start, num, num2);
		if (num9 < num8)
		{
			double num10 = num8;
			num8 = num9;
			num9 = num10;
		}
		double num11 = ProjectAlong(candidate.Start, chamfer.Start, num, num2);
		double num12 = ProjectAlong(candidate.End, chamfer.Start, num, num2);
		if (num12 < num11)
		{
			double num13 = num11;
			num11 = num12;
			num12 = num13;
		}
		double num14 = Math.Max(num11 - num9, num8 - num12);
		double num15 = Math.Max(chamferLeg * 0.75, Math.Max(_config.GeometryTolerance, 2.0));
		return num14 <= num15 + _config.GeometryTolerance;
	}

	private bool IsInnerGrooveChamferSegment(Segment2D chamfer, OutlineFeature2D outline)
	{
		return IsHorizontalInnerGrooveChamferSegment(chamfer, outline, isTopSide: true) || IsHorizontalInnerGrooveChamferSegment(chamfer, outline, isTopSide: false) || IsVerticalInnerGrooveChamferSegment(chamfer, outline, leftSide: true) || IsVerticalInnerGrooveChamferSegment(chamfer, outline, leftSide: false);
	}

	private bool IsHorizontalInnerGrooveChamferSegment(Segment2D chamfer, OutlineFeature2D outline, bool isTopSide)
	{
		double tolerance = _config.GeometryTolerance;
		double num = Math.Max(chamfer.Start.Y, chamfer.End.Y);
		double num2 = Math.Min(chamfer.Start.Y, chamfer.End.Y);
		foreach (Segment2D item in outline.Segments.Where((Segment2D s) => s.IsHorizontal(tolerance) && !s.IsArcChord))
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

	private bool IsVerticalInnerGrooveChamferSegment(Segment2D chamfer, OutlineFeature2D outline, bool leftSide)
	{
		double tolerance = _config.GeometryTolerance;
		double num = Math.Min(chamfer.Start.X, chamfer.End.X);
		double num2 = Math.Max(chamfer.Start.X, chamfer.End.X);
		foreach (Segment2D item in outline.Segments.Where((Segment2D s) => s.IsVertical(tolerance) && !s.IsArcChord))
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

	private bool IsInternalSideGrooveVertical(Segment2D vertical, Segment2D chamfer, bool leftSide, OutlineFeature2D outline)
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

	private bool HorizontalOtherEndConnectsInnerGroove(Segment2D horizontal, Point2D sharedPoint, Segment2D currentChamfer, OutlineFeature2D outline)
	{
		Point2D otherEnd = (PointsEqual(horizontal.Start, sharedPoint) ? horizontal.End : horizontal.Start);
		return outline.Segments.Any((Segment2D segment) => segment != currentChamfer && !segment.IsHorizontal(_config.GeometryTolerance) && !segment.IsVertical(_config.GeometryTolerance) && IsFortyFiveDegreeSegment(segment) && (PointsEqual(segment.Start, otherEnd) || PointsEqual(segment.End, otherEnd))) || outline.Chamfers.Any((ChamferFeature2D chamfer) => PointsEqual(chamfer.StartPoint, otherEnd) || PointsEqual(chamfer.EndPoint, otherEnd)) || outline.Fillets.Any((FilletFeature2D fillet) => PointsEqual(fillet.StartPoint, otherEnd) || PointsEqual(fillet.EndPoint, otherEnd));
	}

	private bool VerticalOtherEndConnectsInnerGroove(Segment2D vertical, Point2D sharedPoint, Segment2D currentChamfer, OutlineFeature2D outline)
	{
		Point2D otherEnd = (PointsEqual(vertical.Start, sharedPoint) ? vertical.End : vertical.Start);
		return outline.Segments.Any((Segment2D segment) => segment != currentChamfer && !segment.IsHorizontal(_config.GeometryTolerance) && !segment.IsVertical(_config.GeometryTolerance) && IsFortyFiveDegreeSegment(segment) && (PointsEqual(segment.Start, otherEnd) || PointsEqual(segment.End, otherEnd))) || outline.Chamfers.Any((ChamferFeature2D chamfer) => PointsEqual(chamfer.StartPoint, otherEnd) || PointsEqual(chamfer.EndPoint, otherEnd)) || outline.Fillets.Any((FilletFeature2D fillet) => PointsEqual(fillet.StartPoint, otherEnd) || PointsEqual(fillet.EndPoint, otherEnd));
	}

	private bool IsKnownChamferSegment(Segment2D segment, OutlineFeature2D outline)
	{
		return outline.Chamfers.Any((ChamferFeature2D chamfer) => (PointsEqual(chamfer.StartPoint, segment.Start) && PointsEqual(chamfer.EndPoint, segment.End)) || (PointsEqual(chamfer.StartPoint, segment.End) && PointsEqual(chamfer.EndPoint, segment.Start)));
	}

	private bool IsFortyFiveDegreeSegment(Segment2D segment)
	{
		double num = Math.Abs(segment.End.X - segment.Start.X);
		double num2 = Math.Abs(segment.End.Y - segment.Start.Y);
		if (num <= _config.GeometryTolerance || num2 <= _config.GeometryTolerance)
		{
			return false;
		}
		return Math.Abs(num - num2) <= Math.Max(_config.GeometryTolerance, Math.Max(num, num2) * 0.05);
	}

	private bool SegmentTouchesPoint(Segment2D segment, Point2D point)
	{
		return PointsEqual(segment.Start, point) || PointsEqual(segment.End, point);
	}

	private static double DistanceFromPointToLine(Point2D point, Point2D linePoint, double directionX, double directionY)
	{
		double num = point.X - linePoint.X;
		double num2 = point.Y - linePoint.Y;
		return Math.Abs(num * directionY - num2 * directionX);
	}

	private static double ProjectAlong(Point2D point, Point2D linePoint, double directionX, double directionY)
	{
		return (point.X - linePoint.X) * directionX + (point.Y - linePoint.Y) * directionY;
	}

	private bool TryGetChamferAxisNeighbors(OutlineFeature2D outline, Segment2D chamfer, out Segment2D firstNeighbor, out Segment2D secondNeighbor)
	{
		firstNeighbor = FindAxisNeighborAtPoint(outline, chamfer, chamfer.Start);
		secondNeighbor = FindAxisNeighborAtPoint(outline, chamfer, chamfer.End);
		return firstNeighbor != null && secondNeighbor != null && firstNeighbor != secondNeighbor && ArePerpendicularAxisSegments(firstNeighbor, secondNeighbor);
	}

	private Segment2D FindAxisNeighborAtPoint(OutlineFeature2D outline, Segment2D chamfer, Point2D point)
	{
		return (from segment in outline.Segments
			where segment != chamfer && !segment.IsArcChord && (segment.IsHorizontal(_config.GeometryTolerance) || segment.IsVertical(_config.GeometryTolerance)) && (PointsEqual(segment.Start, point) || PointsEqual(segment.End, point))
			orderby segment.Length descending
			select segment).FirstOrDefault();
	}

	private bool ArePerpendicularAxisSegments(Segment2D first, Segment2D second)
	{
		return (first.IsHorizontal(_config.GeometryTolerance) && second.IsVertical(_config.GeometryTolerance)) || (first.IsVertical(_config.GeometryTolerance) && second.IsHorizontal(_config.GeometryTolerance));
	}

	private bool PointsEqual(Point2D a, Point2D b)
	{
		return a.DistanceTo(b) <= Math.Max(_config.GeometryTolerance, 0.2);
	}

	private bool HasConnectedOutlineSegment(OutlineFeature2D outline, Point2D point)
	{
		return outline.Segments.Any((Segment2D segment) => !segment.IsArcChord && (PointsEqual(segment.Start, point) || PointsEqual(segment.End, point)));
	}

	private void AddUniqueVertex(OutlineFeature2D outline, Point2D point)
	{
		if (!outline.Vertices.Any((Point2D existing) => PointsEqual(existing, point)))
		{
			outline.Vertices.Add(point);
		}
	}
}
