#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using LifeFromZero.Jobs.Delivery;
using LifeFromZero.Jobs.Tests.Fakes;

namespace LifeFromZero.Jobs.Tests
{
    public sealed class DeliveryIdempotencyTests
    {
        private sealed class Ledger : IIdempotentPayoutAdapter
        {
            public readonly Dictionary<string, long> Entries = new Dictionary<string, long>();
            public long Total;
            public bool TryCredit(long rubles) { Assert.Fail("legacy path used"); return false; }
            public bool TryCredit(string id, long rubles)
            {
                long previous;
                if (Entries.TryGetValue(id, out previous)) return previous == rubles;
                Entries.Add(id, rubles); Total += rubles; return true;
            }
            public bool WasCredited(string id, out bool credited)
            {
                credited = Entries.ContainsKey(id);
                return true;
            }
        }
        [Test] public void RestoreBeforeCreditAndRetryDoesNotCreditTwice()
        {
            var clock = new FakeClock(1); var location = new FakeLocation("a"); var service = new DeliveryService();
            service.RegisterOrder(new DeliveryOrder("order", "cargo", new DeliveryPoint("a", "A"), new DeliveryPoint("b", "B"), 100, 10));
            Assert.IsTrue(service.Accept("order", clock).IsSuccess);
            Assert.IsTrue(service.PickUp("order", clock, location).IsSuccess);
            var snapshot = service.CreateSnapshot(); location.CurrentLocationId = "b";
            var wallet = new Ledger(); Assert.IsTrue(service.Complete("order", clock, location, wallet).IsSuccess);
            var restored = new DeliveryService(); string error;
            Assert.IsTrue(restored.RestoreSnapshot(snapshot, out error), error);
            Assert.IsTrue(restored.Complete("order", clock, location, wallet).IsSuccess);
            Assert.AreEqual(100, wallet.Total); Assert.AreEqual(1, wallet.Entries.Count);
        }
    }
}
#endif
