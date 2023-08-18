namespace TPShipToolkit.MsbData
{
    public class Keyframe
    {
        public float Time { get; set; }
        public float Value { get; set; }
        public uint Smoothing { get; set; }
        public float Tension { get; set; }
        public float Continuity { get; set; }
        public float Bias { get; set; }
        public float IncomingTangent { get; set; }
        public float OutgoingTangent { get; set; }
		
		public override string ToString()
        {
            return "Keyframe";
        }
    }
}
