using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;



public class GlobalVariables 
{
    public static List<Vector2Int> oddRowNeighbours = new List<Vector2Int> { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 1), new Vector2Int(-1, 0), new Vector2Int(-1, -1), new Vector2Int(0, -1)};
    public static List<Vector2Int> evenRowNeighbours = new List<Vector2Int> { new Vector2Int(1, 0), new Vector2Int(1, 1), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(0, -1), new Vector2Int(1, -1) };
}

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class HexTile : MonoBehaviour
{
    GameManager manager;

    public Material[] highlightedList;
    public Material[] materialList;
    public MeshRenderer renderer;
    public GameObject envObj;
    public bool enemyCamp = false;

    public bool highlighted = false;
    public Vector2Int gridNum;
    public HexNode node;

    public Dictionary<int, Vector2Int> neighbourIndexCoordinateDict;
    public Island island = null;
    public bool dockTile = false;

    public float centerHeight;
    public Dictionary<int, float> edgeHeights;
    public TileType terrain;
    public float height;


    public void SpawnTile(GameManager manager, Vector2Int coordinate, Dictionary<Vector2Int, List<Vector2Int>> neighbourInfo) 
    {
        this.manager = manager;
        this.gridNum = coordinate;
        node = new HexNode();
        node = node.CreateNode(this.transform.position.x, this.transform.position.z, this.gridNum, this.terrain);
        this.neighbourIndexCoordinateDict = new Dictionary<int, Vector2Int>();

        for (int i = 0; i < neighbourInfo[gridNum].Count; i++) 
        {
            neighbourIndexCoordinateDict.Add(i, neighbourInfo[gridNum][i]);
        }

        renderer = gameObject.GetComponent<MeshRenderer>();
        
        this.edgeHeights = new Dictionary<int, float>();
        this.centerHeight = -1;
    }

    public HexTile RandomLandNeighbour() 
    {
        foreach (Vector2Int nCoord in neighbourIndexCoordinateDict.Values) 
        {
            HexTile neighbour = manager.grid.tiles[nCoord];
            if (!neighbour.node.occupied && neighbour.terrain != TileType.Water) 
            {
                return neighbour;
            }
        }
        return null;
    }

    private void OnMouseEnter()
    {
        manager.HighlightTile(this);
    }

    private void OnMouseExit()
    {
        manager.UnhighlightTile(this);
    }

    public Mesh CreateMesh(float tileOutterSize, float tileInnerSize, float tileInitialHeight)
    {
        List<Face> hexFaces = GenerateFaces(tileInnerSize, tileOutterSize, tileInitialHeight);
        return CombineFaces(hexFaces);
    }
    private List<Face> GenerateFaces(float tileOutterSize, float tileInnerSize, float tileInitialHeight)
    {
        // Create a new empty list of faces (entire hexagon)
        List<Face> faces = new List<Face>();
        
        // Surface faces
        for (int point = 0; point < 6; point++)
        {
            Face topFace = CreateFace(tileInnerSize, tileOutterSize, tileInitialHeight / 2f, tileInitialHeight / 2f, point, true);
            topFace.Triangles = new List<int> { 0, 1, 2 };
            topFace.Vertices.RemoveAt(0);
            topFace.UVS.RemoveAt(2);
            // Calculate the UVs using the UvCalculator class
            float scale = 85f; // Adjust the scale value as needed
            topFace.UVS = UvCalculator.CalculateUVs(topFace, scale).ToList();
            topFace = topFace.SubdivideFaceNTimes(topFace,0);
            faces.Add(topFace);
        }
        

        // Draw bottom face
        for (int point = 0; point < 6; point++)
        {
            faces.Add(CreateFace(tileInnerSize, tileOutterSize, -tileInitialHeight / 2f, -tileInitialHeight / 2f, point, false));
        }

        // Draw inner faces
        for (int point = 0; point < 6; point++)
        {
            faces.Add(CreateFace(tileInnerSize, tileInnerSize, tileInitialHeight / 2f, -tileInitialHeight / 2f, point, true));
        }

        return faces;

    }

    private Face CreateFace(float innerRad, float outerRad, float heightA, float heightB, int point, bool reverse)
    {
        Vector3 pointA = GetPoint(innerRad, heightB, point);
        Vector3 pointB = GetPoint(innerRad, heightB, (point < 5) ? point + 1 : 0);
        Vector3 pointC = GetPoint(outerRad, heightA, (point < 5) ? point + 1 : 0);
        Vector3 pointD = GetPoint(outerRad, heightA, point);

       
        List<Vector3> vertices = new List<Vector3>() { pointA, pointB, pointC, pointD };
        List<int> triangles = new List<int>() { 0, 1, 2, 2, 3, 0 };
        List<Vector2> uvs = new List<Vector2>() { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };

        if (reverse)
        {
            vertices.Reverse();
        }

        return new Face(vertices, triangles, uvs);
    }

    protected Vector3 GetPoint(float size, float height, int index)
    {
        float angle_deg = 60 * index - 30;
        float angle_rad = Mathf.PI / 180f * angle_deg;

        float x = (float)Math.Round((size * Mathf.Cos(angle_rad)), 3);
        float y = (float)Math.Round(height, 3);
        float z = (float)Math.Round((size * Mathf.Sin(angle_rad)), 3);

        return new Vector3(x,y,z);
    }

    private Mesh CombineFaces(List<Face> faces)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();


        for (int i = 0; i < faces.Count; i++)
        {
            // Add the vertices
            int vertexOffset = vertices.Count;
            vertices.AddRange(faces[i].Vertices);
            uvs.AddRange(faces[i].UVS);

            // Offset the triangles
            foreach (int triangle in faces[i].Triangles)
            {
                tris.Add(triangle + vertexOffset);

            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "HexagonMesh";

        mesh.vertices = vertices.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();

        return mesh;
    }
}

public struct Face
{
    public List<Vector3> Vertices { get; set; }
    public List<int> Triangles { get; set; }
    public List<Vector2> UVS { get; set; }
    public HashSet<Vector3> EdgeVectors { get; set; }

    public Face(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs)
    {
        this.Vertices = vertices;
        this.Triangles = triangles;
        this.UVS = uvs;
        this.EdgeVectors = new HashSet<Vector3>();
    }

    public Face SubdivideFace(Face face)
    {
        HashSet<Vector3> edgeVertices = new HashSet<Vector3>();
        List<Vector3> newVertices = new List<Vector3>(6);
        List<int> newTriangles = new List<int>(12);
        List<Vector2> newUVs = new List<Vector2>(6);

        Vector3 AB_mid = (face.Vertices[0] + face.Vertices[1]) / 2f;
        Vector3 BC_mid = (face.Vertices[1] + face.Vertices[2]) / 2f;
        Vector3 CA_mid = (face.Vertices[2] + face.Vertices[0]) / 2f;

        bool edgeA = face.EdgeVectors.Contains(face.Vertices[0]);
        bool edgeB = face.EdgeVectors.Contains(face.Vertices[1]);
        bool edgeC = face.EdgeVectors.Contains(face.Vertices[2]);

        if (edgeA && edgeB) 
        {
            edgeVertices.Add(face.Vertices[0]);
            edgeVertices.Add(AB_mid);
            edgeVertices.Add(face.Vertices[1]);
        }
        if (edgeB && edgeC) 
        {
            edgeVertices.Add(face.Vertices[1]);
            edgeVertices.Add(BC_mid);
            edgeVertices.Add(face.Vertices[2]);
        }
        if (edgeC && edgeA) 
        {
            edgeVertices.Add(face.Vertices[2]);
            edgeVertices.Add(CA_mid);
            edgeVertices.Add(face.Vertices[0]);
        }

        newVertices.AddRange(new[] { face.Vertices[0], face.Vertices[1], face.Vertices[2], AB_mid, BC_mid, CA_mid });

        newTriangles.AddRange(new[] { 0, 3, 5, 3, 1, 4, 5, 4, 2, 3, 4, 5 });

        Vector2 uvAB_mid = (face.UVS[0] + face.UVS[1]) / 2f;
        Vector2 uvBC_mid = (face.UVS[1] + face.UVS[2]) / 2f;
        Vector2 uvCA_mid = (face.UVS[2] + face.UVS[0]) / 2f;

        newUVs.AddRange(new[] { face.UVS[0], face.UVS[1], face.UVS[2], uvAB_mid, uvBC_mid, uvCA_mid });

        return new Face(newVertices, newTriangles, newUVs);
    }

    public Face SubdivideFaceNTimes(Face face, int n)
    {
        if (n == 0)
        {
            return face;
        }

        Face subdividedFace = SubdivideFace(face);

        List<Vector3> newVertices = new List<Vector3>();
        List<int> newTriangles = new List<int>();
        List<Vector2> newUVs = new List<Vector2>();


        for (int i = 0; i < 4; i++)
        {
            List<Vector3> vertices = new List<Vector3>(3);
            List<int> triangles = new List<int> { 0, 1, 2 };
            List<Vector2> uvs = new List<Vector2>(3);

            for (int j = 0; j < 3; j++)
            {
                int index = subdividedFace.Triangles[i * 3 + j];
                vertices.Add(subdividedFace.Vertices[index]);
                uvs.Add(subdividedFace.UVS[index]);
            }

            Face subFace = SubdivideFaceNTimes(new Face(vertices, triangles, uvs), n - 1);

            // Merge vertices and update triangles
            Dictionary<int, int> indexMap = new Dictionary<int, int>();
            for (int j = 0; j < subFace.Vertices.Count; j++)
            {
                int existingIndex = newVertices.FindIndex(v => v == subFace.Vertices[j]);
                if (existingIndex >= 0)
                {
                    indexMap[j] = existingIndex;
                }
                else
                {
                    newVertices.Add(subFace.Vertices[j]);
                    newUVs.Add(subFace.UVS[j]);
                    indexMap[j] = newVertices.Count - 1;
                }
            }

            subFace.Triangles = subFace.Triangles.Select(t => indexMap[t]).ToList();
            newTriangles.AddRange(subFace.Triangles);
        }

        return new Face(newVertices, newTriangles, newUVs);
    }
}

public class UvCalculator
{
    private enum Facing { Up, Forward, Right };

    public static Vector2[] CalculateUVs(Face face, float scale)
    {
        Vector3[] v = face.Vertices.ToArray();
        var uvs = new Vector2[v.Length];

        for (int i = 0; i < uvs.Length; i += 3)
        {
            int i0 = i;
            int i1 = i + 1;
            int i2 = i + 2;

            Vector3 v0 = v[i0];
            Vector3 v1 = v[i1];
            Vector3 v2 = v[i2];

            Vector3 side1 = v1 - v0;
            Vector3 side2 = v2 - v0;
            var direction = Vector3.Cross(side1, side2);
            var facing = FacingDirection(direction);
            switch (facing)
            {
                case Facing.Forward:
                    uvs[i0] = ScaledUV(v0.x, v0.y, scale);
                    uvs[i1] = ScaledUV(v1.x, v1.y, scale);
                    uvs[i2] = ScaledUV(v2.x, v2.y, scale);
                    break;
                case Facing.Up:
                    uvs[i0] = ScaledUV(v0.x, v0.z, scale);
                    uvs[i1] = ScaledUV(v1.x, v1.z, scale);
                    uvs[i2] = ScaledUV(v2.x, v2.z, scale);
                    break;
                case Facing.Right:
                    uvs[i0] = ScaledUV(v0.y, v0.z, scale);
                    uvs[i1] = ScaledUV(v1.y, v1.z, scale);
                    uvs[i2] = ScaledUV(v2.y, v2.z, scale);
                    break;
            }
        }
        return uvs;
    }



    private static bool FacesThisWay(Vector3 v, Vector3 dir, Facing p, ref float maxDot, ref Facing ret)
    {
        float t = Vector3.Dot(v, dir);
        if (t > maxDot)
        {
            ret = p;
            maxDot = t;
            return true;
        }
        return false;
    }

    private static Facing FacingDirection(Vector3 v)
    {
        var ret = Facing.Up;
        float maxDot = Mathf.NegativeInfinity;

        if (!FacesThisWay(v, Vector3.right, Facing.Right, ref maxDot, ref ret))
            FacesThisWay(v, Vector3.left, Facing.Right, ref maxDot, ref ret);

        if (!FacesThisWay(v, Vector3.forward, Facing.Forward, ref maxDot, ref ret))
            FacesThisWay(v, Vector3.back, Facing.Forward, ref maxDot, ref ret);

        if (!FacesThisWay(v, Vector3.up, Facing.Up, ref maxDot, ref ret))
            FacesThisWay(v, Vector3.down, Facing.Up, ref maxDot, ref ret);

        return ret;
    }

    private static Vector2 ScaledUV(float uv1, float uv2, float scale)
    {
        return new Vector2(uv1 / scale, uv2 / scale);
    }
}