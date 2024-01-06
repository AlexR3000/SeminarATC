using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;


namespace RecognizedAirPicture
{
    public class RecognizedAircraft
    {
        public static readonly int MAX_MEDIAN_VALUES = 10;

        public static readonly int MAX_POSITIONS = 5;

        public static readonly int MIN_POSITIONS_THRESHOLD = 1;

        public string TransponderId { get; set; }
        public string Callsign { get; set; }

        private List<Position> _positions = new List<Position>();

        private List<int> _lastSpeeds { get; set; } = new List<int>();
        private int _groundSpeed = -1;
        public int GroundSpeed {
            get => _groundSpeed;
            set
            {
                AddNewValueToFixedSizeCollection(value, _lastSpeeds);
                _groundSpeed = (int)ApplyMedianFilter(_lastSpeeds);
                HasChanged = true;
            }
        }


        private List<int> _lastTracks { get; set; } = new List<int>();
        private int _track = -1;
        public int Track 
        {
            get => _track;
            set
            {
                if (value > 0 && value < 360)
                {
                    AddNewValueToFixedSizeCollection(value, _lastTracks);
                    _track = (int)ApplyMedianFilter(_lastTracks);
                    HasChanged = true;
                }
            }
        }

        public int EstimationsSinceLastActualPosition { get; set; } = 0;

        // Time supposed to be stored here is the time given in a sbs message
        public DateTime LastMessageReceived { get; set; }

        public bool HasChanged { get; set; }


        // When a first message is received a RecognizedAircraft object can be created from the first message
        public RecognizedAircraft(SBSMessageHelper sbsMessage) 
        {
            TransponderId = sbsMessage.FieldHexIdent;
            Callsign = sbsMessage.FieldCallsign;

            HasChanged = true;
            LastMessageReceived = sbsMessage.FieldDateMessageGenerated.ToDateTime(sbsMessage.FieldTimeMessageGenerated);


            switch (sbsMessage.FieldTransmissionType)
            {
                case SBSMessageHelper.ESAirbornePositionMessage:
                    _positions.Add(new Position
                    {
                        Latitude = sbsMessage.FieldLatitude,
                        Longitude = sbsMessage.FieldLongitude,
                        Generated = sbsMessage.FieldDateMessageGenerated.ToDateTime(sbsMessage.FieldTimeMessageGenerated)
            });
                    break;
                case SBSMessageHelper.ESAirborneVelocityMessage:
                    GroundSpeed = sbsMessage.FieldGroundSpeed;
                    Track = sbsMessage.FieldTrack;
                    break;
                default:
                    break;
            }

        }


        public bool HasValidState()
        {
            lock (_positions)
            {
                return Track != -1 && GroundSpeed != -1 && _positions.Count >= MIN_POSITIONS_THRESHOLD &&
                    Callsign != string.Empty && TransponderId != string.Empty;
            }
        }

        public Position? GetLastPosition()
        {
            lock (_positions)
            {
                var lastPosition = _positions.OrderByDescending(position => position.Generated).FirstOrDefault();
                return lastPosition;
            }
        }

        public Position? GetOldestPosition()
        {
            lock (_positions)
            {
                var oldestPosition = _positions.OrderBy(position => position.Generated).FirstOrDefault();
                return oldestPosition;
            }
        }


        public bool IsOutlierPosition(SBSMessageHelper sbsMessage)
        {
            var lastPosition = GetLastPosition();
            if (lastPosition == null)
            {
                return false;
            }


            var distance = Geolocation.GeoCalculator.GetDistance(lastPosition.Latitude, lastPosition.Longitude,
                    sbsMessage.FieldLatitude, sbsMessage.FieldLongitude);

            var planeSpeed = GroundSpeed;
            var tolerance = 1;
            var knotToKMHConversionValue = 1.852;
            if (planeSpeed == -1)
            {
                // In case the actual speed is unknown
                planeSpeed = 400;
            }
            return distance > ((planeSpeed * knotToKMHConversionValue) / 3600) +
                tolerance + 2*Math.Pow(EstimationsSinceLastActualPosition, 2);
        }

