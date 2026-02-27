namespace ReferenceSoundGridUtils;

using Stride.Core.Mathematics;

public static class PlaneConverter
{
    
    /// <summary>
    /// Debug-Funktion um Matrix-Layout zu verstehen
    /// </summary>
    public static void DebugMatrix(Matrix matrix, int index)
    {
        Console.WriteLine($"Matrix {index}:");
        Console.WriteLine($"M11: {matrix.M11:F2}  M12: {matrix.M12:F2}  M13: {matrix.M13:F2}  M14: {matrix.M14:F2}");
        Console.WriteLine($"M21: {matrix.M21:F2}  M22: {matrix.M22:F2}  M23: {matrix.M23:F2}  M24: {matrix.M24:F2}");
        Console.WriteLine($"M31: {matrix.M31:F2}  M32: {matrix.M32:F2}  M33: {matrix.M33:F2}  M34: {matrix.M34:F2}");
        Console.WriteLine($"M41: {matrix.M41:F2}  M42: {matrix.M42:F2}  M43: {matrix.M43:F2}  M44: {matrix.M44:F2}");
        
        // Teste verschiedene Normal-Extraktionen
        Vector3 normalCol3 = new Vector3(matrix.M13, matrix.M23, matrix.M33);
        Vector3 normalRow3 = new Vector3(matrix.M31, matrix.M32, matrix.M33);
        
        Console.WriteLine($"3. Spalte (M13,M23,M33): {normalCol3}");
        Console.WriteLine($"3. Zeile (M31,M32,M33): {normalRow3}");
        Console.WriteLine($"3. Spalte normalisiert: {Vector3.Normalize(normalCol3)}");
        Console.WriteLine($"3. Zeile normalisiert: {Vector3.Normalize(normalRow3)}");
        Console.WriteLine("---");
    }
    
    /// <summary>
    /// Verschiedene Plane-Extraction-Methoden testen
    /// </summary>
    public static void TestPlaneExtractions(Matrix matrix)
    {
        // Methode 1: 3. Spalte als Normal
        Vector3 normal1 = Vector3.Normalize(new Vector3(matrix.M13, matrix.M23, matrix.M33));
        Vector3 pos1 = new Vector3(matrix.M41, matrix.M42, matrix.M43);
        float dist1 = -Vector3.Dot(normal1, pos1);
        
        // Methode 2: 3. Zeile als Normal
        Vector3 normal2 = Vector3.Normalize(new Vector3(matrix.M31, matrix.M32, matrix.M33));
        Vector3 pos2 = new Vector3(matrix.M14, matrix.M24, matrix.M34);
        float dist2 = -Vector3.Dot(normal2, pos2);
        
        // Methode 3: 3. Spalte als Normal, 4. Spalte als Position
        Vector3 normal3 = Vector3.Normalize(new Vector3(matrix.M13, matrix.M23, matrix.M33));
        Vector3 pos3 = new Vector3(matrix.M14, matrix.M24, matrix.M34);
        float dist3 = -Vector3.Dot(normal3, pos3);
        
        Console.WriteLine($"Methode 1 (Spalte3/Zeile4): Normal=({normal1.X:F2},{normal1.Y:F2},{normal1.Z:F2}), Dist={dist1:F2}");
        Console.WriteLine($"Methode 2 (Zeile3/Spalte4): Normal=({normal2.X:F2},{normal2.Y:F2},{normal2.Z:F2}), Dist={dist2:F2}");
        Console.WriteLine($"Methode 3 (Spalte3/Spalte4): Normal=({normal3.X:F2},{normal3.Y:F2},{normal3.Z:F2}), Dist={dist3:F2}");
    }
    
    /// <summary>
    /// Konvertiert eine TransformSRT Matrix zu einer Plane Equation
    /// </summary>
    /// <param name="transformMatrix">Die 4x4 Transform Matrix aus vvvv TransformSRT</param>
    /// <returns>Vector4 mit (normal.x, normal.y, normal.z, distance)</returns>
    public static Vector4 TransformToPlane(Matrix transformMatrix)
    {
        // Extrahiere Normal aus der 2. ZEILE der Matrix (Y-Achse)
        // Basierend auf Debug-Analyse: 2. Zeile = Y-Achse = Plane Normal
        Vector3 normal = new Vector3(
            transformMatrix.M21,  // 2. Zeile, 1. Spalte
            transformMatrix.M22,  // 2. Zeile, 2. Spalte
            transformMatrix.M23   // 2. Zeile, 3. Spalte
        );
        
        // Normalisiere den Normal-Vektor
        normal = Vector3.Normalize(normal);
        
        // Extrahiere Position (Translation aus der Matrix)
        // 4. Zeile der Matrix (Translation)
        Vector3 position = new Vector3(
            transformMatrix.M41,  // Translation X
            transformMatrix.M42,  // Translation Y
            transformMatrix.M43   // Translation Z
        );
        
        // Berechne Plane Distance: d = -dot(normal, position)
        float distance = -Vector3.Dot(normal, position);
        
        // Rückgabe als Vector4 (normal.xyz, distance)
        return new Vector4(normal.X, normal.Y, normal.Z, distance);
    }
    
    /// <summary>
    /// Extrahiert die Center-Positionen aus Transform Matrices
    /// </summary>
    /// <param name="transformMatrices">Array von Transform Matrices</param>
    /// <returns>Array von Vector3 Center-Positionen</returns>
    public static Vector3 ExtractCenter(Matrix transformMatrix)
    {
        return new Vector3(
            transformMatrix.M41,
            transformMatrix.M42,
            transformMatrix.M43
            );
    }
    
    /// <summary>
    /// Validiert eine Plane Equation
    /// </summary>
    /// <param name="plane">Die zu validierende Plane</param>
    /// <returns>True wenn gültig, False sonst</returns>
    public static bool ValidatePlane(Vector4 plane)
    {
        Vector3 normal = new Vector3(plane.X, plane.Y, plane.Z);
        float normalLength = normal.Length();
        
        // Normal sollte normalisiert sein (Länge ≈ 1)
        return MathF.Abs(normalLength - 1.0f) < 0.001f;
    }
    
    /// <summary>
    /// Berechnet Distanz von einem Punkt zu einer Plane
    /// </summary>
    /// <param name="point">Der 3D-Punkt</param>
    /// <param name="plane">Die Plane (normal.xyz, distance)</param>
    /// <returns>Signierte Distanz zur Plane</returns>
    public static float DistanceToPlane(Vector3 point, Vector4 plane)
    {
        Vector3 normal = new Vector3(plane.X, plane.Y, plane.Z);
        return Vector3.Dot(point, normal) + plane.W;
    }
}