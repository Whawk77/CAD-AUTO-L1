using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;
using CadAuto.CadAdapter.Mapping;
using CadAuto.Core.Planning;
using CadAuto.CadAdapter.Recognition;
using CadAuto.Core.Rules;

namespace CadAuto.CadAdapter.Collection;

public sealed class GeometryCollector
{
	private sealed class OutlineEntityCandidate
	{
		public ObjectId ObjectId { get; set; }

		public double BoundsArea { get; set; }

		public List<Point2d> ContinuityPoints { get; set; }

		public bool IsClosed { get; set; }

		public double LineWeightScore { get; set; }

		public double LayerSemanticScore { get; set; }

		public double Length { get; set; }

		public double Score { get; set; }
	}

	private readonly Editor _editor;

	public GeometryCollector(Editor editor)
	{
		_editor = editor;
	}

	public OutlineSelection PromptForOutlineSelection(Transaction tr)
	{
		PromptSelectionOptions options = new PromptSelectionOptions
		{
			MessageForAdding = "\n请选择 DRAWING 或 0 图层上的零件外轮廓，可点选或框选: ",
			SingleOnly = false
		};
		PromptSelectionResult selection = _editor.GetSelection(options);
		if (selection.Status != PromptStatus.OK)
		{
			throw new OperationCanceledException("用户取消选择外轮廓。");
		}
		List<OutlineEntityCandidate> list = new List<OutlineEntityCandidate>();
		List<OutlineEntityCandidate> list2 = new List<OutlineEntityCandidate>();
		List<ObjectId> list3 = new List<ObjectId>();
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		int num4 = 0;
		int num5 = 0;
		foreach (SelectedObject item in selection.Value)
		{
			if (item == null || item.ObjectId.IsNull)
			{
				continue;
			}
			num++;
			list3.Add(item.ObjectId);
			Entity entity = tr.GetObject(item.ObjectId, OpenMode.ForRead) as Entity;
			if (entity == null || !IsDrawingLayer(entity.Layer))
			{
				continue;
			}
			num2++;
			if (!HasOutlineLayerLinetype(entity, tr))
			{
				continue;
			}
			if (IsFallbackOutlineEntity(entity))
			{
				num5++;
				list2.Add(BuildOutlineEntityCandidate(item.ObjectId, entity, tr));
			}
			if (IsSupportedPolyline(entity))
			{
				num3++;
				if (IsClosedPolyline(entity))
				{
					num4++;
					list.Add(BuildOutlineEntityCandidate(item.ObjectId, entity, tr));
				}
			}
		}
		if (list.Count > 0)
		{
			OutlineSelection outlineSelection = new OutlineSelection
			{
				PrimaryPolylineId = (from c in list
					orderby c.Score descending, c.BoundsArea descending
					select c).First().ObjectId
			};
			foreach (ObjectId item2 in list3)
			{
				outlineSelection.SelectedIds.Add(item2);
			}
			return outlineSelection;
		}
		if (list2.Count > 0)
		{
			List<OutlineEntityCandidate> list4 = SelectMainOutlineComponent(list2);
			_editor.WriteMessage("\n未找到闭合 Polyline，已改用 DRAWING 或 0 图层上的 {0} 个线/圆弧轮廓对象计算外包围盒。", list4.Count);
			OutlineSelection outlineSelection2 = new OutlineSelection();
			foreach (ObjectId item3 in list4.Select((OutlineEntityCandidate c) => c.ObjectId))
			{
				outlineSelection2.EntityIds.Add(item3);
			}
			foreach (ObjectId item4 in list3)
			{
				outlineSelection2.SelectedIds.Add(item4);
			}
			return outlineSelection2;
		}
		_editor.WriteMessage("\n外轮廓筛选结果: 已选 {0} 个对象，DRAWING/0 图层 {1} 个，多段线 {2} 个，闭合多段线 {3} 个，可用线/圆弧轮廓对象 {4} 个。", num, num2, num3, num4, num5);
		throw new InvalidOperationException("选择集中没有 DRAWING 或 0 图层上的可用外轮廓对象。");
	}

