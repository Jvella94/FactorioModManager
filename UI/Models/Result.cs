using System;

namespace FactorioModManager.Models
{
    /// <summary>
    /// Represents the result of an operation that can succeed or fail
    /// </summary>
    public class Result
    {
        public bool Success { get; }
        public string? Error { get; }
        public ErrorCode? Code { get; }

        protected Result(bool success, string? error = null, ErrorCode? errorCode = null)
        {
            Success = success;
            Error = error;
            Code = errorCode;
        }

        public static Result Ok() => new(true);

        public static Result Fail(string error, ErrorCode? code = null) => new(false, error, code);

        public static Result<T> Ok<T>(T value) where T : notnull => Result<T>.Ok(value);

        public static Result<T> Fail<T>(string error, ErrorCode? code = null) => Result<T>.Fail(error, code);
    }

    /// <summary>
    /// Represents the result of an operation that returns a value
    /// </summary>
    public class Result<T> : Result
    {
        public T Value { get; }

        private Result(bool success, T value, string? error, ErrorCode? errorCode)
            : base(success, error, errorCode)
        {
            Value = value;
        }

        // For non-nullable scenarios (enforced at runtime)
        public static Result<T> Ok(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return new(true, value, null, null);
        }

        // For explicitly nullable scenarios
        public static Result<TValue?> OkNullable<TValue>(TValue? value)
        {
            return new Result<TValue?>(true, value, null, null);
        }

        public new static Result<T> Fail(string error, ErrorCode? code = null)
            => new(false, default!, error, code);

        /// <summary>
        /// Maps the result value if successful
        /// </summary>
        public Result<TResult> Map<TResult>(Func<T, TResult> mapper)
        {
            if (!Success || Value == null)
                return Result<TResult>.Fail(Error ?? "Operation failed", Code);

            try
            {
                return Result<TResult>.Ok(mapper(Value));
            }
            catch (Exception ex)
            {
                return Result<TResult>.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        /// <summary>
        /// Executes an action if the result is successful
        /// </summary>
        public Result<T> OnSuccess(Action<T> action)
        {
            if (Success && Value != null)
                action(Value);
            return this;
        }

        /// <summary>
        /// Executes an action if the result is a failure
        /// </summary>
        public Result<T> OnFailure(Action<string> action)
        {
            if (!Success && Error != null)
                action(Error);
            return this;
        }

        /// <summary>
        /// Gets the value or throws if operation failed
        /// </summary>
        public T GetValueOrThrow()
        {
            if (!Success)
                throw new InvalidOperationException(Error ?? "Operation failed");
            return Value;
        }

        /// <summary>
        /// Pattern matching helper for functional-style handling
        /// </summary>
        public TResult Match<TResult>(
            Func<T, TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            return Success ? onSuccess(Value) : onFailure(Error ?? "Unknown error");
        }
    }
}