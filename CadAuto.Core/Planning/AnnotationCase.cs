using System.Collections.Generic;
using CadAuto.Core.Model;

namespace CadAuto.Core.Planning;

/// <summary>
/// Confirmed drawing: annotation <em>logic</em> (strategy ids + covering-chain schemas),
/// not millimetre values. Token decisions are an archive only.
/// </summary>
public sealed class AnnotationCase
{
	public string Id { get; set; }

	public List<string> Strategies { get; set; } = new List<string>();

	public List<string> Schemas { get; set; } = new List<string>();

	public List<AnnotationCaseDecision> Decisions { get; set; } = new List<AnnotationCaseDecision>();
}

public static class AnnotationStrategyIds
{
	/// <summary>
	/// Same-side ResidualMin + real step + ResidualMax cover overall → drop the
	/// Max-end residual (no process meaning) and keep the locating residual.
	/// </summary>
	public const string OpenCoveringDropResidualMax = "OpenCoveringDropResidualMax";
}

public sealed class AnnotationCaseDecision
{
	public string Token { get; set; }

	public bool Keep { get; set; }
}

/// <summary>FeatureFirst snapshot carried by one completed dimension plan.</summary>
public sealed class AnnotationCaseSnapshot
{
	public IList<StructureFeature> Features { get; private set; }

	public OutlineFeature2D Outline { get; private set; }

	public string MatchedCaseId { get; private set; }

	public string DocumentIdentity { get; private set; }

	public string DatabaseIdentity { get; private set; }

	public object DocumentReference { get; private set; }

	public object DatabaseReference { get; private set; }

	public string RunId { get; private set; }

	public bool Succeeded { get; private set; }

	public AnnotationCaseSnapshot(IList<StructureFeature> features, OutlineFeature2D outline, string matchedCaseId)
	{
		Features = features == null ? new List<StructureFeature>() : new List<StructureFeature>(features);
		Outline = outline;
		MatchedCaseId = matchedCaseId ?? string.Empty;
	}

	private AnnotationCaseSnapshot(AnnotationCaseSnapshot source, object documentReference, object databaseReference, string documentIdentity, string databaseIdentity, string runId)
		: this(source.Features, source.Outline, source.MatchedCaseId)
	{
		DocumentReference = documentReference;
		DatabaseReference = databaseReference;
		DocumentIdentity = documentIdentity ?? string.Empty;
		DatabaseIdentity = databaseIdentity ?? string.Empty;
		RunId = runId ?? string.Empty;
		Succeeded = true;
	}

	internal AnnotationCaseSnapshot Publish(object documentReference, object databaseReference, string documentIdentity, string databaseIdentity, string runId)
	{
		return new AnnotationCaseSnapshot(this, documentReference, databaseReference, documentIdentity, databaseIdentity, runId);
	}

	public bool MatchesDocument(object documentReference, object databaseReference, string documentIdentity, string databaseIdentity)
	{
		return Succeeded && !string.IsNullOrEmpty(RunId)
			&& object.ReferenceEquals(DocumentReference, documentReference)
			&& object.ReferenceEquals(DatabaseReference, databaseReference)
			&& string.Equals(DocumentIdentity, documentIdentity, System.StringComparison.OrdinalIgnoreCase)
			&& string.Equals(DatabaseIdentity, databaseIdentity, System.StringComparison.OrdinalIgnoreCase);
	}
}

/// <summary>Last successfully committed FeatureFirst snapshot for ASDCASE.</summary>
public static class AnnotationCaseRuntime
{
	public static AnnotationCaseSnapshot LastSnapshot { get; private set; }

	/// <summary>Diagnostic-only case match from the most recent Core plan.</summary>
	public static string LastMatchedCaseId { get; internal set; }

	public static void Invalidate()
	{
		LastSnapshot = null;
		LastMatchedCaseId = string.Empty;
	}

	public static void Publish(AnnotationCaseSnapshot snapshot, object documentReference, object databaseReference, string documentIdentity, string databaseIdentity, string runId)
	{
		LastSnapshot = snapshot == null || snapshot.Outline == null || snapshot.Features == null
			|| documentReference == null || databaseReference == null
			|| string.IsNullOrEmpty(documentIdentity) || string.IsNullOrEmpty(databaseIdentity) || string.IsNullOrEmpty(runId)
			? null
			: snapshot.Publish(documentReference, databaseReference, documentIdentity, databaseIdentity, runId);
	}
}