	public IList<ClosedViewOutline> PromptForClosedViewOutlines(Transaction tr)
	{
		PromptSelectionOptions options = new PromptSelectionOptions
		{
			MessageForAdding = "\n请框选同一零件的主视、俯视、左视。每个视图一条闭合多段线，或一组首尾相接的 DRAWING/0 轮廓线: ",
			SingleOnly = false
		};
		PromptSelectionResult selection = _editor.GetSelection(options);
		if (selection.Status != PromptStatus.OK)
		{
			throw new OperationCanceledException("用户取消选择视图外轮廓。");
		}
		List<ObjectId> selectedIds = new List<ObjectId>();
		List<OutlineEntityCandidate> closedPolylines = new List<OutlineEntityCandidate>();
		List<OutlineEntityCandidate> outlineEntities = new List<OutlineEntityCandidate>();
		int selectedCount = 0;
		foreach (SelectedObject item in selection.Value)
		{
			if (item == null || item.ObjectId.IsNull)
			{
				continue;
			}
			selectedCount++;
			selectedIds.Add(item.ObjectId);
			Entity entity = tr.GetObject(item.ObjectId, OpenMode.ForRead) as Entity;
			if (entity == null || !IsDrawingLayer(entity.Layer) || !HasOutlineLayerLinetype(entity, tr))
			{
				continue;
			}
			if (IsSupportedPolyline(entity) && IsClosedPolyline(entity))
			{
				closedPolylines.Add(BuildOutlineEntityCandidate(item.ObjectId, entity, tr));
			}
			if (IsFallbackOutlineEntity(entity))
			{
				outlineEntities.Add(BuildOutlineEntityCandidate(item.ObjectId, entity, tr));
			}
		}
		List<List<OutlineEntityCandidate>> connected = GroupConnectedComponents(outlineEntities);
		List<List<OutlineEntityCandidate>> merged = MergeOverlappingComponents(connected, 0.01)
			.Where(HasViewExtent)
			.ToList();
		List<List<OutlineEntityCandidate>> views = merged
			.Where((List<OutlineEntityCandidate> component) => !IsContainedByLargerComponent(component, merged))
			.ToList();
		_editor.WriteMessage(
			"\nASD3V 视图线框: 已选 {0} 个对象，DRAWING/0 轮廓对象 {1} 个，端点连通 {2} 组，重叠合并后 {3} 个视图。",
			selectedCount,
			outlineEntities.Count,
			connected.Count,
			views.Count);
		List<ClosedViewOutline> outlines = new List<ClosedViewOutline>();
		foreach (List<OutlineEntityCandidate> component in views)
		{
			ClosedViewOutline outline = CreateViewOutline(component, closedPolylines);
			outline.SelectedIds.AddRange(selectedIds);
			outlines.Add(outline);
		}
		return outlines;
	}

	private static ClosedViewOutline CreateViewOutline(List<OutlineEntityCandidate> component, IList<OutlineEntityCandidate> closedPolylines)
	{
		double minX = double.MaxValue;
		double minY = double.MaxValue;
		double maxX = double.MinValue;
		double maxY = double.MinValue;
		foreach (OutlineEntityCandidate candidate in component)
		{
			foreach (Point2d point in candidate.ContinuityPoints)
			{
				minX = Math.Min(minX, point.X);
				minY = Math.Min(minY, point.Y);
				maxX = Math.Max(maxX, point.X);
				maxY = Math.Max(maxY, point.Y);
			}
		}
		ClosedViewOutline outline = new ClosedViewOutline
		{
			MinX = minX,
			MinY = minY,
			MaxX = maxX,
			MaxY = maxY,
			ViewId = component.Select((OutlineEntityCandidate candidate) => candidate.ObjectId.Handle.ToString()).OrderBy((string handle) => handle, StringComparer.Ordinal).First()
		};
		OutlineEntityCandidate solePolyline = component.Count == 1
			? closedPolylines.FirstOrDefault((OutlineEntityCandidate candidate) => candidate.ObjectId == component[0].ObjectId)
			: null;
		if (solePolyline != null)
		{
			outline.PolylineId = solePolyline.ObjectId;
		}
		else
		{
			foreach (OutlineEntityCandidate candidate in component)
			{
				outline.EntityIds.Add(candidate.ObjectId);
			}
		}
		return outline;
	}

	private static bool HasViewExtent(IList<OutlineEntityCandidate> component)
	{
		GetComponentBounds(component, out double minX, out double minY, out double maxX, out double maxY);
		return maxX - minX > 0.5 && maxY - minY > 0.5;
	}

	private static bool IsContainedByLargerComponent(List<OutlineEntityCandidate> component, IList<List<OutlineEntityCandidate>> views)
	{
		GetComponentBounds(component, out double minX, out double minY, out double maxX, out double maxY);
		double area = (maxX - minX) * (maxY - minY);
		foreach (List<OutlineEntityCandidate> other in views)
		{
			if (ReferenceEquals(other, component))
			{
				continue;
			}
			GetComponentBounds(other, out double otherMinX, out double otherMinY, out double otherMaxX, out double otherMaxY);
			double otherArea = (otherMaxX - otherMinX) * (otherMaxY - otherMinY);
			if (otherArea <= area + 0.01)
			{
				continue;
			}
			if (minX >= otherMinX - 0.01 && minY >= otherMinY - 0.01 && maxX <= otherMaxX + 0.01 && maxY <= otherMaxY + 0.01)
			{
				return true;
			}
		}
		return false;
	}

