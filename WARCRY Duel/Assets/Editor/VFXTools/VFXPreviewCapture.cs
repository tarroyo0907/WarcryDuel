using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Headless preview tool: renders a prefab (optionally sampling a ParticleSystem
// at specific timestamps) to PNG frames on disk, so VFX can be reviewed without
// opening the Editor UI. Invoked via `Unity.exe -batchmode -quit -executeMethod`.
public static class VFXPreviewCapture
{
    private const int Width = 512;
    private const int Height = 512;

    // Sanity check for the capture pipeline itself (camera -> RenderTexture -> PNG),
    // independent of any particle authoring.
    public static void TestCapture()
    {
        SetUpCaptureScene(out Camera cam, Color.black);
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.transform.position = Vector3.zero;
        Renderer renderer = cube.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = Color.green;
        renderer.material = mat;

        string outDir = Path.Combine(Application.dataPath, "..", "..", "VFXPreview_POC");
        Directory.CreateDirectory(outDir);
        CaptureFrame(cam, Path.Combine(outDir, "poc_frame.png"));

        Debug.Log("[VFXPreviewCapture] POC capture written to " + outDir);
    }

    // resourcePath: path under Assets/Resources (no extension), e.g. "PFX/PS_FruitfulFuryHeal"
    // outDirAbs: absolute output directory for the PNG frames
    // timestampsCsv: comma separated seconds, e.g. "0.1,0.3,0.6,1.0,1.5,2.0"
    public static void CaptureParticlePrefab()
    {
        string[] args = Environment.GetCommandLineArgs();
        string resourcePath = GetArg(args, "-resourcePath");
        string outDirAbs = GetArg(args, "-outDir");
        string timestampsCsv = GetArg(args, "-timestamps");
        float camDistance = 4f;
        string camDistanceStr = GetArg(args, "-camDistance");
        if (!string.IsNullOrEmpty(camDistanceStr)) float.TryParse(camDistanceStr, out camDistance);

        if (string.IsNullOrEmpty(resourcePath) || string.IsNullOrEmpty(outDirAbs) || string.IsNullOrEmpty(timestampsCsv))
        {
            Debug.LogError("[VFXPreviewCapture] Missing required args: -resourcePath -outDir -timestamps");
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab == null)
        {
            Debug.LogError("[VFXPreviewCapture] Could not load prefab at Resources/" + resourcePath);
            return;
        }

        SetUpCaptureScene(out Camera cam, new Color(0.08f, 0.08f, 0.09f, 1f));
        cam.transform.position = new Vector3(0, 0.85f, -camDistance);
        cam.transform.LookAt(new Vector3(0, 0.65f, 0));
        cam.fieldOfView = 45f;

        GameObject instance = GameObject.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        ParticleSystem[] allSystems = instance.GetComponentsInChildren<ParticleSystem>(true);

        Directory.CreateDirectory(outDirAbs);

        string[] timestamps = timestampsCsv.Split(',');
        for (int i = 0; i < timestamps.Length; i++)
        {
            if (!float.TryParse(timestamps[i], out float t)) continue;

            foreach (ParticleSystem ps in allSystems)
            {
                ps.Simulate(t, false, true, true);
            }

            string fileName = string.Format("frame_{0:00}_t{1}.png", i, timestamps[i].Replace(".", "p"));
            CaptureFrame(cam, Path.Combine(outDirAbs, fileName));
            Debug.Log("[VFXPreviewCapture] Captured " + fileName);
        }

        Debug.Log("[VFXPreviewCapture] Done. Frames written to " + outDirAbs);
    }

    private static string GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return null;
    }

    private static void SetUpCaptureScene(out Camera cam, Color background)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject lightGO = new GameObject("CaptureLight");
        Light light = lightGO.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);

        GameObject camGO = new GameObject("CaptureCamera");
        cam = camGO.AddComponent<Camera>();
        cam.transform.position = new Vector3(0, 1.2f, -3f);
        cam.transform.LookAt(Vector3.up * 0.3f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.fieldOfView = 35f;
    }

    private static void CaptureFrame(Camera cam, string outPath)
    {
        RenderTexture rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture prevActive = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        File.WriteAllBytes(outPath, png);

        cam.targetTexture = null;
        RenderTexture.active = prevActive;
        rt.Release();
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);
    }
}
