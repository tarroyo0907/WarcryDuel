using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Procedurally builds the Fruitful Fury heal VFX (green stars + plus symbols)
// as a reusable prefab under Resources/PFX, so it can be tuned from code and
// reviewed headlessly via VFXPreviewCapture instead of hand-authoring in the
// Editor UI.
public static class FruitfulFuryVFXBuilder
{
    private const int TextureSize = 128;
    private const string TexturesDir = "Resources/PFX/Textures";
    private const string MaterialsDir = "Resources/PFX/Materials";
    private const string PrefabPath = "Assets/Resources/PFX/PS_FruitfulFuryHeal.prefab";

    [MenuItem("Tools/VFX/Rebuild Fruitful Fury Heal VFX")]
    public static void Build()
    {
        // Only swap to a blank scene when running headless (-executeMethod).
        // Doing this while the Editor is open with a real scene loaded would
        // silently discard whatever the user currently has open.
        if (Application.isBatchMode)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        Texture2D starTex = GenerateStarTexture(TextureSize);
        Texture2D plusTex = GeneratePlusTexture(TextureSize);
        Texture2D dotTex = GenerateSoftDotTexture(TextureSize);

        string starTexPath = SaveTexturePNG(starTex, Path.Combine(TexturesDir, "HealStarSprite.png"));
        string plusTexPath = SaveTexturePNG(plusTex, Path.Combine(TexturesDir, "HealPlusSprite.png"));
        string dotTexPath = SaveTexturePNG(dotTex, Path.Combine(TexturesDir, "HealDotSprite.png"));

        Texture2D starAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(starTexPath);
        Texture2D plusAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(plusTexPath);
        Texture2D dotAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(dotTexPath);

        Directory.CreateDirectory(Path.Combine(Application.dataPath, MaterialsDir));
        Material starMat = CreateParticleMaterial("HealStarMat", starAsset, "Assets/" + MaterialsDir + "/HealStarMat.mat");
        Material plusMat = CreateParticleMaterial("HealPlusMat", plusAsset, "Assets/" + MaterialsDir + "/HealPlusMat.mat");
        Material spiralMat = CreateParticleMaterial("HealSpiralMat", dotAsset, "Assets/" + MaterialsDir + "/HealSpiralMat.mat");

        GameObject root = new GameObject("PS_FruitfulFuryHeal");

        Color green = new Color(0.55f, 1f, 0.42f, 1f);

        BuildEmitter("Stars", root.transform, starMat, green,
            startSizeMin: 0.11f, startSizeMax: 0.16f,
            bursts: new[] { new ParticleSystem.Burst(0f, 6), new ParticleSystem.Burst(0.15f, 5) },
            lifetimeMin: 1.1f, lifetimeMax: 1.5f);

        BuildEmitter("Pluses", root.transform, plusMat, green,
            startSizeMin: 0.07f, startSizeMax: 0.11f,
            bursts: new[] { new ParticleSystem.Burst(0.05f, 4), new ParticleSystem.Burst(0.22f, 4) },
            lifetimeMin: 0.9f, lifetimeMax: 1.3f);

        BuildSpiralEmitter("Spiral A", root.transform, spiralMat, green, startAngleDegrees: 0f);
        BuildSpiralEmitter("Spiral B", root.transform, spiralMat, green, startAngleDegrees: 180f);

        if (File.Exists(Path.Combine(Application.dataPath, "..", PrefabPath)))
        {
            AssetDatabase.DeleteAsset(PrefabPath);
        }
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        GameObject.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[FruitfulFuryVFXBuilder] Built heal VFX prefab at " + PrefabPath);
    }

    #region Emitter Construction
    private static GameObject BuildEmitter(string name, Transform parent, Material mat, Color color,
        float startSizeMin, float startSizeMax, ParticleSystem.Burst[] bursts, float lifetimeMin, float lifetimeMax)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        float lifetime = (lifetimeMin + lifetimeMax) / 2f;
        float startSize = (startSizeMin + startSizeMax) / 2f;

