using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json; // Newtonsoft.Json is built-in or easily added in Unity 2020+

public class FloorplanExtruder : MonoBehaviour
{
    [Header("Extrusion Dimensions (in Meters)")]
    public float targetPlanSize = 10.0f; // Scale plan so longest axis matches this size
    public float wallHeight = 2.7f;
    public float doorHeight = 2.0f;
    public float windowSill = 0.8f;
    public float windowTop = 2.2f;

    [Header("Materials")]
    public Material wallMaterial;
    public Material doorMaterial;
    public Material windowMaterial;

    // JSON parsing structure
    [Serializable]
    public class PolygonData
    {
        public List<List<float>> outer;
        public List<List<List<float>>> holes;
    }

    [Serializable]
    public class PolygonsContainer
    {
        public List<PolygonData> wall;
        public List<PolygonData> door;
        public List<PolygonData> window;
    }

    [Serializable]
    public class FloorplanResult
    {
        public List<int> canvas_size;
        public List<int> content_rect;
        public PolygonsContainer polygons;
    }

    /// <summary>
    /// Parses the JSON result from the FastAPI server and spawns the 3D model.
    /// Returns the root GameObject of the spawned model.
    /// </summary>
    public GameObject Extrude(string jsonText)
    {
        FloorplanResult data;
        try
        {
            data = JsonConvert.DeserializeObject<FloorplanResult>(jsonText);
        }
        catch (Exception e)
        {
            Debug.LogError("[FloorplanExtruder] JSON Parsing failed: " + e.Message);
            return null;
        }

        if (data == null || data.polygons == null)
        {
            Debug.LogError("[FloorplanExtruder] Deserialized data is empty or invalid.");
            return null;
        }

        // Create a root object for the plan
        GameObject planRoot = new GameObject("ExtrudedFloorplan");
        planRoot.transform.SetParent(this.transform, false);

        // Compute layout dimensions and scale
        int left = data.content_rect[0];
        int top = data.content_rect[1];
        int innerW = data.content_rect[2];
        int innerH = data.content_rect[3];

        float cx = left + (innerW / 2.0f);
        float cy = top + (innerH / 2.0f);
        float scale = targetPlanSize / Mathf.Max(innerW, innerH);

        // Process Walls
        if (data.polygons.wall != null)
        {
            int i = 0;
            foreach (var w in data.polygons.wall)
            {
                SpawnMesh(w, 0.0f, wallHeight, "Wall_" + (i++), wallMaterial, planRoot.transform, scale, cx, cy);
            }
        }

        // Process Doors
        if (data.polygons.door != null)
        {
            int i = 0;
            foreach (var d in data.polygons.door)
            {
                SpawnMesh(d, 0.0f, doorHeight, "Door_" + (i++), doorMaterial, planRoot.transform, scale, cx, cy);
            }
        }

        // Process Windows
        if (data.polygons.window != null)
        {
            int i = 0;
            foreach (var w in data.polygons.window)
            {
                SpawnMesh(w, windowSill, windowTop, "Window_" + (i++), windowMaterial, planRoot.transform, scale, cx, cy);
            }
        }

        Debug.Log("[FloorplanExtruder] Extrusion completed successfully.");
        return planRoot;
    }

