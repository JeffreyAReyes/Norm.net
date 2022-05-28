namespace PostgreSqlSerialUnitTests;

public partial class PostgreSqlSerialUnitTest
{
    [Fact]
    public void WithCommentHeader_Parameters_Test()
    {
        // reset to default
        NormOptions.Configure(o => { });

        var expected = new string[]
        {
        "-- @1 integer = 1",
        "-- @2 text = \"foo\"",
        "-- @3 boolean = false",
        "-- @4 timestamp = \"2022-05-19T00:00:00.0000000\"",
        "select @1, @2, @3, @4"
        };
        string actual = "";
        using var connection = new NpgsqlConnection(_DatabaseFixture.ConnectionString);

        connection
            .WithCommentHeader(comment: null, includeCommandAttributes: false, includeParameters: true, includeCallerInfo: false, includeTimestamp: false)
            .WithCommandCallback(c => actual = c.CommandText)
            .WithParameters(1, "foo", false, new DateTime(2022, 5, 19))
            .Execute("select @1, @2, @3, @4");

        Assert.Equal(string.Join(Environment.NewLine, expected), actual);

        var expected2 = new string[]
        {
        "-- @1 integer = 2",
        "-- @2 text = \"bar\"",
        "-- @3 boolean = false",
        "-- @4 timestamp = \"1977-05-19T00:00:00.0000000\"",
        "select @1, @2, @3, @4"
        };

        connection
            .WithCommentParameters()
            .WithCommandCallback(c => actual = c.CommandText)
            .WithParameters(2, "bar", false, new DateTime(1977, 5, 19))
            .Execute("select @1, @2, @3, @4");

        Assert.Equal(string.Join(Environment.NewLine, expected2), actual);
    }
}