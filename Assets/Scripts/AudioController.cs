using FMOD.Studio;
using FMODUnity;
using UnityEngine;

public class AudioController : MonoBehaviour
{
    // instantly regret making these all labeled parameters
    private const string STATE = "State";
    private const string SAFE = "Safe";
    private const string UNSAFE = "Unsafe";
    
    private const string TRANSITION = "Transition";
    private const string GOOD = "Good";
    private const string BAD = "Bad";

    private const string FILLPERCENT = "FillPercent";
    
    [SerializeField] private EventReference introEvent;
    [SerializeField] private EventReference winEvent;
    [SerializeField] private EventReference loseEvent;
    [SerializeField] private EventReference gameplayEvent;

    private EventInstance gameplayInstance;

    public void PlayIntro()
    {
        RuntimeManager.PlayOneShot(introEvent);
    }
    
    public void PlayWin()
    {
        RuntimeManager.PlayOneShot(winEvent);
    }
    
    public void PlayLose()
    {
        RuntimeManager.PlayOneShot(loseEvent);
    }

    public void StartGameplayMusic()
    {
        gameplayInstance = RuntimeManager.CreateInstance(gameplayEvent);
        SetSafe(true, true);
        SetFillPercent(0.0f);
        gameplayInstance.start();
    }

    public void ReleaseGameplayMusic()
    {
        gameplayInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        gameplayInstance.release();
    }

    public void SetSafe(bool safe, bool transitionGood)
    {
        RuntimeManager.StudioSystem.setParameterByNameWithLabel(TRANSITION, transitionGood ? GOOD : BAD);
        RuntimeManager.StudioSystem.setParameterByNameWithLabel(STATE, safe ? SAFE : UNSAFE);
    }

    public void SetFillPercent(float fillPercent)
    {
        RuntimeManager.StudioSystem.setParameterByName(FILLPERCENT, fillPercent);
    }
}
