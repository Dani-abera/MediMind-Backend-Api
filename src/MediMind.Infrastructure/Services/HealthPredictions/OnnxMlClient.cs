using MediMind.Application.Features.HealthPredictions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace MediMind.Infrastructure.Services.HealthPredictions;

public sealed class OnnxMlClient : IMlServiceClient, IDisposable
{
    private readonly InferenceSession _diabetes;
    private readonly InferenceSession _hypertension;
    private readonly InferenceSession _cvd;
    private readonly ILogger<OnnxMlClient> _logger;
    private const int FeatureCount = 14;
    private const string ModelVersion = "onnx-v1.0";

    public OnnxMlClient(
        IOptions<MlServiceOptions> options,
        IHostEnvironment env,
        ILogger<OnnxMlClient> logger)
    {
        _logger = logger;
        var dir = Path.Combine(env.ContentRootPath, options.Value.ModelDirectory);
        var sessionOpts = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            ExecutionMode          = ExecutionMode.ORT_SEQUENTIAL,
        };

        _diabetes     = new InferenceSession(Path.Combine(dir, "diabetes_model.onnx"),     sessionOpts);
        _hypertension = new InferenceSession(Path.Combine(dir, "hypertension_model.onnx"), sessionOpts);
        _cvd          = new InferenceSession(Path.Combine(dir, "cvd_model.onnx"),          sessionOpts);

        logger.LogInformation("ONNX models loaded from {Dir}", dir);
    }

    public Task<MLPredictionResponse?> PredictAsync(MLPredictionRequest r)
    {
        try
        {
            // Feature order MUST match training notebook FEATURE_SCHEMA exactly.
            // [PatientAge, Gender, TotalDaysOfData, AvgSystolicBp, AvgDiastolicBp,
            //  BpVariability, AvgGlucose, GlucoseStdDev, GlucoseTrend, CurrentBmi,
            //  WeightChange, AvgHeartRate, AvgTemperature, MetabolicScore]
            var features = new float[FeatureCount]
            {
                r.PatientAge,
                r.Gender.Equals("Male", StringComparison.OrdinalIgnoreCase) ? 1f : 0f,
                r.TotalDaysOfData,
                (float)(r.AverageSystolicBp  ?? 0),
                (float)(r.AverageDiastolicBp ?? 0),
                (float)(r.BpVariability      ?? 0),
                (float)(r.AverageGlucose     ?? 0),
                (float)(r.GlucoseStdDev      ?? 0),
                (float)(r.GlucoseTrend       ?? 0),
                (float)(r.CurrentBmi         ?? 0),
                (float)(r.WeightChange       ?? 0),
                (float)(r.AverageHeartRate   ?? 0),
                (float)(r.AverageTemperature ?? 0),
                (float)(r.MetabolicScore     ?? 0),
            };

            var diabetesRisk     = RunOne(_diabetes,     features);
            var hypertensionRisk = RunOne(_hypertension, features);
            var cvdRisk          = RunOne(_cvd,          features);
            var risks            = new[] { diabetesRisk, hypertensionRisk, cvdRisk };

            return Task.FromResult<MLPredictionResponse?>(new MLPredictionResponse(
                DiabetesRisk:        diabetesRisk,
                HypertensionRisk:    hypertensionRisk,
                CvdRisk:             cvdRisk,
                Confidence:          ComputeConfidence(r),
                ModelVersion:        ModelVersion,
                ContributingFactors: BuildContributingFactors(r, risks),
                Recommendations:     BuildRecommendations(r, risks),
                DataPointsUsed:      r.TotalDaysOfData));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ONNX inference failed.");
            return Task.FromResult<MLPredictionResponse?>(null);
        }
    }

    // Each model outputs two tensors: labels [N] and probabilities [N, 2].
    // We find the probabilities output by name containing "prob",
    // then read probs[1] = P(positive class) and convert to 0–100 %.
    private static double RunOne(InferenceSession session, float[] features)
    {
        var tensor    = new DenseTensor<float>(features, [1, FeatureCount]);
        var inputName = session.InputMetadata.Keys.First();
        var inputs    = new[] { NamedOnnxValue.CreateFromTensor(inputName, tensor) };

        using var results = session.Run(inputs);
        var probs = results
            .First(r => r.Name.Contains("prob", StringComparison.OrdinalIgnoreCase))
            .AsEnumerable<float>()
            .ToArray();

        // probs is flattened [neg_prob, pos_prob]; pos_prob * 100 = risk %
        return Math.Clamp(probs[1] * 100.0, 0, 100);
    }

    private static double ComputeConfidence(MLPredictionRequest r)
    {
        double base_ = r.TotalDaysOfData >= 30 ? 85 : r.TotalDaysOfData >= 7 ? 60 : 30;
        if (r.AverageGlucose is null)    base_ -= 10;
        if (r.AverageSystolicBp is null) base_ -= 10;
        return Math.Clamp(base_, 20, 95);
    }

    private static Dictionary<string, List<string>> BuildContributingFactors(
        MLPredictionRequest r, double[] risks)
    {
        var factors = new Dictionary<string, List<string>>();

        if (risks[0] > 30)
        {
            var d = new List<string>();
            if (r.AverageGlucose >= 100) d.Add("Elevated blood glucose");
            if (r.CurrentBmi >= 25)      d.Add("Elevated BMI");
            if (r.PatientAge >= 45)      d.Add("Age risk factor");
            if (r.GlucoseTrend > 0.3)   d.Add("Rising glucose trend");
            if (d.Count > 0) factors["Diabetes"] = d;
        }

        if (risks[1] > 30)
        {
            var h = new List<string>();
            if (r.AverageSystolicBp >= 130)  h.Add("Elevated systolic pressure");
            if (r.AverageDiastolicBp >= 80)  h.Add("Elevated diastolic pressure");
            if (r.BpVariability > 15)        h.Add("High blood pressure variability");
            if (r.PatientAge >= 50)          h.Add("Age risk factor");
            if (h.Count > 0) factors["Hypertension"] = h;
        }

        if (risks[2] > 30)
        {
            var c = new List<string>();
            if (r.AverageSystolicBp >= 130) c.Add("Elevated blood pressure");
            if (r.AverageGlucose >= 100)    c.Add("Elevated glucose");
            if (r.CurrentBmi >= 30)         c.Add("Obesity");
            if (r.PatientAge >= 55)         c.Add("Age risk factor");
            if (c.Count > 0) factors["Cardiovascular"] = c;
        }

        return factors;
    }

    private static string BuildRecommendations(MLPredictionRequest r, double[] risks)
    {
        var recs = new List<string>();

        if (risks[0] > 50)      recs.Add("Monitor blood glucose regularly and consult your doctor about diabetes screening.");
        else if (risks[0] > 30) recs.Add("Maintain a low-sugar diet and monitor glucose levels.");

        if (risks[1] > 50)      recs.Add("Seek medical attention for blood pressure management.");
        else if (risks[1] > 30) recs.Add("Reduce sodium intake and monitor blood pressure at home.");

        if (risks[2] > 50)      recs.Add("Schedule a cardiovascular assessment with your doctor.");
        else if (risks[2] > 30) recs.Add("Increase physical activity and maintain a heart-healthy diet.");

        if (r.CurrentBmi >= 30) recs.Add("Aim for gradual weight reduction through diet and exercise.");

        return recs.Count > 0
            ? string.Join(" ", recs)
            : "Your health indicators look stable. Maintain regular check-ups and a healthy lifestyle.";
    }

    public void Dispose()
    {
        _diabetes.Dispose();
        _hypertension.Dispose();
        _cvd.Dispose();
    }
}
