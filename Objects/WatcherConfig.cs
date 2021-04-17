using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using DVDOrders.Services;

namespace DVDOrders.Objects
{
    [DataContract(Name = "WatcherConfig")]
    public class WatcherConfig
    {
        [DataMember(Name = "Id", IsRequired = true)]
        public string Id { get; set; }

        [DataMember(Name = "Robot", IsRequired = true)]
        public int Robot { get; set; }

        [DataMember(Name = "Enabled", IsRequired = true)]
        public bool Enabled { get; set; }

        [DataMember(Name = "ContentCompare", IsRequired = true)]
        public bool ContentCompare { get; set; }

        [DataMember(Name = "NotifyFilters", IsRequired = true)]
        public NotifyFilters NotifyFilters { get; set; }

        [DataMember(Name = "FileFilters", IsRequired = true)]
        public string FileFilters { get; set; }

        [DataMember(Name = "ChangeType", IsRequired = true)]
        public WatcherChangeTypes ChangeType { get; set; }

        [DataMember(Name = "EmailType", IsRequired = true)]
        public EmailType EmailType { get; set; }

        [DataMember(Name = "WatcherType", IsRequired = true)]
        public WatcherType WatcherType { get; internal set; }

        [DataMember(Name = "EmailRecipients", IsRequired = true)]
        public EmailRecipients EmailRecipients { get; set; }

        [DataMember(Name = "Subfolder", IsRequired = true)]
        public bool Subfolder { get; set; }

        public WatcherConfig(string id, WatcherType watcher, bool enabled, bool content, NotifyFilters filters, string file, WatcherChangeTypes change, EmailType email, bool subfolder, int robot = -1, EmailRecipients recipients = EmailRecipients.Everyone)
        {
            Id = id;
            Enabled = enabled;
            ContentCompare = content;
            NotifyFilters = filters;
            FileFilters = file;
            ChangeType = change;
            EmailType = email;
            WatcherType = watcher;
            Robot = robot;
            EmailRecipients = recipients;
            Subfolder = subfolder;
        }

    }
}
