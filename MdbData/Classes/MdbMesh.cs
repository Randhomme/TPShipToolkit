using System.Collections.Generic;

namespace TPShipToolkit.MdbData.Classes
{
    public class MdbMesh
    {
        public string GroupName { get; set; }
        public IList<MdbMeshModel> MeshModels { get; }

        public MdbMesh(string groupName)
        {
            GroupName = groupName;
            MeshModels = new List<MdbMeshModel>();
        }
    }
}
