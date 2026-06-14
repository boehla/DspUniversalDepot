using System;
using System.Collections.Generic;
using UnityEngine;

namespace DspUniversalDepot {
    /// <summary>
    /// Procedural mesh builder for the Universal Depot's fully custom 3D model.
    ///
    /// DSP renders placed buildings via GPU instancing: <c>BatchRenderer</c> calls
    /// <c>Graphics.DrawMeshInstancedIndirect(mesh, …)</c> while the instanced building shader
    /// samples per-vertex geometry from a <see cref="VertaBuffer"/> StructuredBuffer
    /// (<c>_VertaBuffer</c>) that is bound via <c>SetToAnimMaterial</c>. So a custom model needs
    /// BOTH a <see cref="Mesh"/> (drives index/submesh/vertex count) AND a matching verta whose
    /// floats are laid out in the same vertex order and <see cref="VertType"/> the game uses
    /// (interleaved position, normal, tangent, [color]). <see cref="BuildVerta"/> produces that
    /// verta straight from the mesh, so the two can never disagree.
    ///
    /// The geometry is assembled from flat-shaded primitives (boxes + n-gon prisms/frustums).
    /// Each face owns its four vertices so edges stay crisp, and <see cref="Builder.AddQuad"/>
    /// fixes winding against an outward hint — geometry is always front-facing regardless of how
    /// the corners were ordered, which matters because the station material back-face culls.
    /// </summary>
    internal static class DepotMesh {
        /// <summary>
        /// Build the depot mesh fitted into <paramref name="fit"/> (the PLS mesh bounds, so the
        /// footprint/pivot match the station's belt and drone ports). When
        /// <paramref name="debugBox"/> is set, returns a single box filling the bounds — the
        /// prototype shape used to validate the verta layout before committing to the real model.
        /// </summary>
        public static Mesh Build(Bounds fit, bool debugBox) {
            Builder mb = new Builder();
            if(debugBox) {
                mb.AddBox(fit.center, fit.size * 0.9f);
            } else {
                BuildDepot(mb, fit);
            }
            return mb.ToMesh("UniversalDepotMesh");
        }

        /// <summary>
        /// The depot silhouette, a square base → hexagonal tower: a wide square base plinth and
        /// platform (axis-aligned, so the four flat sides line up with the belt/drone ports — you
        /// can read at a glance where the conveyors enter), a collar, a hexagonal silo body, a
        /// tapered cap and rim disc, a thin antenna mast with a beacon, and four supply crates in
        /// the square's corners (clear of the belt sides). All sized relative to the PLS footprint.
        /// </summary>
        private static void BuildDepot(Builder mb, Bounds b) {
            float fy = b.size.y;
            float foot = Mathf.Min(b.size.x, b.size.z);
            float cx = b.center.x, cz = b.center.z;
            float y = b.center.y - b.extents.y;   // running base height, raised as we stack

            // Low SQUARE base plinth — axis-aligned, kept well inside the footprint so the belt
            // ports stay visible around it, and its flat sides face the conveyors.
            float plinthH = fy * 0.07f;
            float plinthW = foot * 0.70f;
            mb.AddBox(new Vector3(cx, y + plinthH * 0.5f, cz), new Vector3(plinthW, plinthH, plinthW));
            y += plinthH;

            // Square platform.
            float platH = fy * 0.13f;
            float platW = foot * 0.60f;
            mb.AddBox(new Vector3(cx, y + platH * 0.5f, cz), new Vector3(platW, platH, platW));
            y += platH;
            float platTop = y;                     // crates sit on this level

            // Hexagonal collar easing the square platform into the round silo.
            float collarH = fy * 0.05f;
            mb.AddPrism(new Vector3(cx, y + collarH * 0.5f, cz), foot * 0.29f, foot * 0.29f, collarH, 6);
            y += collarH;

            // Hexagonal silo body — a touch narrower than the platform so the square base reads.
            float bodyH = fy * 0.46f;
            mb.AddPrism(new Vector3(cx, y + bodyH * 0.5f, cz), foot * 0.27f, foot * 0.27f, bodyH, 6);
            y += bodyH;

            // Tapered hexagonal cap (frustum narrowing toward the top).
            float capH = fy * 0.16f;
            mb.AddPrism(new Vector3(cx, y + capH * 0.5f, cz), foot * 0.27f, foot * 0.12f, capH, 6);
            y += capH;

            // Thin rim disc capping the frustum.
            float rimH = fy * 0.03f;
            mb.AddPrism(new Vector3(cx, y + rimH * 0.5f, cz), foot * 0.16f, foot * 0.16f, rimH, 6);
            y += rimH;

            // Thin antenna mast with a beacon cube at the tip.
            float mastH = fy * 0.18f;
            mb.AddBox(new Vector3(cx, y + mastH * 0.5f, cz), new Vector3(foot * 0.03f, mastH, foot * 0.03f));
            y += mastH;
            float beacon = foot * 0.06f;
            mb.AddBox(new Vector3(cx, y + beacon * 0.5f, cz), new Vector3(beacon, beacon, beacon));

            // Four corner supply crates tucked into the square platform's corners (beside the
            // silo, clear of the flat belt sides), alternating tall/short for variety.
            float crate = foot * 0.11f;
            float ring = foot * 0.30f;
            for(int i = 0; i < 4; i++) {
                float ang = (i * 90f + 45f) * Mathf.Deg2Rad;
                float px = cx + Mathf.Cos(ang) * ring;
                float pz = cz + Mathf.Sin(ang) * ring;
                float ch = (i % 2 == 0) ? foot * 0.15f : foot * 0.10f;
                mb.AddBox(new Vector3(px, platTop + ch * 0.5f, pz), new Vector3(crate, ch, crate));
            }
        }

