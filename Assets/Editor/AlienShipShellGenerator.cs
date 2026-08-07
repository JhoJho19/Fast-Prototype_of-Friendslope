using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.MeshOperations;
using UnityEditor.ProBuilder;
using UnityEditor.SceneManagement;

public static class AlienShipShellGenerator
{
    private const string RootName = "AlienShipShell";
    private const string MaterialsFolder = "Assets/_Project/Materials/AlienShip";

    private const float HalfW = 5f;
    private const float HalfD = 4f;
    private const float Chamfer = 1.2f;
    private const float WallThickness = 0.3f;
    private const float WallBaseY = 0.3f;
    private const float WallTopY = 3.3f;
    private const float LeanK = 1.025f;
    private const float Overhang = 0.15f;
    private const float FloorThickness = 0.3f;
    private const float RoofThickness = 0.25f;

    [MenuItem("Tools/Level Design/Create Alien Ship Shell")]
    public static void CreateAlienShipShell()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Cannot create the alien ship shell while in Play Mode.");
            return;
        }

        if (GameObject.Find(RootName) != null)
        {
            Debug.LogWarning(RootName + " already exists in the scene. Aborting creation.");
            return;
        }

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Alien Ship Shell");
        root.transform.position = Vector3.zero;
        root.transform.localScale = Vector3.one;

        Material[] mats = LoadOrCreateMaterials();

        var wallsParent = new GameObject("Ship_Walls");
        Undo.RegisterCreatedObjectUndo(wallsParent, "Create Alien Ship Shell part");
        wallsParent.transform.SetParent(root.transform, false);
        wallsParent.transform.localPosition = Vector3.zero;

        BuildFloor(root.transform, mats);
        BuildWalls(wallsParent.transform, mats);
        BuildBelts(wallsParent.transform, mats);
        BuildPanels(wallsParent.transform, mats);
        BuildRoof(root.transform, mats);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log(RootName + " generated successfully.");
    }

    private static List<Vector3> ChamferRing(float hw, float hd, float c)
    {
        return new List<Vector3>
        {
            new Vector3(hw - c, 0f, hd),
            new Vector3(hw, 0f, hd - c),
            new Vector3(hw, 0f, -hd + c),
            new Vector3(hw - c, 0f, -hd),
            new Vector3(-hw + c, 0f, -hd),
            new Vector3(-hw, 0f, -hd + c),
            new Vector3(-hw, 0f, hd - c),
            new Vector3(-hw + c, 0f, hd)
        };
    }

    private static void AddQuad(List<Vector3> verts, List<Face> faces, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int n = verts.Count;
        verts.Add(a);
        verts.Add(b);
        verts.Add(c);
        verts.Add(d);
        faces.Add(new Face(new[] { n, n + 1, n + 2 }));
        faces.Add(new Face(new[] { n, n + 2, n + 3 }));
    }

    private static void AddPrism(List<Vector3> verts, List<Face> faces,
        Vector3 A0, Vector3 B0, Vector3 a0, Vector3 b0,
        Vector3 A1, Vector3 B1, Vector3 a1, Vector3 b1)
    {
        AddQuad(verts, faces, A0, B0, B1, A1);
        AddQuad(verts, faces, a0, a1, b1, b0);
        AddQuad(verts, faces, A0, a0, a1, A1);
        AddQuad(verts, faces, B0, B1, b1, b0);
        AddQuad(verts, faces, A1, a1, b1, B1);
        AddQuad(verts, faces, A0, B0, b0, a0);
    }

    private static void AddSlab(List<Vector3> verts, List<Face> faces, List<Vector3> ringB, float yB, List<Vector3> ringT, float yT)
    {
        int n = verts.Count;
        for (int i = 0; i < 8; i++)
            verts.Add(ringB[i] + new Vector3(0f, yB, 0f));
        for (int i = 0; i < 8; i++)
            verts.Add(ringT[i] + new Vector3(0f, yT, 0f));
        for (int i = 0; i < 8; i++)
        {
            int j = (i + 1) % 8;
            faces.Add(new Face(new[] { n + i, n + j, n + 8 + j }));
            faces.Add(new Face(new[] { n + i, n + 8 + j, n + 8 + i }));
        }
        for (int i = 1; i < 7; i++)
            faces.Add(new Face(new[] { n + 8, n + 8 + i, n + 8 + i + 1 }));
        for (int i = 1; i < 7; i++)
            faces.Add(new Face(new[] { n, n + i + 1, n + i }));
    }

    private static void AddBox(List<Vector3> verts, List<Face> faces, Vector3 center, Vector3 size)
    {
        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        float hz = size.z * 0.5f;
        Vector3 p000 = center + new Vector3(-hx, -hy, -hz);
        Vector3 p100 = center + new Vector3(hx, -hy, -hz);
        Vector3 p110 = center + new Vector3(hx, -hy, hz);
        Vector3 p010 = center + new Vector3(-hx, -hy, hz);
        Vector3 p001 = center + new Vector3(-hx, hy, -hz);
        Vector3 p101 = center + new Vector3(hx, hy, -hz);
        Vector3 p111 = center + new Vector3(hx, hy, hz);
        Vector3 p011 = center + new Vector3(-hx, hy, hz);
        AddQuad(verts, faces, p000, p001, p101, p100);
        AddQuad(verts, faces, p010, p110, p111, p011);
        AddQuad(verts, faces, p000, p100, p110, p010);
        AddQuad(verts, faces, p001, p011, p111, p101);
        AddQuad(verts, faces, p000, p010, p011, p001);
        AddQuad(verts, faces, p100, p101, p111, p110);
    }

    private static void OrientFacesOutward(List<Vector3> verts, List<Face> faces)
    {
        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < verts.Count; i++)
            centroid += verts[i];
        centroid /= verts.Count;

        foreach (Face f in faces)
        {
            System.Collections.ObjectModel.ReadOnlyCollection<int> idx = f.indexes;
            Vector3 a = verts[idx[0]];
            Vector3 b = verts[idx[1]];
            Vector3 c = verts[idx[2]];
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f)
                continue;
            Vector3 fc = Vector3.zero;
            for (int k = 0; k < idx.Count; k++)
                fc += verts[idx[k]];
            fc /= idx.Count;
            if (Vector3.Dot(n, fc - centroid) < 0f)
                f.Reverse();
        }
    }

    private static void BuildPart(string name, Transform parent, List<Vector3> verts, List<Face> faces, Material mat)
    {
        var pb = ProBuilderMesh.Create(verts, faces);
        var go = pb.gameObject;
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        pb.RebuildWithPositionsAndFaces(verts, faces);
        OrientFacesOutward(verts, faces);
        pb.SetMaterial(faces, mat);
        MeshTransform.CenterPivot(pb, new int[0]);
        pb.ToMesh(MeshTopology.Triangles);
        pb.Refresh(RefreshMask.All);
        var mc = go.GetComponent<MeshCollider>();
        if (mc == null)
            mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        EditorMeshUtility.RebuildColliders(pb);
        Undo.RegisterCreatedObjectUndo(go, "Create Alien Ship Shell part");
    }

    private static void BuildFloor(Transform parent, Material[] mats)
    {
        var verts = new List<Vector3>();
        var faces = new List<Face>();
        var ring = ChamferRing(HalfW + 0.1f, HalfD + 0.1f, Chamfer);
        AddSlab(verts, faces, ring, 0f, ring, FloorThickness);
        BuildPart("Ship_Floor", parent, verts, faces, mats[0]);
    }

    private static void BuildWalls(Transform parent, Material[] mats)
    {
        var outerB = ChamferRing(HalfW, HalfD, Chamfer);
        var outerT = ChamferRing(HalfW * LeanK, HalfD * LeanK, Chamfer);
        var innerB = ChamferRing(HalfW - WallThickness, HalfD - WallThickness, Chamfer);
        var innerT = ChamferRing(HalfW * LeanK - WallThickness, HalfD * LeanK - WallThickness, Chamfer);

        string[] names =
        {
            "Wall_Front", "Wall_Corner_FR", "Wall_Right", "Wall_Corner_BR",
            "Wall_Back", "Wall_Corner_BL", "Wall_Left", "Wall_Corner_FL"
        };
        int[][] segs =
        {
            new[] { 7, 0 }, new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 },
            new[] { 3, 4 }, new[] { 4, 5 }, new[] { 5, 6 }, new[] { 6, 7 }
        };

        for (int s = 0; s < 8; s++)
        {
            int i = segs[s][0];
            int j = segs[s][1];
            var verts = new List<Vector3>();
            var faces = new List<Face>();
            Vector3 up = Vector3.up;
            AddPrism(verts, faces,
                outerB[i] + up * WallBaseY, outerB[j] + up * WallBaseY,
                innerB[i] + up * WallBaseY, innerB[j] + up * WallBaseY,
                outerT[i] + up * WallTopY, outerT[j] + up * WallTopY,
                innerT[i] + up * WallTopY, innerT[j] + up * WallTopY);
            BuildPart(names[s], parent, verts, faces, mats[1]);
        }
    }

    private static void BuildBelts(Transform parent, Material[] mats)
    {
        var outerB = ChamferRing(HalfW, HalfD, Chamfer);
        var outerT = ChamferRing(HalfW * LeanK, HalfD * LeanK, Chamfer);
        var beltB = ChamferRing(HalfW + 0.08f, HalfD + 0.08f, Chamfer);
        var beltT = ChamferRing(HalfW * LeanK + 0.08f, HalfD * LeanK + 0.08f, Chamfer);

        int[][] segs =
        {
            new[] { 7, 0 }, new[] { 0, 1 }, new[] { 1, 2 }, new[] { 2, 3 },
            new[] { 3, 4 }, new[] { 4, 5 }, new[] { 5, 6 }, new[] { 6, 7 }
        };

        var lower = new List<Vector3>();
        var lowerF = new List<Face>();
        var upper = new List<Vector3>();
        var upperF = new List<Face>();
        Vector3 up = Vector3.up;

        for (int s = 0; s < 8; s++)
        {
            int i = segs[s][0];
            int j = segs[s][1];
            AddPrism(lower, lowerF,
                beltB[i] + up * WallBaseY, beltB[j] + up * WallBaseY,
                outerB[i] + up * WallBaseY, outerB[j] + up * WallBaseY,
                beltB[i] + up * 0.75f, beltB[j] + up * 0.75f,
                outerB[i] + up * 0.75f, outerB[j] + up * 0.75f);
            AddPrism(upper, upperF,
                beltT[i] + up * 2.85f, beltT[j] + up * 2.85f,
                outerT[i] + up * 2.85f, outerT[j] + up * 2.85f,
                beltT[i] + up * WallTopY, beltT[j] + up * WallTopY,
                outerT[i] + up * WallTopY, outerT[j] + up * WallTopY);
        }

        BuildPart("Belt_Lower", parent, lower, lowerF, mats[2]);
        BuildPart("Belt_Upper", parent, upper, upperF, mats[2]);
    }

    private static void BuildPanels(Transform parent, Material[] mats)
    {
        var verts = new List<Vector3>();
        var faces = new List<Face>();

        float yc = 1.85f;
        float pz = 4.05f;
        float px = 5.06f;
        Vector3 panelXZ = new Vector3(0.9f, 1.7f, 0.06f);
        Vector3 panelZY = new Vector3(0.06f, 1.7f, 0.9f);

        AddBox(verts, faces, new Vector3(-2.4f, yc, pz), panelXZ);
        AddBox(verts, faces, new Vector3(2.4f, yc, pz), panelXZ);
        AddBox(verts, faces, new Vector3(-2.4f, yc, -pz), panelXZ);
        AddBox(verts, faces, new Vector3(2.4f, yc, -pz), panelXZ);
        AddBox(verts, faces, new Vector3(px, yc, -2.4f), panelZY);
        AddBox(verts, faces, new Vector3(px, yc, 2.4f), panelZY);
        AddBox(verts, faces, new Vector3(-px, yc, -2.4f), panelZY);
        AddBox(verts, faces, new Vector3(-px, yc, 2.4f), panelZY);

        BuildPart("Panels", parent, verts, faces, mats[4]);
    }

    private static void BuildRoof(Transform parent, Material[] mats)
    {
        var verts = new List<Vector3>();
        var faces = new List<Face>();
        var ring = ChamferRing(HalfW * LeanK + Overhang, HalfD * LeanK + Overhang, Chamfer);
        AddSlab(verts, faces, ring, WallTopY, ring, WallTopY + RoofThickness);
        BuildPart("Ship_Roof", parent, verts, faces, mats[3]);

        var rv = new List<Vector3>();
        var rf = new List<Face>();
        var rb = ChamferRing(2.4f, 1.8f, 0.8f);
        var rt = ChamferRing(2.4f * 1.06f, 1.8f * 1.06f, 0.8f);
        AddSlab(rv, rf, rb, WallTopY + RoofThickness, rt, WallTopY + RoofThickness + 0.6f);
        BuildPart("Roof_Detail", parent, rv, rf, mats[2]);
    }

    private static Material[] LoadOrCreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MaterialsFolder))
            AssetDatabase.CreateFolder("Assets/_Project/Materials", "AlienShip");

        return new[]
        {
            GetOrCreateMaterial("AlienShell_Floor", new Color(0.42f, 0.44f, 0.48f)),
            GetOrCreateMaterial("AlienShell_Wall", new Color(0.80f, 0.81f, 0.84f)),
            GetOrCreateMaterial("AlienShell_Base", new Color(0.30f, 0.32f, 0.36f)),
            GetOrCreateMaterial("AlienShell_Roof", new Color(0.87f, 0.88f, 0.90f)),
            GetOrCreateMaterial("AlienShell_Panel", new Color(0.55f, 0.53f, 0.68f))
        };
    }

    private static Material GetOrCreateMaterial(string name, Color color)
    {
        string path = MaterialsFolder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
            return mat;

        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader == null)
        {
            Debug.LogError("URP/Lit shader not found. Cannot create material " + name);
            return null;
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.35f);
        mat.SetFloat("_Metallic", 0f);
        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
