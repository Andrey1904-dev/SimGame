// Life From Zero — City/UnityIntegration/Runtime/PlayerHomeAnchor.cs (ТЗ #039, финальное дополнение)
// Namespace: LifeFromZero.Integration
//
// ПОСТОЯННАЯ привязка квартиры №67 к дому на карте района v1.1.
//
// Дом квартиры выбирается генератором по данным раскладки (DistrictMapGenerator.PickPlayerHomeId):
// самый юго-западный многоквартирный дом Северного квартала, стоящий южнее футбольной
// площадки. Координаты НЕ дублируются в сцене: этот компонент получает готовый план через
// CityBootstrap.PlanBuilt и ставит две точки:
//   player.home.door — дверь подъезда (вход в квартиру);
//   player.home.exit — выход во двор (точка возврата/спавна из квартиры).
// Обе точки создаются в мировых координатах района; повторная генерация их пересоздаёт,
// дубликатов не остаётся.
//
// Сцена квартиры (Apartment67 и т.п.) здесь НЕ угадывается: имя сцены задаётся в Inspector
// или инструментом (Tools → Life From Zero → Integrate Into Game). Пока имя пустое —
// OpenApartment() пишет предупреждение и ничего не делает: никаких фиктивных переходов.
//
// Нет Update, нет опроса клавиш, нет триггеров по умолчанию: только события и публичные
// методы для существующей системы переходов проекта (UnityEvent / своя интеракция).
//
// ВНИМАНИЕ: в среде репозитория Unity нет; проверено только статическим разбором C#.

