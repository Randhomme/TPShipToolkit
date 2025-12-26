using System.IO;
using System.Xml.Serialization;

namespace TPShipToolkit.Settings
{
    /// <summary>
    /// Some settings to make the user life easier.
    /// </summary>
    public class AppSettings
    {
        private string filename = "TPShipToolkit.xml";
        public string TextureDirectory { get; set; } = "";
        public string OpenMdbDirectory { get; set; } = "";
        public string OpenObjDirectory { get; set; } = "";
        public string SaveObjDirectory { get; set; } = "";
        public string OpenMsbDirectory { get; set; } = "";
        public string SaveMsbDirectory { get; set; } = "";
        public bool XMdbTo1Obj { get; set; } = true;
        public bool ObjToXMdb { get; set; } = true;
        public bool ExportCBox { get; set; } = false;
        public bool AutoCBox { get; set; } = true;
        public bool ExportLods { get; set; } = true;

        public AppSettings Load()
        {
            try
            {
                using (StreamReader reader = new StreamReader(File.OpenRead(filename)))
                {
                    XmlSerializer xmls = new XmlSerializer(typeof(AppSettings));
                    return xmls.Deserialize(reader) as AppSettings;
                }
            }
            catch
            {
                Save();
                return this;
            }
        }

        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(File.Open(filename, FileMode.Create, FileAccess.ReadWrite)))
                {
                    XmlSerializer xmls = new XmlSerializer(typeof(AppSettings));
                    xmls.Serialize(writer, this);
                }
            }
            catch { }
        }
    }
}
