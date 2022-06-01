using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Core.Items
{
    // var myDeserializedClass = JsonConvert.DeserializeObject<ZoneInfo>(myJsonResponse); 
    /// <summary>
    /// List of all zone configurations
    /// </summary>
    public class ZoneInfo
    {
        public List<ZoneConfiguration> zoneConfigurations { get; set; }
    }
    /// <summary>
    /// One zone configuration 
    /// </summary>
    public class ZoneConfiguration
    {
        public int numberOfMates { get; set; }
        public List<Zone> zones { get; set; }
    }
    /// <summary>
    /// zones and 
    /// </summary>
    public class Zone
    {
        public int zone { get; set; }
        public List<int> mates { get; set; }
    }
}