	private static List<List<OutlineEntityCandidate>> MergeOverlappingComponents(List<List<OutlineEntityCandidate>> components, double gap)
	{
		int count = components.Count;
		int[] parent = new int[count];
		double[] minX = new double[count];
		double[] minY = new double[count];
		double[] maxX = new double[count];
		double[] maxY = new double[count];
		for (int i = 0; i < count; i++)
		{
			parent[i] = i;
			GetComponentBounds(components[i], out minX[i], out minY[i], out maxX[i], out maxY[i]);
		}
		bool changed = true;
		while (changed)
		{
			changed = false;
			for (int i = 0; i < count; i++)
			{
				for (int j = i + 1; j < count; j++)
				{
					int left = FindComponent(parent, i);
					int right = FindComponent(parent, j);
					if (left == right || !BoundsOverlap(minX[left], minY[left], maxX[left], maxY[left], minX[right], minY[right], maxX[right], maxY[right], gap))
					{
						continue;
					}
					parent[right] = left;
					minX[left] = Math.Min(minX[left], minX[right]);
					minY[left] = Math.Min(minY[left], minY[right]);
					maxX[left] = Math.Max(maxX[left], maxX[right]);
					maxY[left] = Math.Max(maxY[left], maxY[right]);
					changed = true;
				}
			}
		}
		List<List<OutlineEntityCandidate>> merged = new List<List<OutlineEntityCandidate>>();
		foreach (IGrouping<int, int> group in Enumerable.Range(0, count).GroupBy((int index) => FindComponent(parent, index)))
		{
			List<OutlineEntityCandidate> component = new List<OutlineEntityCandidate>();
			foreach (int index in group)
			{
				component.AddRange(components[index]);
			}
			merged.Add(component);
		}
		return merged;
	}

	private static int FindComponent(int[] parent, int index)
	{
		while (parent[index] != index)
		{
			parent[index] = parent[parent[index]];
			index = parent[index];
		}
		return index;
	}

	private static bool BoundsOverlap(double minX, double minY, double maxX, double maxY, double otherMinX, double otherMinY, double otherMaxX, double otherMaxY, double gap)
	{
		return minX <= otherMaxX + gap && maxX + gap >= otherMinX && minY <= otherMaxY + gap && maxY + gap >= otherMinY;
	}

	private static void GetComponentBounds(IList<OutlineEntityCandidate> component, out double minX, out double minY, out double maxX, out double maxY)
	{
		minX = double.MaxValue;
		minY = double.MaxValue;
		maxX = double.MinValue;
		maxY = double.MinValue;
		foreach (OutlineEntityCandidate candidate in component)
		{
			foreach (Point2d point in candidate.ContinuityPoints)
			{
				minX = Math.Min(minX, point.X);
				minY = Math.Min(minY, point.Y);
				maxX = Math.Max(maxX, point.X);
				maxY = Math.Max(maxY, point.Y);
			}
		}
	}

	public IList<CenterlineEndpointDefinition> CollectHoleCenterlineEndpoints(Transaction tr, OutlineSelection outlineSelection, double tolerance)
	{
		List<CenterlineEndpointDefinition> endpoints = new List<CenterlineEndpointDefinition>();
		if (tr == null || outlineSelection == null)
		{
			return endpoints;
		}
		double effectiveTolerance = Math.Max(Math.Abs(tolerance), 1E-9);
		foreach (ObjectId selectedId in outlineSelection.SelectedIds)
		{
			Line line = tr.GetObject(selectedId, OpenMode.ForRead) as Line;
			if (line == null || !IsCenterlineLayer(line.Layer))
			{
				continue;
			}
			string sourceGeometryId = selectedId.Handle.ToString();
			AddUniquePoint(endpoints, line.StartPoint, sourceGeometryId, effectiveTolerance);
			AddUniquePoint(endpoints, line.EndPoint, sourceGeometryId, effectiveTolerance);
		}
		return endpoints;
	}

	private static void AddUniquePoint(IList<CenterlineEndpointDefinition> points, Point3d point, string sourceGeometryId, double tolerance)
	{
		if (!points.Any(existing => existing.Point.DistanceTo(point) <= tolerance))
		{
			points.Add(new CenterlineEndpointDefinition
			{
				Point = point,
				SourceGeometryId = sourceGeometryId ?? string.Empty
			});
		}
	}

	public IList<ObjectId> PromptForCircleHoles()
	{
		PromptSelectionOptions options = new PromptSelectionOptions
		{
			MessageForAdding = "\n请选择需要标注的圆孔 Circle: "
		};
		SelectionFilter filter = new SelectionFilter(new TypedValue[1]
		{
			new TypedValue(0, "CIRCLE")
		});
		PromptSelectionResult selection = _editor.GetSelection(options, filter);
		if (selection.Status != PromptStatus.OK)
		{
			return null;
		}
		List<ObjectId> list = new List<ObjectId>();
		foreach (SelectedObject item in selection.Value)
		{
			if (item != null && !item.ObjectId.IsNull)
			{
				list.Add(item.ObjectId);
			}
		}
		if (list.Count == 0)
		{
			return null;
		}
		return list;
	}

	public IList<ObjectId> CollectCircleHolesFromOutlineSelection(Transaction tr, OutlineFeature outline, OutlineSelection outlineSelection, double tolerance)
	{
		HashSet<ObjectId> hashSet = new HashSet<ObjectId>();
		if (outlineSelection != null)
		{
			if (outlineSelection.HasPrimaryPolyline)
			{
				hashSet.Add(outlineSelection.PrimaryPolylineId);
			}
			foreach (ObjectId entityId in outlineSelection.EntityIds)
			{
				hashSet.Add(entityId);
			}
		}
		List<ObjectId> list = new List<ObjectId>();
		if (outlineSelection == null)
		{
			return list;
		}
		foreach (ObjectId selectedId in outlineSelection.SelectedIds)
		{
			if (!hashSet.Contains(selectedId))
			{
				Circle circle = tr.GetObject(selectedId, OpenMode.ForRead) as Circle;
				if (!(circle == null) && IsPointInsideBounds(circle.Center, outline, tolerance))
				{
					list.Add(selectedId);
				}
			}
		}
		_editor.WriteMessage("\n从本次框选对象中自动识别到 Circle 孔 {0} 个。", list.Count);
		return list;
	}

