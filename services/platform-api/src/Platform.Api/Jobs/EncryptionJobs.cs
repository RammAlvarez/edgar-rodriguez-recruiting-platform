using Microsoft.EntityFrameworkCore;
using Platform.Api.Internal;
using Platform.Core.Security;

namespace Platform.Api.Jobs;

/// <summary>
/// <c>reencrypt-fields</c>, run by the operator during a key rotation (docs/HANDOFF.md §6), never by the crontab: every value
/// written under a key of <c>ENCRYPTION_PREVIOUS_KEYS</c> is decrypted and encrypted again with the current key, in batches of
/// <see cref="BatchSize"/>, one transaction each. A value is written back only if nobody changed it meanwhile. Values under a key
/// the API doesn't hold are counted as <c>unreadable</c> and left alone. Idempotent: once <c>pending</c> is 0 everywhere, the old
/// key can leave the environment.
/// </summary>
public sealed class ReencryptFieldsJob(DbContext db, IFieldCipher cipher, EncryptionSettings settings) : IJob
{
    public const int BatchSize = 500;

    /// <summary>Every encrypted column (DATA_MODEL §3.7): live accounts, the cuenta de cargo, paid-line snapshots, cards, MFA secrets.</summary>
    public static readonly (string Table, string Column)[] Columns =
    [
        ("person_bank_accounts", "account_number_enc"),
        ("paying_accounts", "charge_account_number_enc"),
        ("prepayroll_lines", "account_number_enc"),
        ("payment_batch_rows", "account_number_enc"),
        ("card_requests", "card_number_enc"),
        ("users", "mfa_secret_enc"),
    ];

    public string Name => "reencrypt-fields";

    private sealed record Stored(long Id, byte[] Value);

    private sealed record Counts(int Pending, int Unreadable);

    public async Task<object> RunAsync(CancellationToken cancellationToken)
    {
        var previous = settings.PreviousKeys.Keys.Select(k => (int)k).ToArray();
        var known = previous.Append(settings.KeyId).ToArray();
        var columns = new List<object>();
        foreach (var (table, column) in Columns)
        {
            // Identifiers come only from Columns above, never from input; values always go as parameters.
            var select = $$"""
                SELECT id, {{column}} AS value FROM platform.{{table}}
                WHERE {{column}} IS NOT NULL AND get_byte({{column}}, 0) = ANY({0})
                ORDER BY id LIMIT {{BatchSize}}
                """;
            var update = $"UPDATE platform.{table} SET {column} = {{0}} WHERE id = {{1}} AND {column} = {{2}}";
            var count = $$"""
                SELECT count(*) FILTER (WHERE get_byte({{column}}, 0) = ANY({0}))::int AS pending,
                       count(*) FILTER (WHERE NOT get_byte({{column}}, 0) = ANY({1}))::int AS unreadable
                FROM platform.{{table}} WHERE {{column}} IS NOT NULL
                """;
            var reencrypted = 0;
            while (previous.Length > 0)
            {
                // Only the keys we hold are selected, so a batch always makes progress and the loop ends.
                var batch = await db.Database.SqlQueryRaw<Stored>(select, previous)
                    .ToListAsync(cancellationToken);
                if (batch.Count == 0)
                    break;
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                foreach (var row in batch)
                {
                    var fresh = cipher.Encrypt(cipher.Decrypt(row.Value));
                    reencrypted += await db.Database.ExecuteSqlRawAsync(update, [fresh, row.Id, row.Value], cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
            }
            var counts = await db.Database.SqlQueryRaw<Counts>(count, previous, known)
                .ToListAsync(cancellationToken);
            columns.Add(new { table, column, reencrypted, pending = counts[0].Pending, unreadable = counts[0].Unreadable });
        }
        return new { keyId = (int)settings.KeyId, previousKeyIds = previous, columns };
    }
}
