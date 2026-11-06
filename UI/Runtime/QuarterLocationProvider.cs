// Life From Zero — UI/Runtime/QuarterLocationProvider.cs
// Namespace: LifeFromZero.UI.Runtime
//
// Готовые Inspector-адаптеры между миром сцены (QuarterPlan + позиция игрока +
// существующие часы/кошелёк проекта) и чистыми контрактами Jobs.Delivery.
//
// Принцип честности:
//   * QuarterLocationProvider вычисляет локацию ТОЛЬКО из реальных координат игрока
//     и реальных точек QuarterPlan. Вне радиуса точки или без плана возвращает "".
//   * DistrictDeliveryServiceHost не создаёт второй DeliveryService по умолчанию:
//     он оборачивает назначенный компонент владельца из сцены (или создаёт сервис
//     только если разработчик явно включил createServiceWhenMissing в Inspector).
//   * DeliveryClockAdapter / DeliveryPayoutAdapter отражают только реальные компоненты
//     игры; пока целевой компонент не назначен, IsConfigured == false, и мост честно
//     блокирует команду с понятным объяснением.

using System;
using System.Collections.Generic;
using System.Reflection;
using LifeFromZero.City.Quarter;
using LifeFromZero.Jobs.Delivery;
using LifeFromZero.UI.Core;
using UnityEngine;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Признак того, что компонентный адаптер доставки привязан к реальной системе игры.</summary>
    public interface IConfiguredDeliveryAdapter
    {
        bool IsConfigured { get; }
    }

    /// <summary>
    /// Определяет идентификатор точки доставки/выдачи (<c>drop-*</c>), рядом с которой
    /// сейчас стоит игрок в построенном районе <c>District750</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class QuarterLocationProvider : MonoBehaviour, ILocationProvider, IConfiguredDeliveryAdapter
    {
        [Tooltip("Адаптер позиции игрока. Пусто — берётся из UiBootstrap/UiRouter.")]
        [SerializeField] private UiWorldAdapter worldAdapter;

        [Tooltip("Источник плана района (QuarterUnityAdapter). Пусто — берётся из UiBootstrap или сцены.")]
        [SerializeField] private MonoBehaviour districtSource;

        [Tooltip("Минимальный радиус взаимодействия у подъезда/ПВЗ, метры.")]
        [SerializeField] private float minInteractionRadiusMeters = 3.0f;

        public bool IsConfigured => ResolveWorld() != null;

        public string CurrentLocationId
        {
            get
            {
                UiWorldAdapter world = ResolveWorld();
                if (world == null) return string.Empty;

                Vector3 pos;
                float heading;
                if (!world.TryGetPlayerPose(out pos, out heading)) return string.Empty;

                QuarterPlan plan = ResolvePlan();
                if (plan == null) return string.Empty;

                return QuarterPlanAdapter.ResolveLocationId(
                    plan,
                    new MapPoint(pos.x, pos.z),
                    minInteractionRadiusMeters);
            }
        }

        private UiWorldAdapter ResolveWorld()
        {
            if (worldAdapter != null) return worldAdapter;
            if (UiRouter.Instance != null && UiRouter.Instance.World != null)
            {
                worldAdapter = UiRouter.Instance.World;
                return worldAdapter;
            }
            worldAdapter = FindAnyObjectByType<UiWorldAdapter>();
            return worldAdapter;
        }

        private QuarterPlan ResolvePlan()
        {
            var source = districtSource as IQuarterPlanSource;
            if (source != null && source.Plan != null) return source.Plan;

            if (UiBootstrap.Instance != null && UiBootstrap.Instance.CurrentDistrictPlan != null)
                return UiBootstrap.Instance.CurrentDistrictPlan;

            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var candidate = behaviours[i] as IQuarterPlanSource;
                if (candidate == null || candidate.Plan == null) continue;
                districtSource = behaviours[i];
                return candidate.Plan;
            }
            return null;
        }
    }

    /// <summary>
    /// Готовый <see cref="DeliveryServiceHost"/> для Inspector:
    /// подключает существующий компонент проекта, владеющий <see cref="IDeliveryService"/>,
    /// либо (только при явном флаге <c>createServiceWhenMissing</c>) поднимает экземпляр сервиса.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DistrictDeliveryServiceHost : DeliveryServiceHost
    {
        [Header("Существующий владелец в проекте")]
        [Tooltip("Компонент проекта, реализующий IDeliveryService или хранящий свойство/поле типа IDeliveryService / DeliveryService.")]
        [SerializeField] private MonoBehaviour existingServiceOwner;

        [Header("Автономный режим (выключено по умолчанию)")]
        [Tooltip("Включите ТОЛЬКО если в сцене ещё нет собственного владельца DeliveryService.")]
        [SerializeField] private bool createServiceWhenMissing;

        [Tooltip("При автономном режиме зарегистрировать стартовые заказы по реальным точкам построенного District750.")]
        [SerializeField] private bool registerDistrictOrdersOnBuild;

        [Tooltip("Источник плана района для регистрации заказов (пусто — ищется автоматически).")]
        [SerializeField] private MonoBehaviour districtSource;

        private IDeliveryService _cachedService;
        private MonoBehaviour _cachedOwner;
        private bool _ordersSeeded;
        private INotifyQuarterPlanSource _hookedPlanSource;

        /// <summary>Есть ли настроенный сервис доставки (чтобы не логировать ошибку раньше времени).</summary>
        public bool HasConfiguredService => existingServiceOwner != null || createServiceWhenMissing;

        protected override IDeliveryService CreateService()
        {
            IDeliveryService resolved = ResolveService();
            if (resolved != null && registerDistrictOrdersOnBuild && !_ordersSeeded)
                TrySeedDistrictOrders(resolved as DeliveryService);
            return resolved;
        }

        private void OnEnable()
        {
            HookDistrictNotifications();
        }

        private void OnDisable()
        {
            if (_hookedPlanSource != null)
            {
                _hookedPlanSource.PlanChanged -= OnPlanChanged;
                _hookedPlanSource = null;
            }
        }

        private IDeliveryService ResolveService()
        {
            if (existingServiceOwner != null)
            {
                if (ReferenceEquals(_cachedOwner, existingServiceOwner) && _cachedService != null)
                    return _cachedService;

                _cachedOwner = existingServiceOwner;
                _cachedService = ExtractDeliveryService(existingServiceOwner);
                return _cachedService;
            }

            if (_cachedOwner != null && existingServiceOwner == null)
            {
                _cachedOwner = null;
                _cachedService = null;
            }

            if (_cachedService == null && createServiceWhenMissing)
            {
                _cachedService = new DeliveryService();
            }

            return _cachedService;
        }

        private static IDeliveryService ExtractDeliveryService(MonoBehaviour owner)
        {
            if (owner == null) return null;
            if (owner is IDeliveryService direct) return direct;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = owner.GetType();

            PropertyInfo[] props = type.GetProperties(flags);
            for (int i = 0; i < props.Length; i++)
            {
                if (!typeof(IDeliveryService).IsAssignableFrom(props[i].PropertyType)) continue;
                if (props[i].GetIndexParameters().Length != 0) continue;
                try { return props[i].GetValue(owner, null) as IDeliveryService; }
                catch { /* игнорируем недоступные геттеры */ }
            }

            FieldInfo[] fields = type.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                if (!typeof(IDeliveryService).IsAssignableFrom(fields[i].FieldType)) continue;
                try { return fields[i].GetValue(owner) as IDeliveryService; }
                catch { /* игнорируем */ }
            }

            return null;
        }

        private void HookDistrictNotifications()
        {
            if (!registerDistrictOrdersOnBuild) return;
            IQuarterPlanSource source = FindPlanSource();
            var notify = source as INotifyQuarterPlanSource;
            if (notify != null && !ReferenceEquals(_hookedPlanSource, notify))
            {
                if (_hookedPlanSource != null) _hookedPlanSource.PlanChanged -= OnPlanChanged;
                _hookedPlanSource = notify;
                _hookedPlanSource.PlanChanged += OnPlanChanged;
            }
        }

        private void OnPlanChanged(QuarterPlan plan)
        {
            if (plan == null) return;
            var concrete = ResolveService() as DeliveryService;
            if (concrete != null && !_ordersSeeded) TrySeedDistrictOrders(concrete, plan);
        }

        private IQuarterPlanSource FindPlanSource()
        {
            if (districtSource is IQuarterPlanSource assigned) return assigned;
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IQuarterPlanSource candidate)
                {
                    districtSource = behaviours[i];
                    return candidate;
                }
            }
            return null;
        }

        private void TrySeedDistrictOrders(DeliveryService service, QuarterPlan explicitPlan = null)
        {
            if (service == null || _ordersSeeded) return;
            QuarterPlan plan = explicitPlan
                               ?? (UiBootstrap.Instance != null ? UiBootstrap.Instance.CurrentDistrictPlan : null)
                               ?? FindPlanSource()?.Plan;
            if (plan == null || plan.DeliveryPoints == null || plan.DeliveryPoints.Count == 0) return;

            DeliveryPoint pickup = new DeliveryPoint("drop-pvz-01", "ПВЗ");
            string[] cargos = { "Посылка из ПВЗ", "Пакет продуктов", "Документы в офис" };
            long[] payments = { 350L, 480L, 620L };
            long[] deadlines = { 1440L, 1440L, 2880L };

            int added = 0;
            for (int i = 0; i < plan.DeliveryPoints.Count && added < cargos.Length; i++)
            {
                DeliveryPointInfo dp = plan.DeliveryPoints[i];
                if (dp == null || dp.Id == "drop-pvz-01" || dp.Kind == "pickup") continue;
                string id = "ord-d750-" + (added + 1);
                string title = dp.BuildingId + " (подъезд " + dp.EntranceNumber + ")";
                var order = new DeliveryOrder(
                    id,
                    cargos[added],
                    pickup,
                    new DeliveryPoint(dp.Id, title),
                    payments[added],
                    deadlines[added]);
                if (service.RegisterOrder(order).IsSuccess) added++;
            }

            if (added > 0) _ordersSeeded = true;
        }
    }

    /// <summary>
    /// Inspector-адаптер игрового времени (<see cref="IGameClockAdapter"/>) поверх существующего
    /// компонента часов сцены (<c>GameClock</c> и др.). Без назначенного источника <c>IsConfigured == false</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeliveryClockAdapter : MonoBehaviour, IGameClockAdapter, IConfiguredDeliveryAdapter
    {
        [Tooltip("Компонент игровых часов проекта (свойство/поле NowMinutes, TotalMinutes, CurrentMinutes и т.п.).")]
        [SerializeField] private MonoBehaviour clockComponent;

        [Tooltip("Имя числового свойства/поля в минутах (необязательно — определяется автоматически).")]
        [SerializeField] private string minutesMemberName;

        private MonoBehaviour _boundTarget;
        private Func<double> _reader;

        public bool IsConfigured
        {
            get
            {
                EnsureBound();
                return _reader != null;
            }
        }

        public double NowMinutes
        {
            get
            {
                EnsureBound();
                if (_reader == null)
                    throw new InvalidOperationException("DeliveryClockAdapter: компонент игровых часов не подключён.");
                return _reader();
            }
        }

        private void EnsureBound()
        {
            if (clockComponent == null)
            {
                _boundTarget = null;
                _reader = null;
                return;
            }

            if (ReferenceEquals(_boundTarget, clockComponent) && _reader != null) return;
            _boundTarget = clockComponent;
            _reader = BuildMinutesReader(clockComponent, minutesMemberName);
        }

        private static Func<double> BuildMinutesReader(MonoBehaviour target, string explicitMember)
        {
            if (target is IGameClockAdapter direct) return () => direct.NowMinutes;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = target.GetType();
            string[] candidates = string.IsNullOrWhiteSpace(explicitMember)
                ? new[] { "NowMinutes", "TotalMinutes", "CurrentMinutes", "GameMinutes", "ElapsedMinutes" }
                : new[] { explicitMember.Trim() };

            for (int i = 0; i < candidates.Length; i++)
            {
                PropertyInfo prop = type.GetProperty(candidates[i], flags);
                if (prop != null && IsNumeric(prop.PropertyType))
                    return () => Convert.ToDouble(prop.GetValue(target, null));

                FieldInfo field = type.GetField(candidates[i], flags);
                if (field != null && IsNumeric(field.FieldType))
                    return () => Convert.ToDouble(field.GetValue(target));
            }

            return null;
        }

        private static bool IsNumeric(Type t) =>
            t == typeof(double) || t == typeof(float) || t == typeof(long) ||
            t == typeof(int) || t == typeof(decimal);
    }

    /// <summary>
    /// Inspector-адаптер выплат курьеру (<see cref="IIdempotentPayoutAdapter"/>) поверх
    /// существующего компонента кошелька проекта (<c>MoneyWallet</c> и др.).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeliveryPayoutAdapter : MonoBehaviour, IIdempotentPayoutAdapter, IConfiguredDeliveryAdapter
    {
        [Tooltip("Компонент кошелька проекта (TryCredit / AddMoney / Credit).")]
        [SerializeField] private MonoBehaviour walletComponent;

        [Tooltip("Источник данных UI для мгновенного обновления баланса на HUD после выплаты.")]
        [SerializeField] private UiSnapshotSource snapshotSourceToNotify;

        private readonly Dictionary<string, long> _creditedOperations = new Dictionary<string, long>(StringComparer.Ordinal);
        private MonoBehaviour _boundTarget;
        private Func<string, long, bool> _idempotentCredit;
        private Func<long, bool> _simpleCredit;

        public bool IsConfigured
        {
            get
            {
                EnsureBound();
                return _idempotentCredit != null || _simpleCredit != null;
            }
        }

        public bool TryCredit(long rubles)
        {
            if (rubles <= 0L) return false;
            EnsureBound();
            bool ok = false;
            if (_simpleCredit != null) ok = _simpleCredit(rubles);
            else if (_idempotentCredit != null) ok = _idempotentCredit("ui-payout-" + Guid.NewGuid().ToString("N"), rubles);
            if (ok) NotifyUi();
            return ok;
        }

        public bool TryCredit(string operationId, long rubles)
        {
            if (string.IsNullOrWhiteSpace(operationId) || rubles <= 0L) return false;
            EnsureBound();

            if (walletComponent is IIdempotentPayoutAdapter nativeIdempotent)
            {
                bool ok = nativeIdempotent.TryCredit(operationId, rubles);
                if (ok) NotifyUi();
                return ok;
            }

            long recordedAmount;
            if (_creditedOperations.TryGetValue(operationId, out recordedAmount))
                return recordedAmount == rubles;

            if (_idempotentCredit != null)
            {
                bool ok = _idempotentCredit(operationId, rubles);
                if (ok)
                {
                    _creditedOperations[operationId] = rubles;
                    NotifyUi();
                }
                return ok;
            }

            if (_simpleCredit != null)
            {
                bool ok = _simpleCredit(rubles);
                if (ok)
                {
                    _creditedOperations[operationId] = rubles;
                    NotifyUi();
                }
                return ok;
            }

            return false;
        }

        public bool WasCredited(string operationId, out bool credited)
        {
            if (walletComponent is IIdempotentPayoutAdapter nativeIdempotent)
                return nativeIdempotent.WasCredited(operationId, out credited);

            if (string.IsNullOrWhiteSpace(operationId))
            {
                credited = false;
                return false;
            }

            credited = _creditedOperations.ContainsKey(operationId);
            return true;
        }

        private void NotifyUi()
        {
            if (snapshotSourceToNotify != null) snapshotSourceToNotify.NotifyChanged();
            else if (UiRouter.Instance != null && UiRouter.Instance.SnapshotSource != null)
                UiRouter.Instance.SnapshotSource.NotifyChanged();
        }

        private void EnsureBound()
        {
            if (walletComponent == null)
            {
                _boundTarget = null;
                _idempotentCredit = null;
                _simpleCredit = null;
                return;
            }

            if (ReferenceEquals(_boundTarget, walletComponent) &&
                (_idempotentCredit != null || _simpleCredit != null))
                return;

            _boundTarget = walletComponent;
            _idempotentCredit = null;
            _simpleCredit = null;

            if (walletComponent is IPayoutAdapter direct)
            {
                _simpleCredit = direct.TryCredit;
                return;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = walletComponent.GetType();

            MethodInfo idem = type.GetMethod("TryCredit", flags, null, new[] { typeof(string), typeof(long) }, null);
            if (idem != null && idem.ReturnType == typeof(bool))
            {
                _idempotentCredit = (opId, amount) => (bool)idem.Invoke(walletComponent, new object[] { opId, amount });
            }

            string[] names = { "TryCredit", "Credit", "AddMoney", "Deposit", "Add" };
            for (int i = 0; i < names.Length; i++)
            {
                MethodInfo mLong = type.GetMethod(names[i], flags, null, new[] { typeof(long) }, null);
                if (mLong != null)
                {
                    _simpleCredit = CreateCreditInvoker(walletComponent, mLong, typeof(long));
                    return;
                }
                MethodInfo mDec = type.GetMethod(names[i], flags, null, new[] { typeof(decimal) }, null);
                if (mDec != null)
                {
                    _simpleCredit = CreateCreditInvoker(walletComponent, mDec, typeof(decimal));
                    return;
                }
                MethodInfo mInt = type.GetMethod(names[i], flags, null, new[] { typeof(int) }, null);
                if (mInt != null)
                {
                    _simpleCredit = CreateCreditInvoker(walletComponent, mInt, typeof(int));
                    return;
                }
            }
        }

        private static Func<long, bool> CreateCreditInvoker(MonoBehaviour target, MethodInfo method, Type argType)
        {
            return rubles =>
            {
                if (argType == typeof(int) && (rubles > int.MaxValue || rubles < 0)) return false;
                object arg = argType == typeof(long) ? (object)rubles
                           : argType == typeof(decimal) ? (object)(decimal)rubles
                           : (object)(int)rubles;
                object result = method.Invoke(target, new[] { arg });
                return method.ReturnType == typeof(void) || (result is bool b && b);
            };
        }
    }
}
