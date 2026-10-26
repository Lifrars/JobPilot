namespace JobPilot.Domain.Abstractions;

public static class HttpUrl
{
    /// <summary>
    /// Valida una URL web absoluta. Solo <c>Uri.TryCreate(..., UriKind.Absolute)</c> no basta:
    /// en Linux/macOS "/jobs/1" se interpreta como <c>file:///jobs/1</c> y pasa la validación.
    /// </summary>
    public static bool IsValid(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
