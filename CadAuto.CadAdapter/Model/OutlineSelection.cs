using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadAuto.CadAdapter.Model;

public sealed class OutlineSelection
{
	public ObjectId PrimaryPolylineId { get; set; } = ObjectId.Null;

	public List<ObjectId> EntityIds { get; } = new List<ObjectId>();

	public List<ObjectId> SelectedIds { get; } = new List<ObjectId>();

	// 外轮廓选择的判定记录（每个线组一条），写入诊断报告 selection.outlineSelectionDecisions。
	public List<string> SelectionDecisions { get; } = new List<string>();

	// 框选集合中 TY/TY1/TY2 生成的托压块标注；非空时 ASD 按托压块规则规划。
	public List<ObjectId> SupportBlockIds { get; } = new List<ObjectId>();

	public bool IsSupportBlock => SupportBlockIds.Count > 0;

	public bool HasPrimaryPolyline => !PrimaryPolylineId.IsNull;
}
