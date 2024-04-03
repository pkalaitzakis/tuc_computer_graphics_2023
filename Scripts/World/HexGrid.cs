using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SocialPlatforms;

public enum TileType
{
    Grass,
    Ground,
    Rock,
    Water,
    Sand
}

public class HexGrid : MonoBehaviour
{
    public HexGrid instance;

    [Header("Grid Dimensions & Tile Size Settings")]
    public Vector2Int gridDimension = new Vector2Int(50,50);
    public static float tileOuterSize = 120f;
    public static float tileInnerSize = 0f;
    public static float tileInitialHeight = 5f;
    public static float maxWorldHeight = 120f;
    public static float perlinHeightScaleFactor = 100f;

    [Header("Map Settings")]
    public float scale = 20f;
    public int octaves = 6;
    public float persistence = 0.5f;
    public float lacunarity = 2f;

    public NavMeshSurface navMesh;

    // Helper variables that help with random object placement
    public int xoffset = Mathf.RoundToInt((Mathf.Sqrt(3) / 2) * tileOuterSize);
    public int zoffset = Mathf.RoundToInt(tileOuterSize / 2);

    protected List<Vector3> edges;

    // Height map used for applying random 3d noise on the surface vertices of each HexagonMesh
    public float[,] heightMap;
    // Terrain map used for random terrain placement
    public int[,] terrainMap;

    // List of island class objects
    public List<Island> islands;
    public List<RockCluster> clusters;
    public List<HexTile> seaTiles;

    // Dictionary where each key is a tile coordinate and each value is a list of neighbouring tile coordinates
    public Dictionary<Vector2Int, List<Vector2Int>> neighbourInfo;

    // Dictionary where each key is a tile coordinate and each value its corresponding HexTile Component counterpart (attached to each Tile Game Object)
    public Dictionary<Vector2Int, HexTile> tiles;

    // Dictionary where each key is a Vector3 point on top of the surface of a prototype (unmodified) HexagonMesh
    // and each value is a list of integers denoting the Vector's triangle indices inside the Mesh prototype
    public Dictionary<Vector3, List<int>> surfaceVertices;

    /* PREFABS */

    // Tiles
    public GameObject waterTile;
    public GameObject grassTile;
    public GameObject sandTile;
    public GameObject groundTile;
    public GameObject rockTile;
 
    // Environment prefabs (polypirates)
    public List<GameObject> grassPrefabs = new List<GameObject>();
    public List<GameObject> sandPrefabs = new List<GameObject>();
    public List<GameObject> groundPrefabs = new List<GameObject>();
    public List<GameObject> rockPrefabs = new List<GameObject>();

     public HexGrid SpawnWorld(GameManager manager)
    {
        instance = this;

        neighbourInfo = new Dictionary<Vector2Int, List<Vector2Int>>();
        tiles = new Dictionary<Vector2Int, HexTile>();


        // Load Resources
        LoadTilePrefabs();
        LoadEnvironmentPrefabs();

        // Generate random tile board with perlin noise
        GenerateRandomBoard();
        // Find the islands 
        FindIslands();
        
        // Instatiate prefabs & configure their hextile component
        LayoutBoard(manager);

        List<HexTile> rockTiles = SetupNavigation();
        
        FindRockClusters(rockTiles);
        
        return instance;
    }

    public List<HexTile> SetupNavigation() 
    {
        navMesh = gameObject.AddComponent<NavMeshSurface>();
        navMesh.layerMask = LayerMask.GetMask("Sand", "Grass", "Ground", "Rock", "Environment");
        navMesh.BuildNavMesh();
        // cache neighbours of every hex node
        ConnectSailingNodes();
        return ConnectRockNodes();
    }

    public HexTile randomWaterTile() 
    {
        return seaTiles[UnityEngine.Random.Range(0, seaTiles.Count)];
    }

