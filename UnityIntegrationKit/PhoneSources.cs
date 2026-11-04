using System;
using LifeFromZero.Jobs.Delivery;
using LifeFromZero.Debts.Core;
using LifeFromZero.Phone.Integrations;

namespace LifeFromZero.UnityIntegrationKit
{
    // Read-only projections; the authoritative data remains in the source modules.
    public sealed class DeliveryPhoneSource : IDeliveryJobsSource
    {
        private readonly IDeliveryService service;
        public DeliveryPhoneSource(IDeliveryService service) { this.service = service ?? throw new ArgumentNullException(nameof(service)); }
        public bool TryGetOrders(out DeliveryJobDto[] orders)
        {
            orders = null;
            try
            {
                var list = service.Orders;
                var result = new DeliveryJobDto[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    var o = list[i];
                    var status = service.GetStatus(o.Id);
                    DeliveryJobPhase phase;
                    switch (status.Status.State)
                    {
                        case DeliveryOrderState.Available: phase = DeliveryJobPhase.Available; break;
                        case DeliveryOrderState.Accepted: phase = DeliveryJobPhase.Accepted; break;
                        case DeliveryOrderState.PickedUp: phase = DeliveryJobPhase.PickedUp; break;
                        case DeliveryOrderState.Completed: phase = DeliveryJobPhase.Completed; break;
                        case DeliveryOrderState.Failed: phase = DeliveryJobPhase.Failed; break;
                        case DeliveryOrderState.Cancelled: phase = DeliveryJobPhase.Cancelled; break;
                        default: return false;
                    }
                    result[i] = new DeliveryJobDto(o.Id, o.CargoTitle, o.PaymentRubles, phase);
                }
                orders = result; return true;
            }
            catch (Exception) { return false; }
        }
        public bool TryGetActiveOrderId(out string orderId)
        {
            orderId = null;
            try { orderId = service.ActiveOrderId; return true; }
            catch (Exception) { return false; }
        }
    }

    public sealed class DebtPhoneSource : IDebtsSource
    {
        private readonly IDebtReadService service;
        public DebtPhoneSource(IDebtReadService service) { this.service = service ?? throw new ArgumentNullException(nameof(service)); }
        public bool TryGetDebts(out DebtDto[] debts)
        {
            debts = null;
            try
            {
                var list = service.GetDebts();
                var result = new DebtDto[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    var d = list[i]; DebtPhase phase;
                    switch (d.Status)
                    {
                        case DebtStatus.Current: phase = DebtPhase.Current; break;
                        case DebtStatus.Overdue: phase = DebtPhase.Overdue; break;
                        case DebtStatus.Reminder: phase = DebtPhase.Reminder; break;
                        case DebtStatus.Collection: phase = DebtPhase.Collection; break;
                        case DebtStatus.LegalAction: phase = DebtPhase.LegalAction; break;
                        case DebtStatus.Closed: phase = DebtPhase.Closed; break;
                        default: return false;
                    }
                    result[i] = new DebtDto(d.Id, d.Title, d.TotalRemaining, d.DueAtMinutes, phase);
                }
                debts = result; return true;
            }
            catch (Exception) { return false; }
        }
    }
}
