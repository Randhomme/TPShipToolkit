using System.ComponentModel;

namespace TPShipToolkit.MsbData
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class Rotation
    {
        [DisplayName("1 - Yaw")]
        [Description("Angle in degree")]
        public float Yaw { get; set; }
        [DisplayName("2 - Pitch")]
        [Description("Angle in degree")]
        public float Pitch { get; set; }
        [DisplayName("3 - Roll")]
        [Description("Angle in degree")]
        public float Roll { get; set; }

        public Rotation()
        {
            Yaw = 0;
            Pitch = 0;
            Roll = 0;
        }

        public override string ToString()
        {
            return Yaw + "; " + Pitch + "; " + Roll;
        }
    }
}