        /// <summary>
        /// Build a <see cref="VertaBuffer"/> from <paramref name="mesh"/> in the same
        /// <paramref name="type"/> the PLS prefab used, so DSP's instanced building shader reads
        /// our geometry. Layout is the canonical interleave implied by the enum's float count:
        /// V=pos(3), VN=+normal(3), VNT=+tangent(3), VNTC=+color(1). The mesh must already carry
        /// normals + tangents (it does — <see cref="Builder.ToMesh"/> sets them).
        /// </summary>
        public static VertaBuffer BuildVerta(Mesh mesh, VertType type) {
            int n = mesh.vertexCount;
            int size = (int)type;
            VertaBuffer vb = new VertaBuffer();
            vb.Expand(type, n, 1);

            Vector3[] vs = mesh.vertices;
            Vector3[] ns = mesh.normals;
            Vector4[] ts = mesh.tangents;
            float[] d = vb.data;
            for(int i = 0; i < n; i++) {
                int o = i * size;
                d[o] = vs[i].x; d[o + 1] = vs[i].y; d[o + 2] = vs[i].z;
                if(size >= 6) { d[o + 3] = ns[i].x; d[o + 4] = ns[i].y; d[o + 5] = ns[i].z; }
                if(size >= 9) { d[o + 6] = ts[i].x; d[o + 7] = ts[i].y; d[o + 8] = ts[i].z; }
                if(size >= 10) { d[o + 9] = 1f; }   // per-vertex colour: opaque white (no tint)
            }
            vb.SetBufferData();
            return vb;
        }

        /// <summary>
        /// Accumulates flat-shaded geometry. Vertices are never shared between faces, so every
        /// face gets a hard edge and its own outward normal.
        /// </summary>
        private sealed class Builder {
            private readonly List<Vector3> _verts = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();
            private readonly List<int> _tris = new List<int>();

            /// <summary>Axis-aligned box centred at <paramref name="center"/>.</summary>
            public void AddBox(Vector3 center, Vector3 size) {
                Vector3 h = size * 0.5f;
                // 8 corners
                Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
                Vector3 p001 = center + new Vector3(-h.x, -h.y, h.z);
                Vector3 p010 = center + new Vector3(-h.x, h.y, -h.z);
                Vector3 p011 = center + new Vector3(-h.x, h.y, h.z);
                Vector3 p100 = center + new Vector3(h.x, -h.y, -h.z);
                Vector3 p101 = center + new Vector3(h.x, -h.y, h.z);
                Vector3 p110 = center + new Vector3(h.x, h.y, -h.z);
                Vector3 p111 = center + new Vector3(h.x, h.y, h.z);

                AddQuad(p100, p110, p111, p101, Vector3.right);    // +X
                AddQuad(p001, p011, p010, p000, Vector3.left);     // -X
                AddQuad(p010, p011, p111, p110, Vector3.up);       // +Y
                AddQuad(p000, p100, p101, p001, Vector3.down);     // -Y
                AddQuad(p101, p111, p011, p001, Vector3.forward);  // +Z
                AddQuad(p000, p010, p110, p100, Vector3.back);     // -Z
            }

