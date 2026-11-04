// Life From Zero — Debts module (ТЗ №016)
// Namespace: LifeFromZero.Debts.Events

using System;
using System.Collections.Generic;

namespace LifeFromZero.Debts.Events
{
    /// <summary>
    /// Мультикаст-приёмник: подписка нескольких внешних систем на события долгов.
    /// Сбой одного обработчика не мешает остальным и не пробрасывается в сервис.
    /// Не потокобезопасен (рассчитан на главный поток Unity).
    /// </summary>
    public sealed class DebtEventHub : IDebtEventSink
    {
        private readonly List<Action<DebtEvent>> _handlers = new List<Action<DebtEvent>>();

        public int HandlerCount => _handlers.Count;

        public void Subscribe(Action<DebtEvent> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            _handlers.Add(handler);
        }

        public bool Unsubscribe(Action<DebtEvent> handler)
        {
            return handler != null && _handlers.Remove(handler);
        }

        public void Publish(DebtEvent debtEvent)
        {
            if (debtEvent == null)
            {
                return;
            }

            // Копия на случай отписки обработчика во время рассылки.
            Action<DebtEvent>[] snapshot = _handlers.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i](debtEvent);
                }
                catch (Exception)
                {
                    // Сбой подписчика — его проблема; состояние долгов уже зафиксировано.
                }
            }
        }
    }
}
