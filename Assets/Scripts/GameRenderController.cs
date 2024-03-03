using UnityEngine;
using UnityEngine.UI;

public class GameRenderController : MonoBehaviour
{
    [SerializeField] private RawImage gameplayImage;
    [SerializeField] private Texture2D gameplayTextureBase;

    private Texture2D gameplayTexture;

    private void Start()
    {
        SetTextureFromBase();
    }

    private void SetTextureFromBase()
    {
        gameplayTexture = new Texture2D(gameplayTextureBase.width, gameplayTextureBase.height,gameplayTextureBase.format, 0, false);
        gameplayTexture.SetPixels(gameplayTextureBase.GetPixels());
        gameplayTexture.filterMode = FilterMode.Point;
        gameplayTexture.Apply();
        gameplayImage.material.SetTexture("_GameStateMap", gameplayTexture);
    }

    private void ChangeRandomPixel()
    {
        //gameplayTexture.
    }

    private void OnValidate()
    {
        SetTextureFromBase();
    }
}
