// Life From Zero — City/Quarter Unity-адаптер (ТЗ №023)
// Namespace: LifeFromZero.City.Quarter.UnityAdapter
//
// ЭТОТ файл — единственный в модуле City, зависящий от UnityEngine.
// Он превращает QuarterPlan (чистые данные) в GameObject-ы: примитивы,
// процедурные материалы и коллайдеры. Никаких платных ассетов и моделей:
// здания/деревья/фонари собираются из Cube/Cylinder-примитивов по
// размерам из плана. Для продакшена примитивы заменяются префабами
// через QuarterPrefabOverrides — генератор при этом не меняется.
//
// ВАЖНО: файл не компилировался и не запускался в Unity в рамках ТЗ №023
// (Unity-проект в репозитории отсутствует). Проверка в Unity — на стороне
// владельца проекта: см. City/README.md.

using System.Collections.Generic;
using UnityEngine;

namespace LifeFromZero.City.Quarter.UnityAdapter
{
    /// <summary>Необязательные префабы по TypeId. Нет префаба — строится примитив.</summary>
    [System.Serializable]
    public sealed class QuarterPrefabOverride
    {
        public string typeId;
        public GameObject prefab;
    }

    /// <summary>
    /// Строит квартал в сцене. Использование:
    ///   var plan = QuarterGenerator.Generate(seed);
    ///   new QuarterUnityAdapter().Build(plan, parentTransform);
    /// или повесить компонент на пустой объект и задать Seed в инспекторе.
    /// </summary>
    public sealed class QuarterUnityAdapter : MonoBehaviour
    {
        [Header("Генерация")]
        [SerializeField] private int seed = 1904;
        [SerializeField] private bool buildOnStart = true;
        [SerializeField] private bool addColliders = true;

        [Header("Префабы (необязательно; пусто = примитивы)")]
        [SerializeField] private List<QuarterPrefabOverride> prefabOverrides = new List<QuarterPrefabOverride>();

        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();

        private void Start()
        {
            if (buildOnStart)
                Build(QuarterGenerator.Generate(seed), transform);
        }

        /// <summary>Создаёт дочерние объекты по плану. Возвращает корень квартала.</summary>
        public GameObject Build(QuarterPlan plan, Transform parent)
        {
            if (plan == null) throw new System.ArgumentNullException(nameof(plan));

            _prefabs.Clear();
            foreach (QuarterPrefabOverride o in prefabOverrides)
                if (o != null && !string.IsNullOrEmpty(o.typeId) && o.prefab != null)
                    _prefabs[o.typeId] = o.prefab;

            var root = new GameObject("Quarter_seed" + plan.Seed);
            if (parent != null) root.transform.SetParent(parent, false);

            foreach (PlacedObject obj in plan.Objects)
                CreateObject(obj, root.transform);

            return root;
        }

        private void CreateObject(PlacedObject obj, Transform root)
        {
            GameObject go;
            if (_prefabs.TryGetValue(obj.TypeId, out GameObject prefab))
            {
                go = Instantiate(prefab, root);
            }
            else
            {
                go = CreatePrimitive(obj, root);
                if (go == null) return; // тип без примитива (служебный) — пропускаем
            }

            go.name = obj.TypeId + "_" + obj.Id;
            // План: позиция — центр следа на земле; объём — Size. Ставим центр по высоте.
            go.transform.localPosition = new Vector3(obj.Position.X, obj.Position.Y + obj.Size.Y / 2f, obj.Position.Z);
            go.transform.localRotation = Quaternion.Euler(0f, obj.RotationY, 0f);
            if (!_prefabs.ContainsKey(obj.TypeId))
                go.transform.localScale = ScaleFor(go, obj);

            if (addColliders && NeedsCollider(obj.TypeId) && go.GetComponent<Collider>() == null)
            {
                var collider = go.AddComponent<BoxCollider>();
                collider.size = Vector3.one; // примитив единичный, масштаб уже на трансформе
            }
        }

