using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter;
using CadAuto.CadAdapter.Collection;
using CadAuto.CadAdapter.Environment;
using CadAuto.CadAdapter.Mapping;
using CadAuto.CadAdapter.Model;
using CadAuto.CadAdapter.Recognition;
using CadAuto.CadAdapter.Rendering;
using CadAuto.Core.Geometry;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Rules;

namespace AutoFixtureDim;

public static class ThreeViewTrialCommand
{
	private sealed class PreparedView
	{
		public OrthographicViewFrame Frame;

		public ViewExtentOwnership Ownership;

		public OutlineFeature Outline;

		public DatumDefinition Datum;

		public List<HoleFeature> Holes;

		public List<SlotFeature> Slots;
	}

	public static void Execute(Document document)
	{
		if (document == null)
		{
			return;
		}
		Database database = document.Database;
		Editor editor = document.Editor;
		DimensionRuleConfig config = DimensionRuleConfig.CreateDefault();
		config.UseFeatureFirstStructurePipeline = true;
		string pluginDir = Path.GetDirectoryName(typeof(ThreeViewTrialCommand).Assembly.Location);
		if (!string.IsNullOrEmpty(pluginDir))
		{
			config.AnnotationCaseStorePath = Path.Combine(pluginDir, "annotation-cases.json");
		}
		string groupId = DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
		try
		{
			using (Transaction transaction = database.TransactionManager.StartTransaction())
			{
				GeometryCollector collector = new GeometryCollector(editor);
				IList<ClosedViewOutline> outlines = collector.PromptForClosedViewOutlines(transaction);
				if (outlines.Count < 2 || outlines.Count > 3)
				{
					editor.WriteMessage("\nASD3V 需要 2 或 3 个分开的视图外轮廓。当前识别到 {0} 个。", outlines.Count);
					return;
				}
				FeatureRecognizer recognizer = new FeatureRecognizer(config);
				List<PreparedView> prepared = new List<PreparedView>();
				foreach (ClosedViewOutline outline in outlines)
				{
					prepared.Add(RecognizeView(transaction, collector, recognizer, config, outline));
				}
				OrthographicViewSetAssignment assignment = OrthographicViewSetAssigner.Assign(
					prepared.Select(ToFrame).ToList(),
					Math.Max(config.GeometryTolerance, 0.5));
				foreach (PreparedView view in prepared)
				{
					view.Ownership = assignment.Find(view.Frame.Id);
				}
				PreparedView pinView = prepared.FirstOrDefault((PreparedView view) => view.Ownership.KeepPinDimensions && view.Holes.Any((HoleFeature hole) => hole.IsPinHole));
				if (pinView != null)
				{
					List<HoleFeature> pinHoles = pinView.Holes.Where((HoleFeature hole) => hole.IsPinHole).ToList();
					HoleFeature datumHole = collector.PromptForDatumHole(pinHoles, transaction, pinView.Outline);
					if (datumHole != null)
					{
						pinView.Datum.DatumHole = datumHole;
						editor.WriteMessage("\n已设置基准孔: X={0:0.###}, Y={1:0.###}", datumHole.Center.X, datumHole.Center.Y);
						if (!collector.PromptForDatumHoleLocationPoints(config, out var xBase, out var yBase, out var useToleranceX, out var useToleranceY))
						{
							editor.WriteMessage("\n用户取消基准点选择，命令结束。");
							return;
						}
						pinView.Datum.DatumHoleLocationBaseX = xBase;
						pinView.Datum.DatumHoleLocationBaseY = yBase;
						pinView.Datum.DatumHoleLocationUseToleranceX = useToleranceX;
						pinView.Datum.DatumHoleLocationUseToleranceY = useToleranceY;
					}
				}
				AnnotationMetadata.EnsureRegApp(database, transaction);
				string layer = LayerManager.ResolveAnnotationLayer(database, transaction);
				double dimScale = database.Dimscale <= 0.0 ? 1.0 : database.Dimscale;
				ObjectId dimStyleId = DimStyleManager.ResolveDimStyle(database, transaction);
				ObjectId diameterStyleId = DimStyleManager.ResolveDiameterCalloutDimStyle(database, transaction, dimStyleId);
				BlockTableRecord space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
				DimensionDrawer drawer = new DimensionDrawer(database, transaction, space, config, dimStyleId, diameterStyleId, dimScale, layer, groupId);
				int drawn = 0;
				foreach (PreparedView view in prepared)
				{
					drawn += DrawView(drawer, view, config, editor);
				}
				transaction.Commit();
				editor.WriteMessage("\nASD3V 试做完成，线性尺寸 {0} 条。直径、倒角和圆角引线仍用原来的单视图命令。", drawn);
			}
		}
		catch (OperationCanceledException)
		{
			editor.WriteMessage("\nASD3V 已取消。");
		}
		catch (InvalidOperationException ex)
		{
			editor.WriteMessage("\nASD3V 未能分配视图: {0}", ex.Message);
		}
		catch (System.Exception ex)
		{
			editor.WriteMessage("\nASD3V 发生异常: {0}", ex.Message);
		}
	}

