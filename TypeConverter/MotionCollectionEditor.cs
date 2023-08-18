using System;
using System.ComponentModel.Design;
using TPShipToolkit.MsbData;

namespace TPShipToolkit.TypeConverter
{
    public class MotionCollectionEditor : CollectionEditor
    {
        public MotionCollectionEditor(Type type) : base(type)
        {
        }

        protected override object CreateInstance(Type itemType)
        {
            if (Context != null)
            {
                if (Context.Instance is Animation animation)
                {
                    return new Motion(animation.GetElementsName()); ;
                }
            }
            return base.CreateInstance(itemType);
        }
    }
}
