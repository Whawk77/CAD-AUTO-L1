using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadAuto.CadAdapter;
using CadAuto.CadAdapter.Model;
using CadAuto.CadAdapter.Rendering;
using CadAuto.Core.Planning;
using CadAuto.Core.Rules;

namespace CadAuto.CadAdapter.Rendering
{
    public enum DiagnosticDimensionSide
    {
        All,
        Bottom,
        Top,
        Left,
        Right
    }

    public sealed partial class DimensionDrawer
    {
        private readonly Database _db;
        private readonly Transaction _tr;
        private readonly BlockTableRecord _space;
        private readonly DimensionRuleConfig _config;
        private readonly ObjectId _dimStyleId;
        private readonly ObjectId _diameterCalloutDimStyleId;
        private readonly double _dimScale;
        private readonly string _annotationLayer;
        private readonly bool _appendToDatabase;
        private readonly string _groupId;
        private readonly bool _diagnosticsEnabled;
        private readonly DiagnosticDimensionSide _diagnosticSide;
        private readonly CadEntityWriter _entityWriter;
        private readonly CornerCalloutRenderer _cornerCalloutRenderer;
        private readonly HoleDiameterLeaderRenderer _holeDiameterLeaderRenderer;
        private readonly DebugAnnotationRenderer _debugAnnotationRenderer;
        private readonly DimensionExtensionLineRenderer _extensionLineRenderer;
        private readonly DimensionPlanCadMapper _dimensionPlanMapper = new DimensionPlanCadMapper();
        private readonly IList<Entity> _previewEntities = new List<Entity>();
        private readonly List<DeferredDim> _bottomDims = new List<DeferredDim>();
        private readonly List<DeferredDim> _topDims = new List<DeferredDim>();
        private readonly List<DeferredDim> _leftDims = new List<DeferredDim>();
        private readonly List<DeferredDim> _rightDims = new List<DeferredDim>();
        private readonly List<TextBounds> _linearDimTextObstacles = new List<TextBounds>();
        private int _nextLooseChainId = 1;

        private enum DimSide { Bottom, Top, Left, Right }

        private struct DeferredDim
        {
            public double Rotation;
            public Point3d XLine1;
            public Point3d XLine2;
            public string OverrideText;
            public double Span;
            public DimensionType DimType;
            public bool UseSegmentedExtensionLines;
            public bool ForceOuterLevel;
            public int LooseChainId;
            public bool PreferLocalBoundary;
            public string DebugOwner;
            public string DebugRole;
            public int DebugIndex;
        }

        private struct PlacedDim
        {
            public DeferredDim Dim;
            public DimSide Side;
            public Point3d DimLinePoint;
            public TextBounds TextBounds;
            public bool HasCustomTextPosition;
            public Point3d TextPosition;
            public bool UsesLocalBoundary;
        }

        private struct TextBounds
        {
            public double MinX;
            public double MaxX;
            public double MinY;
            public double MaxY;
        }

        private struct StructurePoint
        {
            public Point2d Point;
            public string Source;
        }

        private struct IgnoredPoint
        {
            public Point2d Point;
            public string Reason;
        }

        private struct PointDebugLabel
        {
            public Point2d Point;
            public string Label;
            public short ColorIndex;
        }

        private struct PointDebugLabelPlacement
        {
            public TextBounds Bounds;
        }

        private sealed class PinGroupPlan
        {
            public int GroupIndex { get; set; }
            public HoleFeature BasePin { get; set; }
            public List<HoleFeature> Pins { get; } = new List<HoleFeature>();
            public List<HoleFeature> MemberHoles { get; } = new List<HoleFeature>();
            public DimSide HorizontalSide { get; set; } = DimSide.Bottom;
            public DimSide VerticalSide { get; set; } = DimSide.Left;
        }

        private sealed class FunctionalHoleGroupPlan
        {
            public PinGroupPlan PinGroup { get; set; }
            public List<HoleFeature> Holes { get; } = new List<HoleFeature>();
        }

        private sealed class LooseHoleLineGroup
        {
            public bool Horizontal { get; set; }
            public string SpecKey { get; set; }
            public List<HoleFeature> Holes { get; } = new List<HoleFeature>();
        }

