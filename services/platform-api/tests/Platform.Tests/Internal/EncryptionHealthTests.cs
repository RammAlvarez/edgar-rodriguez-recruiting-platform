using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.Internal;
using Platform.Core.People;
using Platform.Core.Security;
using Platform.Tests.Support;

namespace Platform.Tests.Internal;

/// <summary>
/// The restore drill's check (docs/HANDOFF.md): <c>/internal/health/encryption</c> decrypts one stored account number with the
/// configured key and answers ok, failed or no-data — never the value.
/// </summary>
public sealed class EncryptionHealthTests(ApiFactory factory) : ApiTest(factory)
{
    private async Task<(HttpStatusCode Status, string Body)> Check()
    {
        var http = Client();
        http.DefaultRequestHeaders.Add(InternalEndpoints.SecretHeader, ApiFactory.InternalSecret);
        var response = await http.GetAsync("/internal/health/encryption");
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private Task Account(byte[] stored) => WithDb(async db =>
    {
        var person = await db.AddPerson();
        var bank = db.Set<Platform.Core.Catalogs.Bank>().First(b => b.SpeiCode == "012");
        db.Add(new PersonBankAccount
        {
            PersonId = person.Uid,
            BankId = bank.Uid,
            AccountType = BankAccountType.Clabe,
            AccountNumberEnc = stored,
            AccountLast4 = "0015",
            HolderName = "ANA PEREZ",
            IsPrimary = true,
            ValidFrom = new DateOnly(2026, 1, 1),
        });
        await db.SaveChangesAsync();
    });

    [Fact]
    public async Task It_reads_a_stored_number_with_the_key_and_never_shows_it()
    {
        Assert.Contains("no-data", (await Check()).Body, StringComparison.Ordinal);

        var cipher = Factory.Services.GetRequiredService<IFieldCipher>();
        await Account(cipher.Encrypt("012180000000000015"));
        var ok = await Check();
        Assert.Equal(HttpStatusCode.OK, ok.Status);
        Assert.Contains("\"ok\"", ok.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("012180000000000015", ok.Body, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client().GetAsync("/internal/health/encryption")).StatusCode);
    }

    [Fact]
    public async Task A_value_from_another_key_fails()
    {
        var other = cipherBytes();
        await Account(other);
        var failed = await Check();
        Assert.Equal((HttpStatusCode.InternalServerError, true), (failed.Status, failed.Body.Contains("failed", StringComparison.Ordinal)));

        static byte[] cipherBytes()
        {
            // Key id 9 (not configured) in front of a nonce, tag and ciphertext of the right sizes.
            var bytes = new byte[1 + 12 + 16 + 18];
            bytes[0] = 9;
            return bytes;
        }
    }
}
