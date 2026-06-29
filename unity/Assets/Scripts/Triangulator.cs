using System;
using System.Collections.Generic;
using UnityEngine;

public static class Triangulator
{
    /// <summary>
    /// Triangulates a 2D polygon with optional holes.
    /// Returns an array of indices that form triangles in counter-clockwise order.
    /// </summary>
    /// <param name="outer">The outer boundary vertices of the polygon.</param>
    /// <param name="holes">List of hole boundary vertices.</param>
    /// <param name="outVertices">Output flat list of vertices of the combined polygon (used for mesh generation).</param>
    public static int[] Triangulate(List<Vector2> outer, List<List<Vector2>> holes, out List<Vector2> outVertices)
    {
        // 1. If there are no holes, just triangulate the outer polygon directly
        if (holes == null || holes.Count == 0)
        {
            outVertices = new List<Vector2>(outer);
            return TriangulateSimple(outVertices);
        }

        // 2. Merge holes into the outer polygon using bridge cuts
        outVertices = MergeHoles(outer, holes);

        // 3. Triangulate the merged simple polygon
        return TriangulateSimple(outVertices);
    }

    /// <summary>
    /// Merges holes into the outer polygon by creating dual bridge edges.
    /// Returns a single contiguous vertex list.
    /// </summary>
    private static List<Vector2> MergeHoles(List<Vector2> outer, List<List<Vector2>> holes)
    {
        List<Vector2> currentPoly = new List<Vector2>(outer);
        
        // Make copies of holes so we can sort/modify them
        List<List<Vector2>> remainingHoles = new List<List<Vector2>>();
        foreach (var hole in holes)
        {
            if (hole != null && hole.Count >= 3)
            {
                remainingHoles.Add(new List<Vector2>(hole));
            }
        }

        // Sort holes by their maximum X coordinate (rightmost vertex) descending.
        // Merging rightmost holes first is a standard heuristic to avoid bridge intersections.
        remainingHoles.Sort((h1, h2) =>
        {
            float maxX1 = float.MinValue;
            foreach (var v in h1) maxX1 = Mathf.Max(maxX1, v.x);
            float maxX2 = float.MinValue;
            foreach (var v in h2) maxX2 = Mathf.Max(maxX2, v.x);
            return maxX2.CompareTo(maxX1);
        });

        foreach (var hole in remainingHoles)
        {
            // Find rightmost vertex of the hole
            int holeIdx = 0;
            float maxHoleX = float.MinValue;
            for (int i = 0; i < hole.Count; i++)
            {
                if (hole[i].x > maxHoleX)
                {
                    maxHoleX = hole[i].x;
                    holeIdx = i;
                }
            }
            Vector2 holePt = hole[holeIdx];

            // Search for a visible vertex in currentPoly to bridge to
            int bridgePolyIdx = -1;
            float minDistance = float.MaxValue;

            for (int i = 0; i < currentPoly.Count; i++)
            {
                Vector2 polyPt = currentPoly[i];
                
                // Only consider vertices to the right of the hole point (or close enough)
                if (polyPt.x >= holePt.x)
                {
                    float dist = Vector2.Distance(holePt, polyPt);
                    if (dist < minDistance)
                    {
                        // Check if the bridge segment intersects any edge in currentPoly or the hole
                        if (!IsBridgeIntersecting(holePt, polyPt, currentPoly, hole))
                        {
                            minDistance = dist;
                            bridgePolyIdx = i;
                        }
                    }
                }
            }

            // Fallback: if no vertex to the right is visible, check all vertices
            if (bridgePolyIdx == -1)
            {
                minDistance = float.MaxValue;
                for (int i = 0; i < currentPoly.Count; i++)
                {
                    Vector2 polyPt = currentPoly[i];
                    float dist = Vector2.Distance(holePt, polyPt);
                    if (dist < minDistance)
                    {
                        if (!IsBridgeIntersecting(holePt, polyPt, currentPoly, hole))
                        {
                            minDistance = dist;
                            bridgePolyIdx = i;
                        }
                    }
                }
            }

            // Splicing the hole into the polygon
            if (bridgePolyIdx != -1)
            {
                List<Vector2> newPoly = new List<Vector2>();
                // Add outer polygon up to bridge point
                for (int i = 0; i <= bridgePolyIdx; i++)
                {
                    newPoly.Add(currentPoly[i]);
                }
                // Add hole vertices starting from holeIdx, looping around, and returning to holeIdx
                for (int i = 0; i <= hole.Count; i++)
                {
                    newPoly.Add(hole[(holeIdx + i) % hole.Count]);
                }
                // Add outer polygon from bridge point to the end
                for (int i = bridgePolyIdx; i < currentPoly.Count; i++)
                {
                    newPoly.Add(currentPoly[i]);
                }
                currentPoly = newPoly;
            }
        }

        return currentPoly;
    }

