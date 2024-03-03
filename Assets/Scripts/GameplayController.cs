using System;
using System.Collections.Generic;
using UnityEngine;

public class GameplayController : MonoBehaviour
{
    [SerializeField] private bool oneDimensionalMovement;
    [SerializeField] private float gameplayTickLength = 0.05f;
    [SerializeField] private GameRenderController renderController;

    private Vector2Int playerGridPosition = new Vector2Int(0, 0);
    private Vector2 currentInputVector = Vector2.zero;
    private Vector2 scheduledInputVector = Vector2.zero;
    
    private float gameplayTickCounter = 0.0f;
    private CellState[][] grid;

    private List<Vector2Int> markedPath = new List<Vector2Int>();

    private void Start()
    {
        InitializeGrid();
        renderController.Initialize();
        renderController.UpdateGrid(grid);
        UpdatePlayerPosition(Vector2Int.zero);
    }

    private void Update()
    {
        ReadInput();

        if (gameplayTickCounter < gameplayTickLength)
        {
            gameplayTickCounter += Time.deltaTime;
        }
        else
        {
            gameplayTickCounter = 0.0f;
            ExecuteInput();
        }
    }
    
    private void InitializeGrid()
    {
        grid = new CellState[Constants.GRID_SIZE][];

        for (int x = 0; x < Constants.GRID_SIZE; x++)
        {
            grid[x] = new CellState[Constants.GRID_SIZE];
            
            for (int y = 0; y < Constants.GRID_SIZE; y++)
            {
                if (x == 0 || y == 0 || x == Constants.GRID_SIZE-1 || y == Constants.GRID_SIZE-1)
                {
                    grid[x][y] = CellState.Taken;
                }
                else
                {
                    grid[x][y] = CellState.Free;
                }
            }
        }
    }

    private void ReadInput()
    {
        float xInput = Input.GetAxis("Horizontal");
        float yInput = Input.GetAxis("Vertical");

        if (oneDimensionalMovement)
        {
            float absX = Mathf.Abs(xInput);
            float absY = Mathf.Abs(yInput);
            
            if (absX > absY) yInput = 0.0f;
            else if (absY > absX) xInput = 0.0f;
            else if (absX > 0.0f && absY > 0.0f) yInput = 0.0f;
        }

        currentInputVector = new Vector2(xInput, yInput);
        
        if (currentInputVector.magnitude > 0.0f)
        {
            scheduledInputVector = currentInputVector;
        }
    }

    private void ExecuteInput()
    {
        if (scheduledInputVector.magnitude > 0.0f)
        {
            Vector2Int delta = new Vector2Int(Math.Sign(scheduledInputVector.x), Math.Sign(scheduledInputVector.y));
            scheduledInputVector = Vector2.zero;
            UpdatePlayerPosition(delta);
        }
    }

    private void UpdatePlayerPosition(Vector2Int delta)
    {
        playerGridPosition += delta;

        if (playerGridPosition.x < 0)
        {
            playerGridPosition.x = 0;
        }
        else if (playerGridPosition.x > Constants.GRID_SIZE-1)
        {
            playerGridPosition.x = Constants.GRID_SIZE - 1;
        }
            
        if (playerGridPosition.y < 0)
        {
            playerGridPosition.y = 0;
        }
        else if (playerGridPosition.y > Constants.GRID_SIZE-1)
        {
            playerGridPosition.y = Constants.GRID_SIZE - 1;
        }

        bool gridChanged = false;
        
        if (grid[playerGridPosition.x][playerGridPosition.y] == CellState.Free)
        {
            gridChanged = true;
            grid[playerGridPosition.x][playerGridPosition.y] = CellState.Marked;
            markedPath.Add(playerGridPosition);
        }
        else if (grid[playerGridPosition.x][playerGridPosition.y] == CellState.Taken)
        {
            if (markedPath.Count > 0)
            {
                gridChanged = true;
                HandlePlayerFinishedMark();
            }
        }

        if (gridChanged)
        {
            renderController.UpdateGrid(grid);
        }

        renderController.UpdatePlayerPosition(playerGridPosition);
    }

