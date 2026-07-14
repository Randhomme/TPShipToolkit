using System.Collections.Generic;
using TPShipToolkit.MdbData.Structs;

namespace TPShipToolkit.MdbData.Classes
{
    public class MdbMeshModel
    {
        public IList<MdbVertex> MdbVertices { get; }
        public IList<MdbTriangle> MdbTriangles { get; }

        public MdbMeshModel()
        {
            MdbVertices = new List<MdbVertex>();
            MdbTriangles = new List<MdbTriangle>();
        }
    }
}
