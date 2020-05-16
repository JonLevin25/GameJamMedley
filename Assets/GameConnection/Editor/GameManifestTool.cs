﻿using System;
using System.Collections.Generic;
using System.Linq;
using Common;
using Common.Editor;
using Common.Scripts.PayloadTypes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManifestTool : EditorWindow
{
    private const string DialogTitle = "GameManifestTool";
    [MenuItem(EditorConsts.MenuItemsPrefix + "Generate Editor Manifest", priority = 1)]
    public static void GenerateManifest()
    {
        var confirm = EditorUtility.DisplayDialog(DialogTitle, "This will change all build scenes except the first. Continue?", "OK", "Cancel");
        if (!confirm) return;

        var foundGames = FindGamesInProject();
        var games = foundGames.Select((editorGame, i) =>
        {
            var buildIdx = i + 1; // Skip idx 0 because thats the Initial scene
            return EditorToRuntimeGame(editorGame, buildIdx);
        });

        var manifest = GetOrCreateManifest();
        SetManifestGames(manifest, games);
    }

    private static GameManifest GetOrCreateManifest()
    {
        var manifest = Resources.Load<GameManifest>(Consts.ManifestResourcePath);
        if (manifest) return manifest;

        manifest = ScriptableObject.CreateInstance<GameManifest>();
        EditorHelper.CreateAndSaveAsset(EditorConsts.ManifestAssetPath, manifest);

        return manifest;
    }

    private static void SetManifestGames(GameManifest manifest, IEnumerable<Runtime_ConnectedGame> games)
    {
        var serializedManifest = new SerializedObject(manifest);
        var connectedGamesProp = serializedManifest.FindProperty("connectedGames");
        EditorHelper.SetArrayMangedRefs(connectedGamesProp, games.ToArray());
        
        serializedManifest.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
    
    private static Runtime_ConnectedGame EditorToRuntimeGame(Editor_ConnectedGame editorGame, int buildIdx)
    {
        if (editorGame == null) return null;
        return new Runtime_ConnectedGame(editorGame.InputPayloadType, editorGame.OutputPayloadType, buildIdx);
    }

    private static Editor_ConnectedGame[] FindGamesInProject()
    {
        var scenesWithLevelManagers = FindScenesWithLevelManagers();
        var games = scenesWithLevelManagers
            .Select(gameTup =>
            {
                var (scene, levelManager) = gameTup;
                var (inputType, outputType) = GetPayloadTypes(levelManager);
                return new Editor_ConnectedGame(inputType, outputType, scene);
            });

        return games.ToArray();
    }

    private static IEnumerable<(string scenePath, LevelManagerBase)> FindScenesWithLevelManagers()
    {
        var scenePaths = AssetDatabase
            .FindAssets("t:Scene", new[] {"Assets"})
            .Select(AssetDatabase.GUIDToAssetPath);


        var filtered = 
            scenePaths.Select(scenePath =>
                {
                    var rootGOs = GetRootGameObjects(scenePath);
                    var results = rootGOs.Select(root =>
                    { 
                        var levelManager = root.GetComponent<LevelManagerBase>(); // For simplicity and time - allow only root-level managers
                        return (scenePath, levelManager);
                    })
                    .Where(x => x.levelManager != null)
                    .ToArray();

                if (results.Length == 0) return (scenePath, null);
                if (results.Length == 1) return results[0];

                throw new IndexOutOfRangeException(
                    $"Scene at path {scenePath} had more than one LevelManager! this is not allowed");
            })
            .Where(x => x.levelManager != null);

        return filtered;
    }

    private static (Type inputType, Type outputType) GetPayloadTypes(LevelManagerBase levelManager)
    {
        var lvlManagerType = levelManager.GetType();
        var isGenericLevelManager = lvlManagerType.IsAssignableFrom(typeof(LevelManagerBase<,>));
        if (isGenericLevelManager)
        {
            // Find types (e.g. LevelManager<ElementPayload, EndPayload>) 
            var genericArgs = lvlManagerType.GenericTypeArguments;
            var inpType = genericArgs[0];
            var outType = genericArgs[1];

            return (inpType, outType);
        }
        else
        {
            throw new Exception($"LevelManager {levelManager.name} did not derive from generic LevelManagerBase! Actual type: {lvlManagerType.Name}");
        }
    }

    private static GameObject[] GetRootGameObjects(string scenePath)
    {
        // TODO 
        var scene = EditorSceneManager.GetSceneByPath(scenePath);
        // var sceneWasOpen = scene;
        throw new NotImplementedException();
    }
}