using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Vector2Int = UnityEngine.Vector2Int;

public class GameRenderController : MonoBehaviour
{
    [SerializeField] private bool cloneMaterial = true;
    [SerializeField] private RawImage gameplayImage;
    [SerializeField] private RenderTexture gameplayTexture;
    [SerializeField] private RenderTexture valueRamp;
    [SerializeField] private Texture2D valueRampBase;

    private float defVectorZ = 0.005f;

    private struct CellInfo
    {
        public float value;
        public Vector2Int coord;

        public CellInfo(float value, Vector2Int coord)
        {
            this.value = value;
            this.coord = coord;
        }
    }

    public void Initialize()
    {
        if (cloneMaterial)
        {
            gameplayImage.material = new Material(gameplayImage.material);
        }
        
        InitializeTextures();

        Application.targetFrameRate = 60;
    }

    public void UpdatePlayerPosition(Vector2 newPos)
    {
        CommandBuffer buffer = new CommandBuffer();
        buffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        buffer.SetGlobalVector("_PlayerPosition", new Vector4((newPos.x + 0.5f) / Global.GRID_SIZE, (newPos.y + 0.5f) / Global.GRID_SIZE, defVectorZ, 0.0f));
        Graphics.ExecuteCommandBufferAsync(buffer, ComputeQueueType.Default);
    }
    
    public void UpdateBossPosition(Vector2 newPos)
    {
        CommandBuffer buffer = new CommandBuffer();
        buffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        buffer.SetGlobalVector("_BossPosition", new Vector4((newPos.x + 0.5f) / Global.GRID_SIZE, (newPos.y + 0.5f) / Global.GRID_SIZE, defVectorZ, 1.0f));
        Graphics.ExecuteCommandBufferAsync(buffer, ComputeQueueType.Default);
    }
    
    public void UpdateFillPercent(float newFillPercent)
    {
        CommandBuffer buffer = new CommandBuffer();
        buffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        buffer.SetGlobalFloat("_FillPercent", newFillPercent);
        Graphics.ExecuteCommandBufferAsync(buffer, ComputeQueueType.Urgent);
    }

    public void UpdateMarkStrength(float newMarkStrength)
    {
        CommandBuffer buffer = new CommandBuffer();
        buffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        buffer.SetGlobalFloat("_MarkStrength", newMarkStrength);
        Graphics.ExecuteCommandBufferAsync(buffer, ComputeQueueType.Default);
    }
    
