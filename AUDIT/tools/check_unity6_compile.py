#!/usr/bin/env python3
"""По-сборочная проверка компиляции (.asmdef + Assembly-CSharp) настоящим Roslyn.

В отличие от плоской компиляции всех .cs в одну кучу, этот скрипт:
  1. Компилирует стабы движка в ОТДЕЛЬНЫЕ DLL:
       - UnityEngine.dll
       - UnityEngine.UI.dll
       - Unity.InputSystem.dll (подключается ТОЛЬКО если указана в references или autoReferenced=true для Assembly-CSharp!)
       - UnityEditor.dll
       - NUnit.dll
  2. Компилирует КАЖДЫЙ .asmdef репозитория в собственную .dll строго с его
     references, noEngineReferences и includePlatforms.
  3. Компилирует Assembly-CSharp (PauseKeyboardInput, City/UnityIntegration/Runtime,
     UnityIntegrationKit) поверх собранных .dll.
  4. Прогоняет оба режима ввода Unity 6000.3.27f1:
       - ENABLE_INPUT_SYSTEM (Input System Package)
       - ENABLE_LEGACY_INPUT_MANAGER (Old)
       - ENABLE_INPUT_SYSTEM + ENABLE_LEGACY_INPUT_MANAGER (Both)
"""
import glob
import json
import os
import re
import sys
import tempfile

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
sys.path.insert(0, os.path.join(REPO_ROOT, "UI", "Tools"))
from run import compile_to_image  # noqa: E402

INPUT_SYSTEM_STUB = """
using System;
using UnityEngine;
namespace UnityEngine.InputSystem
{
    public class KeyControl { public bool wasPressedThisFrame => false; public bool isPressed => false; }
    public class Keyboard { public static Keyboard current => null;
        public KeyControl pKey, mKey, escapeKey, enterKey, numpadEnterKey, iKey, jKey, eKey, tabKey, wKey, aKey, sKey, dKey, shiftKey, spaceKey; }
    public class Mouse { public static Mouse current => null;
        public KeyControl rightButton;
        public ControlVec2 delta, scroll, position; }
    public class ControlVec2 { public Vector2 ReadValue() => Vector2.zero; }
}
namespace UnityEngine.InputSystem.UI
{
    public class InputSystemUIInputModule : UnityEngine.EventSystems.BaseInputModule { }
}
"""

