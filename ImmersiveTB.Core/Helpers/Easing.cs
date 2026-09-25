namespace ImmersiveTB.Core.Helpers;

/// <summary>
///     提供常见地缓动函数，用于动画和过渡效果。
///     所有函数接受 [0, 1] 范围的输入，返回对应的缓动值。
/// </summary>
public static class Easing
{
    #region Linear

    /// <summary>
    ///     线性缓动（无缓动效果）
    /// </summary>
    public static double Linear(double t) => t;

    #endregion

    #region Quadratic (二次方)

    public static double EaseInQuad(double t) => t * t;

    public static double EaseOutQuad(double t) => t * (2 - t);

    public static double EaseInOutQuad(double t) => t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;

    #endregion

    #region Cubic (三次方)

    public static double EaseInCubic(double t) => t * t * t;

    public static double EaseOutCubic(double t)
    {
        var t1 = t - 1;
        return t1 * t1 * t1 + 1;
    }

    public static double EaseInOutCubic(double t) =>
        t < 0.5 ? 4 * t * t * t : (t - 1) * (2 * t - 2) * (2 * t - 2) + 1;

    #endregion

    #region Quartic (四次方)

    public static double EaseInQuart(double t) => t * t * t * t;

    public static double EaseOutQuart(double t)
    {
        var t1 = t - 1;
        return 1 - t1 * t1 * t1 * t1;
    }

    public static double EaseInOutQuart(double t)
    {
        if (t < 0.5)
        {
            return 8 * t * t * t * t;
        }

        var t1 = t - 1;
        return 1 - 8 * t1 * t1 * t1 * t1;
    }

    #endregion

    #region Quintic (五次方)

    public static double EaseInQuint(double t) => t * t * t * t * t;

    public static double EaseOutQuint(double t)
    {
        var t1 = t - 1;
        return 1 + t1 * t1 * t1 * t1 * t1;
    }

    public static double EaseInOutQuint(double t)
    {
        if (t < 0.5)
        {
            return 16 * t * t * t * t * t;
        }

        var t1 = t - 1;
        return 1 + 16 * t1 * t1 * t1 * t1 * t1;
    }

    #endregion

    #region Sine (正弦)

    public static double EaseInSine(double t) => 1 - Math.Cos(t * Math.PI / 2);

    public static double EaseOutSine(double t) => Math.Sin(t * Math.PI / 2);

    public static double EaseInOutSine(double t) => (1 - Math.Cos(Math.PI * t)) / 2;

    #endregion

    #region Exponential (指数)

    public static double EaseInExpo(double t) =>
        t <= double.Epsilon ? 0 : Math.Pow(2, 10 * (t - 1));

    public static double EaseOutExpo(double t) =>
        t >= 1 - double.Epsilon ? 1 : 1 - Math.Pow(2, -10 * t);

    public static double EaseInOutExpo(double t)
    {
        if (t <= double.Epsilon)
        {
            return 0;
        }

        if (t >= 1 - double.Epsilon)
        {
            return 1;
        }

        if (t < 0.5)
        {
            return Math.Pow(2, 20 * t - 10) / 2;
        }

        return (2 - Math.Pow(2, -20 * t + 10)) / 2;
    }

    #endregion

    #region Circular (圆形)

    public static double EaseInCirc(double t) => 1 - Math.Sqrt(1 - t * t);

    public static double EaseOutCirc(double t)
    {
        var t1 = t - 1;
        return Math.Sqrt(1 - t1 * t1);
    }

    public static double EaseInOutCirc(double t)
    {
        if (t < 0.5)
        {
            return (1 - Math.Sqrt(1 - 4 * t * t)) / 2;
        }

        var t1 = 2 * t - 2;
        return (Math.Sqrt(1 - t1 * t1) + 1) / 2;
    }

    #endregion

    #region Back (回弹)

    private const double BackOvershoot = 1.70158;
    private const double BackOvershootInOut = BackOvershoot * 1.525;

    public static double EaseInBack(double t) => t * t * ((BackOvershoot + 1) * t - BackOvershoot);

    public static double EaseOutBack(double t)
    {
        var t1 = t - 1;
        return t1 * t1 * ((BackOvershoot + 1) * t1 + BackOvershoot) + 1;
    }

    public static double EaseInOutBack(double t)
    {
        if (t < 0.5)
        {
            var t1 = 2 * t;
            return t1 * t1 * ((BackOvershootInOut + 1) * t1 - BackOvershootInOut) / 2;
        }
        else
        {
            var t1 = 2 * t - 2;
            return (t1 * t1 * ((BackOvershootInOut + 1) * t1 + BackOvershootInOut) + 2) / 2;
        }
    }

    #endregion

    #region Elastic (弹性)

    private const double ElasticPeriod = 0.3;
    private const double ElasticPeriodInOut = 0.45;

    public static double EaseInElastic(double t)
    {
        if (t <= double.Epsilon)
        {
            return 0;
        }

        if (t >= 1 - double.Epsilon)
        {
            return 1;
        }

        var t1 = t - 1;
        return -Math.Pow(2, 10 * t1)
               * Math.Sin((t1 - ElasticPeriod / 4) * (2 * Math.PI) / ElasticPeriod);
    }

    public static double EaseOutElastic(double t)
    {
        if (t <= double.Epsilon)
        {
            return 0;
        }

        if (t >= 1 - double.Epsilon)
        {
            return 1;
        }

        return Math.Pow(2, -10 * t)
               * Math.Sin((t - ElasticPeriod / 4) * (2 * Math.PI) / ElasticPeriod)
               + 1;
    }

