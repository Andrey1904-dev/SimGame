using System;

namespace LifeFromZero.Phone.Contacts
{
    [Serializable] public sealed class PhoneBookSnapshot
    {
        public int Version;
        public PhoneContactSnapshot[] Contacts;
    }
    [Serializable] public sealed class PhoneContactSnapshot
    {
        public string Id;
        public string Name;
        public string PhoneNumber;
        public bool IsAvailable;
    }
}
