using System.Collections.Generic;

namespace FlashMeasurementSystem.Domain.EdgeDetection
{
    public class EdgeResult
    {
        public bool Success { get; set; }
        public List<EdgePoint> EdgePoints { get; set; } = new List<EdgePoint>();
        public List<EdgePair> EdgePairs { get; set; } = new List<EdgePair>();
        public string ErrorMessage { get; set; } = string.Empty;
    }
}
