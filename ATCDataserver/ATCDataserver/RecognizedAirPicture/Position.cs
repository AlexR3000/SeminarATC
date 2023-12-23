using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecognizedAirPicture
{
    public class Position

    {
        public double Longitude {  get; set; }
        public double Latitude { get; set; }
        public DateTime Generated { get; set; }
        public bool IsEstimated { get; set; }

        public Position()
        {
            IsEstimated = false;
        }


        override
        public string ToString()
        {
            var builder = new StringBuilder();

            builder.Append(Latitude);
            builder.Append(':');
            builder.Append(Longitude);
            builder.Append(':');
            builder.Append(Generated);
            builder.Append(':');
            builder.Append(IsEstimated);  
            return builder.ToString();
        }

    }
}
