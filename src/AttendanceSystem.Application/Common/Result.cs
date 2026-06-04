namespace AttendanceSystem.Application.Common;

/// <summary>
/// Functional result type for use cases without throwing for business failures.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public string Error { get; } = string.Empty;
    public string? ErrorCode { get; }

    protected Result(bool isSuccess, string error, string? errorCode = null)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result Success() => new(true, string.Empty);
    public static Result Failure(string error, string? code = null) => new(false, error, code);
}

/// <summary>
/// Result carrying a value on success.
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; }

    private Result(T value) : base(true, string.Empty) => Value = value;
    private Result(string error, string? code) : base(false, error, code) => Value = default;

    public static Result<T> Success(T value) => new(value);
    public new static Result<T> Failure(string error, string? code = null) => new(error, code);
}
