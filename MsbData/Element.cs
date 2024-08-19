using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using TPShipToolkit.TypeConverter;

namespace TPShipToolkit.MsbData
{
    /// <summary>
    /// Parent class for a mesh scene element.
    /// </summary>
    public class Element
    {
        private int _parentId = -1;
        protected StringBuilder _parentName;
        protected StringBuilder _name;
        private Point3d _pivot = new Point3d();
        private Point3d _position = new Point3d();
        private Point3d _scale = new Point3d();
        private readonly List<Attribute> _attributes = new List<Attribute>();
        protected readonly List<StringBuilder> _elementsName;

        [Browsable(false)]
        public int ParentId { get => _parentId; set => _parentId = value; }

        [Description("The element it's attached to."), TypeConverter(typeof(FormatStringConverter))]
        public string ParentName
        {
            get => _parentName != null ? _parentName.ToString() : "None";
            set => _parentName = FindParentName(value);
        }

        [Description("Element name.")]
        public string Name
        {
            get => _name.ToString();
            set
            {
                if (value == "None")
                    throw new ArgumentException("This name is used by the program. It's better to not use it.");
                else if (_elementsName != null)
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
                    _name.Clear();
                    _name.Append(value);
                }
                else
                {
                    throw new ArgumentNullException("Failed to get the element list.");
                }
            }
        }
        [Category("Position and scale"), Description("The point from where the object is scaled.")]
        public Point3d Pivot { get => _pivot; }
        [Category("Position and scale"), Description("The position of the object.")]
        public Point3d Position { get => _position; }
        [Category("Position and scale"), Description("The scale of the object.")]
        public Point3d Scale { get => _scale; }
        [Description("Attribute collection for the element.")]
        public List<Attribute> Attributes { get => _attributes; }

        public void AddAttribute(Attribute attribute)
        {
            _attributes.Add(attribute);
        }

        public Element(List<StringBuilder> elementsname, StringBuilder name)
        {
            _name = name;
            _elementsName = elementsname;
        }

        public List<StringBuilder> GetParentsName()
        {
            return _elementsName;
        }

        public void UpdateParentName()
        {
            if (_parentId >= 0 && _parentId < _elementsName.Count)
            {
                _parentName = _elementsName[_parentId];
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

        public uint GetParentId()
        {
            if(_parentName!=null)
            {
                string parentname = _parentName.ToString();
                for (int i = 0; i < _elementsName.Count; i++)
                {
                    if (_elementsName[i].ToString().Equals(parentname))
                    {
                        return (uint)i-1;
                    }
                }
            }
            return 0xFFFFFFFF;
        }

        //Using this to rename if needed and avoid an other and unecessary check from the public setter of Name
        public void ProcessName()
        {
            bool nameNeedsChange = false;
            var nodeName = Name;
            for (int i = 0; i < _elementsName.Count; i++)
            {
                var s = _elementsName[i].ToString();
                if(s.Equals(nodeName))
                {
                    nameNeedsChange = true;
                    break;
                }
            }
            if(nameNeedsChange)
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
                _name.Clear();
                _name.Append(nodeName + "_" + (maxNodeNumber + 1));
            }
        }

        public override string ToString()
        {
            return _name.ToString();
        }
    }
}
