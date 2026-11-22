using BloodFlow.Application.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BloodFlow.Infrastructure.Ai
{
    public static class AiServiceCollectionExtensions
    {
        public static IServiceCollection AddBloodFlowAi(this IServiceCollection services, IConfiguration configuration)
        {
            var options = configuration.GetSection(BloodFlowAiOptions.SectionName).Get<BloodFlowAiOptions>()
                          ?? new BloodFlowAiOptions();
            // No startup failure when the key is missing, so teammates who don't run the AI service can still
            // start the API. AI calls will then fail with a clear "API key is missing or wrong" error.
            // Set it with: dotnet user-secrets set "BloodFlowAi:ApiKey" "<same value as BLOODFLOW_API_KEY>"
            var baseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");

            services.AddHttpClient(BloodFlowAiClient.HttpClientName, http =>
            {
                http.BaseAddress = baseAddress;
                http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
                AddApiKey(http, options.ApiKey);
            });

            services.AddHttpClient(BloodFlowAiClient.RetrainHttpClientName, http =>
            {
                http.BaseAddress = baseAddress;
                http.Timeout = TimeSpan.FromMinutes(options.RetrainTimeoutMinutes);
                AddApiKey(http, options.ApiKey);
            });

            services.AddScoped<IBloodFlowAiClient, BloodFlowAiClient>();
            return services;
        }

        private static void AddApiKey(HttpClient http, string? apiKey)
        {
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                http.DefaultRequestHeaders.Add("x-api-key", apiKey);
            }
        }
    }
}
