using Autodesk.AutoCAD.DatabaseServices;

namespace CadAuto.CadAdapter.Environment;

public static class LayerManager
{
	public const string PreferredAnnotationLayer = "JEE-DIM标注";

	public static string ResolveAnnotationLayer(Database db, Transaction tr)
	{
		LayerTableRecord layerTableRecord = (LayerTableRecord)tr.GetObject(db.Clayer, OpenMode.ForRead);
		return layerTableRecord.Name;
	}
}
