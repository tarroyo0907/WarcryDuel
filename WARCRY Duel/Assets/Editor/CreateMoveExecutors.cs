using UnityEngine;
using UnityEditor;
// Tyler Arroyo
// Create Move Executors
// Editor script to generate executor assets and wire them to FigurineMove assets
public class CreateMoveExecutors
{
    [MenuItem("Tools/Create Move Executors")]
    public static void CreateAll()
    {
        string executorFolder = "Assets/Resources/Moves/Executors";

        var externalMoves = new (string moveName, System.Type executorType)[]
        {
            ("Fortification",  typeof(FortificationExecutor)),
            ("Fruitful Fury",  typeof(FruitfulFuryExecutor)),
            ("Cleansing Rose", typeof(CleansingRoseExecutor)),
            ("Rejuvinate",     typeof(RejuvinateExecutor)),
        };

        foreach (var (moveName, executorType) in externalMoves)
        {
            string assetPath = $"{executorFolder}/{moveName.Replace(" ", "")}Executor.asset";

            ExternalMoveExecutor executor = AssetDatabase.LoadAssetAtPath<ExternalMoveExecutor>(assetPath);
            if (executor == null)
            {
                executor = (ExternalMoveExecutor)ScriptableObject.CreateInstance(executorType);
                AssetDatabase.CreateAsset(executor, assetPath);
                Debug.Log($"Created external executor asset: {assetPath}");
            }

            string movePath = $"Assets/Resources/Moves/{moveName}.asset";
            FigurineMove move = AssetDatabase.LoadAssetAtPath<FigurineMove>(movePath);
            if (move != null)
            {
                move.externalExecutor = executor;
                EditorUtility.SetDirty(move);
                Debug.Log($"Wired {executorType.Name} -> {moveName}");
            }
            else
            {
                Debug.LogWarning($"Could not find FigurineMove asset at: {movePath}");
            }
        }

        // Create MoveEffect assets
        string moveEffectFolder = "Assets/Resources/MoveEffects";
        string pushbackPath = $"{moveEffectFolder}/PushbackEffect.asset";
        if (AssetDatabase.LoadAssetAtPath<MoveEffect>(pushbackPath) == null)
        {
            MoveEffect pushback = ScriptableObject.CreateInstance<PushbackEffect>();
            AssetDatabase.CreateAsset(pushback, pushbackPath);
            Debug.Log($"Created MoveEffect asset: {pushbackPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Done! All executor assets created and wired.");
    }
}
