namespace SONA.Services
{
    /// <summary>
    /// Converts ISO 3166-1 alpha-2 country codes to emoji flag characters.
    /// Uses Unicode regional indicator symbols (U+1F1E6..U+1F1FF).
    /// Returns 🏳 (white flag) for unknown or empty codes.
    /// </summary>
    public static class CountryFlags
    {
        /// <summary>Get the emoji flag for a 2-letter ISO country code.</summary>
        public static string Get(string? countryCode)
        {
            if (string.IsNullOrWhiteSpace(countryCode) || countryCode.Length < 2)
                return "🏳";

            var code = countryCode.Trim().ToUpperInvariant();
            if (code.Length < 2 || code[0] < 'A' || code[0] > 'Z' || code[1] < 'A' || code[1] > 'Z')
                return "🏳";

            // Each letter maps to a regional indicator symbol: 'A' → U+1F1E6, 'B' → U+1F1E7, etc.
            return string.Concat(
                char.ConvertFromUtf32(0x1F1E6 + (code[0] - 'A')),
                char.ConvertFromUtf32(0x1F1E6 + (code[1] - 'A'))
            );
        }
    }
}
