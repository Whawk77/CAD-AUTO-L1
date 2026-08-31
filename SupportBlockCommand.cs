using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter;
using CadAuto.CadAdapter.Environment;
using CadAuto.CadAdapter.Rendering;
using CadAuto.Core.Planning;
using CadAuto.Core.Rules;

namespace AutoFixtureDim;

internal static class SupportBlockCommand
{
	private const string RoughnessBlockName = "CadAider_国标粗糙度16下";

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
		string pluginDir = Path.GetDirectoryName(typeof(SupportBlockCommand).Assembly.Location);
		if (!string.IsNullOrEmpty(pluginDir))
		{
			config.AnnotationCaseStorePath = Path.Combine(pluginDir, "annotation-cases.json");
		}
		string groupId = DateTime.Now.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
		try
		{
			using (Transaction transaction = database.TransactionManager.StartTransaction())
			{
				Entity sourceEntity;
				Point3d startPoint;
				Point3d endPoint;
				if (!TrySelectSupportSource(editor, transaction, config, out sourceEntity, out startPoint, out endPoint))
				{
					return;
				}

				PromptKeywordOptions directionOptions = new PromptKeywordOptions("\n选择方向进行复制该直线 [左(A)/上(W)/右(D)/下(S)]: ");
				directionOptions.AllowNone = false;
				directionOptions.Keywords.Add("A");
				directionOptions.Keywords.Add("W");
				directionOptions.Keywords.Add("D");
				directionOptions.Keywords.Add("S");
				PromptResult directionResult = editor.GetKeywords(directionOptions);
				if (directionResult.Status != PromptStatus.OK)
				{
					return;
				}
				Vector3d direction = ResolveDirection(directionResult.StringResult);

				string annotationLayer = LayerManager.ResolveAnnotationLayer(database, transaction);
				double dimScale = (database.Dimscale <= 0.0) ? 1.0 : database.Dimscale;
				ObjectId dimStyleId = DimStyleManager.ResolveDimStyle(database, transaction);
				ObjectId diameterCalloutDimStyleId = DimStyleManager.ResolveDiameterCalloutDimStyle(database, transaction, dimStyleId);
				if (!HasRoughnessBlock(database, transaction))
				{
					editor.WriteMessage("\n未找到块 {0}，已取消托压块标注。", RoughnessBlockName);
					return;
				}
				if (!TryResolveDashedLinetype(database, transaction, out ObjectId dashedLinetypeId))
				{
					editor.WriteMessage("\n无法加载 DASHED 线型，已取消托压块标注。\n");
					return;
				}

				Vector3d segmentVector = endPoint - startPoint;
				bool horizontal = Math.Abs(segmentVector.X) >= Math.Abs(segmentVector.Y);
				double globalLinetypeScale = database.Ltscale;
				if (!TryGetVisibleDashPoint(transaction, dashedLinetypeId, startPoint + direction, endPoint + direction, sourceEntity.LinetypeScale, globalLinetypeScale, !horizontal, out Point3d cncArrowPoint)
					|| !TryGetVisibleDashPoint(transaction, dashedLinetypeId, startPoint + direction * 2.0, endPoint + direction * 2.0, sourceEntity.LinetypeScale, globalLinetypeScale, horizontal, out Point3d quenchArrowPoint))
				{
					editor.WriteMessage("\n无法确定 DASHED 可见实线段，已取消托压块标注。\n");
					return;
				}

				AnnotationMetadata.EnsureRegApp(database, transaction);
				BlockTableRecord space = (BlockTableRecord)transaction.GetObject(database.CurrentSpaceId, OpenMode.ForWrite);
				for (int offset = 1; offset <= 2; offset++)
				{
					Vector3d displacement = direction * offset;
					Line line = new Line(startPoint + displacement, endPoint + displacement);
					line.SetDatabaseDefaults(database);
					Commands.CopyEntityDisplayProperties(sourceEntity, line);
					line.LayerId = database.Clayer;
					line.Color = Color.FromColorIndex(ColorMethod.ByAci, 6);
					line.LinetypeId = dashedLinetypeId;
					space.AppendEntity(line);
					transaction.AddNewlyCreatedDBObject(line, add: true);
					AnnotationMetadata.Mark(line, groupId, AnnotationMetadata.KindDimension);
				}

				CadEntityWriter supportWriter = new CadEntityWriter(database, transaction, space, config, dimStyleId, dimScale, annotationLayer, groupId, AnnotationMetadata.KindDimension);
				CornerCalloutRenderer supportRenderer = new CornerCalloutRenderer(database, supportWriter, config, diameterCalloutDimStyleId, annotationLayer);
				double textHeight = supportWriter.GetDimStyleTextHeight(diameterCalloutDimStyleId);
				double leaderOffset = Math.Max(supportWriter.Scale(config.LeaderOffset), textHeight * 2.0);
				double labelSeparation = Math.Max(textHeight * 1.5, supportWriter.Scale(config.DimTextClearance));
				double landingGap = Math.Max(textHeight * 0.5, supportWriter.Scale(config.GeometryTolerance * 4.0));
				DimensionSide supportDimensionSide = ResolveSupportDimensionSide(startPoint, endPoint, direction);
				Point3d dimensionLineMidpoint = GetSupportDimensionLineMidpoint(startPoint, endPoint, supportDimensionSide, supportWriter.Scale(config.FirstDimOffset));
				Vector3d outward = GetSupportDimensionOutward(supportDimensionSide);
				bool cncFromMinimum = !horizontal;
				bool quenchFromMinimum = horizontal;
				Point3d cncTextPoint = GetSupportCalloutTextPoint(dimensionLineMidpoint, startPoint, endPoint, horizontal, outward, labelSeparation, leaderOffset, cncFromMinimum);
				Point3d quenchTextPoint = GetSupportCalloutTextPoint(dimensionLineMidpoint, startPoint, endPoint, horizontal, outward, labelSeparation, leaderOffset, quenchFromMinimum);
				Point3d cncLandingPoint = GetSupportCalloutLandingPoint(dimensionLineMidpoint, cncTextPoint, horizontal, outward, landingGap);
				Point3d quenchLandingPoint = GetSupportCalloutLandingPoint(dimensionLineMidpoint, quenchTextPoint, horizontal, outward, landingGap);
				Point3d cncEvaluatedLandingPoint = supportRenderer.AddLeader(cncArrowPoint, cncLandingPoint, cncTextPoint, "CNC加工");
				supportRenderer.AddLeader(quenchArrowPoint, quenchLandingPoint, quenchTextPoint, "淬火");
				Point3d cncTextMidpoint = Commands.GetAg1TextLandingMidpoint(cncTextPoint, cncArrowPoint, "CNC加工", textHeight);
				Point3d roughnessPoint = new Point3d(cncTextMidpoint.X, cncEvaluatedLandingPoint.Y, cncTextMidpoint.Z);
				NativeDiameterDimensioner.InsertRoughnessBlock(database, transaction, space, roughnessPoint, annotationLayer, diameterCalloutDimStyleId, groupId);
				transaction.Commit();
			}
			editor.WriteMessage("\nTY 已生成两条托压块线（偏移 1、2，洋红色，DASHED）及 CNC加工/淬火引线和粗糙度块。");
		}
		catch (Autodesk.AutoCAD.Runtime.Exception ex)
		{
			editor.WriteMessage("\nTY 取消或失败: {0}: {1}", ex.GetType().Name, ex.Message);
		}
		catch (System.Exception ex)
		{
			editor.WriteMessage("\nTY 发生异常: {0}: {1}", ex.GetType().Name, ex.Message);
		}
	}

	private static bool TrySelectSupportSource(Editor editor, Transaction transaction, DimensionRuleConfig config, out Entity sourceEntity, out Point3d startPoint, out Point3d endPoint)
	{
		sourceEntity = null;
		startPoint = Point3d.Origin;
		endPoint = Point3d.Origin;
		while (true)
		{
			PromptEntityResult entityResult = editor.GetEntity(new PromptEntityOptions("\n选择 DRAWING 图层中的独立直线: "));
			if (entityResult.Status != PromptStatus.OK)
			{
				return false;
			}
			Entity candidate = transaction.GetObject(entityResult.ObjectId, OpenMode.ForRead, openErased: false) as Entity;
			Line line = candidate as Line;
			if (line == null || !string.Equals(line.Layer, "DRAWING", StringComparison.OrdinalIgnoreCase))
			{
				editor.WriteMessage("\nTY 只能选择 DRAWING 图层中的独立 LINE。请重选。");
				continue;
			}
			if (line.StartPoint.DistanceTo(line.EndPoint) <= config.GeometryTolerance)
			{
				editor.WriteMessage("\n所选 LINE 长度无效，请重选。");
				continue;
			}
			sourceEntity = candidate;
			startPoint = line.StartPoint;
			endPoint = line.EndPoint;
			return true;
		}
	}

	private static Vector3d ResolveDirection(string keyword)
	{
		return (keyword ?? string.Empty).ToUpperInvariant() switch
		{
			"A" => new Vector3d(-1.0, 0.0, 0.0),
			"W" => new Vector3d(0.0, 1.0, 0.0),
			"D" => new Vector3d(1.0, 0.0, 0.0),
			_ => new Vector3d(0.0, -1.0, 0.0),
		};
	}

	private static bool HasRoughnessBlock(Database database, Transaction transaction)
	{
		BlockTable blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
		return blockTable.Has(RoughnessBlockName);
	}

	private static DimensionSide ResolveSupportDimensionSide(Point3d startPoint, Point3d endPoint, Vector3d copyDirection)
	{
		bool horizontal = Math.Abs(endPoint.X - startPoint.X) >= Math.Abs(endPoint.Y - startPoint.Y);
		return horizontal
			? (copyDirection.Y >= 0.0 ? DimensionSide.Top : DimensionSide.Bottom)
			: (copyDirection.X >= 0.0 ? DimensionSide.Right : DimensionSide.Left);
	}

	private static Point3d GetSupportDimensionLineMidpoint(Point3d startPoint, Point3d endPoint, DimensionSide side, double firstOffset)
	{
		bool horizontal = Math.Abs(endPoint.X - startPoint.X) >= Math.Abs(endPoint.Y - startPoint.Y);
		double axisMidpoint = horizontal ? (startPoint.X + endPoint.X) * 0.5 : (startPoint.Y + endPoint.Y) * 0.5;
		double crossMidpoint = horizontal ? (startPoint.Y + endPoint.Y) * 0.5 : (startPoint.X + endPoint.X) * 0.5;
		double coordinate = crossMidpoint + ((side == DimensionSide.Bottom || side == DimensionSide.Left) ? -firstOffset : firstOffset);
		return horizontal
			? new Point3d(axisMidpoint, coordinate, startPoint.Z)
			: new Point3d(coordinate, axisMidpoint, startPoint.Z);
	}

	private static Vector3d GetSupportDimensionOutward(DimensionSide side)
	{
		return side switch
		{
			DimensionSide.Bottom => new Vector3d(0.0, -1.0, 0.0),
			DimensionSide.Top => new Vector3d(0.0, 1.0, 0.0),
			DimensionSide.Left => new Vector3d(-1.0, 0.0, 0.0),
			_ => new Vector3d(1.0, 0.0, 0.0),
		};
	}

	private static bool TryGetVisibleDashPoint(Transaction transaction, ObjectId linetypeId, Point3d startPoint, Point3d endPoint, double objectLinetypeScale, double globalLinetypeScale, bool fromMinimum, out Point3d point)
	{
		point = Point3d.Origin;
		Vector3d vector = endPoint - startPoint;
		double length = vector.Length;
		if (transaction == null || linetypeId.IsNull || length <= 1E-9)
		{
			return false;
		}
		LinetypeTableRecord linetype = transaction.GetObject(linetypeId, OpenMode.ForRead, openErased: false) as LinetypeTableRecord;
		if (linetype == null || linetype.NumDashes <= 0)
		{
			return false;
		}
		double scale = Math.Abs(objectLinetypeScale) * Math.Abs(globalLinetypeScale);
		if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 1E-9)
		{
			scale = 1.0;
		}

		double patternLength = 0.0;
		bool hasVisibleDash = false;
		for (int index = 0; index < linetype.NumDashes; index++)
		{
			double dashLength;
			try
			{
				dashLength = linetype.DashLengthAt(index);
			}
			catch (Autodesk.AutoCAD.Runtime.Exception)
			{
				return false;
			}
			double scaledLength = Math.Abs(dashLength) * scale;
			if (double.IsNaN(scaledLength) || double.IsInfinity(scaledLength))
			{
				return false;
			}
			patternLength += scaledLength;
			if (dashLength > 1E-9 && scaledLength > 1E-9)
			{
				hasVisibleDash = true;
			}
		}
		if (!hasVisibleDash || patternLength <= 1E-9)
		{
			return false;
		}

		List<double> visibleCenters = new List<double>();
		double cursor = 0.0;
		int patternIndex = 0;
		int guard = 0;
		while (cursor < length - 1E-9 && guard++ < 100000)
		{
			double dashLength;
			try
			{
				dashLength = linetype.DashLengthAt(patternIndex);
			}
			catch (Autodesk.AutoCAD.Runtime.Exception)
			{
				return false;
			}
			patternIndex = (patternIndex + 1) % linetype.NumDashes;
			double scaledLength = Math.Abs(dashLength) * scale;
			if (scaledLength <= 1E-9)
			{
				continue;
			}
			double segmentEnd = Math.Min(length, cursor + scaledLength);
			if (dashLength > 1E-9 && segmentEnd - cursor > 1E-9)
			{
				visibleCenters.Add((cursor + segmentEnd) * 0.5);
			}
			cursor = segmentEnd;
		}
		if (visibleCenters.Count == 0 || (guard >= 100000 && cursor < length - 1E-9))
		{
			return false;
		}

		bool horizontal = Math.Abs(vector.X) >= Math.Abs(vector.Y);
		double bestDistance = fromMinimum ? double.MaxValue : double.MinValue;
		bool found = false;
		foreach (double centerDistance in visibleCenters)
		{
			Point3d candidate = startPoint + vector * (centerDistance / length);
			double axis = horizontal ? candidate.X : candidate.Y;
			if (!found || (fromMinimum ? axis < bestDistance : axis > bestDistance))
			{
				bestDistance = axis;
				point = candidate;
				found = true;
			}
		}
		return found;
	}

	private static Point3d GetSupportCalloutTextPoint(Point3d dimensionLineMidpoint, Point3d startPoint, Point3d endPoint, bool horizontal, Vector3d outward, double axisDistance, double outwardDistance, bool fromMinimum)
	{
		double startAxis = horizontal ? startPoint.X : startPoint.Y;
		double endAxis = horizontal ? endPoint.X : endPoint.Y;
		double minimum = Math.Min(startAxis, endAxis);
		double maximum = Math.Max(startAxis, endAxis);
		double axis = fromMinimum ? minimum - axisDistance : maximum + axisDistance;
		return horizontal
			? new Point3d(axis, dimensionLineMidpoint.Y + outward.Y * outwardDistance, dimensionLineMidpoint.Z)
			: new Point3d(dimensionLineMidpoint.X + outward.X * outwardDistance, axis, dimensionLineMidpoint.Z);
	}

	private static Point3d GetSupportCalloutLandingPoint(Point3d dimensionLineMidpoint, Point3d textPoint, bool horizontal, Vector3d outward, double landingGap)
	{
		if (horizontal)
		{
			double inward = (textPoint.X <= dimensionLineMidpoint.X) ? 1.0 : -1.0;
			return new Point3d(textPoint.X + inward * landingGap, textPoint.Y, textPoint.Z);
		}
		return new Point3d(dimensionLineMidpoint.X + outward.X * landingGap, textPoint.Y, textPoint.Z);
	}

	private static bool TryResolveDashedLinetype(Database database, Transaction transaction, out ObjectId linetypeId)
	{
		const string linetypeName = "DASHED";
		LinetypeTable table = (LinetypeTable)transaction.GetObject(database.LinetypeTableId, OpenMode.ForRead);
		if (table.Has(linetypeName))
		{
			linetypeId = table[linetypeName];
			return true;
		}
		bool metric = false;
		try
		{
			metric = Convert.ToInt32(Application.GetSystemVariable("MEASUREMENT"), CultureInfo.InvariantCulture) != 0;
		}
		catch (System.Exception)
		{
		}
		string[] files = metric ? new string[2] { "acadiso.lin", "acad.lin" } : new string[2] { "acad.lin", "acadiso.lin" };
		foreach (string file in files)
		{
			try
			{
				database.LoadLineTypeFile(linetypeName, file);
			}
			catch (Autodesk.AutoCAD.Runtime.Exception)
			{
			}
			if (table.Has(linetypeName))
			{
				linetypeId = table[linetypeName];
				return true;
			}
		}
		linetypeId = ObjectId.Null;
		return false;
	}
}
