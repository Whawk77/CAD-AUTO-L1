using Autodesk.AutoCAD.DatabaseServices;

namespace CadAuto.CadAdapter.Environment
{
    public static class DimStyleManager
    {
        private static readonly string[] PreferredStyleNames =
        {
            "SCALE-1-01",
            "SCALE-1-02",
            "SCALE-1-03",
            "SCALE-1-04",
            "SCALE-1-06",
            "SCALE-1-08",
            "SCALE-1-10"
        };

        public static ObjectId ResolveDimStyle(Database db, Transaction tr)
        {
            var table = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            var currentStyle = (DimStyleTableRecord)tr.GetObject(db.Dimstyle, OpenMode.ForRead);
            if (IsPreferredStyle(currentStyle.Name))
            {
                return db.Dimstyle;
            }

            foreach (var styleName in PreferredStyleNames)
            {
                if (table.Has(styleName))
                {
                    return table[styleName];
                }
            }

            return db.Dimstyle;
        }

        public static ObjectId ResolveDiameterCalloutDimStyle(Database db, Transaction tr, ObjectId fallbackDimStyleId)
        {
            const string diameterCalloutStyleName = "SCALE-1-01$3";
            var table = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            if (!fallbackDimStyleId.IsNull)
            {
                var fallbackStyle = tr.GetObject(fallbackDimStyleId, OpenMode.ForRead) as DimStyleTableRecord;
                if (fallbackStyle != null)
                {
                    var matchingDiameterStyleName = fallbackStyle.Name + "$3";
                    if (table.Has(matchingDiameterStyleName))
                    {
                        return table[matchingDiameterStyleName];
                    }
                }
            }

            if (table.Has(diameterCalloutStyleName))
            {
                return table[diameterCalloutStyleName];
            }

            return fallbackDimStyleId.IsNull ? db.Dimstyle : fallbackDimStyleId;
        }

        private static bool IsPreferredStyle(string styleName)
        {
            foreach (var preferredStyleName in PreferredStyleNames)
            {
                if (string.Equals(styleName, preferredStyleName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
