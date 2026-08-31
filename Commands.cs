using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
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

public sealed class Commands
{
	private enum AutoFixDimOutputScope
	{
		All,
		OutlineOnly,
		HoleOnly,
		CornerOnly
	}

	private sealed class DiagnosticBounds
	{
		public double MinX { get; set; }

		public double MinY { get; set; }

		public double MinZ { get; set; }

		public double MaxX { get; set; }

		public double MaxY { get; set; }

		public double MaxZ { get; set; }
	}

	private sealed class DiagnosticFileIdentity
	{
		public string Path { get; set; }

		public string FileName { get; set; }

		public long SizeBytes { get; set; }

		public string LastWriteTimeUtc { get; set; }

		public string Sha256 { get; set; }

		public string IdentityStatus { get; set; }
	}

	private sealed class DiagnosticRunContext
	{
		public string RunId { get; set; }

		public string Command { get; set; }

		public DiagnosticFileIdentity Drawing { get; set; }

		public DiagnosticFileIdentity Plugin { get; set; }

		public int EntityCount { get; set; }

		public List<string> EntityHandles { get; } = new List<string>();

		public SortedDictionary<string, int> EntityTypeCounts { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);

		public DiagnosticBounds SelectedGeometryBoundsWcs { get; set; }

		public string SelectedGeometryBoundsStatus { get; set; }

		public DiagnosticBounds RecognizedOutlineBoundsWcs { get; set; }

		public double BaseX { get; set; }

		public double BaseY { get; set; }

		public bool HasDatumHole { get; set; }

		public string DatumHoleHandle { get; set; }

		public Point3d? DatumHoleCenterWcs { get; set; }

		public double? DatumHoleLocationBaseX { get; set; }

		public double? DatumHoleLocationBaseY { get; set; }

		public bool DatumHoleLocationUseToleranceX { get; set; }

		public bool DatumHoleLocationUseToleranceY { get; set; }

		public string AutoCadVersion { get; set; }

		public string InsUnits { get; set; }

		public string UcsName { get; set; }

		public Point3d? UcsOriginWcs { get; set; }

		public Point3d? UcsXDirectionWcs { get; set; }

		public Point3d? UcsYDirectionWcs { get; set; }

		public double DimScale { get; set; }

		public string DimStyle { get; set; }

		public string ResolvedDimStyleName { get; set; }

		public double? StyleDimscale { get; set; }

		public double? EffectiveTextHeight { get; set; }

		public double? EffectiveArrowSize { get; set; }
	}

	private static readonly string Ag1RoughnessBlockName = "CadAider_国标粗糙度16下";

	[CommandMethod("ASD")]
	public void Asd()
	{
		RunAutoFixDim(clearExistingBeforeGenerate: false, commandName: "ASD");
	}

	[CommandMethod("TY")]
	public void Ty()
	{
		SupportBlockCommand.Execute(Application.DocumentManager.MdiActiveDocument);
	}

