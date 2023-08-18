namespace TPShipToolkit.MdbData
{
    public class Material
    {
        public string MatName;
        public string TexName;

        public Material() { }

        public Material(string matName, string texName)
        {
            MatName = matName;
            TexName = texName;
        }
    }
}
