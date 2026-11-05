// Life From Zero — UI/Runtime/DeliveryServiceHost.cs (ТЗ №042)
// Namespace: LifeFromZero.UI.Runtime
//
// ОДНА маленькая вещь, которой не хватало для подключения телефона:
// DeliveryService — обычный C#-класс (не MonoBehaviour), а сцене нужен компонент,
// который его отдаёт. Этот наследник и есть точка подключения:
//
//   public sealed class GameDeliveryHost : DeliveryServiceHost
//   {
//       protected override IDeliveryService CreateService() => myDeliveryService;
//   }
//
// Ничего не создаёт сам и не подменяет игру: если наследник в сцене не найден,
// телефон честно показывает «Система доставки не подключена».

using UnityEngine;
using LifeFromZero.Jobs.Delivery;

namespace LifeFromZero.UI.Runtime
{
    /// <summary>Компонент-держатель <see cref="IDeliveryService"/> для UI.</summary>
    public abstract class DeliveryServiceHost : MonoBehaviour
    {
        private IDeliveryService _service;

        /// <summary>Сервис доставки игры. Создаётся один раз, при первом обращении.</summary>
        public IDeliveryService Service
        {
            get
            {
                if (_service != null) return _service;
                _service = CreateService();
                if (_service == null)
                {
                    Debug.LogError("[DeliveryServiceHost] " + GetType().Name +
                                   ".CreateService() вернул null — телефон не покажет заказы.");
                }
                return _service;
            }
        }

        /// <summary>Вернуть сервис доставки проекта. Может вернуть null, если его ещё нет.</summary>
        protected abstract IDeliveryService CreateService();

        /// <summary>
        /// Переустановить сервис (например, после загрузки сохранения, когда доставка
        /// пересоздана). UI подхватит новый сервис при следующем чтении.
        /// </summary>
        public void ReplaceService(IDeliveryService service) => _service = service;
    }
}