EXTRA_ENGINE_STUB = """
using System;
using System.Collections;
using System.Collections.Generic;
namespace UnityEngine
{
    public struct Quaternion {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x=x; this.y=y; this.z=z; this.w=w; }
        public static Quaternion identity => new Quaternion(0,0,0,1);
        public static Quaternion Euler(float x, float y, float z) => identity;
        public static Quaternion Euler(Vector3 v) => identity;
        public static Quaternion LookRotation(Vector3 f) => identity;
        public static Quaternion LookRotation(Vector3 f, Vector3 u) => identity;
        public Vector3 eulerAngles => Vector3.zero;
        public static Vector3 operator *(Quaternion q, Vector3 v) => v;
        public static Quaternion operator *(Quaternion a, Quaternion b) => identity;
    }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum LightType { Spot, Directional, Point, Area }
    public enum LightShadows { None, Hard, Soft }
    public class Shader : Object { public static Shader Find(string name) => new Shader(); }
    public class Material : Object {
        public Material(Shader s) {}
        public Material(Material m) {}
        public Color color { get; set; }
        public bool HasProperty(string n) => false;
        public void SetColor(string n, Color c) {}
        public void SetFloat(string n, float v) {}
        public void SetInt(string n, int v) {}
        public void EnableKeyword(string k) {}
    }
    public class Mesh : Object {
        public Vector3[] vertices { get; set; }
        public Vector3[] normals { get; set; }
        public Vector2[] uv { get; set; }
        public int[] triangles { get; set; }
        public void SetVertices(List<Vector3> v) {}
        public void SetTriangles(List<int> t, int sub) {}
        public void SetNormals(List<Vector3> n) {}
        public void SetUVs(int ch, List<Vector2> u) {}
        public void RecalculateNormals() {}
        public void RecalculateBounds() {}
    }
    public class MeshFilter : Component { public Mesh sharedMesh { get; set; } public Mesh mesh { get; set; } }
    public class Renderer : Component {
        public Material sharedMaterial { get; set; }
        public Material material { get; set; }
        public UnityEngine.Rendering.ShadowCastingMode shadowCastingMode { get; set; }
        public bool receiveShadows { get; set; }
    }
    public class MeshRenderer : Renderer { }
    public class Collider : Component { public bool isTrigger { get; set; } }
    public class BoxCollider : Collider { public Vector3 center { get; set; } public Vector3 size { get; set; } }
    public class SphereCollider : Collider { public Vector3 center { get; set; } public float radius { get; set; } }
    public class CapsuleCollider : Collider { public Vector3 center { get; set; } public float radius { get; set; } public float height { get; set; } }
    public class CharacterController : Collider { public bool enabled { get; set; } = true; }
    public class Rigidbody : Component {
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 linearVelocity { get; set; }
        public Vector3 angularVelocity { get; set; }
        public bool isKinematic { get; set; }
    }
    public class AsyncOperation { public bool isDone => true; public float progress => 1f; public event Action<AsyncOperation> completed; }
    public class TextMesh : Component {
        public string text { get; set; }
        public float characterSize { get; set; }
        public int fontSize { get; set; }
        public TextAnchor anchor { get; set; }
        public TextAlignment alignment { get; set; }
        public Color color { get; set; }
        public FontStyle fontStyle { get; set; }
    }
    public enum TextAlignment { Left, Center, Right }
    public class Light : Behaviour {
        public LightType type { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public float range { get; set; }
        public LightShadows shadows { get; set; }
    }
    public class ContextMenu : Attribute { public ContextMenu(string name) {} }
    public class ExecuteAlways : Attribute { }
    public class HideInInspector : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float min) {} }
    public class SpaceAttribute : Attribute { public SpaceAttribute(float h = 8f) {} }
    public class TextAreaAttribute : Attribute { public TextAreaAttribute(int a = 3, int b = 5) {} }
    public class Coroutine { }
    public static class ColorUtility {
        public static bool TryParseHtmlString(string html, out Color c) { c = Color.white; return true; }
    }
    public static class Gizmos {
        public static Color color { get; set; }
        public static void DrawWireSphere(Vector3 c, float r) {}
        public static void DrawSphere(Vector3 c, float r) {}
        public static void DrawLine(Vector3 a, Vector3 b) {}
        public static void DrawWireCube(Vector3 c, Vector3 s) {}
    }
}
namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
}
"""

NUNIT_STUB = """
using System;
using System.Collections;
namespace NUnit.Framework
{
    public class TestFixtureAttribute : Attribute {}
    public class TestAttribute : Attribute {}
    public class SetUpAttribute : Attribute {}
    public class TearDownAttribute : Attribute {}
    public static class Assert
    {
        public static void IsTrue(bool c, string m = null) {}
        public static void IsFalse(bool c, string m = null) {}
        public static void IsNotNull(object o, string m = null) {}
        public static void IsNull(object o, string m = null) {}
        public static void AreEqual(object a, object b, string m = null) {}
        public static void AreEqual(float a, float b, float d, string m = null) {}
        public static void AreSame(object a, object b, string m = null) {}
        public static void AreNotSame(object a, object b, string m = null) {}
        public static void AreNotEqual(object a, object b, string m = null) {}
        public static void Greater(float a, float b, string m = null) {}
        public static void Greater(int a, int b, string m = null) {}
        public static void Less(float a, float b, string m = null) {}
        public static void Less(int a, int b, string m = null) {}
        public static void LessOrEqual(float a, float b, string m = null) {}
        public static void GreaterOrEqual(float a, float b, string m = null) {}
        public static void Fail(string m = null) {}
        public static T Throws<T>(TestDelegate code, string m = null) where T : Exception => null;
    }
    public delegate void TestDelegate();
}
namespace UnityEngine.TestTools
{
    using System;
    public class UnityTestAttribute : Attribute {}
    public class UnitySetUpAttribute : Attribute {}
    public class UnityTearDownAttribute : Attribute {}
}
"""


