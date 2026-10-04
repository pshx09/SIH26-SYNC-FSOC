using UnityEngine;
using UnityEditor;
using System.Text;

[InitializeOnLoad]
public class TransformDumper
{
    static TransformDumper()
    {
        EditorApplication.delayCall += DumpTransforms;
    }

    private static void DumpTransforms()
    {
        // Find all cameras in the scene, including inactive ones
        Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Camera virtualCam = null;
        
        foreach (Camera c in cams)
        {
            if (c.name == "VirtualCamera" && c.gameObject.scene.isLoaded)
            {
                virtualCam = c;
                break;
            }
        }

        if (virtualCam == null)
        {
            Debug.LogWarning("TransformDumper: Could not find VirtualCamera in any loaded scene!");
            return;
        }

        Transform root = virtualCam.transform;
        while (root.parent != null) root = root.parent;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("--- TRANSFORM DUMP START ---");
        DumpRecursive(root, "", sb);
        sb.AppendLine("--- TRANSFORM DUMP END ---");

        Transform panBase = FindChildByName(root, "PanBase");
        Transform tiltBase = FindChildByName(root, "TiltBase");

        if (panBase != null && tiltBase != null)
        {
            Vector3 panAxis = panBase.up;
            Vector3 tiltAxis = tiltBase.right;
            Vector3 camForward = virtualCam.transform.forward;
            Vector3 camUp = virtualCam.transform.up;
            Vector3 camRight = virtualCam.transform.right;

            float anglePanToCam = Vector3.Angle(panAxis, camForward);
            float angleTiltToCam = Vector3.Angle(tiltAxis, camForward);

            sb.AppendLine();
            sb.AppendLine("[AxisAnalysis]");
            sb.AppendLine($"1. PanBase world Y axis: {panAxis.ToString("F3")}");
            sb.AppendLine($"2. TiltBase world X axis: {tiltAxis.ToString("F3")}");
            sb.AppendLine($"3. VirtualCamera world forward: {camForward.ToString("F3")}");
            sb.AppendLine($"4. VirtualCamera world up: {camUp.ToString("F3")}");
            sb.AppendLine($"5. VirtualCamera world right: {camRight.ToString("F3")}");
            sb.AppendLine($"6. Angle between PanBase Y axis and VirtualCamera forward: {anglePanToCam:F3}°");
            sb.AppendLine($"7. Angle between TiltBase X axis and VirtualCamera forward: {angleTiltToCam:F3}°");
        }
        else
        {
            sb.AppendLine("\n[AxisAnalysis] Error: Could not find PanBase or TiltBase within the root: " + root.name);
        }

        Debug.Log(sb.ToString());
    }

    private static Transform FindChildByName(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform child in t)
        {
            Transform result = FindChildByName(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private static void DumpRecursive(Transform t, string indent, StringBuilder sb)
    {
        sb.AppendLine($"{indent}- {t.name}:\n{indent}  LocalPos: {t.localPosition.ToString("F3")}, LocalEuler: {t.localEulerAngles.ToString("F3")}\n{indent}  WorldPos: {t.position.ToString("F3")}, WorldEuler: {t.eulerAngles.ToString("F3")}\n{indent}  Forward: {t.forward.ToString("F3")}, Up: {t.up.ToString("F3")}, Right: {t.right.ToString("F3")}");
        foreach (Transform child in t)
        {
            DumpRecursive(child, indent + "  ", sb);
        }
    }
}

