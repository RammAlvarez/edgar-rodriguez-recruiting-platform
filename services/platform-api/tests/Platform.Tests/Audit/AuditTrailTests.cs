using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Core.Audit;
using Platform.Core.Clients;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.Audit;

/// <summary>audit_log written in the same transaction as the change, append-only; stamps set by the persistence layer.</summary>
public sealed class AuditTrailTests(ApiFactory factory) : ApiTest(factory)
{
    [Fact]
    public async Task Insert_and_update_write_before_and_after_snapshots()
    {
        var client = await WithDb(db => db.AddClient("HEB"));
        await WithDb(async db =>
        {
            var row = await db.Set<Client>().SingleAsync();
            row.LegalName = "Nueva Wal Mart de México";
            await db.SaveChangesAsync();
        });

        var entries = await WithDb(db => db.Set<AuditEntry>().Where(e => e.TableName == "clients").OrderBy(e => e.Id).ToListAsync());
        Assert.Equal([AuditAction.Insert, AuditAction.Update], entries.Select(e => e.Action));
        Assert.All(entries, e => Assert.Equal(client.Uid, e.RowId));

        Assert.Null(entries[0].Before);
        var after = JsonDocument.Parse(entries[0].After!).RootElement;
        Assert.Equal("HEB", after.GetProperty("short_name").GetString());
        Assert.False(after.TryGetProperty("id", out _), "the internal id never enters the log");

        var before = JsonDocument.Parse(entries[1].Before!).RootElement;
        Assert.Equal(JsonValueKind.Null, before.GetProperty("legal_name").ValueKind);
        Assert.Equal("Nueva Wal Mart de México", JsonDocument.Parse(entries[1].After!).RootElement.GetProperty("legal_name").GetString());
    }

    [Fact]
    public async Task Rolled_back_change_leaves_no_audit_row()
    {
        await WithDb(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            db.Add(new Client { ShortName = "GTS" });
            await db.SaveChangesAsync();
            await tx.RollbackAsync();
        });
        Assert.Equal(0, await WithDb(db => db.Set<AuditEntry>().CountAsync()));
    }

    [Fact]
    public async Task Stamps_and_uid_are_assigned_on_insert()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var user = await WithDb(db => db.AddUser(UserRole.Recruiter));
        Assert.NotEqual(Guid.Empty, user.Uid);
        Assert.True(user.CreatedAt > before);
        Assert.Equal(user.CreatedAt, user.UpdatedAt);
    }

    [Theory]
    [InlineData("UPDATE platform.audit_log SET table_name = 'x'")]
    [InlineData("DELETE FROM platform.audit_log")]
    public async Task The_api_role_cannot_change_the_log(string sql)
    {
        await WithDb(db => db.AddClient());
        await using var connection = new NpgsqlConnection(TestDatabase.ApiConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, error.SqlState);
    }

    [Fact]
    public async Task Even_the_owner_cannot_update_the_log()
    {
        await WithDb(db => db.AddClient());
        await using var connection = new NpgsqlConnection(TestDatabase.OwnerConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("UPDATE platform.audit_log SET table_name = 'x'", connection);
        var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Contains("append-only", error.MessageText, StringComparison.Ordinal);
    }
}