def build_engine_stubs(tmpdir):
    base_stub = open(os.path.join(REPO_ROOT, "UI/Tools/unitystubs/UnityStubs.cs"), encoding="utf-8").read()
    # Удаляем встроенный namespace UnityEngine.InputSystem из базового UnityEngine.dll,
    # чтобы поймать любые скрытые обращения без ссылки на Unity.InputSystem!
    idx = base_stub.find("namespace UnityEngine.InputSystem")
    if idx >= 0:
        base_stub = base_stub[:idx]

    # Дополним Object.Instantiate, Color, GameObject.CreatePrimitive и Vector3 методы
    base_stub = base_stub.replace(
        "public static void DestroyImmediate(Object o) {}",
        "public static void DestroyImmediate(Object o) {}\n"
        "        public static T Instantiate<T>(T original) where T : Object => original;\n"
        "        public static T Instantiate<T>(T original, Transform parent) where T : Object => original;\n"
        "        public static T Instantiate<T>(T original, Transform parent, bool worldPositionStays) where T : Object => original;\n"
        "        public static T Instantiate<T>(T original, Vector3 pos, Quaternion rot) where T : Object => original;",
    )
    base_stub = base_stub.replace(
        "public static Color clear => new Color(0,0,0,0);",
        "public static Color clear => new Color(0,0,0,0);\n"
        "        public static Color magenta => new Color(1,0,1,1);\n"
        "        public static Color gray => new Color(0.5f,0.5f,0.5f,1);\n"
        "        public static Color grey => new Color(0.5f,0.5f,0.5f,1);\n"
        "        public static Color red => new Color(1,0,0,1);\n"
        "        public static Color green => new Color(0,1,0,1);\n"
        "        public static Color yellow => new Color(1,1,0,1);\n"
        "        public static Color cyan => new Color(0,1,1,1);",
    )
    base_stub = base_stub.replace(
        "public static GameObject FindGameObjectWithTag(string tag) => null;",
        "public static GameObject FindGameObjectWithTag(string tag) => null;\n"
        "        public static GameObject CreatePrimitive(PrimitiveType t) => new GameObject();\n"
        "        public int layer { get; set; }\n"
        "        public bool isStatic { get; set; }",
    )
    base_stub = base_stub.replace(
        "public Vector3 eulerAngles { get; set; }",
        "public Vector3 eulerAngles { get; set; }\n"
        "        public Quaternion rotation { get; set; }\n"
        "        public Quaternion localRotation { get; set; }\n"
        "        public Vector3 forward => Vector3.forward;\n"
        "        public Vector3 right => Vector3.right;\n"
        "        public Vector3 up => new Vector3(0,1,0);\n"
        "        public void LookAt(Vector3 t) {}\n"
        "        public void Rotate(float x, float y, float z) {}\n"
        "        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }",
    )
    base_stub = base_stub.replace(
        "public static Vector3 right => new Vector3(1,0,0);",
        "public static Vector3 right => new Vector3(1,0,0);\n"
        "        public static Vector3 up => new Vector3(0,1,0);\n"
        "        public static Vector3 down => new Vector3(0,-1,0);\n"
        "        public static Vector3 back => new Vector3(0,0,-1);\n"
        "        public static Vector3 left => new Vector3(-1,0,0);\n"
        "        public float sqrMagnitude => x*x+y*y+z*z;\n"
        "        public float magnitude => (float)Math.Sqrt(x*x+y*y+z*z);\n"
        "        public Vector3 normalized => this;\n"
        "        public void Normalize() {}\n"
        "        public static Vector3 Cross(Vector3 a, Vector3 b) => Vector3.zero;\n"
        "        public static float Dot(Vector3 a, Vector3 b) => a.x*b.x+a.y*b.y+a.z*b.z;\n"
        "        public static float Distance(Vector3 a, Vector3 b) => (a-b).magnitude;\n"
        "        public static Vector3 operator /(Vector3 a, float k) => new Vector3(a.x/k,a.y/k,a.z/k);\n"
        "        public static Vector3 operator *(float k, Vector3 a) => new Vector3(a.x*k,a.y*k,a.z*k);\n"
        "        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x,-a.y,-a.z);",
    )
    base_stub = base_stub.replace(
        "public static bool SetActiveScene(Scene scene) => true;",
        "public static bool SetActiveScene(Scene scene) => true;\n"
        "        public static Scene CreateScene(string name) => new Scene();\n"
        "        public static object UnloadSceneAsync(Scene scene) => null;\n"
        "        public static AsyncOperation LoadSceneAsync(string name) => new AsyncOperation();\n"
        "        public static AsyncOperation LoadSceneAsync(string name, LoadSceneMode mode) => new AsyncOperation();",
    )
    base_stub = base_stub.replace(
        "public enum KeyCode { P, M, Escape, I, J, Return, KeypadEnter, E, Tab, F1 }",
        "public enum KeyCode { P, M, Escape, I, J, Return, KeypadEnter, E, Tab, F1, W, A, S, D, Q, LeftShift, RightShift, Space, UpArrow, DownArrow, LeftArrow, RightArrow }",
    )

    ue_src = os.path.join(tmpdir, "UnityEngineStub.cs")
    ue_extra = os.path.join(tmpdir, "UnityEngineExtraStub.cs")
    open(ue_src, "w", encoding="utf-8").write(base_stub)
    open(ue_extra, "w", encoding="utf-8").write(EXTRA_ENGINE_STUB)
    ue_dll = os.path.join(tmpdir, "UnityEngine.dll")
    ok, errs, _, _ = compile_to_image([ue_src, ue_extra], out=ue_dll, asm_name="UnityEngine")
    if not ok:
        raise RuntimeError("UnityEngine stub failed: " + "\n".join(str(e) for e in errs))

    # UnityEngine.UI.dll как алиас-сборка (в стабах uGUI лежит внутри UnityEngine.dll)
    ugui_src = os.path.join(tmpdir, "UGuiStub.cs")
    open(ugui_src, "w", encoding="utf-8").write("namespace UnityEngine.UI { internal static class _Stub {} }")
    ugui_dll = os.path.join(tmpdir, "UnityEngine.UI.dll")
    ok, errs, _, _ = compile_to_image([ugui_src], extra_refs=[ue_dll], out=ugui_dll, asm_name="UnityEngine.UI")
    if not ok:
        raise RuntimeError("UGUI stub failed: " + "\n".join(str(e) for e in errs))

    is_src = os.path.join(tmpdir, "InputSystemStub.cs")
    open(is_src, "w", encoding="utf-8").write(INPUT_SYSTEM_STUB)
    is_dll = os.path.join(tmpdir, "Unity.InputSystem.dll")
    ok, errs, _, _ = compile_to_image([is_src], extra_refs=[ue_dll], out=is_dll, asm_name="Unity.InputSystem")
    if not ok:
        raise RuntimeError("InputSystem stub failed: " + "\n".join(str(e) for e in errs))

    ed_src = os.path.join(REPO_ROOT, "UI/Tools/editorstubs/EditorStubs.cs")
    ed_dll = os.path.join(tmpdir, "UnityEditor.dll")
    ok, errs, _, _ = compile_to_image([ed_src], extra_refs=[ue_dll], out=ed_dll, asm_name="UnityEditor")
    if not ok:
        raise RuntimeError("UnityEditor stub failed: " + "\n".join(str(e) for e in errs))

    nu_src = os.path.join(tmpdir, "NUnitStub.cs")
    open(nu_src, "w", encoding="utf-8").write(NUNIT_STUB)
    nu_dll = os.path.join(tmpdir, "nunit.framework.dll")
    ok, errs, _, _ = compile_to_image([nu_src], extra_refs=[ue_dll], out=nu_dll, asm_name="nunit.framework")
    if not ok:
        raise RuntimeError("NUnit stub failed: " + "\n".join(str(e) for e in errs))

    return {
        "UnityEngine": ue_dll,
        "UnityEngine.UI": ugui_dll,
        "Unity.InputSystem": is_dll,
        "UnityEditor": ed_dll,
        "NUnit": nu_dll,
    }


