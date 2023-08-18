using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TPShipToolkit.Utils
{
    public static class TimeSpanFormat
    {
        public static string Get(TimeSpan Elapsed)
        {
            var str = Elapsed.ToString(@"d\d\ hh\h\ mm\m\ ss\sfff'ms'").TrimStart(' ', 'd', 'h', 'm', 's', '0');
            return string.IsNullOrWhiteSpace(str) ? "0ms" : str;
        }
    }
}
