using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using CadAuto.CadAdapter.Environment;
using CadAuto.CadAdapter.Model;
using CadAuto.Core.Model;
using CadAuto.Core.Planning;
using CadAuto.Core.Rules;

namespace CadAuto.CadAdapter.Rendering;

public static class NativeDiameterDimensioner
{
	private enum DiameterJigResult
	{
		Picked,
		Skip,
		Cancel
	}

	private sealed class DiameterCalloutJig : DrawJig
	{
		private readonly Point3d _center;

		private readonly double _radius;

		private readonly double _textHeight;

		private readonly string _calloutText;

		private readonly string _previewText;

		private readonly double _redrawTolerance;

		private readonly double _textWidth;

		private Point3d _textPoint;

		public Point3d TextPoint => _textPoint;

		public bool SkipRequested { get; private set; }

		public DiameterCalloutJig(Point3d center, double radius, double textHeight, string calloutText)
		{
			_center = center;
			_radius = radius;
			_textHeight = textHeight;
			_calloutText = calloutText ?? string.Empty;
			_previewText = _calloutText.Replace("%%c", "D");
			_redrawTolerance = Math.Max(textHeight * 0.45, radius * 0.08);
			_textWidth = (double)Math.Max(_previewText.Length, 2) * _textHeight * 0.65;
			_textPoint = center + new Vector3d(radius * 3.0, radius * 3.0, 0.0);
		}

		protected override SamplerStatus Sampler(JigPrompts prompts)
		{
			JigPromptPointOptions jigPromptPointOptions = new JigPromptPointOptions("\nPick point: ");
			jigPromptPointOptions.UserInputControls = UserInputControls.NullResponseAccepted | UserInputControls.Accept3dCoordinates;
			PromptPointResult promptPointResult = prompts.AcquirePoint(jigPromptPointOptions);
			if (promptPointResult.Status == PromptStatus.None)
			{
				SkipRequested = true;
				return SamplerStatus.Cancel;
			}
			if (promptPointResult.Status != PromptStatus.OK)
			{
				return SamplerStatus.Cancel;
			}
			if (promptPointResult.Value.DistanceTo(_textPoint) <= _redrawTolerance)
			{
				return SamplerStatus.NoChange;
			}
			_textPoint = promptPointResult.Value;
			return SamplerStatus.OK;
		}

		protected override bool WorldDraw(WorldDraw draw)
		{
			Vector3d vector3d = _textPoint - _center;
			if (vector3d.Length <= 1E-09)
			{
				vector3d = Vector3d.XAxis;
			}
			vector3d = vector3d.GetNormal();
			Point3d startPoint = _center + vector3d * _radius;
			Vector3d xAxis = Vector3d.XAxis;
			Vector3d zAxis = Vector3d.ZAxis;
			Point3d position = ((_textPoint.X < startPoint.X) ? (_textPoint - xAxis * _textWidth) : _textPoint);
			draw.Geometry.WorldLine(startPoint, _textPoint);
			draw.Geometry.Text(position, zAxis, xAxis, _textHeight, 1.0, 0.0, _previewText);
			return true;
		}

		private void DrawArrowHead(WorldDraw draw, Point3d arrowPoint, Vector3d direction)
		{
			double num = Math.Max(_textHeight * 1.2, _radius * 0.35);
			Vector3d vector3d = -direction;
			Vector3d normal = new Vector3d(0.0 - direction.Y, direction.X, 0.0).GetNormal();
			Point3d endPoint = arrowPoint + (vector3d + normal * 0.45).GetNormal() * num;
			Point3d endPoint2 = arrowPoint + (vector3d - normal * 0.45).GetNormal() * num;
			draw.Geometry.WorldLine(arrowPoint, endPoint);
			draw.Geometry.WorldLine(arrowPoint, endPoint2);
		}
	}

	private static readonly string PinRoughnessBlockName = "CadAider_国标粗糙度16下";

	public static void PromptDiameterDimensions(Document document, IList<IList<HoleFeature>> diameterGroups, DimensionRuleConfig config, string annotationLayer, ObjectId diameterDimStyleId, string groupId)
	{
		if (document == null || diameterGroups == null || diameterGroups.Count == 0)
		{
			return;
		}
		Database database = document.Database;
		Editor editor = document.Editor;
		foreach (IList<HoleFeature> item in diameterGroups.Where((IList<HoleFeature> g) => g.Count > 0))
		{
			List<HoleFeature> list = (from h in item
				orderby h.Center.Y, h.Center.X
				select h).ToList();
			HoleFeature holeFeature = list[0];
			string text = (holeFeature.IsThreadHole ? config.FormatThreadCallout(holeFeature.Diameter, list.Count, holeFeature.ThreadCallout) : config.FormatHoleCallout(holeFeature.Diameter, list.Count, holeFeature.FitTolerance));
			Point3d textPoint;
			switch (PromptCalloutPointWithJig(database, editor, holeFeature, annotationLayer, diameterDimStyleId, text, out textPoint))
			{
			case DiameterJigResult.Skip:
				editor.WriteMessage("\nSkipped hole callout {0}.", text);
				break;
			case DiameterJigResult.Cancel:
				editor.WriteMessage("\nHole callout placement canceled.");
				return;
			default:
				CreateCalloutDimension(database, holeFeature, textPoint, annotationLayer, diameterDimStyleId, text, groupId);
				break;
			}
		}
	}

