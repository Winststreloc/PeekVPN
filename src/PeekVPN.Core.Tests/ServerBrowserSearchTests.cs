using PeekVPN.App.ViewModels;
using PeekVPN.Contracts;

namespace PeekVPN.Core.Tests;

public sealed class ServerBrowserSearchTests
{
    private static readonly ServerItemViewModel Server = new(
        new VpnServerDto(
            "us-nyc-1",
            "New York",
            "United States",
            "US",
            24,
            "Manhattan",
            40.7128,
            -74.0060));

    [Theory]
    [InlineData("united")]
    [InlineData("new york")]
    [InlineData("manhattan")]
    [InlineData("us")]
    public void MatchesSearch_matches_every_server_search_field_case_insensitively(string query)
    {
        Assert.True(ServerBrowserViewModel.MatchesSearch(Server, query.ToUpperInvariant()));
    }

    [Fact]
    public void MatchesSearch_trims_the_query_and_rejects_unrelated_text()
    {
        Assert.True(ServerBrowserViewModel.MatchesSearch(Server, "  New York  "));
        Assert.False(ServerBrowserViewModel.MatchesSearch(Server, "Berlin"));
    }
}
