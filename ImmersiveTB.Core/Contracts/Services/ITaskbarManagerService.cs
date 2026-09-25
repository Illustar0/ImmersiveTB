using System.Drawing;
using ImmersiveTB.Core.Helpers;
using ImmersiveTB.Core.Models;
using Microsoft.Extensions.Hosting;

namespace ImmersiveTB.Core.Contracts.Services;

public interface ITaskbarManagerService : IHostedService
{
    /// <summary>
    ///     任务栏 HWND
    /// </summary>
    IntPtr Hwnd
    {
        get;
    }

    /// <summary>
    ///     实时颜色
    /// </summary>
    Color CurrentColor
    {
        get;
    }

    /// <summary>
    ///     Gets whether the TranslucentTB worker is currently available.
    /// </summary>
    bool IsAvailable
    {
        get;
    }

    /// <summary>
    ///     Occurs when TranslucentTB availability changes.
    /// </summary>
    event EventHandler<TaskbarAvailabilityChangedEventArgs>? AvailabilityChanged;

    /// <summary>
    ///     Rediscovers the taskbar and TranslucentTB worker windows.
    /// </summary>
    /// <returns><see langword="true" /> when the worker is available.</returns>
    bool RefreshTargets();

    /// <summary>
    ///     异步设置任务栏颜色。
    /// </summary>
    /// <param name="state">任务栏状态。</param>
    /// <param name="targetColor">目标颜色。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>任务</returns>
    Task SetColorAsync(
        TaskbarState state,
        Color targetColor,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    ///     异步设置任务栏颜色，带有缓动效果。
    /// </summary>
    /// <param name="state">任务栏状态。</param>
    /// <param name="targetColor">目标颜色。</param>
    /// <param name="easing">缓动算法类型。</param>
    /// <param name="duration">动画持续时间。</param>
    /// <param name="cancellationToken">用于取消当前动画的令牌。</param>
    /// <returns>任务</returns>
    Task SetColorAnimatedAsync(
        TaskbarState state,
        Color targetColor,
        EasingType easing,
        TimeSpan duration,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
///     Describes a change in TranslucentTB availability.
/// </summary>
public sealed class TaskbarAvailabilityChangedEventArgs(bool isAvailable) : EventArgs
{
    /// <summary>Gets whether TranslucentTB is available.</summary>
    public bool IsAvailable
    {
        get;
    } = isAvailable;
}