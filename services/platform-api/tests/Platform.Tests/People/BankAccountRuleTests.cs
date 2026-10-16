using System.Security.Cryptography;
using System.Text;
using Platform.Core.People;
using Platform.Core.Security;

namespace Platform.Tests.People;

/// <summary>Account numbers at capture (PLAN D82) and their encryption (DATA_MODEL §3.7).</summary>
public sealed class BankAccountRuleTests
{
    private static readonly EncryptionSettings Key = EncryptionSettings.Parse(Convert.ToBase64String(new byte[32]), "1");

    [Fact]
    public void A_number_is_stored_encrypted_with_a_random_nonce_and_reads_back()
    {
        var cipher = new AesGcmFieldCipher(Key);
        var a = cipher.Encrypt("4152313800001237");
        var b = cipher.Encrypt("4152313800001237");

        Assert.NotEqual(a, b);
        Assert.DoesNotContain("4152313800001237", Encoding.Latin1.GetString(a));
        Assert.Equal(1, a[0]); // key id in front
        Assert.Equal("4152313800001237", cipher.Decrypt(a));
    }

    [Fact]
    public void A_tampered_value_or_another_key_never_decrypts()
    {
        var stored = new AesGcmFieldCipher(Key).Encrypt("012580015000000011");
        var tampered = (byte[])stored.Clone();
        tampered[^1] ^= 1;
        Assert.ThrowsAny<CryptographicException>(() => new AesGcmFieldCipher(Key).Decrypt(tampered));

        var other = new AesGcmFieldCipher(EncryptionSettings.Parse(Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray()), "2"));
        Assert.ThrowsAny<CryptographicException>(() => other.Decrypt(stored));
    }

    [Theory]
    [InlineData("c2hvcnQ=", null)] // 5 bytes
    [InlineData("not base64!", null)]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=", "0")]
    public void A_bad_key_stops_the_api_at_start(string key, string? id) =>
        Assert.Throws<InvalidOperationException>(() => EncryptionSettings.Parse(key, id));

    [Theory]
    [InlineData(BankAccountType.DebitCard, "4152 3138 0000 1237", "127", null)]
    [InlineData(BankAccountType.DebitCard, "4152313800001238", "127", "dígito verificador")]
    [InlineData(BankAccountType.DebitCard, "415231380000123", "127", "16 dígitos")]
    [InlineData(BankAccountType.Clabe, "012580015000000011", "012", null)]
    [InlineData(BankAccountType.Clabe, "012580015000000012", "012", "dígito verificador")]
    [InlineData(BankAccountType.Clabe, "072580015000000019", "012", "otro banco")]
    [InlineData(BankAccountType.OwnAccount, "12345678901234", "127", null)]
    [InlineData(BankAccountType.OwnAccount, "123456789012345", "127", "14 o 20")]
    public void Numbers_are_checked_by_type(BankAccountType type, string number, string bank, string? problem)
    {
        var issue = AccountNumbers.Issue(type, AccountNumbers.Digits(number), bank);
        if (problem is null)
            Assert.Null(issue);
        else
            Assert.Contains(problem, issue);
    }

    [Theory]
    [InlineData("PÉREZ LÓPEZ ANA", "Ana Perez Lopez", true)]
    [InlineData("LÓPEZ GARZA MARÍA", "PÉREZ LÓPEZ ANA", false)]
    public void A_titular_is_a_third_party_only_when_the_name_differs(string holder, string person, bool same) =>
        Assert.Equal(same, BankAccounts.SameName(holder, person));
}
