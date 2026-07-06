namespace BankingApi.Currency;

/// <summary>
/// ISO 4217 codes supported by the Treasury FX adapter (USD plus mapped foreign currencies).
/// </summary>
public static class SupportedCurrencies
{
    /// <summary>ISO 4217 → Treasury API country_currency_desc.</summary>
    private static readonly Dictionary<string, string> IsoToTreasury =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["AUD"] = "Australia-Dollar",
            ["EUR"] = "Euro Zone-Euro",
            ["GBP"] = "United Kingdom-Pound",
            ["CAD"] = "Canada-Dollar",
            ["JPY"] = "Japan-Yen",
            ["NZD"] = "New Zealand-Dollar",
            ["CHF"] = "Switzerland-Franc",
            ["SGD"] = "Singapore-Dollar",
            ["HKD"] = "Hong Kong-Dollar",
            ["MXN"] = "Mexico-Peso",
            ["CNY"] = "China-Renminbi",
            ["KRW"] = "Korea-Won",
            ["BRL"] = "Brazil-Real",
            ["INR"] = "India-Rupee",
            ["SEK"] = "Sweden-Krona",
            ["NOK"] = "Norway-Krone",
            ["DKK"] = "Denmark-Krone",
            ["THB"] = "Thailand-Baht",
            ["MYR"] = "Malaysia-Ringgit",
        };

    public static bool IsSupported(string? isoCode) =>
        !string.IsNullOrWhiteSpace(isoCode) &&
        (string.Equals(isoCode, "USD", StringComparison.OrdinalIgnoreCase) ||
         IsoToTreasury.ContainsKey(isoCode));

    /// <summary>
    /// Returns the normalised ISO code or throws if the currency is not supported.
    /// </summary>
    public static string Validate(string isoCode)
    {
        if (!IsSupported(isoCode))
        {
            throw new ArgumentException(
                $"Currency '{isoCode}' is not supported. Use ISO 4217 codes (e.g. AUD, EUR, GBP, USD). " +
                "See README for the full list of supported currencies.");
        }

        return isoCode.ToUpperInvariant();
    }

    public static bool TryGetTreasuryName(string isoCode, out string treasuryName)
    {
        treasuryName = string.Empty;
        if (string.Equals(isoCode, "USD", StringComparison.OrdinalIgnoreCase))
            return false;

        return IsoToTreasury.TryGetValue(isoCode, out treasuryName!);
    }
}
