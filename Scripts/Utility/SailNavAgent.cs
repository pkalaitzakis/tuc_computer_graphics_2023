using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class HexNode
{
    public Vector3 worldPoint;
    public float x;
    public float y;
    public float z;
    public bool occupied = false;
    public List<HexNode> neighbours;
    public Vector2Int gridNum;
    // heuristic value, acts as an estimate of the cost of the path from node n to goal node
    public float h;
    // cost value of the path from starting node to next node n
    public float g;
    public float w=1f;
    public TileType tileType;
    public bool dockingSpot = false;

    public HexNode CreateNode(float x, float z, Vector2Int gridNum, TileType tileType)
    {
        this.x = x;
        this.z = z;
        this.gridNum = gridNum;
        this.tileType = tileType;
        return this;
    }

    public void setWorldPoint(float tileHeight) 
    {
        this.y = tileHeight;
        this.worldPoint = new Vector3(x,y,z);
    }

    public float EvalHeuristic(HexNode n)
    {
        return Mathf.Sqrt(Mathf.Pow((n.z - this.z), 2) + Mathf.Pow((n.x - this.x), 2));
    }

    public float CalcCost(HexNode p)
    {
        g = p.g + w;
        return g;
    }
}

public class PriorityQueue<T> where T : IComparable<T>
{
    private readonly List<T> heap = new List<T>();

    public int Count { get { return heap.Count; } }

    public void Enqueue(T item)
    {
        heap.Add(item);
        int i = heap.Count - 1;
        while (i > 0)
        {
            int parent = (i - 1) / 2;
            if (heap[parent].CompareTo(heap[i]) <= 0)
                break;
            (heap[i], heap[parent]) = (heap[parent], heap[i]);
            i = parent;
        }
    }

    public T Dequeue()
    {
        if (heap.Count == 0)
            throw new InvalidOperationException("Queue is empty.");
        T item = heap[0];
        int last = heap.Count - 1;
        heap[0] = heap[last];
        heap.RemoveAt(last);
        last--;
        int i = 0;
        while (true)
        {
            int left = i * 2 + 1;
            int right = i * 2 + 2;
            if (left > last)
                break;
            int minChild = left;
            if (right <= last && heap[right].CompareTo(heap[left]) < 0)
                minChild = right;
            if (heap[i].CompareTo(heap[minChild]) <= 0)
                break;
            (heap[minChild], heap[i]) = (heap[i], heap[minChild]);
            i = minChild;
        }
        return item;
    }

    public T Peek()
    {
        if (heap.Count == 0)
            throw new InvalidOperationException("Queue is empty.");
        return heap[0];
    }
}

public class Path : IComparable<Path>
{
    public float f;
    public float g = 0f;
    public List<HexNode> path;

    public Path(List<HexNode> currentPath)
    {
        path = new List<HexNode>();
        foreach (HexNode node in currentPath)
            path.Add(node);
    }

    public void EstimatePath(float g, float h)
    {
        f = g + h;
    }

    public void IncreaseRealCost(float value)
    {
        g += value;
    }

    public int CompareTo(Path other)
    {
        if (other.f > this.f) return -1;
        else if (other.f == this.f) return 0;
        else return 1;
    }

    public void Add(HexNode node)
    {
        path.Add(node);
    }

    public HexNode Last() 
    {
        return path[path.Count - 1];
    }

    public HexNode First()
    {
        return path[0];
    }
}

public class SailNavAgent { 
    public Path AStar(HexNode start, HexNode goal)
    {
        PriorityQueue<Path> fringe = new PriorityQueue<Path>();
        start.g = 0;
        start.EvalHeuristic(goal);
        Path startingPath = new Path(new List<HexNode>() { start });
        startingPath.EstimatePath(start.g, start.h);
        fringe.Enqueue(startingPath);
        Dictionary<HexNode, float> open = new Dictionary<HexNode, float>();
        Dictionary<HexNode, float> closed = new Dictionary<HexNode, float>();

        while (fringe.Count > 0)
        {
            Path path = fringe.Dequeue();
            HexNode current = path.Last();

            if (current == goal) 
            {
                //pathVisualizer.DrawPath(path);
                return path; 
            }

            if (closed.ContainsKey(current))
                continue;

            closed[current] = current.g;

            foreach (HexNode neighbor in current.neighbours)
            {
                if (neighbor.tileType == TileType.Sand && neighbor!=goal)
                    continue;

                neighbor.CalcCost(current);
                float tentativeG = neighbor.g;

                if (closed.TryGetValue(neighbor, out float closedCost) && tentativeG >= closedCost)
                    continue;

                if (!open.ContainsKey(neighbor) || tentativeG < open[neighbor])
                {
                    Path newPath = new Path(path.path);
                    newPath.Add(neighbor);
                    newPath.EstimatePath(tentativeG, neighbor.h);
                    fringe.Enqueue(newPath);
                    open[neighbor] = tentativeG;
                }
            }
        }

        return null;
    }
}

