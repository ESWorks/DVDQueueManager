using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace DVDOrders.Objects
{
    public enum EmailType
    {

        [Description("File was transferred from the source to the destination")]
        Transferred,

        [Description("File is currently being encoded.")]
        Encoding,

        [Description("File has completed encoding.")]
        Encoded,

        [Description("File is currently being printed by the robot.")]
        Printing,

        [Description("File is currently completed printing by the robot.")]
        Completed,

        [Description("File is currently timed out or faulted.")]
        TimedOut,

        [Description("File is has been diagnosed with an error or is faulted.")]
        Error,

        [Description("File is transferring from the source to the destination.")]
        Transferring,

        [Description("File is can't find the source file listed in entry.")]
        NotFound,

        [Description("File is no longer queued and is ready to be marked for transfer.")]
        Ready,

        [Description("File is queued and ready for transfer queue.")]
        Queued
    }
}
