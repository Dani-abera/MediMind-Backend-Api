using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;

namespace MediMind.Infrastructure.Services.Notifications;

public static class FirebaseBootstrap
{
    /// <summary>Initializes the default <see cref="FirebaseApp"/> from JSON credentials when configured.</summary>
    public static void Initialize(IConfiguration config)
    {
        if (FirebaseApp.DefaultInstance is not null)
            return;

        var json = config["Firebase:ServiceAccountJson"];
        if (string.IsNullOrWhiteSpace(json))
        {
            var path = config["Firebase:ServiceAccountPath"];
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                json = File.ReadAllText(path);
        }

        if (string.IsNullOrWhiteSpace(json))
            return;

        var trimmed = json.TrimStart();
        if (trimmed.StartsWith('{'))
        {
            FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromJson(json) });
            return;
        }

        if (File.Exists(json))
            FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(json) });
    }
}