        private sealed class LooseHoleMacroGroup
        {
            public List<LooseHoleLineGroup> LineGroups { get; } = new List<LooseHoleLineGroup>();
            public List<HoleFeature> Holes { get; } = new List<HoleFeature>();
        }

        private sealed class LooseHoleLocationPlan
        {
            public LooseHoleMacroGroup MacroGroup { get; set; }
            public PinGroupPlan ReferencePinGroup { get; set; }
            public HoleFeature AnchorHole { get; set; }
        }

        public DimensionDrawer(
            Database db,
            Transaction tr,
            BlockTableRecord space,
            DimensionRuleConfig config,
            ObjectId dimStyleId,
            ObjectId diameterCalloutDimStyleId,
            double dimScale,
            string annotationLayer,
            bool appendToDatabase,
            string groupId,
            bool diagnosticsEnabled = false,
            DiagnosticDimensionSide diagnosticSide = DiagnosticDimensionSide.All)
        {
            _db = db;
            _tr = tr;
            _space = space;
            _config = config;
            _dimStyleId = dimStyleId;
            _diameterCalloutDimStyleId = diameterCalloutDimStyleId;
            _dimScale = dimScale <= 0.0 ? 1.0 : dimScale;
            _annotationLayer = annotationLayer;
            _appendToDatabase = appendToDatabase;
            _groupId = groupId;
            _diagnosticsEnabled = diagnosticsEnabled;
            _diagnosticSide = diagnosticSide;
            _entityWriter = new CadEntityWriter(
                db,
                tr,
                space,
                config,
                dimStyleId,
                _dimScale,
                annotationLayer,
                appendToDatabase,
                groupId,
                _previewEntities);
            _cornerCalloutRenderer = new CornerCalloutRenderer(
                db,
                _entityWriter,
                config,
                diameterCalloutDimStyleId,
                appendToDatabase,
                annotationLayer);
            _holeDiameterLeaderRenderer = new HoleDiameterLeaderRenderer(
                db,
                _entityWriter,
                config,
                diameterCalloutDimStyleId,
                appendToDatabase,
                annotationLayer);
            _debugAnnotationRenderer = new DebugAnnotationRenderer(
                db,
                _entityWriter,
                _entityWriter.GetDimStyleTextStyle(dimStyleId),
                annotationLayer);
            _extensionLineRenderer = new DimensionExtensionLineRenderer(
                db,
                _entityWriter,
                annotationLayer);
        }

        public IList<Entity> PreviewEntities
        {
            get { return _previewEntities; }
        }

        public void DrawDimensionPlan(DimensionPlan plan)
        {
            if (plan == null)
            {
                return;
            }

            foreach (var dimension in _dimensionPlanMapper.Map(plan))
            {
                AddPlannedDimension(dimension);
            }
        }

        private void AddPlannedDimension(DimensionPlanCadItem dimension)
        {
            if (dimension == null)
            {
                return;
            }

            var dim = new DeferredDim
            {
                Rotation = dimension.Rotation,
                XLine1 = dimension.FirstPoint,
                XLine2 = dimension.SecondPoint,
                OverrideText = dimension.OverrideText ?? string.Empty,
                Span = dimension.Span,
                DimType = dimension.DimensionType,
                ForceOuterLevel = dimension.ForceOuterLevel,
                UseSegmentedExtensionLines = dimension.UseSegmentedExtensionLines,
                LooseChainId = dimension.ChainId,
                PreferLocalBoundary = dimension.PreferLocalBoundary,
                DebugOwner = dimension.DebugOwner,
                DebugRole = dimension.DebugRole
            };

            AddPlannedDimensionToSide(dim, dimension.Side);
        }

