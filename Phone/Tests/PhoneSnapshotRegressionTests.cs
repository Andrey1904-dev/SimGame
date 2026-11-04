#if UNITY_INCLUDE_TESTS
using NUnit.Framework;
using LifeFromZero.Phone.Apps;
using LifeFromZero.Phone.Contacts;

namespace LifeFromZero.Phone.Tests
{
    public sealed class PhoneSnapshotRegressionTests
    {
        [Test] public void AppsRoundTripAndCorruptionIsAtomic()
        {
            var source = PhoneAppRegistry.CreateDefault(); source.SetAvailability("bank", false); source.AddUnread("bank", 3);
            var snapshot = source.CreateSnapshot(); var target = new PhoneAppRegistry(); string error;
            Assert.IsTrue(target.RestoreSnapshot(snapshot, out error), error);
            Assert.IsTrue(target.RestoreSnapshot(snapshot, out error), error);
            Assert.AreEqual(3, target.TotalUnread);
            snapshot.Apps[0].UnreadCount = -1;
            Assert.IsFalse(target.RestoreSnapshot(snapshot, out error));
            Assert.AreEqual(3, target.TotalUnread);
        }
        [Test] public void ContactsRoundTripAndDuplicateIsAtomic()
        {
            var source = new PhoneBook(); source.Add(new PhoneContact("a", "A", "123", true));
            var snapshot = source.CreateSnapshot(); var target = new PhoneBook(); string error;
            Assert.IsTrue(target.RestoreSnapshot(snapshot, out error), error);
            Assert.IsTrue(target.RestoreSnapshot(snapshot, out error), error);
            snapshot.Contacts = new[] { snapshot.Contacts[0], snapshot.Contacts[0] };
            Assert.IsFalse(target.RestoreSnapshot(snapshot, out error));
            Assert.AreEqual(1, target.Contacts.Count);
            Assert.AreEqual("123", target.Contacts[0].PhoneNumber);
        }
    }
}
#endif