	[CommandMethod("ASDCASE")]
	public void AsdCase()
	{
		Editor editor = Application.DocumentManager.MdiActiveDocument?.Editor;
		if (editor == null)
		{
			return;
		}
		if (AnnotationCaseRuntime.LastFeatures == null || AnnotationCaseRuntime.LastOutline == null)
		{
			editor.WriteMessage("\nASDCASE: run ASD on a drawing first, then ASDCASE to save it as a confirmed case.");
			return;
		}
		string pluginDir = Path.GetDirectoryName(typeof(Commands).Assembly.Location) ?? ".";
		string path = Path.Combine(pluginDir, "annotation-cases.json");
		AnnotationCaseStore store = AnnotationCaseStore.Load(path);
		AnnotationCase captured = AnnotationCaseOverlay.Capture(
			AnnotationCaseRuntime.LastFeatures,
			AnnotationCaseRuntime.LastOutline,
			"case-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture));
		store.Add(captured);
		store.Save(path);
		editor.WriteMessage(
			"\nASDCASE: saved {0} strategies=[{1}] schemas=[{2}] archiveTokens={3} to {4}",
			captured.Id,
			string.Join(",", captured.Strategies ?? new List<string>()),
			string.Join(",", captured.Schemas ?? new List<string>()),
			captured.Decisions.Count,
			path);
	}

	[CommandMethod("AUTOFIXDIM")]
	public void AutoFixDim()
	{
		RunAutoFixDim(clearExistingBeforeGenerate: false, commandName: "AUTOFIXDIM");
	}

	[CommandMethod("ASD4")]
	public void AsdDebug()
	{
		RunAutoFixDim(clearExistingBeforeGenerate: true, diagnosticsEnabled: true, commandName: "ASD4");
	}

	[CommandMethod("ASD5")]
	public void Asd5()
	{
		RunAutoFixDim(clearExistingBeforeGenerate: false, diagnosticsEnabled: false, outputScope: AutoFixDimOutputScope.OutlineOnly, commandName: "ASD5");
	}

	[CommandMethod("ASD6")]
	public void Asd6()
	{
		RunAutoFixDim(clearExistingBeforeGenerate: false, diagnosticsEnabled: false, outputScope: AutoFixDimOutputScope.HoleOnly, commandName: "ASD6");
	}

	[CommandMethod("ASD7")]
	public void Asd7()
	{
		RunAutoFixDim(clearExistingBeforeGenerate: false, diagnosticsEnabled: false, outputScope: AutoFixDimOutputScope.CornerOnly, commandName: "ASD7");
	}

	[CommandMethod("ASDREPRO")]
	public void AsdRepro()
	{
		Editor editor = Application.DocumentManager.MdiActiveDocument?.Editor;
		if (editor == null)
		{
			return;
		}
		RunAutoFixDim(clearExistingBeforeGenerate: true, diagnosticsEnabled: true, outputScope: PromptForReproScope(editor), commandName: "ASDREPRO");
	}

	[CommandMethod("ASDCOREDBG")]
	public void AsdCoreDebug()
	{
		RunCoreDebug();
	}

	[CommandMethod("ASD3")]
	public void Asd3()
	{
		Document mdiActiveDocument = Application.DocumentManager.MdiActiveDocument;
		if (mdiActiveDocument == null)
		{
			return;
		}
		Database database = mdiActiveDocument.Database;
		Editor editor = mdiActiveDocument.Editor;
		try
		{
			using Transaction transaction = database.TransactionManager.StartTransaction();
			int num = AnnotationMetadata.ClearLatestGeneratedAnnotations(database, transaction);
			transaction.Commit();
			editor.WriteMessage("\nAUTOFIXDIM 已清除最近一次插件标注 {0} 个。", num);
		}
		catch (System.Exception ex)
		{
			editor.WriteMessage("\nASD3 发生异常: {0}", ex.Message);
		}
	}

	[CommandMethod("AG1")]
	public void Ag1()
	{
		Document mdiActiveDocument = Application.DocumentManager.MdiActiveDocument;
		if (mdiActiveDocument == null)
		{
			return;
		}
		Database database = mdiActiveDocument.Database;
		Editor editor = mdiActiveDocument.Editor;
		DimensionRuleConfig dimensionRuleConfig = DimensionRuleConfig.CreateDefault();
		string groupId = DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
		try
		{
			using (Transaction transaction = database.TransactionManager.StartTransaction())
			{
				PromptEntityOptions options = new PromptEntityOptions("\n选择需要 AG1 处理的线段: ");
				PromptEntityResult entity = editor.GetEntity(options);
				if (entity.Status != PromptStatus.OK)
				{
					return;
				}
				Entity entity2 = transaction.GetObject(entity.ObjectId, OpenMode.ForRead, openErased: false) as Entity;
				if (!TryGetAg1SourceSegment(transaction, entity2, entity.PickedPoint, out var startPoint, out var endPoint))
				{
					editor.WriteMessage("\nAG1 请选择 Line、二维/三维多段线直线段。");
					return;
				}
				Point3d point3d = Midpoint(startPoint, endPoint);
				bool flag = !IsAg1HorizontalLine(startPoint, endPoint);
				double num = ((entity.PickedPoint.X >= point3d.X) ? 1.0 : (-1.0));
				Vector3d vector3d = (flag ? new Vector3d(num * 2.0, 0.0, 0.0) : new Vector3d(0.0, 2.0, 0.0));
				Line line = new Line(startPoint + vector3d, endPoint + vector3d);
				line.SetDatabaseDefaults(database);
				CopyEntityDisplayProperties(entity2, line);
				line.LayerId = database.Clayer;
				line.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
				AnnotationMetadata.EnsureRegApp(database, transaction);
				BlockTableRecord blockTableRecord = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
				blockTableRecord.AppendEntity(line);
				transaction.AddNewlyCreatedDBObject(line, add: true);
				AnnotationMetadata.Mark(line, groupId, AnnotationMetadata.KindAg1);
				double num2 = ((database.Dimscale <= 0.0) ? 1.0 : database.Dimscale);
				double num3 = dimensionRuleConfig.LeaderOffset * num2;
				ObjectId dimStyleId = DimStyleManager.ResolveDimStyle(database, transaction);
				double ag1DimStyleTextHeight = GetAg1DimStyleTextHeight(database, transaction, dimStyleId);
				Point3d point3d2;
				Point3d point3d3;
				Point3d textPoint;
				Point3d textPoint2;
				if (flag)
				{
					point3d2 = point3d + new Vector3d(0.0, 3.0, 0.0);
					point3d3 = Midpoint(line.StartPoint, line.EndPoint) + new Vector3d(0.0, -3.0, 0.0);
					textPoint = point3d2 + new Vector3d(num * num3, num3, 0.0);
					textPoint2 = point3d3 + new Vector3d(num * num3, 0.0 - num3, 0.0);
				}
				else
				{
					point3d2 = point3d + new Vector3d(-3.0, 0.0, 0.0);
					point3d3 = Midpoint(line.StartPoint, line.EndPoint) + new Vector3d(3.0, 0.0, 0.0);
					textPoint = point3d2 + new Vector3d(0.0 - num3, num3, 0.0);
					textPoint2 = point3d3 + new Vector3d(num3, num3, 0.0);
				}
				Point3d ag1TextLandingMidpoint = GetAg1TextLandingMidpoint(textPoint, point3d2, "CNC加工", ag1DimStyleTextHeight);
				AddAg1Leader(database, transaction, blockTableRecord, point3d2, textPoint, "CNC加工", dimStyleId, groupId);
				AddAg1Leader(database, transaction, blockTableRecord, point3d3, textPoint2, "淬火", dimStyleId, groupId);
				InsertAg1RoughnessBlock(database, transaction, blockTableRecord, ag1TextLandingMidpoint, entity2.Layer, dimStyleId, groupId);
				transaction.Commit();
			}
			editor.WriteMessage("\nAG1 已生成洋红偏移线、CNC加工/淬火引出标注和粗糙度块。");
		}
		catch (Autodesk.AutoCAD.Runtime.Exception ex)
		{
			editor.WriteMessage("\nAG1 取消或失败: {0}", ex.Message);
		}
		catch (System.Exception ex2)
		{
			editor.WriteMessage("\nAG1 发生异常: {0}", ex2.Message);
		}
	}

	internal static void CopyEntityDisplayProperties(Entity source, Line target)
	{
		target.Layer = source.Layer;
		target.LinetypeId = source.LinetypeId;
		target.LinetypeScale = source.LinetypeScale;
		target.LineWeight = source.LineWeight;
		target.Transparency = source.Transparency;
		Line line = source as Line;
		if (line != null)
		{
			target.Normal = line.Normal;
			target.Thickness = line.Thickness;
		}
	}

	internal static bool TryGetAg1SourceSegment(Transaction tr, Entity entity, Point3d pickedPoint, out Point3d startPoint, out Point3d endPoint)
	{
		startPoint = Point3d.Origin;
		endPoint = Point3d.Origin;
		Line line = entity as Line;
		if (line != null)
		{
			startPoint = line.StartPoint;
			endPoint = line.EndPoint;
			return true;
		}
		Polyline polyline = entity as Polyline;
		if (polyline != null)
		{
			return TryGetAg1PolylineSegment(polyline, pickedPoint, out startPoint, out endPoint);
		}
		Polyline2d polyline2d = entity as Polyline2d;
		if (polyline2d != null)
		{
			return TryGetAg1Polyline2dSegment(tr, polyline2d, pickedPoint, out startPoint, out endPoint);
		}
		Polyline3d polyline3d = entity as Polyline3d;
		if (polyline3d != null)
		{
			return TryGetAg1Polyline3dSegment(tr, polyline3d, pickedPoint, out startPoint, out endPoint);
		}
		return false;
	}

	private static bool TryGetAg1PolylineSegment(Polyline polyline, Point3d pickedPoint, out Point3d startPoint, out Point3d endPoint)
	{
		startPoint = Point3d.Origin;
		endPoint = Point3d.Origin;
		if (polyline == null || polyline.NumberOfVertices < 2)
		{
			return false;
		}
		int num = (polyline.Closed ? polyline.NumberOfVertices : (polyline.NumberOfVertices - 1));
		double num2 = double.MaxValue;
		bool result = false;
		for (int i = 0; i < num; i++)
		{
			if (polyline.GetSegmentType(i) == SegmentType.Line)
			{
				Point3d point3dAt = polyline.GetPoint3dAt(i);
				Point3d point3dAt2 = polyline.GetPoint3dAt((i + 1) % polyline.NumberOfVertices);
				double num3 = DistancePointToSegment2D(pickedPoint, point3dAt, point3dAt2);
				if (!(num3 >= num2))
				{
					num2 = num3;
					startPoint = point3dAt;
					endPoint = point3dAt2;
					result = true;
				}
			}
		}
		return result;
	}

	private static bool TryGetAg1Polyline2dSegment(Transaction tr, Polyline2d polyline, Point3d pickedPoint, out Point3d startPoint, out Point3d endPoint)
	{
		List<Point3d> list = new List<Point3d>();
		foreach (ObjectId item in polyline)
		{
			Vertex2d vertex2d = tr.GetObject(item, OpenMode.ForRead, openErased: false) as Vertex2d;
			if (vertex2d != null)
			{
				list.Add(vertex2d.Position);
			}
		}
		return TryGetNearestAg1Segment(list, polyline.Closed, pickedPoint, out startPoint, out endPoint);
	}

	private static bool TryGetAg1Polyline3dSegment(Transaction tr, Polyline3d polyline, Point3d pickedPoint, out Point3d startPoint, out Point3d endPoint)
	{
		List<Point3d> list = new List<Point3d>();
		foreach (ObjectId item in polyline)
		{
			PolylineVertex3d polylineVertex3d = tr.GetObject(item, OpenMode.ForRead, openErased: false) as PolylineVertex3d;
			if (polylineVertex3d != null)
			{
				list.Add(polylineVertex3d.Position);
			}
		}
		return TryGetNearestAg1Segment(list, polyline.Closed, pickedPoint, out startPoint, out endPoint);
	}

	private static bool TryGetNearestAg1Segment(IList<Point3d> points, bool closed, Point3d pickedPoint, out Point3d startPoint, out Point3d endPoint)
	{
		startPoint = Point3d.Origin;
		endPoint = Point3d.Origin;
		if (points == null || points.Count < 2)
		{
			return false;
		}
		int num = (closed ? points.Count : (points.Count - 1));
		double num2 = double.MaxValue;
		bool result = false;
		for (int i = 0; i < num; i++)
		{
			Point3d point3d = points[i];
			Point3d point3d2 = points[(i + 1) % points.Count];
			if (!(DistanceSquared2D(point3d, point3d2) <= 1E-12))
			{
				double num3 = DistancePointToSegment2D(pickedPoint, point3d, point3d2);
				if (!(num3 >= num2))
				{
					num2 = num3;
					startPoint = point3d;
					endPoint = point3d2;
					result = true;
				}
			}
		}
		return result;
	}

	private static double DistanceSquared2D(Point3d first, Point3d second)
	{
		double num = second.X - first.X;
		double num2 = second.Y - first.Y;
		return num * num + num2 * num2;
	}

	private static double DistancePointToSegment2D(Point3d point, Point3d startPoint, Point3d endPoint)
	{
		double num = endPoint.X - startPoint.X;
		double num2 = endPoint.Y - startPoint.Y;
		double num3 = num * num + num2 * num2;
		if (num3 <= 1E-12)
		{
			double num4 = point.X - startPoint.X;
			double num5 = point.Y - startPoint.Y;
			return Math.Sqrt(num4 * num4 + num5 * num5);
		}
		double val = ((point.X - startPoint.X) * num + (point.Y - startPoint.Y) * num2) / num3;
		val = Math.Max(0.0, Math.Min(1.0, val));
		double num6 = startPoint.X + val * num;
		double num7 = startPoint.Y + val * num2;
		double num8 = point.X - num6;
		double num9 = point.Y - num7;
		return Math.Sqrt(num8 * num8 + num9 * num9);
	}

	private static Point3d Midpoint(Point3d first, Point3d second)
	{
		return new Point3d((first.X + second.X) * 0.5, (first.Y + second.Y) * 0.5, (first.Z + second.Z) * 0.5);
	}

	private static bool IsAg1HorizontalLine(Point3d startPoint, Point3d endPoint)
	{
		Vector3d vector3d = endPoint - startPoint;
		double num = Math.Sqrt(vector3d.X * vector3d.X + vector3d.Y * vector3d.Y);
		double num2 = Math.Max(1E-06, num * 0.0001);
		return Math.Abs(vector3d.Y) <= num2 && Math.Abs(vector3d.X) > num2;
	}

	internal static Point3d GetAg1TextLandingMidpoint(Point3d textPoint, Point3d arrowPoint, string text, double textHeight)
	{
		double num = EstimateAg1TextWidth(text, textHeight);
		double num2 = ((textPoint.X < arrowPoint.X) ? (-1.0) : 1.0);
		return textPoint + new Vector3d(num2 * num * 0.5, (0.0 - textHeight) * 0.5, 0.0);
	}

	private static double EstimateAg1TextWidth(string text, double textHeight)
	{
		if (string.IsNullOrEmpty(text))
		{
			return textHeight;
		}
		double num = 0.0;
		foreach (char c in text)
		{
			num += ((c <= '\u007f') ? 0.7 : 1.0);
		}
		return Math.Max(textHeight, num * textHeight);
	}

	private static void AddAg1Leader(Database db, Transaction tr, BlockTableRecord space, Point3d arrowPoint, Point3d textPoint, string text, ObjectId dimStyleId, string groupId)
	{
		MText mText = new MText();
		mText.SetDatabaseDefaults(db);
		mText.Contents = text ?? string.Empty;
		mText.Location = textPoint;
		mText.TextHeight = GetAg1DimStyleTextHeight(db, tr, dimStyleId);
		mText.TextStyleId = GetAg1DimStyleTextStyle(db, tr, dimStyleId);
		mText.Attachment = ((textPoint.X < arrowPoint.X) ? AttachmentPoint.MiddleRight : AttachmentPoint.MiddleLeft);
		space.AppendEntity(mText);
		tr.AddNewlyCreatedDBObject(mText, add: true);
		AnnotationMetadata.Mark(mText, groupId, AnnotationMetadata.KindAg1);
		Leader leader = new Leader();
		leader.SetDatabaseDefaults(db);
		leader.DimensionStyle = dimStyleId;
		leader.AppendVertex(arrowPoint);
		leader.AppendVertex(textPoint);
		space.AppendEntity(leader);
		tr.AddNewlyCreatedDBObject(leader, add: true);
		leader.Annotation = mText.ObjectId;
		leader.EvaluateLeader();
		AnnotationMetadata.Mark(leader, groupId, AnnotationMetadata.KindAg1);
	}

	private static void InsertAg1RoughnessBlock(Database db, Transaction tr, BlockTableRecord space, Point3d insertPoint, string layerName, ObjectId dimStyleId, string groupId)
	{
		BlockTable blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
		if (blockTable.Has(Ag1RoughnessBlockName))
		{
			BlockReference blockReference = new BlockReference(insertPoint, blockTable[Ag1RoughnessBlockName]);
			blockReference.SetDatabaseDefaults(db);
			blockReference.Layer = (string.IsNullOrEmpty(layerName) ? blockReference.Layer : layerName);
			double ag1DimStyleGlobalScale = GetAg1DimStyleGlobalScale(db, tr, dimStyleId);
			blockReference.ScaleFactors = new Scale3d(ag1DimStyleGlobalScale);
			blockReference.Rotation = Math.PI;
			space.AppendEntity(blockReference);
			tr.AddNewlyCreatedDBObject(blockReference, add: true);
			AnnotationMetadata.Mark(blockReference, groupId, AnnotationMetadata.KindAg1);
		}
	}

	private static double GetAg1DimStyleGlobalScale(Database db, Transaction tr, ObjectId dimStyleId)
	{
		DimStyleTableRecord dimStyleTableRecord = tr.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
		if (dimStyleTableRecord != null && dimStyleTableRecord.Dimscale > 1E-09)
		{
			return dimStyleTableRecord.Dimscale;
		}
		return (db.Dimscale <= 0.0) ? 1.0 : db.Dimscale;
	}

	private static double GetAg1DimStyleTextHeight(Database db, Transaction tr, ObjectId dimStyleId)
	{
		DimStyleTableRecord dimStyleTableRecord = tr.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
		double num = ((db.Dimscale <= 0.0) ? 1.0 : db.Dimscale);
		if (dimStyleTableRecord != null && dimStyleTableRecord.Dimtxt > 1E-09)
		{
			return dimStyleTableRecord.Dimtxt * num;
		}
		return 2.5 * num;
	}

	private static ObjectId GetAg1DimStyleTextStyle(Database db, Transaction tr, ObjectId dimStyleId)
	{
		DimStyleTableRecord dimStyleTableRecord = tr.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
		if (dimStyleTableRecord != null && !dimStyleTableRecord.Dimtxsty.IsNull)
		{
			return dimStyleTableRecord.Dimtxsty;
		}
		return db.Textstyle;
	}

	private void RunCoreDebug()
	{
		Document mdiActiveDocument = Application.DocumentManager.MdiActiveDocument;
		if (mdiActiveDocument == null)
		{
			return;
		}
		Database database = mdiActiveDocument.Database;
		Editor editor = mdiActiveDocument.Editor;
		DimensionRuleConfig dimensionRuleConfig = DimensionRuleConfig.CreateDefault();
		try
		{
			using Transaction transaction = database.TransactionManager.StartTransaction();
			ObjectId objectId = DimStyleManager.ResolveDimStyle(database, transaction);
			GeometryCollector geometryCollector = new GeometryCollector(editor);
			OutlineSelection outlineSelection = geometryCollector.PromptForOutlineSelection(transaction);
			FeatureRecognizer featureRecognizer = new FeatureRecognizer(dimensionRuleConfig);
			OutlineFeature outlineFeature;
			if (outlineSelection.HasPrimaryPolyline)
			{
				Entity entity = (Entity)transaction.GetObject(outlineSelection.PrimaryPolylineId, OpenMode.ForRead);
				outlineFeature = featureRecognizer.RecognizeOutline(entity, transaction);
			}
			else
			{
				outlineFeature = featureRecognizer.RecognizeOutline(outlineSelection.EntityIds, transaction);
				ReportEnvelopeSkippedEntities(editor, featureRecognizer);
			}
			DatumDefinition datumDefinition = DatumDefinition.FromOutline(outlineFeature);
			IList<SlotFeature> list = featureRecognizer.RecognizeOutlineSlotFeatures(outlineFeature);
			IList<ObjectId> list2 = geometryCollector.CollectHoleSourcesFromOutlineSelection(transaction, outlineFeature, outlineSelection, dimensionRuleConfig);
			bool flag = false;
			if (list2.Count == 0 && list.Count > 0)
			{
				list2 = new List<ObjectId>();
				flag = true;
			}
			if (list2.Count == 0 && !flag)
			{
				editor.WriteMessage("\nASDCOREDBG: no holes found in selection; select Circle holes manually or press Enter to skip holes.");
				list2 = geometryCollector.PromptForCircleHoles();
				if (list2 == null || list2.Count == 0)
				{
					list2 = new List<ObjectId>();
					flag = true;
				}
			}
			IList<HoleFeature> list3;
			if (!flag)
			{
				list3 = featureRecognizer.RecognizeHoles(list2, transaction, outlineSelection.SelectedIds);
			}
			else
			{
				IList<HoleFeature> list4 = new List<HoleFeature>();
				list3 = list4;
			}
			IList<HoleFeature> list5 = list3;
			if (!flag && list5.Count == 0)
			{
				flag = true;
			}
			if (!flag)
			{
				List<HoleFeature> list6 = list5.Where((HoleFeature h) => h.IsPinHole).ToList();
				if (list6.Count > 0)
				{
					HoleFeature holeFeature = geometryCollector.PromptForDatumHole(list6, transaction, outlineFeature);
					if (holeFeature != null)
					{
						datumDefinition.DatumHole = holeFeature;
						if (!geometryCollector.PromptForDatumHoleLocationPoints(dimensionRuleConfig, out var xBase, out var yBase, out var useToleranceX, out var useToleranceY))
						{
							editor.WriteMessage("\nASDCOREDBG: datum-hole base selection cancelled.");
							return;
						}
						datumDefinition.DatumHoleLocationBaseX = xBase;
						datumDefinition.DatumHoleLocationBaseY = yBase;
						datumDefinition.DatumHoleLocationUseToleranceX = useToleranceX;
						datumDefinition.DatumHoleLocationUseToleranceY = useToleranceY;
					}
				}
			}
			DimensionRuleConfig config = dimensionRuleConfig;
			OutlineFeature2D outline = CadToCoreModelMapper.ToCoreOutline(outlineFeature);
			Datum2D datum2D = CadToCoreModelMapper.ToCoreDatum(datumDefinition);
			List<HoleFeature2D> holes = CadToCoreModelMapper.ToCoreHoles(list5);
			List<SlotFeature2D> list7 = CadToCoreModelMapper.ToCoreSlots(featureRecognizer.LastRecognizedSlots);
			DimensionPlanner dimensionPlanner = new DimensionPlanner(config);
			DimensionPlan dimensionPlan = dimensionPlanner.CreateDimensionPlan(outline, datum2D, holes, list7);
			IList<HoleCalloutPlan> holeCalloutPlans = new HoleCalloutPlanner(config).CreatePlans(holes, datum2D.DatumHole);
			DimensionPlan dimensionPlan2 = CreateCoreDebugRenderPlan(dimensionPlan);
			string text = EnsureCoreDebugLayer(database, transaction);
			BlockTableRecord space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
			DimensionDrawer dimensionDrawer = new DimensionDrawer(database, transaction, space, config, objectId, DimStyleManager.ResolveDiameterCalloutDimStyle(database, transaction, objectId), (database.Dimscale <= 0.0) ? 1.0 : database.Dimscale, text, DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture), annotationKind: AnnotationMetadata.KindCoreDebug);
			dimensionDrawer.DrawDimensionPlan(dimensionPlan2, dimensionPlan.Diagnostics);
			dimensionDrawer.FlushStackedDimensions(outlineFeature);
			dimensionPlan.SynchronizeFinalPlacementSides();
			WriteCoreDebugSummary(editor, outlineFeature, list5, list7.Count, dimensionPlan, dimensionPlan2, holeCalloutPlans, text);
			transaction.Commit();
		}
		catch (Autodesk.AutoCAD.Runtime.Exception ex)
		{
			editor.WriteMessage("\nASDCOREDBG cancelled or failed: {0}", ex.Message);
		}
		catch (System.Exception ex2)
		{
			editor.WriteMessage("\nASDCOREDBG failed: {0}", ex2.Message);
		}
	}

