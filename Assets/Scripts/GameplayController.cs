using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

public class GameplayController : MonoBehaviour
{
    [SerializeField] private int initialEnemyCount = 3;
    [SerializeField] private int maxMarkLength = 64;
    [SerializeField] private float playerTickLength = 0.064f;
    [SerializeField] private float enemyTickLength = 0.5f;
    [SerializeField] private float gridTickLength = 0.032f;
    [SerializeField] private float playerVisualPosLerpSpeed = 10.0f;
    [SerializeField] private float bossVisualPosLerpSpeed = 5.0f;
    [SerializeField] private float gridVisualLerpSpeed = 7.5f;
    [SerializeField] private Vector2Int playerInitialPosition;
    [SerializeField] private Vector2Int bossInitialPosition;
    [SerializeField] private GameRenderController renderController;

    private Vector2Int playerGridPosition = new Vector2Int(0, 0);
    private Vector2 playerVisualPosition = new Vector2();

    private Vector2Int bossGridPosition = new Vector2Int(31, 31);
    private Vector2 bossVisualPosition = new Vector2();
    
    private Vector2Int lastMovementVector = Vector2Int.zero;
    private Vector2 currentInputVector = Vector2.zero;
    private Vector2 scheduledInputVector = Vector2.zero;

    private bool gridChanged = false;
    private bool playerSafe = true;
    private bool actionButtonHeld = false;
    private int lives = 3;
    private int filled = 0;
    private float fillPercent = 0.0f;
    private float visualFillPercent = 0.0f;
    private float markStrength = 1.0f;
    private float visualMarkStrength = 1.0f;
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

    private Vector2Int[] testDirections = new Vector2Int[]
    {
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(-1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(1, 1),
        new Vector2Int(-1, -1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, 1)
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
            playerVisualPosition = Vector2.Lerp(playerVisualPosition, playerGridPosition, playerVisualPosLerpSpeed * Time.deltaTime);
            renderController.UpdatePlayerPosition(playerVisualPosition);
        }

        if (bossVisualPosition != bossGridPosition)
        {
            bossVisualPosition = Vector2.Lerp(bossVisualPosition, bossGridPosition, bossVisualPosLerpSpeed * Time.deltaTime);
            renderController.UpdateBossPosition(bossVisualPosition);
        }

        if (Math.Abs(visualFillPercent - fillPercent) > 0.005f)
        {
            visualFillPercent = Mathf.Lerp(visualFillPercent, fillPercent, gridVisualLerpSpeed * Time.deltaTime);
            renderController.UpdateFillPercent(visualFillPercent);
        }
        
        if (Math.Abs(visualMarkStrength - markStrength) > 0.005f)
        {
            visualMarkStrength = Mathf.Lerp(visualMarkStrength, markStrength, gridVisualLerpSpeed * Time.deltaTime);
            renderController.UpdateMarkStrength(visualMarkStrength);
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
        filled = 0;
        grid = new CellState[Constants.GRID_SIZE][];

        for (int x = 0; x < Constants.GRID_SIZE; x++)
        {
            grid[x] = new CellState[Constants.GRID_SIZE];
            
            for (int y = 0; y < Constants.GRID_SIZE; y++)
            {
                if (x == 0 || y == 0 || x == Constants.GRID_SIZE-1 || y == Constants.GRID_SIZE-1)
                {
                    grid[x][y] = CellState.Taken;
                    filled++;
                }
                else
                {
                    grid[x][y] = CellState.Free;
                }
            }
        }

        UpdateFillPercent();

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
        actionButtonHeld = Input.GetAxis("Fire1") > 0.0f;
        float xInput = Input.GetAxis("Horizontal");
        float yInput = Input.GetAxis("Vertical");

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
            
            if (delta.x != 0 && delta.y != 0)
            {
                Vector2Int h = new Vector2Int(delta.x, 0);
                Vector2Int v = new Vector2Int(0, delta.y);

                if (lastMovementVector.x == 0)
                {
                    if (!TryUpdatePlayerGridPosition(h))
                    {
                        TryUpdatePlayerGridPosition(v);
                    }
                }
                else
                {
                    if (!TryUpdatePlayerGridPosition(v))
                    {
                        TryUpdatePlayerGridPosition(h);
                    }
                }
            }
            else
            {
                if (!TryUpdatePlayerGridPosition(delta) && IsPlayerStuck())
                {
                    DeleteMarkedPath(false);
                    ReturnPlayerToEdge();
                }
            }
        }
    }

    private bool TryUpdatePlayerGridPosition(Vector2Int delta)
    {
        // Test movement target and update position if valid
        
        Vector2Int targetPosition = playerGridPosition + delta;
        
        if (targetPosition.x < 0 
            || targetPosition.x > Constants.GRID_SIZE-1
            || targetPosition.y < 0
            || targetPosition.y > Constants.GRID_SIZE-1)
        {
            return false;
        }
        
        if (!IsCellWalkableToPlayer(targetPosition)) return false;
        
        playerGridPosition += delta;
        
        // Handle game state consequences
        CellState targetState = grid[playerGridPosition.x][playerGridPosition.y];
        
        if (targetState == CellState.Free)
        {
            playerSafe = false;
            grid[playerGridPosition.x][playerGridPosition.y] = CellState.Marked;
            markedPath.Add(playerGridPosition);
            UpdateMarkStrength();

            if (markedPath.Count >= maxMarkLength)
            {
                DeleteMarkedPath(false);
                ReturnPlayerToEdge();
            }
            else if (markedPath.Count > 2)
            {
                AdjacencyCheck();
            }
        }
        else if (targetState >= CellState.Edge)
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
        lastMovementVector = delta;
        return true;
    }

