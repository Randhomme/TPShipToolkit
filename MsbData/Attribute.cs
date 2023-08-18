using TPShipToolkit.Enums;

namespace TPShipToolkit.MsbData
{
    public class Attribute
    {
        public AttributeName AttributeName { get; set; }
        public string DescriptorName { get; set; }

        public override string ToString()
        {
            return DescriptorName;
        }
    }
}
