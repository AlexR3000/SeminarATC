using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RecognizedAirPicture
{
    public class SBSMessageHelper
    {
        // "MSG" types
        public const int ESIdentificationAndCategory = 1;
        public const int ESSurfacePositionMessage = 2;
        public const int ESAirbornePositionMessage = 3;
        public const int ESAirborneVelocityMessage = 4;
        public const int SurveillanceAltMessage = 5;
        public const int SurveillanceIDMessage = 6;
        public const int AirToAirMessage = 7;
        public const int AllCallReply = 8;

        public const string MSG = "MSG";


        // type based
        public string FieldType { get; set; } = string.Empty;
        public int FieldTransmissionType { get; set; } = 0;






        // identification based
        public string FieldSessionID { get; set; } = string.Empty;
        // some database id that appears to be unused by hsog base station
        public string FieldAircraftID { get; set; } = string.Empty;

        // mode s transponder id which is part of every message
        public string FieldHexIdent {  get; set; } = string.Empty;

        public string FieldFlightID { get; set; } = string.Empty;

        public DateOnly FieldDateMessageGenerated { get; set; }
        public TimeOnly FieldTimeMessageGenerated { get; set; }
        public DateOnly FieldDateMessageLogged {get; set; }
        public TimeOnly FieldTimeMessageLogged { get; set; }

        public string FieldCallsign { get; set; } = string.Empty;
        
        // position based
        public int FieldAltitude { get; set; } = 0;
        public int FieldGroundSpeed { get; set; } = 0;
        public int FieldTrack { get; set; } = 0;
        public double FieldLatitude { get; set; } = 0;
        public double FieldLongitude { get; set; } = 0;

        public bool IgnoreMessage { get; set; } = false;
        

        public SBSMessageHelper(bool ignore=false)
        {
            IgnoreMessage = ignore;
        }

        public void SetFields(string[] splitMessage)
        {
            // Until FieldCallsign all are always part of a message. It is why Parse works instead and TryParse is not required.
            FieldType = splitMessage[(int)MessageFieldNumbers.Type];
            FieldTransmissionType = int.Parse(splitMessage[(int)MessageFieldNumbers.TransmissionType]);
            FieldSessionID = splitMessage[(int)MessageFieldNumbers.SessionID];
            FieldAircraftID = splitMessage[(int)MessageFieldNumbers.AircrafID];
            FieldHexIdent = splitMessage[(int)MessageFieldNumbers.HexIdent];

            FieldDateMessageGenerated = DateOnly.Parse(splitMessage[(int)MessageFieldNumbers.DateMessageGenerated]);
            FieldDateMessageLogged = DateOnly.Parse(splitMessage[(int)MessageFieldNumbers.DateMessageLogged]);
            FieldTimeMessageGenerated = TimeOnly.Parse(splitMessage[(int)MessageFieldNumbers.TimeMessageGenerated]);
            FieldTimeMessageLogged = TimeOnly.Parse(splitMessage[(int)MessageFieldNumbers.TimeMessageLogged]);


            FieldCallsign = splitMessage[(int)MessageFieldNumbers.Callsign];

            if (double.TryParse(splitMessage[(int)MessageFieldNumbers.Latitude],NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double latitude))
            {
                FieldLatitude = latitude;
            }
            if (double.TryParse(splitMessage[(int)MessageFieldNumbers.Longitude], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double longitude))
            {
                FieldLongitude = longitude;
            }
            if (int.TryParse(splitMessage[(int)MessageFieldNumbers.Altitude], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out int altitude))
            {
                FieldAltitude = altitude;
            }
            if (int.TryParse(splitMessage[(int)MessageFieldNumbers.GroundSpeed], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out int groundspeed))
            {
                FieldGroundSpeed = groundspeed;
            }
            if (int.TryParse(splitMessage[(int)MessageFieldNumbers.Track], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out int track))
            {
                FieldTrack = track;
            }
            
        }

    }

    // Values correspond to the fields mentioned here http://woodair.net/sbs/article/barebones42_socket_data.htm, but starting from 0
    public enum MessageFieldNumbers
    {
        Type = 0,
        TransmissionType = 1,

        SessionID = 2,
        AircrafID = 3,
        HexIdent = 4,
        FlightID = 5,

        DateMessageGenerated = 6,
        TimeMessageGenerated = 7,
        DateMessageLogged = 8,
        TimeMessageLogged = 9,

        Callsign = 10,

        Altitude = 11,
        GroundSpeed = 12,
        Track = 13,

        Latitude = 14,
        Longitude = 15,
    }
}
