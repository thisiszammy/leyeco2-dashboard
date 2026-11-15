namespace Leyeco2Dashboard.App
{
    /// <summary>
    /// The Transaction Monitor version (SemVer): one number for this API and the <c>Leyeco2Dashboard.Client</c>
    /// console, which are released together. This file is its single source of truth, and both projects compile it:
    /// the console links it into its own build (a <c>Compile</c> item in its csproj), so the version it shows on the
    /// sign-in page and in the sidebar is the one it was built with. Bump it on every release of either.
    /// </summary>
    /// <remarks>
    /// Internal because both assemblies carry the type: Debug builds of this API reference the console, where a
    /// public copy would collide with this one (CS0436).
    /// </remarks>
    internal static class MonitorVersion
    {
        public const string Current = "0.1.0";
    }
}
