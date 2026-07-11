using Autodesk.AutoCAD.DatabaseServices;

namespace CadAuto.CadAdapter.Environment;

public static class LayerManager
{
	public const string PreferredAnnotationLayer = "JEE-DIM标注";

	public static string ResolveAnnotationLayer(Database db, Transaction tr)
	{
		LayerTable layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
		if (layerTable.Has("JEE-DIM标注"))
		{
			return "JEE-DIM标注";
		}
		LayerTableRecord layerTableRecord = (LayerTableRecord)tr.GetObject(db.Clayer, OpenMode.ForRead);
		return layerTableRecord.Name;
	}
}
