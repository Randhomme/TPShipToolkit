using System.ComponentModel;
using TPShipToolkit.MsbData;

namespace TPShipToolkit.TypeConverter
{
    public class FormatStringConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return true;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            if (context.Instance is Element e)
            {
                return new StandardValuesCollection(e.GetParentsName());
            }
            if (context.Instance is Motion f)
            {
                return new StandardValuesCollection(f.GetParentsName());
            }
            return base.GetStandardValues(context);
        }
    }
}
