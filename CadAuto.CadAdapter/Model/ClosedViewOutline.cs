using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadAuto.CadAdapter.Model;

public sealed class ClosedViewOutline
{
	public ObjectId PolylineId { get; set; } = ObjectId.Null;

	public string ViewId { get; set; }

	public List<ObjectId> EntityIds { get; } = new List<ObjectId>();

	public double MinX { get; set; }

	public double MinY { get; set; }

	public double MaxX { get; set; }

	public double MaxY { get; set; }

	public List<ObjectId> SelectedIds { get; } = new List<ObjectId>();
}
