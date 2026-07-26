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

	private const short CurrentSchemaVersion = 1;

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

	public static void Mark(Entity entity, string groupId, string kind = KindDimension)
	{
		List<TypedValue> values = new List<TypedValue>();
		using (ResultBuffer existing = entity.XData)
		{
			if (existing != null)
			{
				bool skipCurrentApplication = false;
				foreach (TypedValue value in existing.AsArray())
				{
					if (value.TypeCode == 1001)
					{
						skipCurrentApplication = string.Equals(value.Value as string, AppName, StringComparison.Ordinal);
					}
					if (!skipCurrentApplication)
					{
						values.Add(value);
					}
				}
			}
		}
		values.Add(new TypedValue(1001, AppName));
		values.Add(new TypedValue(1000, "GroupId"));
		values.Add(new TypedValue(1000, groupId ?? string.Empty));
		values.Add(new TypedValue(1000, "Kind"));
		values.Add(new TypedValue(1000, string.IsNullOrEmpty(kind) ? KindDimension : kind));
		values.Add(new TypedValue(1000, "SchemaVersion"));
		values.Add(new TypedValue(1070, CurrentSchemaVersion));
		using ResultBuffer resultBuffer = new ResultBuffer(values.ToArray());
		entity.XData = resultBuffer;
	}

	public static bool IsMarked(Entity entity)
	{
		using ResultBuffer resultBuffer = entity.GetXDataForApplication(AppName);
		return resultBuffer != null;
	}

	public static string GetGroupId(Entity entity)
	{
		return GetStringField(entity, "GroupId", string.Empty);
	}

	public static string GetKind(Entity entity)
	{
		return GetStringField(entity, "Kind", KindDimension);
	}

	private static string GetStringField(Entity entity, string fieldName, string fallback)
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
				if (array[i].TypeCode == 1000 && string.Equals(array[i].Value as string, fieldName, StringComparison.Ordinal) && array[i + 1].TypeCode == 1000)
				{
					return (array[i + 1].Value as string) ?? fallback;
				}
			}
		}
		return fallback;
	}

	public static int ClearGeneratedAnnotations(Database db, Transaction tr, params string[] kinds)
	{
		EnsureRegApp(db, tr);
		HashSet<string> allowedKinds = CreateKindFilter(kinds);
		int num = 0;
		BlockTable blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
		foreach (ObjectId item in blockTable)
		{
			BlockTableRecord blockTableRecord = (BlockTableRecord)tr.GetObject(item, OpenMode.ForRead);
			if (blockTableRecord.IsFromExternalReference || blockTableRecord.IsDependent || !blockTableRecord.IsLayout)
			{
				continue;
			}
			List<ObjectId> list = new List<ObjectId>();
			foreach (ObjectId item2 in blockTableRecord)
			{
				Entity entity = tr.GetObject(item2, OpenMode.ForRead, openErased: false) as Entity;
				if (entity != null && IsMarked(entity) && IsAllowedKind(GetKind(entity), allowedKinds))
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

	public static int ClearLatestGeneratedAnnotations(Database db, Transaction tr, params string[] kinds)
	{
		EnsureRegApp(db, tr);
		List<GeneratedAnnotation> list = CollectMarkedAnnotations(db, tr, kinds);
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

	private static List<GeneratedAnnotation> CollectMarkedAnnotations(Database db, Transaction tr, params string[] kinds)
	{
		List<GeneratedAnnotation> list = new List<GeneratedAnnotation>();
		HashSet<string> allowedKinds = CreateKindFilter(kinds);
		BlockTable blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
		foreach (ObjectId item in blockTable)
		{
			BlockTableRecord blockTableRecord = (BlockTableRecord)tr.GetObject(item, OpenMode.ForRead);
			if (blockTableRecord.IsFromExternalReference || blockTableRecord.IsDependent || !blockTableRecord.IsLayout)
			{
				continue;
			}
			foreach (ObjectId item2 in blockTableRecord)
			{
				Entity entity = tr.GetObject(item2, OpenMode.ForRead, openErased: false) as Entity;
				if (!(entity == null) && IsMarked(entity))
				{
					string kind = GetKind(entity);
					if (IsAllowedKind(kind, allowedKinds))
					{
						list.Add(new GeneratedAnnotation(item2, GetGroupId(entity)));
					}
				}
			}
		}
		return list;
	}

	private static HashSet<string> CreateKindFilter(IEnumerable<string> kinds)
	{
		return new HashSet<string>((kinds ?? Enumerable.Empty<string>()).Where(kind => !string.IsNullOrEmpty(kind)), StringComparer.Ordinal);
	}

	private static bool IsAllowedKind(string kind, HashSet<string> allowedKinds)
	{
		return allowedKinds.Count == 0 || allowedKinds.Contains(string.IsNullOrEmpty(kind) ? KindDimension : kind);
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
