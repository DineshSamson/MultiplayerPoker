using UnityEngine;

public class ApplicationSettings : MonoBehaviour
{
    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.runInBackground = true;

#if UNITY_SERVER
        Application.targetFrameRate = 30;
#else
        Application.targetFrameRate = 60;
#endif
    }
}