    public void UpdatePlayerHealth(float newPlayerHealth)
    {
        CommandBuffer buffer = new CommandBuffer();
        buffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);
        buffer.SetGlobalFloat("_PlayerHealth", newPlayerHealth);
        Graphics.ExecuteCommandBufferAsync(buffer, ComputeQueueType.Default);
    }

    public void UpdateGrid(CellState[][] newGrid)
    {
        if (gameplayTexture == null)
        {
            InitializeTextures();
        }

        CommandBuffer buffer = new CommandBuffer();
        buffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

        int contiguousCount = 0;
        float lastValue = -1.0f;
        Vector2Int lastChangedCoord = Vector2Int.left;
        
        for (int x = 0; x < Global.GRID_SIZE; x++)
        {
            for (int y = 0; y < Global.GRID_SIZE; y++)
            {
                CellState state = newGrid[x][y];
                float value = (int)state * 0.01f;

                if (y == 0 || value != lastValue)
                {
                    if (y != 0)
                    {
                        UpdateVerticalContiguous(lastChangedCoord.x, lastChangedCoord.y, contiguousCount, lastValue, ref buffer);
                    }

                    lastValue = value;
                    lastChangedCoord = new Vector2Int(x, y);
                    contiguousCount = 1;
                }
                else
                {
                    contiguousCount++;
                }
            }
        }

        Graphics.ExecuteCommandBufferAsync(buffer, ComputeQueueType.Default);
    }

    public IEnumerator UpdateGridIncremental(CellState[][] newGrid)
    {
        int cellAmount = Global.GRID_SIZE * Global.GRID_SIZE;
        
        if (gameplayTexture == null)
        {
            InitializeTextures();
        }

        List<CellInfo> cells = new List<CellInfo>(cellAmount);
        
        for (int x = 0; x < Global.GRID_SIZE; x++)
        {
            for (int y = 0; y < Global.GRID_SIZE; y++)
            {
                CellState state = newGrid[y][x];
                float value = (int)state * 0.01f;

                cells.Add(new CellInfo(value, new Vector2Int(y, x)));
            }
        }

        yield return null;
        cells.Shuffle();
        yield return null;
        
        int waitCounter = 0;
        int threshold = Random.Range(20, 25);
        List<CellInfo> group = new List<CellInfo>(25);
        
        foreach (CellInfo cell in cells)
        {
            group.Add(cell);
            waitCounter++;

            if (waitCounter >= threshold)
            {
                UpdateCellList(group);
                group.Clear();
                waitCounter = 0;
                threshold = Random.Range(20, 25);
                yield return null;
            }
        }

        if (group.Count > 0)
        {
            yield return null;
            UpdateCellList(group);
        }

        yield return null;
    }

    public IEnumerator SetWholeGridIncremental(float value)
    {
        int cellAmount = Global.GRID_SIZE * Global.GRID_SIZE;
        List<CellInfo> cells = new List<CellInfo>(cellAmount);
        
        for (int x = 0; x < Global.GRID_SIZE; x++)
        {
            for (int y = 0; y < Global.GRID_SIZE; y++)
            {
                cells.Add(new CellInfo(value, new Vector2Int(y, x)));
            }
        }

        yield return null;
        cells.Shuffle();
        yield return null;
        
        int waitCounter = 0;
        int threshold = Random.Range(20, 25);
        List<CellInfo> group = new List<CellInfo>(25);
        
        foreach (CellInfo cell in cells)
        {
            group.Add(cell);
            waitCounter++;

            if (waitCounter >= threshold)
            {
                UpdateCellList(group);
                group.Clear();
                waitCounter = 0;
                threshold = Random.Range(20, 25);
                yield return null;
            }
        }

        if (group.Count > 0)
        {
            yield return null;
            UpdateCellList(group);
        }

        yield return null;
    }

    public IEnumerator UpdateFillIncremental(List<Vector2Int> newTaken, List<Vector2Int> newEdges)
    {
        newTaken.Shuffle();

        int waitCounter = 0;
        int threshold = Random.Range(12, 15);
        float value = 1.0f;
        List<CellInfo> cells = new List<CellInfo>(15);

        foreach (Vector2Int coord in newTaken)
        {
            cells.Add(new CellInfo(value, coord));
            waitCounter++;

            if (waitCounter >= threshold)
            {
                UpdateCellList(cells);
                cells.Clear();

                waitCounter = 0;
                threshold = Random.Range(12, 15);

                yield return null;
            }
        }

        if (cells.Count > 0)
        {
            UpdateCellList(cells);
            cells.Clear();
        }

        value = 0.87f;
        threshold = 8;
        yield return null;
        
        foreach (Vector2Int coord in newEdges)
        {
            cells.Add(new CellInfo(value, coord));
            waitCounter++;

            if (waitCounter >= threshold)
            {
                UpdateCellList(cells);
                cells.Clear();

                waitCounter = 0;
                yield return null;
            }
        }

        if (cells.Count > 0)
        {
            UpdateCellList(cells);
            cells.Clear();
        }
    }

    private void InitializeTextures()
    {
        Graphics.Blit(valueRampBase, valueRamp);

        SetWholeGrid(0.0f);

        gameplayImage.material.SetTexture("_GameStateMap", gameplayTexture);
    }

    private void SetWholeGrid(float value)
    {
        CommandBuffer buffer = new CommandBuffer();
        buffer.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

        for (int x = 0; x < gameplayTexture.width; x++)
        {
            UpdateVerticalContiguous(x, 0, Global.GRID_SIZE, value, ref buffer);
        }

        Graphics.ExecuteCommandBufferAsync(buffer, ComputeQueueType.Default);
    }

    private void UpdateVerticalContiguous(int x, int y, int height, float value, ref CommandBuffer buffer)
    {
        buffer.CopyTexture(valueRamp, 0, 0, (int)((Global.GRID_SIZE - 1) * value), 0, 1, height,
                gameplayTexture, 0, 0, x, y);
    }

    private void UpdateCellList(List<CellInfo> cells)
    {
        if (cells == null || cells.Count == 0) return;

        foreach (CellInfo cell in cells)
        {
            Graphics.CopyTexture(valueRamp, 0, 0, (int)((valueRamp.width - 1) * cell.value), 0, 1, 1,
                gameplayTexture, 0, 0, cell.coord.x, cell.coord.y);
        }
    }

    public void OnValidate()
    {
        InitializeTextures();
    }
}
