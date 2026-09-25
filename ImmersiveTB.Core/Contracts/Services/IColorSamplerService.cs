// Copyright (c) 2025 Illustar0
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.

using System.Drawing;

namespace ImmersiveTB.Core.Contracts.Services;

/// <summary>
///     Produces the final taskbar color while hiding capture selection, fallback,
///     region discovery, and pixel processing.
/// </summary>
public interface IColorSamplerService
{
    /// <summary>
    ///     Captures and processes the configured taskbar-adjacent desktop region.
    /// </summary>
    /// <param name="cancellationToken">Cancels the active capture and processing request.</param>
    /// <returns>The sampled and post-processed color.</returns>
    Task<Color> SampleAsync(CancellationToken cancellationToken = default);
}