	public static void PromptHoleCalloutPlans(Document document, IList<HoleCalloutPlan> calloutPlans, IList<HoleFeature> sourceHoles, DimensionRuleConfig config, string annotationLayer, ObjectId diameterDimStyleId, string groupId)
	{
		if (document == null || calloutPlans == null || calloutPlans.Count == 0 || sourceHoles == null)
		{
			return;
		}
		Database database = document.Database;
		Editor editor = document.Editor;
		foreach (HoleCalloutPlan item in calloutPlans.Where((HoleCalloutPlan p) => p != null && p.Holes.Count > 0))
		{
			HoleFeature holeFeature = FindRepresentativeHole(item, sourceHoles, config);
			if (holeFeature != null)
			{
				string text = item.Text ?? string.Empty;
				Point3d textPoint;
				switch (PromptCalloutPointWithJig(database, editor, holeFeature, annotationLayer, diameterDimStyleId, text, out textPoint))
				{
				case DiameterJigResult.Skip:
					editor.WriteMessage("\nSkipped hole callout {0}.", text);
					break;
				case DiameterJigResult.Cancel:
					editor.WriteMessage("\nHole callout placement canceled.");
					return;
				default:
					CreateCalloutDimension(database, holeFeature, textPoint, annotationLayer, diameterDimStyleId, text, groupId);
					break;
				}
			}
		}
	}

	private static HoleFeature FindRepresentativeHole(HoleCalloutPlan plan, IList<HoleFeature> sourceHoles, DimensionRuleConfig config)
	{
		foreach (HoleFeature2D coreHole in from h in plan.Holes
			orderby h.Center.Y, h.Center.X
			select h)
		{
			HoleFeature holeFeature = sourceHoles.FirstOrDefault((HoleFeature h) => IsSameHoleForCallout(coreHole, h, config));
			if (holeFeature != null)
			{
				return holeFeature;
			}
		}
		return null;
	}

	private static bool IsSameHoleForCallout(HoleFeature2D coreHole, HoleFeature sourceHole, DimensionRuleConfig config)
	{
		if (coreHole == null || sourceHole == null)
		{
			return false;
		}
		double num = coreHole.Center.X - sourceHole.Center.X;
		double num2 = coreHole.Center.Y - sourceHole.Center.Y;
		return num * num + num2 * num2 <= config.GeometryTolerance * config.GeometryTolerance && Math.Abs(coreHole.Diameter - sourceHole.Diameter) <= config.GeometryTolerance;
	}

	private static DiameterJigResult PromptCalloutPointWithJig(Database db, Editor editor, HoleFeature hole, string annotationLayer, ObjectId dimStyleId, string calloutText, out Point3d textPoint)
	{
		textPoint = Point3d.Origin;
		Point3d center;
		double radius;
		double dimStyleTextHeight;
		using (Transaction transaction = db.TransactionManager.StartTransaction())
		{
			if (!TryGetHoleGeometry(transaction, hole, out center, out radius))
			{
				transaction.Commit();
				return DiameterJigResult.Skip;
			}
			dimStyleTextHeight = GetDimStyleTextHeight(db, transaction, dimStyleId);
			transaction.Commit();
		}
		editor.WriteMessage("\nMove hole callout {0}, click to place, Enter to skip.", calloutText);
		DiameterCalloutJig diameterCalloutJig = new DiameterCalloutJig(center, radius, dimStyleTextHeight, calloutText);
		PromptResult promptResult = editor.Drag(diameterCalloutJig);
		if (promptResult.Status == PromptStatus.OK)
		{
			textPoint = diameterCalloutJig.TextPoint;
			return DiameterJigResult.Picked;
		}
		if (diameterCalloutJig.SkipRequested || promptResult.Status == PromptStatus.None)
		{
			return DiameterJigResult.Skip;
		}
		return DiameterJigResult.Cancel;
	}

	private static void CreateCalloutDimension(Database db, HoleFeature hole, Point3d textPoint, string annotationLayer, ObjectId dimStyleId, string calloutText, string groupId)
	{
		using Transaction transaction = db.TransactionManager.StartTransaction();
		AnnotationMetadata.EnsureRegApp(db, transaction);
		Dimension dimension = BuildCalloutDimension(db, transaction, hole, textPoint, annotationLayer, dimStyleId, calloutText) as Dimension;
		if (dimension == null)
		{
			transaction.Commit();
			return;
		}
		BlockTableRecord blockTableRecord = (BlockTableRecord)transaction.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
		blockTableRecord.AppendEntity(dimension);
		transaction.AddNewlyCreatedDBObject(dimension, add: true);
		dimension.UsingDefaultTextPosition = false;
		dimension.TextPosition = textPoint;
		dimension.RecomputeDimensionBlock(forceUpdate: true);
		AnnotationMetadata.Mark(dimension, groupId, AnnotationMetadata.KindDimension);
		if (hole.IsPinHole)
		{
			InsertPinRoughnessBlock(db, transaction, blockTableRecord, hole, dimension.TextPosition, annotationLayer, dimStyleId, calloutText, groupId);
		}
		transaction.Commit();
	}

