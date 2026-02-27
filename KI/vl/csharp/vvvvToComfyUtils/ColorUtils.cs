using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Advanced;
using VL.Core.Import;
using Stride.Core.Mathematics;

namespace Main;

[Serializable]
public static class ColorUtils
{
    /// <summary>
    /// VL Node: GetDominantColors
    /// Input: PNG/JPEG image as byte[]
    /// Output: array of colorCount RGBA colors (Color4)
    /// Optional params allow tuning; sensible defaults provided. 'colorCount' is number of colors.
    /// </summary>
    public static void GetDominantColors(
        byte[] image,
        out Color4[] colors,
        int colorCount = 5,
        int maxDimension = 400,
        int maxIters = 20,
        int seed = 42)
    {
        colors = Array.Empty<Color4>();
        if (image is null || image.Length == 0)
        {
            return;
        }

        try
        {
            using Image<Rgba32> img = Image.Load<Rgba32>(image);

            if (colorCount <= 0) colorCount = 5;
            if (colorCount > 16) colorCount = 16; // clamp to a reasonable number for performance
            if (maxDimension < 16) maxDimension = 16;
            if (maxIters < 1) maxIters = 1;

            // Downscale for speed while keeping aspect ratio
            if (Math.Max(img.Width, img.Height) > maxDimension)
            {
                double scale = (double)maxDimension / Math.Max(img.Width, img.Height);
                int w = Math.Max(1, (int)Math.Round(img.Width * scale));
                int h = Math.Max(1, (int)Math.Round(img.Height * scale));
                img.Mutate(ctx => ctx.Resize(w, h));
            }

            // Collect pixels (ignore fully transparent)
            var pixels = new List<System.Numerics.Vector3>(img.Width * img.Height);
            img.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < img.Height; y++)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < row.Length; x++)
                    {
                        var p = row[x];
                        if (p.A < 8) continue;
                        pixels.Add(new System.Numerics.Vector3(p.R / 255f, p.G / 255f, p.B / 255f));
                    }
                }
            });

            if (pixels.Count == 0)
            {
                colors = Array.Empty<Color4>();
                return;
            }

            var rand = new Random(seed);
            var centroids = KMeansPlusPlusInit(pixels, colorCount, rand);

            int n = pixels.Count;
            var assignments = new int[n];
            Array.Fill(assignments, -1);

            for (int iter = 0; iter < maxIters; iter++)
            {
                bool anyChange = false;

                // Assign
                for (int i = 0; i < n; i++)
                {
                    int bestK = 0;
                    float bestD = float.MaxValue;
                    for (int c = 0; c < centroids.Count; c++)
                    {
                        float d = Dist2(pixels[i], centroids[c]);
                        if (d < bestD)
                        {
                            bestD = d;
                            bestK = c;
                        }
                    }
                    if (assignments[i] != bestK)
                    {
                        assignments[i] = bestK;
                        anyChange = true;
                    }
                }

                // Update
            var sums = new System.Numerics.Vector3[centroids.Count];
                var counts = new int[centroids.Count];
                for (int i = 0; i < n; i++)
                {
                    int a = assignments[i];
                    sums[a] += pixels[i];
                    counts[a]++;
                }

                for (int c = 0; c < centroids.Count; c++)
                {
                    if (counts[c] == 0)
                    {
                        centroids[c] = pixels[rand.Next(n)];
                    }
                    else
                    {
                        centroids[c] = sums[c] / counts[c];
                    }
                }

                if (!anyChange) break;
            }

            // Tally
            var clusterCounts = new int[centroids.Count];
            for (int i = 0; i < n; i++) clusterCounts[assignments[i]]++;

            var results = new List<(Rgba32 color, int count)>(centroids.Count);
            for (int c = 0; c < centroids.Count; c++)
            {
                var v = centroids[c];
                var col = new Rgba32(
                    ClampToByte(v.X * 255f),
                    ClampToByte(v.Y * 255f),
                    ClampToByte(v.Z * 255f),
                    255);
                results.Add((col, clusterCounts[c]));
            }

            // Sort by prominence and take top colorCount (adjustable output size)
            results.Sort((a, b) => b.count.CompareTo(a.count));
            colors = results
                .Take(colorCount)
                .Select(t => new Color4(t.color.R / 255f, t.color.G / 255f, t.color.B / 255f, 1f))
                .ToArray();
        }
        catch
        {
            colors = Array.Empty<Color4>();
        }
    }

    private static List<System.Numerics.Vector3> KMeansPlusPlusInit(List<System.Numerics.Vector3> data, int k, Random rand)
    {
        var centroids = new List<System.Numerics.Vector3>(k);
        centroids.Add(data[rand.Next(data.Count)]);
        while (centroids.Count < k)
        {
            var dist2 = new float[data.Count];
            float sum = 0f;
            for (int i = 0; i < data.Count; i++)
            {
                float d = float.MaxValue;
                foreach (var c in centroids)
                {
                    d = Math.Min(d, Dist2(data[i], c));
                }
                dist2[i] = d;
                sum += d;
            }
            if (sum <= 1e-12f)
            {
                centroids.Add(data[rand.Next(data.Count)]);
                continue;
            }
            double r = rand.NextDouble() * sum;
            double cum = 0;
            for (int i = 0; i < data.Count; i++)
            {
                cum += dist2[i];
                if (cum >= r)
                {
                    centroids.Add(data[i]);
                    break;
                }
            }
        }
        return centroids;
    }

    private static float Dist2(in System.Numerics.Vector3 a, in System.Numerics.Vector3 b)
    {
        float dx = a.X - b.X;
        float dy = a.Y - b.Y;
        float dz = a.Z - b.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    private static byte ClampToByte(float v)
    {
        if (v < 0) return 0;
        if (v > 255) return 255;
        return (byte)MathF.Round(v);
    }
}


