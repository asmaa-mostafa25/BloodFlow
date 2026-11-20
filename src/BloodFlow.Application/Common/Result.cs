namespace BloodFlow.Application.Common
{
    /// <summary>
    /// The outcome of an operation: success, or one <see cref="Error"/>.
    /// Expected failures (a rule was broken) are returned, not thrown.
    /// Exceptions are kept for truly unexpected problems.
    /// </summary>
    public class Result
    {
        protected Result(bool isSuccess, Error? error)
        {
            IsSuccess = isSuccess;
            Error = error;
        }

        public bool IsSuccess { get; }
        public bool IsFailure => !IsSuccess;
        public Error? Error { get; }

        public static Result Success() => new(true, null);
        public static Result Failure(Error error) => new(false, error);

        public static implicit operator Result(Error error) => Failure(error);
    }

    public sealed class Result<T> : Result
    {
        private readonly T? _value;

        private Result(T value) : base(true, null) => _value = value;
        private Result(Error error) : base(false, error) { }

        /// <summary>Only read this after checking <see cref="Result.IsSuccess"/>.</summary>
        public T Value => IsSuccess
            ? _value!
            : throw new InvalidOperationException("Cannot read the value of a failed result.");

        public static Result<T> Success(T value) => new(value);
        public static new Result<T> Failure(Error error) => new(error);

        public static implicit operator Result<T>(T value) => Success(value);
        public static implicit operator Result<T>(Error error) => Failure(error);
    }
}