	private void RunAutoFixDim(bool clearExistingBeforeGenerate, bool diagnosticsEnabled = false, AutoFixDimOutputScope outputScope = AutoFixDimOutputScope.All, string commandName = "ASD")
	{
		Document mdiActiveDocument = Application.DocumentManager.MdiActiveDocument;
		if (mdiActiveDocument == null)
		{
			return;
		}
		Database database = mdiActiveDocument.Database;
		Editor editor = mdiActiveDocument.Editor;
		DimensionRuleConfig config = DimensionRuleConfig.CreateDefault();
		// grill-me: CreateDefault stays false. CAD opens FeatureFirst after
		// RotationSignature_F338CadGoldenMultisetFourWayEqual is green (166/166).
		config.UseFeatureFirstStructurePipeline = true;
		string pluginDir = Path.GetDirectoryName(typeof(Commands).Assembly.Location);
		if (!string.IsNullOrEmpty(pluginDir))
		{
			config.AnnotationCaseStorePath = Path.Combine(pluginDir, "annotation-cases.json");
		}
		string groupId = DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
		DiagnosticDimensionSide diagnosticSide = (diagnosticsEnabled ? PromptForDiagnosticSide(editor) : DiagnosticDimensionSide.All);
		bool flag = outputScope != AutoFixDimOutputScope.CornerOnly;
		bool flag2 = outputScope == AutoFixDimOutputScope.All || outputScope == AutoFixDimOutputScope.HoleOnly;
		bool flag3 = outputScope == AutoFixDimOutputScope.All || outputScope == AutoFixDimOutputScope.CornerOnly;
		bool flag4 = outputScope == AutoFixDimOutputScope.All || outputScope == AutoFixDimOutputScope.HoleOnly;
		bool flag5 = flag4;
		IList<HoleCalloutPlan> list = null;
		IList<HoleFeature> sourceHoles = null;
		OutlineFeature outline = null;
		IList<SlotFeature> slotFeatures = null;
		ObjectId dimStyleId = ObjectId.Null;
		ObjectId objectId = ObjectId.Null;
		string annotationLayer = string.Empty;
		double dimScale = 1.0;
		DimensionPlan completedLinearPlan = null;
		IList<HoleFeature> recognizedHoles = null;
		DiagnosticRunContext diagnosticContext = null;
		try
		{
			using (Transaction transaction = database.TransactionManager.StartTransaction())
			{
				string text = LayerManager.ResolveAnnotationLayer(database, transaction);
				double num = ((database.Dimscale <= 0.0) ? 1.0 : database.Dimscale);
				ObjectId objectId2 = DimStyleManager.ResolveDimStyle(database, transaction);
				ObjectId objectId3 = DimStyleManager.ResolveDiameterCalloutDimStyle(database, transaction, objectId2);
				GeometryCollector geometryCollector = new GeometryCollector(editor);
				OutlineSelection outlineSelection = geometryCollector.PromptForOutlineSelection(transaction);
				FeatureRecognizer featureRecognizer = new FeatureRecognizer(config);
				OutlineFeature outlineFeature;
				if (outlineSelection.HasPrimaryPolyline)
				{
					Entity entity = (Entity)transaction.GetObject(outlineSelection.PrimaryPolylineId, OpenMode.ForRead);
					outlineFeature = featureRecognizer.RecognizeOutline(entity, transaction);
				}
				else
				{
					outlineFeature = featureRecognizer.RecognizeOutline(outlineSelection.EntityIds, transaction);
					ReportEnvelopeSkippedEntities(editor, featureRecognizer);
				}
				DatumDefinition datumDefinition = DatumDefinition.FromOutline(outlineFeature);
				IList<SlotFeature> list3;
				if (!flag4)
				{
					IList<SlotFeature> list2 = new List<SlotFeature>();
					list3 = list2;
				}
				else
				{
					list3 = featureRecognizer.RecognizeOutlineSlotFeatures(outlineFeature);
				}
				IList<SlotFeature> list4 = list3;
				IList<ObjectId> list5 = new List<ObjectId>();
				bool flag6 = !flag2;
				if (flag2)
				{
					list5 = geometryCollector.CollectHoleSourcesFromOutlineSelection(transaction, outlineFeature, outlineSelection, config);
					if (list5.Count == 0 && list4.Count > 0)
					{
						list5 = new List<ObjectId>();
						flag6 = true;
					}
					if (list5.Count == 0 && !flag6)
					{
						editor.WriteMessage("\n本次框选对象中未识别到孔，请手动选择需要标注的圆孔。");
						list5 = geometryCollector.PromptForCircleHoles();
						if (list5 == null || list5.Count == 0)
						{
							editor.WriteMessage("\n未选择孔，已跳过孔标注，继续外轮廓标注。");
							list5 = new List<ObjectId>();
							flag6 = true;
						}
					}
				}
				IList<HoleFeature> list6;
				if (!flag6)
				{
					list6 = featureRecognizer.RecognizeHoles(list5, transaction, outlineSelection.SelectedIds);
				}
				else
				{
					IList<HoleFeature> list7 = new List<HoleFeature>();
					list6 = list7;
				}
				IList<HoleFeature> list8 = list6;
				if (!flag6 && list8.Count == 0)
				{
					editor.WriteMessage("\n未识别到有效圆孔，已跳过孔标注，继续外轮廓标注。");
					flag6 = true;
				}
				if (!flag6)
				{
					List<HoleFeature> list9 = list8.Where((HoleFeature h) => h.IsPinHole).ToList();
					if (list9.Count > 0)
					{
						HoleFeature holeFeature = geometryCollector.PromptForDatumHole(list9, transaction, outlineFeature);
						if (holeFeature != null)
						{
							datumDefinition.DatumHole = holeFeature;
							editor.WriteMessage("\n已设置基准孔: X={0:0.###}, Y={1:0.###}", holeFeature.Center.X, holeFeature.Center.Y);
							if (!geometryCollector.PromptForDatumHoleLocationPoints(config, out var xBase, out var yBase, out var useToleranceX, out var useToleranceY))
							{
								editor.WriteMessage("\n用户取消基准点选择，命令结束。");
								return;
							}
							datumDefinition.DatumHoleLocationBaseX = xBase;
							datumDefinition.DatumHoleLocationBaseY = yBase;
							datumDefinition.DatumHoleLocationUseToleranceX = useToleranceX;
							datumDefinition.DatumHoleLocationUseToleranceY = useToleranceY;
							editor.WriteMessage("\n基准孔定位基准: X基准={0:0.###}, Y基准={1:0.###}", xBase, yBase);
						}
					}
				}
				diagnosticContext = CreateDiagnosticRunContext(database, transaction, outlineSelection, list5, outlineFeature, datumDefinition, groupId, commandName);
				if (clearExistingBeforeGenerate)
				{
					AnnotationMetadata.EnsureRegApp(database, transaction);
					int num2 = AnnotationMetadata.ClearGeneratedAnnotations(database, transaction);
					editor.WriteMessage("\n已清除旧插件标注 {0} 个。", num2);
				}
				List<SlotFeature> list10 = (flag4 ? featureRecognizer.LastRecognizedSlots.Concat(list4).ToList() : new List<SlotFeature>());
				IList<HoleCalloutPlan> list12;
				if (!flag2)
				{
					IList<HoleCalloutPlan> list11 = new List<HoleCalloutPlan>();
					list12 = list11;
				}
				else
				{
					list12 = BuildHoleCalloutPlansForPlacement(list8, datumDefinition.DatumHole, config);
				}
				IList<HoleCalloutPlan> list13 = list12;
				BlockTableRecord space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
				DimensionDrawer dimensionDrawer = new DimensionDrawer(database, transaction, space, config, objectId2, objectId3, num, text, groupId, diagnosticsEnabled, diagnosticSide);
				if (flag)
				{
					DimensionPlan dimensionPlan = DrawLinearDimensions(dimensionDrawer, outlineFeature, datumDefinition, list8, list10, flag6, config, outputScope);
					completedLinearPlan = dimensionPlan;
					recognizedHoles = list8;
				}
				outline = outlineFeature;
				slotFeatures = list10;
				dimStyleId = objectId2;
				objectId = objectId3;
				annotationLayer = text;
				dimScale = num;
				if (EnableInlineInteractiveCallouts() && (flag3 || flag5))
				{
					if (flag3)
					{
						try
						{
							DrawCornerFeatureLeadersWithPreview(editor, dimensionDrawer, outlineFeature);
						}
						catch (System.Exception ex)
						{
							editor.WriteMessage("\n倒角/圆角标注已跳过: {0}", ex.Message);
						}
					}
					if (flag5 && list10.Count > 0)
					{
						try
						{
							dimensionDrawer.DrawSlotRadiusLeadersWithJig(editor, list10);
						}
						catch (System.Exception ex2)
						{
							editor.WriteMessage("\nU slot radius callouts skipped: {0}", ex2.Message);
						}
					}
				}
				if (!diagnosticsEnabled && flag2 && !flag6)
				{
					list = list13;
					sourceHoles = list8;
					objectId = objectId3;
					annotationLayer = text;
				}
				transaction.Commit();
			}
			WriteDimensionRunSummary(editor, completedLinearPlan, outline, recognizedHoles, slotFeatures, outputScope, diagnosticsEnabled, diagnosticSide, diagnosticContext);
			if (!diagnosticsEnabled && (flag3 || flag5))
			{
				DrawPostLinearInteractiveAnnotations(mdiActiveDocument, config, dimStyleId, objectId, dimScale, annotationLayer, groupId, outline, slotFeatures, flag3, flag5);
			}
			if (list != null)
			{
				NativeDiameterDimensioner.PromptHoleCalloutPlans(mdiActiveDocument, list, sourceHoles, config, annotationLayer, objectId, groupId);
			}
			editor.WriteMessage("\nAUTOFIXDIM 标注完成。");
		}
		catch (Autodesk.AutoCAD.Runtime.Exception ex3)
		{
			editor.WriteMessage("\nAUTOFIXDIM 取消或失败: {0}: {1}", ex3.GetType().Name, ex3.Message);
			WriteDimensionRunSummary(editor, completedLinearPlan, outline, recognizedHoles, slotFeatures, outputScope, diagnosticsEnabled, diagnosticSide, diagnosticContext, ex3);
		}
		catch (System.Exception ex4)
		{
			editor.WriteMessage("\nAUTOFIXDIM 发生异常: {0}: {1}", ex4.GetType().Name, ex4.Message);
			WriteDimensionRunSummary(editor, completedLinearPlan, outline, recognizedHoles, slotFeatures, outputScope, diagnosticsEnabled, diagnosticSide, diagnosticContext, ex4);
		}
	}

