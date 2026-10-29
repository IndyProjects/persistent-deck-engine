using System.Collections.Generic;
using UnityEngine;

/// Builds the mesh for one card: a thin box whose top (+Y) shows the face, bottom shows the back and
/// sides show the paper edge, all from CardAtlas. The card lies flat; its top edge points along +Z.
public static class CardMeshBuilder
{
    /// Poker size (63.5 × 88.9 mm) scaled up slightly so cards read well from the seat.
    public const float Scale     = 1.3f;
    public const float Width     = 0.0635f * Scale;
    public const float Length    = 0.0889f * Scale;
    public const float Thickness = 0.0004f;

    public static Mesh Build(int card)
    {
        float hw = Width * 0.5f, hl = Length * 0.5f, ht = Thickness * 0.5f;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs   = new List<Vector2>();
        var tris  = new List<int>();

        var face = CardAtlas.FaceUV(card);
        var back = CardAtlas.BackUV;
        var edge = CardAtlas.EdgeUV;

        // Face (top) and back (bottom); corners are listed as seen from outside, so neither reads mirrored
        Quad(verts, norms, uvs, tris, Vector3.up,
            new Vector3(-hw, ht, -hl), new Vector3(-hw, ht, hl), new Vector3(hw, ht, hl), new Vector3(hw, ht, -hl), face);
        Quad(verts, norms, uvs, tris, Vector3.down,
            new Vector3(hw, -ht, -hl), new Vector3(hw, -ht, hl), new Vector3(-hw, -ht, hl), new Vector3(-hw, -ht, -hl), back);

        // Edges, inset by the corner radius so they don't poke out past the rounded corners
        float inset = 13f / CardAtlas.TileH * Length * 0.6f;
        Quad(verts, norms, uvs, tris, Vector3.left,
            new Vector3(-hw, -ht, -hl + inset), new Vector3(-hw, -ht, hl - inset), new Vector3(-hw, ht, hl - inset), new Vector3(-hw, ht, -hl + inset), edge);
        Quad(verts, norms, uvs, tris, Vector3.right,
            new Vector3(hw, -ht, hl - inset), new Vector3(hw, -ht, -hl + inset), new Vector3(hw, ht, -hl + inset), new Vector3(hw, ht, hl - inset), edge);
        Quad(verts, norms, uvs, tris, Vector3.forward,
            new Vector3(-hw + inset, -ht, hl), new Vector3(hw - inset, -ht, hl), new Vector3(hw - inset, ht, hl), new Vector3(-hw + inset, ht, hl), edge);
        Quad(verts, norms, uvs, tris, Vector3.back,
            new Vector3(hw - inset, -ht, -hl), new Vector3(-hw + inset, -ht, -hl), new Vector3(-hw + inset, ht, -hl), new Vector3(hw - inset, ht, -hl), edge);

        var mesh = new Mesh { name = $"Card {card}", hideFlags = HideFlags.DontSave };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }

    /// Adds a quad from corners given bottom-left, top-left, top-right, bottom-right as seen from
    /// the <paramref name="normal"/> side. Winding is corrected automatically.
    private static void Quad(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 normal,
                             Vector3 bl, Vector3 tl, Vector3 tr, Vector3 br, Rect uvRect)
    {
        int i = v.Count;
        v.Add(bl); v.Add(tl); v.Add(tr); v.Add(br);
        for (int k = 0; k < 4; k++) n.Add(normal);

        uv.Add(new Vector2(uvRect.xMin, uvRect.yMin)); uv.Add(new Vector2(uvRect.xMin, uvRect.yMax));
        uv.Add(new Vector2(uvRect.xMax, uvRect.yMax)); uv.Add(new Vector2(uvRect.xMax, uvRect.yMin));

        // Unity treats clockwise triangles as front-facing; flip if these corners wind the other way
        bool clockwise = Vector3.Dot(Vector3.Cross(tl - bl, tr - bl), normal) > 0f;
        if (clockwise) { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        else           { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
    }
}
