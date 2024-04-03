using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using UnityEngine;

public class RockCluster
{
    public Island island;
    public List<HexTile> rockTiles;
    public HexTile centerTile;

    public RockCluster(Island island) 
    {
        rockTiles = new List<HexTile>();
        this.island = island;
    }


    public void CenterTile()
    {
        int maxTiles = 0;
        foreach (HexTile tile in rockTiles)
        {
            if (tile.node.neighbours.Count == 6) 
            {
                HashSet<HexNode> visited = new HashSet<HexNode>();
                DFS(tile.node, visited, 0);
                if(visited.Count == 37) 
                {
                    centerTile = tile;
                    return;
                }
                else if (visited.Count > maxTiles)
                {
                    maxTiles = visited.Count;
                    centerTile = tile;
                }
            }
            
        }
    }



    public void DFS(HexNode node, HashSet<HexNode> visited, int depth) 
    {
        visited.Add(node);

        foreach (HexNode neighbour in node.neighbours) 
        {
            if (!visited.Contains(neighbour)&& neighbour.neighbours.Count == 6 && depth<4) 
            {
                DFS(neighbour, visited, depth+1);
            }
        }
    }

   
}

public class Island : MonoBehaviour
{
    public List<HexTile> dockTiles;
    public List<HexTile> islandTiles; // All tiles that belong to this island
    public List<RockCluster> rockClusters;
    public List<Vector2Int> tileCoods; // All tiles' coords that belong to this island
    public HexTile highestTile; // Highest tile in the island
    public bool hasEnemyCamp;
    private void Awake()
    {
        this.dockTiles = new List<HexTile>();
        this.tileCoods = new List<Vector2Int>();
        this.islandTiles = new List<HexTile>();
        this.rockClusters = new List<RockCluster>();
        highestTile = null;
    }

    // Adds a tile to the island
    public void AddIslandTile(HexTile tile)
    {
        this.islandTiles.Add(tile);
        if (highestTile == null)
            highestTile = tile;
        else if (tile.centerHeight > highestTile.centerHeight)
        {
            highestTile = tile;
        }
    }

    public void AddTileCoord(Vector2Int coord)
    {
        this.tileCoods.Add(coord);
    }

    public void AddDockingTile(HexTile tile)
    {
        this.dockTiles.Add(tile);
    }

    public RockCluster AddTileToRockCluster(RockCluster cluster, HexTile tile)
    {
        if (cluster == null)
        {
            RockCluster newCluster = new RockCluster(this);
            newCluster.rockTiles.Add(tile);
            rockClusters.Add(newCluster);
            return newCluster;
        }
        else 
        {
            if(!cluster.rockTiles.Contains(tile)) 
            {
                cluster.rockTiles.Add(tile);
            }
            return cluster;
        }
        

    }

    public bool TileBelongsToIsland(HexTile tile)
    {
        return this.islandTiles.Contains(tile);
    }
}