	private static OrthographicViewFrame ToFrame(PreparedView view)
	{
		view.Frame.PinHoleCount = view.Holes.Count((HoleFeature hole) => hole.IsPinHole);
		view.Frame.MinX = view.Outline.MinX;
		view.Frame.MinY = view.Outline.MinY;
		view.Frame.MaxX = view.Outline.MaxX;
		view.Frame.MaxY = view.Outline.MaxY;
		return view.Frame;
	}

	private static PreparedView RecognizeView(Transaction transaction, GeometryCollector collector, FeatureRecognizer recognizer, DimensionRuleConfig config, ClosedViewOutline source)
	{
		OutlineSelection selection = new OutlineSelection();
		selection.SelectedIds.AddRange(source.SelectedIds);
		OutlineFeature outline;
		if (!source.PolylineId.IsNull && source.EntityIds.Count == 0)
		{
			Entity entity = (Entity)transaction.GetObject(source.PolylineId, OpenMode.ForRead);
			outline = recognizer.RecognizeOutline(entity, transaction);
			selection.PrimaryPolylineId = source.PolylineId;
		}
		else
		{
			if (!source.PolylineId.IsNull)
			{
				selection.EntityIds.Add(source.PolylineId);
			}
			selection.EntityIds.AddRange(source.EntityIds);
			outline = recognizer.RecognizeOutline(selection.EntityIds, transaction);
		}
		IList<ObjectId> holeSources = collector.CollectHoleSourcesFromOutlineSelection(transaction, outline, selection, config);
		IList<HoleFeature> holes = holeSources.Count == 0
			? new List<HoleFeature>()
			: recognizer.RecognizeHoles(holeSources, transaction, selection.SelectedIds);
		DatumDefinition datum = DatumDefinition.FromOutline(outline);
		foreach (CenterlineEndpointDefinition endpoint in collector.CollectHoleCenterlineEndpoints(transaction, selection, config.GeometryTolerance))
		{
			if (endpoint != null && IsInside(endpoint.Point, outline, config.GeometryTolerance))
			{
				datum.HoleCenterlineEndpoints.Add(endpoint);
			}
		}
		List<HoleFeature> recognizedHoles = holes.Where((HoleFeature hole) => hole != null).ToList();
		return new PreparedView
		{
			Frame = new OrthographicViewFrame
			{
				Id = source.ViewId,
				PinHoleCount = recognizedHoles.Count((HoleFeature hole) => hole.IsPinHole)
			},
			Outline = outline,
			Datum = datum,
			Holes = recognizedHoles,
			Slots = recognizer.RecognizeOutlineSlotFeatures(outline).ToList()
		};
	}

	private static int DrawView(DimensionDrawer drawer, PreparedView view, DimensionRuleConfig config, Editor editor)
	{
		OutlineFeature2D outline = CadToCoreModelMapper.ToCoreOutline(view.Outline);
		Datum2D datum = CadToCoreModelMapper.ToCoreDatum(view.Datum);
		List<HoleFeature2D> holes = CadToCoreModelMapper.ToCoreHoles(view.Holes);
		List<SlotFeature2D> slots = CadToCoreModelMapper.ToCoreSlots(view.Slots);
		DimensionPlan plan = new DimensionPlanner(config).CreateDimensionPlan(outline, datum, holes, slots);
		int removed = OrthographicViewSetAssigner.Apply(plan, view.Ownership);
		drawer.DrawDimensionPlan(plan, plan.Diagnostics, datum);
		drawer.FlushStackedDimensions(view.Outline);
		plan.SynchronizeFinalPlacementSides();
		editor.WriteMessage(
			"\nASD3V {0}: 保留{1}{2}，跨视图收起 {3} 条，留下 {4} 条。",
			OrthographicViewSetAssigner.RoleName(view.Ownership.Role),
			DescribeKeptExtents(view.Ownership),
			view.Ownership.KeepPinDimensions ? "，销孔链留在本视图" : string.Empty,
			removed,
			plan.Dimensions.Count);
		return plan.Dimensions.Count;
	}

	private static string DescribeKeptExtents(ViewExtentOwnership ownership)
	{
		List<string> kept = new List<string>();
		if (ownership.KeepHorizontalOverall)
		{
			kept.Add(OrthographicViewSetAssigner.ExtentName(ownership.HorizontalExtent));
		}
		if (ownership.KeepVerticalOverall)
		{
			kept.Add(OrthographicViewSetAssigner.ExtentName(ownership.VerticalExtent));
		}
		return kept.Count == 0 ? "外形总尺寸交给其他视图" : string.Join("、", kept);
	}

	private static bool IsInside(Point3d point, OutlineFeature outline, double tolerance)
	{
		double pad = Math.Max(Math.Abs(tolerance), 1E-6);
		return point.X >= outline.MinX - pad
			&& point.X <= outline.MaxX + pad
			&& point.Y >= outline.MinY - pad
			&& point.Y <= outline.MaxY + pad;
	}
}
