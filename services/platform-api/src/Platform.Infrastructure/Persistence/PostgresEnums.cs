using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Platform.Core.Audit;
using Platform.Core.Catalogs;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Recruiting.Candidates;
using Platform.Recruiting.Openings;

namespace Platform.Infrastructure.Persistence;

/// <summary>
/// Every Postgres enum of schema <c>platform</c> (DATA_MODEL §2). Npgsql needs each one declared in the EF model, mapped
/// on the data source and mapped in the EF provider options; this list feeds all three so none is forgotten.
/// </summary>
public static class PostgresEnums
{
    public sealed record Entry(
        Action<ModelBuilder> Declare,
        Action<NpgsqlDataSourceBuilder> MapOnDataSource,
        Action<NpgsqlDbContextOptionsBuilder> MapInEf);

    public static readonly IReadOnlyList<Entry> All =
    [
        Of<UserRole>("user_role"),
        Of<PermissionModule>("permission_module"),
        Of<AuditAction>("audit_action"),
        Of<DataAccessKind>("data_access_kind"),
        Of<RegistrationBank>("registration_bank"),
        Of<DocumentTemplateScope>("document_template_scope"),
        Of<Gender>("gender"),
        Of<CandidateSource>("candidate_source"),
        Of<DocumentStatus>("document_status"),
        Of<OpeningStatus>("opening_status"),
        Of<OpeningPriority>("opening_priority"),
        Of<OpeningEventKind>("opening_event_kind"),
        Of<CandidateStage>("candidate_stage"),
        Of<InterviewOutcome>("interview_outcome"),
    ];

    private static Entry Of<T>(string name) where T : struct, Enum => new(
        m => m.HasPostgresEnum<T>(PlatformDbContext.Schema, name),
        d => d.MapEnum<T>($"{PlatformDbContext.Schema}.{name}"),
        o => o.MapEnum<T>(name, PlatformDbContext.Schema));
}
