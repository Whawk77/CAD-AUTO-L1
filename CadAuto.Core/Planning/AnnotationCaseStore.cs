using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CadAuto.Core.Planning;

/// <summary>
/// JSON file of confirmed annotation cases. Hand-rolled for net472 (no extra package).
/// </summary>
public sealed class AnnotationCaseStore
{
	private readonly List<AnnotationCase> _cases = new List<AnnotationCase>();

	public IList<AnnotationCase> Cases { get { return _cases; } }

	public void Add(AnnotationCase annotationCase)
	{
		if (annotationCase == null || string.IsNullOrEmpty(annotationCase.Id))
		{
			return;
		}
		_cases.RemoveAll(c => string.Equals(c.Id, annotationCase.Id, StringComparison.Ordinal));
		_cases.Add(annotationCase);
	}

	public static AnnotationCaseStore Load(string path)
	{
		AnnotationCaseStore store = new AnnotationCaseStore();
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
		{
			return store;
		}
		ParseInto(store, File.ReadAllText(path));
		return store;
	}

	public void Save(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return;
		}
		string directory = Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
		{
			Directory.CreateDirectory(directory);
		}
		File.WriteAllText(path, ToJson());
	}

	public string ToJson()
	{
		StringBuilder builder = new StringBuilder();
		builder.Append("{\"cases\":[");
		for (int i = 0; i < _cases.Count; i++)
		{
			if (i > 0)
			{
				builder.Append(',');
			}
			AnnotationCase c = _cases[i];
			builder.Append("{\"id\":\"").Append(Escape(c.Id)).Append("\",\"decisions\":[");
			if (c.Decisions != null)
			{
				for (int j = 0; j < c.Decisions.Count; j++)
				{
					if (j > 0)
					{
						builder.Append(',');
					}
					AnnotationCaseDecision d = c.Decisions[j];
					builder.Append("{\"token\":\"").Append(Escape(d.Token ?? string.Empty))
						.Append("\",\"keep\":").Append(d.Keep ? "true" : "false").Append('}');
				}
			}
			builder.Append("]}");
		}
		builder.Append("]}");
		return builder.ToString();
	}

	private static string Escape(string value)
	{
		return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
	}

	private static void ParseInto(AnnotationCaseStore store, string json)
	{
		if (string.IsNullOrEmpty(json))
		{
			return;
		}
		int index = 0;
		while ((index = json.IndexOf("\"id\"", index, StringComparison.Ordinal)) >= 0)
		{
			string id = ReadStringValue(json, json.IndexOf(':', index) + 1);
			AnnotationCase annotationCase = new AnnotationCase { Id = id };
			int decisionsAt = json.IndexOf("\"decisions\"", index, StringComparison.Ordinal);
			int nextId = json.IndexOf("\"id\"", index + 4, StringComparison.Ordinal);
			int blockEnd = nextId < 0 ? json.Length : nextId;
			if (decisionsAt >= 0 && decisionsAt < blockEnd)
			{
				int cursor = decisionsAt;
				while (true)
				{
					int tokenAt = json.IndexOf("\"token\"", cursor, StringComparison.Ordinal);
					if (tokenAt < 0 || tokenAt >= blockEnd)
					{
						break;
					}
					string token = ReadStringValue(json, json.IndexOf(':', tokenAt) + 1);
					int keepAt = json.IndexOf("\"keep\"", tokenAt, StringComparison.Ordinal);
					bool keep = keepAt >= 0 && keepAt < blockEnd
						&& json.IndexOf("true", keepAt, StringComparison.Ordinal) >= 0
						&& json.IndexOf("true", keepAt, StringComparison.Ordinal) < (json.IndexOf('}', keepAt) < 0 ? blockEnd : json.IndexOf('}', keepAt));
					annotationCase.Decisions.Add(new AnnotationCaseDecision { Token = token, Keep = keep });
					cursor = tokenAt + 6;
				}
			}
			store.Add(annotationCase);
			index += 4;
		}
	}

	private static string ReadStringValue(string json, int from)
	{
		int q1 = json.IndexOf('"', from);
		if (q1 < 0)
		{
			return string.Empty;
		}
		int q2 = json.IndexOf('"', q1 + 1);
		if (q2 < 0)
		{
			return string.Empty;
		}
		return json.Substring(q1 + 1, q2 - q1 - 1);
	}
}