	private static void ReportEnvelopeSkippedEntities(Editor editor, FeatureRecognizer recognizer)
	{
		if (editor == null || recognizer == null || recognizer.LastEnvelopeSkippedEntityIds.Count == 0)
		{
			return;
		}
		editor.WriteMessage("\n已排除 {0} 个不含线/弧几何的对象（例如独立圆），它们不参与外形包围盒计算。", recognizer.LastEnvelopeSkippedEntityIds.Count);
	}

	private static void WriteDimensionRunSummary(Editor editor, DimensionPlan plan, OutlineFeature outline, IEnumerable<HoleFeature> holes, IEnumerable<SlotFeature> slots, AutoFixDimOutputScope outputScope, bool diagnosticsEnabled, DiagnosticDimensionSide diagnosticSide, DiagnosticRunContext context, System.Exception error = null)
	{
		if (editor == null)
		{
			return;
		}
		DimensionDiagnosticReport dimensionDiagnosticReport = plan?.Diagnostics ?? new DimensionDiagnosticReport();
		if (plan != null)
		{
			List<DimensionCandidateDiagnostic> skipped = dimensionDiagnosticReport.DimensionCandidates.Where((DimensionCandidateDiagnostic item) => string.Equals(item.DecisionStatus, "Skipped", StringComparison.Ordinal)).ToList();
			editor.WriteMessage("\nAUTOFIXDIM 线性尺寸汇总: 已生成 {0}，已跳过 {1}，失败 0。", dimensionDiagnosticReport.FinalDimensions.Count, skipped.Count);
			foreach (DimensionCandidateDiagnostic item in skipped.Take(10))
			{
				editor.WriteMessage("\n  已跳过 {0}/{1}: {2}", item.Kind, item.DebugRole, item.DecisionReason);
			}
			if (skipped.Count > 10)
			{
				editor.WriteMessage("\n  另有 {0} 条跳过记录，请查看诊断 JSON。", skipped.Count - 10);
			}
		}
		try
		{
			string path = WriteDimensionDiagnosticReport(dimensionDiagnosticReport, outline, holes, slots, outputScope, diagnosticsEnabled, diagnosticSide, context, plan?.CoordinateFrame, error);
			editor.WriteMessage("\n诊断报告已输出: {0}", path);
		}
		catch (System.Exception ex)
		{
			editor.WriteMessage("\n诊断报告写入失败，标注结果已保留: {0}", ex.Message);
		}
	}

	private static string EnsureCoreDebugLayer(Database db, Transaction tr)
	{
		LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
		if (!layerTable.Has("AUTOFIXDIM_COREDBG"))
		{
			layerTable.UpgradeOpen();
			LayerTableRecord layerTableRecord = new LayerTableRecord
			{
				Name = "AUTOFIXDIM_COREDBG"
			};
			layerTable.Add(layerTableRecord);
			tr.AddNewlyCreatedDBObject(layerTableRecord, add: true);
		}
		return "AUTOFIXDIM_COREDBG";
	}

	private static void WriteCoreDebugSummary(Editor editor, OutlineFeature outline, IList<HoleFeature> holes, int slotCount, DimensionPlan plan, DimensionPlan renderPlan, IList<HoleCalloutPlan> holeCalloutPlans, string debugLayer)
	{
		editor.WriteMessage("\nASDCOREDBG: outline W={0:0.###}, H={1:0.###}; holes={2}; slots={3}; core dims={4}; rendered={5}; skipped={6}; pin groups={7}; layer={8}", outline.Width, outline.Height, holes?.Count ?? 0, slotCount, plan.Dimensions.Count, renderPlan.Dimensions.Count, plan.Dimensions.Count - renderPlan.Dimensions.Count, plan.PinGroups.Count, debugLayer);
		foreach (IGrouping<DimensionKind, PlannedDimension> item in from d in plan.Dimensions
			group d by d.Kind into g
			orderby g.Key.ToString()
			select g)
		{
			editor.WriteMessage("\n  {0}: {1}", item.Key, item.Count());
		}
		if (holeCalloutPlans != null && holeCalloutPlans.Count > 0)
		{
			int num = 1;
			foreach (HoleCalloutPlan holeCalloutPlan in holeCalloutPlans)
			{
				editor.WriteMessage("\n  callout#{0:00} kind={1} holes={2} anchor=({3:0.###},{4:0.###}) owner={5} text='{6}'", num, holeCalloutPlan.Kind, holeCalloutPlan.Holes.Count, holeCalloutPlan.AnchorPoint.X, holeCalloutPlan.AnchorPoint.Y, holeCalloutPlan.DebugOwner ?? string.Empty, holeCalloutPlan.Text ?? string.Empty);
				num++;
			}
		}
		if (holes != null && holes.Count > 0)
		{
			int num2 = 1;
			foreach (HoleFeature item2 in from h in holes
				orderby h.Center.X, h.Center.Y
				select h)
			{
				editor.WriteMessage("\n  hole#{0:00} kind={1} dia={2:0.###} center=({3:0.###},{4:0.###}) fit='{5}' thread='{6}'", num2, item2.HoleKind, item2.Diameter, item2.Center.X, item2.Center.Y, item2.FitTolerance ?? string.Empty, item2.ThreadCallout ?? string.Empty);
				num2++;
			}
		}
		foreach (PinGroupPlan item3 in plan.PinGroups.OrderBy((PinGroupPlan g) => g.GroupIndex))
		{
			editor.WriteMessage("\n  pinGroup PG{0}: base=({1:0.###},{2:0.###}) pins={3} members={4} hSide={5} vSide={6}", item3.GroupIndex, (item3.BasePin == null) ? 0.0 : item3.BasePin.Center.X, (item3.BasePin == null) ? 0.0 : item3.BasePin.Center.Y, item3.Pins.Count, item3.MemberHoles.Count, item3.HorizontalSide, item3.VerticalSide);
			foreach (HoleFeature2D item4 in from h in item3.MemberHoles
				orderby h.Center.X, h.Center.Y
				select h)
			{
				editor.WriteMessage("\n    member kind={0} dia={1:0.###} center=({2:0.###},{3:0.###})", item4.Kind, item4.Diameter, item4.Center.X, item4.Center.Y);
			}
		}
		int num3 = 1;
		foreach (PlannedDimension item5 in from d in plan.Dimensions
			orderby d.Side, d.Kind, d.DebugRole ?? string.Empty
			select d)
		{
			editor.WriteMessage("\n  #{0:00} {1}/{2}/{3} span={4:0.###} from=({5:0.###},{6:0.###}) to=({7:0.###},{8:0.###}) role={9} owner={10} text='{11}'", num3, item5.Kind, item5.Side, item5.Orientation, GetCoreDebugSpan(item5), item5.FirstPoint.X, item5.FirstPoint.Y, item5.SecondPoint.X, item5.SecondPoint.Y, item5.DebugRole ?? string.Empty, item5.DebugOwner ?? string.Empty, item5.OverrideText ?? string.Empty);
			num3++;
		}
	}

	private static double GetCoreDebugSpan(PlannedDimension dim)
	{
		return (dim.Orientation == DimensionOrientation.Horizontal) ? Math.Abs(dim.SecondPoint.X - dim.FirstPoint.X) : Math.Abs(dim.SecondPoint.Y - dim.FirstPoint.Y);
	}