	public IList<ObjectId> CollectHoleSourcesFromOutlineSelection(Transaction tr, OutlineFeature outline, OutlineSelection outlineSelection, DimensionRuleConfig config)
	{
		double geometryTolerance = config.GeometryTolerance;
		HashSet<ObjectId> hashSet = new HashSet<ObjectId>();
		if (outlineSelection != null)
		{
			if (outlineSelection.HasPrimaryPolyline)
			{
				hashSet.Add(outlineSelection.PrimaryPolylineId);
			}
			foreach (ObjectId entityId in outlineSelection.EntityIds)
			{
				hashSet.Add(entityId);
			}
		}
		List<ObjectId> list = new List<ObjectId>();
		if (outlineSelection == null)
		{
			return list;
		}
		ISet<ObjectId> closedCircleArcs = new FeatureRecognizer(config)
			.CollectClosedCircleArcIds(outlineSelection.SelectedIds, tr);
		foreach (ObjectId selectedId in outlineSelection.SelectedIds)
		{
			Entity entity = tr.GetObject(selectedId, OpenMode.ForRead) as Entity;
			if (entity == null)
			{
				continue;
			}
			Circle circle = entity as Circle;
			if (circle != null)
			{
				if (!hashSet.Contains(selectedId) && IsPointInsideBounds(circle.Center, outline, geometryTolerance))
				{
					list.Add(selectedId);
				}
				continue;
			}
			Arc arc = entity as Arc;
			if (arc != null && IsDrawingLayer(arc.Layer) && IsThreadArc(arc, config) && IsPointInsideBounds(arc.Center, outline, geometryTolerance))
			{
				list.Add(selectedId);
			}
			else
			{
				// A merged view component may contain hole halves as well as outer edges.
				if (hashSet.Contains(selectedId) && !closedCircleArcs.Contains(selectedId))
				{
					continue;
				}
				if (arc != null && (IsDrawingLayer(arc.Layer) || string.Equals(arc.Layer, "HIDDEN", StringComparison.OrdinalIgnoreCase))
					&& !IsThreadArc(arc, config) && IsPointInsideBounds(arc.Center, outline, geometryTolerance))
				{
					list.Add(selectedId);
					continue;
				}
				Line line = entity as Line;
				if (line != null && IsDrawingLayer(line.Layer) && IsLineInsideBounds(line, outline, geometryTolerance))
				{
					list.Add(selectedId);
				}
			}
		}
		_editor.WriteMessage("\n从本次框选对象中自动识别到孔特征 {0} 个。", list.Count);
		return list;
	}

	public DatumDefinition PromptForDatum(OutlineFeature outline)
	{
		return DatumDefinition.FromOutline(outline);
	}

	public HoleFeature PromptForDatumHole(List<HoleFeature> pinHoles, Transaction tr, OutlineFeature outline)
	{
		int count = pinHoles?.Count ?? 0;
		_editor.WriteMessage("\n自动识别销孔数量: {0}", count);
		if (count == 0)
		{
			return null;
		}
		if (count == 1)
		{
			HoleFeature holeFeature = pinHoles[0];
			_editor.WriteMessage("\n仅有一个合法销孔候选，已自动设为基准孔: X={0:0.###}, Y={1:0.###}", holeFeature.Center.X, holeFeature.Center.Y);
			return holeFeature;
		}
		PromptEntityOptions promptEntityOptions = new PromptEntityOptions("\n请选择一个销孔作为基准孔: ");
		promptEntityOptions.SetRejectMessage("\n所选对象不是 Circle 或 CadAider_销孔标记块，请重新选择销孔。");
		promptEntityOptions.AddAllowedClass(typeof(Circle), exactMatch: true);
		promptEntityOptions.AddAllowedClass(typeof(BlockReference), exactMatch: true);
		PromptEntityResult entity = _editor.GetEntity(promptEntityOptions);
		if (entity.Status != PromptStatus.OK)
		{
			return null;
		}
		BlockReference blockReference = tr.GetObject(entity.ObjectId, OpenMode.ForRead) as BlockReference;
		if (blockReference != null)
		{
			if (!FeatureRecognizer.IsPinMarkerBlock(blockReference, tr))
			{
				_editor.WriteMessage("\n所选块不是 CadAider_销孔标记块，将使用轮廓边线基准。");
				return null;
			}
			HoleFeature markerHole = FindPinHoleByMarkerPosition(pinHoles, blockReference.Position);
			if (markerHole != null)
			{
				_editor.WriteMessage("\n已匹配到 CadAider_销孔标记块对应的销孔，直径={0:0.###}", markerHole.Diameter);
				return markerHole;
			}
			_editor.WriteMessage("\n所选 CadAider_销孔标记块未匹配到已识别销孔，将使用轮廓边线基准。");
			return null;
		}
		foreach (HoleFeature pinHole in pinHoles)
		{
			if (pinHole.CircleId == entity.ObjectId)
			{
				_editor.WriteMessage("\n已匹配到已识别销孔 (ObjectId)，直径={0:0.###}", pinHole.Diameter);
				return pinHole;
			}
		}
		Circle circle = tr.GetObject(entity.ObjectId, OpenMode.ForRead) as Circle;
		if (circle == null)
		{
			_editor.WriteMessage("\n所选对象无法读取为 Circle，将使用轮廓边线基准。");
			return null;
		}
		_editor.WriteMessage("\n用户选择 Circle 直径={0:0.###}", circle.Radius * 2.0);
		foreach (HoleFeature pinHole2 in pinHoles)
		{
			double num = pinHole2.Center.DistanceTo(circle.Center);
			double num2 = Math.Abs(pinHole2.Diameter / 2.0 - circle.Radius);
			if (num <= 0.01 && num2 <= 0.01)
			{
				_editor.WriteMessage("\n已匹配到已识别销孔 (容差匹配)，直径={0:0.###}", pinHole2.Diameter);
				return pinHole2;
			}
		}
		if (circle.Radius < 0.5 || circle.Radius > 100.0)
		{
			_editor.WriteMessage("\n所选 Circle 半径={0:0.###} 超出合理孔径范围，将使用轮廓边线基准。", circle.Radius);
			return null;
		}
		if (outline != null && !IsPointInsideBounds(circle.Center, outline, 0.01))
		{
			_editor.WriteMessage("\n所选 Circle 圆心不在外轮廓范围内，将使用轮廓边线基准。");
			return null;
		}
		_editor.WriteMessage("\n所选 Circle 未在自动识别销孔列表中，已作为手动基准销孔使用。");
		return new HoleFeature
		{
			Center = circle.Center,
			Diameter = circle.Radius * 2.0,
			SourceId = entity.ObjectId,
			CircleId = entity.ObjectId,
			HoleKind = HoleKind.Pin
		};
	}