            /// <summary>
            /// Vertical n-gon prism / frustum / cone centred at <paramref name="center"/>.
            /// <paramref name="rBottom"/>/<paramref name="rTop"/> give the bottom/top radii (equal
            /// = cylinder, top 0 = cone). Adds the side faces plus the bottom cap and, unless the
            /// top is a point, the top cap.
            /// </summary>
            public void AddPrism(Vector3 center, float rBottom, float rTop, float height, int sides) {
                float yB = center.y - height * 0.5f;
                float yT = center.y + height * 0.5f;
                bool pointTop = rTop < 1e-4f;
                Vector3 topCenter = new Vector3(center.x, yT, center.z);
                Vector3 botCenter = new Vector3(center.x, yB, center.z);

                for(int i = 0; i < sides; i++) {
                    float a0 = (float)i / sides * Mathf.PI * 2f;
                    float a1 = (float)(i + 1) / sides * Mathf.PI * 2f;
                    float am = (a0 + a1) * 0.5f;
                    Vector3 outward = new Vector3(Mathf.Cos(am), 0f, Mathf.Sin(am));

                    Vector3 b0 = new Vector3(center.x + Mathf.Cos(a0) * rBottom, yB, center.z + Mathf.Sin(a0) * rBottom);
                    Vector3 b1 = new Vector3(center.x + Mathf.Cos(a1) * rBottom, yB, center.z + Mathf.Sin(a1) * rBottom);

                    if(pointTop) {
                        AddTriangle(b0, b1, topCenter, outward);
                    } else {
                        Vector3 t0 = new Vector3(center.x + Mathf.Cos(a0) * rTop, yT, center.z + Mathf.Sin(a0) * rTop);
                        Vector3 t1 = new Vector3(center.x + Mathf.Cos(a1) * rTop, yT, center.z + Mathf.Sin(a1) * rTop);
                        AddQuad(b0, b1, t1, t0, outward);
                        AddTriangle(t0, t1, topCenter, Vector3.up);
                    }
                    AddTriangle(b1, b0, botCenter, Vector3.down);
                }
            }

            /// <summary>
            /// Quad a→b→c→d with UVs (0,0)(1,0)(1,1)(0,1). Winding is corrected so the front face
            /// points along <paramref name="hint"/>; all four vertices get that flat normal.
            /// </summary>
            public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 hint) {
                Vector3 nrm = Vector3.Cross(b - a, c - a).normalized;
                bool flip = Vector3.Dot(nrm, hint) < 0f;
                if(flip) nrm = -nrm;

                int i0 = _verts.Count;
                _verts.Add(a); _verts.Add(b); _verts.Add(c); _verts.Add(d);
                _uvs.Add(new Vector2(0, 0)); _uvs.Add(new Vector2(1, 0));
                _uvs.Add(new Vector2(1, 1)); _uvs.Add(new Vector2(0, 1));
                for(int k = 0; k < 4; k++) _normals.Add(nrm);

                if(!flip) {
                    _tris.Add(i0); _tris.Add(i0 + 1); _tris.Add(i0 + 2);
                    _tris.Add(i0); _tris.Add(i0 + 2); _tris.Add(i0 + 3);
                } else {
                    _tris.Add(i0); _tris.Add(i0 + 2); _tris.Add(i0 + 1);
                    _tris.Add(i0); _tris.Add(i0 + 3); _tris.Add(i0 + 2);
                }
            }

            /// <summary>Triangle a→b→c with UVs (0,0)(1,0)(0.5,1), winding fixed against <paramref name="hint"/>.</summary>
            public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 hint) {
                Vector3 nrm = Vector3.Cross(b - a, c - a).normalized;
                bool flip = Vector3.Dot(nrm, hint) < 0f;
                if(flip) nrm = -nrm;

                int i0 = _verts.Count;
                _verts.Add(a); _verts.Add(b); _verts.Add(c);
                _uvs.Add(new Vector2(0, 0)); _uvs.Add(new Vector2(1, 0)); _uvs.Add(new Vector2(0.5f, 1));
                for(int k = 0; k < 3; k++) _normals.Add(nrm);

                if(!flip) { _tris.Add(i0); _tris.Add(i0 + 1); _tris.Add(i0 + 2); }
                else { _tris.Add(i0); _tris.Add(i0 + 2); _tris.Add(i0 + 1); }
            }

            public Mesh ToMesh(string name) {
                Mesh mesh = new Mesh { name = name };
                if(_verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(_verts);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);
                mesh.SetTriangles(_tris, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();   // needs normals + UVs, both set above
                return mesh;
            }
        }
    }
}
