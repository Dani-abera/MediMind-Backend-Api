namespace MediMind.Infrastructure.Services.Notifications;

public static class EthiopiaPhone
{
    /// <summary>
    /// Normalizes Ethiopian mobile numbers to +251XXXXXXXXX for Geez SMS.
    /// </summary>
    public static string Normalize(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Phone number is required.", nameof(phone));

        var p = phone.Trim().Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);

        if (p.StartsWith('+'))
            return p;

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
