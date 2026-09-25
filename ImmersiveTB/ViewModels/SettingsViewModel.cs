using System.Diagnostics;
using System.Reflection;
using BetterWinUI.PageActivation.DependencyInjection;
using ImmersiveTB.Core.Helpers;
using ImmersiveTB.Core.Logging;
using ImmersiveTB.Core.Models;
using ImmersiveTB.Core.Services;
using ImmersiveTB.Helpers;
using ImmersiveTB.Logging;
using ImmersiveTB.Models;
using ImmersiveTB.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Windows.ApplicationModel;

#pragma warning disable S2325 // WinUI bindings require these instance properties.

namespace ImmersiveTB.ViewModels;

/// <summary>
///     Maps typed application preferences to the settings page.
/// </summary>
[ViewModel(ServiceLifetime.Transient)]
public partial class SettingsViewModel : ObservableRecipient
{
    private readonly bool _isInitializing;
    private readonly ApplicationPreferencesService _preferences;

    /// <summary>
    ///     Initializes the view model from the current typed preference snapshot.
    /// </summary>
    public SettingsViewModel(
        ApplicationPreferencesService preferences,
        ILogger<SettingsViewModel> logger
    )
    {
        _preferences = preferences;
        Version = GetVersionDescription(logger);

        var current = preferences.Current;
        _isInitializing = true;
        AppThemeOption = current.Application.AppTheme;
        ApplicationLanguageIndex = (int)current.Application.ApplicationLanguage;
        CaptureMethod = current.ColorSampling.CaptureMethod;
        SampleHeight = current.ColorSampling.SampleHeight;
        TaskbarOffset = current.ColorSampling.TaskbarOffset;
        SamplingAlgorithm = current.ColorSampling.Algorithm;
        GaussianBlurRadius = current.ColorSampling.GaussianBlurOptions.Radius;
        ColorAnimationEnabled = current.TaskbarAppearance.ColorAnimationEnabled;
        ColorEasing = current.TaskbarAppearance.ColorEasing;
        ColorAnimationFrameRate = current.TaskbarAppearance.ColorAnimationFrameRate;
        ColorAnimationDurationMs = current.TaskbarAppearance.ColorAnimationDurationMs;
        ColorSamplingDelayMs = current.TaskbarAppearance.ColorSamplingDelayMs;
        PostProcessEnabled = current.PostProcess.Enabled;
        BrightnessAdjustment = current.PostProcess.BrightnessAdjustment;
        SaturationAdjustment = current.PostProcess.SaturationAdjustment;
        ContrastAdjustment = current.PostProcess.ContrastAdjustment;
        OpacityValue = current.PostProcess.Opacity;
        LogLevel = current.Logging.MinimumLevel;
        _isInitializing = false;
    }

    /// <summary>
    ///     Occurs when a changed application language requires a restart.
    /// </summary>
    public event EventHandler? LanguageRestartRequired;

    [ObservableProperty]
    public partial string Version
    {
        get;
        set;
    }

    /// <summary>Gets the localized application display name.</summary>
    public string DisplayName => "AppDisplayName".GetLocalized();

    /// <summary>Gets the localized copyright notice.</summary>
    public string Copyright => "Copyright".GetLocalized();

    /// <summary>Gets the project repository URI.</summary>
    public Uri GitHubUri
    {
        get;
    } = new("https://github.com/Illustar0/ImmersiveTB");

