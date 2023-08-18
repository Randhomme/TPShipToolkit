using System.Numerics;

namespace TPShipToolkit.MdbData
{
    public class CollisionBox
    {
        public string BoxName;
        public uint Level = 0;
        public Vector3 Position;
        public Vector3 OCross;
        public Vector3 OForward;
        public Vector3 OUp;
        public Vector3 Length;
        public CollisionBox Leftchild { get; set; }
        public CollisionBox Rightchild { get; set; }
    }
}
