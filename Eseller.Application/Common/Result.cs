namespace Eseller.Application.Common;

public class Result
{
    public bool IsSuccess { get; init; }
    public string? Error { get; init; }
    public string? ErrorCode { get; init; }

    protected Result(bool isSuccess, string? error, string? errorCode)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string error, string? errorCode = null) => new(false, error, errorCode);

    public static Result<T> Success<T>(T value) => new(value, true, null, null);
    public static Result<T> Failure<T>(string error, string? errorCode = null) => new(default, false, error, errorCode);
}

public class Result<T> : Result
{
    public T? Value { get; init; }

    internal Result(T? value, bool isSuccess, string? error, string? errorCode)
        : base(isSuccess, error, errorCode)
    {
        Value = value;
    }
}