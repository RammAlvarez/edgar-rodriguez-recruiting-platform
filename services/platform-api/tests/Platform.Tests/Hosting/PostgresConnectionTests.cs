using Npgsql;
using Platform.Infrastructure.Persistence;

namespace Platform.Tests.Hosting;

public sealed class PostgresConnectionTests
{
    [Fact]
    public void Database_url_is_converted_to_an_npgsql_connection_string()
    {
        var cs = new NpgsqlConnectionStringBuilder(
            PostgresConnection.ToConnectionString("postgres://platform_api:p%40ss@db.example:6543/platform?sslmode=require"));
        Assert.Equal("db.example", cs.Host);
        Assert.Equal(6543, cs.Port);
        Assert.Equal("platform", cs.Database);
        Assert.Equal("platform_api", cs.Username);
        Assert.Equal("p@ss", cs.Password);
        Assert.Equal(SslMode.Require, cs.SslMode);
    }

    [Fact]
    public void Connection_strings_pass_through() =>
        Assert.Equal("Host=x;Database=y", PostgresConnection.ToConnectionString("Host=x;Database=y"));
}
