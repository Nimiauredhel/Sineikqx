using UnityEngine;
using UnityEngine.UI;

public class GameRenderController : MonoBehaviour
{
    [SerializeField] private RawImage gameplayImage;
    [SerializeField] private Texture2D gameplayTextureBase;

    private Texture2D gameplayTexture;
    private float defVectorZ = 0.005f;

    public void Initialize()
    {
        SetTextureFromBase();
    }

    public void UpdatePlayerPosition(Vector2 newPos)
    {
        gameplayImage.material.SetVector("_PlayerPosition", new Vector4((newPos.x+0.5f)/Constants.GRID_SIZE, (newPos.y+0.5f)/Constants.GRID_SIZE, defVectorZ));
    }
    
    public void UpdateBossPosition(Vector2 newPos)
    {
        gameplayImage.material.SetVector("_BossPosition", new Vector4((newPos.x+0.5f)/Constants.GRID_SIZE, (newPos.y+0.5f)/Constants.GRID_SIZE, defVectorZ));
    }

    public void UpdateGrid(CellState[][] newGrid)
    {
        if (gameplayTexture == null)
        {
            SetTextureFromBase();
        }

        Color col = new Color();
        Color[] newFloatGrid = new Color[Constants.GRID_SIZE*Constants.GRID_SIZE];

        int index = 0;
        
        for (int x = 0; x < Constants.GRID_SIZE; x++)
        {
            for (int y = 0; y < Constants.GRID_SIZE; y++)
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
