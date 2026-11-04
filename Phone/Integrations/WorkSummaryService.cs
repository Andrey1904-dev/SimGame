// Life From Zero — Phone integrations (ТЗ №014-Т.1)
// Namespace: LifeFromZero.Phone.Integrations

using LifeFromZero.Phone.Core;

namespace LifeFromZero.Phone.Integrations
{
    /// <summary>
    /// Данные для экрана «Работа»: доступные и активный заказы.
    /// Телефон ничего не принимает и не завершает — только показывает.
    /// </summary>
    public readonly struct WorkSummary
    {
        /// <summary>Доступен ли источник заказов.</summary>
        public bool JobsAvailable { get; }

        /// <summary>Заказы для витрины.</summary>
        public DeliveryJobDto[] Orders { get; }

        /// <summary>ID активного заказа или null.</summary>
        public string ActiveOrderId { get; }

        public WorkSummary(bool jobsAvailable, DeliveryJobDto[] orders, string activeOrderId)
        {
            JobsAvailable = jobsAvailable;
            Orders = orders ?? new DeliveryJobDto[0];
            ActiveOrderId = activeOrderId;
        }

        public override string ToString()
        {
            return "WorkSummary(orders=" + Orders.Length + ", active=" + (ActiveOrderId ?? "<none>") + ")";
        }
    }

    /// <summary>Собирает данные экрана «Работа» из <see cref="IDeliveryJobsSource"/>.</summary>
    public sealed class WorkSummaryService
    {
        private readonly IDeliveryJobsSource _source;

        public WorkSummaryService(IDeliveryJobsSource source)
        {
            _source = source;
        }

        public PhoneResult<WorkSummary> GetSummary()
        {
            bool available = false;
            DeliveryJobDto[] orders = new DeliveryJobDto[0];
            string activeId = null;

            if (_source != null)
            {
                try
                {
                    available = _source.TryGetOrders(out orders) && orders != null;
                }
                catch (System.Exception)
                {
                    available = false;
                    orders = new DeliveryJobDto[0];
                }

                if (available)
                {
                    try
                    {
                        string id;
                        if (_source.TryGetActiveOrderId(out id))
                        {
                            activeId = id;
                        }
                    }
                    catch (System.Exception)
                    {
                        activeId = null;
                    }
                }
            }

            return PhoneResult<WorkSummary>.Ok(new WorkSummary(available, orders, activeId));
        }
    }
}
