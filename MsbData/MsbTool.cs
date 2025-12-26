using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using TPShipToolkit.Enums;

namespace TPShipToolkit.MsbData
{
    public class MsbTool
    {
        private int _parentcount = 0;
        private readonly List<Element> _nodes = new List<Element>();
        private readonly List<Element> _meshes = new List<Element>();
        private readonly List<Bone> _bones = new List<Bone>();
        private readonly List<Animation> _animations = new List<Animation>();
        private readonly List<StringBuilder> _elementsName = new List<StringBuilder>() { new("None") };

        public List<Element> GetNodes()
        {
            return _nodes;
        }
        public List<Element> GetMeshes()
        {
            return _meshes;
        }
        public List<Bone> GetBones()
        {
            return _bones;
        }
        public List<Animation> GetAnimations()
        {
            return _animations;
        }
        public List<StringBuilder> GetElementsName()
        {
            return _elementsName;
        }

        /// <summary>
        /// Import the data from one or multiple mesh scene file, and add the elements to the TreeView.
        /// </summary>
        /// <param name="msbs">Msb files path.</param>
        /// <param name="treeView">The TreeView that contains the msb elements.</param>
        /// <param name="progress">Progress on the progress bar.</param>
        /// <param name="logs">Logs in the text box.</param>
        public void Import(string[] msbs, TreeView treeView,IProgress<int> progress, IProgress<string> logs)
        {
            for(int i=0;i<msbs.Length;i++)
            {
                var msb = msbs[i];
                logs.Report("---- " + Path.GetFileName(msb) + " ----\n");
                Import(msb, treeView, logs);
                logs.Report("\n");
                progress.Report(i + 1);
            }
        }

        /// <summary>
        /// Import the data from one mesh scene file, and add the elements to the TreeView.
        /// </summary>
        /// <param name="path">Msb file path.</param>
        /// <param name="treeView">The TreeView that contains the msb elements.</param>
        /// <param name="logs">Logs in the text box.</param>
        private void Import(string path, TreeView treeView, IProgress<string> logs)
        {
            try
            {
                using (BinaryReader reader = new BinaryReader(File.OpenRead(path)))
                {
                    _parentcount = _elementsName.Count;
                    reader.BaseStream.Seek(0, SeekOrigin.Begin);
                    ReadHeaderData(reader, logs);
                    ImportNodes(reader, treeView, logs);
                    ImportMeshes(reader, treeView, logs);
                    ImportBones(reader, treeView, logs);
                    ImportAnimations(reader, treeView, logs);
                    UpdateParentName();
                    reader?.Dispose();
                }
            }
            catch(Exception ex)
            {
                logs.Report(ex.Message + "\n");
            }
        }

