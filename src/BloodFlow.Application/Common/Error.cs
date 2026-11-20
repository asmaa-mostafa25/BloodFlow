namespace BloodFlow.Application.Common
{
    /// <summary>The kind of failure. The API turns each one into an HTTP status code.</summary>
    public enum ErrorType
    {
        Validation,   // 400
        Unauthorized, // 401
        Forbidden,    // 403
        NotFound,     // 404
        Conflict      // 409
    }

    /// <summary>An expected business failure (wrong state, not found, duplicate...).</summary>
    public sealed record Error(string Code, string Message, ErrorType Type)
    {
        public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);
        public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);
        public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
        public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);
        public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);
    }
}
