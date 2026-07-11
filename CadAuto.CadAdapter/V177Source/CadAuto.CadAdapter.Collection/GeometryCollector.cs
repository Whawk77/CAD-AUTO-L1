using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter.Model;
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
			MessageForAdding = "\n请选择 DRAWING 图层上的零件外轮廓，可点选或框选: ",
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
			_editor.WriteMessage("\n未找到闭合 Polyline，已改用 DRAWING 图层上的 {0} 个线/圆弧轮廓对象计算外包围盒。", list4.Count);
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
		_editor.WriteMessage("\n外轮廓筛选结果: 已选 {0} 个对象，DRAWING 图层 {1} 个，多段线 {2} 个，闭合多段线 {3} 个，可用线/圆弧轮廓对象 {4} 个。", num, num2, num3, num4, num5);
		throw new InvalidOperationException("选择集中没有 DRAWING 图层上的可用外轮廓对象。");
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
				if (hashSet.Contains(selectedId))
				{
					continue;
				}
				if (arc != null && IsDrawingLayer(arc.Layer) && !IsThreadArc(arc, config) && IsPointInsideBounds(arc.Center, outline, geometryTolerance))
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
		_editor.WriteMessage("\n自动识别销孔数量: {0}", pinHoles.Count);
		PromptEntityOptions promptEntityOptions = new PromptEntityOptions("\n请选择一个销孔作为基准孔: ");
		promptEntityOptions.SetRejectMessage("\n所选对象不是 Circle，请重新选择销孔。");
		promptEntityOptions.AddAllowedClass(typeof(Circle), exactMatch: true);
		PromptEntityResult entity = _editor.GetEntity(promptEntityOptions);
		if (entity.Status != PromptStatus.OK)
		{
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
		return list.OrderByDescending(GetComponentScore).First();
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
		return string.Equals((layerName ?? string.Empty).Trim(), "DRAWING", StringComparison.OrdinalIgnoreCase);
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
		double num;
		for (num = arc.EndAngle - arc.StartAngle; num < 0.0; num += Math.PI * 2.0)
		{
		}
		while (num > Math.PI * 2.0)
		{
			num -= Math.PI * 2.0;
		}
		double num2 = 4.71238898038469;
		return num >= num2 - config.GeometryTolerance;
	}
}
