namespace Platform.Core.Abstractions;

/// <summary>
/// A row computed by the API from audited source rows (PLAN D38, D84): it is not written to <c>audit_log</c> — its sources
/// are — so a recompute does not flood the trail. Prenómina lines and their items.
/// </summary>
public interface IDerived;
