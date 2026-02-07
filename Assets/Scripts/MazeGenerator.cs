using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class MazeGenerator : MonoBehaviour
{
    public GameObject wallPrefab;
    public GameObject doorPrefab; 
    private bool doorPlaced = false;

    public int width, height;
    public Material brick;
    public int[,] Maze;
    public List<Vector3> pathMazes = new List<Vector3>();
    private Stack<Vector2> _tiletoTry = new Stack<Vector2>();
    private List<Vector2> offsets = new List<Vector2> { new Vector2(0, 1), new Vector2(0, -1), new Vector2(1, 0), new Vector2(-1, 0) };
    private System.Random rnd = new System.Random();
    private int _width, _height;
    private Vector2 _currentTile;

    public Vector2 CurrentTile
    {
        get { return _currentTile; }
        private set
        {
            if (value.x < 1 || value.x >= this.width - 1 || value.y < 1 || value.y >= this.height - 1)
            {
                throw new ArgumentException("CurrentTile must be within the one tile border all around the maze");
            }
            if (value.x % 2 == 1 || value.y % 2 == 1)
            {
                _currentTile = value;
            }
            else
            {
                throw new ArgumentException("The current square must not be both on an even X-axis and an even Y-axis, to ensure we can get walls around all tunnels");
            }
        }
    }

    private static MazeGenerator instance;
    public static MazeGenerator Instance => instance;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {

    }

 public void GenerateMaze()
{
    if (width < 5 || height < 5)
    {
        return;
    }

    Maze = new int[width, height];
    for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            Maze[x, y] = 1;

    CurrentTile = new Vector2(1, 1);
    _tiletoTry.Push(CurrentTile);

    Maze = CreateMaze();

    AddLoops(0.12f);

    float offsetX = -(width - 1) / 2f;
    float offsetY = -(height - 1) / 2f;

    List<(int x, int y, Quaternion rot)> doorCandidates = new List<(int, int, Quaternion)>();

    for (int i = 0; i < width; i++)
    {
        if (Maze[i, 0] == 1 && Maze[i, 1] == 0)
            doorCandidates.Add((i, 0, Quaternion.Euler(0, 0, 180)));
        if (Maze[i, height - 1] == 1 && Maze[i, height - 2] == 0)
            doorCandidates.Add((i, height - 1, Quaternion.identity));
    }

    for (int j = 0; j < height; j++)
    {
        if (Maze[0, j] == 1 && Maze[1, j] == 0)
            doorCandidates.Add((0, j, Quaternion.Euler(0, 0, 90)));
        if (Maze[width - 1, j] == 1 && Maze[width - 2, j] == 0)
            doorCandidates.Add((width - 1, j, Quaternion.Euler(0, 0, -90)));
    }

    if (doorCandidates.Count > 0)
    {
        var chosen = doorCandidates[rnd.Next(doorCandidates.Count)];
        Vector3 doorPos = new Vector3(chosen.x + offsetX, chosen.y + offsetY, 0);
        Instantiate(doorPrefab, doorPos, chosen.rot, transform);
        Maze[chosen.x, chosen.y] = 0;
    }

    for (int i = 0; i < width; i++)
    {
        for (int j = 0; j < height; j++)
        {
            Vector3 spawnPos = new Vector3(i + offsetX, j + offsetY, 0);

            if (Maze[i, j] == 1)
            {
                Instantiate(wallPrefab, spawnPos, Quaternion.identity, transform);
            }
            else if (Maze[i, j] == 0)
            {
                if (!pathMazes.Contains(spawnPos))
                {
                    pathMazes.Add(spawnPos);
                }
            }
        }
    }
}



    public int[,] CreateMaze()
    {
        List<Vector2> neighbors;
        while (_tiletoTry.Count > 0)
        {
            Maze[(int)CurrentTile.x, (int)CurrentTile.y] = 0;
            neighbors = GetValidNeighbors(CurrentTile);

            if (neighbors.Count > 0)
            {
                _tiletoTry.Push(CurrentTile);
                CurrentTile = neighbors[rnd.Next(neighbors.Count)];
            }
            else
            {
                CurrentTile = _tiletoTry.Pop();
            }
        }

        return Maze;
    }

    private void AddLoops(float loopChance = 0.15f)
    {
        for (int x = 1; x < width - 1; x++)
        {
            for (int y = 1; y < height - 1; y++)
            {
                if (Maze[x, y] == 1 && UnityEngine.Random.value < loopChance)
                {
                    int pathCount = 0;
                    foreach (var dir in offsets)
                    {
                        int nx = x + (int)dir.x;
                        int ny = y + (int)dir.y;
                        if (nx >= 0 && ny >= 0 && nx < width && ny < height && Maze[nx, ny] == 0)
                        {
                            pathCount++;
                        }
                    }

                    if (pathCount >= 2)
                    {
                        Maze[x, y] = 0;
                        float offsetX = -(width - 1) / 2f;
                        float offsetY = -(height - 1) / 2f;
                        Vector3 worldPos = new Vector3(x + offsetX, y + offsetY, 0);
                        if (!pathMazes.Contains(worldPos))
                        {
                            pathMazes.Add(worldPos);
                        }
                    }
                }
            }
        }
    }
    
    private List<Vector2> GetValidNeighbors(Vector2 centerTile)
    {
        List<Vector2> validNeighbors = new List<Vector2>();

        foreach (var offset in offsets)
        {
            Vector2 toCheck = new Vector2(centerTile.x + offset.x, centerTile.y + offset.y);

            if (!IsInside(toCheck)) continue;

            if ((toCheck.x % 2 == 1 || toCheck.y % 2 == 1)
                && Maze[(int)toCheck.x, (int)toCheck.y] == 1
                && HasThreeWallsIntact(toCheck))
            {
                validNeighbors.Add(toCheck);
            }
        }

        return validNeighbors;
    }


    private bool HasThreeWallsIntact(Vector2 Vector2ToCheck)
    {
        int intactWallCounter = 0;

        foreach (var offset in offsets)
        {
            Vector2 neighborToCheck = new Vector2(Vector2ToCheck.x + offset.x, Vector2ToCheck.y + offset.y);
            if (IsInside(neighborToCheck) && Maze[(int)neighborToCheck.x, (int)neighborToCheck.y] == 1)
            {
                intactWallCounter++;
            }
        }

        return intactWallCounter == 3;
    }
    public List<Vector3> GetPathPositions()
    {
        return pathMazes;
    }
   

    private bool IsInside(Vector2 p)
    {
        return p.x >= 0 && p.y >= 0 && p.x < width && p.y < height;
    }
}
