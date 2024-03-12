using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Vector2Int = UnityEngine.Vector2Int;

public class GameRenderController : MonoBehaviour
{
    [SerializeField] private bool cloneMaterial = true;
    [SerializeField] private RawImage gameplayImage;
    [SerializeField] private Texture2D gameplayTextureBase;

    private Texture2D gameplayTexture;
    private float defVectorZ = 0.005f;

    public void Initialize()
    {
        if (cloneMaterial)
        {
            gameplayImage.material = new Material(gameplayImage.material);
        }
        
        SetTextureFromBase();
    }

    public void UpdatePlayerPosition(Vector2 newPos)
    {
        gameplayImage.material.SetVector("_PlayerPosition", new Vector4((newPos.x+0.5f)/Global.GRID_SIZE, (newPos.y+0.5f)/Global.GRID_SIZE, defVectorZ));
    }
    
    public void UpdateBossPosition(Vector2 newPos)
    {
        gameplayImage.material.SetVector("_BossPosition", new Vector4((newPos.x+0.5f)/Global.GRID_SIZE, (newPos.y+0.5f)/Global.GRID_SIZE, defVectorZ));
    }
    
    public void UpdateFillPercent(float newFillPercent)
    {
        gameplayImage.material.SetFloat("_FillPercent", newFillPercent);
    }

    public void UpdateMarkStrength(float newMarkStrength)
    {
        gameplayImage.material.SetFloat("_MarkStrength", newMarkStrength);
    }
    
    public void UpdatePlayerHealth(float newPlayerHealth)
    {
        gameplayImage.material.SetFloat("_PlayerHealth", newPlayerHealth);
    }

    public void UpdateGrid(CellState[][] newGrid)
    {
        if (gameplayTexture == null)
        {
            SetTextureFromBase();
        }

        Color col = new Color();
        Color[] newFloatGrid = new Color[Global.GRID_SIZE*Global.GRID_SIZE];

        int index = 0;
        
        for (int x = 0; x < Global.GRID_SIZE; x++)
        {
            for (int y = 0; y < Global.GRID_SIZE; y++)
            {
                CellState state = newGrid[y][x];
                float value = (float)(int)state;
                col.a = value * 0.01f;
                newFloatGrid[index] = col;

                index++;
            }
        }
        
        gameplayTexture.SetPixels(newFloatGrid);
        gameplayTexture.Apply();
    }

    public IEnumerator UpdateGridFancy(CellState[][] newGrid)
    {
        int cellAmount = Global.GRID_SIZE * Global.GRID_SIZE;
        
        if (gameplayTexture == null)
        {
            SetTextureFromBase();
        }
        
        List<Vector3Int> pixelCoords = new List<Vector3Int>(cellAmount);
        
        Color col = new Color();
        Color[] newFloatGrid = new Color[cellAmount];

        int index = 0;
        
        for (int x = 0; x < Global.GRID_SIZE; x++)
        {
            for (int y = 0; y < Global.GRID_SIZE; y++)
            {
                CellState state = newGrid[y][x];
                float value = (float)(int)state;
                col.a = value * 0.01f;
                newFloatGrid[index] = col;
                
                pixelCoords.Add(new Vector3Int(y, x, index));
                
                index++;
            }
        }

        yield return null;
        pixelCoords.Shuffle();
        yield return null;
        
        int waitCounter = 0;
        int threshold = Random.Range(7, 10);
        
        foreach (Vector3Int coord in pixelCoords)
        {
            gameplayTexture.SetPixel(coord.x, coord.y, newFloatGrid[coord.z]);
            gameplayTexture.Apply();
            waitCounter++;

            if (waitCounter > threshold)
            {
                waitCounter = 0;
                threshold = Random.Range(7, 10);
                yield return null;
            }
        }
    }

    public IEnumerator DissolveToColor(Color targetColor)
    {
        int cellAmount = Global.GRID_SIZE * Global.GRID_SIZE;
        List<Vector3Int> pixelCoords = new List<Vector3Int>(cellAmount);

        int index = 0;
        
        for (int x = 0; x < Global.GRID_SIZE; x++)
        {
            for (int y = 0; y < Global.GRID_SIZE; y++)
            {
                pixelCoords.Add(new Vector3Int(y, x, index));
                index++;
            }
        }

        yield return null;
        pixelCoords.Shuffle();
        yield return null;
        
        int waitCounter = 0;
        int threshold = Random.Range(7, 10);
        
        foreach (Vector3Int coord in pixelCoords)
        {
            gameplayTexture.SetPixel(coord.x, coord.y, targetColor);
            gameplayTexture.Apply();
            waitCounter++;

            if (waitCounter > threshold)
            {
                waitCounter = 0;
                threshold = Random.Range(7, 10);
                yield return null;
            }
        }
    }

    public IEnumerator UpdateFillIncremental(List<Vector2Int> newTaken, List<Vector2Int> newEdges)
    {
        newTaken.Shuffle();
        newEdges.Shuffle();

        int waitCounter = 0;
        int threshold = Random.Range(1, 2);
        Color cellColor = Color.red;
        
        foreach (Vector2Int coord in newTaken)
        {
            gameplayTexture.SetPixel(coord.x, coord.y, cellColor);
            gameplayTexture.Apply();
            waitCounter++;

            if (waitCounter > threshold)
            {
                waitCounter = 0;
                threshold = Random.Range(1, 2);
                yield return null;
            }
        }

        cellColor.a = 0.87f;
        
        foreach (Vector2Int coord in newEdges)
        {
            gameplayTexture.SetPixel(coord.x, coord.y, cellColor);
            gameplayTexture.Apply();
            waitCounter++;

            if (waitCounter > threshold)
            {
                waitCounter = 0;
                threshold = Random.Range(1, 2);
                yield return null;
            }
        }
    }

    private void SetTextureFromBase()
    {
        gameplayTexture = new Texture2D(gameplayTextureBase.width, gameplayTextureBase.height,gameplayTextureBase.format, 0, false);
        gameplayTexture.SetPixels(gameplayTextureBase.GetPixels());
        gameplayTexture.filterMode = FilterMode.Point;
        gameplayTexture.wrapMode = TextureWrapMode.Repeat;
        gameplayTexture.Apply();
        gameplayImage.material.SetTexture("_GameStateMap", gameplayTexture);
    }

    private void ChangeRandomPixel()
    {
        //gameplayTexture.
    }

    public void OnValidate()
    {
        SetTextureFromBase();
    }
}
