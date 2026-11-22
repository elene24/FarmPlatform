namespace FarmPlatform.Application.Common.Results;

/// <summary>What kind of failure occurred; the API maps each kind to an HTTP status.</summary>
public enum ErrorKind
{
    Validation = 1,
    Unauthorized = 2,
    Forbidden = 3,
    NotFound = 4,
    Conflict = 5,
    Locked = 6,
}

/// <summary>
/// A failure with a stable, machine-readable <paramref name="Code"/> (e.g. "auth.email_taken") that the
/// frontend translates, and optionally the request field it belongs to (camelCase, e.g. "email").
/// </summary>
public sealed record AppError(string Code, string? Field = null);

/// <summary>Outcome of a use case. Expected failures are returned, not thrown.</summary>
public class Result
{
    private protected Result(ErrorKind? kind, IReadOnlyList<AppError> errors)
    {
        Kind = kind;
        Errors = errors;
    }

    public bool IsSuccess => Kind is null;

    public ErrorKind? Kind { get; }

    public IReadOnlyList<AppError> Errors { get; }

    public static Result Success() => new(null, []);

    public static Result Failure(ErrorKind kind, params AppError[] errors) => new(kind, errors);

    public static Result<T> Success<T>(T value) => new(value);

    public static Result<T> Failure<T>(ErrorKind kind, params AppError[] errors) => new(kind, errors);
}

public sealed class Result<T> : Result
{
    internal Result(T value) : base(null, []) => Value = value;

    internal Result(ErrorKind kind, IReadOnlyList<AppError> errors) : base(kind, errors) { }

    /// <summary>Set only when <see cref="Result.IsSuccess"/> is true.</summary>
    public T? Value { get; }
}
