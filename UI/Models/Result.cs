using System;
using System.Threading.Tasks;

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

        // ✨ NEW: Helper methods for common patterns
        public bool IsFailure => !Success;
        
        public Result OnSuccess(Action action)
        {
            if (Success) action();
            return this;
        }

        public Result OnFailure(Action<string?, ErrorCode?> action)
        {
            if (!Success) action(Error, Code);
            return this;
        }
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

        // ✨ NEW: Async map
        public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
        {
            if (!Success)
                return Result<TNew>.Fail(Error ?? "Operation failed", Code);

            try
            {
                var result = await mapper(Value);
                return Result<TNew>.Ok(result);
            }
            catch (Exception ex)
            {
                return Result<TNew>.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        // ✨ NEW: Bind (flatMap)
        public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
        {
            if (!Success)
                return Result<TNew>.Fail(Error ?? "Operation failed", Code);

            try
            {
                return binder(Value);
            }
            catch (Exception ex)
            {
                return Result<TNew>.Fail(ex.Message, ErrorCode.UnexpectedError);
            }
        }

        // ✨ NEW: Match pattern
        public TResult Match<TResult>(
            Func<T, TResult> onSuccess,
            Func<string?, ErrorCode?, TResult> onFailure)
        {
            return Success ? onSuccess(Value) : onFailure(Error, Code);
        }

        // ✨ NEW: Get value or default
        public T? ValueOrDefault() => Success ? Value : default;

        public T ValueOr(T defaultValue) => Success ? Value : defaultValue;

        public T ValueOr(Func<T> defaultValueFactory) => Success ? Value : defaultValueFactory();
    }
}