        private static bool NeedsCollider(string typeId) =>
            typeId == QuarterObjectTypes.Building || typeId == QuarterObjectTypes.Tree ||
            typeId == QuarterObjectTypes.StreetLamp || typeId == QuarterObjectTypes.YardLamp ||
            typeId == QuarterObjectTypes.Bench || typeId == QuarterObjectTypes.Playground;

        private static Vector3 ScaleFor(GameObject go, PlacedObject obj)
        {
            // Цилиндры Unity имеют высоту 2 и диаметр 1 — компенсируем для деревьев/фонарей.
            if (go.GetComponent<MeshFilter>() != null &&
                go.GetComponent<MeshFilter>().sharedMesh != null &&
                go.GetComponent<MeshFilter>().sharedMesh.name.Contains("Cylinder"))
                return new Vector3(obj.Size.X, obj.Size.Y / 2f, obj.Size.Z);
            return new Vector3(obj.Size.X, obj.Size.Y, obj.Size.Z);
        }

        private GameObject CreatePrimitive(PlacedObject obj, Transform root)
        {
            PrimitiveType primitive;
            switch (obj.TypeId)
            {
                case QuarterObjectTypes.Tree:
                case QuarterObjectTypes.StreetLamp:
                case QuarterObjectTypes.YardLamp:
                    primitive = PrimitiveType.Cylinder;
                    break;
                default:
                    primitive = PrimitiveType.Cube;
                    break;
            }

            var go = GameObject.CreatePrimitive(primitive);
            go.transform.SetParent(root, false);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = MaterialFor(obj.TypeId);
            // CreatePrimitive уже даёт коллайдер; примитивам дорог/разметки он не нужен.
            var existing = go.GetComponent<Collider>();
            if (existing != null && !NeedsCollider(obj.TypeId))
                Destroy(existing);
            return go;
        }

        /// <summary>Процедурные материалы URP/HDRP-совместимые через Standard-шейдер проекта по умолчанию.</summary>
        private Material MaterialFor(string typeId)
        {
            if (_materials.TryGetValue(typeId, out Material cached)) return cached;

            Color color;
            switch (typeId)
            {
                case QuarterObjectTypes.Road:
                case QuarterObjectTypes.Driveway:
                case QuarterObjectTypes.Intersection: color = new Color(0.16f, 0.16f, 0.17f); break;
                case QuarterObjectTypes.Sidewalk:
                case QuarterObjectTypes.ParkingLot: color = new Color(0.55f, 0.55f, 0.55f); break;
                case QuarterObjectTypes.Building: color = new Color(0.78f, 0.62f, 0.48f); break; // панель/кирпич
                case QuarterObjectTypes.Entrance: color = new Color(0.35f, 0.22f, 0.15f); break;
                case QuarterObjectTypes.Tree: color = new Color(0.22f, 0.48f, 0.22f); break;
                case QuarterObjectTypes.Lawn: color = new Color(0.35f, 0.6f, 0.3f); break;
                case QuarterObjectTypes.Playground: color = new Color(0.85f, 0.55f, 0.25f); break;
                case QuarterObjectTypes.Bench: color = new Color(0.45f, 0.3f, 0.18f); break;
                case QuarterObjectTypes.StreetLamp:
                case QuarterObjectTypes.YardLamp: color = new Color(0.25f, 0.25f, 0.28f); break;
                case QuarterObjectTypes.Crosswalk:
                case QuarterObjectTypes.CenterLine:
                case QuarterObjectTypes.StopLine:
                case QuarterObjectTypes.ParkingStall: color = new Color(0.92f, 0.92f, 0.9f); break;
                default: color = Color.magenta; break; // неизвестный тип виден сразу
            }

            // Shader.Find("Universal Render Pipeline/Lit") — для URP; в Built-in проекте заменить на "Standard".
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            _materials[typeId] = material;
            return material;
        }
    }
}
