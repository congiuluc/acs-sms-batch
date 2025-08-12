using System.Diagnostics.CodeAnalysis;

namespace BatchSMS.Models;

/// <summary>
/// Represents the result of an operation that can succeed or fail
/// </summary>
/// <typeparam name="T">The type of the success value</typeparam>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly string? _error;
    private readonly Exception? _exception;

    private Result(T value)
    {
        _value = value;
        _error = null;
        _exception = null;
        IsSuccess = true;
    }

    private Result(string error, Exception? exception = null)
    {
        _value = default;
        _error = error;
        _exception = exception;
        IsSuccess = false;
    }

    public bool IsSuccess { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException($"Cannot access value of failed result: {Error}");

    public string? Error => _error;

    public Exception? Exception => _exception;

    public static Result<T> Success(T value) => new(value);

    public static Result<T> Failure(string error) => new(error);

    public static Result<T> Failure(string error, Exception exception) => new(error, exception);

    public static Result<T> Failure(Exception exception) => new(exception.Message, exception);

    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess ? Result<TNew>.Success(mapper(Value)) : 
            Exception != null ? Result<TNew>.Failure(Error!, Exception) : Result<TNew>.Failure(Error!);
    }

    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        if (IsFailure)
            return Exception != null ? Result<TNew>.Failure(Error!, Exception) : Result<TNew>.Failure(Error!);

        try
        {
            var result = await mapper(Value).ConfigureAwait(false);
            return Result<TNew>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<TNew>.Failure(ex);
        }
    }

    public T GetValueOrDefault(T defaultValue = default!) => IsSuccess ? Value : defaultValue;

    public override string ToString() => IsSuccess ? $"Success({Value})" : $"Failure({Error})";
}

/// <summary>
/// Represents the result of an operation that can succeed or fail without a return value
/// </summary>
public readonly struct Result
{
    private readonly string? _error;
    private readonly Exception? _exception;

    private Result(string error, Exception? exception = null)
    {
        _error = error;
        _exception = exception;
        IsSuccess = false;
    }

    public bool IsSuccess { get; private init; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsFailure => !IsSuccess;

    public string? Error => _error;

    public Exception? Exception => _exception;

    public static Result Success() => new() { IsSuccess = true };

    public static Result Failure(string error) => new(error);

    public static Result Failure(string error, Exception exception) => new(error, exception);

    public static Result Failure(Exception exception) => new(exception.Message, exception);

    public Result<T> Map<T>(Func<T> mapper)
    {
        if (IsFailure)
            return Exception != null ? Result<T>.Failure(Error!, Exception) : Result<T>.Failure(Error!);

        try
        {
            return Result<T>.Success(mapper());
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ex);
        }
    }

    public async Task<Result<T>> MapAsync<T>(Func<Task<T>> mapper)
    {
        if (IsFailure)
            return Exception != null ? Result<T>.Failure(Error!, Exception) : Result<T>.Failure(Error!);

        try
        {
            var result = await mapper().ConfigureAwait(false);
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ex);
        }
    }

    public override string ToString() => IsSuccess ? "Success" : $"Failure({Error})";
}
