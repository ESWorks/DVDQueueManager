using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Windows.Documents;

namespace DVDOrders.Objects
{
    [DataContract(Name = "RobotConfig")]
    public class RobotConfig
    {
        [DataMember(Name = "Id", IsRequired = true)]
        public int Id { get; set; }

        [DataMember(Name = "Encoder", IsRequired = true)]
        public string Encoder { get; set; }

        [DataMember(Name = "Name", IsRequired = true)]
        public string Name { get; set; }

        [DataMember(Name = "JobLocation", IsRequired = true)]
        public string JobLocation { get; set; }

        [DataMember(Name = "LabelLocation", IsRequired = true)]
        public string LabelLocation { get; set; }

        public RobotConfig(int id, string encoder, string name, string job, string labelLocation)
        {
            Id = id;
            Encoder = encoder;
            Name = name;
            JobLocation = job;
            LabelLocation = labelLocation;
        }

        public override string ToString()
        {
            return Id+": "+Name;
        }
    }
}