	private static DimensionPlan CreateCoreDebugRenderPlan(DimensionPlan source)
	{
		DimensionPlan dimensionPlan = new DimensionPlan();
		dimensionPlan.CoordinateFrame = source?.CoordinateFrame ?? CoordinateFrame2D.Identity;
		foreach (PinGroupPlan pinGroup in source.PinGroups)
		{
			dimensionPlan.PinGroups.Add(pinGroup);
		}
		foreach (PlannedDimension dimension in source.Dimensions)
		{
			dimensionPlan.Dimensions.Add(dimension);
		}
		return dimensionPlan;
	}

	private static DiagnosticDimensionSide PromptForDiagnosticSide(Editor editor)
	{
		PromptKeywordOptions promptKeywordOptions = new PromptKeywordOptions("\n选择诊断方向 [全部(A)/顶部(T)/底部(B)/左侧(L)/右侧(R)]", "All Top Bottom Left Right");
		promptKeywordOptions.AllowNone = true;
		promptKeywordOptions.Keywords.Default = "All";
		PromptResult keywords = editor.GetKeywords(promptKeywordOptions);
		if (keywords.Status != PromptStatus.OK)
		{
			return DiagnosticDimensionSide.All;
		}
		return keywords.StringResult switch
		{
			"Top" => DiagnosticDimensionSide.Top,
			"Bottom" => DiagnosticDimensionSide.Bottom,
			"Left" => DiagnosticDimensionSide.Left,
			"Right" => DiagnosticDimensionSide.Right,
			_ => DiagnosticDimensionSide.All,
		};
	}

	private static AutoFixDimOutputScope PromptForReproScope(Editor editor)
	{
		PromptKeywordOptions promptKeywordOptions = new PromptKeywordOptions("\n选择复现范围 [全部(A)/外轮廓(O)/孔(H)/倒角圆角(C)]", "All Outline Hole Corner");
		promptKeywordOptions.AllowNone = true;
		promptKeywordOptions.Keywords.Default = "All";
		PromptResult keywords = editor.GetKeywords(promptKeywordOptions);
		if (keywords.Status != PromptStatus.OK)
		{
			return AutoFixDimOutputScope.All;
		}
		return keywords.StringResult switch
		{
			"Outline" => AutoFixDimOutputScope.OutlineOnly,
			"Hole" => AutoFixDimOutputScope.HoleOnly,
			"Corner" => AutoFixDimOutputScope.CornerOnly,
			_ => AutoFixDimOutputScope.All,
		};
	}

	private static void DrawPostLinearInteractiveAnnotations(Document document, DimensionRuleConfig config, ObjectId dimStyleId, ObjectId diameterCalloutDimStyleId, double dimScale, string annotationLayer, string groupId, OutlineFeature outline, IEnumerable<SlotFeature> slotFeatures, bool includeCornerCallouts, bool includeSlotRadiusCallouts)
	{
		if (document == null || outline == null)
		{
			return;
		}
		Database database = document.Database;
		Editor editor = document.Editor;
		try
		{
			using Transaction transaction = database.TransactionManager.StartTransaction();
			BlockTableRecord space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
			DimensionDrawer dimensionDrawer = new DimensionDrawer(database, transaction, space, config, dimStyleId, diameterCalloutDimStyleId, dimScale, annotationLayer, groupId);
			if (includeCornerCallouts)
			{
				try
				{
					dimensionDrawer.DrawCornerFeatureLeadersWithJig(editor, outline);
				}
				catch (System.Exception ex)
				{
					editor.WriteMessage("\nChamfer/fillet callouts skipped: {0}", ex.Message);
				}
			}
			List<SlotFeature> list = ((slotFeatures == null) ? new List<SlotFeature>() : slotFeatures.Where((SlotFeature s) => s != null).ToList());
			if (includeSlotRadiusCallouts && list.Count > 0)
			{
				try
				{
					dimensionDrawer.DrawSlotRadiusLeadersWithJig(editor, list);
				}
				catch (System.Exception ex2)
				{
					editor.WriteMessage("\nU slot radius callouts skipped: {0}", ex2.Message);
				}
			}
			transaction.Commit();
		}
		catch (System.Exception ex3)
		{
			editor.WriteMessage("\nInteractive corner/slot callouts skipped: {0}", ex3.Message);
		}
	}

	private static bool EnableInlineInteractiveCallouts()
	{
		return false;
	}

	private static IList<HoleCalloutPlan> BuildHoleCalloutPlansForPlacement(IEnumerable<HoleFeature> holes, HoleFeature datumPin, DimensionRuleConfig config)
	{
		List<HoleFeature> source = ((holes == null) ? new List<HoleFeature>() : holes.Where((HoleFeature h) => h != null && !h.IsSlotPoint).ToList());
		DimensionRuleConfig config2 = config;
		List<HoleFeature2D> list = CadToCoreModelMapper.ToCoreHoles(source);
		HoleFeature2D datumPin2 = ((datumPin == null) ? null : (list.FirstOrDefault((HoleFeature2D h) => IsSameHoleForCallout(h, datumPin, config)) ?? CadToCoreModelMapper.ToCoreHoles(new HoleFeature[1] { datumPin }).FirstOrDefault()));
		return new HoleCalloutPlanner(config2).CreatePlans(list, datumPin2);
	}

	private static bool IsSameHoleForCallout(HoleFeature2D a, HoleFeature b, DimensionRuleConfig config)
	{
		if (a == null || b == null)
		{
			return false;
		}
		double num = a.Center.X - b.Center.X;
		double num2 = a.Center.Y - b.Center.Y;
		return num * num + num2 * num2 <= config.GeometryTolerance * config.GeometryTolerance && Math.Abs(a.Diameter - b.Diameter) <= config.GeometryTolerance;
	}

	private static DimensionPlan DrawLinearDimensions(DimensionDrawer drawer, OutlineFeature outline, DatumDefinition datum, IEnumerable<HoleFeature> holes, IEnumerable<SlotFeature> slots, bool skipHoleDimensions, DimensionRuleConfig config, AutoFixDimOutputScope outputScope)
	{
		OutlineFeature2D outline2 = CadToCoreModelMapper.ToCoreOutline(outline);
		Datum2D datum2 = CadToCoreModelMapper.ToCoreDatum(datum);
		List<HoleFeature> source = (skipHoleDimensions ? new List<HoleFeature>() : (holes ?? new List<HoleFeature>()).Where((HoleFeature hole) => hole != null).ToList());
		List<HoleFeature2D> holes2 = CadToCoreModelMapper.ToCoreHoles(source);
		List<SlotFeature2D> slots2 = CadToCoreModelMapper.ToCoreSlots(slots);
		DimensionPlan dimensionPlan = new DimensionPlanner(config).CreateDimensionPlan(outline2, datum2, holes2, slots2);
		drawer.DrawDimensionPlan(FilterDimensionPlan(dimensionPlan, outputScope), dimensionPlan.Diagnostics);
		drawer.FlushStackedDimensions(outline);
		dimensionPlan.SynchronizeFinalPlacementSides();
		return dimensionPlan;
	}

	private static DiagnosticRunContext CreateDiagnosticRunContext(Database database, Transaction transaction, OutlineSelection selection, IEnumerable<ObjectId> holeSourceIds, OutlineFeature outline, DatumDefinition datum, string runId, string commandName)
	{
		DiagnosticRunContext diagnosticRunContext = new DiagnosticRunContext
		{
			RunId = runId ?? string.Empty,
			Command = commandName ?? string.Empty,
			Drawing = CreateDiagnosticFileIdentity(database?.Filename),
			Plugin = CreateDiagnosticFileIdentity(typeof(Commands).Assembly.Location),
			SelectedGeometryBoundsStatus = "Unavailable",
			BaseX = datum?.BaseX ?? 0.0,
			BaseY = datum?.BaseY ?? 0.0,
			RecognizedOutlineBoundsWcs = ((outline == null) ? null : new DiagnosticBounds
			{
				MinX = outline.MinX,
				MinY = outline.MinY,
				MinZ = 0.0,
				MaxX = outline.MaxX,
				MaxY = outline.MaxY,
				MaxZ = 0.0
			}),
			HasDatumHole = datum?.DatumHole != null,
			DatumHoleHandle = TryGetHandle(datum?.DatumHole),
			DatumHoleCenterWcs = ((datum?.DatumHole == null) ? null : new Point3d?(datum.DatumHole.Center)),
			DatumHoleLocationBaseX = datum?.DatumHoleLocationBaseX,
			DatumHoleLocationBaseY = datum?.DatumHoleLocationBaseY,
			DatumHoleLocationUseToleranceX = datum?.DatumHoleLocationUseToleranceX ?? false,
			DatumHoleLocationUseToleranceY = datum?.DatumHoleLocationUseToleranceY ?? false,
			AutoCadVersion = GetSystemVariableText("ACADVER"),
			InsUnits = GetSystemVariableText("INSUNITS"),
			UcsName = GetSystemVariableText("UCSNAME"),
			UcsOriginWcs = GetSystemVariablePoint("UCSORG"),
			UcsXDirectionWcs = GetSystemVariablePoint("UCSXDIR"),
			UcsYDirectionWcs = GetSystemVariablePoint("UCSYDIR"),
			DimScale = database?.Dimscale ?? 1.0,
			DimStyle = GetSystemVariableText("DIMSTYLE")
		};
		try
		{
			ObjectId resolvedDimStyleId = DimStyleManager.ResolveDimStyle(database, transaction);
			if (!resolvedDimStyleId.IsNull)
			{
				DimStyleTableRecord dimStyleTableRecord = (DimStyleTableRecord)transaction.GetObject(resolvedDimStyleId, OpenMode.ForRead);
				diagnosticRunContext.ResolvedDimStyleName = dimStyleTableRecord.Name;
				diagnosticRunContext.StyleDimscale = dimStyleTableRecord.Dimscale;
				double effectiveDimScale = ((dimStyleTableRecord.Dimscale > 1E-09) ? dimStyleTableRecord.Dimscale : (database?.Dimscale ?? 1.0));
				if (effectiveDimScale <= 0.0)
				{
					effectiveDimScale = 1.0;
				}
				diagnosticRunContext.EffectiveTextHeight = dimStyleTableRecord.Dimtxt * effectiveDimScale;
				diagnosticRunContext.EffectiveArrowSize = dimStyleTableRecord.Dimasz * effectiveDimScale;
			}
		}
		catch (System.Exception)
		{
			// Observability only - never fail the run over style metrics.
		}
		IEnumerable<ObjectId> source = selection?.SelectedIds ?? Enumerable.Empty<ObjectId>();
		IEnumerable<ObjectId> second = holeSourceIds ?? Enumerable.Empty<ObjectId>();
		ObjectId objectId = ((datum?.DatumHole == null) ? ObjectId.Null : (!datum.DatumHole.CircleId.IsNull ? datum.DatumHole.CircleId : datum.DatumHole.SourceId));
		List<ObjectId> list = source.Concat(second).Concat(objectId.IsNull ? Enumerable.Empty<ObjectId>() : new ObjectId[1] { objectId }).Where((ObjectId id) => !id.IsNull).Distinct().ToList();
		diagnosticRunContext.EntityCount = list.Count;
		DiagnosticBounds diagnosticBounds = null;
		int num = 0;
		foreach (ObjectId item in list)
		{
			try
			{
				diagnosticRunContext.EntityHandles.Add(item.Handle.ToString());
			}
			catch (System.Exception)
			{
			}
			Entity entity = null;
			try
			{
				entity = transaction.GetObject(item, OpenMode.ForRead, openErased: false) as Entity;
			}
			catch (System.Exception)
			{
			}
			if (entity == null)
			{
				continue;
			}
			string name = entity.GetRXClass()?.DxfName ?? entity.GetType().Name;
			diagnosticRunContext.EntityTypeCounts[name] = (diagnosticRunContext.EntityTypeCounts.TryGetValue(name, out var value) ? (value + 1) : 1);
			try
			{
				Extents3d geometricExtents = entity.GeometricExtents;
				ExpandDiagnosticBounds(ref diagnosticBounds, geometricExtents.MinPoint);
				ExpandDiagnosticBounds(ref diagnosticBounds, geometricExtents.MaxPoint);
				num++;
			}
			catch (System.Exception)
			{
			}
		}
		diagnosticRunContext.EntityHandles.Sort(StringComparer.Ordinal);
		diagnosticRunContext.SelectedGeometryBoundsWcs = diagnosticBounds;
		diagnosticRunContext.SelectedGeometryBoundsStatus = ((num == list.Count && diagnosticRunContext.EntityHandles.Count == list.Count) ? "Ok" : ("Partial:" + num.ToString(CultureInfo.InvariantCulture) + "/" + list.Count.ToString(CultureInfo.InvariantCulture)));
		return diagnosticRunContext;
	}

