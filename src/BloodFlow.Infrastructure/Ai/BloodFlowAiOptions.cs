namespace BloodFlow.Infrastructure.Ai
{
    public class BloodFlowAiOptions
    {
        public const string SectionName = "BloodFlowAi";

        /// <summary>Address of the AI service, e.g. http://127.0.0.1:8000</summary>
        public string BaseUrl { get; set; } = "http://127.0.0.1:8000";

        /// <summary>Must equal BLOODFLOW_API_KEY on the AI server. Keep it in User Secrets, not in appsettings.json.</summary>
        public string ApiKey { get; set; } = null!;

        /// <summary>Timeout for the fast endpoints.</summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>Timeout for /retrain, which can take a couple of minutes.</summary>
        public int RetrainTimeoutMinutes { get; set; } = 5;
    }
}
