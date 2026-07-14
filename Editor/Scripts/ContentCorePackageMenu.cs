#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ActionFit.Content.Editor
{
    public static class ContentCorePackageMenu
    {
        private const string MenuRoot = "Tools/Package/Content Core/";
        private const string ReadmePath = "Packages/com.actionfit.content-core/README.md";
        private const int ReadmePriority = 904;

        [MenuItem(MenuRoot + "README", false, ReadmePriority)]
        private static void OpenReadme()
        {
            var readme = AssetDatabase.LoadAssetAtPath<TextAsset>(ReadmePath);
            if (readme == null)
            {
                EditorUtility.DisplayDialog("Package README", $"README was not found.\n{ReadmePath}", "OK");
                return;
            }

            Selection.activeObject = readme;
            AssetDatabase.OpenAsset(readme);
        }
    }
}
#endif