    // Check if player's mark is touching a taken cell and fill immediately
    // To avoid leaving random holes
    private void AdjacencyCheck()
    {
        Vector2Int current;
        
        for (int i = 0; i < testDirections.Length; i++)
        {
            current = playerGridPosition + testDirections[i];
            if (current.x < 0 || current.x >= Constants.GRID_SIZE
                              || current.y < 0 || current.y >= Constants.GRID_SIZE)
                continue;
            
            if (grid[current.x][current.y] >= CellState.Edge)
            {
                HandlePlayerFinishedMark();
                ReturnPlayerToEdge();
                return;
            }
        }
    }

    private void HandlePlayerFinishedMark()
    {
        // Determine if any cells need to be filled
        bool fillSuccess;
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
                filled++;
            }
        }
        
        UpdateFillPercent();
    }

    private void DeleteMarkedPath(bool fill)
    {
        // Remove marked cells one by one
        for (int i = markedPath.Count - 1; i >= 0; i--)
        {
            Vector2Int markedCell = markedPath[i];
            markedPath.RemoveAt(i);
            grid[markedCell.x][markedCell.y] = fill ? CellState.Edge : CellState.Free;
            
            if (fill)
            {
                filled++;
            }
        }
        
        UpdateMarkStrength();
    }

    private void HandlePlayerHit()
    {
        ReturnPlayerToEdge();
        lives--;
        DeleteMarkedPath(false);

        if (lives <= 0)
        {
            SceneManager.LoadScene(0);
        }
    }

    private void ReturnPlayerToEdge()
    {
        int closestDist = Constants.GRID_SIZE;
        int currentDist = 0;
        Vector2Int closestEdge = playerInitialPosition;
        Vector2Int current;
        
        //Right
        currentDist = 0;
        for (int x = playerGridPosition.x + 1; x < Constants.GRID_SIZE; x++)
        {
            currentDist++;
            if (currentDist > closestDist) break;
            current = new Vector2Int(x, playerGridPosition.y);
            
            if (grid[current.x][current.y] >= CellState.Edge
                && IsCellWalkableToPlayer(current))
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
            current = new Vector2Int(x, playerGridPosition.y);
            
            if (grid[current.x][current.y] >= CellState.Edge
                && IsCellWalkableToPlayer(current))
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
            current = new Vector2Int(playerGridPosition.x, y);
            
            if (grid[current.x][current.y] >= CellState.Edge
                && IsCellWalkableToPlayer(current))
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
            current = new Vector2Int(playerGridPosition.x, y);
            
            if (grid[current.x][current.y] >= CellState.Edge
                && IsCellWalkableToPlayer(current))
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

    private bool IsPlayerStuck()
    {
        Vector2Int current;
        
        for (int i = 0; i < testDirections.Length; i++)
        {
            current = playerGridPosition + testDirections[i];
            if (current.x < 0 || current.x >= Constants.GRID_SIZE
                || current.y < 0 || current.y >= Constants.GRID_SIZE)
                continue;
            
            if (grid[current.x][current.y] >= CellState.Edge
                && IsCellWalkableToPlayer(current))
            {
                return false;
            }
        }

        return true;
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
        }
        
        // Move Small Enemies
        for (int i = 0; i < enemyPositions.Count; i++)
        {
            // Move to Player
            if (fillPercent > (playerSafe ? Random.Range(4.0f, 10.0f)*0.1 : (Random.Range(0.0f, 10.0f)-markedPath.Count*0.4)*0.1))
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
            
            if (targetCell.x < 0 || targetCell.x >= Constants.GRID_SIZE
                || targetCell.y < 0 || targetCell.y >= Constants.GRID_SIZE)
                continue;
            
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

    private void UpdateFillPercent()
    {
        fillPercent = filled / (float)(Constants.GRID_SIZE*Constants.GRID_SIZE)*0.75f;
    }

    private void UpdateMarkStrength()
    {
        markStrength = 1.0f - ((float)markedPath.Count/maxMarkLength);
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
        if (cellState == CellState.Edge) return true;
        if (cellState == CellState.Marked) return false;
        if (cellState == CellState.Free && actionButtonHeld) return true;
        
        if (cellState == CellState.Taken)
        {
            if (cellCoord.x >= 0 && cellCoord.y >= 0 && cellCoord.x < Constants.GRID_SIZE && cellCoord.y < Constants.GRID_SIZE)
            {
                Vector2Int testDir;
                
                for (int i = 0; i < testDirections.Length; i++)
                {
                    testDir = cellCoord + testDirections[i];

                    if (testDir.x >= 0 && testDir.x < Constants.GRID_SIZE
                                       && testDir.y >= 0 && testDir.y < Constants.GRID_SIZE)
                    {
                        if (grid[testDir.x][testDir.y] == CellState.Free)
                        {
                            return true;
                        }
                    }
                }
            }
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
            
            if (score > 16 && score < 1024 && score < lowestScore)
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
            
            if (node. x < 0 || node.x >= Constants.GRID_SIZE
                || node. y < 0 || node.y >= Constants.GRID_SIZE) continue;
            
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
