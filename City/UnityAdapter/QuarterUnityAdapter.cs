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

        /// <summary>Seed генерации (то же поле, что в Inspector). Для CityBootstrap и кода.</summary>
        public int Seed { get => seed; set => seed = value; }
        /// <summary>Строить ли квартал автоматически в Start (то же поле, что в Inspector).</summary>
        public bool BuildOnStart { get => buildOnStart; set => buildOnStart = value; }
        /// <summary>Ставить ли коллайдеры (то же поле, что в Inspector).</summary>
        public bool AddColliders { get => addColliders; set => addColliders = value; }
        /// <summary>Текущий построенный корень квартала (null, если не построен или очищен).</summary>
        public GameObject BuiltRoot => _builtRoot;

        /// <summary>
        /// Уничтожает построенный квартал и его меши деталей. Материалы кэша
        /// принадлежат адаптеру и освобождаются в OnDestroy, а не здесь.
        /// </summary>
        public void ClearBuilt()
        {
            if (_builtRoot != null)
            {
                Destroy(_builtRoot);
                _builtRoot = null;
            }
            foreach (Mesh mesh in _createdMeshes)
                if (mesh != null) Destroy(mesh);
            _createdMeshes.Clear();
        }

        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        private readonly Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>();
        // Меши деталей зданий создаются кодом и не уничтожаются вместе с
        // GameObject — держим список и освобождаем в OnDestroy (аудит №026).
        private readonly List<Mesh> _createdMeshes = new List<Mesh>();

        private void OnDestroy()
        {
            // Корень мог быть построен под чужим родителем — уничтожаем явно,
            // чтобы квартал не остался сиротой после уничтожения адаптера.
            if (_builtRoot != null) Destroy(_builtRoot);
            foreach (Mesh mesh in _createdMeshes)
                if (mesh != null) Destroy(mesh);
            _createdMeshes.Clear();
            foreach (Material material in _materials.Values)
                if (material != null) Destroy(material);
            _materials.Clear();
        }

        private void Start()
        {
            if (buildOnStart)
                Build(QuarterGenerator.Generate(seed), transform);
        }

        // Последний построенный этим адаптером квартал. Повторный Build()
        // уничтожает его вместе с мешами деталей, а не накладывает новый
        // квартал поверх старого (аудит ТЗ №027). Нужны два квартала в одной
        // сцене — используйте два компонента-адаптера.
        private GameObject _builtRoot;

        /// <summary>Создаёт дочерние объекты по плану. Возвращает корень квартала.</summary>
        public GameObject Build(QuarterPlan plan, Transform parent)
        {
            if (plan == null) throw new System.ArgumentNullException(nameof(plan));

            // Повторный Build: предыдущий квартал и его меши уничтожаются.
            ClearBuilt();

            _prefabs.Clear();
            foreach (QuarterPrefabOverride o in prefabOverrides)
                if (o != null && !string.IsNullOrEmpty(o.typeId) && o.prefab != null)
                    _prefabs[o.typeId] = o.prefab;

            var root = new GameObject("Quarter_seed" + plan.Seed);
            _builtRoot = root;
            if (parent != null) root.transform.SetParent(parent, false);

            foreach (PlacedObject obj in plan.Objects)
                CreateObject(obj, root.transform);

            return root;
        }

        private void CreateObject(PlacedObject obj, Transform root)
        {
            // Подъезд жилого дома — не куб-заглушка, а дверь + козырёк + номер.
            if (obj.TypeId == QuarterObjectTypes.Entrance && !_prefabs.ContainsKey(obj.TypeId))
            {
                BuildEntrance(obj, root);
                return;
            }
            // Железная дверь подвала — отдельный компонент на петлях.
            if (obj.TypeId == QuarterObjectTypes.BasementDoor && !_prefabs.ContainsKey(obj.TypeId))
            {
                BuildBasementDoor(obj, root);
                return;
            }

            GameObject go;
            bool isPrefab = _prefabs.TryGetValue(obj.TypeId, out GameObject prefab);
            if (isPrefab)
            {
                go = Instantiate(prefab, root);
            }
            else
            {
                go = CreatePrimitive(obj, root);
                if (go == null) return; // тип без примитива (служебный) — пропускаем
            }

            go.name = obj.TypeId + "_" + obj.Id;
            if (obj.TypeId == DistrictObjectTypes.Sign)
                AddSignText(go, obj.Param("text", ""));
            else if (obj.TypeId == QuarterObjectTypes.PickupSign || obj.TypeId == QuarterObjectTypes.ShopSign)
                AddSignText(go, obj.Param("text",
                    obj.TypeId == QuarterObjectTypes.ShopSign ? GroceryShopInfo.SignText : PickupPointInfo.SignText));
            // План: позиция — центр следа на земле; объём — Size. Ставим центр по высоте.
            go.transform.localPosition = new Vector3(obj.Position.X, obj.Position.Y + obj.Size.Y / 2f, obj.Position.Z);
            go.transform.localRotation = obj.TypeId == QuarterObjectTypes.BasementPipe
                ? Quaternion.Euler(0f, obj.RotationY, 0f) * Quaternion.Euler(0f, 0f, 90f) // труба: цилиндр вдоль корпуса
                : Quaternion.Euler(0f, obj.RotationY, 0f);
            if (!isPrefab)
                go.transform.localScale = ScaleFor(go, obj);

            if (obj.TypeId == QuarterObjectTypes.Tree && !isPrefab)
                AddTreeCanopy(go);
            if (obj.TypeId == QuarterObjectTypes.Building && !isPrefab)
                BuildBuildingDetails(obj, root);
            if (obj.TypeId != QuarterObjectTypes.BasementLamp)
                go.isStatic = true; // статичная геометрия: Unity сможет батчить; материалы и меши примитивов общие

            if (addColliders && NeedsCollider(obj.TypeId) && go.GetComponent<Collider>() == null)
            {
                var collider = go.AddComponent<BoxCollider>();
                collider.size = Vector3.one; // примитив единичный, масштаб уже на трансформе
            }
        }

        /// <summary>Подъезд: дверь у стены, козырёк над ней, номер подъезда текстом.</summary>
        private void BuildEntrance(PlacedObject obj, Transform root)
        {
            float nx = ParseFloat(obj.Param("normalX"), 0f);
            float nz = ParseFloat(obj.Param("normalZ"), 1f);
            var normal = new Vector3(nx, 0f, nz);
            if (normal.sqrMagnitude < 0.01f) normal = Vector3.forward;
            normal.Normalize();
            Vector3 wallPoint = new Vector3(obj.Position.X, 0f, obj.Position.Z) - normal * 0.35f;
            Quaternion facing = Quaternion.LookRotation(normal, Vector3.up);
            string suffix = "_" + obj.Id;

            string entranceStyle = obj.Param("entranceStyle", "flat-canopy");
            string doorColor = obj.Param("doorColor");

            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "entrance.door" + suffix;
            door.transform.SetParent(root, false);
            door.transform.localPosition = wallPoint + normal * 0.06f + Vector3.up * 1.05f;
            door.transform.localRotation = facing;
            door.transform.localScale = new Vector3(1.1f, 2.1f, 0.1f);
            door.GetComponent<Renderer>().sharedMaterial = MaterialFromHex(doorColor, "entrance.door");
            door.isStatic = true;

            var canopy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            canopy.name = "entrance.canopy" + suffix;
            canopy.transform.SetParent(root, false);
            canopy.transform.localPosition = wallPoint + normal * 0.55f + Vector3.up * 2.3f;
            canopy.transform.localRotation = entranceStyle == "sloped-canopy"
                ? facing * Quaternion.Euler(-18f, 0f, 0f) // наклонный козырёк
                : facing;
            canopy.transform.localScale = new Vector3(1.9f, 0.1f, 1.1f);
            canopy.GetComponent<Renderer>().sharedMaterial = MaterialFor("entrance.canopy");
            canopy.isStatic = true;
            var canopyCollider = canopy.GetComponent<Collider>();
            if (canopyCollider != null) Destroy(canopyCollider);

            if (entranceStyle == "porch")
            {
                // Входная группа с крыльцом: две стойки по краям козырька.
                Vector3 side = Vector3.Cross(Vector3.up, normal).normalized;
                for (int s = -1; s <= 1; s += 2)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    post.name = "entrance.porch-post" + suffix;
                    post.transform.SetParent(root, false);
                    post.transform.localPosition = wallPoint + normal * 1.0f + side * s * 0.85f + Vector3.up * 1.15f;
                    post.transform.localScale = new Vector3(0.09f, 2.3f, 0.09f);
                    post.GetComponent<Renderer>().sharedMaterial = MaterialFor("entrance.canopy");
                    post.isStatic = true;
                    var postCollider = post.GetComponent<Collider>();
                    if (postCollider != null) Destroy(postCollider);
                }
            }

            string number = obj.Param("entranceNumber");
            if (!string.IsNullOrEmpty(number))
            {
                var textObject = new GameObject("entrance.number" + suffix);
                textObject.transform.SetParent(root, false);
                textObject.transform.localPosition = wallPoint + normal * 0.2f + Vector3.up * 2.55f;
                textObject.transform.localRotation = facing;
                var mesh = textObject.AddComponent<TextMesh>();
                mesh.text = number;
                mesh.fontSize = 48;
                mesh.characterSize = 0.3f;
                mesh.anchor = TextAnchor.MiddleCenter;
                mesh.alignment = TextAlignment.Center;
                mesh.color = Color.white;
            }
        }

        /// <summary>
        /// Железная дверь подвала: петля по левому краю, компонент BasementDoor
        /// поворачивает дверь по состоянию контроллера из ядра.
        /// </summary>
        private void BuildBasementDoor(PlacedObject obj, Transform root)
        {
            var pivot = new GameObject(obj.TypeId + "_" + obj.Id);
            pivot.transform.SetParent(root, false);
            Quaternion rot = Quaternion.Euler(0f, obj.RotationY, 0f);
            Vector3 center = new Vector3(obj.Position.X, obj.Position.Y + obj.Size.Y / 2f, obj.Position.Z);
            Vector3 axisX = rot * Vector3.right;
            pivot.transform.localPosition = center - axisX * (obj.Size.X / 2f); // край-петля
            pivot.transform.localRotation = rot;

            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "basement.door.leaf";
            door.transform.SetParent(pivot.transform, false);
            door.transform.localPosition = new Vector3(obj.Size.X / 2f, 0f, 0f);
            door.transform.localScale = new Vector3(obj.Size.X, obj.Size.Y, obj.Size.Z);
            door.GetComponent<Renderer>().sharedMaterial = MaterialFor(QuarterObjectTypes.BasementDoor);
            // Коллайдер от CreatePrimitive остаётся и едет вместе с дверью на петле.

            var component = pivot.AddComponent<BasementDoor>();
            component.Configure(new BasementDoorController(BasementDoorState.Locked));
        }

        private static float ParseFloat(string value, float fallback)
        {
            return float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
        }

        private static bool NeedsCollider(string typeId) =>
            // Дороги/проезды/тротуары — земля, по которой ходит игрок: без их
            // коллайдеров персонаж проваливался бы сквозь покрытие (аудит №026).
            typeId == QuarterObjectTypes.Road || typeId == QuarterObjectTypes.Driveway ||
            typeId == QuarterObjectTypes.Intersection || typeId == QuarterObjectTypes.Sidewalk ||
            // Районная карта: коммерция, завод и труба — твёрдые; острова, колонки, киоск и стойки АЗС тоже непроходимы.
            // Навес без коллайдера.
            typeId == DistrictObjectTypes.Commerce || typeId == DistrictObjectTypes.IndustryBuilding ||
            typeId == DistrictObjectTypes.IndustryChimney ||
            // Круговое кольцо и площадка погрузки намеренно БЕЗ коллайдера: проходимы (как в Python-зеркале),
            // их примитивные коллайдеры снимаются в CreatePrimitive.
            typeId == DistrictObjectTypes.GasIsland ||
            typeId == DistrictObjectTypes.GasPump || typeId == DistrictObjectTypes.GasKiosk ||
            typeId == DistrictObjectTypes.GasPost ||
            // Открытая земля квартала: газоны, дорожки, парковки и площадки —
            // плоские тонкие боксы, по ним персонаж ходит между дорогами
            // (аудит ТЗ №027). Разметка/места поверх них коллайдеров не получают.
            typeId == QuarterObjectTypes.Lawn || typeId == QuarterObjectTypes.Footpath ||
            typeId == QuarterObjectTypes.PrivatePath || typeId == QuarterObjectTypes.PrivateGarden ||
            typeId == QuarterObjectTypes.ParkingLot || typeId == QuarterObjectTypes.PickupPlaza ||
            typeId == QuarterObjectTypes.ShopPlaza ||
            typeId == QuarterObjectTypes.Building || typeId == QuarterObjectTypes.Tree ||
            typeId == QuarterObjectTypes.StreetLamp || typeId == QuarterObjectTypes.YardLamp ||
            typeId == QuarterObjectTypes.Bench || typeId == QuarterObjectTypes.Playground ||
            typeId == QuarterObjectTypes.PickupBuilding || typeId == QuarterObjectTypes.ShopBuilding ||
            typeId == QuarterObjectTypes.BasementWall || typeId == QuarterObjectTypes.BasementSecondDoor ||
            typeId == QuarterObjectTypes.BasementFloor || typeId == QuarterObjectTypes.BasementStep ||
            typeId == QuarterObjectTypes.EliteLobby || typeId == QuarterObjectTypes.PrivateGarage ||
            typeId == QuarterObjectTypes.PrivateFence ||
            typeId == QuarterObjectTypes.BasementShelf || typeId == QuarterObjectTypes.BasementTable ||
            typeId == QuarterObjectTypes.TrashContainer || typeId == QuarterObjectTypes.Swing ||
            typeId == QuarterObjectTypes.Sandbox;

        /// <summary>
        /// Текст вывески ПВЗ: встроенный TextMesh (без ассетов и шрифтов из пакетов).
        /// Ребёнок таблички; локальный масштаб компенсирует масштаб родителя,
        /// текст смотрит по локальному +Z — генератор поворачивает вывеску фасадом к улице.
        /// </summary>
        private static void AddSignText(GameObject sign, string text)
        {
            var textObject = new GameObject("SignText");
            textObject.transform.SetParent(sign.transform, false);
            textObject.transform.localPosition = new Vector3(0f, 0f, 0.6f);
            textObject.transform.localScale = new Vector3(0.16f, 1.1f, 1f);
            var mesh = textObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.fontSize = 64;
            mesh.characterSize = 0.35f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
        }

        private static Vector3 ScaleFor(GameObject go, PlacedObject obj)
        {
            // Труба: цилиндр повёрнут вокруг Z, длина Size.X — это его высота.
            if (obj.TypeId == QuarterObjectTypes.BasementPipe)
                return new Vector3(obj.Size.Z, obj.Size.X / 2f, obj.Size.Z);
            // Цилиндры Unity имеют высоту 2 и диаметр 1 — компенсируем для деревьев/фонарей.
            if (go.GetComponent<MeshFilter>() != null &&
                go.GetComponent<MeshFilter>().sharedMesh != null &&
                go.GetComponent<MeshFilter>().sharedMesh.name.Contains("Cylinder"))
                return new Vector3(obj.Size.X, obj.Size.Y / 2f, obj.Size.Z);
            return new Vector3(obj.Size.X, obj.Size.Y, obj.Size.Z);
        }

        /// <summary>Крона дерева — сфера-ребёнок ствола-цилиндра (масштаб компенсирует масштаб родителя).</summary>
        private void AddTreeCanopy(GameObject trunk)
        {
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            canopy.name = "tree.canopy";
            canopy.transform.SetParent(trunk.transform, false);
            canopy.transform.localPosition = new Vector3(0f, 0.38f, 0f);
            canopy.transform.localScale = new Vector3(1.05f, 0.42f, 1.05f);
            canopy.GetComponent<Renderer>().sharedMaterial = MaterialFor("tree.canopy");
            var collider = canopy.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
        }

        /// <summary>
        /// Детализация жилого дома ОДНИМ объектом на вид детали (не сотни кубов):
        /// окна, балконы с ограждениями, панельные швы и крыша собираются в
        /// объединённые меши с общими материалами. Количество окон и балконов
        /// зависит от этажности и длины корпуса из данных генератора.
        /// </summary>
        private void BuildBuildingDetails(PlacedObject obj, Transform root)
        {
            int floors = Mathf.RoundToInt(ParseFloat(obj.Param("floors"), 5f));
            if (floors < 1) floors = 1;
            string style = obj.Param("style", "panel");
            string roofVariant = obj.Param("roofVariant", obj.Param("roof", "flat"));
            string windowStyle = obj.Param("windowStyle", "standard");
            string balconyType = obj.Param("balconyType", "open");
            int balconyOffset = Mathf.RoundToInt(ParseFloat(obj.Param("balconyOffset"), 0f));
            float wear = ParseFloat(obj.Param("wear"), 0.4f);
            float length = obj.Size.X, height = obj.Size.Y, depth = obj.Size.Z;
            float floorHeight = height / floors;

            Vector2 windowSize;
            switch (windowStyle)
            {
                case "wide": windowSize = new Vector2(1.7f, 1.2f); break;
                case "tall": windowSize = new Vector2(1.1f, 1.6f); break;
                default: windowSize = new Vector2(1.3f, 1.35f); break;
            }

            var windows = new MeshBuilder();
            var frames = new MeshBuilder();
            var concrete = new MeshBuilder();
            var seams = new MeshBuilder();
            var extras = new MeshBuilder();

            int perFloor = Mathf.FloorToInt(length / 3f);
            int perFloorParam = Mathf.RoundToInt(ParseFloat(obj.Param("windowsPerFloor"), 0f));
            if (perFloorParam > 0) perFloor = perFloorParam;
            if (perFloor < 2) perFloor = 2;

            var patchSet = new HashSet<string>(SplitList(obj.Param("patchWindows")));
            var acSet = new HashSet<string>(SplitList(obj.Param("acUnits")));

            for (int floor = 0; floor < floors; floor++)
            {
                float y = 1.55f + floor * floorHeight;
                if (y > height - 0.7f) break;
                for (int i = 0; i < perFloor; i++)
                {
                    float x = -length / 2f + (i + 0.5f) * (length / perFloor);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float facadeZ = side * (depth / 2f);
                        // Рама чуть больше стекла и выступает меньше: окно читается как окно, а не пятно.
                        frames.AddBox(new Vector3(x, y, facadeZ + side * 0.03f),
                            new Vector3(windowSize.x + 0.16f, windowSize.y + 0.16f, 0.05f));
                        windows.AddBox(new Vector3(x, y, facadeZ + side * 0.055f),
                            new Vector3(windowSize.x, windowSize.y, 0.06f));
                        string key = floor + ":" + i + ":" + side;
                        if (patchSet.Contains(key))
                            seams.AddBox(new Vector3(x, y, facadeZ + side * 0.045f),
                                new Vector3(windowSize.x + 0.3f, windowSize.y + 0.3f, 0.04f)); // отремонтированный участок фасада
                        if (acSet.Contains(key))
                            extras.AddBox(new Vector3(x, y - windowSize.y / 2f - 0.45f, facadeZ + side * 0.22f),
                                new Vector3(0.75f, 0.55f, 0.28f)); // кондиционер под окном

                        // Балконы/лоджии со второго этажа; рисунок сдвинут на balconyOffset.
                        if (floor >= 1 && balconyType != "none" && (i + balconyOffset) % 3 == 1)
                        {
                            float slabY = y - 0.82f;
                            if (balconyType == "loggia")
                            {
                                concrete.AddBox(new Vector3(x, slabY + floorHeight / 2f - 0.4f, facadeZ + side * 0.5f),
                                    new Vector3(2.1f, 0.14f, 1.05f)); // плита лоджии (потолок)
                                concrete.AddBox(new Vector3(x - 1.02f, y + 0.2f, facadeZ + side * 0.5f),
                                    new Vector3(0.09f, floorHeight - 0.3f, 1.05f)); // боковые стены лоджии
                                concrete.AddBox(new Vector3(x + 1.02f, y + 0.2f, facadeZ + side * 0.5f),
                                    new Vector3(0.09f, floorHeight - 0.3f, 1.05f));
                                concrete.AddBox(new Vector3(x, slabY + 0.45f, facadeZ + side * 1.0f),
                                    new Vector3(2.1f, 0.9f, 0.08f)); // ограждение лоджии
                                concrete.AddBox(new Vector3(x, slabY, facadeZ + side * 0.5f),
                                    new Vector3(2.1f, 0.12f, 1.05f));
                            }
                            else if (balconyType == "glazed")
                            {
                                concrete.AddBox(new Vector3(x, slabY, facadeZ + side * 0.5f), new Vector3(2f, 0.12f, 1f)); // плита
                                windows.AddBox(new Vector3(x, slabY + 0.85f, facadeZ + side * 0.98f),
                                    new Vector3(1.9f, 1.55f, 0.05f)); // остекление спереди
                                windows.AddBox(new Vector3(x - 0.98f, slabY + 0.85f, facadeZ + side * 0.5f),
                                    new Vector3(0.05f, 1.55f, 0.95f));
                                windows.AddBox(new Vector3(x + 0.98f, slabY + 0.85f, facadeZ + side * 0.5f),
                                    new Vector3(0.05f, 1.55f, 0.95f));
                                concrete.AddBox(new Vector3(x, slabY + 1.68f, facadeZ + side * 0.5f),
                                    new Vector3(2f, 0.1f, 1f)); // крыша застеклённого балкона
                            }
                            else // open
                            {
                                concrete.AddBox(new Vector3(x, slabY, facadeZ + side * 0.5f), new Vector3(2f, 0.12f, 1f));
                                concrete.AddBox(new Vector3(x, slabY + 0.48f, facadeZ + side * 0.98f), new Vector3(2f, 0.85f, 0.05f));
                                concrete.AddBox(new Vector3(x - 0.98f, slabY + 0.48f, facadeZ + side * 0.5f), new Vector3(0.05f, 0.85f, 1f));
                                concrete.AddBox(new Vector3(x + 0.98f, slabY + 0.48f, facadeZ + side * 0.5f), new Vector3(0.05f, 0.85f, 1f));
                            }
                        }
                    }
                }
            }

            // Панельные швы панельных домов.
            if (style == "panel")
            {
                for (int floor = 1; floor < floors; floor++)
                {
                    float y = floor * floorHeight;
                    for (int side = -1; side <= 1; side += 2)
                        seams.AddBox(new Vector3(0f, y, side * (depth / 2f + 0.02f)), new Vector3(length, 0.05f, 0.03f));
                }
                for (float x = -length / 2f + 3f; x < length / 2f - 0.5f; x += 3f)
                    for (int side = -1; side <= 1; side += 2)
                        seams.AddBox(new Vector3(x, height / 2f, side * (depth / 2f + 0.02f)), new Vector3(0.05f, height, 0.03f));
            }

            // Водосточные трубы по торцам фасадов.
            foreach (string part in SplitList(obj.Param("drainpipes")))
            {
                if (!float.TryParse(part, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float pipeX)) continue;
                for (int side = -1; side <= 1; side += 2)
                    seams.AddBox(new Vector3(pipeX, height / 2f, side * (depth / 2f + 0.14f)),
                        new Vector3(0.13f, height, 0.13f));
            }

            // Износ: потемнение цоколя; у старых домов — и потёки под крышей.
            if (wear > 0.3f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    seams.AddBox(new Vector3(0f, 0.4f, side * (depth / 2f + 0.03f)), new Vector3(length, 0.8f, 0.04f));
                    if (wear > 0.6f)
                        seams.AddBox(new Vector3(0f, height - 0.35f, side * (depth / 2f + 0.03f)), new Vector3(length, 0.7f, 0.04f));
                }
            }

            // Технадстройка (выход на крышу/лифтовая) на плоских крышах домов от 5 этажей.
            if (floors >= 5 && (roofVariant == "flat" || roofVariant == "flat-parapet"))
                concrete.AddBox(new Vector3(length / 4f, height + 0.6f, 0f), new Vector3(4f, 1.2f, 3f));

            // Крыши по вариантам.
            var roofBuilder = new MeshBuilder();
            switch (roofVariant)
            {
                case "pitched":
                    roofBuilder.AddPrism(new Vector3(0f, height, 0f), length + 0.5f, depth + 0.6f, Mathf.Min(2.6f, depth * 0.28f));
                    break;
                case "pitched-steep":
                    roofBuilder.AddPrism(new Vector3(0f, height, 0f), length + 0.5f, depth + 0.6f, Mathf.Min(3.8f, depth * 0.42f));
                    break;
                case "mansard":
                    roofBuilder.AddPrism(new Vector3(0f, height, 0f), length + 0.4f, depth + 0.5f, 1.5f);
                    roofBuilder.AddBox(new Vector3(0f, height + 1.6f, 0f), new Vector3(length * 0.72f, 0.22f, depth * 0.55f));
                    break;
                case "flat-parapet":
                    roofBuilder.AddBox(new Vector3(0f, height + 0.12f, 0f), new Vector3(length + 0.4f, 0.25f, depth + 0.4f));
                    roofBuilder.AddBox(new Vector3(0f, height + 0.45f, depth / 2f + 0.1f), new Vector3(length + 0.4f, 0.5f, 0.18f));
                    roofBuilder.AddBox(new Vector3(0f, height + 0.45f, -depth / 2f - 0.1f), new Vector3(length + 0.4f, 0.5f, 0.18f));
                    roofBuilder.AddBox(new Vector3(length / 2f + 0.1f, height + 0.45f, 0f), new Vector3(0.18f, 0.5f, depth + 0.4f));
                    roofBuilder.AddBox(new Vector3(-length / 2f - 0.1f, height + 0.45f, 0f), new Vector3(0.18f, 0.5f, depth + 0.4f));
                    break;
                default: // flat
                    roofBuilder.AddBox(new Vector3(0f, height + 0.12f, 0f), new Vector3(length + 0.4f, 0.25f, depth + 0.4f));
                    break;
            }

            CreateDetailMesh(windows, "detail.windows", obj, root, MaterialFor("detail.glass"));
            CreateDetailMesh(frames, "detail.frames", obj, root, MaterialFor("detail.frame"));
            CreateDetailMesh(concrete, "detail.balconies", obj, root, MaterialFor("detail.concrete"));
            CreateDetailMesh(seams, "detail.seams", obj, root, MaterialFor("detail.seam"));
            CreateDetailMesh(extras, "detail.extras", obj, root, MaterialFor("detail.ac"));
            CreateDetailMesh(roofBuilder, "detail.roof", obj, root,
                MaterialFor(roofVariant == "flat" || roofVariant == "flat-parapet" ? "detail.roof.flat" : "detail.roof.pitched"));
        }

        private static string[] SplitList(string value)
        {
            if (string.IsNullOrEmpty(value)) return new string[0];
            return value.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        private void CreateDetailMesh(MeshBuilder builder, string name, PlacedObject building, Transform root, Material material)
        {
            Mesh mesh = builder.Build();
            if (mesh == null) return;
            _createdMeshes.Add(mesh);
            var go = new GameObject(name + "_" + building.Id);
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(building.Position.X, building.Position.Y, building.Position.Z);
            go.transform.localRotation = Quaternion.Euler(0f, building.RotationY, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.isStatic = true;
        }

        /// <summary>Накопитель боксов/призм в один меш: общие материалы, минимум draw calls.</summary>
        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<int> _triangles = new List<int>();

            public void AddBox(Vector3 center, Vector3 size)
            {
                Vector3 h = size / 2f;
                // 6 граней: направление, два касательных направления.
                AddFace(center, new Vector3(0, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 1, 0), h);
                AddFace(center, new Vector3(0, 0, -1), new Vector3(-1, 0, 0), new Vector3(0, 1, 0), h);
                AddFace(center, new Vector3(1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 1, 0), h);
                AddFace(center, new Vector3(-1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0), h);
                AddFace(center, new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1), h);
                AddFace(center, new Vector3(0, -1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1), h);
            }

            private void AddFace(Vector3 center, Vector3 normal, Vector3 tangent, Vector3 bitangent, Vector3 half)
            {
                float n = Vector3.Dot(normal, half);
                float t = Vector3.Dot(tangent, half);
                float b = Vector3.Dot(bitangent, half);
                // Модули полуразмеров по осям граней.
                float ht = Mathf.Abs(t), hb = Mathf.Abs(b);
                Vector3 c = center + normal * Mathf.Abs(n);
                int start = _vertices.Count;
                _vertices.Add(c - tangent * ht - bitangent * hb);
                _vertices.Add(c + tangent * ht - bitangent * hb);
                _vertices.Add(c + tangent * ht + bitangent * hb);
                _vertices.Add(c - tangent * ht + bitangent * hb);
                for (int i = 0; i < 4; i++) _normals.Add(normal);
                _triangles.Add(start); _triangles.Add(start + 1); _triangles.Add(start + 2);
                _triangles.Add(start); _triangles.Add(start + 2); _triangles.Add(start + 3);
            }

            /// <summary>Скатная крыша: конёк вдоль X на высоте height над baseCenter.</summary>
            public void AddPrism(Vector3 baseCenter, float length, float width, float height)
            {
                float x = length / 2f, z = width / 2f, y0 = baseCenter.y, y1 = baseCenter.y + height;
                var a = new Vector3(baseCenter.x - x, y0, baseCenter.z - z);
                var b = new Vector3(baseCenter.x + x, y0, baseCenter.z - z);
                var c = new Vector3(baseCenter.x + x, y0, baseCenter.z + z);
                var d = new Vector3(baseCenter.x - x, y0, baseCenter.z + z);
                var r1 = new Vector3(baseCenter.x - x, y1, baseCenter.z);
                var r2 = new Vector3(baseCenter.x + x, y1, baseCenter.z);
                AddQuad(a, b, r2, r1); // южный скат
                AddQuad(c, d, r1, r2); // северный скат
                // Фронтоны — настоящие треугольники без вырожденных индексов
                // (аудит ТЗ №027). Порядок вершин даёт внешнюю нормаль.
                AddTriangle(a, d, r1); // западный фронтон (нормаль -X)
                AddTriangle(b, r2, c); // восточный фронтон (нормаль +X)
                AddQuad(a, b, c, d);   // низ (нормаль вниз)
            }

            private void AddTriangle(Vector3 p0, Vector3 p1, Vector3 p2)
            {
                Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
                if (normal.sqrMagnitude < 0.0001f) return;
                normal.Normalize();
                int start = _vertices.Count;
                _vertices.Add(p0); _vertices.Add(p1); _vertices.Add(p2);
                for (int i = 0; i < 3; i++) _normals.Add(normal);
                _triangles.Add(start); _triangles.Add(start + 1); _triangles.Add(start + 2);
            }

            private void AddQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
            {
                Vector3 normal = Vector3.Cross(p1 - p0, p3 - p0);
                if (normal.sqrMagnitude < 0.0001f) return;
                normal.Normalize();
                int start = _vertices.Count;
                _vertices.Add(p0); _vertices.Add(p1); _vertices.Add(p2); _vertices.Add(p3);
                for (int i = 0; i < 4; i++) _normals.Add(normal);
                _triangles.Add(start); _triangles.Add(start + 1); _triangles.Add(start + 2);
                _triangles.Add(start); _triangles.Add(start + 2); _triangles.Add(start + 3);
            }

            public Mesh Build()
            {
                if (_triangles.Count == 0) return null;
                var mesh = new Mesh { name = "QuarterDetail" };
                mesh.SetVertices(_vertices);
                mesh.SetNormals(_normals);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private GameObject CreatePrimitive(PlacedObject obj, Transform root)
        {
            PrimitiveType primitive;
            switch (obj.TypeId)
            {
                case QuarterObjectTypes.Tree:
                case QuarterObjectTypes.StreetLamp:
                case QuarterObjectTypes.YardLamp:
                case QuarterObjectTypes.Urn:
                case QuarterObjectTypes.PickupPoint: // плоский диск-маркер точки взаимодействия
                case QuarterObjectTypes.ShopPoint:
                case QuarterObjectTypes.DeliveryDropoff:
                case QuarterObjectTypes.PlayerHomeExit:
                case QuarterObjectTypes.BasementPoint:
                case DistrictObjectTypes.Roundabout: // круглый островок кольца
                case DistrictObjectTypes.IndustryChimney: // дымовая труба
                    primitive = PrimitiveType.Cylinder;
                    break;
                case QuarterObjectTypes.Bush:
                    primitive = PrimitiveType.Sphere;
                    break;
                default:
                    primitive = PrimitiveType.Cube;
                    break;
            }

            var go = GameObject.CreatePrimitive(primitive);
            go.transform.SetParent(root, false);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = MaterialForObject(obj);
            // CreatePrimitive уже даёт коллайдер; примитивам дорог/разметки он не нужен.
            var existing = go.GetComponent<Collider>();
            if (existing != null && !NeedsCollider(obj.TypeId))
                Destroy(existing);
            return go;
        }

        /// <summary>Материал объекта: у жилых домов — по цвету фасада из данных (кэш по цвету).</summary>
        private Material MaterialForObject(PlacedObject obj)
        {
            // Стеклянный фасад ЖК проверяем ДО цвета: у элитных башен тоже есть
            // facadeColor, и раньше ветка glass была недостижима (аудит ТЗ №026).
            if (obj.TypeId == QuarterObjectTypes.Building && obj.Param("style") == "glass")
                return MaterialFor("detail.glass"); // стеклянный фасад элитной высотки целиком
            if (obj.TypeId == QuarterObjectTypes.Building || obj.TypeId == DistrictObjectTypes.Commerce ||
                obj.TypeId == DistrictObjectTypes.IndustryBuilding)
            {
                string hex = obj.Param("facadeColor");
                if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color facade))
                {
                    string key = "facade:" + hex;
                    if (!_materials.TryGetValue(key, out Material cachedFacade))
                    {
                        cachedFacade = CreateMaterial(facade);
                        _materials[key] = cachedFacade;
                    }
                    return cachedFacade;
                }
            }
            if (obj.TypeId == QuarterObjectTypes.PrivateFence)
                return MaterialFor(obj.Param("fenceStyle") == "wood" ? "fence.wood" : "fence.metal");
            if (obj.TypeId == QuarterObjectTypes.Tree)
                return MaterialFor("tree.trunk");
            if (obj.TypeId == QuarterObjectTypes.Bench)
                return MaterialFor(obj.Param("benchStyle") == "new" ? "bench.new" : "bench.old");
            if (obj.TypeId == QuarterObjectTypes.YardFence)
                return MaterialFor(obj.Param("fenceStyle") == "wood" ? "fence.wood" : "fence.metal");
            if (obj.TypeId == QuarterObjectTypes.Playground)
            {
                string variant = obj.Param("playgroundVariant");
                return MaterialFor(variant == "2" ? "playground.2" : variant == "3" ? "playground.3" : "playground.1");
            }
            return MaterialFor(obj.TypeId);
        }

        /// <summary>Материал по hex-цвету из данных (кэш по цвету, не по объекту).</summary>
        private Material MaterialFromHex(string hex, string fallbackKey)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color parsed))
            {
                string key = "hex:" + hex;
                if (!_materials.TryGetValue(key, out Material cached))
                {
                    cached = CreateMaterial(parsed);
                    _materials[key] = cached;
                }
                return cached;
            }
            return MaterialFor(fallbackKey);
        }

        private Material CreateMaterial(Color color)
        {
            // Shader.Find("Universal Render Pipeline/Lit") — для URP; в Built-in проекте заменить на "Standard".
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return new Material(shader) { color = color };
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
                case QuarterObjectTypes.PickupBuilding: color = new Color(0.88f, 0.86f, 0.82f); break;
                case QuarterObjectTypes.PickupRoof: color = new Color(0.3f, 0.3f, 0.32f); break;
                case QuarterObjectTypes.PickupDoor: color = new Color(0.15f, 0.3f, 0.55f); break;
                case QuarterObjectTypes.PickupWindow: color = new Color(0.6f, 0.8f, 0.95f); break;
                case QuarterObjectTypes.PickupSign: color = new Color(0.95f, 0.45f, 0.1f); break; // оранжевая вывеска
                case QuarterObjectTypes.PickupPlaza: color = new Color(0.6f, 0.6f, 0.6f); break;
                case QuarterObjectTypes.PickupPoint: color = new Color(0.95f, 0.6f, 0.1f); break; // маркер взаимодействия
                case QuarterObjectTypes.ShopBuilding: color = new Color(0.82f, 0.78f, 0.7f); break;
                case QuarterObjectTypes.ShopRoof: color = new Color(0.3f, 0.3f, 0.32f); break;
                case QuarterObjectTypes.ShopDoor: color = new Color(0.2f, 0.45f, 0.3f); break;
                case QuarterObjectTypes.ShopWindow: color = new Color(0.6f, 0.8f, 0.95f); break;
                case QuarterObjectTypes.ShopSign: color = new Color(0.2f, 0.65f, 0.3f); break; // зелёная вывеска «ПРОДУКТЫ»
                case QuarterObjectTypes.ShopPlaza: color = new Color(0.6f, 0.6f, 0.6f); break;
                case QuarterObjectTypes.ShopPoint: color = new Color(0.2f, 0.8f, 0.35f); break;
                case QuarterObjectTypes.DeliveryDropoff: color = new Color(0.2f, 0.5f, 0.95f); break; // синий маркер доставки
                case QuarterObjectTypes.PlayerHomeDoor: color = new Color(0.4f, 0.25f, 0.12f); break;
                case QuarterObjectTypes.PlayerHomeExit: color = new Color(0.95f, 0.85f, 0.2f); break;
                case QuarterObjectTypes.PlayerHomeMarker: color = new Color(0.95f, 0.85f, 0.2f); break;
                case "entrance.door": color = new Color(0.35f, 0.22f, 0.15f); break;
                case "entrance.canopy": color = new Color(0.5f, 0.5f, 0.52f); break;
                case "tree.trunk": color = new Color(0.4f, 0.28f, 0.18f); break;
                case "tree.canopy": color = new Color(0.22f, 0.5f, 0.22f); break;
                case "detail.glass": color = new Color(0.55f, 0.72f, 0.88f); break;
                case "detail.frame": color = new Color(0.92f, 0.9f, 0.85f); break;
                case "detail.ac": color = new Color(0.88f, 0.88f, 0.86f); break;
                case "bench.new": color = new Color(0.55f, 0.38f, 0.2f); break;
                case "bench.old": color = new Color(0.38f, 0.3f, 0.24f); break;
                case "fence.wood": color = new Color(0.5f, 0.38f, 0.24f); break;
                case "fence.metal": color = new Color(0.45f, 0.5f, 0.48f); break;
                case "playground.1": color = new Color(0.85f, 0.55f, 0.25f); break;
                case "playground.2": color = new Color(0.3f, 0.55f, 0.75f); break;
                case "playground.3": color = new Color(0.45f, 0.65f, 0.35f); break;
                case QuarterObjectTypes.RoadPatch: color = new Color(0.1f, 0.1f, 0.11f); break;
                case QuarterObjectTypes.EliteLobby: color = new Color(0.85f, 0.83f, 0.78f); break;
                case QuarterObjectTypes.EliteCanopy: color = new Color(0.25f, 0.27f, 0.3f); break;
                case QuarterObjectTypes.PrivateGarage: color = new Color(0.55f, 0.55f, 0.57f); break;
                case QuarterObjectTypes.PrivateGate: color = new Color(0.35f, 0.38f, 0.4f); break;
                case QuarterObjectTypes.PrivatePath: color = new Color(0.62f, 0.6f, 0.56f); break;
                case QuarterObjectTypes.PrivateGarden: color = new Color(0.3f, 0.55f, 0.28f); break;
                case QuarterObjectTypes.YardFence: color = new Color(0.5f, 0.38f, 0.24f); break;
                case "detail.concrete": color = new Color(0.75f, 0.75f, 0.73f); break;
                case "detail.seam": color = new Color(0.45f, 0.44f, 0.42f); break;
                case "detail.roof.flat": color = new Color(0.32f, 0.32f, 0.34f); break;
                case "detail.roof.pitched": color = new Color(0.5f, 0.25f, 0.18f); break;
                case QuarterObjectTypes.Swing: color = new Color(0.7f, 0.3f, 0.25f); break;
                case QuarterObjectTypes.Sandbox: color = new Color(0.9f, 0.78f, 0.5f); break;
                case QuarterObjectTypes.Urn: color = new Color(0.3f, 0.4f, 0.32f); break;
                case QuarterObjectTypes.TrashContainer: color = new Color(0.28f, 0.42f, 0.3f); break;
                case QuarterObjectTypes.Bush: color = new Color(0.28f, 0.55f, 0.28f); break;
                case QuarterObjectTypes.Footpath: color = new Color(0.68f, 0.66f, 0.6f); break;
                case QuarterObjectTypes.BasementFloor:
                case QuarterObjectTypes.BasementCeiling:
                case QuarterObjectTypes.BasementWall: color = new Color(0.52f, 0.52f, 0.5f); break; // бетон
                case QuarterObjectTypes.BasementStep: color = new Color(0.58f, 0.58f, 0.56f); break;
                case QuarterObjectTypes.BasementDoor: color = new Color(0.55f, 0.3f, 0.16f); break; // ржавое железо
                case QuarterObjectTypes.BasementSecondDoor: color = new Color(0.35f, 0.35f, 0.38f); break;
                case QuarterObjectTypes.BasementShelf: color = new Color(0.45f, 0.48f, 0.52f); break; // металл
                case QuarterObjectTypes.BasementCrate: color = new Color(0.6f, 0.45f, 0.28f); break; // дерево
                case QuarterObjectTypes.BasementTable: color = new Color(0.5f, 0.38f, 0.24f); break;
                case QuarterObjectTypes.BasementChair: color = new Color(0.45f, 0.33f, 0.2f); break;
                case QuarterObjectTypes.BasementPipe: color = new Color(0.4f, 0.42f, 0.45f); break;
                case QuarterObjectTypes.BasementLamp: color = new Color(0.9f, 0.75f, 0.4f); break; // тусклый тёплый
                case QuarterObjectTypes.BasementPoint: color = new Color(0.85f, 0.3f, 0.25f); break;
                case QuarterObjectTypes.Crosswalk:
                case QuarterObjectTypes.CenterLine:
                case QuarterObjectTypes.StopLine:
                case QuarterObjectTypes.ParkingStall: color = new Color(0.92f, 0.92f, 0.9f); break;
                // Районная карта (ТЗ №031): цвета — из существующей палитры, без новых ассетов.
                case DistrictObjectTypes.Commerce: color = new Color(0.82f, 0.8f, 0.76f); break; // штукатурка коммерции
                case DistrictObjectTypes.Sign: color = new Color(0.12f, 0.2f, 0.36f); break;     // вывеска
                case DistrictObjectTypes.Door: color = new Color(0.35f, 0.3f, 0.26f); break;     // входная дверь
                case DistrictObjectTypes.Roundabout: color = new Color(0.3f, 0.45f, 0.3f); break; // газон кольца
                case DistrictObjectTypes.GasCanopy: color = new Color(0.85f, 0.25f, 0.2f); break; // навес АЗС
                case DistrictObjectTypes.GasPost: color = new Color(0.7f, 0.7f, 0.72f); break;
                case DistrictObjectTypes.GasIsland: color = new Color(0.75f, 0.75f, 0.72f); break;
                case DistrictObjectTypes.GasPump: color = new Color(0.2f, 0.3f, 0.5f); break;
                case DistrictObjectTypes.GasKiosk: color = new Color(0.9f, 0.9f, 0.88f); break;
                case DistrictObjectTypes.IndustryBuilding: color = new Color(0.55f, 0.56f, 0.55f); break; // металл/бетон
                case DistrictObjectTypes.IndustryChimney: color = new Color(0.5f, 0.3f, 0.25f); break;
                case DistrictObjectTypes.IndustryLoading: color = new Color(0.45f, 0.45f, 0.45f); break;
                default: color = Color.magenta; break; // неизвестный тип виден сразу
            }

            var material = CreateMaterial(color);
            _materials[typeId] = material;
            return material;
        }
    }

    /// <summary>
    /// Железная дверь подвала (точка взаимодействия basement.door).
    /// Состояния ведёт BasementDoorController из ядра генератора
    /// (Closed/Open/Locked); этот компонент только плавно поворачивает
    /// дверь на петлях (родитель-пивот стоит на кромке петли, коллайдер
    /// едет вместе со створкой). Взаимодействие дёргает будущий код игрока:
    /// if (door.Interact()) { /* дверь открылась или закрылась */ }
    /// Locked-дверь Interact() не откроет — сначала Unlock() (ключ/сюжет).
    ///
    /// ВАЖНО: в Play Mode не проверялось (Unity в репозитории нет).
    /// </summary>
    public sealed class BasementDoor : MonoBehaviour
    {
        [SerializeField] private float openAngle = -105f;
        [SerializeField] private float speed = 6f;

        private BasementDoorController _controller;
        private float _currentAngle;

        public BasementDoorState State => _controller != null ? _controller.State : BasementDoorState.Locked;
        public bool IsOpen => _controller != null && _controller.IsOpen;

        internal void Configure(BasementDoorController controller)
        {
            _controller = controller ?? throw new System.ArgumentNullException(nameof(controller));
            _currentAngle = _controller.IsOpen ? openAngle : 0f;
            ApplyAngle();
        }

        /// <summary>Точка взаимодействия: открыть/закрыть. Locked возвращает false.</summary>
        public bool Interact()
        {
            EnsureController();
            return _controller.Toggle();
        }

        public bool Unlock()
        {
            EnsureController();
            return _controller.Unlock();
        }

        public bool Lock()
        {
            EnsureController();
            return _controller.Lock();
        }

        private void EnsureController()
        {
            if (_controller == null)
                _controller = new BasementDoorController(BasementDoorState.Locked);
        }

        private void Update()
        {
            float target = IsOpen ? openAngle : 0f;
            if (Mathf.Abs(target - _currentAngle) < 0.05f) return;
            _currentAngle = Mathf.Lerp(_currentAngle, target, Time.deltaTime * speed);
            ApplyAngle();
        }

        private void ApplyAngle()
        {
            // Пивот уже повёрнут по RotationY двери из плана; добавляем угол открытия вокруг Y.
            // Базовый поворот пивота храним первым кадром через localRotation при создании,
            // поэтому храним его отдельно.
            if (_baseRotation == null) _baseRotation = transform.localRotation;
            transform.localRotation = _baseRotation.Value * Quaternion.Euler(0f, _currentAngle, 0f);
        }

        private Quaternion? _baseRotation;
    }
}
