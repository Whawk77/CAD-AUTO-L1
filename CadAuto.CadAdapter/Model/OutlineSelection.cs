using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadAuto.CadAdapter.Model;

public sealed class OutlineSelection
{
	public ObjectId PrimaryPolylineId { get; set; } = ObjectId.Null;

	public List<ObjectId> EntityIds { get; } = new List<ObjectId>();

	public List<ObjectId> SelectedIds { get; } = new List<ObjectId>();

	public bool HasPrimaryPolyline => !PrimaryPolylineId.IsNull;
}
