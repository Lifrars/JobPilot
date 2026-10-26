# Genéricos en JobPilot

Los genéricos permiten escribir **una vez** código que funciona para muchos tipos, **con seguridad de tipos en
compilación**. Estos son todos los usos del proyecto, de más simple a más avanzado.

## 1. Clases genéricas con restricciones (`where`)
```csharp
public abstract class Entity<TId> where TId : struct, IEquatable<TId>
public interface IRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId : struct, IEquatable<TId>
```
- `struct` obliga a que el ID sea un tipo valor (sin nulls ni boxing al comparar).
- `AggregateRoot<TId>` impide **en compilación** crear un `IRepository<Interview, ...>`: las entidades internas no se persisten solas.

📂 `src/JobPilot.Domain/Abstractions/Entity.cs`, `IRepository.cs`

## 2. CRTP + `static abstract` (C# 11+)
```csharp
public interface IStronglyTypedId<TSelf, TValue> where TSelf : struct, IStronglyTypedId<TSelf, TValue>
{
    TValue Value { get; }
    static abstract TSelf From(TValue value);
}
```
- **CRTP** (*Curiously Recurring Template Pattern*): el tipo se pasa a sí mismo (`TSelf`).
- `static abstract` permite llamar a `TId.From(value)` desde código genérico **sin reflexión**.
- Limitación real: un árbol de expresión no puede invocar un miembro `static abstract` (error CS8927). Por eso
  `StronglyTypedIdConverter` pasa por un método auxiliar `Create`.

📂 `src/JobPilot.Domain/Abstractions/IStronglyTypedId.cs`, `src/JobPilot.Infrastructure/Persistence/Converters.cs`

## 3. Smart enum genérico autorreferenciado
```csharp
public abstract class Enumeration<TEnum> where TEnum : Enumeration<TEnum>
{
    private static readonly Lazy<Dictionary<int, TEnum>> All = ...; // un diccionario POR TIPO cerrado
}
```
Cada tipo genérico cerrado (`Enumeration<ApplicationStatus>`) tiene **sus propios campos estáticos**: es un caché por tipo gratis.

## 4. Varianza: `in` (contravarianza)
```csharp
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
public interface ICommandHandler<in TCommand, TResponse>
```
`in` indica que `TEvent` solo entra como parámetro. Así un `IDomainEventHandler<IDomainEvent>` es asignable a
`IDomainEventHandler<JobApplicationCreatedDomainEvent>`.

## 5. Genéricos abiertos en el contenedor de DI
```csharp
services.AddScoped(typeof(IRepository<,>), typeof(EfRepository<,>));                  // un registro, N tipos
services.Decorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator.CommandHandler<,>));
```
Al pedir `IRepository<Company, CompanyId>`, el contenedor **cierra** el genérico en tiempo de ejecución.

## 6. Cerrar genéricos con reflexión (cómo funciona un Mediator)
```csharp
var wrapper = (RequestWrapper<TResponse>)Wrappers.GetOrAdd(command.GetType(),
    t => Activator.CreateInstance(typeof(CommandWrapper<,>).MakeGenericType(t, typeof(TResponse)))!);
```
El problema: dentro de `SendAsync<TResponse>` conocemos `TResponse`, pero el tipo concreto del comando solo en
ejecución. `MakeGenericType` construye `CommandWrapper<CreateCompanyCommand, CompanyResponse>` **una vez**, se
cachea, y a partir de ahí todo es código tipado sin reflexión. Es la técnica que usa MediatR por dentro.

📂 `src/JobPilot.Application/Abstractions/Messaging/Sender.cs`

## 7. Métodos genéricos y combinadores
```csharp
public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> map)
public PagedList<TOut> Map<TOut>(Func<T, TOut> map)
public static IResult ToCreated<T>(this Result<T> result, Func<T, string> location)
```
La inferencia de tipos hace que casi nunca haya que escribir `<...>` al llamarlos.

## 8. Genéricos en librerías modernas
- `chatClient.GetResponseAsync<AnalysisPayload>(...)` genera un **JSON Schema a partir del tipo** y obliga al LLM a cumplirlo.
- `HybridCache.GetOrCreateAsync<T>` / `JsonValueConverter<T>` / `Specification<T>` / `Scored<T>`.

## Ejercicios propuestos
1. Crea `IReadRepository<T, TId>` solo con métodos de lectura y haz que `IRepository` herede de él.
2. Agrega un decorator genérico `CachingDecorator<TQuery, TResponse>` que solo actúe si `TQuery : ICacheableQuery`.
3. Convierte los IDs a `IParsable<TSelf>` para que Minimal APIs los bindee directamente desde la ruta.