	private static void InsertPinRoughnessBlock(Database db, Transaction tr, BlockTableRecord space, HoleFeature hole, Point3d textPoint, string annotationLayer, ObjectId dimStyleId, string calloutText, string groupId)
	{
		BlockTable blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
		if (blockTable.Has(PinRoughnessBlockName))
		{
			BlockReference blockReference = new BlockReference(textPoint, blockTable[PinRoughnessBlockName]);
			blockReference.SetDatabaseDefaults(db);
			blockReference.Layer = annotationLayer;
			double dimStyleGlobalScale = GetDimStyleGlobalScale(db, tr, dimStyleId);
			blockReference.ScaleFactors = new Scale3d(dimStyleGlobalScale);
			blockReference.Rotation = Math.PI;
			space.AppendEntity(blockReference);
			tr.AddNewlyCreatedDBObject(blockReference, add: true);
			AnnotationMetadata.Mark(blockReference, groupId, AnnotationMetadata.KindDimension);
		}
	}

	private static Entity BuildCalloutDimension(Database db, Transaction tr, HoleFeature hole, Point3d textPoint, string annotationLayer, ObjectId dimStyleId, string calloutText)
	{
		if (!TryGetHoleGeometry(tr, hole, out var center, out var radius))
		{
			return null;
		}
		return BuildCalloutDimension(db, center, radius, textPoint, annotationLayer, dimStyleId, calloutText);
	}

	private static DiametricDimension BuildCalloutDimension(Database db, Point3d center, double radius, Point3d textPoint, string annotationLayer, ObjectId dimStyleId, string calloutText)
	{
		Vector3d vector3d = textPoint - center;
		if (vector3d.Length <= 1E-09)
		{
			vector3d = Vector3d.XAxis;
		}
		vector3d = vector3d.GetNormal();
		Point3d chordPoint = center + vector3d * radius;
		Point3d farChordPoint = center - vector3d * radius;
		double leaderLength = chordPoint.DistanceTo(textPoint);
		DiametricDimension diametricDimension = new DiametricDimension(chordPoint, farChordPoint, leaderLength, calloutText, dimStyleId);
		diametricDimension.SetDatabaseDefaults(db);
		diametricDimension.Layer = annotationLayer;
		diametricDimension.DimensionStyle = dimStyleId;
		DimStyleManager.ApplyGeneratedDimensionColors(diametricDimension);
		diametricDimension.DimensionText = calloutText;
		return diametricDimension;
	}

	private static bool TryGetHoleGeometry(Transaction tr, HoleFeature hole, out Point3d center, out double radius)
	{
		center = Point3d.Origin;
		radius = 0.0;
		ObjectId id = ((!hole.SourceId.IsNull) ? hole.SourceId : hole.CircleId);
		if (id.IsNull)
		{
			return false;
		}
		Entity entity = tr.GetObject(id, OpenMode.ForRead) as Entity;
		Arc arc = entity as Arc;
		if (arc != null)
		{
			center = arc.Center;
			radius = arc.Radius;
			return true;
		}
		Circle circle = entity as Circle;
		if (circle != null)
		{
			center = circle.Center;
			radius = circle.Radius;
			return true;
		}
		return false;
	}

	private static double GetDimStyleTextHeight(Database db, Transaction tr, ObjectId dimStyleId)
	{
		double num = ((db.Dimscale <= 0.0) ? 1.0 : db.Dimscale);
		DimStyleTableRecord dimStyleTableRecord = tr.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
		if (dimStyleTableRecord != null && dimStyleTableRecord.Dimtxt > 1E-09)
		{
			return dimStyleTableRecord.Dimtxt * num;
		}
		return 2.5 * num;
	}

	private static double GetDimStyleGlobalScale(Database db, Transaction tr, ObjectId dimStyleId)
	{
		DimStyleTableRecord dimStyleTableRecord = tr.GetObject(dimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
		if (dimStyleTableRecord != null && dimStyleTableRecord.Dimscale > 1E-09)
		{
			return dimStyleTableRecord.Dimscale;
		}
		return (db.Dimscale <= 0.0) ? 1.0 : db.Dimscale;
	}

	private static double EstimateCalloutTextWidth(string calloutText, double textHeight)
	{
		string text = (calloutText ?? string.Empty).Replace("%%c", "O");
		return (double)Math.Max(text.Length, 2) * textHeight * 0.65;
	}
}