        var main = ps.main;
        main.loop = false;
        main.duration = 1.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.35f);
        main.startSize = new ParticleSystem.MinMaxCurve(startSize, startSize);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;
        main.playOnAwake = true;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(bursts);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.28f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // cone's local +Z default points forward; redirect it to +Y (up)

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.475f, 0.475f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(1f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = grad;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.15f, 1f), new Keyframe(0.75f, 0.9f), new Keyframe(1f, 0.5f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(10f, 10f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.material = mat;

        return go;
    }

    // A single long-lived particle that orbits the vertical axis while
    // climbing, dragging its own trail behind it, so it draws one continuous
    // spiral ribbon from the target's base up past its head. Two instances,
    // started startAngleDegrees apart (180 = opposite sides), read as a
    // double helix.
    private static GameObject BuildSpiralEmitter(string name, Transform parent, Material mat, Color color, float startAngleDegrees)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.duration = 2.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 2.2f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.07f);
        main.startColor = color;
        main.startRotation3D = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 4;
        main.playOnAwake = true;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.26f;
        shape.radiusThickness = 0f; // emit from the ring edge only
        shape.rotation = new Vector3(-90f, 0f, startAngleDegrees); // lay the ring flat (around the vertical axis), then spin its start point by startAngleDegrees
        shape.arcMode = ParticleSystemShapeMultiModeValue.Loop; // deterministic start position instead of a random point on the ring

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.3f, 0.3f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(185f, 185f); // same rise:rotation ratio as the original angle, just much slower overall
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.08f),
                new GradientAlphaKey(0.9f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = grad;

        var trails = ps.trails;
        trails.enabled = true;
        trails.mode = ParticleSystemTrailMode.PerParticle;
        trails.ratio = 1f;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.97f); // trail spans almost the whole climb
        trails.minVertexDistance = 0.01f;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(0.5f);
        trails.textureMode = ParticleSystemTrailTextureMode.Stretch;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.material = mat;
        renderer.trailMaterial = mat;

        return go;
    }
    #endregion

    #region Material
    private static Material CreateParticleMaterial(string name, Texture2D tex, string savePath)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader) { name = name };

        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
        }
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 2f); // Additive
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.One);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.One);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
        mat.renderQueue = (int)RenderQueue.Transparent;

        if (File.Exists(Path.Combine(Application.dataPath, "..", savePath)))
        {
            AssetDatabase.DeleteAsset(savePath);
        }
        AssetDatabase.CreateAsset(mat, savePath);
        return mat;
    }
    #endregion

    #region Texture Generation
    // 4-point sparkle in the style of the Gemini logo: a classic astroid
    // curve (|x|^p + |y|^p <= 1, p = 2/3), which gives sharp axis-aligned
    // cusps with smooth concave sides between them.
    private static Texture2D GenerateStarTexture(int size)
    {
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerR = size * 0.47f;
        const float p = 2f / 3f;

        return RasterizeShape(size, (px, py) =>
        {
            float dx = Mathf.Abs(px - center.x) / outerR;
            float dy = Mathf.Abs(py - center.y) / outerR;
            return Mathf.Pow(dx, p) + Mathf.Pow(dy, p) <= 1f;
        });
    }

    private static Texture2D GenerateSoftDotTexture(int size)
    {
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float maxR = size * 0.48f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float t = Mathf.Clamp01(dist / maxR);
                float alpha = Mathf.Pow(1f - t, 1.8f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static Texture2D GeneratePlusTexture(int size)
    {
        float halfW = size * 0.16f;
        float halfL = size * 0.40f;
        float cx = size / 2f, cy = size / 2f;

        return RasterizeShape(size, (px, py) =>
        {
            float x = px - cx, y = py - cy;
            bool horizontalBar = Mathf.Abs(x) <= halfL && Mathf.Abs(y) <= halfW;
            bool verticalBar = Mathf.Abs(y) <= halfL && Mathf.Abs(x) <= halfW;
            return horizontalBar || verticalBar;
        });
    }

    private static Texture2D RasterizeShape(int size, System.Func<float, float, bool> insideTest)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[size * size];
        const int ss = 3;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int coverage = 0;
                for (int sy = 0; sy < ss; sy++)
                {
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float px = x + (sx + 0.5f) / ss;
                        float py = y + (sy + 0.5f) / ss;
                        if (insideTest(px, py)) coverage++;
                    }
                }
                float alpha = (float)coverage / (ss * ss);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return tex;
    }

    private static string SaveTexturePNG(Texture2D tex, string assetsRelativePath)
    {
        string fullPath = Path.Combine(Application.dataPath, assetsRelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());

        string assetPath = "Assets/" + assetsRelativePath.Replace("\\", "/");
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        return assetPath;
    }
    #endregion
}
