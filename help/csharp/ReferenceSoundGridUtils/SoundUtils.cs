using System;
using System.Collections.Generic;
// System.Numerics might not be strictly needed if all vector types are from Stride and Math functions are implicitly available.
// However, keeping it for now unless a cleanup is requested.
using System.Numerics; 
using Vector3 = Stride.Core.Mathematics.Vector3;
using Vector4 = Stride.Core.Mathematics.Vector4;
using Int3 = Stride.Core.Mathematics.Int3;

namespace Main;

public static class SoundUtils
{
    /// <summary>
    /// Generates 3D positions on the 6 faces of a box, based on a specified resolution per axis.
    /// For each position, a Vector4 is returned where XYZ is the position and W is the ID of the box side.
    /// The side IDs are: +X (0), -X (1), +Y (2), -Y (3), +Z (4), -Z (5).
    /// Points are generated to cover the entire face, determined by the resolution.
    /// A resolution of N for an axis means N segments (N+1 points) along that axis of the face.
    /// </summary>
    /// <param name="boxSize">The dimensions (width, height, depth) of the box.</param>
    /// <param name="boxCenter">The center coordinates of the box.</param>
    /// <param name="resolution">The number of segments for point generation along X, Y, and Z axes of the faces. resolution.X applies to YZ faces, resolution.Y to XZ faces, resolution.Z to XY faces.</param>
    /// <returns>A list of Vector4, where XYZ is the point position and W is the face ID.</returns>
    public static List<Vector4> GenerateBoxSurfacePoints(Vector3 boxSize, Vector3 boxCenter, Int3 resolution)
    {
        var points = new List<Vector4>();

        // Validate inputs: boxSize components should be positive, resolution components non-negative.
        // Using a small epsilon for floating point comparisons on boxSize.
        if (boxSize.X <= 1e-6f || boxSize.Y <= 1e-6f || boxSize.Z <= 1e-6f)
        {
            // If any dimension of boxSize is zero or negative, there's no valid surface.
            return points;
        }
        if (resolution.X < 0 || resolution.Y < 0 || resolution.Z < 0)
        {
            // Resolution components cannot be negative.
            return points;
        }

        Vector3 halfDim = boxSize / 2.0f;

        // Define face plane coordinates
        float xPlusFace = boxCenter.X + halfDim.X;
        float xMinusFace = boxCenter.X - halfDim.X;
        float yPlusFace = boxCenter.Y + halfDim.Y;
        float yMinusFace = boxCenter.Y - halfDim.Y;
        float zPlusFace = boxCenter.Z + halfDim.Z;
        float zMinusFace = boxCenter.Z - halfDim.Z;

        // Define starting coordinates for loops on faces (min corner of the box)
        float minXCoord = boxCenter.X - halfDim.X;
        float minYCoord = boxCenter.Y - halfDim.Y;
        float minZCoord = boxCenter.Z - halfDim.Z;
        
        // Calculate step sizes for each dimension based on resolution.
        // If resolution is 0 for an axis, step is 0, resulting in 1 point at the start of that axis.
        float stepX = (resolution.X > 0) ? boxSize.X / resolution.X : 0f;
        float stepY = (resolution.Y > 0) ? boxSize.Y / resolution.Y : 0f;
        float stepZ = (resolution.Z > 0) ? boxSize.Z / resolution.Z : 0f;

        // Face +X (ID 0) - Iterate over Y (using resolution.Y) and Z (using resolution.Z)
        for (int iy = 0; iy <= resolution.Y; iy++)
        {
            float y = minYCoord + iy * stepY;
            for (int iz = 0; iz <= resolution.Z; iz++)
            {
                float z = minZCoord + iz * stepZ;
                points.Add(new Vector4(xPlusFace, y, z, 0.0f));
            }
        }

        // Face -X (ID 1) - Iterate over Y (resolution.Y) and Z (resolution.Z)
        for (int iy = 0; iy <= resolution.Y; iy++)
        {
            float y = minYCoord + iy * stepY;
            for (int iz = 0; iz <= resolution.Z; iz++)
            {
                float z = minZCoord + iz * stepZ;
                points.Add(new Vector4(xMinusFace, y, z, 1.0f));
            }
        }

        // Face +Y (ID 2) - Iterate over X (resolution.X) and Z (resolution.Z)
        for (int ix = 0; ix <= resolution.X; ix++)
        {
            float x = minXCoord + ix * stepX;
            for (int iz = 0; iz <= resolution.Z; iz++)
            {
                float z = minZCoord + iz * stepZ;
                points.Add(new Vector4(x, yPlusFace, z, 2.0f));
            }
        }

        // Face -Y (ID 3) - Iterate over X (resolution.X) and Z (resolution.Z)
        for (int ix = 0; ix <= resolution.X; ix++)
        {
            float x = minXCoord + ix * stepX;
            for (int iz = 0; iz <= resolution.Z; iz++)
            {
                float z = minZCoord + iz * stepZ;
                points.Add(new Vector4(x, yMinusFace, z, 3.0f));
            }
        }

        // Face +Z (ID 4) - Iterate over X (resolution.X) and Y (resolution.Y)
        for (int ix = 0; ix <= resolution.X; ix++)
        {
            float x = minXCoord + ix * stepX;
            for (int iy = 0; iy <= resolution.Y; iy++)
            {
                float y = minYCoord + iy * stepY;
                points.Add(new Vector4(x, y, zPlusFace, 4.0f));
            }
        }

        // Face -Z (ID 5) - Iterate over X (resolution.X) and Y (resolution.Y)
        for (int ix = 0; ix <= resolution.X; ix++)
        {
            float x = minXCoord + ix * stepX;
            for (int iy = 0; iy <= resolution.Y; iy++)
            {
                float y = minYCoord + iy * stepY;
                points.Add(new Vector4(x, y, zMinusFace, 5.0f));
            }
        }
        return points;
    }

    /// <summary>
    /// Splits a list into a list of lists, where each inner list contains at most a specified number of entries.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="source">The list to be split. If null, an empty list of lists will be returned.</param>
    /// <param name="maxEntriesPerChunk">The maximum number of entries for each inner list (chunk).</param>
    /// <returns>A list of lists, where each inner list is a chunk of the original list. Returns an empty list of lists if the source is null or empty.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if maxEntriesPerChunk is not positive.</exception>
    public static List<List<T>> SplitList<T>(List<T> source, int maxEntriesPerChunk)
    {
        if (source == null)
        {
            return []; // Handle null source by returning an empty list of lists
        }

        if (maxEntriesPerChunk <= 0)
        {
            return [];
        }

        var chunks = new List<List<T>>();
        if (source.Count == 0)
        {
            // This also ensures an empty list of lists is returned for an empty (but not null) source list.
            return chunks; 
        }

        for (int i = 0; i < source.Count; i += maxEntriesPerChunk)
        {
            int count = Math.Min(maxEntriesPerChunk, source.Count - i);
            List<T> chunk = source.GetRange(i, count);
            chunks.Add(chunk);
        }

        return chunks;
    }
} 