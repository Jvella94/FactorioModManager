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

        public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);
        public static Result<T> Fail<T>(string error, ErrorCode? code = null) => Result<T>.Fail(error, code);
    }

    /// <summary>
    /// Represents the result of an operation that returns a value
    /// </summary>
    public class Result<T> : Result
    {
        public T? Value { get; }

        private Result(bool success, T? value, string? error, ErrorCode? errorCode)
            : base(success, error, errorCode)
        {
            Value = value;
        }

        public static Result<T> Ok(T value) => new(true, value, null, null);
        public static new Result<T> Fail(string error, ErrorCode? code = null) => new(false, default, error, code);

        /// <summary>
        /// Maps the result value if successful
        /// </summary>
        public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
        {
            if (!Success || Value == null)
                return Result<TNew>.Fail(Error ?? "Operation failed", Code);

            try
            {
                return Result<TNew>.Ok(mapper(Value));
            }
            catch (Exception ex)
            {
                return Result<TNew>.Fail(ex.Message, ErrorCode.UnexpectedError);
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
    }
}
