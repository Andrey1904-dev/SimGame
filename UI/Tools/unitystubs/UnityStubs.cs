
// Minimal compile-only stubs of the Unity API surface used by UI/Runtime.
// Used ONLY to type-check UI scripts in this sandbox (no Unity runtime).
// Not shipped, not compiled in Unity.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Vector2 {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0,0);
        public static Vector2 one => new Vector2(1,1);
        public float sqrMagnitude => x*x+y*y;
        public float magnitude => (float)Math.Sqrt(x*x+y*y);
        public Vector2 normalized => this;
        public static Vector2 operator /(Vector2 a, float k) => new Vector2(a.x/k, a.y/k);
        public static Vector2 operator *(float k, Vector2 a) => new Vector2(a.x*k, a.y*k);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static float Distance(Vector2 a, Vector2 b) => (a-b).magnitude;
        public static Vector2 operator *(Vector2 a, float k) => new Vector2(a.x*k, a.y*k);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x-b.x, a.y-b.y);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x+b.x, a.y+b.y);
    }
    public struct Vector3 {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0,0,0);
        public static Vector3 one => new Vector3(1,1,1);
        public static Vector3 forward => new Vector3(0,0,1);
        public static Vector3 right => new Vector3(1,0,0);
        public static Vector3 operator *(Vector3 a, float k) => new Vector3(a.x*k,a.y*k,a.z*k);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
    }
    public struct Vector4 { public float x,y,z,w; public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;} }
    public struct Rect {
        public float x,y,width,height;
        public Rect(float x,float y,float w,float h){this.x=x;this.y=y;width=w;height=h;}
        public float xMin => x;
        public float xMax => x+width;
        public float yMin => y;
        public float yMax => y+height;
        public float center => 0f;
    }
    public struct Color {
        public float r,g,b,a;
        public Color(float r,float g,float b,float a=1f){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Color white => new Color(1,1,1,1);
        public static Color black => new Color(0,0,0,1);
        public static Color clear => new Color(0,0,0,0);
        public static Color Lerp(Color a, Color b, float t) => a;
        public static Color HSVToRGB(float h, float s, float v) => new Color(h,s,v,1f);
        public static bool operator ==(Color a, Color b) => a.r==b.r && a.g==b.g && a.b==b.b && a.a==b.a;
        public static bool operator !=(Color a, Color b) => !(a==b);
        public override bool Equals(object o) => o is Color c && this == c;
        public override int GetHashCode() => 0;
        public static Color operator *(Color c, float k) => new Color(c.r*k,c.g*k,c.b*k,c.a);
    }
    public struct Color32 {
        public byte r,g,b,a;
        public Color32(byte r,byte g,byte b,byte a){this.r=r;this.g=g;this.b=b;this.a=a;}
    }
    public static class Mathf {
        public const float PI = 3.14159265f;
        public const float Infinity = float.PositiveInfinity;
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static int RoundToInt(float v) => (int)Math.Round(v);
        public static float Round(float v) => (float)Math.Round(v);
        public static int Max(int a, int b) => Math.Max(a,b);
        public static float Max(float a, float b) => Math.Max(a,b);
        public static float Max(float a, float b, float c) => Math.Max(a, Math.Max(b,c));
        public static int Min(int a, int b) => Math.Min(a,b);
        public static float Min(float a, float b) => Math.Min(a,b);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Abs(float v) => Math.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y,x);
        public static float Acos(float v) => (float)Math.Acos(v);
        public static float Floor(float v) => (float)Math.Floor(v);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static float Lerp(float a, float b, float t) => a + (b-a)*Clamp01(t);
        public static float MoveTowards(float a, float b, float d) => a;
        public static float Repeat(float t, float len) => t;
        public static float Sign(float v) => v < 0 ? -1 : 1;
    }
    public enum FindObjectsInactive { Exclude, Include }
    public class Object {
        public string name;
        public int GetInstanceID() => 0;
        public static void Destroy(Object o) {}
        public static void DestroyImmediate(Object o) {}
        public static T FindAnyObjectByType<T>() where T : Object => null;
        public static T FindFirstObjectByType<T>() where T : Object => null;
        public static T FindFirstObjectByType<T>(FindObjectsInactive inactive) where T : Object => null;
        public static T FindObjectOfType<T>() where T : Object => null;
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T : Object => new T[0];
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a,b);
        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a,b);
        public override bool Equals(object o) => ReferenceEquals(this,o);
        public override int GetHashCode() => base.GetHashCode();
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
    }
    public class GameObject : Object {
        public GameObject() {}
        public GameObject(string n) { name = n; }
        public GameObject(string n, params Type[] t) { name = n; }
        public static GameObject FindWithTag(string tag) => null;
        public static GameObject FindGameObjectWithTag(string tag) => null;
        public Transform transform { get; } = new Transform();
        public T AddComponent<T>() where T : Component, new() => new T();
        public Component AddComponent(Type t) => (Component)Activator.CreateInstance(t);
        public T GetComponent<T>() => default(T);
        public T GetComponentInChildren<T>(bool includeInactive = false) => default(T);
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) => new T[0];
        public Component GetComponentInChildren(Type type, bool includeInactive = false) => null;
        public T GetComponentInParent<T>() => default(T);
        public bool activeInHierarchy => true;
        public bool activeSelf => true;
        public string tag { get; set; } = "Untagged";
        public void SetActive(bool v) {}
    }
    public class AudioListener : Behaviour {
        public static float volume { get; set; } = 1f;
    }
    public enum CameraClearFlags { Skybox = 1, Color = 2, SolidColor = 2, Depth = 3, Nothing = 4 }
    public static class Screen {
        public static bool fullScreen { get; set; }
        public static int width => 1920;
        public static int height => 1080;
    }
    public class Camera : Behaviour {
        public static Camera main => null;
        public new Transform transform => gameObject.transform;
        public bool orthographic { get; set; }
        public float orthographicSize { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
    }
    public class Component : Object {
        public T GetComponentInChildren<T>(bool includeInactive = false) => default(T);
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) => new T[0];
        public Component GetComponentInChildren(Type type, bool includeInactive = false) => null;
        public T GetComponentInParent<T>() => default(T);
        public T[] GetComponents<T>() => new T[0];
        public GameObject gameObject { get; } = new GameObject();
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component, new() => new T();
        public bool isActiveAndEnabled => true;
        public bool enabled { get; set; } = true;
        public string tag { get; set; }
    }
    public class Behaviour : Component { public new bool enabled { get; set; } = true; }
    public enum FindObjectsSortMode { None, InstanceID }
    public class MonoBehaviour : Behaviour {
        public static T FindAnyObjectByType<T>() where T : Object => null;
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T : Object => new T[0];
        public static void DontDestroyOnLoad(Object target) {}
        public static void Destroy(Object o) {}
        public static void DestroyImmediate(Object o) {}
    }
    public class Transform : Component, IEnumerable<Transform> {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Vector3 localScale { get; set; } = Vector3.one;
        public Vector3 localEulerAngles { get; set; }
        public Vector3 eulerAngles { get; set; }
        public Transform parent { get; set; }
        public int childCount => 0;
        public void SetParent(Transform p, bool worldPositionStays) { parent = p; }
        public Transform GetChild(int i) => null;
        public Transform Find(string n) => null;
        public void SetAsLastSibling() {}
        public void SetSiblingIndex(int i) {}
        public IEnumerator<Transform> GetEnumerator() { yield break; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { yield break; }
    }
    public class RectTransform : Transform {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Rect rect => new Rect(0,0,100,100);
        public void SetSizeWithCurrentAnchors(Axis axis, float size) {}
        public enum Axis { Horizontal, Vertical }
    }
    public class Texture : Object { public int width, height; public void Apply(bool a, bool b) {} }
    public class Texture2D : Texture {
        public Texture2D(int w, int h, TextureFormat f, bool mip) { width = w; height = h; }
        public TextureWrapMode wrapMode { get; set; }
        public FilterMode filterMode { get; set; }
        public void SetPixels32(Color32[] p) {}
        public void Apply() {}
        public new void Apply(bool a, bool b) {}
    }
    public enum TextureFormat { RGBA32 }
    public enum TextureWrapMode { Clamp, Repeat }
    public enum FilterMode { Point, Bilinear }
    public enum SpriteMeshType { FullRect, Tight }
    public class Sprite : Object {
        public Vector4 border { get; }
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu = 100f) => new Sprite();
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu, uint ext, SpriteMeshType m) => new Sprite();
        public static Sprite Create(Texture2D t, Rect r, Vector2 pivot, float ppu, uint ext, SpriteMeshType m, Vector4 b) => new Sprite();
    }
    public class Font : Object { }
    public static class Resources {
        public static T GetBuiltinResource<T>(string path) where T : Object => null;
    }
    public class Canvas : Behaviour {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
        public bool overrideSorting { get; set; }
    }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public class CanvasGroup : Behaviour { public float alpha = 1f; public bool interactable = true; public bool blocksRaycasts = true; }
    public static class Application {
        public static bool isPlaying => true;
        public static void Quit() {}
        public static bool CanStreamedLevelBeLoaded(string s) => true;
        public static bool isBatchMode => true;
        public static string dataPath => "/tmp/project/Assets";
    }
    public static class Debug {
        public static void Log(object m) {}
        public static void LogWarning(object m) {}
        public static void LogError(object m) {}
    }
    public static class PlayerPrefs {
        public static bool HasKey(string key) => false;
        public static void SetFloat(string key, float value) {}
        public static float GetFloat(string key, float fallback) => fallback;
        public static void Save() {}
    }
    public static class Time {
        public static float timeScale { get; set; } = 1f;
        public static float deltaTime => 0.016f;
        public static float unscaledDeltaTime => 0.016f;
        public static float time => 0f;
    }
    public static class Cursor {
        public static bool visible { get; set; } = true;
        public static CursorLockMode lockState { get; set; }
    }
    public enum CursorLockMode { None, Locked, Confined }
    public enum KeyCode { P, M, Escape, I, J, Return, KeypadEnter, E, Tab, F1 }
    public static class Input {
        public static bool GetKeyDown(KeyCode k) => false;
        public static bool GetKey(KeyCode k) => false;
        public static bool GetMouseButtonDown(int b) => false;
        public static bool GetMouseButton(int b) => false;
        public static bool GetMouseButtonUp(int b) => false;
        public static Vector3 mousePosition => Vector3.zero;
        public static Vector2 mouseScrollDelta => Vector2.zero;
        public static float GetAxis(string a) => 0f;
    }
    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic, BoldItalic }
    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }
    public enum HorizontalWrapMode { Wrap, Overflow }
    public enum VerticalWrapMode { Truncate, Overflow }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrderAttribute : Attribute { public DefaultExecutionOrderAttribute(int order) {} }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeFieldAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string h) {} }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string h) {} }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float a, float b) {} }
    [AttributeUsage(AttributeTargets.Class)] public sealed class RequireComponentAttribute : Attribute { public RequireComponentAttribute(Type t) {} }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponentAttribute : Attribute { }
    public class WaitForEndOfFrame { }
    public class WaitForSeconds { public WaitForSeconds(float s) {} }
    public class WaitForSecondsRealtime { public WaitForSecondsRealtime(float s) {} }
    public class RenderTexture : Texture {
        public static RenderTexture GetTemporary(int w, int h, int depth) => new RenderTexture();
        public static void ReleaseTemporary(RenderTexture t) {}
    }
}
namespace UnityEngine.UI
{
    using UnityEngine;
    public class Graphic : Behaviour {
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
        public RectTransform rectTransform => new RectTransform();
        public virtual void SetAllDirty() {}
        public void SetVerticesDirty() {}
        public void SetLayoutDirty() {}
        protected virtual void OnPopulateMesh(VertexHelper vh) {}
        public void SetMaterialDirty() {}
    }
    public class VertexHelper {
        public int currentVertCount => 0;
        public void Clear() {}
        public void AddVert(Vector3 pos, Color32 color, Vector2 uv0) {}
        public void AddVert(Vector2 pos, Color color, Vector2 uv0) {}
        public void AddTriangle(int a, int b, int c) {}
    }
    public class MaskableGraphic : Graphic { }
    public class Image : MaskableGraphic {
        public Sprite sprite { get; set; }
        public Type type { get; set; } = Type.Simple;
        public bool preserveAspect { get; set; }
        public FillMethod fillMethod { get; set; }
        public int fillOrigin { get; set; }
        public float fillAmount { get; set; } = 1f;
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }
    }
    public class RawImage : MaskableGraphic { public Texture texture { get; set; } }
    public class Text : MaskableGraphic {
        public Font font { get; set; }
        public string text { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public bool supportRichText { get; set; }
        public bool resizeTextForBestFit { get; set; }
        public int resizeTextMinSize { get; set; }
        public int resizeTextMaxSize { get; set; }
        public float lineSpacing { get; set; }
    }
    public class Selectable : Behaviour {
        public bool interactable { get; set; } = true;
        public Graphic targetGraphic { get; set; }
        public Navigation navigation { get; set; }
        public ColorBlock colors { get; set; }
    }
    public struct Navigation { public Mode mode; public enum Mode { None, Horizontal, Vertical, Automatic } }
    public struct ColorBlock {
        public Color normalColor, highlightedColor, pressedColor, selectedColor, disabledColor;
        public float colorMultiplier, fadeDuration;
    }
    public class Button : Selectable {
        public ButtonClickedEvent onClick { get; } = new ButtonClickedEvent();
        public class ButtonClickedEvent : UnityEngine.Events.UnityEvent { }
    }
    public class Toggle : Selectable { public bool isOn { get; set; } }
    public class Slider : Selectable { public float value { get; set; } }
    public class ScrollRect : Behaviour {
        public RectTransform content { get; set; }
        public RectTransform viewport { get; set; }
        public bool horizontal { get; set; }
        public bool vertical { get; set; }
        public Vector2 normalizedPosition { get; set; }
        public float verticalNormalizedPosition { get; set; }
        public MovementType movementType { get; set; }
        public enum MovementType { Unrestricted, Elastic, Clamped }
    }
    public class Mask : Behaviour { public bool showMaskGraphic { get; set; } }
    public class RectMask2D : Behaviour { }
    public class LayoutElement : Behaviour { public float preferredHeight { get; set; } public float minHeight { get; set; } }
    public class ContentSizeFitter : Behaviour { public FitMode verticalFit { get; set; } public enum FitMode { Unconstrained, MinSize, PreferredSize } }
    public class HorizontalLayoutGroup : Behaviour { public float spacing { get; set; } public RectOffset padding { get; set; } }
    public class VerticalLayoutGroup : Behaviour { public float spacing { get; set; } public RectOffset padding { get; set; } }
    public class GridLayoutGroup : Behaviour { public Vector2 cellSize { get; set; } public Vector2 spacing { get; set; } public int constraintCount { get; set; } }
    public class RectOffset { public RectOffset(int l, int r, int t, int b) {} }
    public class CanvasScaler : Behaviour {
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public ScreenMatchMode screenMatchMode { get; set; }
        public float matchWidthOrHeight { get; set; }
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public enum ScreenMatchMode { MatchWidthOrHeight, Expand, Shrink }
    }
    public class GraphicRaycaster : Behaviour { }
    public class AspectRatioFitter : Behaviour { public AspectMode aspectMode { get; set; } public float aspectRatio { get; set; }
        public enum AspectMode { None, WidthControlsHeight, HeightControlsWidth, FitInParent, EnvelopeParent } }
    public class LayoutRebuilder { public static void ForceRebuildLayoutImmediate(RectTransform r) {} }
}
namespace UnityEngine.Events
{
    public class UnityEventBase { public int GetPersistentEventCount() => 0; }
    public class UnityEvent : UnityEventBase {
        public void AddListener(UnityAction call) {}
        public void RemoveAllListeners() {}
        public void Invoke() {}
    }
    public class UnityEvent<T> : UnityEventBase { public void AddListener(UnityAction<T> call) {} }
    public delegate void UnityAction();
    public delegate void UnityAction<T>(T arg);
}
namespace UnityEngine.EventSystems
{
    using UnityEngine;
    public class EventSystem :Behaviour { public static EventSystem current => null; public void SetSelectedGameObject(GameObject go) {} }
    public class BaseInputModule : Behaviour { }
    public class StandaloneInputModule : BaseInputModule { }
    public class PointerEventData {
        public Vector2 delta { get; set; }
        public Vector2 position { get; set; }
        public Vector2 scrollDelta { get; set; }
    }
    public interface IPointerClickHandler { void OnPointerClick(PointerEventData e); }
    public interface IPointerEnterHandler { void OnPointerEnter(PointerEventData e); }
    public interface IPointerExitHandler { void OnPointerExit(PointerEventData e); }
    public interface IDragHandler { void OnDrag(PointerEventData e); }
    public interface IBeginDragHandler { void OnBeginDrag(PointerEventData e); }
    public interface IEndDragHandler { void OnEndDrag(PointerEventData e); }
    public interface IScrollHandler { void OnScroll(PointerEventData e); }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name { get; set; } public string path { get; set; } public bool isLoaded { get; set; } public bool isDirty { get; set; } public bool IsValid() => true;
        public GameObject[] GetRootGameObjects() => new GameObject[0]; }
    public enum LoadSceneMode { Single, Additive }
    public static class SceneManager {
        public static event Action<Scene, LoadSceneMode> sceneLoaded;
        public static void LoadScene(string name) {}
        public static void LoadScene(string name, LoadSceneMode mode) {}
        public static Scene GetActiveScene() => new Scene();
        public static Scene GetSceneByPath(string path) => new Scene();
        public static int sceneCount => 1;
        public static Scene GetSceneAt(int i) => new Scene();
        public static void MoveGameObjectToScene(GameObject go, Scene scene) {}
        public static bool SetActiveScene(Scene scene) => true;
    }
}
namespace UnityEngine.InputSystem
{
    public class KeyControl { public bool wasPressedThisFrame => false; public bool isPressed => false; }
    public class Keyboard { public static Keyboard current => null;
        public KeyControl pKey, mKey, escapeKey, enterKey, numpadEnterKey, iKey, jKey, eKey, tabKey; }
    public class Mouse { public static Mouse current => null; public Vector2 scrollDelta => Vector2.zero; public Vector2 position => Vector2.zero; }
}