    private static bool IsBridgeIntersecting(Vector2 p1, Vector2 p2, List<Vector2> poly, List<Vector2> hole)
    {
        // Check intersection with outer polygon edges
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 a1 = poly[i];
            Vector2 a2 = poly[(i + 1) % poly.Count];
            // Skip edges sharing vertices with the bridge
            if (a1 == p1 || a1 == p2 || a2 == p1 || a2 == p2) continue;
            if (SegmentsIntersect(p1, p2, a1, a2)) return true;
        }

        // Check intersection with hole edges
        for (int i = 0; i < hole.Count; i++)
        {
            Vector2 a1 = hole[i];
            Vector2 a2 = hole[(i + 1) % hole.Count];
            if (a1 == p1 || a1 == p2 || a2 == p1 || a2 == p2) continue;
            if (SegmentsIntersect(p1, p2, a1, a2)) return true;
        }

        return false;
    }

    private static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float denominator = ((b.x - a.x) * (d.y - c.y)) - ((b.y - a.y) * (d.x - c.x));
        if (Mathf.Approximately(denominator, 0)) return false; // Parallel or collinear

        float numerator1 = ((a.y - c.y) * (d.x - c.x)) - ((a.x - c.x) * (d.y - c.y));
        float numerator2 = ((a.y - c.y) * (b.x - a.x)) - ((a.x - c.x) * (b.y - a.y));

        float r = numerator1 / denominator;
        float s = numerator2 / denominator;

        return (r >= 0 && r <= 1) && (s >= 0 && s <= 1);
    }

    /// <summary>
    /// Triangulates a simple polygon (no holes) using Ear Clipping.
    /// </summary>
    private static int[] TriangulateSimple(List<Vector2> vertices)
    {
        int n = vertices.Count;
        if (n < 3) return new int[0];

        List<int> indices = new List<int>();
        for (int i = 0; i < n; i++) indices.Add(i);

        List<int> triangles = new List<int>();

        // Ensure the vertices are in counter-clockwise order
        if (Area(vertices) < 0)
        {
            indices.Reverse();
        }

        int count = n;
        int threshold = 2 * count; // Prevent infinite loops on self-intersecting geometry

        while (count > 2 && threshold > 0)
        {
            threshold--;
            bool earFound = false;

            for (int i = 0; i < count; i++)
            {
                int prev = indices[(i - 1 + count) % count];
                int curr = indices[i];
                int next = indices[(i + 1) % count];

                if (IsEar(prev, curr, next, vertices, indices))
                {
                    // Add triangle (prev, curr, next)
                    triangles.Add(prev);
                    triangles.Add(curr);
                    triangles.Add(next);

                    indices.RemoveAt(i);
                    count--;
                    earFound = true;
                    break;
                }
            }

            if (!earFound)
            {
                // If we get stuck, force-clip the first convex vertex we find to avoid hanging
                for (int i = 0; i < count; i++)
                {
                    int prev = indices[(i - 1 + count) % count];
                    int curr = indices[i];
                    int next = indices[(i + 1) % count];

                    if (IsConvex(vertices[prev], vertices[curr], vertices[next]))
                    {
                        triangles.Add(prev);
                        triangles.Add(curr);
                        triangles.Add(next);
                        indices.RemoveAt(i);
                        count--;
                        earFound = true;
                        break;
                    }
                }
                
                // If still stuck, break
                if (!earFound) break;
            }
        }

        return triangles.ToArray();
    }

    private static bool IsEar(int prevIdx, int currIdx, int nextIdx, List<Vector2> vertices, List<int> indices)
    {
        Vector2 a = vertices[prevIdx];
        Vector2 b = vertices[currIdx];
        Vector2 c = vertices[nextIdx];

        if (!IsConvex(a, b, c)) return false;

        // Check if any other vertex lies inside triangle abc
        for (int i = 0; i < indices.Count; i++)
        {
            int idx = indices[i];
            if (idx == prevIdx || idx == currIdx || idx == nextIdx) continue;
            
            Vector2 p = vertices[idx];
            if (PointInTriangle(p, a, b, c)) return false;
        }

        return true;
    }

    private static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        // Cross product of ab and bc: (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x)
        return ((b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x)) > 0;
    }

    private static float Area(List<Vector2> vertices)
    {
        float area = 0;
        int n = vertices.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 v1 = vertices[i];
            Vector2 v2 = vertices[(i + 1) % n];
            area += (v1.x * v2.y) - (v2.x * v1.y);
        }
        return area * 0.5f;
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool has_neg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool has_pos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(has_neg && has_pos);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }
}