	private static HoleFeature FindPinHoleByMarkerPosition(IEnumerable<HoleFeature> pinHoles, Point3d markerPosition)
	{
		HoleFeature result = null;
		double bestDistance = double.MaxValue;
		foreach (HoleFeature pinHole in pinHoles ?? Enumerable.Empty<HoleFeature>())
		{
			if (pinHole == null)
			{
				continue;
			}
			double distance = pinHole.Center.DistanceTo(markerPosition);
			double matchDistance = Math.Max(pinHole.Diameter / 2.0 + 0.01, 1.0);
			if (distance <= matchDistance && distance < bestDistance)
			{
				result = pinHole;
				bestDistance = distance;
			}
		}
		return result;
	}

	public bool PromptForDatumHoleLocationPoints(DimensionRuleConfig config, out double? xBase, out double? yBase, out bool useToleranceX, out bool useToleranceY)
	{
		xBase = null;
		yBase = null;
		useToleranceX = false;
		useToleranceY = false;
		PromptPointOptions options = new PromptPointOptions("\n请选择基准孔 X 轴方向上的基准点: ");
		PromptPointResult point = _editor.GetPoint(options);
		if (point.Status != PromptStatus.OK)
		{
			return false;
		}
		xBase = point.Value.X;
		if (!PromptForDatumHoleToleranceMode(config, "X", out useToleranceX))
		{
			return false;
		}
		PromptPointOptions options2 = new PromptPointOptions("\n请选择基准孔 Y 轴方向上的基准点: ");
		PromptPointResult point2 = _editor.GetPoint(options2);
		if (point2.Status != PromptStatus.OK)
		{
			return false;
		}
		yBase = point2.Value.Y;
		if (!PromptForDatumHoleToleranceMode(config, "Y", out useToleranceY))
		{
			return false;
		}
		return true;
	}

