using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace CadAuto.CadAdapter;

public static class AnnotationMetadata
{
	private sealed class GeneratedAnnotation
	{
		public ObjectId EntityId { get; private set; }

		public string GroupId { get; private set; }

		public GeneratedAnnotation(ObjectId entityId, string groupId)
		{
			EntityId = entityId;
			GroupId = groupId ?? string.Empty;
		}
	}

	public const string AppName = "AUTOFIXDIM";

	public const string KindDimension = "Dimension";

	public const string KindAg1 = "Ag1";

	public const string KindCoreDebug = "CoreDebug";

	public static void EnsureRegApp(Database db, Transaction tr)
	{
		RegAppTable regAppTable = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
		if (!regAppTable.Has(AppName))
		{
			regAppTable.UpgradeOpen();
			RegAppTableRecord regAppTableRecord = new RegAppTableRecord
			{
				Name = AppName
			};
			regAppTable.Add(regAppTableRecord);
			tr.AddNewlyCreatedDBObject(regAppTableRecord, add: true);
		}
	}

	public static void Mark(Entity entity, string groupId, string kind)
	{
		using (ResultBuffer resultBuffer = new ResultBuffer(new TypedValue(1001, AppName), new TypedValue(1000, "GroupId"), new TypedValue(1000, groupId ?? string.Empty), new TypedValue(1000, "Kind"), new TypedValue(1000, kind ?? KindDimension), new TypedValue(1000, "SchemaVersion"), new TypedValue(1070, (short)1)))
		{
			entity.XData = resultBuffer;
		}
	}

	public static string GetKind(Entity entity)
	{
		using (ResultBuffer resultBuffer = entity.GetXDataForApplication(AppName))
		{
			if (resultBuffer == null)
			{
				return string.Empty;
			}
			TypedValue[] array = resultBuffer.AsArray();
			for (int i = 0; i < array.Length - 1; i++)
			{
				if (array[i].TypeCode == 1000 && string.Equals(array[i].Value as string, "Kind", StringComparison.Ordinal) && array[i + 1].TypeCode == 1000)
				{
					return (array[i + 1].Value as string) ?? KindDimension;
				}
			}
		}
		// Entities marked before the Kind field existed are regenerable dimensions.
		return KindDimension;
	}

	public static bool IsMarked(Entity entity)
	{
		using ResultBuffer resultBuffer = entity.GetXDataForApplication(AppName);
		return resultBuffer != null;
	}

	public static string GetGroupId(Entity entity)
	{
		using (ResultBuffer resultBuffer = entity.GetXDataForApplication(AppName))
		{
			if (resultBuffer == null)
			{
				return string.Empty;
			}
			TypedValue[] array = resultBuffer.AsArray();
			for (int i = 0; i < array.Length - 1; i++)
			{
				if (array[i].TypeCode == 1000 && string.Equals(array[i].Value as string, "GroupId", StringComparison.Ordinal) && array[i + 1].TypeCode == 1000)
				{
					return (array[i + 1].Value as string) ?? string.Empty;
				}
			}
		}
		return string.Empty;
	}

	public static int ClearGeneratedAnnotations(Database db, Transaction tr)
	{
		EnsureRegApp(db, tr);
		int num = 0;
		BlockTable blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
		foreach (ObjectId item in blockTable)
		{
			BlockTableRecord blockTableRecord = (BlockTableRecord)tr.GetObject(item, OpenMode.ForRead);
			if (blockTableRecord.IsFromExternalReference || blockTableRecord.IsDependent)
			{
				continue;
			}
			List<ObjectId> list = new List<ObjectId>();
			foreach (ObjectId item2 in blockTableRecord)
			{
				Entity entity = tr.GetObject(item2, OpenMode.ForRead, openErased: false) as Entity;
				if (entity != null && IsMarked(entity))
				{
					list.Add(item2);
				}
			}
			foreach (ObjectId item3 in list)
			{
				Entity entity2 = (Entity)tr.GetObject(item3, OpenMode.ForWrite);
				entity2.Erase();
				num++;
			}
		}
		return num;
	}

	public static int ClearLatestGeneratedAnnotations(Database db, Transaction tr)
	{
		EnsureRegApp(db, tr);
		List<GeneratedAnnotation> list = CollectMarkedAnnotations(db, tr);
		if (list.Count == 0)
		{
			return 0;
		}
		string latestGroupId = SelectLatestGroupId(list.Select((GeneratedAnnotation m) => m.GroupId));
		if (string.IsNullOrEmpty(latestGroupId))
		{
			return 0;
		}
		int num = 0;
		foreach (GeneratedAnnotation item in list.Where((GeneratedAnnotation m) => string.Equals(m.GroupId, latestGroupId, StringComparison.Ordinal)))
		{
			Entity entity = (Entity)tr.GetObject(item.EntityId, OpenMode.ForWrite);
			entity.Erase();
			num++;
		}
		return num;
	}

	private static List<GeneratedAnnotation> CollectMarkedAnnotations(Database db, Transaction tr)
	{
		List<GeneratedAnnotation> list = new List<GeneratedAnnotation>();
		BlockTable blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
		foreach (ObjectId item in blockTable)
		{
			BlockTableRecord blockTableRecord = (BlockTableRecord)tr.GetObject(item, OpenMode.ForRead);
			if (blockTableRecord.IsFromExternalReference || blockTableRecord.IsDependent)
			{
				continue;
			}
			foreach (ObjectId item2 in blockTableRecord)
			{
				Entity entity = tr.GetObject(item2, OpenMode.ForRead, openErased: false) as Entity;
				if (!(entity == null) && IsMarked(entity))
				{
					list.Add(new GeneratedAnnotation(item2, GetGroupId(entity)));
				}
			}
		}
		return list;
	}

	private static string SelectLatestGroupId(IEnumerable<string> groupIds)
	{
		List<string> list = groupIds.Where((string id) => !string.IsNullOrEmpty(id)).Distinct().ToList();
		if (list.Count == 0)
		{
			return string.Empty;
		}
		var anon = (from id in list
			select new
			{
				Id = id,
				Timestamp = ParseGroupTimestamp(id)
			} into g
			where g.Timestamp.HasValue
			orderby g.Timestamp.Value descending
			select g).FirstOrDefault();
		if (anon != null)
		{
			return anon.Id;
		}
		return (list.Count == 1) ? list[0] : string.Empty;
	}

	private static DateTime? ParseGroupTimestamp(string groupId)
	{
		DateTime result;
		return DateTime.TryParseExact(groupId, "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.None, out result) ? new DateTime?(result) : ((DateTime?)null);
	}
}
