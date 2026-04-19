namespace MediMind.Infrastructure.Services.Notifications;

public class GeezSmsOptions
{
    public const string SectionName = "GeezSms";

    public string ApiKey { get; set; } = string.Empty;
    public string SenderId { get; set; } = "MediMind";
    public string BaseUrl { get; set; } = "https://api.geezsms.com/api/v1";
}
