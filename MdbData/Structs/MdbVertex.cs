namespace TPShipToolkit.MdbData.Structs
{
    public struct MdbVertex
    {
        public float X, Y, Z;
        public float U, V;
        public float NX, NY;
        public byte R, G, B, A;

        public MdbVertex(float x, float y, float z, float u, float v, float nx, float ny, byte r, byte g, byte b, byte a)
        {
            X = x;
            Y = y;
            Z = z;
            U = u;
            V = v;
            NX = nx;
            NY = ny;
            R = r;
            G = g;
            B = b;
            A = a;
        }
    }
}