    private void SpawnMesh(PolygonData poly, float bottomY, float topY, string name, Material mat, Transform parent, float scale, float cx, float cy)
    {
        if (poly.outer == null || poly.outer.Count < 3) return;

        // Convert outer polygon points
        List<Vector2> outerPoints = ConvertPoints(poly.outer, scale, cx, cy);

        // Convert holes
        List<List<Vector2>> holePoints = new List<List<Vector2>>();
        if (poly.holes != null)
        {
            foreach (var h in poly.holes)
            {
                if (h != null && h.Count >= 3)
                {
                    holePoints.Add(ConvertPoints(h, scale, cx, cy));
                }
            }
        }

        // Build vertices and triangles for procedural mesh
        List<Vector3> meshVertices = new List<Vector3>();
        List<int> meshTriangles = new List<int>();

        // 1. Build vertical side faces
        BuildSides(outerPoints, bottomY, topY, meshVertices, meshTriangles, false); // outer wall faces
        foreach (var hole in holePoints)
        {
            BuildSides(hole, bottomY, topY, meshVertices, meshTriangles, true); // hole cuts (inner wall faces, opposite winding)
        }

        // 2. Build top and bottom caps using Triangulator
        List<Vector2> combined2DPoints;
        int[] capTriangles = Triangulator.Triangulate(outerPoints, holePoints, out combined2DPoints);

        if (capTriangles.Length > 0)
        {
            // Top Cap
            int topCapVStart = meshVertices.Count;
            foreach (var pt in combined2DPoints)
            {
                meshVertices.Add(new Vector3(pt.x, topY, pt.y));
            }
            foreach (int triIdx in capTriangles)
            {
                meshTriangles.Add(topCapVStart + triIdx);
            }

            // Bottom Cap (facing down, reverse winding order)
            int botCapVStart = meshVertices.Count;
            foreach (var pt in combined2DPoints)
            {
                meshVertices.Add(new Vector3(pt.x, bottomY, pt.y));
            }
            for (int t = capTriangles.Length - 1; t >= 0; t--)
            {
                meshTriangles.Add(botCapVStart + capTriangles[t]);
            }
        }

        // Create GameObject and Mesh
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        
        // If no material is assigned, create a default diffuse one
        if (mat != null)
        {
            mr.material = mat;
        }
        else
        {
            mr.material = new Material(Shader.Find("Standard"));
            mr.material.color = name.StartsWith("Wall") ? Color.white : (name.StartsWith("Door") ? new Color(0.6f, 0.4f, 0.2f) : new Color(0.6f, 0.8f, 1.0f, 0.5f));
        }

        Mesh mesh = new Mesh();
        mesh.vertices = meshVertices.ToArray();
        mesh.triangles = meshTriangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        mf.mesh = mesh;

        // Add a MeshCollider for interaction/selection support
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private List<Vector2> ConvertPoints(List<List<float>> srcPoints, float scale, float cx, float cy)
    {
        List<Vector2> dstPoints = new List<Vector2>();
        foreach (var pt in srcPoints)
        {
            if (pt.Count >= 2)
            {
                // Translate so center of layout content is (0,0) and scale to Unity meters
                float ux = (pt[0] - cx) * scale;
                float uz = -(pt[1] - cy) * scale; // Map pixel Y-down to Unity Z-forward (negative pixel Y = positive Z)
                dstPoints.Add(new Vector2(ux, uz));
            }
        }
        return dstPoints;
    }

    private void BuildSides(List<Vector2> loop, float bottomY, float topY, List<Vector3> vertices, List<int> triangles, bool reverseWinding)
    {
        int count = loop.Count;
        for (int i = 0; i < count; i++)
        {
            Vector2 a = loop[i];
            Vector2 b = loop[(i + 1) % count];

            int vIdx = vertices.Count;
            // Map 2D coordinate Vector2(x, y) to 3D Vector3(x, height, z) where z = Vector2.y
            vertices.Add(new Vector3(a.x, bottomY, a.y));
            vertices.Add(new Vector3(a.x, topY, a.y));
            vertices.Add(new Vector3(b.x, topY, b.y));
            vertices.Add(new Vector3(b.x, bottomY, b.y));

            if (reverseWinding)
            {
                triangles.Add(vIdx);
                triangles.Add(vIdx + 2);
                triangles.Add(vIdx + 1);

                triangles.Add(vIdx);
                triangles.Add(vIdx + 3);
                triangles.Add(vIdx + 2);
            }
            else
            {
                triangles.Add(vIdx);
                triangles.Add(vIdx + 1);
                triangles.Add(vIdx + 2);

                triangles.Add(vIdx);
                triangles.Add(vIdx + 2);
                triangles.Add(vIdx + 3);
            }
        }
    }
}