        private void AddPlannedDimensionToSide(DeferredDim dim, DimensionSide side)
        {
            switch (side)
            {
                case DimensionSide.Top:
                    _topDims.Add(dim);
                    break;
                case DimensionSide.Left:
                    if (dim.Rotation == Math.PI / 2.0)
                    {
                        var verticalSide = ChooseVerticalNormalDimensionSide(dim, DimSide.Left);
                        if (verticalSide == DimSide.Right)
                        {
                            _rightDims.Add(dim);
                            break;
                        }
                    }

                    _leftDims.Add(dim);
                    break;
                case DimensionSide.Right:
                    if (dim.Rotation == Math.PI / 2.0)
                    {
                        var verticalSide = ChooseVerticalNormalDimensionSide(dim, DimSide.Right);
                        if (verticalSide == DimSide.Left)
                        {
                            _leftDims.Add(dim);
                            break;
                        }
                    }

                    _rightDims.Add(dim);
                    break;
                default:
                    _bottomDims.Add(dim);
                    break;
            }
        }

        private DimSide ChooseHorizontalHoleSide(OutlineFeature outline, Point3d point)
        {
            var bottomDistance = Math.Abs(point.Y - outline.MinY);
            var topDistance = Math.Abs(outline.MaxY - point.Y);
            return bottomDistance <= topDistance ? DimSide.Bottom : DimSide.Top;
        }

        private DimSide ChooseVerticalHoleSide(OutlineFeature outline, Point3d point)
        {
            var leftDistance = Math.Abs(point.X - outline.MinX);
            var rightDistance = Math.Abs(outline.MaxX - point.X);
            return leftDistance <= rightDistance ? DimSide.Left : DimSide.Right;
        }

        private void DrawNonPinHolesFromOutlineEdge(OutlineFeature outline, DatumDefinition datum, IList<HoleFeature> holes)
        {
            foreach (var hole in holes.Where(h => !h.IsPinHole && !h.IsSlotPoint))
            {
                AddHorizontalDimFromXToSide(datum.BaseX, hole.Center, string.Empty, DimensionType.HoleLocation, ChooseHorizontalHoleSide(outline, hole.Center));
                AddVerticalDimFromYToSide(datum.BaseY, hole.Center, string.Empty, DimensionType.HoleLocation, ChooseVerticalHoleSide(outline, hole.Center));
            }
        }

        private void AddRotatedDimension(
            double rotation,
            Point3d xLine1,
            Point3d xLine2,
            Point3d dimLinePoint,
            string overrideText,
            bool useSegmentedExtensionLines,
            bool useCustomTextPosition = false,
            Point3d customTextPosition = default(Point3d))
        {
            _entityWriter.AddRotatedDimension(
                rotation,
                xLine1,
                xLine2,
                dimLinePoint,
                overrideText,
                useSegmentedExtensionLines,
                useCustomTextPosition,
                customTextPosition);
        }

        private ObjectId GetDimStyleTextStyle(ObjectId dimStyleId)
        {
            return _entityWriter.GetDimStyleTextStyle(dimStyleId);
        }

        private string GetCurrentLayerName()
        {
            return _entityWriter.GetCurrentLayerName();
        }

        private Autodesk.AutoCAD.Colors.Color GetCurrentEntityColor()
        {
            return _entityWriter.GetCurrentEntityColor();
        }

        private Autodesk.AutoCAD.Colors.Color GetDimStyleTextColor(ObjectId dimStyleId)
        {
            return _entityWriter.GetDimStyleTextColor(dimStyleId);
        }

        private double GetDimStyleTextHeight(ObjectId dimStyleId)
        {
            return _entityWriter.GetDimStyleTextHeight(dimStyleId);
        }

        private int GetDimStyleLinearPrecision(ObjectId dimStyleId)
        {
            return _entityWriter.GetDimStyleLinearPrecision(dimStyleId);
        }

        private static string FormatNumberWithPrecision(double value, int precision)
        {
            var rounded = Math.Round(value, precision);
            var roundedInteger = Math.Round(rounded);
            if (Math.Abs(rounded - roundedInteger) <= 1e-9)
            {
                return roundedInteger.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            }

            var format = precision <= 0 ? "0" : "0." + new string('0', precision);
            return rounded.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        }

        private void Append(Entity entity)
        {
            _entityWriter.Append(entity);
        }

        private double Scale(double value)
        {
            return _entityWriter.Scale(value);
        }

    }
}

