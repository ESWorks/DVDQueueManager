using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace DVDOrders.Objects
{
    public enum WatcherType
    {
        [Description("Specifies use for robot job folder.")]
        Robot,
        [Description("Specifies use for robot encoder folder.")]
        Encoder
    }
}
