namespace AircraftFetcherAWSLambda
{
    public class Aircraft
    {
        public string ID { get; set; } = string.Empty;
        public int Estimates { get; set; }
        public string Latitude { get; set; } = string.Empty;
        public string Longitude { get; set; } = string.Empty;
        public string PositionCreated { get; set; } = string.Empty;
        public int Track { get; set; }
        public string Callsign {  get; set; } = string.Empty;
    }
}