def collect_asmdefs():
    asmdefs = {}
    folder_to_asm = {}
    for f in sorted(glob.glob(os.path.join(REPO_ROOT, "**/*.asmdef"), recursive=True)):
        rel = os.path.relpath(f, REPO_ROOT).replace("\\", "/")
        data = json.load(open(f, encoding="utf-8"))
        folder = os.path.dirname(rel)
        asmdefs[data["name"]] = {"path": rel, "data": data, "folder": folder, "files": []}
        folder_to_asm[folder] = data["name"]

    cs_without_asmdef = []
    for f in sorted(glob.glob(os.path.join(REPO_ROOT, "**/*.cs"), recursive=True)):
        rel = os.path.relpath(f, REPO_ROOT).replace("\\", "/")
        if "/Tools/" in rel or "/bin/" in rel or "/obj/" in rel:
            continue
        d = os.path.dirname(rel)
        matched = None
        while True:
            if d in folder_to_asm:
                matched = folder_to_asm[d]
                break
            if not d or "/" not in d:
                break
            d = os.path.dirname(d)
        if matched:
            asmdefs[matched]["files"].append(os.path.join(REPO_ROOT, rel))
        else:
            cs_without_asmdef.append(rel)

    return asmdefs, cs_without_asmdef


def topo_sort(asmdefs):
    order = []
    visited = set()

    def visit(name):
        if name in visited or name not in asmdefs:
            return
        visited.add(name)
        for r in asmdefs[name]["data"].get("references", []):
            visit(r)
        order.append(name)

    for n in sorted(asmdefs):
        visit(n)
    return order


