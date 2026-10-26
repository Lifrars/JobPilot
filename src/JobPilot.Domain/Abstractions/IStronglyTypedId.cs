namespace JobPilot.Domain.Abstractions;

/// <summary>
/// Contrato para IDs fuertemente tipados (evita confundir un CompanyId con un JobApplicationId).
/// Usa el patrón CRTP (<typeparamref name="TSelf"/>) y un miembro <c>static abstract</c>
/// para que el código genérico pueda construir el ID sin reflexión: <c>TSelf.From(value)</c>.
/// </summary>
public interface IStronglyTypedId<TSelf, TValue>
    where TSelf : struct, IStronglyTypedId<TSelf, TValue>
    where TValue : notnull
{
    TValue Value { get; }

    static abstract TSelf From(TValue value);
}
