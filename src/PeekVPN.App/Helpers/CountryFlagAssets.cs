namespace PeekVPN.App.Helpers;

/// <summary>
/// Resolves ISO 3166-1 alpha-2 country codes to flag SVG assets
/// from <c>Assets/Flags/{code}.svg</c> (lowercase), matching
/// https://github.com/hampusborgos/country-flags.
/// </summary>
public static class CountryFlagAssets
{
    public static Uri? GetUri(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length != 2)
        {
            return null;
        }

        var code = countryCode.Trim().ToLowerInvariant();
        if (code[0] is < 'a' or > 'z' || code[1] is < 'a' or > 'z')
        {
            return null;
        }

        return new Uri($"avares://PeekVPN/Assets/Flags/{code}.svg");
    }
}
