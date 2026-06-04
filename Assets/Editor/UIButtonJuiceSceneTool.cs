using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class UIButtonJuiceSceneTool
{
    [MenuItem("Tools/Against The Mist/UI/Add Button Juice To Open Scene")]
    public static void AddButtonJuiceToOpenScene()
    {
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int added = 0;

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null || button.GetComponent<UIButtonJuice>() != null)
                continue;

            Undo.AddComponent<UIButtonJuice>(button.gameObject);
            EditorUtility.SetDirty(button.gameObject);
            added++;
        }

        if (added > 0)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"UIButtonJuiceSceneTool: added UIButtonJuice to {added} buttons in open scene.");
    }

    [MenuItem("Tools/Against The Mist/UI/Remove Button Juice From Open Scene")]
    public static void RemoveButtonJuiceFromOpenScene()
    {
        UIButtonJuice[] juices = Object.FindObjectsByType<UIButtonJuice>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int removed = 0;

        for (int i = 0; i < juices.Length; i++)
        {
            UIButtonJuice juice = juices[i];
            if (juice == null)
                continue;

            Undo.DestroyObjectImmediate(juice);
            removed++;
        }

        if (removed > 0)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log($"UIButtonJuiceSceneTool: removed UIButtonJuice from {removed} objects in open scene.");
    }
}
