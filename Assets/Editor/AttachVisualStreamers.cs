using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class AttachVisualStreamers
{
    [MenuItem("Tools/Attach Visual Streamers")]
    public static void Attach()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "FSOC_Main")
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FSOC_Main.unity");
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        }

        var bridge = GameObject.Find("TelemetryBridge");
        if (bridge == null)
        {
            Debug.LogError("TelemetryBridge not found!");
            return;
        }

        if (bridge.GetComponent<UnitySimulationStreamPublisher>() == null)
            bridge.AddComponent<UnitySimulationStreamPublisher>();

        if (bridge.GetComponent<UnitySensorStreamPublisher>() == null)
            bridge.AddComponent<UnitySensorStreamPublisher>();

        EditorSceneManager.SaveScene(scene);
        Debug.Log("Visual streamers attached and scene saved.");
    }
}