        /// <summary>
        /// Export the elements to a new mesh scene file.
        /// </summary>
        /// <param name="path">Msb path.</param>
        /// <param name="logs">Logs in the text box.</param>
        public void Export(string path, IProgress<string> logs)
        {
            try
            {
                using(BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
                {
                    List<string> msbstruct = new List<string>
                    {
                        "Mesh Scene Data",
                        "Name",
                        "ID",
                        "Nodes - Size"
                    };
                    string meshscenename = Path.GetFileNameWithoutExtension(path);
                    int blockLength, nodescount = _nodes.Count, meshescount = _meshes.Count, bonescount = _bones.Count, animationscount = _animations.Count,
                    //a = meshes size, b = bones size, c = animations size, d = parent id, e = influence map name
                    a, b, c, d, e;
                    long posbloc, pos;

                    //structure building
                    if (nodescount > 0)
                    {
                        a = 13;
                        d = 5;
                        msbstruct.Add("Nodes - Element");
                        msbstruct.Add("Parent ID");
                        msbstruct.Add("Type");
                        msbstruct.Add("Pivot Position");
                        msbstruct.Add("Element");
                        msbstruct.Add("Attributes - Size");
                        msbstruct.Add("Attributes - Element");
                        msbstruct.Add("AttributeName");
                        msbstruct.Add("DescriptorName");
                        msbstruct.Add("Meshes - Size");
                        if (meshescount > 0)
                        {
                            msbstruct.Add("Meshes - Element");
                        }
                        b = msbstruct.Count;
                        e = b + 2;
                        msbstruct.Add("Bones - Size");
                        if (bonescount > 0)
                        {
                            msbstruct.Add("Bones - Element");
                            msbstruct.Add("Influence Map Name");
                            msbstruct.Add("Rest Length");
                        }
                        c = msbstruct.Count;
                        msbstruct.Add("Animations - Size");
                        if (animationscount > 0)
                        {
                            msbstruct.Add("Animations - Element");
                            msbstruct.Add("Duration");
                            msbstruct.Add("Node Motion Count");
                            msbstruct.Add("Node ID");
                            msbstruct.Add("Motion");
                            msbstruct.Add("Channel");
                            msbstruct.Add("Keyframes - Size");
                            msbstruct.Add("Keyframes - Element");
                            msbstruct.Add("Time");
                            msbstruct.Add("Value");
                            msbstruct.Add("Smoothing");
                            msbstruct.Add("Tension");
                            msbstruct.Add("Continuity");
                            msbstruct.Add("Bias");
                            msbstruct.Add("Incoming Tangent");
                            msbstruct.Add("Outgoing Tangent");
                        }
                    }
                    else
                    {
                        a = 4;
                        d = 6;
                        msbstruct.Add("Meshes - Size");
                        if (meshescount > 0)
                        {
                            msbstruct.Add("Nodes - Element");
                            msbstruct.Add("Parent ID");
                            msbstruct.Add("Type");
                            msbstruct.Add("Pivot Position");
                            msbstruct.Add("Element");
                            msbstruct.Add("Attributes - Size");
                            msbstruct.Add("Attributes - Element");
                            msbstruct.Add("AttributeName");
                            msbstruct.Add("DescriptorName");
                            b = msbstruct.Count;
                            e = b + 2;
                            msbstruct.Add("Bones - Size");
                            if (bonescount > 0)
                            {
                                msbstruct.Add("Bones - Element");
                                msbstruct.Add("Influence Map Name");
                                msbstruct.Add("Rest Length");
                            }
                            c = msbstruct.Count;
                            msbstruct.Add("Animations - Size");
                            if (animationscount > 0)
                            {
                                msbstruct.Add("Animations - Element");
                                msbstruct.Add("Duration");
                                msbstruct.Add("Node Motion Count");
                                msbstruct.Add("Node ID");
                                msbstruct.Add("Motion");
                                msbstruct.Add("Channel");
                                msbstruct.Add("Keyframes - Size");
                                msbstruct.Add("Keyframes - Element");
                                msbstruct.Add("Time");
                                msbstruct.Add("Value");
                                msbstruct.Add("Smoothing");
                                msbstruct.Add("Tension");
                                msbstruct.Add("Continuity");
                                msbstruct.Add("Bias");
                                msbstruct.Add("Incoming Tangent");
                                msbstruct.Add("Outgoing Tangent");
                            }
                        }
                        else
                        {
                            b = 5;
                            d = 7;
                            e = 15;
                            msbstruct.Add("Bones - Size");
                            if (bonescount > 0)
                            {
                                msbstruct.Add("Bones - Element");
                                msbstruct.Add("Parent ID");
                                msbstruct.Add("Type");
                                msbstruct.Add("Pivot Position");
                                msbstruct.Add("Element");
                                msbstruct.Add("Attributes - Size");
                                msbstruct.Add("Attributes - Element");
                                msbstruct.Add("AttributeName");
                                msbstruct.Add("DescriptorName");
                                msbstruct.Add("Influence Map Name");
                                msbstruct.Add("Rest Length");
                            }
                            c = msbstruct.Count;
                            msbstruct.Add("Animations - Size");
                            if (animationscount > 0)
                            {
                                msbstruct.Add("Animations - Element");
                                msbstruct.Add("Duration");
                                msbstruct.Add("Node Motion Count");
                                msbstruct.Add("Node ID");
                                msbstruct.Add("Motion");
                                msbstruct.Add("Channel");
                                msbstruct.Add("Keyframes - Size");
                                msbstruct.Add("Keyframes - Element");
                                msbstruct.Add("Time");
                                msbstruct.Add("Value");
                                msbstruct.Add("Smoothing");
                                msbstruct.Add("Tension");
                                msbstruct.Add("Continuity");
                                msbstruct.Add("Bias");
                                msbstruct.Add("Incoming Tangent");
                                msbstruct.Add("Outgoing Tangent");
                            }
                        }
                    }

                    //msb definition + nodes size
                    try
                    {
                        List<string> msbstructs = new List<string>();

                        //file definition
                        writer.Write((double)0);
                        writer.Write(0);
                        msbstructs.Add("Mesh Scene Data");

                        //name
                        writer.Write(1);
                        writer.Write(meshscenename.Length);
                        writer.Write(Encoding.Default.GetBytes(meshscenename));
                        msbstructs.Add("Name");

                        //id
                        writer.Write(2);
                        var rootId = 0;
                        // Find the first root node or mesh
                        //nodes loop
                        for (int i = 0; i < nodescount; i++)
                        {
                            var node = _nodes[i];
                            if (GetNewParentId(node) == -1)
                            {
                                rootId = i;
                                break;
                            }
                        }
                        //meshes loop
                        for (int i = 0; i < meshescount; i++)
                        {
                            var mesh = _meshes[i];
                            if (GetNewParentId(mesh) == -1)
                            {
                                rootId = i + nodescount;
                                break;
                            }
                        }
                        writer.Write(rootId);
                        msbstructs.Add("ID");

                        //nodes - size
                        writer.Write(3);
                        writer.Write(nodescount);
                        msbstructs.Add("Nodes - Size");
                    }
                    catch
                    {
                        logs.Report("Failed to write the msb file.\n");
                        return;
                    }

                    //nodes loop
                    for (int i = 0; i < nodescount; i++)
                    {
                        try
                        {
                            Element node = _nodes[i];
                            logs.Report("Writting node " + node.DisplayedName + "... ");

                            //nodes element
                            writer.Write(4);
                            posbloc = writer.BaseStream.Position;
                            writer.Write(0);

                            //id
                            writer.Write(2);
                            writer.Write(i);

                            //parent id
                            writer.Write(5);
                            //writer.Write(node.GetParentId());
                            writer.Write(GetNewParentId(node));

                            //type
                            writer.Write(6);
                            writer.Write(0);

                            //name
                            writer.Write(1);
                            writer.Write(node.RealName.Length);
                            writer.Write(Encoding.Default.GetBytes(node.RealName));

                            //pivot position
                            writer.Write(7);
                            writer.Write(node.Pivot.X);
                            writer.Write(node.Pivot.Y);
                            writer.Write(node.Pivot.Z);

                            //element (pos x, y, z and scale x, y, z)
                            writer.Write(8);
                            writer.Write(node.Position.X);
                            writer.Write(8);
                            writer.Write(node.Position.Y);
                            writer.Write(8);
                            writer.Write(node.Position.Z);
                            writer.Write(8);
                            writer.Write(node.Scale.X);
                            writer.Write(8);
                            writer.Write(node.Scale.Y);
                            writer.Write(8);
                            writer.Write(node.Scale.Z);

                            //attribute size
                            writer.Write(9);
                            writer.Write(node.Attributes.Count);
                            foreach (Attribute attribute in node.Attributes)
                            {
                                //hasnodeattribute = true;
                                //attribute - element
                                writer.Write(10);
                                pos = writer.BaseStream.Position;
                                writer.Write(0);

                                //attribute name
                                writer.Write(11);
                                writer.Write(attribute.AttributeName.ToString().Length);
                                writer.Write(Encoding.Default.GetBytes(attribute.AttributeName.ToString()));

                                //descriptor name
                                writer.Write(12);
                                writer.Write(attribute.DescriptorName.Length);
                                writer.Write(Encoding.Default.GetBytes(attribute.DescriptorName));
                                blockLength = (int)(writer.BaseStream.Position - pos - 4);
                                writer.BaseStream.Seek(pos, SeekOrigin.Begin);
                                writer.Write(blockLength);
                                writer.BaseStream.Seek(0, SeekOrigin.End);
                            }
                            blockLength = (int)(writer.BaseStream.Position - posbloc - 4);
                            writer.BaseStream.Seek(posbloc, SeekOrigin.Begin);
                            writer.Write(blockLength);
                            writer.BaseStream.Seek(0, SeekOrigin.End);
                            logs.Report("Done\n");
                        }
                        catch
                        {
                            logs.Report("Failed to write node " + (i + 1) + ".\n");
                            return;
                        }
                    }

                    //meshes - size
                    try
                    {
                        writer.Write(a);
                        writer.Write(meshescount);
                    }
                    catch
                    {
                        logs.Report("Failed to write meshes size.\n");
                        return;
                    }

                    //meshes loop
                    for (int i = 0; i < meshescount; i++)
                    {
                        try
                        {
                            Element mesh = _meshes[i];
                            logs.Report("Writting mesh " + mesh.DisplayedName + "... ");

                            //meshes - element
                            writer.Write(a + 1);
                            posbloc = writer.BaseStream.Position;
                            writer.Write(0);

                            //id
                            writer.Write(2);
                            writer.Write(i + nodescount);

                            //parent id
                            writer.Write(d);
                            //writer.Write(mesh.GetParentId());
                            writer.Write(GetNewParentId(mesh));

                            //type
                            writer.Write(d + 1);
                            writer.Write(1);

                            //name
                            writer.Write(1);
                            writer.Write(mesh.RealName.Length);
                            writer.Write(Encoding.Default.GetBytes(mesh.RealName));

                            //pivot position
                            writer.Write(d + 2);
                            writer.Write(mesh.Pivot.X);
                            writer.Write(mesh.Pivot.Y);
                            writer.Write(mesh.Pivot.Z);

                            //element (pos x, y, z and scale x, y, z)
                            writer.Write(d + 3);
                            writer.Write(mesh.Position.X);
                            writer.Write(d + 3);
                            writer.Write(mesh.Position.Y);
                            writer.Write(d + 3);
                            writer.Write(mesh.Position.Z);
                            writer.Write(d + 3);
                            writer.Write(mesh.Scale.X);
                            writer.Write(d + 3);
                            writer.Write(mesh.Scale.Y);
                            writer.Write(d + 3);
                            writer.Write(mesh.Scale.Z);

                            //attribute - size
                            writer.Write(d + 4);
                            writer.Write(mesh.Attributes.Count);
                            foreach (Attribute attribute in mesh.Attributes)
                            {
                                //hasmeshattribute = true;
                                //attribute - element
                                writer.Write(d + 5);
                                pos = writer.BaseStream.Position;
                                writer.Write(0);

                                //attribute name
                                writer.Write(d + 6);
                                writer.Write(attribute.AttributeName.ToString().Length);
                                writer.Write(Encoding.Default.GetBytes(attribute.AttributeName.ToString()));

                                //descriptor name
                                writer.Write(d + 7);
                                writer.Write(attribute.DescriptorName.Length);
                                writer.Write(Encoding.Default.GetBytes(attribute.DescriptorName));
                                blockLength = (int)(writer.BaseStream.Position - pos - 4);
                                writer.BaseStream.Seek(pos, SeekOrigin.Begin);
                                writer.Write(blockLength);
                                writer.BaseStream.Seek(0, SeekOrigin.End);
                            }
                            blockLength = (int)(writer.BaseStream.Position - posbloc - 4);
                            writer.BaseStream.Seek(posbloc, SeekOrigin.Begin);
                            writer.Write(blockLength);
                            writer.BaseStream.Seek(0, SeekOrigin.End);
                            logs.Report("Done\n");
                        }
                        catch
                        {
                            logs.Report("Failed to write mesh " + (i + 1) + ".\n");
                        }
                    }

                    //bones - size
                    try
                    {
                        writer.Write(b);
                        writer.Write(bonescount);
                    }
                    catch
                    {
                        logs.Report("Failed to write meshes size.\n");
                        return;
                    }

                    //bones loop
                    for (int i = 0; i < bonescount; i++)
                    {
                        try
                        {
                            Bone bone = _bones[i];
                            logs.Report("Writting bone " + bone.DisplayedName + "... ");

                            //bones - element
                            writer.Write(b + 1);
                            posbloc = writer.BaseStream.Position;
                            writer.Write(0);

                            //id
                            writer.Write(2);
                            writer.Write(i + nodescount + meshescount);

                            //parent id
                            writer.Write(d);
                            //writer.Write(bone.GetParentId());
                            writer.Write(GetNewParentId(bone));

                            //type
                            writer.Write(d + 1);
                            writer.Write(2);

                            //name
                            writer.Write(1);
                            writer.Write(bone.RealName.Length);
                            writer.Write(Encoding.Default.GetBytes(bone.RealName));

                            //pivot position
                            writer.Write(d + 2);
                            writer.Write(bone.Pivot.X);
                            writer.Write(bone.Pivot.Y);
                            writer.Write(bone.Pivot.Z);

                            //element (pos x, y, z and scale x, y, z)
                            writer.Write(d + 3);
                            writer.Write(bone.Position.X);
                            writer.Write(d + 3);
                            writer.Write(bone.Position.Y);
                            writer.Write(d + 3);
                            writer.Write(bone.Position.Z);
                            writer.Write(d + 3);
                            writer.Write(bone.Scale.X);
                            writer.Write(d + 3);
                            writer.Write(bone.Scale.Y);
                            writer.Write(d + 3);
                            writer.Write(bone.Scale.Z);

                            //attribute - size
                            writer.Write(d + 4);
                            writer.Write(bone.Attributes.Count);
                            foreach (Attribute attribute in bone.Attributes)
                            {
                                //hasmeshattribute = true;
                                //attribute - element
                                writer.Write(d + 5);
                                pos = writer.BaseStream.Position;
                                writer.Write(0);

                                //attribute name
                                writer.Write(d + 6);
                                writer.Write(attribute.AttributeName.ToString().Length);
                                writer.Write(Encoding.Default.GetBytes(attribute.AttributeName.ToString()));

                                //descriptor name
                                writer.Write(d + 7);
                                writer.Write(attribute.DescriptorName.Length);
                                writer.Write(Encoding.Default.GetBytes(attribute.DescriptorName));
                                blockLength = (int)(writer.BaseStream.Position - pos - 4);
                                writer.BaseStream.Seek(pos, SeekOrigin.Begin);
                                writer.Write(blockLength);
                                writer.BaseStream.Seek(0, SeekOrigin.End);
                            }

                            //influence map name
                            writer.Write(e);
                            writer.Write(bone.InfluenceMapName.Length);
                            writer.Write(Encoding.Default.GetBytes(bone.InfluenceMapName));

                            //rest length
                            writer.Write(e + 1);
                            writer.Write(bone.RestLength);

                            blockLength = (int)(writer.BaseStream.Position - posbloc - 4);
                            writer.BaseStream.Seek(posbloc, SeekOrigin.Begin);
                            writer.Write(blockLength);
                            writer.BaseStream.Seek(0, SeekOrigin.End);

                            logs.Report("Done\n");
                        }
                        catch
                        {
                            logs.Report("Failed to write node " + (i + 1) + ".\n");
                            return;
                        }
                    }

                    //animations - size
                    try
                    {
                        writer.Write(c);
                        writer.Write(animationscount);
                    }
                    catch
                    {
                        logs.Report("Failed to write animations size.\n");
                        return;
                    }

                    //animations loop
                    for (int i = 0; i < animationscount; i++)
                    {
                        try
                        {
                            Animation animation = _animations[i];
                            logs.Report("Writting animation " + animation.DisplayedName + "... ");

                            //animations - element
                            writer.Write(c + 1);
                            posbloc = writer.BaseStream.Position;
                            writer.Write(0);

                            //name
                            writer.Write(1);
                            writer.Write(animation.RealName.Length);
                            writer.Write(Encoding.Default.GetBytes(animation.RealName));

                            //duration
                            writer.Write(c + 2);
                            writer.Write(animation.Duration);

                            //node motion count
                            writer.Write(c + 3);
                            writer.Write(animation.Motions.Count);

                            //motion loop
                            foreach (Motion motion in animation.Motions)
                            {
                                //node id
                                writer.Write(c + 4);
                                //writer.Write(motion.GetParentId());
                                writer.Write(GetNewParentId(motion));

                                //motion definition
                                writer.Write(c + 5);
                                pos = writer.BaseStream.Position;
                                writer.Write(0);

                                //channel loop
                                for (int j = 0; j < 6; j++)
                                {
                                    var channel = motion.Channels[j];

                                    //channel definition
                                    writer.Write(c + 6);
                                    writer.Write(channel.Count * 72 + 8);

                                    //keyframe - size
                                    writer.Write(c + 7);
                                    writer.Write(channel.Count);

                                    //keyframe loop
                                    foreach (Keyframe keyframe in channel)
                                    {
                                        //keyframe - element
                                        writer.Write(c + 8);
                                        writer.Write(64);

                                        //time
                                        writer.Write(c + 9);
                                        writer.Write(keyframe.Time);

                                        //value
                                        writer.Write(c + 10);
                                        writer.Write(keyframe.Value);

                                        //smoothing
                                        writer.Write(c + 11);
                                        writer.Write(keyframe.Smoothing);

                                        //tension
                                        writer.Write(c + 12);
                                        writer.Write(keyframe.Tension);

                                        //continuity
                                        writer.Write(c + 13);
                                        writer.Write(keyframe.Continuity);

                                        //bias
                                        writer.Write(c + 14);
                                        writer.Write(keyframe.Bias);

                                        //incomming tangent
                                        writer.Write(c + 15);
                                        writer.Write(keyframe.IncomingTangent);

                                        //outgoing tangent
                                        writer.Write(c + 16);
                                        writer.Write(keyframe.OutgoingTangent);
                                    }
                                }
                                blockLength = (int)(writer.BaseStream.Position - pos - 4);
                                writer.BaseStream.Seek(pos, SeekOrigin.Begin);
                                writer.Write(blockLength);
                                writer.BaseStream.Seek(0, SeekOrigin.End);
                            }
                            blockLength = (int)(writer.BaseStream.Position - posbloc - 4);
                            writer.BaseStream.Seek(posbloc, SeekOrigin.Begin);
                            writer.Write(blockLength);
                            writer.BaseStream.Seek(0, SeekOrigin.End);

                            logs.Report("Done\n");
                        }
                        catch
                        {
                            logs.Report("Failed to write animation " + (i + 1) + ".\n");
                            return;
                        }
                    }

                    blockLength = (int)(writer.BaseStream.Position);
                    writer.BaseStream.Seek(0, SeekOrigin.Begin);
                    writer.Write(blockLength);
                    writer.BaseStream.Seek(8, SeekOrigin.Begin);
                    writer.Write(blockLength - 12);
                    writer.BaseStream.Seek(0, SeekOrigin.End);

                    //msb structure
                    writer.Write(msbstruct.Count);
                    foreach (string s in msbstruct)
                    {
                        writer.Write(s.Length);
                        writer.Write(Encoding.Default.GetBytes(s));
                    }
                }
            }
            catch
            {
                logs.Report("Failed to write the msb file.\n");
            }
        }

        private void ReadHeaderData(BinaryReader reader, IProgress<string> logs)
        {
            uint stringlength;
            //skip file size bytes
            try
            {
                reader.BaseStream.Seek(12, SeekOrigin.Current);
            }
            catch
            {
                throw new Exception("Failed to read the msb data.\n");
            }

            //01 00 00 00 for mesh scene name
            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Current);
                //mesh scene real name
                stringlength = reader.ReadUInt32();
                reader.BaseStream.Seek(stringlength, SeekOrigin.Current);
            }
            catch
            {
                throw new Exception("Failed to read the mesh scene name.\n");
            }

