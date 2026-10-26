using JobPilot.Domain.Abstractions;

namespace JobPilot.Domain.Companies;

public readonly record struct CompanyId(Guid Value) : IStronglyTypedId<CompanyId, Guid>
{
    public static CompanyId New() => new(Guid.CreateVersion7());

    public static CompanyId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}

public sealed class Company : AggregateRoot<CompanyId>
{
    public const int NameMaxLength = 200;

    private Company(CompanyId id, string name, string? website, string? industry, DateTimeOffset createdAt) : base(id)
    {
        Name = name;
        Website = website;
        Industry = industry;
        CreatedAt = createdAt;
    }

    private Company() { }

    public string Name { get; private set; } = null!;

    public string? Website { get; private set; }

    public string? Industry { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Factory Method: única forma de crear una Company válida.</summary>
    public static Result<Company> Create(string name, string? website, string? industry, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return CompanyErrors.NameRequired;
        }

        if (website is not null && !HttpUrl.IsValid(website))
        {
            return CompanyErrors.InvalidWebsite;
        }

        return new Company(CompanyId.New(), name.Trim(), website, industry?.Trim(), now);
    }
}

public static class CompanyErrors
{
    public static readonly Error NameRequired = Error.Validation("Company.NameRequired", "The company name is required.");

    public static readonly Error InvalidWebsite = Error.Validation("Company.InvalidWebsite", "The website must be an absolute URL.");

    public static Error NotFound(CompanyId id) => Error.NotFound("Company.NotFound", $"The company '{id}' was not found.");

    public static Error NameAlreadyExists(string name) =>
        Error.Conflict("Company.NameAlreadyExists", $"A company named '{name}' already exists.");
}
