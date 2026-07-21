using System;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using CadAuto.Core.Rules;

namespace CadAuto.CadAdapter.Rendering;

public sealed class CornerCalloutRenderer
{
	private sealed class CornerFeatureLeaderJig : DrawJig
	{
		private readonly Point3d _arrowPoint;

		private readonly string _text;

		private readonly double _textHeight;

		private readonly double _redrawTolerance;

		private readonly double _textWidth;

		private readonly string _promptMessage;

		private Point3d _textPoint;

		public Point3d TextPoint => _textPoint;

		public bool SkipRequested { get; private set; }

		public CornerFeatureLeaderJig(Point3d arrowPoint, string text, double textHeight)
		{
			_arrowPoint = arrowPoint;
			_text = text ?? string.Empty;
			_textHeight = textHeight;
			_redrawTolerance = Math.Max(textHeight * 0.18, 1E-06);
			_textWidth = (double)Math.Max(_text.Length, 2) * _textHeight * 0.65;
			_promptMessage = "\nMove corner callout " + _text + ", click to place, Enter to skip: ";
			_textPoint = arrowPoint + new Vector3d(textHeight * 6.0, textHeight * 4.0, 0.0);
		}

		protected override SamplerStatus Sampler(JigPrompts prompts)
		{
			JigPromptPointOptions jigPromptPointOptions = new JigPromptPointOptions(_promptMessage);
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
			if (draw == null)
			{
				return false;
			}
			Vector3d vector3d = _textPoint - _arrowPoint;
			if (vector3d.Length <= 1E-09)
			{
				vector3d = Vector3d.XAxis;
			}
			vector3d = vector3d.GetNormal();
			Vector3d zAxis = Vector3d.ZAxis;
			Vector3d xAxis = Vector3d.XAxis;
			Point3d position = ((_textPoint.X < _arrowPoint.X) ? (_textPoint - xAxis * _textWidth) : _textPoint);
			draw.Geometry.WorldLine(_arrowPoint, _textPoint);
			draw.Geometry.Text(position, zAxis, xAxis, _textHeight, 1.0, 0.0, _text);
			return true;
		}
	}

	private readonly Database _database;

	private readonly CadEntityWriter _writer;

	private readonly DimensionRuleConfig _config;

	private readonly ObjectId _diameterCalloutDimStyleId;

	private readonly string _annotationLayer;

	public CornerCalloutRenderer(Database database, CadEntityWriter writer, DimensionRuleConfig config, ObjectId diameterCalloutDimStyleId, string annotationLayer)
	{
		_database = database;
		_writer = writer;
		_config = config;
		_diameterCalloutDimStyleId = diameterCalloutDimStyleId;
		_annotationLayer = annotationLayer;
	}

	public CornerCalloutJigResult PromptLeaderPoint(Editor editor, Point2d target, string text, double textHeight, out Point3d textPoint)
	{
		textPoint = Point3d.Origin;
		Point3d arrowPoint = new Point3d(target.X, target.Y, 0.0);
		CornerFeatureLeaderJig cornerFeatureLeaderJig = new CornerFeatureLeaderJig(arrowPoint, text, textHeight);
		PromptResult promptResult = editor.Drag(cornerFeatureLeaderJig);
		if (promptResult.Status == PromptStatus.OK)
		{
			textPoint = cornerFeatureLeaderJig.TextPoint;
			return CornerCalloutJigResult.Picked;
		}
		if (cornerFeatureLeaderJig.SkipRequested || promptResult.Status == PromptStatus.None)
		{
			return CornerCalloutJigResult.Skip;
		}
		return CornerCalloutJigResult.Cancel;
	}

	public void AddLeader(Point3d arrowPoint, Point3d landingPoint, Point3d textPoint, string text)
	{
		MText mText = new MText();
		mText.SetDatabaseDefaults(_database);
		mText.Contents = text;
		mText.TextHeight = _writer.GetDimStyleTextHeight(_diameterCalloutDimStyleId);
		mText.TextStyleId = _writer.GetDimStyleTextStyle(_diameterCalloutDimStyleId);
		mText.Location = textPoint;
		bool flag = textPoint.X < landingPoint.X - _config.GeometryTolerance || (Math.Abs(textPoint.X - landingPoint.X) <= _config.GeometryTolerance && textPoint.X < arrowPoint.X);
		mText.Attachment = (flag ? AttachmentPoint.MiddleRight : AttachmentPoint.MiddleLeft);
		mText.Layer = _annotationLayer;
		mText.Color = _writer.GetDimStyleTextColor(_diameterCalloutDimStyleId);
		_writer.Append(mText);
		Leader leader = new Leader();
		leader.SetDatabaseDefaults(_database);
		leader.Layer = _writer.GetCurrentLayerName();
		leader.DimensionStyle = _diameterCalloutDimStyleId;
		leader.Color = _writer.GetCurrentEntityColor();
		leader.AppendVertex(arrowPoint);
		leader.AppendVertex(landingPoint);
		_writer.Append(leader);
		leader.Annotation = mText.ObjectId;
		leader.EvaluateLeader();
		leader.Color = _writer.GetCurrentEntityColor();
	}

	public void AddRadialDimension(Point3d centerPoint, Point3d chordPoint, Point3d textPoint, double minimumLeaderLength, string text, bool useCustomTextPosition)
	{
		double leaderLength = Math.Max(chordPoint.DistanceTo(textPoint), minimumLeaderLength);
		RadialDimension radialDimension = new RadialDimension(centerPoint, chordPoint, leaderLength, text, _diameterCalloutDimStyleId);
		radialDimension.SetDatabaseDefaults(_database);
		radialDimension.Layer = _annotationLayer;
		radialDimension.DimensionStyle = _diameterCalloutDimStyleId;
		radialDimension.DimensionText = text;
		if (useCustomTextPosition)
		{
			radialDimension.UsingDefaultTextPosition = false;
			radialDimension.TextPosition = textPoint;
		}
		_writer.Append(radialDimension);
		if (useCustomTextPosition)
		{
			radialDimension.RecomputeDimensionBlock(forceUpdate: true);
		}
	}
}
