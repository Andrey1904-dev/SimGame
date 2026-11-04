using System;

namespace LifeFromZero.Phone.Apps
{
    [Serializable] public sealed class PhoneAppsSnapshot
    {
        public int Version;
        public PhoneAppSnapshot[] Apps;
    }
    [Serializable] public sealed class PhoneAppSnapshot
    {
        public string Id;
        public string Title;
        public int Category;
        public bool IsAvailable;
        public int UnreadCount;
    }
}
