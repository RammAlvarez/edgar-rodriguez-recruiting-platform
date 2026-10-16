using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Platform.Core.Persistence;

/// <summary>Maps Postgres constraint violations to the user-facing 409 the route returns (no pre-check races).</summary>
public static class DbErrors
{
    /// <summary>The unique index or constraint that rejected the save, or <c>null</c> for any other failure.</summary>
    public static string? UniqueViolation(DbUpdateException e) =>
        e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg ? pg.ConstraintName ?? "" : null;
}

public static class SaveExtensions
{
    /// <summary>Saves; a unique violation becomes 409 with <paramref name="conflictMessage"/>.</summary>
    public static async Task<Microsoft.AspNetCore.Http.IResult> SaveOrConflictAsync(this DbContext db,
        Func<Task<Microsoft.AspNetCore.Http.IResult>> ok, string conflictMessage, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (DbErrors.UniqueViolation(e) is not null)
        {
            return Http.ApiResults.Conflict(conflictMessage);
        }
        return await ok();
    }

    /// <remarks>With <paramref name="transaction"/>, it is committed after a successful save (rolled back on dispose otherwise).</remarks>
    public static async Task<Microsoft.AspNetCore.Http.IResult> SaveOrConflictAsync(this DbContext db,
        Func<Microsoft.AspNetCore.Http.IResult> ok, string conflictMessage, CancellationToken ct,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            if (transaction is not null)
                await transaction.CommitAsync(ct);
            return ok();
        }
        catch (DbUpdateException e) when (DbErrors.UniqueViolation(e) is not null)
        {
            return Http.ApiResults.Conflict(conflictMessage);
        }
    }
}
