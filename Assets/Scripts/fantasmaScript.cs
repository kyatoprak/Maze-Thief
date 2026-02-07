using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AStarAI : MonoBehaviour
{
    public float cellSize = 1f;
    public MazeGenerator mazeGenerator;
    public float repathDelay = 0.5f;
    public float moveSpeed = 2f;

    private Vector2? fleeTarget = null; 
    private gameManager GameManager;
    private playerScript PlayerScript;

    private bool isPlayerInTrigger = false;
    private float requiredTimeToLose = 6f;
    private bool hasLost = false;

    public List<Vector2> path = new List<Vector2>();
    private float timeSinceLastPath = 0f;
    private bool playerInCloset = false;

    private Coroutine loseCountdown;

    void Start()
    {
        mazeGenerator = FindObjectOfType<MazeGenerator>();
        GameManager = GameObject.FindGameObjectWithTag("GameController")?.GetComponent<gameManager>();
        PlayerScript = GameObject.FindGameObjectWithTag("Player")?.GetComponent<playerScript>();

        if (GameManager != null) GameManager.ghosts.Add(this);
    }

    public void PlayerIsInCloset(bool isInCloset)
    {
        playerInCloset = isInCloset;

        if (isInCloset)
        {
        }
        else
        {
            fleeTarget = null; 
        }

        timeSinceLastPath = repathDelay; 
    }
    void Update()
    {
        bool canMove = GameManager != null && GameManager.GameIsActive && !GameManager.eyeIsActive;
        timeSinceLastPath += Time.deltaTime;

        if (canMove && timeSinceLastPath >= repathDelay)
        {
            timeSinceLastPath = 0f;

            if (playerInCloset)
            {
                if (!fleeTarget.HasValue || path.Count == 0)
                {
                    Vector2 newTarget = GetRandomSafePosition();
                    fleeTarget = newTarget;
                }

                Vector2 aiPos = WorldToGrid(transform.position);
                Vector2 targetPos = WorldToGrid(fleeTarget.Value);
                path = AStarAlgorithm(aiPos, targetPos);
            }
            else
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return;

                Vector2 startPos = WorldToGrid(transform.position);
                Vector2 endPos = WorldToGrid(player.transform.position);
                path = AStarAlgorithm(startPos, endPos);
            }
        }

        if (canMove) FollowPath();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = true;

            if (loseCountdown == null)
                loseCountdown = StartCoroutine(LoseIfStayed());
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInTrigger = false;

            if (loseCountdown != null)
            {
                StopCoroutine(loseCountdown);
                loseCountdown = null;
            }
        }
    }

    private IEnumerator LoseIfStayed()
    {
        float t = 0f;
        while (t < requiredTimeToLose)
        {
            if (!isPlayerInTrigger) yield break;

            t += Time.deltaTime;
            yield return null;
        }

        if (!hasLost)
        {
            hasLost = true;
            GameManager.GameIsFinishedFailure();
        }
    }

    void FollowPath()
    {
        if (path == null || path.Count == 0) return;

        Vector2 targetWaypoint = path[0];
        transform.position = Vector2.MoveTowards(transform.position, targetWaypoint, moveSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetWaypoint) < 0.1f)
        {
            path.RemoveAt(0);

            if (path.Count == 0 && playerInCloset)
            {
                fleeTarget = GetRandomSafePosition();
            }
        }
    }

    private Vector2 GetRandomSafePosition()
    {
        List<Vector3> paths = MazeGenerator.Instance.GetPathPositions();
        if (paths.Count == 0) return transform.position;

        Vector3 playerPos = PlayerScript.transform.position;

        Vector3 hidePosition = playerPos;

        List<Vector3> candidates = new List<Vector3>();

        foreach (Vector3 pos in paths)
        {
            if (Vector3.Distance(pos, hidePosition) > 5f) 
            {
                candidates.Add(pos);
            }
        }

        if (candidates.Count == 0)
        {
            foreach (Vector3 pos in paths)
            {
                if (Vector3.Distance(pos, hidePosition) > 3f)
                {
                    candidates.Add(pos);
                }
            }
        }

        if (candidates.Count == 0)
        {
            candidates = paths;
        }

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    // ----------------- A* -----------------
    public List<Vector2> AStarAlgorithm(Vector2 start, Vector2 end)
    {
        var openList = new List<Node>();
        var closedList = new HashSet<Vector2>();
        var allNodes = new Dictionary<Vector2, Node>();

        Node startNode = new Node(start);
        startNode.gCost = 0;
        startNode.hCost = Vector2.Distance(start, end);
        startNode.fCost = startNode.gCost + startNode.hCost;
        openList.Add(startNode);
        allNodes[start] = startNode;

        while (openList.Count > 0)
        {
            Node current = GetLowestCostNode(openList);
            openList.Remove(current);
            closedList.Add(current.position);

            if (current.position == end) return ReconstructPath(current);

            foreach (Vector2 neighbor in GetNeighbors(current.position))
            {
                if (closedList.Contains(neighbor) || !IsWalkable(neighbor)) continue;

                float tentativeG = current.gCost + Vector2.Distance(current.position, neighbor);
                Node neighborNode = allNodes.ContainsKey(neighbor) ? allNodes[neighbor] : new Node(neighbor);

                if (tentativeG < neighborNode.gCost)
                {
                    neighborNode.gCost = tentativeG;
                    neighborNode.hCost = Vector2.Distance(neighbor, end);
                    neighborNode.fCost = neighborNode.gCost + neighborNode.hCost;
                    neighborNode.parent = current;

                    if (!openList.Contains(neighborNode))
                    {
                        openList.Add(neighborNode);
                        allNodes[neighbor] = neighborNode;
                    }
                }
            }
        }
        return new List<Vector2>();
    }

    private List<Vector2> GetNeighbors(Vector2 pos)
    {
        return new List<Vector2>
        {
            pos + Vector2.up,
            pos + Vector2.down,
            pos + Vector2.left,
            pos + Vector2.right
        };
    }

    private bool IsWithinBounds(Vector2 pos)
    {
        return pos.x >= 0 && pos.y >= 0 && pos.x < mazeGenerator.width && pos.y < mazeGenerator.height;
    }

    private bool IsWalkable(Vector2 pos)
    {
        int x = Mathf.RoundToInt(pos.x), y = Mathf.RoundToInt(pos.y);
        return x >= 0 && y >= 0 && x < mazeGenerator.width && y < mazeGenerator.height && mazeGenerator.Maze[x, y] == 0;
    }

    private Node GetLowestCostNode(List<Node> list)
    {
        Node lowest = list[0];
        for (int i = 1; i < list.Count; i++)
            if (list[i].fCost < lowest.fCost) lowest = list[i];
        return lowest;
    }

    public List<Vector2> ReconstructPath(Node node)
    {
        List<Vector2> path = new List<Vector2>();
        while (node != null)
        {
            path.Add(GridToWorld(node.position));
            node = node.parent;
        }
        path.Reverse();
        return path;
    }

    private Vector2 GridToWorld(Vector2 gridPos)
    {
        float offsetX = -(mazeGenerator.width - 1) / 2f;
        float offsetY = -(mazeGenerator.height - 1) / 2f;
        return new Vector2(gridPos.x + offsetX, gridPos.y + offsetY);
    }

    private Vector2 WorldToGrid(Vector2 worldPos)
    {
        float offsetX = -(mazeGenerator.width - 1) / 2f;
        float offsetY = -(mazeGenerator.height - 1) / 2f;
        return new Vector2(Mathf.Round(worldPos.x - offsetX), Mathf.Round(worldPos.y - offsetY));
    }

    public class Node
    {
        public Vector2 position;
        public float gCost, hCost, fCost;
        public Node parent;
        public Node(Vector2 pos) { position = pos; gCost = hCost = fCost = float.MaxValue; parent = null; }
    }

    void OnDrawGizmos()
    {
        if (path == null || path.Count < 2) return;
        Gizmos.color = Color.red;
        for (int i = 0; i < path.Count - 1; i++) Gizmos.DrawLine(path[i], path[i + 1]);
    }
}