    private void GenerateRandomBoard()
    {
        // Create a new heightMap float array
        heightMap = new float[gridDimension.x, gridDimension.y];
        // Create a new terraintMap int array
        terrainMap = new int[gridDimension.x, gridDimension.y];

        // Randomize seed & perlin offset variable
        System.Random rng = new System.Random();
        Vector2 offset = new Vector2(rng.Next(-100000, 100000), rng.Next(-100000, 100000));

        // Start filling the arrays
        for (int y = 0; y < gridDimension.y; y++)
        {
            for (int x = 0; x < gridDimension.x; x++)
            {
                // Normalized perlin value calculation
                float nx = (x + offset.x) / scale;
                float ny = (y + offset.y) / scale;
                float elevation = Mathf.PerlinNoise(nx * lacunarity, ny * lacunarity) * (1 + persistence);
                elevation = Mathf.Pow(elevation, octaves);

                // Store the scaled value (actual 3D height) in the height map
                heightMap[x, y] = perlinHeightScaleFactor * elevation;
                if (heightMap[x, y] > maxWorldHeight) 
                {
                    heightMap[x, y] = maxWorldHeight;
                }

                // Store the terrain value in the terrain map
                if (elevation < 0.15f)
                {
                    terrainMap[x, y] = (int)TileType.Water;
                }
                else if (elevation < 0.25f)
                {
                    terrainMap[x, y] = (int)TileType.Sand;
                }
                else if (elevation < 0.4f)
                {
                    terrainMap[x, y] = (int)TileType.Grass;
                }
                else if (elevation < 0.75f)
                {
                    terrainMap[x, y] = (int)TileType.Ground;
                }
                else
                {
                    terrainMap[x, y] = (int)TileType.Rock;
                }

                CalcNeighbouringTiles(x, y);
            }
        }
    }

    // Adds a new Vector2Int entry in the neighbourInfo dictionary, with a list of neighbouring tile coordinates
    public void CalcNeighbouringTiles(int x, int y)
    {
        Vector2Int tile = new Vector2Int(x, y);
        neighbourInfo.Add(tile, new List<Vector2Int>());
        List<Vector2Int> possibleNeighbours = tile.y % 2 == 0
            ? GlobalVariables.evenRowNeighbours
            : GlobalVariables.oddRowNeighbours;
        for (int i = 0; i < possibleNeighbours.Count; i++)
        {
            Vector2Int neighbourNum = tile + possibleNeighbours[i];
            if (neighbourNum.x >= 0 && neighbourNum.x < gridDimension.x && neighbourNum.y >= 0 && neighbourNum.y < gridDimension.y)
            {
                neighbourInfo[tile].Add(neighbourNum);
            }
        }
    }

    private void LayoutBoard(GameManager manager)
    {
        // Keep track of visited tiles
        bool[,] visited = new bool[gridDimension.x,gridDimension.y];
        SpawnIslands(visited, manager);
        SpawnOcean(visited, manager);
    }

    void SpawnIslands(bool[,] visited, GameManager manager)
    {
        GameObject tileObject;
        for (int i = 0; i < islands.Count; i++)
        {
            Island currentIsland = islands[i];

            foreach (Vector2Int tileCoord in currentIsland.tileCoods)
            {
                visited[tileCoord.x, tileCoord.y] = true;
                Vector3 tileWorldPos = GetPositionForHexFromCoordinate(tileCoord);

                if (terrainMap[tileCoord.x, tileCoord.y] == (int)TileType.Rock)
                {
                    tileObject = Instantiate(rockTile, tileWorldPos, Quaternion.identity, currentIsland.gameObject.transform);
                }
                else if (terrainMap[tileCoord.x, tileCoord.y] == (int)TileType.Ground)
                {
                    tileObject = Instantiate(groundTile, tileWorldPos, Quaternion.identity, currentIsland.gameObject.transform);
                }
                else if (terrainMap[tileCoord.x, tileCoord.y] == (int)TileType.Grass)
                {
                    tileObject = Instantiate(grassTile, tileWorldPos, Quaternion.identity, currentIsland.gameObject.transform);
                }
                else
                {
                    tileObject = Instantiate(sandTile, tileWorldPos, Quaternion.identity, currentIsland.gameObject.transform);
                }

                tileObject.name = $"Tile ({tileCoord.x},{tileCoord.y})";
                HexTile hexTile = tileObject.GetComponent<HexTile>();
                hexTile.SpawnTile(manager, tileCoord, neighbourInfo);
                ModifyTileMesh(tileObject);
                hexTile.node.setWorldPoint(hexTile.centerHeight);
                hexTile.island = currentIsland;
                tiles.Add(hexTile.gridNum, hexTile);
                currentIsland.AddIslandTile(hexTile);
                AddEnvironementToTile(tileObject);
            }
        }
    }

