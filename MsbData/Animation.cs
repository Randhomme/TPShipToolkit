using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Text;
using TPShipToolkit.TypeConverter;

namespace TPShipToolkit.MsbData
{
    public class Animation
    {
        private StringBuilder _displayedName;
        private float _duration;
        private readonly List<Motion> _motions = new List<Motion>();
        private readonly List<StringBuilder> _elementsName;

        [Description("The displayed name of this element in this program. This name won't be used in the mesh scene file.")]
        public string DisplayedName
        {
            get => _displayedName.ToString();
            set
            {
                if (_elementsName != null)
                {
                    //check if the name isn't already in the list
                    bool contains = false;
                    for (int i = 0; i < _elementsName.Count; i++)
                    {
                        if (_elementsName[i].ToString().Equals(value))
                        {
                            contains = true;
                            break;
                        }
                    }
                    //invalid name exception
                    if (contains)
                    {
                        throw new ArgumentException("An element with the same name already exists. Please choose an other name.");
                    }
                    else if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new ArgumentException("Invalid name.");
                    }
                    //set the name if no error
                    _displayedName.Clear();
                    _displayedName.Append(value);
                }
                else
                {
                    throw new ArgumentNullException("Failed to get the element list.");
                }
            }
        }

        [Description("The real name of this element, used in the mesh scene file.")]
        public string RealName { get; set; }

        [Description("The total duration of the animation.")]
        public float Duration { get => _duration; set => _duration = value; }

        [Description("The animation elements."), Editor(typeof(MotionCollectionEditor), typeof(UITypeEditor))]
        public List<Motion> Motions { get => _motions; }

        public Animation(List<StringBuilder> elementsname, StringBuilder name)
        {
            _displayedName = name;
            _elementsName = elementsname;
            RealName = name.ToString();
        }

        public void AddMotion(Motion motion)
        {
            _motions.Add(motion);
        }

        public void UpdateMotionNode()
        {
            foreach(Motion motion in _motions)
            {
                motion.UpdateParentName();
            }
        }

        public List<StringBuilder> GetElementsName()
        {
            return _elementsName;
        }

        //Using this to rename if needed and avoid an other and unecessary check from the public setter of Name
        public void ProcessName()
        {
            bool nameNeedsChange = false;
            var nodeName = DisplayedName;
            for (int i = 0; i < _elementsName.Count; i++)
            {
                var s = _elementsName[i].ToString();
                if (s.Equals(nodeName))
                {
                    nameNeedsChange = true;
                    break;
                }
            }
            if (nameNeedsChange)
            {
                int maxNodeNumber = 0, index = nodeName.LastIndexOf('_');
                if (index != -1)
                {
                    string temp = nodeName.Substring(index + 1);
                    if (int.TryParse(temp, out _))
                        nodeName = nodeName.Remove(index);
                }
                for (int i = 0; i < _elementsName.Count; i++)
                {
                    var s = _elementsName[i].ToString();
                    int tempNodeNumber = 0, sIndex = s.LastIndexOf('_');
                    if (sIndex != -1)
                    {
                        string temp = s.Substring(sIndex + 1);
                        if (int.TryParse(temp, out tempNodeNumber))
                        {
                            s = s.Remove(sIndex);
                        }
                    }
                    if (s.Equals(nodeName))
                        if (tempNodeNumber > maxNodeNumber)
                            maxNodeNumber = tempNodeNumber;
                }
                if (maxNodeNumber == int.MaxValue)
                    throw new Exception();
                _displayedName.Clear();
                _displayedName.Append(nodeName + "_" + (maxNodeNumber + 1));
            }
        }
        public override string ToString()
        {
            return _displayedName.ToString();
        }
    }
}
