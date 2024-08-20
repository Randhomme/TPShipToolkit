using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TPShipToolkit.TypeConverter;

namespace TPShipToolkit.MsbData
{
    public class Motion
    {
        private int _nodeId = -1;
        private StringBuilder _parentName;
        private List<Keyframe>[] _channels = new List<Keyframe>[6];
        private readonly List<StringBuilder> _elementsName;


        [Browsable(false)]
        public int NodeId { get => _nodeId; set => _nodeId = value; }

        [Description("The element of this animation part."), TypeConverter(typeof(FormatStringConverter))]
        public string Node
        {
            get => _parentName != null ? _parentName.ToString() : "None";
            set => _parentName = FindParentName(value);
        }

        public List<Keyframe>[] Channels { get => _channels; }

        public Motion(List<StringBuilder> elementsName)
        {
            //initialize the keyframe lists
            for (int i = 0; i < 6; i++)
            {
                _channels[i] = new List<Keyframe>();
            }
            _elementsName = elementsName;
        }

        public void AddKeyframe(int motion, Keyframe keyframe)
        {
            _channels[motion].Add(keyframe);
        }

        public List<StringBuilder> GetParentsName()
        {
            return _elementsName;
        }

        public void UpdateParentName()
        {
            if (_nodeId >= 0 && _nodeId < _elementsName.Count)
            {
                _parentName = _elementsName[_nodeId];
            }
        }

        private StringBuilder FindParentName(string name)
        {
            for (int i = 0; i < _elementsName.Count; i++)
            {
                if (_elementsName[i].ToString().Equals(name))
                {
                    return _elementsName[i];
                }
            }
            return null;
        }

        public override string ToString()
        {
            return _parentName != null ? _parentName.ToString() : "Motion";
        }
    }
}
