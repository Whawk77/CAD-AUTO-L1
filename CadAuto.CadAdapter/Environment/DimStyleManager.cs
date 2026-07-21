using System;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadAuto.CadAdapter.Environment;

public static class DimStyleManager
{
	private static readonly string[] PreferredStyleNames = new string[7] { "SCALE-1-01", "SCALE-1-02", "SCALE-1-03", "SCALE-1-04", "SCALE-1-06", "SCALE-1-08", "SCALE-1-10" };

	public static ObjectId ResolveDimStyle(Database db, Transaction tr)
	{
		DimStyleTable dimStyleTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
		DimStyleTableRecord dimStyleTableRecord = (DimStyleTableRecord)tr.GetObject(db.Dimstyle, OpenMode.ForRead);
		if (IsPreferredStyle(dimStyleTableRecord.Name))
		{
			return db.Dimstyle;
		}
		string[] preferredStyleNames = PreferredStyleNames;
		foreach (string key in preferredStyleNames)
		{
			if (dimStyleTable.Has(key))
			{
				return dimStyleTable[key];
			}
		}
		return db.Dimstyle;
	}

	public static ObjectId ResolveDiameterCalloutDimStyle(Database db, Transaction tr, ObjectId fallbackDimStyleId)
	{
		DimStyleTable dimStyleTable = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
		if (!fallbackDimStyleId.IsNull)
		{
			DimStyleTableRecord dimStyleTableRecord = tr.GetObject(fallbackDimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
			if (dimStyleTableRecord != null)
			{
				string key = dimStyleTableRecord.Name + "$3";
				if (dimStyleTable.Has(key))
				{
					return dimStyleTable[key];
				}
			}
		}
		if (dimStyleTable.Has("SCALE-1-01$3"))
		{
			return dimStyleTable["SCALE-1-01$3"];
		}
		return fallbackDimStyleId.IsNull ? db.Dimstyle : fallbackDimStyleId;
	}

	private static bool IsPreferredStyle(string styleName)
	{
		string[] preferredStyleNames = PreferredStyleNames;
		foreach (string b in preferredStyleNames)
		{
			if (string.Equals(styleName, b, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}
}
