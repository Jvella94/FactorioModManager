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
        private readonly T? _value;

        public T Value => Success && _value != null
            ? _value
            : throw new InvalidOperationException("Cannot access Value of a failed result");

        private Result(bool success, T? value, string? error, ErrorCode? errorCode)
            : base(success, error, errorCode)
        {
            _value = value;
        }

        public static Result<T> Ok(T value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return new(true, value, null, null);
        }

        public new static Result<T> Fail(string error, ErrorCode? code = null)
            => new(false, default, error, code);

        public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
        {
            if (!Success)
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
    }
}