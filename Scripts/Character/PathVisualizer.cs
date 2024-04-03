using UnityEngine;

public class PathVisualizer : MonoBehaviour
{
    public Transform player;
    public float fadeDistance = 5f;
    [SerializeField] private Material Get;
    private LineRenderer lineRenderer;
    private Path path = null;

    void Awake()
    {
        player = gameObject.transform;
        gameObject.AddComponent<LineRenderer>();
        lineRenderer = gameObject.GetComponent<LineRenderer>();

        // Load the material from the Resources folder
        Material lineMaterial = Resources.Load<Material>("Materials/Green");

        // Assign the material to the LineRenderer component
        lineRenderer.material = lineMaterial;

    }

    public void DrawPath()
    {
        if (path != null && path.path != null)
        {
            lineRenderer.positionCount = path.path.Count;
            for (int i = 0; i < path.path.Count; i++)
            {
                Vector3 startPoint = new Vector3(path.path[i].x, path.path[i].y, path.path[i].z);
                lineRenderer.SetPosition(i, startPoint);
            }
        }
    }

    public void AssignPath(Path path) 
    {
        this.path = path;
    }

    public void UnsetPath() 
    {
        this.path = null;
    }

   private void Update()
    {
        DrawPath();

       
    }
}

