using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib.WzStructure.Data.MapStructure
{
    public class AutoLieDetector
    {
        public int startHour, endHour, interval, prop; //interval in mins, prop default = 80

        public AutoLieDetector(int startHour, int endHour, int interval, int prop)
        {
            this.startHour = startHour;
            this.endHour = endHour;
            this.interval = interval;
            this.prop = prop;
        }
    }
}
