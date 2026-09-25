using Windows.Win32;
using Windows.Win32.Foundation;

namespace ImmersiveTB.Helpers;

/// <summary>
///     Provides information about the current application runtime.
/// </summary>
public static class RuntimeHelper
{
    /// <summary>
    ///     Gets whether the current process has package identity.
    /// </summary>
    public static bool IsMSIX
    {
        get
        {
            uint length = 0;

            return PInvoke.GetCurrentPackageFullName(ref length, [])
                   != WIN32_ERROR.APPMODEL_ERROR_NO_PACKAGE;
        }
    }
}