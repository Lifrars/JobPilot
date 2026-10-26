using System.Linq.Expressions;

namespace JobPilot.Domain.Abstractions;

/// <summary>
/// Specification pattern: encapsula un criterio de consulta reutilizable (filtros, includes, orden, paginación)
/// como objeto. La infraestructura lo traduce a IQueryable con un evaluador genérico.
/// </summary>
public abstract class Specification<T>
    where T : class
{
    private readonly List<Expression<Func<T, bool>>> _criteria = [];
    private readonly List<Expression<Func<T, object>>> _includes = [];

    public IReadOnlyList<Expression<Func<T, bool>>> Criteria => _criteria;

    public IReadOnlyList<Expression<Func<T, object>>> Includes => _includes;

    public Expression<Func<T, object>>? OrderBy { get; private set; }

    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    public int? Skip { get; private set; }

    public int? Take { get; private set; }

    public bool IsReadOnly { get; private set; }

    protected void Where(Expression<Func<T, bool>> criteria) => _criteria.Add(criteria);

    protected void Include(Expression<Func<T, object>> include) => _includes.Add(include);

    protected void ApplyOrderBy(Expression<Func<T, object>> orderBy) => OrderBy = orderBy;

    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderBy) => OrderByDescending = orderBy;

    protected void ApplyPaging(int page, int pageSize)
    {
        Skip = (page - 1) * pageSize;
        Take = pageSize;
    }

    /// <summary>Consultas de solo lectura: EF no rastrea cambios (más rápido).</summary>
    protected void AsReadOnly() => IsReadOnly = true;
}
