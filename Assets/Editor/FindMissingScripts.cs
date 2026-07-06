using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FindMissingScripts
{
    [MenuItem("Tools/Find Missing Scripts In Scene")]
    private static void FindMissingScriptsInScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        GameObject[] rootObjects = activeScene.GetRootGameObjects();

        int totalMissingScripts = 0;

        foreach (GameObject rootObject in rootObjects)
        {
            totalMissingScripts += CheckObjectAndChildren(rootObject);
        }

        if (totalMissingScripts == 0)
        {
            UnityEngine.Debug.Log(
                "Missing script scan complete. No missing scripts were found."
            );
        }
        else
        {
            UnityEngine.Debug.LogWarning(
                $"Missing script scan complete. Found {totalMissingScripts} missing script reference(s)."
            );
        }
    }

    private static int CheckObjectAndChildren(GameObject gameObject)
    {
        int missingOnCurrentObject =
            GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);

        int totalMissing = missingOnCurrentObject;

        if (missingOnCurrentObject > 0)
        {
            UnityEngine.Debug.LogWarning(
                $"Found {missingOnCurrentObject} missing script(s) on: {GetObjectPath(gameObject)}",
                gameObject
            );
        }

        foreach (Transform child in gameObject.transform)
        {
            totalMissing += CheckObjectAndChildren(child.gameObject);
        }

        return totalMissing;
    }

    private static string GetObjectPath(GameObject gameObject)
    {
        string objectPath = gameObject.name;
        Transform currentParent = gameObject.transform.parent;

        while (currentParent != null)
        {
            objectPath = currentParent.name + "/" + objectPath;
            currentParent = currentParent.parent;
        }

        return objectPath;
    }
}