	public bool PromptForDatumPinGroupLayout(OutlineFeature outline, IEnumerable<HoleFeature> holes, HoleFeature datumPin,
		DimensionRuleConfig config, out CadAuto.Core.Model.PinGroupVerticalLayoutPreference vertical,
		out CadAuto.Core.Model.PinGroupHorizontalLayoutPreference horizontal)
	{
		vertical = CadAuto.Core.Model.PinGroupVerticalLayoutPreference.Automatic;
		horizontal = CadAuto.Core.Model.PinGroupHorizontalLayoutPreference.Automatic;
		var orientation = new DimensionPlanner(config).GetSelectedPinGroupLayoutOrientation(
			CadToCoreModelMapper.ToCoreOutline(outline), CadToCoreModelMapper.ToCoreHoles(holes),
			datumPin == null ? null : CadToCoreModelMapper.ToCoreHoles(new[] { datumPin }).Single());
		string message = orientation == DimensionOrientation.Horizontal
			? "\n基准孔所在组的水平尺寸布局 [W=上侧/S=下侧] <自动>: "
			: orientation == DimensionOrientation.Vertical
				? "\n基准孔所在组的竖向尺寸布局 [A=左侧/D=右侧] <自动>: "
				: "\n基准孔所在组的尺寸布局 [A=左侧/D=右侧/W=上侧/S=下侧] <自动>: ";
		var options = new PromptKeywordOptions(message);
		if (orientation != DimensionOrientation.Horizontal) { options.Keywords.Add("A"); options.Keywords.Add("D"); }
		if (orientation != DimensionOrientation.Vertical) { options.Keywords.Add("W"); options.Keywords.Add("S"); }
		options.AllowNone = true;
		PromptResult result = _editor.GetKeywords(options);
		if (result.Status == PromptStatus.None) { return true; }
		if (result.Status != PromptStatus.OK) { return false; }
		if (orientation != DimensionOrientation.Horizontal && string.Equals(result.StringResult, "A", StringComparison.OrdinalIgnoreCase))
		{
			vertical = CadAuto.Core.Model.PinGroupVerticalLayoutPreference.Left;
			return true;
		}
		if (orientation != DimensionOrientation.Horizontal && string.Equals(result.StringResult, "D", StringComparison.OrdinalIgnoreCase))
		{
			vertical = CadAuto.Core.Model.PinGroupVerticalLayoutPreference.Right;
			return true;
		}
		if (orientation != DimensionOrientation.Vertical && string.Equals(result.StringResult, "W", StringComparison.OrdinalIgnoreCase))
		{
			horizontal = CadAuto.Core.Model.PinGroupHorizontalLayoutPreference.Top;
			return true;
		}
		if (orientation != DimensionOrientation.Vertical && string.Equals(result.StringResult, "S", StringComparison.OrdinalIgnoreCase))
		{
			horizontal = CadAuto.Core.Model.PinGroupHorizontalLayoutPreference.Bottom;
			return true;
		}
		return false;
	}

	private bool PromptForDatumHoleToleranceMode(DimensionRuleConfig config, string axisName, out bool useTolerance)
	{
		useTolerance = config?.DatumHoleLocationDefaultUseTolerance ?? false;
		string text = (useTolerance ? "S" : "A");
		PromptKeywordOptions promptKeywordOptions = new PromptKeywordOptions("\nDatum hole " + axisName + " tolerance [A=NoTolerance/S=PlusMinus0.05] <" + text + ">: ");
		promptKeywordOptions.Keywords.Add("A");
		promptKeywordOptions.Keywords.Add("S");
		promptKeywordOptions.Keywords.Default = text;
		promptKeywordOptions.AllowNone = true;
		PromptResult keywords = _editor.GetKeywords(promptKeywordOptions);
		if (keywords.Status == PromptStatus.None)
		{
			return true;
		}
		if (keywords.Status != PromptStatus.OK)
		{
			return false;
		}
		useTolerance = string.Equals(keywords.StringResult, "S", StringComparison.OrdinalIgnoreCase);
		return true;
	}

	public bool PromptForManualCornerLeaderPlacement()
	{
		PromptKeywordOptions promptKeywordOptions = new PromptKeywordOptions("\nCorner feature text placement [Auto/Manual] <Auto>: ");
		promptKeywordOptions.Keywords.Add("Auto");
		promptKeywordOptions.Keywords.Add("Manual");
		promptKeywordOptions.Keywords.Default = "Auto";
		promptKeywordOptions.AllowNone = true;
		PromptResult keywords = _editor.GetKeywords(promptKeywordOptions);
		if (keywords.Status == PromptStatus.None)
		{
			return false;
		}
		return keywords.Status == PromptStatus.OK && string.Equals(keywords.StringResult, "Manual", StringComparison.OrdinalIgnoreCase);
	}

	private static double GetBoundsArea(Entity entity)
	{
		try
		{
			Extents3d geometricExtents = entity.GeometricExtents;
			return (geometricExtents.MaxPoint.X - geometricExtents.MinPoint.X) * (geometricExtents.MaxPoint.Y - geometricExtents.MinPoint.Y);
		}
		catch
		{
			return 0.0;
		}
	}

	private static OutlineEntityCandidate BuildOutlineEntityCandidate(ObjectId id, Entity entity, Transaction tr)
	{
		List<Point2d> continuityPoints = GetContinuityPoints(entity, tr);
		double boundsArea = GetBoundsArea(entity);
		double layerSemanticScore = GetLayerSemanticScore(entity.Layer);
		double lineWeightScore = GetLineWeightScore(entity, tr);
		bool flag = IsClosedPolyline(entity);
		return new OutlineEntityCandidate
		{
			ObjectId = id,
			BoundsArea = boundsArea,
			ContinuityPoints = continuityPoints,
			IsClosed = flag,
			LineWeightScore = lineWeightScore,
			LayerSemanticScore = layerSemanticScore,
			Length = GetEntityLength(entity, tr),
			Score = layerSemanticScore * 1000000000000.0 + lineWeightScore * 1000000000.0 + (flag ? 100000000.0 : 0.0) + (double)continuityPoints.Count * 1000000.0 + boundsArea
		};
	}

	private static List<OutlineEntityCandidate> SelectMainOutlineComponent(IList<OutlineEntityCandidate> candidates)
	{
		return GroupConnectedComponents(candidates).OrderByDescending(GetComponentScore).First();
	}

