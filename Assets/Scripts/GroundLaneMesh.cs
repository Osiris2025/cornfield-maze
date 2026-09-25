using UnityEngine;

/// <summary>
/// M31 (§17): one lane piece — a flat quad with lane-local UVs. The boundary is not geometry any more.
///
/// M29 built this as a strip whose margin was ragged in GEOMETRY: boundary vertices were pulled inwards on a
/// derived mask, so the join with the field was a cut line with polygon facets. Todd's verdict on that was
/// "bubbly and janky", and M29's own edge frame agrees — a margin that wanders 0.42 m is still a CUT, just
/// an irregular one. Geometry is not transparency.
///
/// The lane is now a plain rectangle laid over the field, and the whole boundary is the alpha fade baked
/// into `T_Ground_LaneA` (see scripts/m31_lane_alpha_bake.py): solid down the middle, gone at both edges,
/// with fine grain eating into the ramp. That fade reaches zero exactly at this mesh's edge, so there is no
/// line where the layer stops — which is the defect the milestone exists to remove.
///
/// The UVs are lane-local, and that is the one thing a fade across a lane needs: **U runs along the lane,
/// V across it**. M29's world-space UVs tiled continuously across pieces (good for gravel) but they have no
/// idea which way the lane runs, so a fade driven from them would cross the lane at whatever angle the
/// world axes happened to give it.
/// </summary>
public static class GroundLaneMesh
{
    /// <summary>Metres of ground per repeat along the lane. The material tiles once; the repeat lives in
    /// the UVs, so the gravel stays continuous across the pieces where two lanes meet.</summary>
    public const float TileMetres = 2f;

    /// <summary>
    /// A flat lane piece: <paramref name="w"/> along X, <paramref name="d"/> along Z, centred on
    /// <paramref name="centre"/>. Four vertices, two triangles — the ragged boundary is gone, so the
    /// 625-vertex grid M29 needed to carry it is gone with it.
    ///
    /// <paramref name="uAlongX"/> picks which way the fade runs, and it is NOT a matter of which side is
    /// longer: a square cell at a lane's turn is square either way, and only the maze knows which of its
    /// edges face corn. Get it wrong and the fade runs down the lane instead of across it, which is a hard
    /// cut with a soft stripe next to it.
    /// </summary>
    public static Mesh Build(float w, float d, Vector3 centre, bool uAlongX)
    {
        float halfW = w * 0.5f;
        float halfD = d * 0.5f;

        var verts = new Vector3[4];
        var uvs = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            float sx = (i == 1 || i == 3) ? 1f : -1f;
            float sz = (i >= 2) ? 1f : -1f;
            float lx = sx * halfW;
            float lz = sz * halfD;
            verts[i] = new Vector3(lx, 0f, lz);
            // U: metres along the lane, so the gravel tiles continuously across piece boundaries.
            // V: 0..1 across the lane, centred — the strip's middle sits on the lane's middle.
            uvs[i] = uAlongX
                ? new Vector2((centre.x + lx) / TileMetres, 0.5f + sz * 0.5f)
                : new Vector2((centre.z + lz) / TileMetres, 0.5f + sx * 0.5f);
        }

        var mesh = new Mesh { name = "LanePiece" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = new[] { 0, 3, 1, 0, 2, 3 };   // wound so the normal faces up
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
