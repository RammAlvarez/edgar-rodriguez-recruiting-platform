using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Api.Internal;
using Platform.Core.People;
using Platform.Core.Security;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.Internal;

/// <summary>The API host mid-rotation: key 2 is current and the local key 1 is only kept to read old values (HANDOFF §6).</summary>
public sealed class RotatedKeyApiFactory : ApiFactory
{
    public const string OldKey = "bG9jYWwtb25seS1rZXktbm90LWZvci1yZWFsLWRhdGE=";
    public static readonly string NewKey = Convert.ToBase64String(Enumerable.Repeat((byte)42, 32).ToArray());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ENCRYPTION_KEY", NewKey);
        builder.UseSetting("ENCRYPTION_KEY_ID", "2");
        builder.UseSetting("ENCRYPTION_PREVIOUS_KEYS", $"1:{OldKey}");
    }
}

/// <summary>
/// Key rotation (HANDOFF §6): values under a previous key still read, new writes use the current key, and
/// <c>reencrypt-fields</c> moves every stored value to the current key without changing what it decrypts to.
/// </summary>
public sealed class KeyRotationTests(RotatedKeyApiFactory factory) : ApiTest(factory), IClassFixture<RotatedKeyApiFactory>
{
    private static readonly AesGcmFieldCipher Old = new(EncryptionSettings.Parse(RotatedKeyApiFactory.OldKey, "1"));

    private async Task<JsonElement> Run()
    {
        var http = Client();
        http.DefaultRequestHeaders.Add(InternalEndpoints.SecretHeader, ApiFactory.InternalSecret);
        var response = await http.PostAsync("/internal/jobs/reencrypt-fields", null);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static JsonElement Column(JsonElement result, string table) =>
        result.GetProperty("columns").EnumerateArray().Single(c => c.GetProperty("table").GetString() == table);

    private Task<PersonBankAccount> Account(byte[] stored) => WithDb(async db =>
    {
        var person = await db.AddPerson();
        var bank = db.Set<Platform.Core.Catalogs.Bank>().First(b => b.SpeiCode == "012");
        var account = new PersonBankAccount
        {
            PersonId = person.Uid,
            BankId = bank.Uid,
            AccountType = BankAccountType.Clabe,
            AccountNumberEnc = stored,
            AccountLast4 = "0015",
            HolderName = "ANA PEREZ",
            IsPrimary = true,
            ValidFrom = new DateOnly(2026, 1, 1),
        };
        db.Add(account);
        await db.SaveChangesAsync();
        return account;
    });

    [Fact]
    public async Task Old_values_move_to_the_current_key_and_still_read_the_same()
    {
        var cipher = Factory.Services.GetRequiredService<IFieldCipher>();
        var oldAccount = await Account(Old.Encrypt("012180000000000015"));
        var newAccount = await Account(cipher.Encrypt("012580015000000011"));
        var user = await WithDb(async db =>
        {
            var u = await db.AddUser(UserRole.Administrator);
            u.MfaSecretEnc = Old.Encrypt("JBSWY3DPEHPK3PXP");
            await db.SaveChangesAsync();
            return u;
        });
        Assert.Equal("012180000000000015", cipher.Decrypt(oldAccount.AccountNumberEnc)); // readable mid-rotation

        var first = await Run();
        Assert.Equal(2, first.GetProperty("keyId").GetInt32());
        Assert.Equal((1, 0, 0), (Column(first, "person_bank_accounts").GetProperty("reencrypted").GetInt32(),
            Column(first, "person_bank_accounts").GetProperty("pending").GetInt32(),
            Column(first, "person_bank_accounts").GetProperty("unreadable").GetInt32()));
        Assert.Equal(1, Column(first, "users").GetProperty("reencrypted").GetInt32());

        await WithDb(async db =>
        {
            var accounts = await db.Set<PersonBankAccount>().AsNoTracking().ToDictionaryAsync(a => a.Uid);
            Assert.All(accounts.Values, a => Assert.Equal(2, a.AccountNumberEnc[0]));
            Assert.Equal("012180000000000015", cipher.Decrypt(accounts[oldAccount.Uid].AccountNumberEnc));
            Assert.Equal(newAccount.AccountNumberEnc, accounts[newAccount.Uid].AccountNumberEnc); // already current: untouched
            var mfa = (await db.Set<User>().AsNoTracking().SingleAsync(u => u.Uid == user.Uid)).MfaSecretEnc!;
            Assert.Equal(("JBSWY3DPEHPK3PXP", (byte)2), (cipher.Decrypt(mfa), mfa[0]));
        });

        var again = await Run();
        Assert.All(again.GetProperty("columns").EnumerateArray(), c => Assert.Equal(0, c.GetProperty("reencrypted").GetInt32()));
    }

    [Fact]
    public async Task A_value_under_an_unknown_key_is_counted_and_left_alone()
    {
        var unknown = new byte[1 + 12 + 16 + 18];
        unknown[0] = 9;
        var account = await Account(unknown);

        var result = await Run();
        Assert.Equal((0, 1), (Column(result, "person_bank_accounts").GetProperty("reencrypted").GetInt32(),
            Column(result, "person_bank_accounts").GetProperty("unreadable").GetInt32()));
        var stored = await WithDb(db => db.Set<PersonBankAccount>().AsNoTracking().Where(a => a.Uid == account.Uid)
            .Select(a => a.AccountNumberEnc).SingleAsync());
        Assert.Equal(unknown, stored);
    }

    [Fact]
    public void Previous_keys_are_parsed_and_checked()
    {
        var settings = EncryptionSettings.Parse(RotatedKeyApiFactory.NewKey, "2", $" 1:{RotatedKeyApiFactory.OldKey} ");
        var rotated = new AesGcmFieldCipher(settings);
        var stored = Old.Encrypt("4152313800001237");
        Assert.Equal("4152313800001237", rotated.Decrypt(stored));
        Assert.Equal(2, rotated.Encrypt("4152313800001237")[0]);
        Assert.ThrowsAny<CryptographicException>(() => Old.Decrypt(rotated.Encrypt("x")));

        foreach (var bad in new[] { $"2:{RotatedKeyApiFactory.OldKey}", "1", $"0:{RotatedKeyApiFactory.OldKey}", "1:c2hvcnQ=",
                     $"1:{RotatedKeyApiFactory.OldKey},1:{RotatedKeyApiFactory.OldKey}" })
            Assert.Throws<InvalidOperationException>(() => EncryptionSettings.Parse(RotatedKeyApiFactory.NewKey, "2", bad));
    }
}