	private static List<List<OutlineEntityCandidate>> GroupConnectedComponents(IList<OutlineEntityCandidate> candidates)
	{
		HashSet<OutlineEntityCandidate> hashSet = new HashSet<OutlineEntityCandidate>(candidates);
		List<List<OutlineEntityCandidate>> list = new List<List<OutlineEntityCandidate>>();
		while (hashSet.Count > 0)
		{
			OutlineEntityCandidate item = hashSet.First();
			List<OutlineEntityCandidate> list2 = new List<OutlineEntityCandidate>();
			Queue<OutlineEntityCandidate> queue = new Queue<OutlineEntityCandidate>();
			queue.Enqueue(item);
			hashSet.Remove(item);
			while (queue.Count > 0)
			{
				OutlineEntityCandidate outlineEntityCandidate = queue.Dequeue();
				list2.Add(outlineEntityCandidate);
				foreach (OutlineEntityCandidate item2 in hashSet.ToList())
				{
					if (AreConnected(outlineEntityCandidate, item2))
					{
						hashSet.Remove(item2);
						queue.Enqueue(item2);
					}
				}
			}
			list.Add(list2);
		}
		return list;
	}

	private static double GetComponentScore(IList<OutlineEntityCandidate> component)
	{
		double num = double.MaxValue;
		double num2 = double.MaxValue;
		double num3 = double.MinValue;
		double num4 = double.MinValue;
		foreach (OutlineEntityCandidate item in component)
		{
			foreach (Point2d continuityPoint in item.ContinuityPoints)
			{
				num = Math.Min(num, continuityPoint.X);
				num3 = Math.Max(num3, continuityPoint.X);
				num2 = Math.Min(num2, continuityPoint.Y);
				num4 = Math.Max(num4, continuityPoint.Y);
			}
		}
		double num5 = ((num == double.MaxValue) ? 0.0 : ((num3 - num) * (num4 - num2)));
		return component.Max((OutlineEntityCandidate c) => c.LayerSemanticScore) * 1000000000000.0 + component.Max((OutlineEntityCandidate c) => c.LineWeightScore) * 1000000000.0 + (IsClosedComponent(component) ? 100000000.0 : 0.0) + (double)component.Count * 1000000.0 + component.Sum((OutlineEntityCandidate c) => c.Length) + num5;
	}

	private static bool IsClosedComponent(IList<OutlineEntityCandidate> component)
	{
		if (component.Any((OutlineEntityCandidate c) => c.IsClosed))
		{
			return true;
		}
		List<Point2d> list = component.SelectMany((OutlineEntityCandidate c) => c.ContinuityPoints).ToList();
		if (list.Count <= 2)
		{
			return false;
		}
		foreach (Point2d point in list)
		{
			if (list.Count((Point2d p) => AreSamePoint(point, p)) < 2)
			{
				return false;
			}
		}
		return true;
	}

	private static bool AreConnected(OutlineEntityCandidate a, OutlineEntityCandidate b)
	{
		foreach (Point2d continuityPoint in a.ContinuityPoints)
		{
			foreach (Point2d continuityPoint2 in b.ContinuityPoints)
			{
				if (AreSamePoint(continuityPoint, continuityPoint2))
				{
					return true;
				}
			}
		}
		return false;
	}

	private static bool AreSamePoint(Point2d a, Point2d b)
	{
		return a.GetDistanceTo(b) <= 0.01;
	}

	private static List<Point2d> GetContinuityPoints(Entity entity, Transaction tr)
	{
		Line line = entity as Line;
		if (line != null)
		{
			return new List<Point2d>
			{
				new Point2d(line.StartPoint.X, line.StartPoint.Y),
				new Point2d(line.EndPoint.X, line.EndPoint.Y)
			};
		}
		Arc arc = entity as Arc;
		if (arc != null)
		{
			return new List<Point2d>
			{
				new Point2d(arc.StartPoint.X, arc.StartPoint.Y),
				new Point2d(arc.EndPoint.X, arc.EndPoint.Y)
			};
		}
		Polyline polyline = entity as Polyline;
		if (polyline != null)
		{
			List<Point2d> list = new List<Point2d>();
			for (int i = 0; i < polyline.NumberOfVertices; i++)
			{
				list.Add(polyline.GetPoint2dAt(i));
			}
			return list;
		}
		Polyline2d polyline2d = entity as Polyline2d;
		if (polyline2d != null)
		{
			List<Point2d> list2 = new List<Point2d>();
			foreach (ObjectId item in polyline2d)
			{
				Vertex2d vertex2d = tr.GetObject(item, OpenMode.ForRead) as Vertex2d;
				if (vertex2d != null)
				{
					list2.Add(new Point2d(vertex2d.Position.X, vertex2d.Position.Y));
				}
			}
			return list2;
		}
		return new List<Point2d>();
	}

