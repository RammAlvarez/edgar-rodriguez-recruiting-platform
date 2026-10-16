namespace Platform.Core.People;

/// <summary>
/// Whether the caller may read or change a person's profile and documents (DATA_MODEL §8, PLAN D73). An Administrador outside
/// cover mode reaches everyone; a Reclutador (or an Administrador covering one) only the candidates of their vacantes. The
/// rule needs recruiting tables, so it is implemented in <c>Platform.Recruiting</c> (D45).
/// </summary>
public interface IPersonScope
{
    Task<bool> CanAccessAsync(Guid personId, CancellationToken ct);
}