            //02 00 00 00 for mesh scene id (id of the root element)
            try
            {
                reader.BaseStream.Seek(8, SeekOrigin.Current);
            }
            catch
            {
                throw new Exception("Failed to read mesh scene type.\n");
            }
        }

        private void ImportNodes(BinaryReader reader, TreeView treeView, IProgress<string> logs)
        {
            uint stringlength, attributesize, nodeCount;
            //03 00 00 00 (node count)
            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Current);
                nodeCount = reader.ReadUInt32();
            }
            catch
            {
                throw new Exception("Failed to read the node count.\n");
            }

            //node loop
            for (uint i = 0; i < nodeCount; i++)
            {
                StringBuilder name = new StringBuilder();
                Element node = new Element(_elementsName, name);
                try
                {
                    //node definition
                    //skip 8 bytes (bloc size)
                    reader.BaseStream.Seek(8, SeekOrigin.Current);

                    //id
                    reader.BaseStream.Seek(8, SeekOrigin.Current);

                    //parent id
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    node.ParentId = reader.ReadInt32();
                    if (node.ParentId >= 0)
                        node.ParentId += _parentcount;

                    //type (00 for node)
                    reader.BaseStream.Seek(8, SeekOrigin.Current);

                    //name
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    stringlength = reader.ReadUInt32();
                    name.Append(new string(reader.ReadChars((int)stringlength)));
                    node.RealName = name.ToString();

                    //pivot position
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    node.Pivot.X = reader.ReadSingle();
                    node.Pivot.Y = reader.ReadSingle();
                    node.Pivot.Z = reader.ReadSingle();

                    //element (position and scale)
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    node.Position.X = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    node.Position.Y = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    node.Position.Z = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    node.Scale.X = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    node.Scale.Y = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    node.Scale.Z = reader.ReadSingle();

                    //attribute count
                    try
                    {
                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                        attributesize = reader.ReadUInt32();
                    }
                    catch
                    {
                        throw new Exception("Failed to read attribute count of element number " + (i + 1) + ".\n");
                    }

                    //attribute loop
                    for (uint j = 0; j < attributesize; j++)
                    {
                        try
                        {
                            Attribute attribute = new Attribute();
                            //attribute definition
                            reader.BaseStream.Seek(8, SeekOrigin.Current);
                            //attribute name
                            reader.BaseStream.Seek(4, SeekOrigin.Current);
                            stringlength = reader.ReadUInt32();
                            attribute.AttributeName = GetAttributeName(new string(reader.ReadChars((int)stringlength)));
                            //descriptor name
                            reader.BaseStream.Seek(4, SeekOrigin.Current);
                            stringlength = reader.ReadUInt32();
                            attribute.DescriptorName = new string(reader.ReadChars((int)stringlength));
                            node.AddAttribute(attribute);
                        }
                        catch
                        {
                            throw new Exception("Failed to read attribute " + (j + 1) + " of node " + (i + 1) + ".\n");
                        }
                    }

                    //add the node, fill the treeview
                    node.ProcessName();
                    _nodes.Add(node);
                    _elementsName.Add(name);
                    treeView.Invoke(() => treeView.Nodes[0].Nodes.Add(node.DisplayedName));
                    logs.Report("Adding node " + name + "\n");
                }
                catch
                {
                    throw new Exception("Failed to read the node number " + (i + 1) + ".\n");
                }
            }
        }

        private void ImportMeshes(BinaryReader reader, TreeView treeView, IProgress<string> logs)
        {
            uint meshcount, stringlength, attributesize;
            //mesh count
            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Current);
                meshcount = reader.ReadUInt32();
            }
            catch
            {
                throw new Exception("Failed to read the mesh count.\n");
            }

            //mesh loop
            for (uint i = 0; i < meshcount; i++)
            {
                StringBuilder name = new StringBuilder();
                Element mesh = new Element(_elementsName, name);
                try
                {
                    //mesh definition
                    //skip 8 bytes (bloc size)
                    reader.BaseStream.Seek(8, SeekOrigin.Current);

                    //id
                    reader.BaseStream.Seek(8, SeekOrigin.Current);

                    //parent id
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    mesh.ParentId = reader.ReadInt32();
                    if (mesh.ParentId >= 0)
                        mesh.ParentId += _parentcount;

                    //type (00 for node)
                    reader.BaseStream.Seek(8, SeekOrigin.Current);

                    //name
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    stringlength = reader.ReadUInt32();
                    name.Append(new string(reader.ReadChars((int)stringlength)));
                    mesh.RealName = name.ToString();

                    //pivot position
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    mesh.Pivot.X = reader.ReadSingle();
                    mesh.Pivot.Y = reader.ReadSingle();
                    mesh.Pivot.Z = reader.ReadSingle();

                    //element (position and scale)
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    mesh.Position.X = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    mesh.Position.Y = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    mesh.Position.Z = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    mesh.Scale.X = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    mesh.Scale.Y = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    mesh.Scale.Z = reader.ReadSingle();

                    //attribute count
                    try
                    {
                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                        attributesize = reader.ReadUInt32();
                    }
                    catch
                    {
                        throw new Exception("Failed to read attribute count of element number " + (i + 1) + ".\n");
                    }

                    //attribute loop
                    for (uint j = 0; j < attributesize; j++)
                    {
                        try
                        {
                            Attribute attribute = new Attribute();
                            //attribute definition
                            reader.BaseStream.Seek(8, SeekOrigin.Current);
                            //attribute name
                            reader.BaseStream.Seek(4, SeekOrigin.Current);
                            stringlength = reader.ReadUInt32();
                            attribute.AttributeName = GetAttributeName(new string(reader.ReadChars((int)stringlength)));
                            //descriptor name
                            reader.BaseStream.Seek(4, SeekOrigin.Current);
                            stringlength = reader.ReadUInt32();
                            attribute.DescriptorName = new string(reader.ReadChars((int)stringlength));
                            mesh.AddAttribute(attribute);
                        }
                        catch
                        {
                            throw new Exception("Failed to read attribute " + (j + 1) + " of mesh " + (i + 1) + ".\n");
                        }
                    }

                    //add the mesh, fill the treeview
                    mesh.ProcessName();
                    _meshes.Add(mesh);
                    _elementsName.Add(name);
                    treeView.Invoke(() => treeView.Nodes[1].Nodes.Add(mesh.DisplayedName));
                    logs.Report("Adding mesh " + name + "\n");
                }
                catch
                {
                    throw new Exception("Failed to read the mesh number " + (i + 1) + ".\n");
                }
            }
        }

        private void ImportBones(BinaryReader reader, TreeView treeView, IProgress<string> logs)
        {
            uint attributesize, bonecount, stringlength;
            //bone count
            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Current);
                bonecount = reader.ReadUInt32();
            }
            catch
            {
                throw new Exception("Failed to read the bone count.\n");
            }

            //bone loop
            for (int i = 0; i < bonecount; i++)
            {
                try
                {
                    StringBuilder name = new StringBuilder();
                    Bone bone = new Bone(_elementsName, name);


                    //bone definition
                    reader.BaseStream.Seek(8, SeekOrigin.Current);

                    //id
                    reader.BaseStream.Seek(8, SeekOrigin.Current);

                    //parent id
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    bone.ParentId = reader.ReadInt32();
                    if (bone.ParentId >= 0)
                        bone.ParentId += _parentcount;

                    //type
                    reader.BaseStream.Seek(8, SeekOrigin.Current);

                    //name
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    stringlength = reader.ReadUInt32();
                    name.Append(new string(reader.ReadChars((int)stringlength)));
                    bone.RealName = name.ToString();

                    //pivot position
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    bone.Pivot.X = reader.ReadSingle();
                    bone.Pivot.Y = reader.ReadSingle();
                    bone.Pivot.Z = reader.ReadSingle();

                    //element (position and scale)
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    bone.Position.X = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    bone.Position.Y = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    bone.Position.Z = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    bone.Scale.X = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    bone.Scale.Y = reader.ReadSingle();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    bone.Scale.Z = reader.ReadSingle();

                    //attribute count
                    try
                    {
                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                        attributesize = reader.ReadUInt32();
                    }
                    catch
                    {
                        throw new Exception("Failed to read attribute count of element number " + (i + 1) + ".\n");
                    }

                    //attribute loop
                    for (uint j = 0; j < attributesize; j++)
                    {
                        try
                        {
                            Attribute attribute = new Attribute();
                            //attribute definition
                            reader.BaseStream.Seek(8, SeekOrigin.Current);
                            //attribute name
                            reader.BaseStream.Seek(4, SeekOrigin.Current);
                            stringlength = reader.ReadUInt32();
                            attribute.AttributeName = GetAttributeName(new string(reader.ReadChars((int)stringlength)));
                            //descriptor name
                            reader.BaseStream.Seek(4, SeekOrigin.Current);
                            stringlength = reader.ReadUInt32();
                            attribute.DescriptorName = new string(reader.ReadChars((int)stringlength));
                            bone.AddAttribute(attribute);
                        }
                        catch
                        {
                            throw new Exception("Failed to read attribute " + (j + 1) + " of bone " + (i + 1) + ".\n");
                        }
                    }

                    //influence map name
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    stringlength = reader.ReadUInt32();
                    bone.InfluenceMapName = new string(reader.ReadChars((int)stringlength));

                    //rest length
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    bone.RestLength = reader.ReadSingle();

                    //add the node, fill the treeview
                    bone.ProcessName();
                    _bones.Add(bone);
                    _elementsName.Add(name);
                    treeView.Invoke(() => treeView.Nodes[2].Nodes.Add(bone.DisplayedName));
                    logs.Report("Adding bone " + name + "\n");
                }
                catch
                {
                    throw new Exception("Failed to read the bone number " + (i + 1) + ".\n");
                }
            }
        }

        private void ImportAnimations(BinaryReader reader, TreeView treeView, IProgress<string> logs)
        {
            uint animationcount, stringlength, nodemotioncount, keyframecount;
            //animation count
            try
            {
                reader.BaseStream.Seek(4, SeekOrigin.Current);
                animationcount = reader.ReadUInt32();
            }
            catch
            {
                throw new Exception("Failed to read the animation count.\n");
            }

            //animation loop
            for (int i = 0; i < animationcount; i++)
            {
                try
                {
                    StringBuilder name = new StringBuilder();
                    Animation animation = new Animation(_elementsName, name);

                    //animation definition
                    reader.BaseStream.Seek(8, SeekOrigin.Current);

                    //name
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    stringlength = reader.ReadUInt32();
                    name.Append(new string(reader.ReadChars((int)stringlength)));
                    animation.RealName = name.ToString();

                    //duration
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    animation.Duration = reader.ReadSingle();

                    //node motion count
                    try
                    {
                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                        nodemotioncount = reader.ReadUInt32();
                    }
                    catch
                    {
                        throw new Exception("Failed to read node motion count of animation number " + (i + 1) + ".\n");
                    }

                    //node motion loop
                    for (int j = 0; j < nodemotioncount; j++)
                    {
                        try
                        {
                            Motion motion = new Motion(_elementsName);

                            //node id
                            reader.BaseStream.Seek(4, SeekOrigin.Current);
                            motion.NodeId = reader.ReadInt32();
                            if (motion.NodeId >= 0)
                                motion.NodeId += _parentcount;

                            //node definition
                            reader.BaseStream.Seek(8, SeekOrigin.Current);

                            //channel loop
                            for (int k = 0; k < 6; k++)
                            {
                                try
                                {
                                    //channel definition
                                    reader.BaseStream.Seek(8, SeekOrigin.Current);

                                    //keyframe count
                                    try
                                    {
                                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                                        keyframecount = reader.ReadUInt32();
                                    }
                                    catch
                                    {
                                        throw new Exception("Failed to read keyframe count of channel number " + (k + 1) + ", motion " + (j + 1) + ", animation " + (i + 1) + "\n");
                                    }

                                    //keyframe loop
                                    for (int l = 0; l < keyframecount; l++)
                                    {
                                        Keyframe keyframe = new Keyframe();

                                        //keyframe definition
                                        reader.BaseStream.Seek(8, SeekOrigin.Current);

                                        //time
                                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                                        keyframe.Time = reader.ReadSingle();

                                        //value
                                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                                        keyframe.Value = reader.ReadSingle();

                                        //smoothing
                                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                                        keyframe.Smoothing = reader.ReadUInt32();

                                        //tension
                                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                                        keyframe.Tension = reader.ReadSingle();

                                        //continuity
                                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                                        keyframe.Continuity = reader.ReadSingle();

                                        //bias
                                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                                        keyframe.Bias = reader.ReadSingle();

                                        //incomming tangent
                                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                                        keyframe.IncomingTangent = reader.ReadSingle();

                                        //outgoing tangent
                                        reader.BaseStream.Seek(4, SeekOrigin.Current);
                                        keyframe.OutgoingTangent = reader.ReadSingle();

                                        //add the keyframe to the channel
                                        motion.AddKeyframe(k, keyframe);
                                    }
                                }
                                catch
                                {
                                    throw new Exception("Failed to read channel number " + (k + 1) + ", motion " + (j + 1) + ", animation " + (i + 1) + "\n");
                                }
                            }
                            animation.AddMotion(motion);
                        }
                        catch
                        {
                            throw new Exception("Failed to read motion " + (j + 1) + " in animation " + (i + 1) + ".\n");
                        }
                    }

                    //add animation, fill the treeview
                    animation.ProcessName();
                    _animations.Add(animation);
                    _elementsName.Add(name);
                    logs.Report("Adding animation " + name + "\n");
                    treeView.Invoke(() => treeView.Nodes[3].Nodes.Add(animation.DisplayedName));
                }
                catch
                {
                    throw new Exception("Failed to read animation number " + (i + 1) + ".\n");
                }
            }
        }

        private void UpdateParentName()
        {
            foreach(Element node in _nodes)
            {
                node.UpdateParentName();
            }
            foreach (Element mesh in _meshes)
            {
                mesh.UpdateParentName();
            }
            foreach (Bone bone in _bones)
            {
                bone.UpdateParentName();
            }
            foreach (Animation animation in _animations)
            {
                animation.UpdateMotionNode();
            }
        }

        private int GetNewParentId(Element element)
        {
            for(int i = 0; i < _nodes.Count; i++)
                if (element.ParentName.Equals(_nodes[i].DisplayedName))
                    return i;
            for (int i = 0; i < _meshes.Count; i++)
                if (element.ParentName.Equals(_meshes[i].DisplayedName))
                    return i + _nodes.Count;
            for (int i = 0; i < _bones.Count; i++)
                if (element.ParentName.Equals(_bones[i].DisplayedName))
                    return i + _nodes.Count + _meshes.Count;
            for (int i = 0; i < _animations.Count; i++)
                if (element.ParentName.Equals(_animations[i].DisplayedName))
                    return i + _nodes.Count + _meshes.Count + _bones.Count;
            return -1;
        }

        private int GetNewParentId(Motion motion)
        {
            for (int i = 0; i < _nodes.Count; i++)
                if (motion.Node.Equals(_nodes[i].DisplayedName))
                    return i;
            for (int i = 0; i < _meshes.Count; i++)
                if (motion.Node.Equals(_meshes[i].DisplayedName))
                    return i + _nodes.Count;
            for (int i = 0; i < _bones.Count; i++)
                if (motion.Node.Equals(_bones[i].DisplayedName))
                    return i + _nodes.Count + _meshes.Count;
            for (int i = 0; i < _animations.Count; i++)
                if (motion.Node.Equals(_animations[i].DisplayedName))
                    return i + _nodes.Count + _meshes.Count + _bones.Count;
            return -1;
        }

        private AttributeName GetAttributeName(string attributeName)
        {
            return attributeName switch
            {
                "BoardingEffectPoint" => AttributeName.BoardingEffectPoint,
                "DamageSection" => AttributeName.DamageSection,
                "DockPoint" => AttributeName.DockPoint,
                "EnginePortPlacement" => AttributeName.EnginePortPlacement,
                "FlagAttachmentPoint" => AttributeName.FlagAttachmentPoint,
                "GunMuzzlePlacement" => AttributeName.GunMuzzlePlacement,
                "GunPlacement" => AttributeName.GunPlacement,
                "GunVerticalPivotPlacement" => AttributeName.GunVerticalPivotPlacement,
                "PlayEffect" => AttributeName.PlayEffect,
                "TorpedoHomingPoint" => AttributeName.TorpedoHomingPoint,
                "ToweePoint" => AttributeName.ToweePoint,
                "TowerPoint" => AttributeName.TowerPoint,
                "WakePlacement" => AttributeName.WakePlacement,
                _ => AttributeName.GunPlacement,
            };
        }

        /// <summary>
        /// Remove an element with the specified name from the elements list.
        /// </summary>
        /// <param name="name">The name of the element we want to remove.</param>
        public void RemoveElementName(string name)
        {
            for (int i = 0; i < _elementsName.Count; i++)
            {
                if (_elementsName[i].ToString().Equals(name))
                {
                    _elementsName.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
        /// Add a node with the specified name to the node list and its name to the elements list.
        /// </summary>
        /// <param name="nodeName">The name of the node.</param>
        public void AddNode(string nodeName)
        {
            StringBuilder name = new StringBuilder(nodeName);
            _elementsName.Insert(1 + _nodes.Count, name);
            _nodes.Add(new Element(_elementsName, name));
        }

        /// <summary>
        /// Add a mesh with the specified name to the mesh list and its name to the elements list.
        /// </summary>
        /// <param name="meshName">The name of the node.</param>
        public void AddMesh(string meshName)
        {
            StringBuilder name = new StringBuilder(meshName);
            _elementsName.Insert(1 + _nodes.Count + _meshes.Count, name);
            _meshes.Add(new Element(_elementsName, name));
        }

        /// <summary>
        /// Add a bone with the specified name to the bone list and its name to the elements list.
        /// </summary>
        /// <param name="boneName">The name of the node.</param>
        public void AddBone(string boneName)
        {
            StringBuilder name = new StringBuilder(boneName);
            _elementsName.Insert(1 + _nodes.Count + _meshes.Count + _bones.Count, name);
            _bones.Add(new Bone(_elementsName, name));
        }

        /// <summary>
        /// Add an animation with the specified name to the animation list and its name to the elements list.
        /// </summary>
        /// <param name="animationName">The name of the node.</param>
        public void AddAnimation(string animationName)
        {
            StringBuilder name = new StringBuilder(animationName);
            _elementsName.Insert(1 + _nodes.Count + _meshes.Count + _bones.Count + _animations.Count, name);
            _animations.Add(new Animation(_elementsName, name));
        }
    }
}
