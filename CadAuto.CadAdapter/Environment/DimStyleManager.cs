using System;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadAuto.CadAdapter.Environment;

public static class DimStyleManager
{
	public static Color ByLayerColor => Color.FromColorIndex(ColorMethod.ByLayer, 256);

	public static Color DefaultTextColor => Color.FromColorIndex(ColorMethod.ByAci, 3);

	public static ObjectId ResolveDimStyle(Database db, Transaction tr)
	{
		return db.Dimstyle;
	}

	public static ObjectId ResolveDiameterCalloutDimStyle(Database db, Transaction tr, ObjectId fallbackDimStyleId)
	{
		DimStyleTable dimStyleTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
		ObjectId currentDimStyleId = fallbackDimStyleId.IsNull ? db.Dimstyle : fallbackDimStyleId;
		if (!currentDimStyleId.IsNull)
		{
			DimStyleTableRecord dimStyleTableRecord = tr.GetObject(currentDimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
			if (dimStyleTableRecord != null)
			{
				string key = dimStyleTableRecord.Name + "$3";
				if (dimStyleTable.Has(key))
				{
					return dimStyleTable[key];
				}
			}
		}
		return currentDimStyleId;
	}
}
