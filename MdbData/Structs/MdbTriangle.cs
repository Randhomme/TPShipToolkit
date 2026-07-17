namespace TPShipToolkit.MdbData.Structs
{
    public struct MdbTriangle
    {
        public ushort P0;
        public ushort P1;
        public ushort P2;
        public ushort TextureIndex;

        public MdbTriangle(ushort p0, ushort p1, ushort p2, ushort textureIndex)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            TextureIndex = textureIndex;
        }

        public override string ToString()
        {
            return $"{P0}, {P1}, {P2}";
        }
    }
}
