// Compile-only stubs for UnityEditor + IMGUI API used by UI/Editor and City/UnityIntegration/Editor. Not shipped.
using System;
using UnityEngine;

namespace UnityEngine
{
    public class GUILayoutOption { }
    public class GUIStyle { public GUIStyle() {} public GUIStyle(GUIStyle s) {} public bool wordWrap { get; set; } public int fontSize { get; set; } public FontStyle fontStyle { get; set; } }
    public static class GUILayout
    {
        public static bool Button(string text, params GUILayoutOption[] options) => false;
        public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options) => false;
        public static bool Toggle(bool value, string text, params GUILayoutOption[] options) => value;
        public static bool Toggle(bool value, string text, GUIStyle style, params GUILayoutOption[] options) => value;
        public static void Label(string text, params GUILayoutOption[] options) {}
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] options) {}
        public static void Space(float px) {}
        public static void FlexibleSpace() {}
        public static GUILayoutOption Width(float w) => new GUILayoutOption();
        public static GUILayoutOption Height(float h) => new GUILayoutOption();
        public static GUILayoutOption MinHeight(float h) => new GUILayoutOption();
        public static GUILayoutOption MaxHeight(float h) => new GUILayoutOption();
        public static GUILayoutOption ExpandWidth(bool v) => new GUILayoutOption();
        public static GUILayoutOption ExpandHeight(bool v) => new GUILayoutOption();
    }
    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }
}

namespace UnityEditor
{
    public class SceneAsset : UnityEngine.Object { }
    public class MenuItem : Attribute { public MenuItem(string path, bool validate = false, int priority = 0) {} }
    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok) => true;
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => true;
        public static void SetDirty(UnityEngine.Object target) {}
    }
    public static class AssetDatabase
    {
        public static void Refresh() {}
        public static void SaveAssets() {}
        public static T LoadAssetAtPath<T>(string assetPath) where T : UnityEngine.Object => null;
        public static string GetAssetPath(UnityEngine.Object assetObject) => "";
        public static string GUIDToAssetPath(string guid) => "";
        public static string AssetPathToGUID(string path) => "";
        public static string[] FindAssets(string filter) => new string[0];
        public static string[] FindAssets(string filter, string[] searchInFolders) => new string[0];
        public static bool IsValidFolder(string path) => true;
        public static string CreateFolder(string parentFolder, string newFolderName) => "";
        public static UnityEngine.Object[] LoadAllAssetsAtPath(string path) => new UnityEngine.Object[0];
    }
    public class EditorBuildSettingsScene
    {
        public bool enabled;
        public string path;
        public EditorBuildSettingsScene() {}
        public EditorBuildSettingsScene(string path, bool enabled) { this.path = path; this.enabled = enabled; }
    }
    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; } = new EditorBuildSettingsScene[0];
    }
    public static class EditorApplication
    {
        public static bool isBatchMode => true;
        public static bool isPlaying { get; set; }
        public static void Exit(int code) {}
    }
    public enum SerializedPropertyType { Generic = -1, Integer = 0, Boolean = 1, Float = 2, String = 3, Color = 4, ObjectReference = 5, Enum = 7 }
    public class SerializedProperty
    {
        public UnityEngine.Object objectReferenceValue { get; set; }
        public bool boolValue { get; set; }
        public int intValue { get; set; }
        public float floatValue { get; set; }
        public string stringValue { get; set; }
        public int enumValueIndex { get; set; }
        public int arraySize { get; set; }
        public bool isArray => true;
        public SerializedPropertyType propertyType { get; set; }
        public SerializedProperty GetArrayElementAtIndex(int index) => new SerializedProperty();
        public void InsertArrayElementAtIndex(int index) {}
        public void ClearArray() {}
    }
    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object obj) {}
        public SerializedProperty FindProperty(string path) => new SerializedProperty();
        public bool ApplyModifiedPropertiesWithoutUndo() => true;
        public bool ApplyModifiedProperties() => true;
        public void Update() {}
    }
    public class EditorWindow : ScriptableObject
    {
        public Vector2 minSize { get; set; }
        public static T GetWindow<T>(bool utility = false, string title = null, bool focus = true) where T : EditorWindow, new() => new T();
        public void Show() {}
        public void Repaint() {}
    }
    public enum MessageType { None, Info, Warning, Error }
    public static class EditorStyles
    {
        public static GUIStyle boldLabel => new GUIStyle();
        public static GUIStyle miniLabel => new GUIStyle();
        public static GUIStyle wordWrappedLabel => new GUIStyle();
        public static GUIStyle wordWrappedMiniLabel => new GUIStyle();
        public static GUIStyle helpBox => new GUIStyle();
        public static GUIStyle toolbarButton => new GUIStyle();
    }
    public static class EditorGUI
    {
        public class DisabledScope : IDisposable { public DisabledScope(bool disabled) {} public void Dispose() {} }
    }
    public static class EditorGUILayout
    {
        public class HorizontalScope : IDisposable { public HorizontalScope(params GUILayoutOption[] o) {} public void Dispose() {} }
        public class VerticalScope : IDisposable { public VerticalScope(params GUILayoutOption[] o) {} public VerticalScope(GUIStyle s, params GUILayoutOption[] o) {} public void Dispose() {} }
        public class ScrollViewScope : IDisposable { public Vector2 scrollPosition; public ScrollViewScope(Vector2 p, params GUILayoutOption[] o) { scrollPosition = p; } public void Dispose() {} }
        public static void LabelField(string label, params GUILayoutOption[] options) {}
        public static void LabelField(string label, GUIStyle style, params GUILayoutOption[] options) {}
        public static void LabelField(string label, string label2, params GUILayoutOption[] options) {}
        public static string TextField(string text, params GUILayoutOption[] options) => text;
        public static string TextField(string label, string text, params GUILayoutOption[] options) => text;
        public static string TextArea(string text, params GUILayoutOption[] options) => text;
        public static string TextArea(string text, GUIStyle style, params GUILayoutOption[] options) => text;
        public static bool Toggle(string label, bool value, params GUILayoutOption[] options) => value;
        public static bool ToggleLeft(string label, bool value, params GUILayoutOption[] options) => value;
        public static bool Foldout(bool foldout, string content, bool toggleOnLabelClick = false) => foldout;
        public static void HelpBox(string message, MessageType type) {}
        public static void Space() {}
        public static void Space(float width) {}
        public static Vector2 BeginScrollView(Vector2 pos, params GUILayoutOption[] options) => pos;
        public static void EndScrollView() {}
    }
    public static class Selection
    {
        public static GameObject activeGameObject { get; set; }
        public static UnityEngine.Object activeObject { get; set; }
    }
}

namespace UnityEditor.SceneManagement
{
    using UnityEngine.SceneManagement;
    public enum OpenSceneMode { Single, Additive, AdditiveWithoutLoading }
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }
    public static class EditorSceneManager
    {
        public static SceneAsset playModeStartScene { get; set; }
        public static Scene OpenScene(string path, OpenSceneMode mode = OpenSceneMode.Single) => new Scene();
        public static bool SaveScene(Scene scene) => true;
        public static bool SaveScene(Scene scene, string path) => true;
        public static void MarkSceneDirty(Scene scene) {}
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode = NewSceneMode.Single) => new Scene();
        public static bool CloseScene(Scene scene, bool removeScene) => true;
    }
}
