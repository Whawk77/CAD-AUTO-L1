using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace AutoFixtureDim
{
    public static class AnnotationMetadata
    {
        public const string AppName = "AUTOFIXDIM";

        public static void EnsureRegApp(Database db, Transaction tr)
        {
            var table = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (table.Has(AppName))
            {
                return;
            }

            table.UpgradeOpen();
            var record = new RegAppTableRecord { Name = AppName };
            table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        public static void Mark(Entity entity, string groupId)
        {
            entity.XData = new ResultBuffer(
                new TypedValue((int)DxfCode.ExtendedDataRegAppName, AppName),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, "GroupId"),
                new TypedValue((int)DxfCode.ExtendedDataAsciiString, groupId ?? string.Empty));
        }

        public static bool IsMarked(Entity entity)
        {
            using (ResultBuffer buffer = entity.GetXDataForApplication(AppName))
            {
                return buffer != null;
            }
        }

        public static int ClearGeneratedAnnotations(Database db, Transaction tr)
        {
            EnsureRegApp(db, tr);
            var erased = 0;
            var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

            foreach (ObjectId blockId in blockTable)
            {
                var block = (BlockTableRecord)tr.GetObject(blockId, OpenMode.ForRead);
                if (block.IsFromExternalReference || block.IsDependent)
                {
                    continue;
                }

                var toErase = new List<ObjectId>();
                foreach (ObjectId entityId in block)
                {
                    var entity = tr.GetObject(entityId, OpenMode.ForRead, false) as Entity;
                    if (entity != null && IsMarked(entity))
                    {
                        toErase.Add(entityId);
                    }
                }

                foreach (ObjectId entityId in toErase)
                {
                    var entity = (Entity)tr.GetObject(entityId, OpenMode.ForWrite);
                    entity.Erase();
                    erased++;
                }
            }

            return erased;
        }
    }
}
