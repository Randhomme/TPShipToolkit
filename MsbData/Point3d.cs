using System.ComponentModel;

namespace TPShipToolkit.MsbData
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public class Point3d
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Point3d()
        {
            X = 0;
            Y = 0;
            Z = 0;
        }

        public override string ToString()
        {
            return X + "; " + Y + "; " + Z;
        }
    }
}
