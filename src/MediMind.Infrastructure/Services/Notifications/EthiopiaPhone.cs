namespace MediMind.Infrastructure.Services.Notifications;

public static class EthiopiaPhone
{
    /// <summary>
    /// Normalizes Ethiopian mobile numbers to 251XXXXXXXXX (no + prefix) for Geez SMS Gateway.
    /// Geez SMS rejects the + prefix — always returns the bare country-code form.
    /// </summary>
    public static string Normalize(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Phone number is required.", nameof(phone));

        var p = phone.Trim()
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal);

        // Strip leading + unconditionally — Geez SMS requires 251XXXXXXXXX, not +251XXXXXXXXX
        if (p.StartsWith('+'))
            p = p[1..];

        if (p.StartsWith("09", StringComparison.Ordinal) && p.Length >= 10)
            return "251" + p[1..];

        if (p.StartsWith("07", StringComparison.Ordinal) && p.Length >= 10)
            return "251" + p[1..];

        if (p.StartsWith("251", StringComparison.Ordinal))
            return p;

        if (p.StartsWith('9') && p.Length == 9)
            return "251" + p;

        if (p.StartsWith('7') && p.Length == 9)
            return "251" + p;

        return p;
    }
}
