namespace JobPilot.Domain.Abstractions;

public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unavailable
}

/// <summary>Error de negocio expresado como dato (no como excepción).</summary>
public record Error(string Code, string Description, ErrorType Type)
{
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);

    public static Error Validation(string code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Unavailable(string code, string description) => new(code, description, ErrorType.Unavailable);
}

/// <summary>Agrupa varios errores de validación (uno por campo inválido).</summary>
public sealed record ValidationError(IReadOnlyList<Error> Errors)
    : Error("General.Validation", "One or more validation errors occurred.", ErrorType.Validation);
