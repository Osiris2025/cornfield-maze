using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class CornPlant
{
    public const int VariantCount = 16;

    const int SubStalk = 0;
    const int SubLeaf = 1;
    const int SubCob = 2;
    const int SubHusk = 3;
    const int SubSilk = 4;
    const int SubTassel = 5;
    const int SubCount = 6;

    public static Material[] CreateMaterials()
    {
        var mats = new[]
        {
            Instanced(Materials.Lit(new Color(0.24f, 0.40f, 0.09f), 0.12f)),
            Instanced(Materials.Lit(new Color(0.38f, 0.60f, 0.13f), 0.10f)),
            Instanced(Materials.Lit(new Color(0.93f, 0.74f, 0.14f), 0.32f)),
            Instanced(Materials.Lit(new Color(0.52f, 0.68f, 0.28f), 0.12f)),
            Instanced(Materials.Lit(new Color(0.93f, 0.88f, 0.62f), 0.08f)),
            Instanced(Materials.Lit(new Color(0.70f, 0.64f, 0.30f), 0.08f))
        };
        return mats;
    }

    static Material Instanced(Material mat)
    {
        mat.enableInstancing = true;
        return mat;
    }

    public static void BuildVariants(System.Random rng, out Mesh[] meshes, out float[] heights)
    {
        meshes = new Mesh[VariantCount];
        heights = new float[VariantCount];
        for (int i = 0; i < VariantCount; i++)
        {
            float height = 2.85f + (float)rng.NextDouble() * 1.15f;
            heights[i] = height;
            var mesh = CreateMesh(rng, height);
            mesh.name = "CornPlant_" + i;
            mesh.UploadMeshData(false);
            meshes[i] = mesh;
        }
    }

    public static GameObject Spawn(Transform parent, Vector3 pos, Mesh mesh, float height, Material[] mats, System.Random rng)
    {
        var go = new GameObject("CornStalk");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        float leanX = ((float)rng.NextDouble() - 0.5f) * 12f;
        float leanZ = ((float)rng.NextDouble() - 0.5f) * 12f;
        float yaw = (float)rng.NextDouble() * 360f;
        float scale = 0.94f + (float)rng.NextDouble() * 0.12f;
        go.transform.rotation = Quaternion.Euler(leanX, yaw, leanZ);
        go.transform.localScale = Vector3.one * scale;

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = mats;
        renderer.shadowCastingMode = ShadowCastingMode.On;

        var col = go.AddComponent<CapsuleCollider>();
        col.direction = 1;
        col.radius = 0.11f;
        col.height = height;
        col.center = new Vector3(0f, height * 0.5f, 0f);

        var sway = go.AddComponent<CornSway>();
        sway.Init((float)rng.NextDouble() * 12f, 0.55f + (float)rng.NextDouble() * 0.45f, 2.8f + (float)rng.NextDouble() * 2.2f);
        return go;
    }

    static Mesh CreateMesh(System.Random rng, float height)
    {
        var b = new Builder();
        float baseR = 0.085f + (float)rng.NextDouble() * 0.03f;
        float topR = 0.032f + (float)rng.NextDouble() * 0.016f;
        b.AddCylinder(SubStalk, Vector3.zero, new Vector3(0f, height, 0f), baseR, topR, 8);
        b.AddDisk(SubStalk, Vector3.zero, Vector3.down, baseR, 8);

        int leafCount = 7 + rng.Next(4);
        for (int i = 0; i < leafCount; i++)
        {
            float t = (i + 0.35f) / (leafCount + 0.2f);
            float y = height * Mathf.Lerp(0.10f, 0.86f, t);
            float radius = Mathf.Lerp(baseR, topR, y / height);
            float yaw = i * 180f + ((float)rng.NextDouble() - 0.5f) * 28f;
            float length = Mathf.Lerp(0.85f, 1.65f, Mathf.Sin(t * Mathf.PI)) * (0.88f + (float)rng.NextDouble() * 0.22f);
            float width = Mathf.Lerp(0.12f, 0.22f, Mathf.Sin(t * Mathf.PI)) * (0.9f + (float)rng.NextDouble() * 0.2f);
            float startPitch = Mathf.Lerp(12f, 42f, t) + ((float)rng.NextDouble() - 0.5f) * 8f;
            float droop = Mathf.Lerp(62f, 28f, t) + ((float)rng.NextDouble() - 0.5f) * 10f;
            float twist = ((float)rng.NextDouble() - 0.5f) * 22f;
            var attach = Heading(0f, yaw) * new Vector3(0f, y, radius * 0.85f);
            attach.y = y;
            b.AddBlade(SubLeaf, attach, yaw, length, width, startPitch, droop, twist, 4);

            float nodeR = radius * 1.35f;
            float nodeH = 0.045f;
            b.AddCylinder(SubStalk, new Vector3(0f, y - nodeH * 0.5f, 0f), new Vector3(0f, y + nodeH * 0.5f, 0f), nodeR, nodeR * 0.92f, 8);
        }

        int cobs = rng.NextDouble() < 0.42 ? 2 : 1;
        for (int c = 0; c < cobs; c++)
        {
            float cobT = 0.40f + (float)rng.NextDouble() * 0.22f + c * 0.08f;
            float cobY = height * Mathf.Clamp01(cobT);
            float cobYaw = (float)rng.NextDouble() * 360f;
            AddCob(b, rng, cobY, cobYaw, Mathf.Lerp(baseR, topR, cobY / height));
        }

        AddTassel(b, rng, height, topR);
        return b.ToMesh();
    }

    static void AddCob(Builder b, System.Random rng, float y, float yaw, float stalkR)
    {
        float length = 0.30f + (float)rng.NextDouble() * 0.12f;
        float kernelR = 0.048f + (float)rng.NextDouble() * 0.014f;
        float lift = 16f + (float)rng.NextDouble() * 12f;
        var attach = new Vector3(0f, y, 0f) + Heading(0f, yaw) * new Vector3(0f, 0f, stalkR * 0.9f);
        attach.y = y;
        Vector3 axis = Heading(lift, yaw) * Vector3.forward;
        Vector3 tip = attach + axis * length;

        b.AddCylinder(SubCob, attach, tip, kernelR * 0.92f, kernelR * 0.78f, 8);
        b.AddDisk(SubCob, tip, axis, kernelR * 0.78f, 8);

        int rings = 4;
        int around = 6;
        for (int r = 0; r < rings; r++)
        {
            float t = 0.55f + 0.38f * (r / (float)(rings - 1));
            var center = attach + axis * (length * t);
            float rowR = Mathf.Lerp(kernelR * 0.92f, kernelR * 0.78f, t);
            for (int k = 0; k < around; k++)
            {
                float a = (k + (r % 2) * 0.5f) * (360f / around);
                var radial = Quaternion.AngleAxis(a, axis) * Vector3.Cross(axis, Vector3.up).normalized;
                if (radial.sqrMagnitude < 0.01f) radial = Quaternion.AngleAxis(a, axis) * Vector3.right;
                var nub = center + radial * rowR;
                b.AddBox(SubCob, nub, new Vector3(0.016f, 0.014f, 0.018f), Quaternion.LookRotation(radial, axis));
            }
        }

        int husks = 4 + rng.Next(2);
        for (int h = 0; h < husks; h++)
        {
            float huskYaw = yaw + h * (360f / husks) + ((float)rng.NextDouble() - 0.5f) * 16f;
            bool peel = h >= husks - 2;
            float startPitch = lift + (peel ? 8f : -4f);
            float droop = peel ? 38f + (float)rng.NextDouble() * 18f : 10f;
            float huskLen = length * (peel ? 0.95f : 1.12f);
            float huskW = 0.075f + (float)rng.NextDouble() * 0.03f;
            b.AddBlade(SubHusk, attach, huskYaw, huskLen, huskW, startPitch, droop, ((float)rng.NextDouble() - 0.5f) * 10f, 3);
        }

        int silks = 5 + rng.Next(3);
        for (int s = 0; s < silks; s++)
        {
            float silkYaw = yaw + ((float)rng.NextDouble() - 0.5f) * 80f;
            float silkPitch = -8f + (float)rng.NextDouble() * 36f;
            Vector3 silkDir = Heading(silkPitch, silkYaw) * Vector3.forward;
            float silkLen = 0.07f + (float)rng.NextDouble() * 0.10f;
            Vector3 silkA = tip + axis * 0.01f;
            Vector3 silkB = silkA + silkDir * silkLen * 0.55f;
            Vector3 silkC = silkB + (silkDir + Vector3.down * 0.55f).normalized * silkLen * 0.5f;
            b.AddCylinder(SubSilk, silkA, silkB, 0.0055f, 0.0035f, 4);
            b.AddCylinder(SubSilk, silkB, silkC, 0.0035f, 0.0022f, 4);
        }
    }

    static void AddTassel(Builder b, System.Random rng, float height, float topR)
    {
        var crown = new Vector3(0f, height - 0.02f, 0f);
        b.AddCylinder(SubTassel, crown, crown + Vector3.up * 0.26f, 0.013f, 0.005f, 5);

        int branches = 9 + rng.Next(5);
        for (int i = 0; i < branches; i++)
        {
            float yaw = i * (360f / branches) + ((float)rng.NextDouble() - 0.5f) * 18f;
            float elev = 50f + (float)rng.NextDouble() * 28f;
            float len = 0.16f + (float)rng.NextDouble() * 0.16f;
            Vector3 dir = Heading(elev, yaw) * Vector3.forward;
            Vector3 a = crown + Vector3.up * (0.03f + (float)rng.NextDouble() * 0.08f);
            Vector3 mid = a + dir * len * 0.55f;
            Vector3 end = mid + (dir + Vector3.down * 0.4f).normalized * len * 0.5f;
            b.AddCylinder(SubTassel, a, mid, 0.0075f, 0.0045f, 4);
            b.AddCylinder(SubTassel, mid, end, 0.0045f, 0.0024f, 4);

            if (rng.NextDouble() < 0.55)
            {
                float sideYaw = yaw + 18f + (float)rng.NextDouble() * 16f;
                Vector3 side = Heading(elev - 12f, sideYaw) * Vector3.forward;
                Vector3 sEnd = mid + side * (len * 0.42f);
                b.AddCylinder(SubTassel, mid, sEnd, 0.0038f, 0.002f, 3);
            }
        }

        int wisps = 6 + rng.Next(4);
        for (int i = 0; i < wisps; i++)
        {
            float yaw = (float)rng.NextDouble() * 360f;
            float elev = 35f + (float)rng.NextDouble() * 40f;
            Vector3 dir = Heading(elev, yaw) * Vector3.forward;
            Vector3 a = crown + Vector3.up * 0.16f + dir * topR;
            b.AddCylinder(SubTassel, a, a + dir * (0.10f + (float)rng.NextDouble() * 0.08f), 0.0032f, 0.0016f, 3);
        }
    }

    static Quaternion Heading(float pitchUp, float yaw) => Quaternion.Euler(-pitchUp, yaw, 0f);

    sealed class Builder
    {
        readonly List<Vector3>[] _verts = new List<Vector3>[SubCount];
        readonly List<int>[] _tris = new List<int>[SubCount];

        public Builder()
        {
            for (int i = 0; i < SubCount; i++)
            {
                _verts[i] = new List<Vector3>(256);
                _tris[i] = new List<int>(512);
            }
        }

        public void AddTri(int sub, Vector3 a, Vector3 b, Vector3 c)
        {
            int i = _verts[sub].Count;
            _verts[sub].Add(a);
            _verts[sub].Add(b);
            _verts[sub].Add(c);
            _tris[sub].Add(i);
            _tris[sub].Add(i + 1);
            _tris[sub].Add(i + 2);
        }

        public void AddQuad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool twoSided)
        {
            AddTri(sub, a, b, c);
            AddTri(sub, a, c, d);
            if (twoSided)
            {
                AddTri(sub, a, c, b);
                AddTri(sub, a, d, c);
            }
        }

        public void AddCylinder(int sub, Vector3 bottom, Vector3 top, float r0, float r1, int sides)
        {
            Vector3 axis = top - bottom;
            float len = axis.magnitude;
            if (len < 1e-5f) return;
            Vector3 n = axis / len;
            Vector3 side = Vector3.Cross(n, Mathf.Abs(n.y) < 0.92f ? Vector3.up : Vector3.right).normalized;
            Vector3 binormal = Vector3.Cross(n, side);

            var ring0 = new Vector3[sides];
            var ring1 = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                Vector3 radial = Mathf.Cos(a) * side + Mathf.Sin(a) * binormal;
                ring0[i] = bottom + radial * r0;
                ring1[i] = top + radial * r1;
            }

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                AddQuad(sub, ring0[i], ring0[j], ring1[j], ring1[i], false);
            }
        }

        public void AddDisk(int sub, Vector3 center, Vector3 normal, float radius, int sides)
        {
            Vector3 n = normal.normalized;
            Vector3 side = Vector3.Cross(n, Mathf.Abs(n.y) < 0.92f ? Vector3.up : Vector3.right).normalized;
            Vector3 binormal = Vector3.Cross(n, side);
            Vector3 prev = center + side * radius;
            for (int i = 1; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                Vector3 next = center + (Mathf.Cos(a) * side + Mathf.Sin(a) * binormal) * radius;
                AddTri(sub, center, prev, next);
                prev = next;
            }
        }

        public void AddBox(int sub, Vector3 center, Vector3 size, Quaternion rot)
        {
            Vector3 hx = rot * new Vector3(size.x, 0f, 0f);
            Vector3 hy = rot * new Vector3(0f, size.y, 0f);
            Vector3 hz = rot * new Vector3(0f, 0f, size.z);
            Vector3 p000 = center - hx - hy - hz;
            Vector3 p001 = center - hx - hy + hz;
            Vector3 p010 = center - hx + hy - hz;
            Vector3 p011 = center - hx + hy + hz;
            Vector3 p100 = center + hx - hy - hz;
            Vector3 p101 = center + hx - hy + hz;
            Vector3 p110 = center + hx + hy - hz;
            Vector3 p111 = center + hx + hy + hz;
            AddQuad(sub, p000, p100, p101, p001, false);
            AddQuad(sub, p010, p011, p111, p110, false);
            AddQuad(sub, p000, p001, p011, p010, false);
            AddQuad(sub, p100, p110, p111, p101, false);
            AddQuad(sub, p000, p010, p110, p100, false);
            AddQuad(sub, p001, p101, p111, p011, false);
        }

        public void AddBlade(int sub, Vector3 attach, float yaw, float length, float width, float startPitch, float droop, float twist, int segs)
        {
            var centers = new Vector3[segs + 1];
            var rights = new Vector3[segs + 1];
            var ups = new Vector3[segs + 1];
            var widths = new float[segs + 1];

            Vector3 p = attach;
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                float pitch = startPitch - droop * t;
                var rot = Quaternion.Euler(-pitch, yaw, twist * t);
                Vector3 fwd = rot * Vector3.forward;
                centers[i] = p;
                Vector3 right = Vector3.Cross(Vector3.up, fwd);
                if (right.sqrMagnitude < 1e-4f) right = rot * Vector3.right;
                right.Normalize();
                rights[i] = right;
                ups[i] = Vector3.Cross(fwd, right).normalized;
                widths[i] = WidthAt(t) * width;
                if (i < segs)
                    p += fwd * (length / segs);
            }

            for (int i = 0; i < segs; i++)
            {
                Vector3 l0 = centers[i] - rights[i] * (widths[i] * 0.5f) - ups[i] * 0.012f;
                Vector3 r0 = centers[i] + rights[i] * (widths[i] * 0.5f) - ups[i] * 0.012f;
                Vector3 m0 = centers[i] + ups[i] * 0.006f;
                Vector3 l1 = centers[i + 1] - rights[i + 1] * (widths[i + 1] * 0.5f) - ups[i + 1] * 0.012f;
                Vector3 r1 = centers[i + 1] + rights[i + 1] * (widths[i + 1] * 0.5f) - ups[i + 1] * 0.012f;
                Vector3 m1 = centers[i + 1] + ups[i + 1] * 0.006f;
                AddQuad(sub, l0, m0, m1, l1, true);
                AddQuad(sub, m0, r0, r1, m1, true);
            }
        }

        static float WidthAt(float t)
        {
            if (t < 0.28f) return Mathf.Lerp(0.38f, 1f, t / 0.28f);
            return Mathf.Sqrt(Mathf.Max(0f, 1f - (t - 0.28f) / 0.72f));
        }

        public Mesh ToMesh()
        {
            var mesh = new Mesh { name = "CornPlant" };
            var all = new List<Vector3>(1024);
            mesh.subMeshCount = SubCount;
            var offsets = new int[SubCount];
            for (int s = 0; s < SubCount; s++)
            {
                offsets[s] = all.Count;
                all.AddRange(_verts[s]);
            }

            mesh.indexFormat = all.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(all);
            for (int s = 0; s < SubCount; s++)
            {
                var tris = _tris[s];
                var shifted = new int[tris.Count];
                int off = offsets[s];
                for (int i = 0; i < tris.Count; i++)
                    shifted[i] = tris[i] + off;
                mesh.SetTriangles(shifted, s, true);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
