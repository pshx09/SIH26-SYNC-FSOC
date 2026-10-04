using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FixSkybox
{
    static FixSkybox()
    {
        EditorApplication.delayCall += ApplyFix;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void ApplyFix()
    {
        string path = "Assets/Materials/FSOC_NightSky_Mat_Generated.mat";
        Material nightSky = AssetDatabase.LoadAssetAtPath<Material>(path);
        
        if (nightSky == null)
        {
            Shader skyboxShader = Shader.Find("Skybox/NightSkyGradient");
            if (skyboxShader != null)
            {
                nightSky = new Material(skyboxShader);
                ColorUtility.TryParseHtmlString("#020713", out Color topCol);
                ColorUtility.TryParseHtmlString("#040B18", out Color midCol);
                ColorUtility.TryParseHtmlString("#0A1A32", out Color botCol);
                
                nightSky.SetColor("_TopColor", topCol);
                nightSky.SetColor("_MidColor", midCol);
                nightSky.SetColor("_BottomColor", botCol);
                
                if (!System.IO.Directory.Exists("Assets/Materials"))
                {
                    AssetDatabase.CreateFolder("Assets", "Materials");
                }
                
                AssetDatabase.CreateAsset(nightSky, path);
                AssetDatabase.SaveAssets();
                Debug.Log($"[FixSkybox] Created completely new valid Skybox material at {path}");
            }
        }
        else
        {
            // Just update shader and colors if it already exists
            Shader skyboxShader = Shader.Find("Skybox/NightSkyGradient");
            if (skyboxShader != null && nightSky.shader != skyboxShader)
            {
                nightSky.shader = skyboxShader;
            }
            
            ColorUtility.TryParseHtmlString("#020713", out Color topCol);
            ColorUtility.TryParseHtmlString("#040B18", out Color midCol);
            ColorUtility.TryParseHtmlString("#0A1A32", out Color botCol);
            
            nightSky.SetColor("_TopColor", topCol);
            nightSky.SetColor("_MidColor", midCol);
            nightSky.SetColor("_BottomColor", botCol);
            EditorUtility.SetDirty(nightSky);
        }

        if (nightSky != null)
        {
            RenderSettings.skybox = nightSky;
            
            // Force Camera Settings
            foreach (Camera cam in Camera.allCameras)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.backgroundColor = new Color(0.015f, 0.043f, 0.094f); // Fallback color
            }
            
            Debug.Log("[FixSkybox] RenderSettings.skybox successfully forced!");
        }
    }
}
