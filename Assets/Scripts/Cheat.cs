using UnityEngine;

public class Cheat : MonoBehaviour
{
    [ContextMenu("Slow Down Gameplay")]
    private void SlowDownGameplay()
    {
        Time.timeScale = 0.1f;
    }

    [ContextMenu("Normal Gameplay")]
    private void NormalGameplay()
    {
        Time.timeScale = 1;
    }

    [ContextMenu("Speed Up Gameplay")]
    private void SpeedUpGameplay()
    {
        Time.timeScale = 2;
    }
}
