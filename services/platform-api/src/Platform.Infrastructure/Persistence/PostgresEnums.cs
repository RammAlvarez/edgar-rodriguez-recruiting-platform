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
        Of<BankAccountType>("bank_account_type"),
        Of<Platform.Payroll.Groups.PeriodType>("period_type"),
        Of<Platform.Payroll.Engine.ConceptKind>("concept_kind"),
        Of<Platform.Payroll.Groups.ConceptCalc>("concept_calc"),
        Of<Platform.Payroll.Staff.EmployeeStatus>("employee_status"),
        Of<Platform.Payroll.Staff.CardRequestStatus>("card_request_status"),
        Of<Platform.Payroll.Staff.AttendanceStatus>("attendance_status"),
        Of<Platform.Payroll.Periods.PeriodStatus>("period_status"),
        Of<Platform.Payroll.Engine.ItemSource>("item_source"),
        Of<Platform.Payroll.Prepayroll.PaymentRowStatus>("payment_row_status"),
        Of<Platform.Bank.Payments.BatchStatus>("batch_status"),
        Of<Platform.Bank.Payments.PaymentBatchKind>("payment_batch_kind"),
        Of<Platform.Bank.Payments.PaymentType>("payment_type"),
        Of<Platform.Core.Notifications.NotificationKind>("notification_kind"),
    ];

    private static Entry Of<T>(string name) where T : struct, Enum => new(
        m => m.HasPostgresEnum<T>(PlatformDbContext.Schema, name),
        d => d.MapEnum<T>($"{PlatformDbContext.Schema}.{name}"),
        o => o.MapEnum<T>(name, PlatformDbContext.Schema));
}
