using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Platform.Core.Http;

namespace Platform.Tests.Hosting;

/// <summary>X-Invalidates on every response (errors included), 400 field issues, decimals as strings.</summary>
public sealed class HttpConventionTests : IAsyncLifetime
{
    public sealed record Payment(string Name, decimal Amount);

    public sealed class PaymentValidator : AbstractValidator<Payment>
    {
        public PaymentValidator() => RuleFor(p => p.Amount).GreaterThan(0).WithMessage("El importe debe ser mayor a cero.");
    }

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new DecimalStringJsonConverter()));
        builder.Services.AddScoped<IValidator<Payment>, PaymentValidator>();
        builder.Services.AddProblemDetails();
        _app = builder.Build();
        _app.UseRouting();
        _app.UseExceptionHandler();
        _app.UseInvalidatesHeader();
        _app.MapPost("/payments", (Payment p) => TypedResults.Ok(p)).Validate<Payment>().Invalidates("payments", "periods");
        _app.MapPost("/fails", IResult () => throw new InvalidOperationException("boom")).Invalidates("periods");
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task Success_carries_the_tags_and_decimals_round_trip_as_strings()
    {
        var response = await _client.PostAsync("/payments", Json("""{"name":"A","amount":"1234.5678"}"""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("payments,periods", response.Headers.GetValues(InvalidatesExtensions.HeaderName).Single());
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.String, body.GetProperty("amount").ValueKind);
        Assert.Equal("1234.5678", body.GetProperty("amount").GetString());
    }

    [Fact]
    public async Task Validation_failure_returns_400_field_messages_and_still_carries_the_tags()
    {
        var response = await _client.PostAsync("/payments", Json("""{"name":"A","amount":"0"}"""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("payments,periods", response.Headers.GetValues(InvalidatesExtensions.HeaderName).Single());
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.Equal("El importe debe ser mayor a cero.", errors.GetProperty("amount")[0].GetString());
    }

    [Fact]
    public async Task Unhandled_error_still_carries_the_tags()
    {
        var response = await _client.PostAsync("/fails", null);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("periods", response.Headers.GetValues(InvalidatesExtensions.HeaderName).Single());
    }

    [Fact]
    public async Task Money_sent_as_a_json_number_is_rejected()
    {
        var response = await _client.PostAsync("/payments", Json("""{"name":"A","amount":1234.56}"""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("Amount", "amount")]
    [InlineData("Accounts[0].HolderName", "accounts[0].holderName")]
    public void Field_paths_are_camel_case(string property, string expected) =>
        Assert.Equal(expected, ValidationFields.Path(property));

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");
}