    void SpawnOcean(bool[,] visited, GameManager manager) 
    {
        GameObject openSea = new GameObject("Ocean");
        openSea.transform.SetParent(transform);
        seaTiles = new List<HexTile>();

        GameObject tileObject;
        for (int y = 0; y < gridDimension.y; y++)
        {
            for (int x = 0; x < gridDimension.x; x++)
            {
                if (!visited[x, y])
                {
                    Vector2Int gridCoordinate = new Vector2Int(x, y);
                    Vector3 tileWorldPos = GetPositionForHexFromCoordinate(gridCoordinate);
                    tileObject = Instantiate(waterTile, tileWorldPos, Quaternion.identity, openSea.transform);
                    tileObject.name = $"Tile ({x},{y})";
                    HexTile hexTile = tileObject.GetComponent<HexTile>();
                    hexTile.SpawnTile(manager, gridCoordinate, neighbourInfo);
                    ModifyTileMesh(tileObject);
                    hexTile.node.setWorldPoint(hexTile.centerHeight);
                    tiles.Add(hexTile.gridNum, hexTile);
                    seaTiles.Add(hexTile);
                }
            }
        }
    }

    void FindRockClusters(List<HexTile> rockTiles) 
    {
        bool[,] visited = new bool[gridDimension.x, gridDimension.y];
        clusters = new List<RockCluster>();

        foreach (HexTile tile in rockTiles) 
        {
            if (!visited[tile.gridNum.x, tile.gridNum.y])
            {
                Queue<HexTile> rockQueue = new Queue<HexTile>();
                RockCluster targetCluster;
                targetCluster = tile.island.AddTileToRockCluster(null, tile);
                rockQueue.Enqueue(tile);

                while (rockQueue.Count > 0)
                {
                    HexTile rockTile = rockQueue.Dequeue();
                    visited[rockTile.gridNum.x, rockTile.gridNum.y] = true;

                    foreach (HexNode node in rockTile.node.neighbours)
                    {
                        if (!visited[node.gridNum.x, node.gridNum.y])
                        {
                            tile.island.AddTileToRockCluster(targetCluster, tiles[node.gridNum]);
                            rockQueue.Enqueue(tiles[node.gridNum]);
                        }
                    }
                }
                clusters.Add(targetCluster);
            }
        }
        clusters.Sort((cluster1, cluster2) => cluster1.rockTiles.Count.CompareTo(cluster2.rockTiles.Count));
    }

    void FindIslands()
    {
        // Helper bool array to mark which values have been visited
        bool[,] visited = new bool[gridDimension.x, gridDimension.y];
        islands = new List<Island>();
        for (int y = 0; y < gridDimension.y; y++) 
        {
            for(int x = 0; x < gridDimension.x; x++) 
            {
                if (terrainMap[x, y] != (int)TileType.Water && !visited[x, y]) 
                {
                    GameObject islandObject = new GameObject($"Island {islands.Count}");
                    islandObject.transform.SetParent(transform);
                    Island islandComponent = islandObject.AddComponent<Island>();
                    FloodFill(x, y, islandComponent, visited);
                    islands.Add(islandComponent);
                }
            }
        }

        ArrangeIslandsAscendingOrder();
    }

