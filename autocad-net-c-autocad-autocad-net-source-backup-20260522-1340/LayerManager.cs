using Autodesk.AutoCAD.DatabaseServices;

namespace AutoFixtureDim
{
    public static class LayerManager
    {
        public const string PreferredAnnotationLayer = "JEE-DIM标注";

        public static string ResolveAnnotationLayer(Database db, Transaction tr)
        {
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (layerTable.Has(PreferredAnnotationLayer))
            {
                return PreferredAnnotationLayer;
            }

            var currentLayer = (LayerTableRecord)tr.GetObject(db.Clayer, OpenMode.ForRead);
            return currentLayer.Name;
        }
    }
}
