using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public class AttachRadarHandover
{
    static AttachRadarHandover()
    {
        EditorApplication.delayCall += AttachScript;
    }

    private static void AttachScript()
    {
        // Find PanTiltTracker to know where to attach
        PanTiltTracker tracker = Object.FindObjectOfType<PanTiltTracker>();
        if (tracker != null)
        {
            bool modified = false;

            // Fix the incorrectly serialized tilt limit properly
            if (tracker.tiltMin != -90f)
            {
                tracker.tiltMin = -90f;
                Debug.Log("[Auto-Setup] Fixed PanTiltTracker tiltMin serialized value to -90f.");
                modified = true;
            }

            // Attach RadarHandover to the same GameObject if missing
            if (tracker.gameObject.GetComponent<RadarHandover>() == null)
            {
                tracker.gameObject.AddComponent<RadarHandover>();
                Debug.Log("[Auto-Setup] Attached RadarHandover.cs to " + tracker.gameObject.name);
                modified = true;
            }

            // Mark scene dirty so it saves
            if (modified && !Application.isPlaying)
            {
                EditorUtility.SetDirty(tracker);
                EditorSceneManager.MarkSceneDirty(tracker.gameObject.scene);
                EditorSceneManager.SaveScene(tracker.gameObject.scene);
            }
        }
    }
}
