using Platform.Bank.Layouts;

namespace Platform.Tests.Bank;

/// <summary>One test per bank rule (SPEC §5.5, §5.7; Banorte «Descripción de campos»), on made-up data.</summary>
public sealed class BankLayoutRuleTests
{
    private static readonly DateOnly Today = new(2026, 10, 16);
    private static readonly HashSet<string> Banks = ["012", "072", "127"];
    private const string Curp = "PEPJ900101HNLRRN09";
    private const string Card = "4111111111111111";
    private const string Clabe = "012180000000000015";

    private static readonly AztecaPayingAccount Payer = new("01234567890123", "MAXIMA REPARACION Y LIMPIEZA SA DE CV", "MRL2407307G7");

    private static BanorteRegistrationRow Banorte(string employee = "2000000020", string curp = Curp,
        string email = "juan.perez@example.com", string mobile = "8112345670", string street = "Av  Constitución",
        string number = "100", string colonia = "Centro", string cp = "64000", string? card = Card,
        string rfc = "PEPJ900101AB1") => new(employee, curp, email, mobile, street, number, colonia, cp, card, rfc);

    private static IReadOnlyList<LayoutIssue> ValidateBanorte(params BanorteRegistrationRow[] rows) =>
        BanorteMassRegistration.Layout(new BanorteBatchSettings()).Validate(rows, Today);

    private static IReadOnlyList<LayoutIssue> ValidateAzteca(params AztecaPaymentRow[] rows) =>
        AztecaMassPayment.Layout(Payer, Banks).Validate(rows, Today);

    [Theory]
    [InlineData("  José   Ñúñez ", "JOSE NUNEZ")]
    [InlineData("peña nieto", "PENA NIETO")]
    [InlineData("Güemes", "GUEMES")]
    [InlineData(null, "")]
    public void Text_is_uppercased_without_accents_and_Ñ_becomes_N(string? input, string expected) =>
        Assert.Equal(expected, BankText.Normalize(input));

    [Fact]
    public void A_valid_Banorte_record_passes_and_is_written_normalized()
    {
        Assert.Empty(ValidateBanorte(Banorte()));
        var layout = BanorteMassRegistration.Layout(new BanorteBatchSettings());
        Assert.Equal("AV CONSTITUCION", layout.Columns.Single(c => c.Letter == "H").Value(Banorte()));
    }

    [Fact]
    public void Special_characters_left_after_normalizing_are_refused() =>
        Assert.Contains(ValidateBanorte(Banorte(street: "Av. Constitución #5")),
            i => i.Field == "Calle" && i.Message.Contains("especiales", StringComparison.Ordinal));

    [Theory]
    [InlineData("PEPJ900101HNLRRN0", "estructura")]
    [InlineData("PEPJ901301HNLRRN09", "estructura")]
    [InlineData("1EPJ900101HNLRRN09", "estructura")]
    [InlineData("PEPJ900101HNERRN09", "extranjero")]
    public void Curp_structure_and_birthplace_are_checked(string curp, string message) =>
        Assert.Contains(ValidateBanorte(Banorte(curp: curp)),
            i => i.Field == "CURP" && i.Message.Contains(message, StringComparison.Ordinal));

    [Theory]
    [InlineData("PEPJ081017HNLRRNA9", true)]   // born 2008-10-17: 17 on 2026-10-16
    [InlineData("PEPJ081016HNLRRNA9", false)]  // turns 18 that day
    [InlineData("PEPJ261017HNLRRN09", false)]  // 1926-10-17: 99
    [InlineData("PEPJ261016HNLRRN09", true)]   // 1926-10-16: 100
    public void The_employee_must_be_18_to_99(string curp, bool rejected) =>
        Assert.Equal(rejected, ValidateBanorte(Banorte(curp: curp))
            .Any(i => i.Message.Contains("mayor de edad", StringComparison.Ordinal)));

    [Theory]
    [InlineData("1111111111")]
    [InlineData("0123456789")]
    [InlineData("9876543210")]
    [InlineData("1234567890")]
    [InlineData("81123456")]
    public void Mobile_must_be_10_digits_not_repeated_nor_consecutive(string mobile) =>
        Assert.Contains(ValidateBanorte(Banorte(mobile: mobile)), i => i.Field == "Celular");

