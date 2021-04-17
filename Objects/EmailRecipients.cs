using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace DVDOrders.Objects
{
    public enum EmailRecipients
    {
        [Description("Emails the sender and admins.")]
        Everyone,

        [Description("Emails the sender only.")]
        Sender,

        [Description("Emails the admins only.")]
        Admins,

        [Description("Emails no one is purely for logging.")]
        None
    }
}
