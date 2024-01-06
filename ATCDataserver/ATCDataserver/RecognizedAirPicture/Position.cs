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
    }
}
