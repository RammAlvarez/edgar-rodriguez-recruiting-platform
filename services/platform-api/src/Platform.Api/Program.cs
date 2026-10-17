using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Platform.Api.Auth;
using Platform.Api.Hosting;
using Platform.Api.Internal;
using Platform.Api.Jobs;
using Platform.Bank.Payments;
using Platform.Bank.Registrations;
using Platform.Bank.Reports;
using Platform.Core.Abstractions;
using Platform.Core.Access;
using Platform.Core.Audit;
using Platform.Core.Catalogs;
using Platform.Core.Clients;
using Platform.Core.Http;
using Platform.Core.Notifications;
using Platform.Core.People;
using Platform.Core.Reports;
using Platform.Core.Security;
using Platform.Core.Users;
using Platform.Infrastructure.Email;
using Platform.Infrastructure.Persistence;
using Platform.Infrastructure.Reports;
using Platform.Infrastructure.Storage;
using Platform.Payroll.Groups;
using Platform.Payroll.Prepayroll;
using Platform.Payroll.Staff;
using Platform.Recruiting;
using Platform.Recruiting.Candidates;
using Platform.Recruiting.Openings;
using Platform.Recruiting.Portal;
using Platform.Recruiting.Recruiters;
using Platform.Recruiting.Reports;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// The OpenAPI build step (GetDocument.Insider) starts the host without any environment.
var generatingOpenApi = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
var settings = PlatformSettings.FromConfiguration(builder.Configuration,
    allowDefaults: builder.Environment.IsDevelopment() || generatingOpenApi);

builder.Host.UseSerilog((context, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new DecimalStringJsonConverter());
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});
builder.Services.AddPlatformProblemDetails();
builder.Services.AddOpenApi("v1", o => o.AddSchemaTransformer<DecimalAsStringSchemaTransformer>());
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins([.. settings.AppOrigins])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders(InvalidatesExtensions.HeaderName)));

builder.Services.AddSingleton(settings);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<RequestContext>();
builder.Services.AddScoped<DataAccess>();
builder.Services.AddScoped<IPersonScope, RecruiterPersonScope>();
builder.Services.AddPlatformPersistence(settings.DatabaseUrl);
builder.Services.AddSingleton(settings.S3);
builder.Services.AddSingleton<IObjectStorage, S3ObjectStorage>();
builder.Services.AddSingleton<IReportRenderer, ReportRenderer>();
builder.Services.AddSingleton<IPrepayrollFiles, PrepayrollFiles>();
builder.Services.AddSingleton(settings.Encryption);
builder.Services.AddSingleton<IFieldCipher, AesGcmFieldCipher>();
builder.Services.AddScoped<IJob, AttendanceLockJob>();
builder.Services.AddScoped<IJob, PeriodRollJob>();
builder.Services.AddScoped<IJob, StatusRollJob>();
builder.Services.AddScoped<IJob, DocumentExpiryJob>();
builder.Services.AddScoped<IJob, NotificationsDigestJob>();
builder.Services.AddScoped<Platform.Core.Notifications.ICandidateDocumentListener, Platform.Recruiting.Candidates.CandidateDocumentListener>();
builder.Services.AddSingleton(settings.Firebase);
builder.Services.AddSingleton<IAuthProvider, FirebaseAuthProvider>();
builder.Services.AddSingleton(settings.Links);
builder.Services.AddSingleton(settings.Email);
if (settings.Email.ResendApiKey is { Length: > 0 })
    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(c => c.BaseAddress = ResendEmailSender.BaseAddress);
else
    builder.Services.AddSingleton<IEmailSender, LogEmailSender>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>(includeInternalTypes: true);
builder.Services.AddValidatorsFromAssemblyContaining<MeDto>(includeInternalTypes: true);
builder.Services.AddValidatorsFromAssemblyContaining<OpeningRequest>(includeInternalTypes: true);
builder.Services.AddValidatorsFromAssemblyContaining<PayrollGroupRequest>(includeInternalTypes: true);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseRouting();
app.UseExceptionHandler();
app.UseCors();
app.UseInvalidatesHeader();
app.UseMiddleware<AuthenticationMiddleware>();

app.MapOpenApi("/openapi/{documentName}.json");

app.MapGet("/health", async (DbContext db, IObjectStorage storage, CancellationToken ct) =>
{
    var database = await db.Database.CanConnectAsync(ct);
    string bucket;
    try
    {
        await storage.PingAsync(ct);
        bucket = "ok";
    }
    catch (Exception e) when (e is not OperationCanceledException)
    {
        bucket = e.GetType().Name;
    }
    var healthy = database && bucket == "ok";
    return Results.Json(new { status = healthy ? "ok" : "degraded", database = database ? "ok" : "unreachable", bucket },
        statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
}).ExcludeFromDescription();

var v1 = app.MapGroup("/v1");
v1.MapMeEndpoints();
v1.MapMfaEndpoints();
v1.MapCatalogEndpoints();
v1.MapClientEndpoints();
v1.MapPeopleEndpoints();
v1.MapDocumentEndpoints();
v1.MapUserEndpoints();
v1.MapOpeningEndpoints();
v1.MapCandidateEndpoints();
v1.MapRecruiterEndpoints();
v1.MapCandidatePortalEndpoints();
v1.MapClientPortalEndpoints();
v1.MapRecruitingReportEndpoints();
v1.MapPayrollGroupEndpoints();
v1.MapEmployeeEndpoints();
v1.MapScheduleEndpoints();
v1.MapAttendanceEndpoints();
v1.MapPrepayrollEndpoints();
v1.MapIncidentEndpoints();
v1.MapPaymentEndpoints();
v1.MapRegistrationEndpoints();
v1.MapPayrollReportEndpoints();
v1.MapNotificationEndpoints();

app.MapInternalEndpoints(settings.InternalSecret);

app.Run();

/// <summary>Entry point, public for WebApplicationFactory in the tests.</summary>
public partial class Program;
