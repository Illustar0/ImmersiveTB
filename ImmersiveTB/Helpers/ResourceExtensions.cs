using Microsoft.Windows.ApplicationModel.Resources;

namespace ImmersiveTB.Helpers;

/// <summary>
///     Provides concise access to the application's current resource map.
/// </summary>
public static class ResourceExtensions
{
    private static readonly ResourceLoader ResourceLoader = new();

    extension(string resourceKey)
    {
        /// <summary>
        ///     Resolves a resource key for the current application language.
        /// </summary>
        public string GetLocalized() => ResourceLoader.GetString(resourceKey);
    }
}