        public static double ApplyMedianFilter(ICollection<int> lastValues) 
        {
            if (lastValues.Count == 0) { return 0; }

            var sortedValues = lastValues.OrderBy(value => value).ToList();

            var isEven = sortedValues.Count % 2 == 0;
            double median = 0;
            if (isEven)
            {
                median = (sortedValues[sortedValues.Count / 2 - 1] + sortedValues[sortedValues.Count / 2]) / 2;
            }
            else
            {
                median = sortedValues[sortedValues.Count / 2];
            }
            return median;
        }

        public static void AddNewValueToFixedSizeCollection<T>(T value, ICollection<T> valueCollection)
        {
            if (valueCollection == null) {  return; }

            var values = (List<T>)valueCollection;

            if (values.Count >= MAX_MEDIAN_VALUES)
            {
                values.RemoveAt(0);
            }

            values.Add(value);
        }

        public void AddNewEstimatePosition()
        {
            var lastPosition = GetLastPosition();

            if (lastPosition == null) { return; }

            const double ratioKnotToKmPerSecond = 0.000514;
            // seconds in the future to calculate the next estimate position.
            const double PositionIn = 10;

            var distance = GroundSpeed * ratioKnotToKmPerSecond * PositionIn;

            lock (_positions)
            {
                var estimatedPosition = CalculateDestinationPoint(lastPosition.Latitude, lastPosition.Longitude, Track, distance);
                _positions.Add(estimatedPosition);
                if (_positions.Count > MAX_POSITIONS)
                {
                    var oldestPosition = GetOldestPosition();
                    if (oldestPosition != null)
                    {
                        _positions.Remove(oldestPosition);
                    }
                }
                EstimationsSinceLastActualPosition++;
            }

            HasChanged = true;
        }

        public void AddNewPosition(Position position)
        {
            if (position == null) { return; }

            lock (_positions)
            {
                _positions.Add(position);
                if (_positions.Count > MAX_POSITIONS)
                {
                    var oldestPosition = GetOldestPosition();
                    if (oldestPosition != null)
                    {
                        _positions.Remove(oldestPosition);
                    }
                }
                // Reset estimations
                EstimationsSinceLastActualPosition = 0;
                HasChanged = true;
            }
        }


        /// <summary>
        /// http://www.movable-type.co.uk/scripts/latlong.html
        /// Destination point given distance and bearing from start point
        /// </summary>
        /// <param name="aircraft"></param>
        public static Position CalculateDestinationPoint(double latitudeInDegree, double longitudeInDegree,
            int trackInDegree, double distance)
        {
            const int earthRadius = 6371;

            var latitudeInRadians = latitudeInDegree * Math.PI / 180;
            var longitudeInRadians = longitudeInDegree * Math.PI / 180;
            var trackInRadians = trackInDegree * Math.PI / 180;

            var estimatedLatitude = Math.Asin(Math.Sin(latitudeInRadians) * Math.Cos(distance / earthRadius) +
            Math.Cos(latitudeInRadians) * Math.Sin(distance / earthRadius) * Math.Cos(trackInRadians));

            var estimatedLongitude = longitudeInRadians +
                Math.Atan2(Math.Sin(trackInRadians) * Math.Sin(distance / earthRadius) *
                    Math.Cos(latitudeInRadians), Math.Cos(distance / earthRadius) -
                    Math.Sin(latitudeInRadians) * Math.Sin(estimatedLatitude));

            return new Position
            {
                Latitude = estimatedLatitude * 180 / Math.PI,
                Longitude = estimatedLongitude * 180 / Math.PI,
                Generated = DateTime.UtcNow,
                IsEstimated = true,
            };
        }


    }
}