	private static DiagnosticFileIdentity CreateDiagnosticFileIdentity(string path)
	{
		DiagnosticFileIdentity diagnosticFileIdentity = new DiagnosticFileIdentity
		{
			Path = path ?? string.Empty,
			FileName = string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileName(path),
			LastWriteTimeUtc = string.Empty,
			Sha256 = string.Empty,
			IdentityStatus = "Missing"
		};
		if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
		{
			return diagnosticFileIdentity;
		}
		try
		{
			string fullPath = Path.GetFullPath(path);
			FileInfo fileInfo = new FileInfo(fullPath);
			diagnosticFileIdentity.Path = fullPath;
			diagnosticFileIdentity.FileName = fileInfo.Name;
			diagnosticFileIdentity.SizeBytes = fileInfo.Length;
			diagnosticFileIdentity.LastWriteTimeUtc = fileInfo.LastWriteTimeUtc.ToString("o", CultureInfo.InvariantCulture);
			using (FileStream inputStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
			using (SHA256 sHA = SHA256.Create())
			{
				diagnosticFileIdentity.Sha256 = BitConverter.ToString(sHA.ComputeHash(inputStream)).Replace("-", string.Empty);
			}
			diagnosticFileIdentity.IdentityStatus = "Ok";
		}
		catch (System.Exception ex)
		{
			diagnosticFileIdentity.IdentityStatus = "HashUnavailable:" + ex.GetType().Name;
		}
		return diagnosticFileIdentity;
	}

	private static string TryGetHandle(HoleFeature hole)
	{
		if (hole == null)
		{
			return string.Empty;
		}
		ObjectId objectId = !hole.CircleId.IsNull ? hole.CircleId : hole.SourceId;
		if (objectId.IsNull)
		{
			return string.Empty;
		}
		try
		{
			return objectId.Handle.ToString();
		}
		catch (System.Exception)
		{
			return string.Empty;
		}
	}

	private static string GetSystemVariableText(string name)
	{
		try
		{
			return Convert.ToString(Application.GetSystemVariable(name), CultureInfo.InvariantCulture) ?? string.Empty;
		}
		catch (System.Exception)
		{
			return string.Empty;
		}
	}

	private static Point3d? GetSystemVariablePoint(string name)
	{
		try
		{
			object systemVariable = Application.GetSystemVariable(name);
			return (systemVariable is Point3d point3d) ? new Point3d?(point3d) : null;
		}
		catch (System.Exception)
		{
			return null;
		}
	}

	private static void ExpandDiagnosticBounds(ref DiagnosticBounds bounds, Point3d point)
	{
		if (bounds == null)
		{
			bounds = new DiagnosticBounds
			{
				MinX = point.X,
				MinY = point.Y,
				MinZ = point.Z,
				MaxX = point.X,
				MaxY = point.Y,
				MaxZ = point.Z
			};
			return;
		}
		bounds.MinX = Math.Min(bounds.MinX, point.X);
		bounds.MinY = Math.Min(bounds.MinY, point.Y);
		bounds.MinZ = Math.Min(bounds.MinZ, point.Z);
		bounds.MaxX = Math.Max(bounds.MaxX, point.X);
		bounds.MaxY = Math.Max(bounds.MaxY, point.Y);
		bounds.MaxZ = Math.Max(bounds.MaxZ, point.Z);
	}

	private static string WriteDimensionDiagnosticReport(DimensionDiagnosticReport diagnostics, OutlineFeature outline, IEnumerable<HoleFeature> holes, IEnumerable<SlotFeature> slots, AutoFixDimOutputScope outputScope, bool diagnosticsEnabled, DiagnosticDimensionSide diagnosticSide, DiagnosticRunContext context, CoordinateFrame2D coordinateFrame, System.Exception error = null)
	{
		diagnostics = diagnostics ?? new DimensionDiagnosticReport();
		List<HoleFeature> source = (holes ?? Enumerable.Empty<HoleFeature>()).Where((HoleFeature h) => h != null).ToList();
		diagnostics.Features.OutlineCount = ((outline != null) ? 1 : 0);
		diagnostics.Features.HoleCount = source.Count((HoleFeature h) => !h.IsPinHole && !h.IsThreadHole && !h.IsSlotPoint);
		diagnostics.Features.PinHoleCount = source.Count((HoleFeature h) => h.IsPinHole);
		diagnostics.Features.ThreadHoleCount = source.Count((HoleFeature h) => h.IsThreadHole);
		diagnostics.Features.SlotCount = (slots ?? Enumerable.Empty<SlotFeature>()).Count((SlotFeature s) => s != null);
		diagnostics.Features.ChamferCount = outline?.Chamfers.Count ?? 0;
		diagnostics.Features.FilletCount = outline?.Fillets.Count ?? 0;
		string text2 = Environment.GetEnvironmentVariable("AUTOFIXDIM_DIAGNOSTIC_REPORT_PATH");
		if (string.IsNullOrWhiteSpace(text2))
		{
			string text = Path.Combine(GetProjectRootOrAssemblyDirectory(), "diagnostics");
			Directory.CreateDirectory(text);
			text2 = Path.Combine(text, "last-run.json");
		}
		else
		{
			text2 = Path.GetFullPath(text2);
			Directory.CreateDirectory(Path.GetDirectoryName(text2));
		}
		File.WriteAllText(text2, SerializeDimensionDiagnosticReport(diagnostics, outputScope, diagnosticsEnabled, diagnosticSide, context, coordinateFrame, error), Encoding.UTF8);
		return text2;
	}

	private static string GetProjectRootOrAssemblyDirectory()
	{
		string location = typeof(Commands).Assembly.Location;
		string text = (string.IsNullOrEmpty(location) ? Environment.CurrentDirectory : Path.GetDirectoryName(location));
		string text2 = text;
		while (!string.IsNullOrEmpty(text2))
		{
			if (File.Exists(Path.Combine(text2, "AutoFixtureDim.csproj")))
			{
				return text2;
			}
			text2 = Directory.GetParent(text2)?.FullName;
		}
		return text ?? Environment.CurrentDirectory;
	}

	private static int _nonFiniteJsonValueCount;

	private static string SerializeDimensionDiagnosticReport(DimensionDiagnosticReport report, AutoFixDimOutputScope outputScope, bool diagnosticsEnabled, DiagnosticDimensionSide diagnosticSide, DiagnosticRunContext context, CoordinateFrame2D coordinateFrame, System.Exception error = null)
	{
		_nonFiniteJsonValueCount = 0;
		context = context ?? new DiagnosticRunContext
		{
			RunId = string.Empty,
			Command = string.Empty,
			Drawing = new DiagnosticFileIdentity(),
			Plugin = new DiagnosticFileIdentity()
		};
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("{");
		AppendJsonProperty(stringBuilder, 1, "schemaVersion", 2, comma: true);
		AppendJsonProperty(stringBuilder, 1, "runId", context.RunId, comma: true);
		AppendJsonProperty(stringBuilder, 1, "generatedAt", DateTime.Now.ToString("o", CultureInfo.InvariantCulture), comma: true);
		AppendJsonProperty(stringBuilder, 1, "command", context.Command, comma: true);
		AppendJsonProperty(stringBuilder, 1, "commandScope", outputScope.ToString(), comma: true);
		AppendJsonProperty(stringBuilder, 1, "diagnosticsEnabled", diagnosticsEnabled, comma: true);
		AppendJsonProperty(stringBuilder, 1, "diagnosticSide", diagnosticSide.ToString(), comma: true);
		if (error != null)
		{
			AppendIndent(stringBuilder, 1);
			stringBuilder.AppendLine("\"error\": {");
			AppendJsonProperty(stringBuilder, 2, "type", error.GetType().FullName, comma: true);
			AppendJsonProperty(stringBuilder, 2, "message", error.Message, comma: true);
			AppendJsonProperty(stringBuilder, 2, "stackTrace", error.StackTrace ?? string.Empty, comma: false);
			AppendIndent(stringBuilder, 1);
			stringBuilder.AppendLine("},");
		}
		AppendDiagnosticFileIdentity(stringBuilder, 1, "drawing", context.Drawing, comma: true);
		AppendDiagnosticFileIdentity(stringBuilder, 1, "plugin", context.Plugin, comma: true);
		AppendJsonProperty(stringBuilder, 1, "coordinateSystem", "WCS", comma: true);
		CoordinateFrame2D effectiveFrame = coordinateFrame ?? CoordinateFrame2D.Identity;
		AppendJsonProperty(stringBuilder, 1, "dimensionCoordinateSystem", effectiveFrame.IsIdentity ? "WCS" : "LocalPlanningFrame", comma: true);
		AppendJsonProperty(stringBuilder, 1, "dimensionFrameAngleRadians", effectiveFrame.Angle, comma: true);
		AppendJsonProperty(stringBuilder, 1, "dimensionFrameOriginX", effectiveFrame.Origin.X, comma: true);
		AppendJsonProperty(stringBuilder, 1, "dimensionFrameOriginY", effectiveFrame.Origin.Y, comma: true);
		AppendDiagnosticSelection(stringBuilder, context, comma: true);
		AppendDiagnosticDatum(stringBuilder, context, comma: true);
		AppendDiagnosticEnvironment(stringBuilder, context, comma: true);
		AppendFeatureCounts(stringBuilder, report.Features, comma: true);
		AppendDimensionDiagnostics(stringBuilder, 1, "dimensionCandidates", report.DimensionCandidates, comma: true);
		AppendDimensionDiagnostics(stringBuilder, 1, "finalDimensions", report.FinalDimensions, comma: true);
		AppendJsonProperty(stringBuilder, 1, "nonFiniteValueCount", _nonFiniteJsonValueCount, comma: true);
		AppendIndent(stringBuilder, 1);
		stringBuilder.AppendLine("\"warnings\": [");
		for (int warningIndex = 0; warningIndex < report.Warnings.Count; warningIndex++)
		{
			AppendIndent(stringBuilder, 2);
			stringBuilder.Append('"').Append(JsonEscape(report.Warnings[warningIndex])).Append('"');
			stringBuilder.AppendLine((warningIndex < report.Warnings.Count - 1) ? "," : string.Empty);
		}
		AppendIndent(stringBuilder, 1);
		stringBuilder.AppendLine("]");
		stringBuilder.AppendLine("}");
		return stringBuilder.ToString();
	}

	private static void AppendDiagnosticFileIdentity(StringBuilder builder, int indent, string name, DiagnosticFileIdentity identity, bool comma)
	{
		identity = identity ?? new DiagnosticFileIdentity();
		AppendIndent(builder, indent);
		builder.Append('"').Append(JsonEscape(name)).AppendLine("\": {");
		AppendJsonProperty(builder, indent + 1, "path", identity.Path, comma: true);
		AppendJsonProperty(builder, indent + 1, "fileName", identity.FileName, comma: true);
		AppendJsonProperty(builder, indent + 1, "sizeBytes", identity.SizeBytes, comma: true);
		AppendJsonProperty(builder, indent + 1, "lastWriteTimeUtc", identity.LastWriteTimeUtc, comma: true);
		AppendJsonProperty(builder, indent + 1, "sha256", identity.Sha256, comma: true);
		AppendJsonProperty(builder, indent + 1, "identityStatus", identity.IdentityStatus, comma: false);
		AppendIndent(builder, indent);
		builder.Append("}");
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendDiagnosticSelection(StringBuilder builder, DiagnosticRunContext context, bool comma)
	{
		AppendIndent(builder, 1);
		builder.AppendLine("\"selection\": {");
		AppendJsonProperty(builder, 2, "entityCount", context.EntityCount, comma: true);
		AppendStringArray(builder, 2, "entityHandles", context.EntityHandles, comma: true);
		AppendStringIntMap(builder, 2, "entityTypeCounts", context.EntityTypeCounts, comma: true);
		AppendJsonProperty(builder, 2, "selectedGeometryBoundsStatus", context.SelectedGeometryBoundsStatus, comma: true);
		AppendDiagnosticBounds(builder, 2, "selectedGeometryBoundsWcs", context.SelectedGeometryBoundsWcs, comma: true);
		AppendDiagnosticBounds(builder, 2, "recognizedOutlineBoundsWcs", context.RecognizedOutlineBoundsWcs, comma: false);
		AppendIndent(builder, 1);
		builder.Append("}");
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendDiagnosticDatum(StringBuilder builder, DiagnosticRunContext context, bool comma)
	{
		AppendIndent(builder, 1);
		builder.AppendLine("\"datum\": {");
		AppendJsonProperty(builder, 2, "baseX", context.BaseX, comma: true);
		AppendJsonProperty(builder, 2, "baseY", context.BaseY, comma: true);
		AppendJsonProperty(builder, 2, "hasDatumHole", context.HasDatumHole, comma: true);
		AppendJsonProperty(builder, 2, "holeHandle", context.DatumHoleHandle, comma: true);
		AppendNullablePoint(builder, 2, "holeCenterWcs", context.DatumHoleCenterWcs, comma: true);
		AppendNullableJsonProperty(builder, 2, "xBaseCoordinate", context.DatumHoleLocationBaseX, comma: true);
		AppendNullableJsonProperty(builder, 2, "yBaseCoordinate", context.DatumHoleLocationBaseY, comma: true);
		AppendJsonProperty(builder, 2, "useToleranceX", context.DatumHoleLocationUseToleranceX, comma: true);
		AppendJsonProperty(builder, 2, "useToleranceY", context.DatumHoleLocationUseToleranceY, comma: false);
		AppendIndent(builder, 1);
		builder.Append("}");
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendDiagnosticEnvironment(StringBuilder builder, DiagnosticRunContext context, bool comma)
	{
		AppendIndent(builder, 1);
		builder.AppendLine("\"environment\": {");
		AppendJsonProperty(builder, 2, "autoCadVersion", context.AutoCadVersion, comma: true);
		AppendJsonProperty(builder, 2, "insUnits", context.InsUnits, comma: true);
		AppendJsonProperty(builder, 2, "ucsName", context.UcsName, comma: true);
		AppendNullablePoint(builder, 2, "ucsOriginWcs", context.UcsOriginWcs, comma: true);
		AppendNullablePoint(builder, 2, "ucsXDirectionWcs", context.UcsXDirectionWcs, comma: true);
		AppendNullablePoint(builder, 2, "ucsYDirectionWcs", context.UcsYDirectionWcs, comma: true);
		AppendJsonProperty(builder, 2, "dimScale", context.DimScale, comma: true);
		AppendJsonProperty(builder, 2, "dimStyle", context.DimStyle, comma: true);
		AppendJsonProperty(builder, 2, "resolvedDimStyleName", context.ResolvedDimStyleName ?? string.Empty, comma: true);
		AppendNullableJsonProperty(builder, 2, "styleDimscale", context.StyleDimscale, comma: true);
		AppendNullableJsonProperty(builder, 2, "effectiveTextHeight", context.EffectiveTextHeight, comma: true);
		AppendNullableJsonProperty(builder, 2, "effectiveArrowSize", context.EffectiveArrowSize, comma: false);
		AppendIndent(builder, 1);
		builder.Append("}");
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendDiagnosticBounds(StringBuilder builder, int indent, string name, DiagnosticBounds bounds, bool comma)
	{
		if (bounds == null)
		{
			AppendJsonNullProperty(builder, indent, name, comma);
			return;
		}
		AppendIndent(builder, indent);
		builder.Append('"').Append(JsonEscape(name)).AppendLine("\": {");
		AppendJsonProperty(builder, indent + 1, "minX", bounds.MinX, comma: true);
		AppendJsonProperty(builder, indent + 1, "minY", bounds.MinY, comma: true);
		AppendJsonProperty(builder, indent + 1, "minZ", bounds.MinZ, comma: true);
		AppendJsonProperty(builder, indent + 1, "maxX", bounds.MaxX, comma: true);
		AppendJsonProperty(builder, indent + 1, "maxY", bounds.MaxY, comma: true);
		AppendJsonProperty(builder, indent + 1, "maxZ", bounds.MaxZ, comma: false);
		AppendIndent(builder, indent);
		builder.Append("}");
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendNullablePoint(StringBuilder builder, int indent, string name, Point3d? point, bool comma)
	{
		if (!point.HasValue)
		{
			AppendJsonNullProperty(builder, indent, name, comma);
			return;
		}
		AppendIndent(builder, indent);
		builder.Append('"').Append(JsonEscape(name)).AppendLine("\": {");
		AppendJsonProperty(builder, indent + 1, "x", point.Value.X, comma: true);
		AppendJsonProperty(builder, indent + 1, "y", point.Value.Y, comma: true);
		AppendJsonProperty(builder, indent + 1, "z", point.Value.Z, comma: false);
		AppendIndent(builder, indent);
		builder.Append("}");
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendStringArray(StringBuilder builder, int indent, string name, IEnumerable<string> values, bool comma)
	{
		AppendIndent(builder, indent);
		builder.Append('"').Append(JsonEscape(name)).Append("\": [");
		List<string> list = (values ?? Enumerable.Empty<string>()).ToList();
		for (int i = 0; i < list.Count; i++)
		{
			if (i > 0)
			{
				builder.Append(", ");
			}
			builder.Append('"').Append(JsonEscape(list[i] ?? string.Empty)).Append('"');
		}
		builder.Append("]");
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendStringIntMap(StringBuilder builder, int indent, string name, IEnumerable<KeyValuePair<string, int>> values, bool comma)
	{
		AppendIndent(builder, indent);
		builder.Append('"').Append(JsonEscape(name)).AppendLine("\": {");
		List<KeyValuePair<string, int>> list = (values ?? Enumerable.Empty<KeyValuePair<string, int>>()).ToList();
		for (int i = 0; i < list.Count; i++)
		{
			AppendJsonProperty(builder, indent + 1, list[i].Key, list[i].Value, i < list.Count - 1);
		}
		AppendIndent(builder, indent);
		builder.Append("}");
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendFeatureCounts(StringBuilder builder, FeatureDiagnosticCounts counts, bool comma)
	{
		AppendIndent(builder, 1);
		builder.AppendLine("\"features\": {");
		AppendJsonProperty(builder, 2, "outlineCount", counts.OutlineCount, comma: true);
		AppendJsonProperty(builder, 2, "holeCount", counts.HoleCount, comma: true);
		AppendJsonProperty(builder, 2, "pinHoleCount", counts.PinHoleCount, comma: true);
		AppendJsonProperty(builder, 2, "threadHoleCount", counts.ThreadHoleCount, comma: true);
		AppendJsonProperty(builder, 2, "slotCount", counts.SlotCount, comma: true);
		AppendJsonProperty(builder, 2, "chamferCount", counts.ChamferCount, comma: true);
		AppendJsonProperty(builder, 2, "filletCount", counts.FilletCount, comma: false);
		AppendIndent(builder, 1);
		builder.Append("}");
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendDimensionDiagnostics(StringBuilder builder, int indent, string name, IEnumerable<DimensionCandidateDiagnostic> candidates, bool comma)
	{
		AppendIndent(builder, indent);
		builder.Append('"').Append(JsonEscape(name)).AppendLine("\": [");
		List<DimensionCandidateDiagnostic> list = (candidates ?? Enumerable.Empty<DimensionCandidateDiagnostic>()).ToList();
		for (int i = 0; i < list.Count; i++)
		{
			DimensionCandidateDiagnostic dimensionCandidateDiagnostic = list[i];
			AppendIndent(builder, indent + 1);
			builder.AppendLine("{");
			AppendJsonProperty(builder, indent + 2, "id", dimensionCandidateDiagnostic.Id, comma: true);
			AppendJsonProperty(builder, indent + 2, "kind", dimensionCandidateDiagnostic.Kind, comma: true);
			AppendJsonProperty(builder, indent + 2, "sourceFeatureId", dimensionCandidateDiagnostic.SourceFeatureId, comma: true);
			AppendJsonProperty(builder, indent + 2, "value", dimensionCandidateDiagnostic.Value, comma: true);
			AppendJsonProperty(builder, indent + 2, "firstPointX", dimensionCandidateDiagnostic.FirstPointX, comma: true);
			AppendJsonProperty(builder, indent + 2, "firstPointY", dimensionCandidateDiagnostic.FirstPointY, comma: true);
			AppendJsonProperty(builder, indent + 2, "secondPointX", dimensionCandidateDiagnostic.SecondPointX, comma: true);
			AppendJsonProperty(builder, indent + 2, "secondPointY", dimensionCandidateDiagnostic.SecondPointY, comma: true);
			AppendJsonProperty(builder, indent + 2, "measurementMinimum", dimensionCandidateDiagnostic.MeasurementMinimum, comma: true);
			AppendJsonProperty(builder, indent + 2, "measurementMaximum", dimensionCandidateDiagnostic.MeasurementMaximum, comma: true);
			AppendJsonProperty(builder, indent + 2, "placementSide", dimensionCandidateDiagnostic.PlacementSide, comma: true);
			AppendJsonProperty(builder, indent + 2, "requestedPlacementSide", dimensionCandidateDiagnostic.RequestedPlacementSide, comma: true);
			AppendJsonProperty(builder, indent + 2, "priority", dimensionCandidateDiagnostic.Priority, comma: true);
			AppendJsonProperty(builder, indent + 2, "readingLevel", dimensionCandidateDiagnostic.ReadingLevel, comma: true);
			AppendJsonProperty(builder, indent + 2, "alignmentKey", dimensionCandidateDiagnostic.AlignmentKey, comma: true);
			AppendJsonProperty(builder, indent + 2, "alignmentPriority", dimensionCandidateDiagnostic.AlignmentPriority, comma: true);
			AppendJsonProperty(builder, indent + 2, "preserveAlignmentLevel", dimensionCandidateDiagnostic.PreserveAlignmentLevel, comma: true);
			AppendJsonProperty(builder, indent + 2, "hasFinalPlacement", dimensionCandidateDiagnostic.HasFinalPlacement, comma: true);
			AppendNullableJsonProperty(builder, indent + 2, "stackingLevel", dimensionCandidateDiagnostic.StackingLevel, comma: true);
			AppendNullableJsonProperty(builder, indent + 2, "stackingOffset", dimensionCandidateDiagnostic.StackingOffset, comma: true);
			AppendNullableJsonProperty(builder, indent + 2, "resolvedDimLineCoordinate", dimensionCandidateDiagnostic.ResolvedDimLineCoordinate, comma: true);
			AppendNullableJsonProperty(builder, indent + 2, "usesLocalBoundary", dimensionCandidateDiagnostic.UsesLocalBoundary, comma: true);
			AppendNullableJsonProperty(builder, indent + 2, "hasAlignmentCoordinateOverride", dimensionCandidateDiagnostic.HasAlignmentCoordinateOverride, comma: true);
			AppendJsonProperty(builder, indent + 2, "alignmentLaneKey", dimensionCandidateDiagnostic.AlignmentLaneKey, comma: true);
			AppendNullableJsonProperty(builder, indent + 2, "alignmentLaneMemberCount", dimensionCandidateDiagnostic.AlignmentLaneMemberCount, comma: true);
			AppendJsonProperty(builder, indent + 2, "alignmentDecision", dimensionCandidateDiagnostic.AlignmentDecision, comma: true);
			AppendJsonProperty(builder, indent + 2, "layoutBlockId", dimensionCandidateDiagnostic.LayoutBlockId, comma: true);
			AppendJsonProperty(builder, indent + 2, "layoutBlockType", dimensionCandidateDiagnostic.LayoutBlockType, comma: true);
			AppendJsonProperty(builder, indent + 2, "effectiveSpan", dimensionCandidateDiagnostic.EffectiveSpan, comma: true);
			AppendJsonProperty(builder, indent + 2, "effectiveOrder", dimensionCandidateDiagnostic.EffectiveOrder, comma: true);
			AppendJsonProperty(builder, indent + 2, "orderingReason", dimensionCandidateDiagnostic.OrderingReason, comma: true);
			AppendJsonProperty(builder, indent + 2, "promotedByConflictWith", dimensionCandidateDiagnostic.PromotedByConflictWith, comma: true);
			AppendJsonProperty(builder, indent + 2, "physicalOutwardDistance", dimensionCandidateDiagnostic.PhysicalOutwardDistance, comma: true);
			AppendJsonProperty(builder, indent + 2, "physicalOrderValidated", dimensionCandidateDiagnostic.PhysicalOrderValidated, comma: true);
			AppendJsonProperty(builder, indent + 2, "isSuppressed", dimensionCandidateDiagnostic.IsSuppressed, comma: true);
			AppendJsonProperty(builder, indent + 2, "isSelected", dimensionCandidateDiagnostic.IsSelected, comma: true);
			AppendJsonProperty(builder, indent + 2, "isAttachmentValid", dimensionCandidateDiagnostic.IsAttachmentValid, comma: true);
			AppendJsonProperty(builder, indent + 2, "decisionStatus", dimensionCandidateDiagnostic.DecisionStatus, comma: true);
			AppendJsonProperty(builder, indent + 2, "decisionReason", dimensionCandidateDiagnostic.DecisionReason, comma: true);
			AppendJsonProperty(builder, indent + 2, "suppressedReason", dimensionCandidateDiagnostic.SuppressedReason, comma: true);
			AppendJsonProperty(builder, indent + 2, "orientation", dimensionCandidateDiagnostic.Orientation, comma: true);
			AppendJsonProperty(builder, indent + 2, "debugRole", dimensionCandidateDiagnostic.DebugRole, comma: true);
			AppendJsonProperty(builder, indent + 2, "overrideText", dimensionCandidateDiagnostic.OverrideText, comma: true);
			AppendJsonProperty(builder, indent + 2, "role", dimensionCandidateDiagnostic.Role.ToString(), comma: true);
			AppendJsonProperty(builder, indent + 2, "ownerKind", dimensionCandidateDiagnostic.OwnerKind.ToString(), comma: true);
			AppendStringArray(builder, indent + 2, "sourceGeometryIds", dimensionCandidateDiagnostic.SourceGeometryIds, comma: true);
			AppendJsonProperty(builder, indent + 2, "topologyEvidence", dimensionCandidateDiagnostic.TopologyEvidence, comma: true);
			AppendJsonProperty(builder, indent + 2, "decision", dimensionCandidateDiagnostic.Decision.ToString(), comma: true);
			AppendJsonProperty(builder, indent + 2, "ruleId", dimensionCandidateDiagnostic.RuleId, comma: false);
			AppendIndent(builder, indent + 1);
			builder.Append("}");
			builder.AppendLine((i == list.Count - 1) ? string.Empty : ",");
		}
		AppendIndent(builder, indent);
		builder.Append("]");
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendJsonProperty(StringBuilder builder, int indent, string name, string value, bool comma)
	{
		AppendIndent(builder, indent);
		builder.Append('"').Append(JsonEscape(name)).Append("\": \"")
			.Append(JsonEscape(value ?? string.Empty))
			.Append('"');
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendJsonProperty(StringBuilder builder, int indent, string name, int value, bool comma)
	{
		AppendIndent(builder, indent);
		builder.Append('"').Append(JsonEscape(name)).Append("\": ")
			.Append(value.ToString(CultureInfo.InvariantCulture));
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendJsonProperty(StringBuilder builder, int indent, string name, long value, bool comma)
	{
		AppendIndent(builder, indent);
		builder.Append('"').Append(JsonEscape(name)).Append("\": ")
			.Append(value.ToString(CultureInfo.InvariantCulture));
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendJsonProperty(StringBuilder builder, int indent, string name, double value, bool comma)
	{
		if (double.IsNaN(value) || double.IsInfinity(value))
		{
			// A bare NaN/Infinity token would make the whole file unparseable JSON.
			_nonFiniteJsonValueCount++;
			AppendJsonNullProperty(builder, indent, name, comma);
			return;
		}
		AppendIndent(builder, indent);
		builder.Append('"').Append(JsonEscape(name)).Append("\": ")
			.Append(value.ToString("0.########", CultureInfo.InvariantCulture));
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendJsonProperty(StringBuilder builder, int indent, string name, bool value, bool comma)
	{
		AppendIndent(builder, indent);
		builder.Append('"').Append(JsonEscape(name)).Append("\": ")
			.Append(value ? "true" : "false");
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendNullableJsonProperty(StringBuilder builder, int indent, string name, int? value, bool comma)
	{
		if (value.HasValue)
		{
			AppendJsonProperty(builder, indent, name, value.Value, comma);
			return;
		}
		AppendJsonNullProperty(builder, indent, name, comma);
	}

	private static void AppendNullableJsonProperty(StringBuilder builder, int indent, string name, double? value, bool comma)
	{
		if (value.HasValue)
		{
			AppendJsonProperty(builder, indent, name, value.Value, comma);
			return;
		}
		AppendJsonNullProperty(builder, indent, name, comma);
	}

	private static void AppendNullableJsonProperty(StringBuilder builder, int indent, string name, bool? value, bool comma)
	{
		if (value.HasValue)
		{
			AppendJsonProperty(builder, indent, name, value.Value, comma);
			return;
		}
		AppendJsonNullProperty(builder, indent, name, comma);
	}

	private static void AppendJsonNullProperty(StringBuilder builder, int indent, string name, bool comma)
	{
		AppendIndent(builder, indent);
		builder.Append('"').Append(JsonEscape(name)).Append("\": null");
		builder.AppendLine(comma ? "," : string.Empty);
	}

	private static void AppendIndent(StringBuilder builder, int indent)
	{
		builder.Append(new string(' ', indent * 2));
	}

	private static string JsonEscape(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder(value.Length + 8);
		foreach (char c in value)
		{
			switch (c)
			{
			case '\\':
				stringBuilder.Append("\\\\");
				break;
			case '"':
				stringBuilder.Append("\\\"");
				break;
			case '\r':
				stringBuilder.Append("\\r");
				break;
			case '\n':
				stringBuilder.Append("\\n");
				break;
			case '\t':
				stringBuilder.Append("\\t");
				break;
			default:
				if (c < ' ')
				{
					stringBuilder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
				}
				else
				{
					stringBuilder.Append(c);
				}
				break;
			}
		}
		return stringBuilder.ToString();
	}

	private static DimensionPlan FilterDimensionPlan(DimensionPlan source, AutoFixDimOutputScope outputScope)
	{
		if (source == null || outputScope == AutoFixDimOutputScope.All)
		{
			return source;
		}
		DimensionPlan dimensionPlan = new DimensionPlan();
		dimensionPlan.CoordinateFrame = source.CoordinateFrame ?? CoordinateFrame2D.Identity;
		foreach (PinGroupPlan pinGroup in source.PinGroups)
		{
			dimensionPlan.PinGroups.Add(pinGroup);
		}
		foreach (PlannedDimension dimension in source.Dimensions)
		{
			if (ShouldRenderPlannedDimension(dimension, outputScope))
			{
				dimensionPlan.Dimensions.Add(dimension);
			}
			else
			{
				source.Diagnostics.RecordRenderSuppressed(dimension.DiagnosticId, "OutOfCommandScope:" + outputScope);
			}
		}
		return dimensionPlan;
	}

	private static bool ShouldRenderPlannedDimension(PlannedDimension dimension, AutoFixDimOutputScope outputScope)
	{
		if (dimension == null)
		{
			return false;
		}
		return outputScope switch
		{
			AutoFixDimOutputScope.OutlineOnly => IsOutlineDimension(dimension),
			AutoFixDimOutputScope.HoleOnly => IsHoleDimension(dimension) || IsSlotDimension(dimension),
			_ => outputScope == AutoFixDimOutputScope.All,
		};
	}

	private static bool IsOutlineDimension(PlannedDimension dimension)
	{
		switch (dimension.Kind)
		{
		case DimensionKind.OverallWidth:
		case DimensionKind.OverallHeight:
			return true;
		case DimensionKind.Normal:
			return !IsSlotDimension(dimension);
		default:
			return false;
		}
	}

	private static bool IsHoleDimension(PlannedDimension dimension)
	{
		switch (dimension.Kind)
		{
		case DimensionKind.HoleDiameter:
		case DimensionKind.PinDistance:
		case DimensionKind.PinGroupDistance:
		case DimensionKind.HoleLocation:
		case DimensionKind.DatumHoleLocationX:
		case DimensionKind.DatumHoleLocationY:
			return true;
		default:
			return false;
		}
	}

	private static bool IsSlotDimension(PlannedDimension dimension)
	{
		return !string.IsNullOrEmpty(dimension.DebugRole) && dimension.DebugRole.IndexOf("Slot", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static void DrawCornerFeatureLeadersWithPreview(Editor editor, DimensionDrawer drawer, OutlineFeature outline)
	{
		if (outline.Chamfers.Count != 0 || outline.Fillets.Count != 0)
		{
			drawer.DrawCornerFeatureLeadersWithJig(editor, outline);
		}
	}
}
