using PeekVPN.Infrastructure.Vpn.Platform;
using PeekVPN.Infrastructure.Vpn.WireGuard;

namespace PeekVPN.Core.Tests;

public sealed class WireGuardWindowsSupportTests
{
    [Fact]
    public void Parser_extracts_keys_addresses_and_endpoint()
    {
        var parsed = WireGuardConfigParser.Parse("""
            [Interface]
            PrivateKey = sLd0GOQ6uDna0efwpQKEdP7Ljs2rxMjN0XtDWSbluk4=
            Address = 10.8.0.2/32
            DNS = 1.1.1.1, 1.0.0.1

            [Peer]
            PublicKey = Y5XJGHaOZeVbOcRWMe/A41DUrQ0pn1IwbMWJdik5rGY=
            Endpoint = 104.171.128.186:51820
            AllowedIPs = 0.0.0.0/0
            PersistentKeepalive = 25
            """);

        Assert.Equal(["10.8.0.2/32"], parsed.Addresses);
        Assert.Equal(["1.1.1.1", "1.0.0.1"], parsed.DnsServers);
        Assert.Equal(["0.0.0.0/0"], parsed.AllowedIps);
        Assert.Equal("104.171.128.186:51820", parsed.Endpoint);
        Assert.Equal(25, parsed.PersistentKeepalive);
        Assert.NotNull(parsed.PrivateKey);
        Assert.Equal(32, parsed.PrivateKey!.Length);
        Assert.NotNull(parsed.PeerPublicKey);
        Assert.Equal(32, parsed.PeerPublicKey!.Length);
    }

    [Theory]
    [InlineData("104.171.128.186:51820", "104.171.128.186", 51820)]
    [InlineData("[2001:db8::1]:51820", "2001:db8::1", 51820)]
    public void Endpoint_parser_accepts_ipv4_and_ipv6(string endpoint, string host, int port)
    {
        Assert.True(WireGuardEndpoint.TryParse(endpoint, out var parsedHost, out var parsedPort));
        Assert.Equal(host, parsedHost);
        Assert.Equal(port, parsedPort);
    }

    [Fact]
    public void Blake2s_empty_input_matches_rfc_7693()
    {
        var hash = WireGuardCrypto.Hash([]);
        Assert.Equal("69217a3079908094e11121d042354a7c1f55b6482ca1a51e1b250dfd1ed1e527", Convert.ToHexString(hash).ToLowerInvariant());
    }

    [Fact]
    public void X25519_public_key_matches_rfc_7748_alice()
    {
        var privateKey = Convert.FromHexString("77076d0a7318a57d3c16c17251b26645df4c2f87ebc0992ab177fba51db92c2a");
        var expected = Convert.FromHexString("8520f0098930a754748b7ddcb43ef75a0dbf3a0d26381af4eba4a98eaa9b4e6a");
        Assert.Equal(expected, WireGuardCrypto.PublicFromPrivate(privateKey));
    }

    [Fact]
    public void Replay_window_rejects_duplicates_and_accepts_newer_counters()
    {
        var window = new ReplayWindow();
        Assert.True(window.TryAccept(1));
        Assert.False(window.TryAccept(1));
        Assert.True(window.TryAccept(3));
        Assert.True(window.TryAccept(2));
        Assert.False(window.TryAccept(2));
    }

    [Fact]
    public void Default_route_is_split_into_two_slash_one_prefixes_on_windows()
    {
        Assert.Equal(["0.0.0.0/1", "128.0.0.0/1"], CidrUtil.ExpandDefaultRoute("0.0.0.0/0"));
        Assert.Equal("255.255.255.255", CidrUtil.ToIpv4Mask(32));
        Assert.Equal("255.255.255.0", CidrUtil.ToIpv4Mask(24));
    }
}
