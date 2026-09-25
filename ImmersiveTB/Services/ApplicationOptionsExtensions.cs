using ImmersiveTB.Core.Models;
using ImmersiveTB.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ImmersiveTB.Services;

/// <summary>Registers generated configuration binding and validation for all settings.</summary>
public static class ApplicationOptionsExtensions
{
    /// <summary>
    /// Binds the shared settings schema and its independently consumed sections.
    /// Invalid values and unknown keys fail before hosted services start.
    /// </summary>
    public static IServiceCollection AddApplicationOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptionsWithValidateOnStart<ApplicationPreferences, ApplicationOptionsValidator>()
            .Bind(configuration, binding => binding.ErrorOnUnknownConfiguration = true);
        services.AddOptionsWithValidateOnStart<ColorSamplingOptions, ApplicationOptionsValidator>()
            .Bind(configuration.GetSection(nameof(ApplicationPreferences.ColorSampling)),
                binding => binding.ErrorOnUnknownConfiguration = true);
        services.AddOptionsWithValidateOnStart<PostProcessOptions, ApplicationOptionsValidator>()
            .Bind(configuration.GetSection(nameof(ApplicationPreferences.PostProcess)),
                binding => binding.ErrorOnUnknownConfiguration = true);
        services.AddOptionsWithValidateOnStart<TaskbarAppearanceOptions, ApplicationOptionsValidator>()
            .Bind(configuration.GetSection(nameof(ApplicationPreferences.TaskbarAppearance)),
                binding => binding.ErrorOnUnknownConfiguration = true);
        return services;
    }
}

/// <summary>Generates recursive, AOT-compatible validation from the settings annotations.</summary>
[OptionsValidator]
public sealed partial class ApplicationOptionsValidator :
    IValidateOptions<ApplicationPreferences>,
    IValidateOptions<ColorSamplingOptions>,
    IValidateOptions<PostProcessOptions>,
    IValidateOptions<TaskbarAppearanceOptions>;