    /// <summary>Gets the Microsoft Store search URI for ImmersiveTB.</summary>
    public Uri StoreUri
    {
        get;
    } = new("ms-windows-store://search/?query=ImmersiveTB");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaptureMethodIndex))]
    public partial ScreenCaptureMethod CaptureMethod
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial int SampleHeight
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial int TaskbarOffset
    {
        get;
        set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SamplingAlgorithmIndex))]
    public partial ColorSamplingAlgorithm SamplingAlgorithm
    {
        get;
        set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AppThemeIndex))]
    private partial AppTheme AppThemeOption
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial int ApplicationLanguageIndex
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial int GaussianBlurRadius
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial bool ColorAnimationEnabled
    {
        get;
        set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorEasingIndex))]
    public partial EasingType ColorEasing
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial int ColorAnimationFrameRate
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial int ColorAnimationDurationMs
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial int ColorSamplingDelayMs
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial bool PostProcessEnabled
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial double BrightnessAdjustment
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial double SaturationAdjustment
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial double ContrastAdjustment
    {
        get;
        set;
    }

    [ObservableProperty]
    public partial double OpacityValue
    {
        get;
        set;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogLevelIndex))]
    public partial ApplicationLogLevel LogLevel
    {
        get;
        set;
    }

    /// <summary>Gets localized capture method names.</summary>
    public string[] CaptureMethods
    {
        get;
    } =
    [
        "CaptureMethod_WindowsGraphicsCapture".GetLocalized(),
        "CaptureMethod_Gdi".GetLocalized()
    ];

    /// <summary>Gets localized sampling algorithm names.</summary>
    public string[] SamplingAlgorithms
    {
        get;
    } =
    [
        "SamplingAlgorithm_DominantColor".GetLocalized(),
        "SamplingAlgorithm_GaussianBlur".GetLocalized()
    ];

    /// <summary>Gets localized application theme names.</summary>
    public string[] AppThemeDisplayNames
    {
        get;
    } =
    [
        "AppTheme_Light".GetLocalized(),
        "AppTheme_Dark".GetLocalized(),
        "AppTheme_System".GetLocalized()
    ];

    /// <summary>Gets localized application language names.</summary>
    public string[] ApplicationLanguageDisplayNames
    {
        get;
    } =
    [
        "AppLanguage_System".GetLocalized(),
        "AppLanguage_English".GetLocalized(),
        "AppLanguage_ChineseSimplified".GetLocalized()
    ];

    /// <summary>Gets localized color easing names.</summary>
    public string[] ColorEasingDisplayNames
    {
        get;
    } = [.. Enum.GetValues<EasingType>().Select(GetEasingDisplayName)];

    /// <summary>Gets localized log level names.</summary>
    public string[] LogLevelDisplayNames
    {
        get;
    } =
    [
        .. Enum.GetValues<ApplicationLogLevel>()
            .Select(level => $"LogLevelName_{level}".GetLocalized())
    ];

    /// <summary>Gets or sets the capture method index used by XAML.</summary>
    public int CaptureMethodIndex
    {
        get => (int)CaptureMethod;
        set
        {
            if (Enum.IsDefined((ScreenCaptureMethod)value))
            {
                CaptureMethod = (ScreenCaptureMethod)value;
            }
        }
    }

    /// <summary>Gets or sets the sampling algorithm index used by XAML.</summary>
    public int SamplingAlgorithmIndex
    {
        get => (int)SamplingAlgorithm;
        set
        {
            if (Enum.IsDefined((ColorSamplingAlgorithm)value))
            {
                SamplingAlgorithm = (ColorSamplingAlgorithm)value;
            }
        }
    }

    /// <summary>Gets or sets the application theme index used by XAML.</summary>
    public int AppThemeIndex
    {
        get => (int)AppThemeOption;
        set
        {
            if (Enum.IsDefined((AppTheme)value))
            {
                AppThemeOption = (AppTheme)value;
            }
        }
    }

    /// <summary>Gets or sets the color easing index used by XAML.</summary>
    public int ColorEasingIndex
    {
        get => (int)ColorEasing;
        set
        {
            if (Enum.IsDefined((EasingType)value))
            {
                ColorEasing = (EasingType)value;
            }
        }
    }

    /// <summary>Gets or sets the log level index used by XAML.</summary>
    public int LogLevelIndex
    {
        get => (int)LogLevel;
        set
        {
            if (Enum.IsDefined((ApplicationLogLevel)value))
            {
                LogLevel = (ApplicationLogLevel)value;
            }
        }
    }

    /// <summary>Gets whether Gaussian blur controls should be visible.</summary>
    public bool IsGaussianBlurSelected =>
        SamplingAlgorithm == ColorSamplingAlgorithm.GaussianBlur;

    partial void OnAppThemeOptionChanged(AppTheme value) =>
        Update(current => current with
        {
            Application = current.Application with { AppTheme = value }
        });

    partial void OnApplicationLanguageIndexChanged(int value)
    {
        if (!Enum.IsDefined((ApplicationLanguage)value))
        {
            return;
        }

        Update(current => current with
        {
            Application = current.Application with
            {
                ApplicationLanguage = (ApplicationLanguage)value
            }
        });
        if (!_isInitializing)
        {
            _preferences.Flush();
            LanguageRestartRequired?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnCaptureMethodChanged(ScreenCaptureMethod value) =>
        Update(current => current with
        {
            ColorSampling = current.ColorSampling with { CaptureMethod = value }
        });

    partial void OnSampleHeightChanged(int value) =>
        Update(current => current with
        {
            ColorSampling = current.ColorSampling with { SampleHeight = value }
        });

    partial void OnTaskbarOffsetChanged(int value) =>
        Update(current => current with
        {
            ColorSampling = current.ColorSampling with { TaskbarOffset = value }
        });

    partial void OnSamplingAlgorithmChanged(ColorSamplingAlgorithm value)
    {
        Update(current => current with
        {
            ColorSampling = current.ColorSampling with { Algorithm = value }
        });
        OnPropertyChanged(nameof(IsGaussianBlurSelected));
    }

    partial void OnGaussianBlurRadiusChanged(int value) =>
        Update(current => current with
        {
            ColorSampling = current.ColorSampling with
            {
                GaussianBlurOptions = new GaussianBlurOptions { Radius = value }
            }
        });

    partial void OnColorAnimationEnabledChanged(bool value) =>
        Update(current => current with
        {
            TaskbarAppearance = current.TaskbarAppearance with
            {
                ColorAnimationEnabled = value
            }
        });

    partial void OnColorEasingChanged(EasingType value) =>
        Update(current => current with
        {
            TaskbarAppearance = current.TaskbarAppearance with { ColorEasing = value }
        });

    partial void OnColorAnimationFrameRateChanged(int value) =>
        Update(current => current with
        {
            TaskbarAppearance = current.TaskbarAppearance with
            {
                ColorAnimationFrameRate = value
            }
        });

    partial void OnColorAnimationDurationMsChanged(int value) =>
        Update(current => current with
        {
            TaskbarAppearance = current.TaskbarAppearance with
            {
                ColorAnimationDurationMs = value
            }
        });

    partial void OnColorSamplingDelayMsChanged(int value) =>
        Update(current => current with
        {
            TaskbarAppearance = current.TaskbarAppearance with
            {
                ColorSamplingDelayMs = value
            }
        });

    partial void OnPostProcessEnabledChanged(bool value) =>
        Update(current => current with
        {
            PostProcess = current.PostProcess with { Enabled = value }
        });

    partial void OnBrightnessAdjustmentChanged(double value) =>
        Update(current => current with
        {
            PostProcess = current.PostProcess with { BrightnessAdjustment = value }
        });

    partial void OnSaturationAdjustmentChanged(double value) =>
        Update(current => current with
        {
            PostProcess = current.PostProcess with { SaturationAdjustment = value }
        });

    partial void OnContrastAdjustmentChanged(double value) =>
        Update(current => current with
        {
            PostProcess = current.PostProcess with { ContrastAdjustment = value }
        });

    partial void OnOpacityValueChanged(double value) =>
        Update(current => current with
        {
            PostProcess = current.PostProcess with { Opacity = value }
        });

    partial void OnLogLevelChanged(ApplicationLogLevel value) =>
        Update(current => current with
        {
            Logging = current.Logging with { MinimumLevel = value }
        });

    private void Update(
        Func<ApplicationPreferences, ApplicationPreferences> update
    )
    {
        if (!_isInitializing)
        {
            _preferences.Update(update);
        }
    }

    private static string GetEasingDisplayName(EasingType easing)
    {
        if (easing == EasingType.Linear)
        {
            return "Easing_Linear".GetLocalized();
        }

        var name = easing.ToString();

        var direction = name switch
        {
            _ when name.StartsWith("EaseInOut", StringComparison.Ordinal) => "EaseInOut",
            _ when name.StartsWith("EaseIn", StringComparison.Ordinal) => "EaseIn",
            _ => "EaseOut"
        };

        var family = name[direction.Length..];
        var directionName = $"Easing_{direction}".GetLocalized();
        var familyName = $"Easing_{family}".GetLocalized();
        return $"{directionName} · {familyName}";
    }

    /// <summary>Gets the three-component version displayed by packaged and portable builds.</summary>
    private static string GetVersionDescription(ILogger logger)
    {
        Version version;
        if (RuntimeHelper.IsMSIX)
        {
            var packageVersion = Package.Current.Id.Version;
            version = new Version(
                packageVersion.Major,
                packageVersion.Minor,
                packageVersion.Build
            );
        }
        else
        {
            AppLogMessages.AssemblyVersionFallback(logger);
            version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
        }

        return $"v{version.ToString(3)}";
    }
}