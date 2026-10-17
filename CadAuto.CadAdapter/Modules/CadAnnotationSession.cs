using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using CadAuto.CadAdapter.Model;
using CadAuto.CadAdapter.Rendering;
using CadAuto.Core.Geometry;
using CadAuto.Core.Planning.Modules;
using CadAuto.Core.Rules;

namespace CadAuto.CadAdapter.Modules;

public sealed class CadAnnotationSession
{
	public AnnotationSession Core { get; set; }

	public Document Document { get; set; }

	public Database Database { get; set; }

	public Editor Editor { get; set; }

	public Transaction Transaction { get; set; }

	public DimensionDrawer Drawer { get; set; }

	public OutlineFeature Outline { get; set; }

	public DatumDefinition Datum { get; set; }

	public IList<HoleFeature> Holes { get; set; }

	public IList<SlotFeature> Slots { get; set; }

	public IList<Segment2D> Centerlines { get; set; }

	public DimensionRuleConfig Config { get; set; }

	public ObjectId DimStyleId { get; set; }

	public ObjectId DiameterCalloutDimStyleId { get; set; }

	public double DimScale { get; set; }

	public string AnnotationLayer { get; set; }

	public string GroupId { get; set; }
}