    private void HandlePlayerFinishedMark()
    {
        // Determine if any cells need to be filled
        List<HashSet<Vector2Int>> areasToFill = DetermineFillAreas();
        
        // Remove marked cells one by one
        for (int i = markedPath.Count - 1; i >= 0; i--)
        {
            Vector2Int markedCell = markedPath[i];
            markedPath.RemoveAt(i);
            grid[markedCell.x][markedCell.y] = CellState.Free;
        }
        
        // Fill in cells that need to be filled
        foreach (HashSet<Vector2Int> fillArea in areasToFill)
        {
            foreach (Vector2Int cell in fillArea)
            {
                grid[cell.x][cell.y] = CellState.Taken;
            }
        }
    }

    private void OnValidate()
    {
        InitializeGrid();
        renderController.UpdateGrid(grid);
    }

    private List<HashSet<Vector2Int>> DetermineFillAreas()
    {
        if (markedPath.Count <= 0) return null;

        List<HashSet<Vector2Int>> potentialFillAreas = new List<HashSet<Vector2Int>>();

        bool SideOfMarkedNodeIsRedundant(Vector2Int startNode)
        {
            bool alreadyChecked = false;
            
            for (int j = 0; j < potentialFillAreas.Count; j++)
            {
                if (potentialFillAreas[j].Contains(startNode))
                {
                    return true;
                }
            }

            return false;
        }
        
        for (int i = 0; i < markedPath.Count; i++)
        {
            //Test Down
            Vector2Int startNode = markedPath[i] + Vector2Int.down;
            if (!SideOfMarkedNodeIsRedundant(startNode))
            {
                potentialFillAreas.Add(GenerateFillList(startNode));
            }
            //Test Up
            startNode = markedPath[i] + Vector2Int.up;
            if (!SideOfMarkedNodeIsRedundant(startNode))
            {
                potentialFillAreas.Add(GenerateFillList(startNode));
            }
            //Test Left
            startNode = markedPath[i] + Vector2Int.left;
            if (!SideOfMarkedNodeIsRedundant(startNode))
            {
                potentialFillAreas.Add(GenerateFillList(startNode));
            }
            //Test Right
            startNode = markedPath[i] + Vector2Int.right;
            if (!SideOfMarkedNodeIsRedundant(startNode))
            {
                potentialFillAreas.Add(GenerateFillList(startNode));
            }
        }

        int ScoreFillArea(HashSet<Vector2Int> fillArea)
        {
            int score = 0;

            foreach (Vector2Int cell in fillArea)
            {
                if (grid[cell.x][cell.y] == CellState.Enemy)
                {
                    return 0;
                }
            }

            return fillArea.Count;
        }

        List<HashSet<Vector2Int>> validFillAreas = new List<HashSet<Vector2Int>>();
        int lowestScore = 9999;
        
        foreach (HashSet<Vector2Int> fillArea in potentialFillAreas)
        {
            int score = ScoreFillArea(fillArea);
            //temporary hack to only fill the smaller area, since no enemies yet
            if (score > 0 && score < lowestScore)
            {
                validFillAreas.Clear();
                validFillAreas.Add(fillArea);
                lowestScore = score;
            }
        }

        return validFillAreas;
    }

    private HashSet<Vector2Int> GenerateFillList(Vector2Int startNode)
    {
        HashSet<Vector2Int> cellList = new HashSet<Vector2Int>();
        
        if (cellList.Contains(startNode)) return cellList;

        Stack<Vector2Int> nodes = new Stack<Vector2Int>();
        nodes.Push(startNode);

        while (nodes.Count > 0)
        {
            Vector2Int node = nodes.Pop();
            
            // TODO: add boundary checking
            
            CellState state = grid[node.x][node.y];
            
            if (state == CellState.Free || state == CellState.Enemy)
            {
                cellList.Add(node);

                Vector2Int next = node + Vector2Int.down;
                if (!cellList.Contains(next))
                {
                    nodes.Push(next);
                }

                next = node + Vector2Int.up;
                if (!cellList.Contains(next))
                {
                    nodes.Push(next);
                }
                
                next = node + Vector2Int.left;
                if (!cellList.Contains(next))
                {
                    nodes.Push(next);
                }
                
                next = node + Vector2Int.right;
                if (!cellList.Contains(next))
                {
                    nodes.Push(next);
                }
            }
        }
        
        return cellList;
    }
}