    public static double EaseInOutElastic(double t)
    {
        if (t <= double.Epsilon)
        {
            return 0;
        }

        if (t >= 1 - double.Epsilon)
        {
            return 1;
        }

        if (t < 0.5)
        {
            var t1 = 2 * t - 1;
            return -Math.Pow(2, 10 * t1)
                   * Math.Sin((t1 - ElasticPeriodInOut / 4) * (2 * Math.PI) / ElasticPeriodInOut)
                   / 2;
        }
        else
        {
            var t1 = 2 * t - 1;
            return Math.Pow(2, -10 * t1)
                   * Math.Sin((t1 - ElasticPeriodInOut / 4) * (2 * Math.PI) / ElasticPeriodInOut)
                   / 2
                   + 1;
        }
    }

    #endregion

    #region Bounce (弹跳)

    public static double EaseInBounce(double t) => 1 - EaseOutBounce(1 - t);

    public static double EaseOutBounce(double t)
    {
        const double n1 = 7.5625;
        const double d1 = 2.75;

        if (t < 1 / d1)
        {
            return n1 * t * t;
        }

        if (t < 2 / d1)
        {
            t -= 1.5 / d1;
            return n1 * t * t + 0.75;
        }

        if (t < 2.5 / d1)
        {
            t -= 2.25 / d1;
            return n1 * t * t + 0.9375;
        }

        t -= 2.625 / d1;
        return n1 * t * t + 0.984375;
    }

    public static double EaseInOutBounce(double t) =>
        t < 0.5 ? (1 - EaseOutBounce(1 - 2 * t)) / 2 : (1 + EaseOutBounce(2 * t - 1)) / 2;

    #endregion

    #region Helper Methods

    /// <summary>
    ///     根据缓动类型获取对应的缓动函数
    /// </summary>
    public static Func<double, double> GetEasingFunction(EasingType type) =>
        type switch
        {
            EasingType.Linear => Linear,
            EasingType.EaseInQuad => EaseInQuad,
            EasingType.EaseOutQuad => EaseOutQuad,
            EasingType.EaseInOutQuad => EaseInOutQuad,
            EasingType.EaseInCubic => EaseInCubic,
            EasingType.EaseOutCubic => EaseOutCubic,
            EasingType.EaseInOutCubic => EaseInOutCubic,
            EasingType.EaseInQuart => EaseInQuart,
            EasingType.EaseOutQuart => EaseOutQuart,
            EasingType.EaseInOutQuart => EaseInOutQuart,
            EasingType.EaseInQuint => EaseInQuint,
            EasingType.EaseOutQuint => EaseOutQuint,
            EasingType.EaseInOutQuint => EaseInOutQuint,
            EasingType.EaseInSine => EaseInSine,
            EasingType.EaseOutSine => EaseOutSine,
            EasingType.EaseInOutSine => EaseInOutSine,
            EasingType.EaseInExpo => EaseInExpo,
            EasingType.EaseOutExpo => EaseOutExpo,
            EasingType.EaseInOutExpo => EaseInOutExpo,
            EasingType.EaseInCirc => EaseInCirc,
            EasingType.EaseOutCirc => EaseOutCirc,
            EasingType.EaseInOutCirc => EaseInOutCirc,
            EasingType.EaseInBack => EaseInBack,
            EasingType.EaseOutBack => EaseOutBack,
            EasingType.EaseInOutBack => EaseInOutBack,
            EasingType.EaseInElastic => EaseInElastic,
            EasingType.EaseOutElastic => EaseOutElastic,
            EasingType.EaseInOutElastic => EaseInOutElastic,
            EasingType.EaseInBounce => EaseInBounce,
            EasingType.EaseOutBounce => EaseOutBounce,
            EasingType.EaseInOutBounce => EaseInOutBounce,
            _ => Linear
        };

    /// <summary>
    ///     应用缓动函数进行值插值
    /// </summary>
    /// <param name="start">起始值</param>
    /// <param name="end">结束值</param>
    /// <param name="t">进度 [0, 1]</param>
    /// <param name="easingFunc">缓动函数</param>
    public static double Interpolate(
        double start,
        double end,
        double t,
        Func<double, double> easingFunc
    )
    {
        var easedT = easingFunc(Math.Clamp(t, 0, 1));
        return start + (end - start) * easedT;
    }

    #endregion
}

/// <summary>
///     缓动类型枚举
/// </summary>
public enum EasingType
{
    Linear,

    // Quadratic
    EaseInQuad,
    EaseOutQuad,
    EaseInOutQuad,

    // Cubic
    EaseInCubic,
    EaseOutCubic,
    EaseInOutCubic,

    // Quartic
    EaseInQuart,
    EaseOutQuart,
    EaseInOutQuart,

    // Quintic
    EaseInQuint,
    EaseOutQuint,
    EaseInOutQuint,

    // Sine
    EaseInSine,
    EaseOutSine,
    EaseInOutSine,

    // Exponential
    EaseInExpo,
    EaseOutExpo,
    EaseInOutExpo,

    // Circular
    EaseInCirc,
    EaseOutCirc,
    EaseInOutCirc,

    // Back
    EaseInBack,
    EaseOutBack,
    EaseInOutBack,

    // Elastic
    EaseInElastic,
    EaseOutElastic,
    EaseInOutElastic,

    // Bounce
    EaseInBounce,
    EaseOutBounce,
    EaseInOutBounce
}