def run_profile(profile_name, base_defines, stubs, asmdefs, cs_without_asmdef):
    with tempfile.TemporaryDirectory() as outdir:
        built = {}
        order = topo_sort(asmdefs)
        for name in order:
            info = asmdefs[name]
            data = info["data"]
            files = info["files"]
            if not files:
                continue

            defines = list(base_defines)
            is_editor = data.get("includePlatforms") == ["Editor"]
            is_test = "TestAssemblies" in data.get("optionalUnityReferences", []) or "UNITY_INCLUDE_TESTS" in data.get("defineConstraints", [])
            if is_editor or is_test:
                defines.append("UNITY_EDITOR")
            if is_test:
                defines.append("UNITY_INCLUDE_TESTS")

            refs = []
            if not data.get("noEngineReferences", False):
                refs.append(stubs["UnityEngine"])
                if is_editor:
                    refs.append(stubs["UnityEditor"])
                if is_test:
                    refs.append(stubs["NUnit"])

            for r in data.get("references", []):
                if r == "UnityEngine.UI":
                    refs.append(stubs["UnityEngine.UI"])
                elif r == "Unity.InputSystem":
                    refs.append(stubs["Unity.InputSystem"])
                elif r in built:
                    refs.append(built[r])
                elif r in ("UnityEngine.TestRunner", "UnityEditor.TestRunner"):
                    refs.append(stubs["NUnit"])
                else:
                    raise RuntimeError(f"{name}: неизвестная ссылка {r}")

            dll_path = os.path.join(outdir, f"{name}.dll")
            ok, errs, _, _ = compile_to_image(files, extra_refs=refs, out=dll_path, defines=defines, asm_name=name)
            if not ok:
                print(f"[FAIL] {profile_name} :: {name} ({info['path']})")
                for e in errs[:20]:
                    loc = e.Location.GetLineSpan()
                    print(f"  {e.Id}: {e.GetMessage()} @ {loc.Path}:{loc.StartLinePosition.Line + 1}")
                return False
            built[name] = dll_path

        # Компилируем runtime-скрипты Assembly-CSharp (без /Editor/)
        runtime_loose = [
            os.path.join(REPO_ROOT, r)
            for r in cs_without_asmdef
            if "/Editor/" not in r
        ]
        if runtime_loose:
            ac_refs = [
                stubs["UnityEngine"],
                stubs["UnityEngine.UI"],
                stubs["Unity.InputSystem"],
            ] + [
                dll
                for asm_name, dll in built.items()
                if asmdefs[asm_name]["data"].get("includePlatforms") != ["Editor"]
                and "TestAssemblies" not in asmdefs[asm_name]["data"].get("optionalUnityReferences", [])
            ]
            ac_dll = os.path.join(outdir, "Assembly-CSharp.dll")
            ok, errs, _, _ = compile_to_image(
                runtime_loose,
                extra_refs=ac_refs,
                out=ac_dll,
                defines=list(base_defines),
                asm_name="Assembly-CSharp",
            )
            if not ok:
                print(f"[FAIL] {profile_name} :: Assembly-CSharp")
                for e in errs[:20]:
                    loc = e.Location.GetLineSpan()
                    print(f"  {e.Id}: {e.GetMessage()} @ {loc.Path}:{loc.StartLinePosition.Line + 1}")
                return False

        # Компилируем editor-скрипты Assembly-CSharp-Editor (/Editor/ без .asmdef)
        editor_loose = [
            os.path.join(REPO_ROOT, r)
            for r in cs_without_asmdef
            if "/Editor/" in r
        ]
        if editor_loose:
            ed_refs = [
                stubs["UnityEngine"],
                stubs["UnityEngine.UI"],
                stubs["Unity.InputSystem"],
                stubs["UnityEditor"],
            ] + [
                dll
                for asm_name, dll in built.items()
                if "TestAssemblies" not in asmdefs[asm_name]["data"].get("optionalUnityReferences", [])
            ]
            if runtime_loose:
                ed_refs.append(ac_dll)
            ed_ac_dll = os.path.join(outdir, "Assembly-CSharp-Editor.dll")
            ok, errs, _, _ = compile_to_image(
                editor_loose,
                extra_refs=ed_refs,
                out=ed_ac_dll,
                defines=list(base_defines) + ["UNITY_EDITOR"],
                asm_name="Assembly-CSharp-Editor",
            )
            if not ok:
                print(f"[FAIL] {profile_name} :: Assembly-CSharp-Editor")
                for e in errs[:25]:
                    loc = e.Location.GetLineSpan()
                    print(f"  {e.Id}: {e.GetMessage()} @ {loc.Path}:{loc.StartLinePosition.Line + 1}")
                return False

        print(f"[PASS] {profile_name}: собрано {len(built)} .asmdef + Assembly-CSharp ({len(runtime_loose)}) + Assembly-CSharp-Editor ({len(editor_loose)}), 0 ошибок")
        return True


def main():
    asmdefs, cs_without_asmdef = collect_asmdefs()
    with tempfile.TemporaryDirectory() as stubdir:
        stubs = build_engine_stubs(stubdir)
        profiles = [
            ("Unity 6000.3.27f1 [InputSystem]", ["UNITY_2023_1_OR_NEWER", "UNITY_6000_0_OR_NEWER", "ENABLE_INPUT_SYSTEM"]),
            ("Unity 6000.3.27f1 [LegacyInput]", ["UNITY_2023_1_OR_NEWER", "UNITY_6000_0_OR_NEWER", "ENABLE_LEGACY_INPUT_MANAGER"]),
            ("Unity 6000.3.27f1 [Both]", ["UNITY_2023_1_OR_NEWER", "UNITY_6000_0_OR_NEWER", "ENABLE_INPUT_SYSTEM", "ENABLE_LEGACY_INPUT_MANAGER"]),
        ]
        for pname, pdefs in profiles:
            if not run_profile(pname, pdefs, stubs, asmdefs, cs_without_asmdef):
                sys.exit(1)


if __name__ == "__main__":
    main()
