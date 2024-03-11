using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class GameplayController : MonoBehaviour
{
    [SerializeField] private bool oneDimensionalMovement;
    [SerializeField] private int initialEnemyCount = 3;
    [SerializeField] private float playerTickLength = 0.064f;
    [SerializeField] private float enemyTickLength = 0.5f;
    [SerializeField] private float gridTickLength = 0.032f;
    [SerializeField] private float playerVisualPosLerpSpeed = 10.0f;
    [SerializeField] private Vector2Int playerInitialPosition;
    [SerializeField] private Vector2Int bossInitialPosition;
    [SerializeField] private GameRenderController renderController;

    private Vector2Int playerGridPosition = new Vector2Int(0, 0);
    private Vector2 playerVisualPosition = new Vector2();

    private Vector2Int bossGridPosition = new Vector2Int(31, 31);
    private Vector2 bossVisualPosition = new Vector2();
    
    private Vector2 currentInputVector = Vector2.zero;
    private Vector2 scheduledInputVector = Vector2.zero;

    private bool gridChanged = false;
    private bool lastWasUp = false;
    private bool playerSafe = true;
    private int lives = 3;
    private float playerTickCounter = 0.0f;
    private float enemyTickCounter = 0.0f;
    private float gridTickCounter = 0.0f;
    private CellState[][] grid;
    
    private List<Vector2Int> markedPath = new List<Vector2Int>();
    private List<Vector2Int> enemyPositions = new List<Vector2Int>();

    private Vector2Int[] moveDirections = new Vector2Int[]
    {
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(-1, 0),
        new Vector2Int(0, -1)
    };

    private void Start()
    {
        InitializeGrid();
        renderController.Initialize();
        playerGridPosition = playerInitialPosition;
        bossGridPosition = bossInitialPosition;
        playerVisualPosition = (Vector2)playerInitialPosition;
        bossVisualPosition = (Vector2)bossInitialPosition;
        renderController.UpdatePlayerPosition(playerVisualPosition);
        renderController.UpdateBossPosition(bossVisualPosition);
        UpdateRenderGrid();
    }

    private void FixedUpdate()
    {
        ReadInput();
    }

    private void Update()
    {
        if (playerTickCounter < playerTickLength)
        {
            playerTickCounter += Time.deltaTime;
        }
        else
        {
            playerTickCounter = 0.0f;
            ExecuteInput();
        }
        
        if (enemyTickCounter < enemyTickLength)
        {
            enemyTickCounter += Time.deltaTime;
        }
        else
        {
            enemyTickCounter = 0.0f;
            MoveEnemies();
        }
        
        if (playerVisualPosition != playerGridPosition)
        {
            playerVisualPosition = Vector2.Lerp(playerVisualPosition, playerGridPosition, playerVisualPosLerpSpeed*Time.deltaTime);
            renderController.UpdatePlayerPosition(playerVisualPosition);
        }

        if (bossVisualPosition != bossGridPosition)
        {
            bossVisualPosition = Vector2.Lerp(bossVisualPosition, bossGridPosition, playerVisualPosLerpSpeed*Time.deltaTime);
            renderController.UpdateBossPosition(bossVisualPosition);
        }

        if (gridTickCounter < gridTickLength)
        {
            gridTickCounter += Time.deltaTime;
        }
        else
        {
            gridTickCounter = 0.0f;
            
            if (gridChanged)
            {
                UpdateRenderGrid();
                gridChanged = false;
            }
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

        enemyPositions = new List<Vector2Int>();
        
        for (int i = 0; i < initialEnemyCount; i++)
        {
            Vector2Int newPos;
            
            do
            {
                newPos =
                    new Vector2Int(Random.Range(1, Constants.GRID_SIZE), Random.Range(1, Constants.GRID_SIZE));
            } while (enemyPositions.Contains(newPos));

            grid[newPos.x][newPos.y] = CellState.Enemy;
            enemyPositions.Add(newPos);
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

            if (absX > absY)
            {
                lastWasUp = false;
                yInput = 0.0f;
            }
            else if (absY > absX)
            {
                lastWasUp = true;
                xInput = 0.0f;
            }
            else if (absX > 0.0f && absY > 0.0f)
            {
                if (lastWasUp)
                {
                    lastWasUp = false;
                    yInput = 0.0f;
                }
                else
                {
                    xInput = 0.0f;
                }
            }
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
            currentInputVector = Vector2Int.zero;
            scheduledInputVector = Vector2.zero;
            UpdatePlayerGridPosition(delta);
        }
    }

    private void UpdatePlayerGridPosition(Vector2Int delta)
    {
        // Test movement target and update position if valid
        
        Vector2Int targetPosition = playerGridPosition + delta;
        
        if (targetPosition.x < 0 
            || targetPosition.x > Constants.GRID_SIZE-1
            || targetPosition.y < 0
            || targetPosition.y > Constants.GRID_SIZE-1)
        {
            return;
        }
        
        if (!IsCellWalkableToPlayer(targetPosition)) return;
        
        playerGridPosition += delta;
        
        // Handle game state consequences
        CellState targetState = grid[playerGridPosition.x][playerGridPosition.y];
        
        if (targetState == CellState.Free)
        {
            playerSafe = false;
            grid[playerGridPosition.x][playerGridPosition.y] = CellState.Marked;
            markedPath.Add(playerGridPosition);
        }
        else if (targetState == CellState.Taken)
        {
            playerSafe = true;
            
            if (markedPath.Count > 0)
            {
                HandlePlayerFinishedMark();
            }
        }
        else if (targetState == CellState.Enemy)
        {
            HandlePlayerHit();
            playerSafe = true;
        }

        gridChanged = true;
    }

    private void HandlePlayerFinishedMark()
    {
        // Determine if any cells need to be filled
        bool fillSuccess = false;
        List<HashSet<Vector2Int>> areasToFill = DetermineFillAreas(out fillSuccess);
        
        DeleteMarkedPath(fillSuccess);
        
        // Fill in cells that need to be filled
        foreach (HashSet<Vector2Int> fillArea in areasToFill)
        {
            foreach (Vector2Int cell in fillArea)
            {
                CellState prefillState = grid[cell.x][cell.y];

                if (prefillState == CellState.Enemy)
                {
                    enemyPositions.Remove(cell);
                }

                grid[cell.x][cell.y] = CellState.Taken;
            }
        }
    }

    private void DeleteMarkedPath(bool fill)
    {
        // Remove marked cells one by one
        for (int i = markedPath.Count - 1; i >= 0; i--)
        {
            Vector2Int markedCell = markedPath[i];
            markedPath.RemoveAt(i);
            grid[markedCell.x][markedCell.y] = fill ? CellState.Taken : CellState.Free;
        }
    }

    private void HandlePlayerHit()
    {
        ReturnPlayerToEdge();
        lives--;
        DeleteMarkedPath(false);
    }

    private void ReturnPlayerToEdge()
    {
        int closestDist = Constants.GRID_SIZE;
        int currentDist = 0;
        Vector2Int closestEdge = playerInitialPosition;
        
        //Right
        currentDist = 0;
        for (int x = playerGridPosition.x + 1; x < Constants.GRID_SIZE; x++)
        {
            currentDist++;
            if (currentDist > closestDist) break;
            if (grid[x][playerGridPosition.y] == CellState.Taken)
            {
                closestDist = x - playerGridPosition.x;
                closestEdge = new Vector2Int(x, playerGridPosition.y);
                break;
            }
        }
        
        //Left
        currentDist = 0;
        for (int x = playerGridPosition.x-1; x >= 0 ; x--)
        {
            currentDist++;
            if (currentDist > closestDist) break;
            if (grid[x][playerGridPosition.y] == CellState.Taken)
            {
                if (playerGridPosition.x - x < closestDist)
                {
                    closestDist = playerGridPosition.x - x;
                    closestEdge = new Vector2Int(x, playerGridPosition.y);
                }
            }
        }
        
        //Up
        currentDist = 0;
        for (int y = playerGridPosition.y + 1; y < Constants.GRID_SIZE; y++)
        {
            currentDist++;
            if (currentDist > closestDist) break;
            if (grid[playerGridPosition.x][y] == CellState.Taken)
            {
                if (y - playerGridPosition.y < closestDist)
                {
                    closestDist = y - playerGridPosition.y;
                    closestEdge = new Vector2Int(playerGridPosition.x, y);
                }
                
                break;
            }
        }
        
        //Down
        currentDist = 0;
        for (int y = playerGridPosition.y-1; y >= 0 ; y--)
        {
            currentDist++;
            if (currentDist > closestDist) break;
            if (grid[playerGridPosition.x][y] == CellState.Taken)
            {
                if (playerGridPosition.y - y < closestDist)
                {
                    closestDist = playerGridPosition.y - y;
                    closestEdge = new Vector2Int(playerGridPosition.x, y);
                }
            }
        }
        
        playerGridPosition = closestEdge;
    }

    private void MoveEnemies()
    {
        bool willMove;
        Vector2Int delta = Vector2Int.zero;
        Vector2Int targetCell = Vector2Int.zero;
        CellState targetState = CellState.None;
        
        // Move Boss
        if (Random.Range(0, 9) > 0)
        {
            delta = moveDirections[Random.Range(0, moveDirections.Length)];
            targetCell = bossGridPosition + delta;
            targetState = grid[targetCell.x][targetCell.y];

            willMove = targetState is CellState.Free or CellState.Marked;
            
            if (willMove)
            {
                if (targetState == CellState.Marked || targetCell == playerGridPosition)
                {
                    HandlePlayerHit();
                }
                
                bossGridPosition = targetCell;
            }
            else if (targetState == CellState.Taken)
            {
                if (targetCell.x == 0)
                {
                    targetCell = new Vector2Int(Constants.GRID_SIZE - 2, bossGridPosition.y);
                    targetState = grid[targetCell.x][targetCell.y];
                    if (targetState == CellState.Free)
                    {
                        bossGridPosition = targetCell;
                    }
                }
            }
        }
        
        // Move Small Enemies
        for (int i = 0; i < enemyPositions.Count; i++)
        {
            // Move to Player
            if (Random.Range(0, 9) > (playerSafe ? 9 : 1))
            {
                int xDist = Mathf.Abs(enemyPositions[i].x - playerGridPosition.x);
                int yDist = Mathf.Abs(enemyPositions[i].y - playerGridPosition.y);
                
                if (Random.Range(0, 9) > 4 && xDist > 0)
                {
                    delta = Vector2Int.right * (enemyPositions[i].x < playerGridPosition.x ? 1 : -1);
                }
                else if (yDist > 0)
                {
                    delta = Vector2Int.up * (enemyPositions[i].y < playerGridPosition.y ? 1 : -1);
                }
            }
            // Move Randomly
            else if (Random.Range(0, 9) > 5)
            {
                delta = moveDirections[Random.Range(0, moveDirections.Length)];
            }
            // Do not move
            else continue;
            
            targetCell = enemyPositions[i] + delta;

            targetState = grid[targetCell.x][targetCell.y];
            willMove = targetState is CellState.Free or CellState.Marked;

            if (willMove)
            {
                if (targetState == CellState.Marked || targetCell == playerGridPosition)
                {
                    HandlePlayerHit();
                }
                
                grid[enemyPositions[i].x][enemyPositions[i].y] = CellState.Free;
                grid[targetCell.x][targetCell.y] = CellState.Enemy;
                enemyPositions[i] = targetCell;

                gridChanged = true;
            }
        }
    }

    private void UpdateRenderGrid()
    {
        renderController.UpdateGrid(grid);
    }

    private void OnValidate()
    {
        InitializeGrid();
        renderController.UpdateGrid(grid);
    }

    private bool IsCellWalkableToPlayer(Vector2Int cellCoord)
    {
        CellState cellState = grid[cellCoord.x][cellCoord.y];
        if (cellState == CellState.Free) return true;
        if (cellState == CellState.Marked) return false;

        if (cellState == CellState.Taken)
        {
            if (grid[playerGridPosition.x][playerGridPosition.y] == CellState.Marked
                || cellCoord.x == 0 || cellCoord.y == 0
                || cellCoord.x == Constants.GRID_SIZE - 1 || cellCoord.y == Constants.GRID_SIZE - 1
                || grid[cellCoord.x + 1][cellCoord.y] == CellState.Free
                || grid[cellCoord.x][cellCoord.y + 1] == CellState.Free
                || grid[cellCoord.x - 1][cellCoord.y] == CellState.Free
                || grid[cellCoord.x][cellCoord.y - 1] == CellState.Free)
                return true;
        }

        return false;
    }

    #region Fill
    
    private List<HashSet<Vector2Int>> DetermineFillAreas(out bool fillSuccess)
    {
        fillSuccess = false;
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
            foreach (Vector2Int cell in fillArea)
            {
                if (cell == bossGridPosition)
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
                fillSuccess = true;
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
    
    #endregion
    
}