using LifeFromZero.City.Quarter;
using LifeFromZero.City.Quarter.UnityAdapter;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LifeFromZero.Integration
{
    /// <summary>
    /// Привязка квартиры №67: точки входа/выхода дома по данным плана District750
    /// и мост «район ↔ сцена квартиры». Ничего не делает без вызова извне.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHomeAnchor : MonoBehaviour
    {
        [Header("Источник данных")]
        [Tooltip("CityBootstrap того же района. Пусто — ищется в сцене автоматически.")]
        [SerializeField] private CityBootstrap bootstrap;

        [Header("Сцены (задаёт Codex после интеграции)")]
        [Tooltip("Имя сцены квартиры без .unity. Пусто — переход в квартиру отключён.")]
        [SerializeField] private string apartmentSceneName = "";

        [Tooltip("Имя сцены района District750 без .unity (создаётся инструментом интеграции).")]
        [SerializeField] private string districtSceneName = "District750";

        [Tooltip("Additive: район остаётся загруженным под сценой квартиры. Off: single-загрузка.")]
        [SerializeField] private bool apartmentAdditive = false;

        [Header("Точки на земле")]
        [Tooltip("Имена дочерних объектов-точек (стабильные, без id).")]
        [SerializeField] private string doorAnchorName = "player.home.door";
        [SerializeField] private string exitAnchorName = "player.home.exit";

        [Tooltip("Добавить на точку двери BoxCollider-триггер для существующей системы взаимодействия.")]
        [SerializeField] private bool createDoorTrigger = false;
        [SerializeField] private Vector3 doorTriggerSize = new Vector3(2.4f, 2.4f, 2.4f);

        private Transform _root;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation = Quaternion.identity;
        private bool _hasSpawn;

        /// <summary>Дом квартиры №67 в текущем плане (например bld-n3) или пусто.</summary>
        public string HomeBuildingId { get; private set; } = "";

        /// <summary>Точка возврата из квартиры во двор. False — план ещё не построен.</summary>
        public bool HasSpawn => _hasSpawn;
        public Vector3 SpawnPosition => _spawnPosition;
        public Quaternion SpawnRotation => _spawnRotation;

        /// <summary>Готова ли привязка к сцене квартиры (имя сцены задано).</summary>
        public bool ApartmentConfigured => !string.IsNullOrWhiteSpace(apartmentSceneName);

        private void OnEnable()
        {
            if (bootstrap == null)
                bootstrap = FindBootstrap();
            if (bootstrap == null)
            {
                Debug.LogWarning("[PlayerHomeAnchor] CityBootstrap не найден: привязка дома не работает. " +
                                 "Положите компонент на объект с CityBootstrap или укажите его в Inspector.");
                return;
            }

            bootstrap.PlanBuilt += OnPlanBuilt;
            // План мог быть построен до появления компонента (порядок выполнения не гарантирован).
            if (bootstrap.LastPlan != null)
                OnPlanBuilt(bootstrap.LastPlan);
        }

        private void OnDisable()
        {
            if (bootstrap != null)
                bootstrap.PlanBuilt -= OnPlanBuilt;
        }

        /// <summary>Пересобрать точки по текущему плану (вызывается автоматически после PlanBuilt).</summary>
        public void Rebuild()
        {
            if (bootstrap != null && bootstrap.LastPlan != null)
                OnPlanBuilt(bootstrap.LastPlan);
        }

        private void OnPlanBuilt(QuarterPlan plan)
        {
            PlayerHomeInfo home = plan != null ? plan.PlayerHome : null;
            if (home == null)
            {
                _hasSpawn = false;
                HomeBuildingId = "";
                Debug.LogError("[PlayerHomeAnchor] В плане «" + (plan != null ? plan.LayoutId : "?") +
                               "» нет player.home: раскладка не задаёт квартиру №67. Точки не созданы.");
                return;
            }

            HomeBuildingId = home.BuildingId;
            _spawnPosition = ToVector3(home.CourtyardExit);
            _spawnRotation = Quaternion.Euler(0f, home.RotationY, 0f);
            _hasSpawn = true;

            // Старые точки (в т.ч. от предыдущей генерации) удаляем, чтобы не было дубликатов.
            if (_root != null)
                UnityEngine.Object.Destroy(_root.gameObject);

            var root = new GameObject("player.home.anchors");
            // План задан в координатах района: адаптер строит город дочерним объектом
            // CityBootstrap без смещения, поэтому локальные координаты = координаты плана.
            root.transform.SetParent(bootstrap != null ? bootstrap.transform : transform, false);
            _root = root.transform;

            Transform door = CreateAnchor(root.transform, doorAnchorName, ToVector3(home.EntranceDoor), home.RotationY);
            if (createDoorTrigger)
            {
                var box = door.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = doorTriggerSize;
                box.center = new Vector3(0f, doorTriggerSize.y / 2f, 0f);
            }
            CreateAnchor(root.transform, exitAnchorName, _spawnPosition, home.RotationY);

            Debug.Log("[PlayerHomeAnchor] Квартира №67 привязана: дом=" + home.BuildingId +
                      ", дверь=" + Format(ToVector3(home.EntranceDoor)) +
                      ", выход=" + Format(_spawnPosition) +
                      ", сцена квартиры=" + (ApartmentConfigured ? apartmentSceneName : "не задана"));
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 position, float rotationY)
        {
            var go = new GameObject(string.IsNullOrWhiteSpace(name) ? "anchor" : name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);
            return go.transform;
        }

        /// <summary>Войти в квартиру (вызывается из существующей двери/интеракции).</summary>
        public void OpenApartment()
        {
            if (!ApartmentConfigured)
            {
                Debug.LogWarning("[PlayerHomeAnchor] Сцена квартиры не задана — переход отменён. " +
                                 "Задайте Apartment Scene Name в Inspector или через Tools → Life From Zero → Integrate Into Game.");
                return;
            }

            Load(apartmentSceneName, apartmentAdditive ? LoadSceneMode.Additive : LoadSceneMode.Single);
        }

        /// <summary>Вернуться из квартиры в район. Точку появления берёт вызывающая система (SpawnPosition).</summary>
        public void ReturnToDistrict()
        {
            Load(districtSceneName, LoadSceneMode.Single);
        }

        /// <summary>
        /// Поставить объект (игрока) в точку выхода во двор. Для CharacterController
        /// его временно отключают — иначе позиция не применяется.
        /// </summary>
        public bool ApplyToSpawn(Transform target)
        {
            if (target == null || !_hasSpawn)
                return false;

            var cc = target.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                target.SetPositionAndRotation(_spawnPosition, _spawnRotation);
                cc.enabled = true;
                return true;
            }

            var rb = target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = _spawnPosition;
                rb.rotation = _spawnRotation;
                target.SetPositionAndRotation(_spawnPosition, _spawnRotation);
                return true;
            }

            target.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            return true;
        }

        private static void Load(string sceneName, LoadSceneMode mode)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                Debug.LogWarning("[PlayerHomeAnchor] имя сцены не задано — переход отменён.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError("[PlayerHomeAnchor] сцена '" + sceneName + "' недоступна: добавьте её в " +
                               "Build Settings / Build Profile. Переход отменён, текущая сцена не изменена.");
                return;
            }

            SceneManager.LoadScene(sceneName, mode);
        }

        private static CityBootstrap FindBootstrap()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<CityBootstrap>(FindObjectsInactive.Exclude);
#else
            return UnityEngine.Object.FindObjectOfType<CityBootstrap>();
#endif
        }

        private static Vector3 ToVector3(Vec3 v) => new Vector3(v.X, v.Y, v.Z);

        private static string Format(Vector3 v) =>
            "(" + v.x.ToString("0.##") + "; " + v.y.ToString("0.##") + "; " + v.z.ToString("0.##") + ")";
    }
}