	private static double GetEntityLength(Entity entity, Transaction tr)
	{
		Line line = entity as Line;
		if (line != null)
		{
			return line.Length;
		}
		Arc arc = entity as Arc;
		if (arc != null)
		{
			return arc.Length;
		}
		Polyline polyline = entity as Polyline;
		if (polyline != null)
		{
			return polyline.Length;
		}
		Polyline2d polyline2d = entity as Polyline2d;
		if (polyline2d != null)
		{
			List<Point2d> continuityPoints = GetContinuityPoints(polyline2d, tr);
			double num = 0.0;
			for (int i = 1; i < continuityPoints.Count; i++)
			{
				num += continuityPoints[i - 1].GetDistanceTo(continuityPoints[i]);
			}
			if (polyline2d.Closed && continuityPoints.Count > 1)
			{
				num += continuityPoints[continuityPoints.Count - 1].GetDistanceTo(continuityPoints[0]);
			}
			return num;
		}
		return 0.0;
	}

	private static double GetLayerSemanticScore(string layerName)
	{
		string text = (layerName ?? string.Empty).Trim();
		if (string.Equals(text, "DRAWING", StringComparison.OrdinalIgnoreCase))
		{
			return 100.0;
		}
		if (string.Equals(text, "0", StringComparison.OrdinalIgnoreCase))
		{
			return 90.0;
		}
		return (text.IndexOf("OUTLINE", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("CONTOUR", StringComparison.OrdinalIgnoreCase) >= 0) ? 80.0 : 0.0;
	}

	private static double GetLineWeightScore(Entity entity, Transaction tr)
	{
		LineWeight lineWeight = entity.LineWeight;
		if (lineWeight == LineWeight.ByLayer)
		{
			LayerTableRecord layerRecord = GetLayerRecord(entity, tr);
			if (layerRecord != null)
			{
				lineWeight = layerRecord.LineWeight;
			}
		}
		int num = (int)lineWeight;
		return (num > 0) ? ((double)num) : 0.0;
	}

	private static bool HasOutlineLayerLinetype(Entity entity, Transaction tr)
	{
		LayerTableRecord layerRecord = GetLayerRecord(entity, tr);
		if (layerRecord == null || layerRecord.LinetypeObjectId.IsNull)
		{
			return true;
		}
		LinetypeTableRecord linetypeTableRecord = tr.GetObject(layerRecord.LinetypeObjectId, OpenMode.ForRead) as LinetypeTableRecord;
		string linetypeName = ((linetypeTableRecord == null) ? string.Empty : linetypeTableRecord.Name);
		return !IsNonOutlineLinetype(linetypeName);
	}

	private static LayerTableRecord GetLayerRecord(Entity entity, Transaction tr)
	{
		if (entity == null || entity.LayerId.IsNull)
		{
			return null;
		}
		return tr.GetObject(entity.LayerId, OpenMode.ForRead) as LayerTableRecord;
	}

	private static bool IsNonOutlineLinetype(string linetypeName)
	{
		string text = (linetypeName ?? string.Empty).Trim();
		if (text.Length == 0)
		{
			return false;
		}
		return text.IndexOf("CENTER", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("HIDDEN", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("DASH", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("DOT", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("PHANTOM", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static bool IsDrawingLayer(string layerName)
	{
		string text = (layerName ?? string.Empty).Trim();
		return string.Equals(text, "DRAWING", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "0", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsCenterlineLayer(string layerName)
	{
		string text = (layerName ?? string.Empty).Trim();
		return string.Equals(text, "CENTER", StringComparison.OrdinalIgnoreCase)
			|| string.Equals(text, "CEN", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsSupportedPolyline(Entity entity)
	{
		return entity is Polyline || entity is Polyline2d;
	}

	private static bool IsClosedPolyline(Entity entity)
	{
		Polyline polyline = entity as Polyline;
		if (polyline != null)
		{
			return polyline.Closed;
		}
		Polyline2d polyline2d = entity as Polyline2d;
		return polyline2d != null && polyline2d.Closed;
	}

	private static bool IsFallbackOutlineEntity(Entity entity)
	{
		Arc arc = entity as Arc;
		if (arc != null && IsDrawingLayer(arc.Layer) && IsThreadArc(arc, DimensionRuleConfig.CreateDefault()))
		{
			return false;
		}
		return entity is Line || entity is Arc || entity is Polyline || entity is Polyline2d;
	}

	private static bool IsPointInsideBounds(Point3d point, OutlineFeature outline, double tolerance)
	{
		return point.X >= outline.MinX - tolerance && point.X <= outline.MaxX + tolerance && point.Y >= outline.MinY - tolerance && point.Y <= outline.MaxY + tolerance;
	}

	private static bool IsLineInsideBounds(Line line, OutlineFeature outline, double tolerance)
	{
		Point3d point = new Point3d((line.StartPoint.X + line.EndPoint.X) / 2.0, (line.StartPoint.Y + line.EndPoint.Y) / 2.0, 0.0);
		return IsPointInsideBounds(line.StartPoint, outline, tolerance) || IsPointInsideBounds(line.EndPoint, outline, tolerance) || IsPointInsideBounds(point, outline, tolerance);
	}

	private static bool IsThreadArc(Arc arc, DimensionRuleConfig config)
	{
		double sweep;
		for (sweep = arc.EndAngle - arc.StartAngle; sweep < 0.0; sweep += Math.PI * 2.0)
		{
		}
		while (sweep > Math.PI * 2.0)
		{
			sweep -= Math.PI * 2.0;
		}
		return ThreadArcRules.IsThreadSweep(sweep, config);
	}
}
