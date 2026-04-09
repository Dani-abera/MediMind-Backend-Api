using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediMind.Domain.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Services.ML
{

// ─── ML Prediction HTTP Client ────────────────────────────────────────────────

    /// <summary>
    /// HTTP client that calls the Python Flask ML microservice at /ml/predict.
    /// Configured with a named HttpClient ("MlService") in DI.
    /// </summary>
    public class MlPredictionService(
        IHttpClientFactory httpClientFactory,
        ILogger<MlPredictionService> logger)
        : IMlPredictionService
    {
        public async Task<MlPredictionResult> PredictAsync(
            MlPredictionRequest request, CancellationToken ct = default)
        {
            var client = httpClientFactory.CreateClient("MlService");

            try
            {
                var payload = new
                {
                    avg_systolic_bp = request.AvgSystolicBp,
                    avg_diastolic_bp = request.AvgDiastolicBp,
                    avg_glucose_level = request.AvgGlucoseLevel,
                    current_bmi = request.CurrentBmi,
                    weight_change = request.WeightChange,
                    age = request.Age,
                    gender = request.Gender,
                    data_points_count = request.DataPointsCount
                };

                var response = await client.PostAsJsonAsync("/ml/predict", payload, ct);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<MlApiResponse>(cancellationToken: ct)
                             ?? throw new InvalidOperationException("ML service returned null response.");

                return new MlPredictionResult(
                    result.DiabetesRisk,
                    result.HypertensionRisk,
                    result.CvdRisk,
                    result.Confidence,
                    result.ContributingFactors,
                    result.Recommendations,
                    result.ModelVersion);
            }
            catch (HttpRequestException ex)
            {
                logger.LogError(ex, "ML service unavailable. Request failed.");
                throw new MediMind.Domain.Common.Interfaces.MlServiceUnavailableException();
            }
        }

        private record MlApiResponse(
            double DiabetesRisk,
            double HypertensionRisk,
            double CvdRisk,
            double Confidence,
            Dictionary<string, List<string>> ContributingFactors,
            string Recommendations,
            string ModelVersion);
    }
}

namespace MediMind.Domain.Common.Interfaces
{
    public class MlServiceUnavailableException()
        : Exception(
            "AI prediction service is temporarily unavailable. Your request is saved and you'll be notified when ready.");
}

namespace MediMind.Infrastructure.Services.Payment
{
using MediMind.Domain.Common.Interfaces;

// ─── Chapa Payment Service ────────────────────────────────────────────────────

    /// <summary>
    /// Integrates with Chapa Payment Gateway (ETB, 2.5% per transaction).
    /// Uses named HttpClient "ChapaClient".
    /// </summary>
    public class ChapaPaymentService(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<ChapaPaymentService> logger)
        : MediMind.Domain.Common.Interfaces.IPaymentService
    {
        private readonly string _secretKey = config["Chapa:SecretKey"]
                                             ?? throw new InvalidOperationException(
                                                 "Chapa:SecretKey is not configured.");

        public async Task<ChapaPaymentInitResponse> InitiateAsync(
            ChapaPaymentRequest request, CancellationToken ct = default)
        {
            var client = httpClientFactory.CreateClient("ChapaClient");

            var payload = new
            {
                amount = request.Amount.ToString("F2"),
                currency = request.Currency,
                email = request.Email,
                first_name = request.FirstName,
                last_name = request.LastName,
                phone_number = request.PhoneNumber,
                tx_ref = request.TxRef,
                callback_url = request.CallbackUrl,
                return_url = request.ReturnUrl,
                description = request.Description
            };

            try
            {
                var response = await client.PostAsJsonAsync("v1/transaction/initialize", payload, ct);
                var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

                if (response.IsSuccessStatusCode && content.TryGetProperty("data", out var data))
                {
                    var checkoutUrl = data.GetProperty("checkout_url").GetString();
                    return new ChapaPaymentInitResponse(true, checkoutUrl, null);
                }

                var message = content.TryGetProperty("message", out var msg)
                    ? msg.GetString()
                    : "Payment initiation failed.";
                return new ChapaPaymentInitResponse(false, null, message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Chapa payment initiation failed for tx_ref: {TxRef}", request.TxRef);
                return new ChapaPaymentInitResponse(false, null, "Payment service temporarily unavailable.");
            }
        }

        public async Task<bool> VerifyWebhookAsync(string payload, string signature, CancellationToken ct = default)
        {
            // HMAC-SHA256 webhook signature verification (NFR-006 Payment Security)
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var computedSignature = Convert.ToHexString(
                hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

            return computedSignature == signature.ToLowerInvariant();
        }

        public async Task<ChapaTransactionStatus> GetTransactionStatusAsync(
            string txRef, CancellationToken ct = default)
        {
            var client = httpClientFactory.CreateClient("ChapaClient");
            var response = await client.GetAsync($"v1/transaction/verify/{txRef}", ct);
            var content = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

            if (response.IsSuccessStatusCode && content.TryGetProperty("data", out var data))
            {
                var status = data.GetProperty("status").GetString() ?? "unknown";
                var amount = data.TryGetProperty("amount", out var amt) ? (decimal?)amt.GetDecimal() : null;
                return new ChapaTransactionStatus(status, txRef, amount);
            }

            return new ChapaTransactionStatus("failed", txRef, null);
        }
    }
}
