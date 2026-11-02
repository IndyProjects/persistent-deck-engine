using UnityEngine;

/// Procedural meshes Unity's primitives don't cover.
public static class MeshShapes
{
    /// Spins a profile of (radius, height) points around the Y axis into a smooth round shape.
    /// Walking along the profile, the surface faces to the walker's left: outwards from the centre
    /// gives an upward face, downwards along the outside gives an outward face.
    public static Mesh Lathe(string name, Vector2[] profile, int segments = 96)
    {
        int rings = profile.Length;
        var vertices = new Vector3[rings * segments];
        for (int s = 0; s < segments; s++)
        {
            float angle = s * Mathf.PI * 2f / segments;
            float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            for (int r = 0; r < rings; r++)
                vertices[s * rings + r] = new Vector3(profile[r].x * cos, profile[r].y, profile[r].x * sin);
        }

        var triangles = new int[(rings - 1) * segments * 6];
        int t = 0;
        for (int s = 0; s < segments; s++)
        {
            int next = (s + 1) % segments;
            for (int r = 0; r < rings - 1; r++)
            {
                int a = s * rings + r, b = s * rings + r + 1;
                int c = next * rings + r, d = next * rings + r + 1;
                triangles[t++] = a; triangles[t++] = d; triangles[t++] = b;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = d;
            }
        }

        var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
