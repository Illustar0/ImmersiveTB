using System.Buffers;
using System.Drawing;
using System.Runtime.InteropServices;
using ImmersiveTB.Core.Models;
using DotNext.Buffers;

namespace ImmersiveTB.Core.Services;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct ColorSamplingRequest(
    ScreenCaptureMethod CaptureMethod,
    int SampleHeight,
    int TaskbarOffset,
    ColorSamplingAlgorithm Algorithm,
    int GaussianBlurRadius
);

[StructLayout(LayoutKind.Auto)]
internal readonly record struct ColorPostProcessRequest(
    bool Enabled,
    double BrightnessAdjustment,
    double SaturationAdjustment,
    double ContrastAdjustment,
    double Opacity
);

internal static class ColorProcessor
{
    private const int BucketCount = 16 * 16 * 16;

    public static Color Sample(CapturedFrame frame, ColorSamplingRequest request)
    {
        var pixels = frame.Pixels.Span;
        return request.Algorithm switch
        {
            ColorSamplingAlgorithm.DominantColor => CalculateDominantColor(pixels),
            ColorSamplingAlgorithm.GaussianBlur => CalculateGaussianBlurColor(
                frame.Pixels,
                frame.Width,
                frame.Height,
                request.GaussianBlurRadius
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(request))
        };
    }

    public static Color ApplyPostProcess(Color color, ColorPostProcessRequest request)
    {
        RgbToHsl(color.R, color.G, color.B, out var h, out var s, out var l);
        l = Math.Clamp(l + request.BrightnessAdjustment, 0.0, 1.0);
        s = Math.Clamp(s + request.SaturationAdjustment, 0.0, 1.0);
        HslToRgb(h, s, l, out var r, out var g, out var b);

        if (Math.Abs(request.ContrastAdjustment) > 0.001)
        {
            var factor = 1.0 + request.ContrastAdjustment;
            r = (int)Math.Clamp((r - 128) * factor + 128, 0, 255);
            g = (int)Math.Clamp((g - 128) * factor + 128, 0, 255);
            b = (int)Math.Clamp((b - 128) * factor + 128, 0, 255);
        }

        var alpha = (int)Math.Clamp(request.Opacity * 255, 0, 255);
        return Color.FromArgb(alpha, r, g, b);
    }

    private static Color CalculateDominantColor(ReadOnlySpan<byte> pixels)
    {
        using var bucketsOwner = new MemoryOwner<int>(
            ArrayPool<int>.Shared,
            BucketCount * 4
        );
        var buckets = bucketsOwner.Span;
        buckets.Clear();

        var counts = buckets[..BucketCount];
        var redSums = buckets.Slice(BucketCount, BucketCount);
        var greenSums = buckets.Slice(BucketCount * 2, BucketCount);
        var blueSums = buckets.Slice(BucketCount * 3, BucketCount);

        for (var index = 0; index < pixels.Length; index += 4)
        {
            var blue = pixels[index];
            var green = pixels[index + 1];
            var red = pixels[index + 2];
            var bucket = ((red >> 4) << 8) | ((green >> 4) << 4) | (blue >> 4);

            counts[bucket]++;
            redSums[bucket] += red;
            greenSums[bucket] += green;
            blueSums[bucket] += blue;
        }

        var dominantBucket = 0;
        for (var bucket = 1; bucket < BucketCount; bucket++)
        {
            if (counts[bucket] > counts[dominantBucket])
            {
                dominantBucket = bucket;
            }
        }

        var count = counts[dominantBucket];
        return count == 0
            ? Color.Transparent
            : Color.FromArgb(
                redSums[dominantBucket] / count,
                greenSums[dominantBucket] / count,
                blueSums[dominantBucket] / count
            );
    }

    private static Color CalculateGaussianBlurColor(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int radius
    )
    {
        if (width <= 0 || height <= 0)
        {
            return Color.Transparent;
        }

        var maximumRadius = Math.Max(1, Math.Min(width, height) / 2);
        radius = Math.Clamp(radius, 1, maximumRadius);
        Span<double> kernel = stackalloc double[radius * 2 + 1];
        GenerateGaussianKernel(radius, kernel);

        var image = pixels.Span;
        var rowStride = checked(width * 4);
        var centerX = width / 2;
        var centerY = height / 2;
        double red = 0;
        double green = 0;
        double blue = 0;

        for (var kernelY = 0; kernelY < kernel.Length; kernelY++)
        {
            var y = Math.Clamp(centerY + kernelY - radius, 0, height - 1);
            var row = image.Slice(y * rowStride, rowStride);
            double rowRed = 0;
            double rowGreen = 0;
            double rowBlue = 0;
            for (var kernelX = 0; kernelX < kernel.Length; kernelX++)
            {
                var x = Math.Clamp(centerX + kernelX - radius, 0, width - 1);
                var pixel = x * 4;
                var weight = kernel[kernelX];
                rowBlue += row[pixel] * weight;
                rowGreen += row[pixel + 1] * weight;
                rowRed += row[pixel + 2] * weight;
            }

            var rowWeight = kernel[kernelY];
            blue += rowBlue * rowWeight;
            green += rowGreen * rowWeight;
            red += rowRed * rowWeight;
        }

        return Color.FromArgb(
            (int)Math.Clamp(red, 0, 255),
            (int)Math.Clamp(green, 0, 255),
            (int)Math.Clamp(blue, 0, 255)
        );
    }

    private static void GenerateGaussianKernel(int radius, Span<double> kernel)
    {
        var sigma = radius / 3.0;
        var twoSigmaSquare = 2.0 * sigma * sigma;
        var sum = 0.0;

        for (var index = 0; index < kernel.Length; index++)
        {
            var distance = index - radius;
            kernel[index] = Math.Exp(-(distance * distance) / twoSigmaSquare);
            sum += kernel[index];
        }

        foreach (ref var weight in kernel)
        {
            weight /= sum;
        }
    }

    private static void RgbToHsl(int r, int g, int b, out double h, out double s, out double l)
    {
        var red = r / 255.0;
        var green = g / 255.0;
        var blue = b / 255.0;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;
        l = (max + min) / 2.0;

        if (Math.Abs(delta) <= double.Epsilon)
        {
            h = 0;
            s = 0;
            return;
        }

        s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);
        if (Math.Abs(max - red) <= double.Epsilon)
        {
            h = ((green - blue) / delta + (green < blue ? 6 : 0)) / 6.0;
        }
        else if (Math.Abs(max - green) <= double.Epsilon)
        {
            h = ((blue - red) / delta + 2) / 6.0;
        }
        else
        {
            h = ((red - green) / delta + 4) / 6.0;
        }
    }

    private static void HslToRgb(double h, double s, double l, out int r, out int g, out int b)
    {
        double red;
        double green;
        double blue;
        if (Math.Abs(s) <= double.Epsilon)
        {
            red = green = blue = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            red = HueToRgb(p, q, h + 1.0 / 3.0);
            green = HueToRgb(p, q, h);
            blue = HueToRgb(p, q, h - 1.0 / 3.0);
        }

        r = (int)Math.Round(red * 255);
        g = (int)Math.Round(green * 255);
        b = (int)Math.Round(blue * 255);
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0)
        {
            t += 1;
        }

        if (t > 1)
        {
            t -= 1;
        }

        if (t < 1.0 / 6.0)
        {
            return p + (q - p) * 6 * t;
        }

        if (t < 1.0 / 2.0)
        {
            return q;
        }

        return t < 2.0 / 3.0 ? p + (q - p) * (2.0 / 3.0 - t) * 6 : p;
    }
}