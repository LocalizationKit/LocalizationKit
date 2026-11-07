using Microsoft.AspNetCore.Http;

namespace LocalizationKit;

/// <summary>One place a request can say which language it wants.</summary>
/// <remarks>
/// A source returns candidates rather than a decision, and does no validation: whether a candidate is
/// a culture the application ships is <see cref="ILocalizationContext.NegotiateCulture"/>'s business,
/// and it is asked in order until one matches. That is what lets <c>Accept-Language</c> offer its
/// whole list, and what keeps a source free of any knowledge of the resources.
/// </remarks>
public interface IRequestCultureSource
{
    /// <summary>Gets a short name for this source, used in diagnostics.</summary>
    string Name { get; }

    /// <summary>Reads the cultures this request asks for, best first.</summary>
    /// <param name="context">The request.</param>
    /// <returns>Candidate culture names, which may be empty and need not be valid.</returns>
    IReadOnlyList<string> GetCandidates(HttpContext context);
}