    [Fact]
    public void Lengths_follow_the_bank_sheet()
    {
        var issues = ValidateBanorte(Banorte(street: new string('A', 25), number: "12345678", colonia: new string('B', 41),
            cp: "6400", email: new string('a', 40) + "@example.com", employee: "12345678901"));
        Assert.Equal(["Calle", "Código postal", "Colonia", "Correo electrónico", "Número", "Número de empleado"],
            issues.Select(i => i.Field).Order());
    }

    [Fact]
    public void Card_number_depends_on_the_card_type()
    {
        var generic = BanorteMassRegistration.Layout(new BanorteBatchSettings());
        Assert.Contains(generic.Validate([Banorte(card: null)], Today), i => i.Field == "Número de tarjeta");
        Assert.Contains(generic.Validate([Banorte(card: "4111111111111112")], Today),
            i => i.Field == "Número de tarjeta");

        var named = BanorteMassRegistration.Layout(new BanorteBatchSettings(CardType: BanorteMassRegistration.NamedCard));
        Assert.Contains(named.Validate([Banorte(street: "AV CONSTITUCION")], Today), i => i.Field == "Sucursal a enviar");
        var withBranch = BanorteMassRegistration.Layout(
            new BanorteBatchSettings(CardType: BanorteMassRegistration.NamedCard, BranchToSend: "01234"));
        Assert.Empty(withBranch.Validate([Banorte(street: "AV CONSTITUCION")], Today));
        Assert.Null(withBranch.Columns.Single(c => c.Letter == "N").Value(Banorte()));
    }

    [Fact]
    public void A_third_employee_at_the_same_address_is_rejected()
    {
        var issues = ValidateBanorte(
            Banorte(employee: "1", street: "AV CONSTITUCION"),
            Banorte(employee: "2", street: "av. constitucion".Replace(".", "", StringComparison.Ordinal)),
            Banorte(employee: "3", street: "AV  CONSTITUCIÓN"),
            Banorte(employee: "4", street: "OTRA CALLE"));
        Assert.Equal([3], issues.Where(i => i.Message.Contains("domicilio", StringComparison.Ordinal)).Select(i => i.Line));
    }

    [Fact]
    public void Employee_number_and_curp_cannot_repeat()
    {
        var issues = ValidateBanorte(Banorte(street: "AV CONSTITUCION"), Banorte(street: "OTRA CALLE"));
        Assert.Equal([2], issues.Where(i => i.Message.Contains("repetidos", StringComparison.Ordinal)).Select(i => i.Line));
    }

    [Fact]
    public void More_than_5000_records_must_be_split()
    {
        var rows = Enumerable.Range(1, 5001).Select(i => Banorte(employee: i.ToString(System.Globalization.CultureInfo.InvariantCulture),
            street: "CALLE " + i)).ToArray();
        Assert.Contains(ValidateBanorte(rows), i => i.Line == 0 && i.Message.Contains("5,000", StringComparison.Ordinal));
    }

    [Fact]
    public void Occupation_codes_are_written_as_the_bank_catalog_spells_them()
    {
        Assert.Equal("09-TECNICOS            ", BanorteMassRegistration.Occupation("09-TECNICOS"));
        Assert.Equal("09-TECNICOS            ", BanorteMassRegistration.Occupation("09"));
        Assert.Contains(BanorteMassRegistration.Layout(new BanorteBatchSettings(OccupationCode: "99-OTRO"))
            .Validate([Banorte(street: "AV CONSTITUCION")], Today), i => i.Field == "Código nacional de ocupación");
    }