    void FloodFill(int x, int y, Island island, bool[,] visited)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(x, y));

        while (queue.Count > 0)
        {
            Vector2Int coord = queue.Dequeue();

            if (visited[coord.x, coord.y]) continue; // Already visited

            visited[coord.x, coord.y] = true;
            //tile.island = island;
            island.AddTileCoord(coord); // Add this tile to the island

            List<Vector2Int> neighbourCoords = neighbourInfo[coord];

            // Enqueue all neighboring non water tiles
            foreach (Vector2Int nCoord in neighbourCoords)
            {
                if (!visited[nCoord.x, nCoord.y] && terrainMap[nCoord.x, nCoord.y] != (int)TileType.Water)
                {
                    queue.Enqueue(nCoord);
                }
            }
        }
    }

    void ArrangeIslandsAscendingOrder() 
    {
        islands.Sort((isle1, isle2) => isle1.tileCoods.Count.CompareTo(isle2.tileCoods.Count));

        for (int i = 0; i < islands.Count; i++)
        {
            islands[i].gameObject.name = $"Island {i}";
        }
    }

    public void LoadTilePrefabs()
    {
        waterTile = Resources.Load<GameObject>("Prefabs/Tiles/WaterTile");
        sandTile = Resources.Load<GameObject>("Prefabs/Tiles/SandTile");
        grassTile = Resources.Load<GameObject>("Prefabs/Tiles/GrassTile");
        groundTile = Resources.Load<GameObject>("Prefabs/Tiles/GroundTile");
        rockTile = Resources.Load<GameObject>("Prefabs/Tiles/RockTile");

        Mesh mesh = waterTile.GetComponent<MeshFilter>().sharedMesh;
        if (mesh== null ) 
        {
            // Check the bounds size of the mesh saved in the water tile prefab
            // if zero, we need to create a new mesh
            HexTile tile = waterTile.GetComponent<HexTile>();

            // create the mesh once
            mesh = tile.CreateMesh(tileOuterSize, tileInnerSize, tileInitialHeight);
            /*
#if UNITY_EDITOR
            SaveMeshAsset(mesh);
#endif
            */
            // update the rest prefabs with the newly generated mesh
            List<GameObject> tilePrefabs = new List<GameObject> { waterTile, sandTile, grassTile, groundTile, rockTile };

            foreach (GameObject prefab in tilePrefabs)
            {
                MeshFilter meshFilter = prefab.GetComponent<MeshFilter>();
                MeshCollider meshCollider = prefab.GetComponent<MeshCollider>();

                meshFilter.sharedMesh = mesh;
                meshCollider.sharedMesh = mesh;
            }
        }
        parseHexSurface(mesh);
    }
    /*
    private void SaveMeshAsset(Mesh mesh)
    {
        string assetPath = "Assets/Resources/Prefabs/Tiles/HexagonMesh.asset";
        Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);

        if (savedMesh == null)
        {
            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        else
        {
            savedMesh.Clear();
            EditorUtility.CopySerialized(mesh, savedMesh);
            savedMesh.RecalculateNormals();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
    */
    public void parseHexSurface(Mesh mesh)
    {
        this.surfaceVertices = new Dictionary<Vector3, List<int>>();

        edges = new List<Vector3>(6) { Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero };

        for (int i = 0; i < mesh.vertices.Length; i++)
        {
            Vector3 currentVector = mesh.vertices[i];

            if (currentVector.y > 0)
            {
                int vectorX = Mathf.RoundToInt(currentVector.x);
                int vectorZ = Mathf.RoundToInt(currentVector.z);
                if ((vectorX, vectorZ) == (xoffset, zoffset))
                    edges[0] = currentVector;
                else if ((vectorX, vectorZ) == (0, 2 * zoffset))
                    edges[1] = currentVector;
                else if ((vectorX, vectorZ) == (-xoffset, zoffset))
                    edges[2] = currentVector;
                else if ((vectorX, vectorZ) == (-xoffset, -zoffset))
                    edges[3] = currentVector;
                else if ((vectorX, vectorZ) == (0, -2 * zoffset))
                    edges[4] = currentVector;
                else if ((vectorX, vectorZ) == (xoffset, -zoffset))
                    edges[5] = currentVector;

                if (surfaceVertices.TryGetValue(currentVector, out List<int> index))
                {
                    index.Add(i);
                }
                else
                {
                    this.surfaceVertices.Add(currentVector, new List<int> { i });
                }
            }
        }
    }

    public static Vector3 RandomPointOnPlane(Vector3 A, Vector3 B, Vector3 C)
    {
        // Use barycentric coordinates to find a point within a triangle
        // s and t are random numbers in the [0,1] interval
        float s = UnityEngine.Random.value;
        float t = UnityEngine.Random.value;

        // If the sum of s and t is > 1, we flip it back
        if (s + t > 1f)
        {
            s = 1f - s;
            t = 1f - t;
        }

        // Weighted sum
        Vector3 P = A + s * (B - A) + t * (C - A);
        return P;
    }

    public List<Vector3> CalcEdgePoints(HexTile tile) 
    {
        List<Vector3> edges = new List<Vector3>();

        for (int i = 0; i < this.edges.Count; i++) 
        {
            edges.Add(new Vector3(this.edges[i].x, tile.edgeHeights[i], this.edges[i].z));
        }
        return edges;
    }

    public void LoadEnvironmentPrefabs() 
    {
        grassPrefabs.AddRange(Resources.LoadAll<GameObject>("Prefabs/Environment/Grass"));
        sandPrefabs.AddRange(Resources.LoadAll<GameObject>("Prefabs/Environment/Sand"));
        groundPrefabs.AddRange(Resources.LoadAll<GameObject>("Prefabs/Environment/Ground"));
        rockPrefabs.AddRange(Resources.LoadAll<GameObject>("Prefabs/Environment/Rock"));
    }

    public void AddEnvironementToTile(GameObject tile)
    {
        HexTile hexTile = tile.GetComponent<HexTile>();

        // Get the terrain type from the hexagon
        TileType terrainType = hexTile.terrain;

        // Choose a random prefab based on the terrain type.
        GameObject prefabToSpawn;
        float yoffset = 0f;
        bool centered = false;

        switch (terrainType)
        {
            case TileType.Grass:
                prefabToSpawn = grassPrefabs[UnityEngine.Random.Range(0, grassPrefabs.Count)];
                if (prefabToSpawn.name.Contains("Grass_02") || prefabToSpawn.name.Contains("Grass_03") || prefabToSpawn.name.Contains("Bld"))
                {
                    centered = true;
                }
                break;
            case TileType.Sand:
                prefabToSpawn = sandPrefabs[UnityEngine.Random.Range(0, sandPrefabs.Count)];
                break;
            case TileType.Ground:
                prefabToSpawn = groundPrefabs[UnityEngine.Random.Range(0, groundPrefabs.Count)];
                if (prefabToSpawn.name.Contains("Mangrove"))
                {
                    yoffset = -16.5f;
                }
                else if (prefabToSpawn.name.Contains("Bld"))
                {
                    centered = true;
                }
                break;
            case TileType.Rock:
                prefabToSpawn = rockPrefabs[UnityEngine.Random.Range(0, rockPrefabs.Count - 1)];
                if (prefabToSpawn.name.Contains("Rock"))
                {
                    if (prefabToSpawn.name.Contains("Huge"))
                    {
                        yoffset = UnityEngine.Random.Range(-1f, -3f);
                    }
                    else if (prefabToSpawn.name.Contains("Large"))
                    {
                        yoffset = -1f;
                    }
                    else
                    {
                        yoffset = -0.1f;
                    }
                }
                break;
            default:
                // In case the terrain type doesn't match any known types.
                prefabToSpawn = null;
                break;
        }

        // Instantiate the prefab at the hexagon's position.
        if (prefabToSpawn != null)
        {
            Vector3 center = new Vector3(0, hexTile.centerHeight, 0);
            Vector3 worldPoint;
            int randEdge = UnityEngine.Random.Range(0, 6);
            int prevEdge = randEdge == 0 ? 5 : randEdge - 1;
            List<Vector3> planePoints = CalcEdgePoints(hexTile);
            Vector3 side1 = planePoints[prevEdge] - center;
            Vector3 side2 = planePoints[randEdge] - center;
            Vector3 normal = Vector3.Cross(side1, side2).normalized;
            if (centered)
            {
                worldPoint = tile.transform.TransformPoint(center);
            }
            else
            {
                Vector3 point = RandomPointOnPlane(planePoints[randEdge], planePoints[prevEdge], center);
                worldPoint = tile.transform.TransformPoint(point);
                worldPoint.y += yoffset;
            }

            // Instantiate the prefab with a random rotation
            GameObject tempInstance = Instantiate(prefabToSpawn, worldPoint, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));

            Collider[] colliders = new Collider[10]; // Array to store colliders
            int numColliders = Physics.OverlapBoxNonAlloc(tempInstance.transform.position, tempInstance.transform.localScale / 2f, colliders, Quaternion.identity);

            bool overlapDetected = false;
            for (int i = 0; i < numColliders; i++)
            {
                if (colliders[i].gameObject != tempInstance && colliders[i].gameObject.layer == tempInstance.layer)
                {
                    overlapDetected = true;
                    break;
                }
            }
            // Rotate incrementally until no overlap or full rotation
            const float rotationStep = 10f;
            float totalRotation = 0f;
            while (overlapDetected && totalRotation < 360f)
            {
                tempInstance.transform.rotation *= Quaternion.Euler(0f, rotationStep, 0f);
                totalRotation += rotationStep;

                overlapDetected = false;
                foreach (Collider collider in colliders)
                {
                    Collider[] overlappingColliders = Physics.OverlapBox(collider.bounds.center, collider.bounds.extents, Quaternion.identity);
                    if (overlappingColliders.Length > 1)
                    {
                        overlapDetected = true;
                        break;
                    }
                }
            }

            if (overlapDetected)
            {
                for (int i = 0; i < numColliders; i++)
                {
                    if (colliders[i].gameObject != tempInstance && colliders[i].gameObject.layer == tempInstance.layer)
                    {
                        colliders[i].gameObject.SetActive(false);
                        Destroy(colliders[i].gameObject);
                        break;
                    }
                }
            }

            // Set the final rotation
            tempInstance.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            tempInstance.transform.parent = tile.transform;
            tempInstance.name = prefabToSpawn.name;

            hexTile.envObj = tempInstance;
        }
    }

    private void PrintMeshInfo(GameObject tilePrefab, string prefabName)
    {
        MeshFilter meshFilter = tilePrefab.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            Mesh mesh = meshFilter.sharedMesh;
            Debug.Log($"{prefabName}: Vertices = {mesh.vertexCount}, Triangles = {mesh.triangles.Length / 3}");
        }
        else
        {
            Debug.Log($"{prefabName}: Mesh Filter not found");
        }
    }

    private void OnDrawGizmos()
    {
        GameObject sea = GameObject.Find("Sea");
        
        if (sea!=null)
        {
            List<HexTile> seaTiles = sea.GetComponents<HexTile>().ToList();

            if (seaTiles!= null) 
            {
                foreach (HexTile tile in seaTiles)
                {
                    if (tile.node.neighbours != null)
                    {
                        foreach (HexNode neighbour in tile.node.neighbours)
                        {
                            Gizmos.DrawLine(tile.transform.position, tiles[neighbour.gridNum].transform.position);
                        }
                    }
                }
            }
            
        }
    }

    

    public bool IsPointInTheMiddle(Vector3 A, Vector3 B, Vector3 C)
    {

        if ((A.x == edges[0].x &&  A.z == edges[0].z && B.x == edges[5].x && B.z == edges[5].z) || (A.x == edges[3].x && A.z == edges[3].z && B.x == edges[2].x && B.z == edges[2].z))
        {
            if (A.x == C.x && C.z == 0)
                return true;

        }
        else 
        {
            Vector3 AB = (A - B)/2;
            if (Math.Abs((AB).x) == Math.Abs(C.x) && Math.Abs(AB.z) == Math.Abs(C.z))
                return true;
        }
        return false;
    }

    private void ModifyTileMesh(GameObject tile)
    {
        HexTile hexTile = tile.GetComponent<HexTile>();
        float tileHeight = heightMap[hexTile.gridNum.x, hexTile.gridNum.y];
        MeshFilter meshFilter = tile.GetComponent<MeshFilter>();
        MeshCollider meshCollider = tile.GetComponent<MeshCollider>();
        Mesh tileMesh = meshFilter.mesh;
        Vector3[] vertices = tileMesh.vertices;
        List<int> meshIndices;
        

        for (int i=0; i<edges.Count; i++)
        {
            float targetHeight;
            meshIndices = surfaceVertices[edges[i]];
            int nnIndex = i == edges.Count - 1 ? 0 : i + 1;
            int npIndex = i;

            Vector2Int nnGridNum, npGridNum;
            float nnHeight = 0, npHeight = 0;
            bool nnFlag = hexTile.neighbourIndexCoordinateDict.TryGetValue(nnIndex, out nnGridNum);
            bool npFlag = hexTile.neighbourIndexCoordinateDict.TryGetValue(npIndex, out npGridNum);
            if (nnFlag)
            {
                nnHeight = heightMap[nnGridNum.x, nnGridNum.y];
            }

            if (npFlag)
            {
                npHeight = heightMap[npGridNum.x, npGridNum.y];
            }

            switch ((npFlag, nnFlag))
            {
                case (true, true):
                    targetHeight = (float)Math.Round((tileHeight + nnHeight + npHeight) / 3, 1);
                    break;
                case (false, true):
                    targetHeight = (float)Math.Round((tileHeight + nnHeight) / 2, 1);
                    break;
                case (true, false):
                    targetHeight = (float)Math.Round((tileHeight + npHeight) / 2, 1);
                    break;
                case (false, false):
                    targetHeight = tileHeight;
                    break;
            }
            foreach (int index in meshIndices)
            {
                Vector3 vector = vertices[index];
                vector.y = targetHeight;
                vertices[index] = vector;
                
            }
            hexTile.edgeHeights.Add(i,targetHeight);
        }
        Vector3 center = new Vector3(0f, tileInitialHeight/2f, 0f);
        meshIndices = surfaceVertices[center];

        foreach (int index in meshIndices)
        {
            Vector3 vector = vertices[index];
            vector.y = (hexTile.edgeHeights[0]+ hexTile.edgeHeights[1] + hexTile.edgeHeights[2] + hexTile.edgeHeights[3] + hexTile.edgeHeights[4] + hexTile.edgeHeights[5])/6;
            vertices[index] = vector;
            if (hexTile.centerHeight == -1)
            {
                hexTile.centerHeight = vector.y;
            }
        }


        foreach (KeyValuePair<Vector3, List<int>> vector in surfaceVertices)
        {
            if (!edges.Contains(vector.Key) && vector.Key != center)
            {
                float targetHeight;
                float angle = Mathf.Round(Mathf.Atan2(vector.Key.z, vector.Key.x) * Mathf.Rad2Deg);
                angle = angle>0? angle + 30: angle - 30;
                int edgeIndex1 = ((int)Math.Round((angle) / 60)+6) % 6;
                int edgeIndex2 = edgeIndex1==0 ? 5:edgeIndex1-1;

                Vector3 edgePoint1 = vertices[surfaceVertices[edges[edgeIndex1]][0]];
                Vector3 edgePoint2 = vertices[surfaceVertices[edges[edgeIndex2]][0]];

                if (IsPointInTheMiddle(edgePoint1,edgePoint2,vector.Key)) 
                {
                    targetHeight = (edgePoint1.y + edgePoint2.y) / 2;
                    foreach (int index in meshIndices)
                    {
                        Vector3 targetVector = vertices[index];
                        targetVector.y = targetHeight;
                        vertices[index] = targetVector;
                    }
                }
                   
                else
                {
                    Vector3 centerPoint = vertices[surfaceVertices[center][0]];

                    // Calculate plane normal
                    Vector3 edgeVector1 = edgePoint1 - centerPoint;
                    Vector3 edgeVector2 = edgePoint2 - centerPoint;
                    Vector3 planeNormal = Vector3.Cross(edgeVector1, edgeVector2).normalized;

                    // Calculate D in plane equation
                    float D = -Vector3.Dot(planeNormal, centerPoint);

                    foreach (int index in vector.Value)
                    {
                        Vector3 vertex = vertices[index];

                        // Calculate y coordinate using the plane equation
                        if (Math.Abs(planeNormal.y) > float.Epsilon)  // prevent division by zero
                        {
                            vertex.y = -(planeNormal.x * vertex.x + planeNormal.z * vertex.z + D) / planeNormal.y;
                        }

                        vertices[index] = vertex;
                    }
                }
                
            }

        }
        tileMesh.vertices = vertices;
        tileMesh.RecalculateBounds();
        tileMesh.RecalculateNormals();
        meshFilter.mesh = tileMesh;
        meshCollider.sharedMesh = tileMesh;
    }

    private List<HexTile> ConnectRockNodes() 
    {
        List<HexTile> rockTiles = new List<HexTile>();
        foreach(Island island in islands) 
        {
            foreach (HexTile tile in island.islandTiles) 
            {
                if (tile.terrain == TileType.Rock) 
                {
                    rockTiles.Add(tile);
                    // Instantiate a new empty list of hexnodes
                    List<HexNode> rockNodeNeighbours = new List<HexNode>();

                    foreach (Vector2Int neighbourCoord in neighbourInfo[tile.gridNum])
                    {
                        if(tiles[neighbourCoord].node.tileType == TileType.Rock)
                            rockNodeNeighbours.Add(tiles[neighbourCoord].node);
                    }
                    tile.node.neighbours = rockNodeNeighbours;
                }
            }
        }
        return rockTiles;
    }

    private void ConnectSailingNodes()
    {
        // go through each tile in the grid (dict values)
        foreach (HexTile tile in tiles.Values)
        {
            if (tile.terrain == TileType.Grass || tile.terrain == TileType.Ground || tile.terrain == TileType.Rock)
                continue;
            // Instantiate a new empty list of hexnodes
            List<HexNode> neighbours = new List<HexNode>();

            foreach (Vector2Int neighbourCoord in neighbourInfo[tile.gridNum])
            {
                if (tile.terrain == TileType.Water && (tiles[neighbourCoord].terrain == TileType.Water || tiles[neighbourCoord].terrain == TileType.Sand))
                {
                    neighbours.Add(tiles[neighbourCoord].node);
                }
                else if (tile.terrain == TileType.Sand && tiles[neighbourCoord].terrain == TileType.Water)
                {
                    tile.island.AddDockingTile(tile);
                    tile.dockTile = true;
                    tile.node.dockingSpot = true;
                    neighbours.Add(tiles[neighbourCoord].node);
                }
               
            }
            tile.node.neighbours = neighbours;
        }
    }

    private Vector3 GetPositionForHexFromCoordinate(Vector2Int coordinate)
    {
        int column = coordinate.x;
        int row = coordinate.y;
        float width;
        float height;
        float xPosition;
        float yPosition;
        bool shouldOffset;
        float horizontalDistance;
        float verticalDistance;
        float offset;
        float size = tileOuterSize;

        shouldOffset = (row % 2) == 0;
        width = Mathf.Sqrt(3) * size;
        height = 2f * size;
        horizontalDistance = width;
        verticalDistance = height * (3f / 4f);
        offset = shouldOffset ? width / 2 : 0;
        xPosition = column * horizontalDistance + offset;
        yPosition = row * verticalDistance;

        return new Vector3(xPosition, 0, yPosition);
    }


    public HexTile RandomTileAtDepth(HexTile current, int currentDepth, int maxDepth, bool[,] visited)
    {
        System.Random random = new System.Random();
        visited[current.gridNum.x, current.gridNum.y] = true;

        if (currentDepth < maxDepth) 
        {
            List<KeyValuePair<int,Vector2Int>> shuffledNeighbours = current.neighbourIndexCoordinateDict.OrderBy(x => random.Next()).Take(current.neighbourIndexCoordinateDict.Count).ToList();
            
            foreach (KeyValuePair<int, Vector2Int> entry in shuffledNeighbours)
            {
                if (tiles[entry.Value].terrain==TileType.Rock && !visited[entry.Value.x, entry.Value.y])
                {
                    bool[,] newVisited = new bool[gridDimension.x, gridDimension.y];
                    newVisited = (bool[,]) visited.Clone();
                    int leftKey = entry.Key == 5 ? 0 : entry.Key + 1;
                    int rightKey = entry.Key == 0 ? 5 : entry.Key - 1;

                    Vector2Int leftCoord;
                    Vector2Int rightCoord;

                    if (current.neighbourIndexCoordinateDict.TryGetValue(leftKey, out leftCoord))
                    {
                        newVisited[leftCoord.x, leftCoord.y] = true;
                    }
                    
                    if (current.neighbourIndexCoordinateDict.TryGetValue(rightKey, out rightCoord))
                    {
                        newVisited[rightCoord.x, rightCoord.y] = true;
                    }
                    current = RandomTileAtDepth(tiles[entry.Value], currentDepth+1, maxDepth, newVisited);
                    
                    if(!current.node.occupied)
                        break;
                }
            } 
        }
       
        return current;
    }

}
