namespace BloodFlow.Application.Ai
{
    public enum AiErrorKind
    {
        /// <summary>The AI service rejected the input (400/422). Message is its "detail", safe to show to the user.</summary>
        BadRequest,
        /// <summary>The API key is missing or wrong (401). A configuration problem on our side.</summary>
        Unauthorized,
        /// <summary>The AI service is down, still starting, or timed out.</summary>
        Unavailable,
        /// <summary>A retrain is already running.</summary>
        Busy,
        /// <summary>Any other unexpected response.</summary>
        Failed
    }

    public class AiServiceException : Exception
    {
        public AiErrorKind Kind { get; }
        public int? UpstreamStatusCode { get; }

        public AiServiceException(AiErrorKind kind, string message, int? upstreamStatusCode = null, Exception? inner = null)
            : base(message, inner)
        {
            Kind = kind;
            UpstreamStatusCode = upstreamStatusCode;
        }
    }
}
