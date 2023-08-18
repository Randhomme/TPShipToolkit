using System.Collections.Generic;
using System.Text;

namespace TPShipToolkit.MsbData
{
    public class Bone : Element
    {
        public string InfluenceMapName { get; set; }
        public float RestLength { get; set; }

        public Bone(List<StringBuilder> elementsname, StringBuilder name) : base(elementsname, name)
        {
        }
    }
}