    [Theory]
    [InlineData(AccountType.DebitCard, Card, "012", null)]
    [InlineData(AccountType.DebitCard, "411111111111111", "012", "16")]
    [InlineData(AccountType.DebitCard, "4111111111111112", "012", "verificador")]
    [InlineData(AccountType.Clabe, Clabe, "012", null)]
    [InlineData(AccountType.Clabe, "012180000000000016", "012", "verificador")]
    [InlineData(AccountType.Clabe, Clabe, "072", "otro banco")]
    [InlineData(AccountType.Clabe, "01218000000000001", "012", "18")]
    [InlineData(AccountType.OwnAccount, "12345678901234", "127", null)]
    [InlineData(AccountType.OwnAccount, "12345678901234567890", "127", null)]
    [InlineData(AccountType.OwnAccount, "1234567890123456", "127", "14 o 20")]
    public void Account_number_matches_its_type(AccountType type, string number, string bank, string? message)
    {
        var issue = AztecaMassPayment.AccountIssue(type, number, bank);
        if (message is null)
        {
            Assert.Null(issue);
        }
        else
        {
            Assert.Contains(message, issue, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("0", "mayor a cero")]
    [InlineData("-1", "mayor a cero")]
    [InlineData("100.005", "centavos")]
    public void Azteca_amount_is_positive_and_already_truncated_to_cents(string amount, string message) =>
        Assert.Contains(ValidateAzteca(new AztecaPaymentRow("012", AccountType.Clabe, Clabe, "JUAN PEREZ",
                decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture))),
            i => i.Field == "IMPORTE" && i.Message.Contains(message, StringComparison.Ordinal));

    [Fact]
    public void Azteca_refuses_unknown_banks_and_long_names()
    {
        var issues = ValidateAzteca(
            new AztecaPaymentRow("999", AccountType.DebitCard, Card, "JUAN PEREZ", 100m),
            new AztecaPaymentRow("012", AccountType.Clabe, Clabe, new string('X', 41), 100m));
        Assert.Equal([(1, "CLAVE DEL BANCO RECEPTOR"), (2, "NOMBRE DEL BENEFICIARIO")], issues.Select(i => (i.Line, i.Field)));
    }

    [Fact]
    public void Payment_type_is_derived_from_the_receiving_bank()
    {
        Assert.Equal("3", AztecaMassPayment.PaymentType("127"));
        Assert.Equal("1", AztecaMassPayment.PaymentType("012"));
    }

    [Fact]
    public void An_invalid_batch_is_never_written()
    {
        using var stream = new MemoryStream();
        var ex = Assert.Throws<BankFileInvalidException>(() => BankFileWriter.Write(AztecaMassPayment.Layout(Payer, Banks),
            [new AztecaPaymentRow("012", AccountType.Clabe, Clabe, "JUAN PEREZ", 0m)], Today, stream));
        Assert.Single(ex.Issues);
        Assert.Equal(0, stream.Length);
        Assert.Throws<BankFileInvalidException>(() =>
            BankFileWriter.Write(AztecaMassPayment.Layout(Payer, Banks), [], Today, stream));
    }

    [Fact]
    public void Banregio_checks_states_and_birth_date_against_the_curp()
    {
        var row = new BanregioRegistrationRow("3000000001", "Juan", "Pérez", "", "PEPJ900101AB1", Curp, "Calle 1", "10",
            "Centro", "64000", "8112345670", new DateOnly(1990, 1, 1), Gender.Male, "Técnico", "juan@example.com", Card,
            "Nuevo León");
        var layout = BanregioRegistration.Layout();
        Assert.Empty(layout.Validate([row], Today));
        Assert.Equal(["Estado de nacimiento", "FechaNac"], layout.Validate(
            [row with { BirthState = "TEXAS", BirthDate = new DateOnly(1990, 1, 2) }], Today).Select(i => i.Field).Order());
    }

    [Fact]
    public void Only_debit_cards_are_registered_as_frequent_accounts_until_Q13_is_answered()
    {
        var layout = AztecaFrequentAccounts.Layout(Banks);
        Assert.Empty(layout.Validate([new FrequentAccountRow("hfc", "JUAN PEREZ", AccountType.DebitCard, Card, "012")], Today));
        Assert.Equal([1, 2], layout.Validate([
            new FrequentAccountRow("hfc", "JUAN PEREZ", AccountType.Clabe, Clabe, "012"),
            new FrequentAccountRow("hfc", "JUAN PEREZ", AccountType.OwnAccount, "12345678901234", "127"),
        ], Today).Select(i => i.Line));
    }
}
