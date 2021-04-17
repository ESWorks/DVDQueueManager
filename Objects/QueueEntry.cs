using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace DVDOrders.Objects
{
    [DataContract(Name = "QueueEntry")]
    public class QueueEntry
    {
        [DataMember(Name = "Id", IsRequired = true)]
        public string ID {  set; get; }

        [DataMember(Name = "Robot", IsRequired = true)]
        public int Robot { set; get; }

        [DataMember(Name = "Status", IsRequired = true)]
        public EmailType Status {  set; get; }

        [DataMember(Name = "Source", IsRequired = true)]
        public string Source {  set; get; }

        [DataMember(Name = "Line1", IsRequired = true)]
        public string Line1 {  set; get; }

        [DataMember(Name = "Line2", IsRequired = true)]
        public string Line2 {  set; get; }

        [DataMember(Name = "Line3", IsRequired = true)]
        public string Line3 {  set; get; }

        [DataMember(Name = "EmailAddress", IsRequired = true)]
        public string EmailAddress { set; get; }

        [DataMember(Name = "TaskId", IsRequired = true)]
        public int TaskId { set; get; }

        [DataMember(Name = "TimeSpan", IsRequired = true)]
        public TimeSpan TimeSpan { set; get; }

        [DataMember(Name = "Progress", IsRequired = true)]
        public double Progress { set; get; }

        [DataMember(Name = "CopyCount", IsRequired = false)]
        public int CopyCount { get; set; }

        public QueueEntry(string id, int robot, EmailType status, string source, string line1, string line2, string line3, int copyCount=1, string emailAddress=null)
        {
            ID = id;
            Robot = robot;
            Source = source;
            Status = status;
            Progress = 0;
            Line1 = line1;
            Line2 = line2;
            Line3 = line3;
            TimeSpan = TimeSpan.Zero;
            TaskId = -1;
            EmailAddress = emailAddress?? Settings.EmailAddress;
            CopyCount = copyCount;
        }
    }
}
