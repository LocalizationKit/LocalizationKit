namespace System.Runtime.CompilerServices;

/// <summary>
/// The marker the compiler needs for <c>init</c> accessors, and therefore for records.
/// </summary>
/// <remarks>
/// It ships with .NET 5 and later, but an analyzer targets netstandard2.0 to be loadable by every
/// compiler host, so it is declared here instead.
/// </remarks>
internal static class IsExternalInit;
