using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Platform.Core.Audit;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Tests.Support;

namespace Platform.Tests.People;

/// <summary>SPEC §3.1 people registry: capture rules, CURP/RFC duplicate detection and the profile read log.</summary>
public sealed class PeopleTests(ApiFactory factory) : ApiTest(factory)
{
    public const string Curp = "PEPJ900101HNLRRN09";

    public static object NewPerson(string? curp = Curp, string? rfc = "PEPJ900101AB1", string? birthDate = null) => new
    {
        firstNames = " juan  carlos ",
        lastNamePaternal = "pérez",
        lastNameMaternal = "lópez",
        curp,
        rfc,
        birthDate,
        mobilePhone = "8112345670",
        email = "Juan.Perez@Example.com",
        isForeign = false,
    };

    [Fact]
    public async Task Names_are_stored_uppercase_and_birth_date_and_sex_come_from_the_curp()
    {
        var person = await BodyOf(await (await AsOwner()).PostAsJsonAsync("/v1/people", NewPerson()));

        Assert.Equal("JUAN CARLOS", person.GetProperty("firstNames").GetString());
        Assert.Equal("PÉREZ LÓPEZ JUAN CARLOS", person.GetProperty("fullName").GetString());
        Assert.Equal("1990-01-01", person.GetProperty("birthDate").GetString());
        Assert.Equal("male", person.GetProperty("gender").GetString());
        Assert.Equal("juan.perez@example.com", person.GetProperty("email").GetString());
    }

    [Fact]
    public async Task An_enum_value_of_zero_is_stored_and_not_replaced_by_the_column_default()
    {
        // D-005: EF omitted a column holding the CLR default (Gender.Male = 0) and the database default «unspecified» won.
        var person = await BodyOf(await (await AsOwner()).PostAsJsonAsync("/v1/people",
            new { firstNames = "Ana", lastNamePaternal = "Leal", gender = "male", isForeign = false }));

        Assert.Equal("male", person.GetProperty("gender").GetString());
    }

    [Fact]
    public async Task A_birth_date_that_contradicts_the_curp_is_refused()
    {
        var response = await (await AsOwner()).PostAsJsonAsync("/v1/people", NewPerson(birthDate: "1990-01-02"));

        Assert.Equal(["La fecha de nacimiento no coincide con la CURP."], (await ErrorsOf(response))["birthDate"]);
    }

    [Theory]
    [InlineData("PEPJ900101XNLRRN09", "curp")]
    [InlineData("PEPJ901301HNLRRN09", "curp")]
    [InlineData("PEP900101", "rfc")]
    public async Task Malformed_identifiers_are_400(string value, string field)
    {
        var body = field == "curp" ? NewPerson(curp: value) : NewPerson(rfc: value);
        Assert.Contains(field, (await ErrorsOf(await (await AsOwner()).PostAsJsonAsync("/v1/people", body))).Keys);
    }

    [Fact]
    public async Task A_second_person_with_the_same_curp_or_rfc_is_a_409_naming_the_first()
    {
        var admin = await AsOwner();
        await admin.PostAsJsonAsync("/v1/people", NewPerson());

        var sameCurp = await admin.PostAsJsonAsync("/v1/people", NewPerson(rfc: null));
        Assert.Equal(HttpStatusCode.Conflict, sameCurp.StatusCode);
        Assert.Equal("Ya existe una persona con esa CURP: PÉREZ LÓPEZ JUAN CARLOS.", await DetailOf(sameCurp));

        var sameRfc = await admin.PostAsJsonAsync("/v1/people", NewPerson(curp: null));
        Assert.Equal("Ya existe una persona con ese RFC: PÉREZ LÓPEZ JUAN CARLOS.", await DetailOf(sameRfc));
    }

    [Fact]
    public async Task Duplicate_lookup_reports_what_matched_including_deleted_people()
    {
        var admin = await AsOwner();
        var id = (await BodyOf(await admin.PostAsJsonAsync("/v1/people", NewPerson()))).GetProperty("id").GetGuid();

        var found = Assert.Single((await BodyOf(await admin.GetAsync($"/v1/people/duplicates?curp={Curp.ToLowerInvariant()}&rfc=PEPJ900101AB1")))
            .EnumerateArray());
        Assert.Equal(["curp", "rfc"], found.GetProperty("matchedOn").EnumerateArray().Select(m => m.GetString()));
        Assert.False(found.GetProperty("isDeleted").GetBoolean());
        Assert.Empty((await BodyOf(await admin.GetAsync($"/v1/people/duplicates?curp={Curp}&excludeId={id}"))).EnumerateArray());

        await WithDb(async db =>
        {
            var p = await db.Set<Person>().SingleAsync(x => x.Uid == id);
            p.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });
        var deleted = Assert.Single((await BodyOf(await admin.GetAsync($"/v1/people/duplicates?curp={Curp}"))).EnumerateArray());
        Assert.True(deleted.GetProperty("isDeleted").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/v1/people", NewPerson(rfc: null))).StatusCode);
    }

    [Fact]
    public async Task Reading_a_profile_is_logged()
    {
        var admin = await AsOwner();
        var id = (await BodyOf(await admin.PostAsJsonAsync("/v1/people", NewPerson()))).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/v1/people/{id}")).StatusCode);

        var log = await WithDb(db => db.Set<DataAccessEntry>().Where(e => e.PersonId == id).ToListAsync());
        Assert.Equal(DataAccessKind.ViewProfile, Assert.Single(log).Access);
    }

    [Fact]
    public async Task Search_matches_curp_phone_and_part_of_the_name()
    {
        var admin = await AsOwner();
        await admin.PostAsJsonAsync("/v1/people", NewPerson());

        foreach (var q in new[] { Curp, "8112345670", "lópez juan" })
            Assert.Single((await BodyOf(await admin.GetAsync($"/v1/people?search={Uri.EscapeDataString(q)}"))).EnumerateArray());
        Assert.Empty((await BodyOf(await admin.GetAsync("/v1/people?search=GARZA"))).EnumerateArray());
    }

    [Fact]
    public async Task Clients_never_use_the_registry_and_recruiters_reach_only_their_candidates()
    {
        var client = await As(UserRole.Client);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/v1/people")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/v1/people", NewPerson())).StatusCode);

        // PLAN D73: search and create are open so «Nuevo candidato» can find or add a person; the profile is scoped.
        var id = (await BodyOf(await (await AsOwner()).PostAsJsonAsync("/v1/people", NewPerson()))).GetProperty("id").GetGuid();
        var recruiter = await As(UserRole.Recruiter);
        Assert.Single((await BodyOf(await recruiter.GetAsync($"/v1/people?search={Curp}"))).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await recruiter.GetAsync($"/v1/people/{id}")).StatusCode);
    }
}
