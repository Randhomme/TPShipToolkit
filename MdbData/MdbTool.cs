using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using TPShipToolkit.Structs;
using TPShipToolkit.Utils;

namespace TPShipToolkit.MdbData
{
    internal class MdbTool
    {
        private uint gVCount = 1;
        private uint gVtCount = 1;
        private uint gVnCount = 1;
        private uint boxNumber = 0;
        //material list for the obj file
        private List<Material> finalMat = new List<Material>();
        //material list of the current mdb we are reading
        private List<Material> currentMat = new List<Material>();
        //v for victory and vertexes
        private List<Vector3> v = new List<Vector3>();
        //vertex textures
        private List<Vector2> vt = new List<Vector2>();
        //vertex normals
        private List<Vector2> vn = new List<Vector2>();
        //groups, with group of material containing the triangles
        private List<(string groupName, uint vCount, List<(int matIndex, List<MdbTriangle> tris)> matGroups)> groups = new();
        //collision boxes for each mdb
        private CollisionBox parentBox = new CollisionBox();

        /// <summary>
        /// Converts X mdb file to 1 obj file.
        /// </summary>
        /// <param name="mdbs">The mdb file(s) path.</param>
        /// <param name="objPath">The obj file path.</param>
        /// <param name="progress">Progress on the progress bar.</param>
        /// <param name="logs">Logs in the text box.</param>
        public void XMdbTo1Obj(string[] mdbs, string objPath, bool exportCboxes, IProgress<int> progress, IProgress<string> logs)
        {
            try
            {
                var watch = new System.Diagnostics.Stopwatch();
                using (StreamWriter objWriter = new StreamWriter(File.Open(objPath, FileMode.Create, FileAccess.ReadWrite)))
                {
                    string mtlPath = Path.ChangeExtension(objPath, "mtl");
                    try
                    {
                        objWriter.WriteLine("mtllib " + Path.GetFileName(mtlPath));
                    }
                    catch
                    {
                        logs.Report("Unable to write mtl name.");
                    }
                    using (StreamWriter mtlWriter = new StreamWriter(File.Open(mtlPath, FileMode.Create, FileAccess.ReadWrite)))
                    {
                        string configPath = Path.ChangeExtension(objPath, "txt");
                        if(exportCboxes)
                        {
                            using (StreamWriter configWriter = new StreamWriter(File.Open(configPath, FileMode.Create, FileAccess.ReadWrite)))
                            {
                                for (int i = 0; i < mdbs.Length; i++)
                                {
                                    string mdb, groupName;
                                    try
                                    {
                                        mdb = mdbs[i];
                                        groupName = Path.GetFileNameWithoutExtension(mdb);
                                        using (BinaryReader mdbReader = new BinaryReader(File.OpenRead(mdb)))
                                        {
                                            logs.Report("Reading " + mdb + " ... ");
                                            watch.Start();
                                            ReadMdb(mdbReader, groupName);
                                            watch.Stop();
                                            logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                            logs.Report("Writing obj ... ");
                                            watch.Restart();
                                            WriteObj(objWriter, configWriter, groupName);
                                            watch.Stop();
                                            logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                        }
                                        progress.Report(i + 1);
                                    }
                                    catch (Exception ex)
                                    {
                                        logs.Report(ex.Message);
                                        progress.Report(i + 1);
                                        continue;
                                    }
                                }
                                try
                                {
                                    logs.Report("Writing mtl ... ");
                                    watch.Restart();
                                    WriteMtl(mtlWriter);
                                    watch.Stop();
                                    logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                }
                                catch (Exception ex)
                                {
                                    logs.Report(ex.Message);
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < mdbs.Length; i++)
                            {
                                string mdb, groupName;
                                try
                                {
                                    mdb = mdbs[i];
                                    groupName = Path.GetFileNameWithoutExtension(mdb);
                                    using (BinaryReader mdbReader = new BinaryReader(File.OpenRead(mdb)))
                                    {
                                        logs.Report("Reading " + mdb + " ... ");
                                        watch.Start();
                                        ReadMdb(mdbReader, groupName);
                                        watch.Stop();
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                        logs.Report("Writing obj ... ");
                                        watch.Restart();
                                        WriteObj(objWriter, null, groupName);
                                        watch.Stop();
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                    }
                                    progress.Report(i + 1);
                                }
                                catch (Exception ex)
                                {
                                    logs.Report(ex.Message);
                                    progress.Report(i + 1);
                                    continue;
                                }
                            }
                            try
                            {
                                logs.Report("Writing mtl ... ");
                                watch.Restart();
                                WriteMtl(mtlWriter);
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                            }
                            catch (Exception ex)
                            {
                                logs.Report(ex.Message);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logs.Report(ex.Message);
            }
        }

        /// <summary>
        /// Converts X mdb file to X obj file (1 obj for each mdb).
        /// </summary>
        /// <param name="mdbs">The mdb file(s) path.</param>
        /// <param name="objFolderPath">The folder path to export the obj file(s).</param>
        /// <param name="progress">Progress on the progress bar.</param>
        /// <param name="logs">Logs in the text box.</param>
        public void XMdbToXObj(string[] mdbs, string objFolderPath, bool exportCboxes, IProgress<int> progress, IProgress<string> logs)
        {
            try
            {
                var watch = new System.Diagnostics.Stopwatch();
                if(exportCboxes)
                {
                    for (int i = 0; i < mdbs.Length; i++)
                    {
                        try
                        {
                            var mdb = mdbs[i];
                            var groupName = Path.GetFileNameWithoutExtension(mdb);
                            using (BinaryReader mdbReader = new BinaryReader(File.OpenRead(mdb)))
                            {
                                logs.Report("Reading " + mdb + " ... ");
                                watch.Start();
                                ReadMdb(mdbReader, groupName);
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                            }
                            var objPath = Path.Combine(objFolderPath, Path.GetFileName(Path.ChangeExtension(mdb, "obj")));
                            var mtlPath = Path.ChangeExtension(objPath, "mtl");
                            var configPath = Path.ChangeExtension(objPath, "txt");
                            using (StreamWriter objWriter = new StreamWriter(File.Open(objPath, FileMode.Create, FileAccess.ReadWrite)))
                            {
                                using (StreamWriter mtlWriter = new StreamWriter(File.Open(mtlPath, FileMode.Create, FileAccess.ReadWrite)))
                                {
                                    using (StreamWriter configWriter = new StreamWriter(File.Open(configPath, FileMode.Create, FileAccess.ReadWrite)))
                                    {
                                        logs.Report("Writing obj ... ");
                                        watch.Restart();
                                        try
                                        {
                                            objWriter.WriteLine("mtllib " + Path.GetFileName(mtlPath));
                                        }
                                        catch
                                        {
                                            throw new Exception("Unable to write mtl name.");
                                        }
                                        WriteObj(objWriter, configWriter, groupName);
                                        watch.Stop();
                                        gVCount = gVtCount = gVnCount = 1;
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                        logs.Report("Writing mtl ... ");
                                        watch.Restart();
                                        WriteMtl(mtlWriter);
                                        watch.Stop();
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                    }
                                }
                            }
                            progress.Report(i + 1);
                        }
                        catch(Exception ex)
                        {
                            logs.Report(ex.Message);
                            progress.Report(i + 1);
                            continue;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < mdbs.Length; i++)
                    {
                        try
                        {
                            var mdb = mdbs[i];
                            var groupName = Path.GetFileNameWithoutExtension(mdb);
                            using (BinaryReader mdbReader = new BinaryReader(File.OpenRead(mdb)))
                            {
                                logs.Report("Reading " + mdb + " ... ");
                                watch.Start();
                                ReadMdb(mdbReader, groupName);
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                progress.Report(i + 1);
                            }
                            var objPath = Path.Combine(objFolderPath, Path.GetFileName(Path.ChangeExtension(mdb, "obj")));
                            var mtlPath = Path.ChangeExtension(objPath, "mtl");
                            var configPath = Path.ChangeExtension(objPath, "txt");
                            using (StreamWriter objWriter = new StreamWriter(File.Open(objPath, FileMode.Create, FileAccess.ReadWrite)))
                            {
                                using (StreamWriter mtlWriter = new StreamWriter(File.Open(mtlPath, FileMode.Create, FileAccess.ReadWrite)))
                                {
                                    logs.Report("Writing obj ... ");
                                    watch.Restart();
                                    try
                                    {
                                        objWriter.WriteLine("mtllib " + Path.GetFileName(mtlPath));
                                    }
                                    catch
                                    {
                                        throw new Exception("Unable to write mtl name.");
                                    }
                                    WriteObj(objWriter, null, groupName);
                                    watch.Stop();
                                    gVCount = gVtCount = gVnCount = 1;
                                    logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                    logs.Report("Writing mtl ... ");
                                    watch.Restart();
                                    WriteMtl(mtlWriter);
                                    watch.Stop();
                                    logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                }
                            }
                            progress.Report(i + 1);
                        }
                        catch (Exception ex)
                        {
                            logs.Report(ex.Message);
                            progress.Report(i + 1);
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logs.Report(ex.Message);
            }
        }

        /// <summary>
        /// Read an mdb file and get all the needed data from it.
        /// </summary>
        /// <param name="mdbReader">The mdb file.</param>
        /// <param name="groupName">The base group name to name the groups/object of the mdb.</param>
        /// <exception cref="Exception"></exception>
        private void ReadMdb(BinaryReader mdbReader, string groupName)
        {
            uint modelCount, modelLength, modelStart, matCount, vCount, tCount, bCount;
            //File length stuff
            try
            {
                //skip 12 bytes (file data block length)
                mdbReader.BaseStream.Seek(12, SeekOrigin.Begin);
                //number of model in the mdb (lod)
                modelCount = mdbReader.ReadUInt32();
            }
            catch
            {
                throw new Exception("Skipped\nUnable to read the number of model in the file.\n");
            }

            //3d model
            for (int i = 0; i < modelCount; i++)
            {
                try
                {
                    //model block length
                    modelLength = mdbReader.ReadUInt32();
                    modelStart = (uint)mdbReader.BaseStream.Position;
                    //skip 4 bytes (model index)
                    mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
                    //vertex count
                    vCount = mdbReader.ReadUInt32();
                }
                catch
                {
                    throw new Exception("Skipped\nUnable to read point count of model number " + i + " in the file.\n");
                }
                //Vertexes
                for (uint j = 0; j < vCount; j++)
                {
                    try
                    {
                        //skip 4 bytes (vertex block length)
                        mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
                        //v, vt, vn
                        v.Add(new Vector3(mdbReader.ReadSingle(), mdbReader.ReadSingle(), mdbReader.ReadSingle()));
                        vt.Add(new Vector2(mdbReader.ReadSingle(), mdbReader.ReadSingle()));
                        vn.Add(new Vector2(mdbReader.ReadSingle(), mdbReader.ReadSingle()));        
                        //vn.Add(((float, float, float))(-Math.Sin(vnx), Math.Sin(vny), -Math.Cos(vnx)));
                        //skip 4 bytes (transparency) (FF FF FF FF)
                        mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
                    }
                    catch
                    {
                        throw new Exception("Skipped\nUnable to read point number " + j + " of model number " + i + " in the file.\n");
                    }
                }
                try
                {
                    //tris count
                    tCount = mdbReader.ReadUInt32();
                }
                catch
                {
                    throw new Exception("Skipped\nUnable to read triangle count of model number " + i + " in the file.\n");
                }
                //Triangles
                int currentMat = -1;
                var group = (groupName + "_" + i, vCount, new List<(int, List<MdbTriangle>)>());
                var triList = new List<MdbTriangle>();
                for (uint j = 0; j < tCount; j++)
                {
                    try
                    {
                        //skip 4 bytes (tri block length)
                        mdbReader.BaseStream.Seek(4, SeekOrigin.Current);

                        var tri = new MdbTriangle() { P0 = mdbReader.ReadUInt16(), P1 = mdbReader.ReadUInt16(), P2 = mdbReader.ReadUInt16() };
                        var mat = mdbReader.ReadUInt16();
                        if(currentMat!=mat)
                        {
                            triList = new List<MdbTriangle>();
                            group.Item3.Add((mat, triList));
                            triList.Add(tri);
                            currentMat = mat;
                        }
                        else
                        {
                            triList.Add(tri);
                        }
                    }
                    catch
                    {
                        throw new Exception("Skipped\nUnable to read triangle number " + j + " of model number " + i + ".\n");
                    }
                }
                groups.Add(group);
                try
                {
                    //skip 4 bytes (potential animation count)
                    mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
                    //check if we reach the end (because potential animation block)
                    var length = mdbReader.BaseStream.Position - modelStart;
                    if (length < modelLength)
                        mdbReader.BaseStream.Seek(modelLength - length, SeekOrigin.Current);
                }
                catch
                {
                    throw new Exception("Skipped\nUnable to reach the end of model " + i + " in the file.\n");
                }
            }

            //Material
            try
            {
                //material count
                matCount = mdbReader.ReadUInt32();
            }
            catch
            {
                throw new Exception("Skipped\nUnable to read texture count in the file.\n");
            }
            var separator = new char[] { ' ', ';', ',', '+', '\r', '\t', '\n' };
            for (uint i = 0; i < matCount; i++)
            {
                try
                {
                    //skip 4 bytes (block length)
                    mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
                    //texture name
                    int strlength = mdbReader.ReadInt32(); //can't use uint because ReadChars uses int and it's not cool
                    //checking negative case
                    if (strlength < 0)
                        strlength = -strlength - 1; //let's just use the opposite for simplicity
                    //creating material
                    var mat = new Material();
                    mat.TexName = new string(mdbReader.ReadChars(strlength));
                    //generate material name from texture name
                    mat.MatName = Path.GetFileNameWithoutExtension
                        (string.Join("_", mat.TexName.Split(separator)));
                    //add to current mat
                    currentMat.Add(mat);
                    //add to final mat without duplicating
                    if (!finalMat.Exists(m => m.MatName.Equals(mat.MatName, StringComparison.OrdinalIgnoreCase)))
                        finalMat.Add(mat);
                    //skip 72 bytes (material data)
                    mdbReader.BaseStream.Seek(72, SeekOrigin.Current);
                }
                catch
                {
                    throw new Exception("Skipped\nUnable to read material " + i + ".\n");
                }
            }
            //Bones ?
            try
            {
                //bones count
                bCount = mdbReader.ReadUInt32();
            }
            catch
            {
                throw new Exception("Skipped\nUnable to read bones count in the file.\n");
            }
            //we basically skip because I have no idea what this is
            for(int i=0;i<bCount;i++)
            {
                try
                {
                    var bLength = mdbReader.ReadUInt32();
                    mdbReader.BaseStream.Seek(bLength, SeekOrigin.Current); //skip to next block
                }
                catch
                {
                    throw new Exception("Skipped\nUnable to read bone " + i + ".\n");
                }
            }
            //skip some object bounding data (used for banner placement for exemple)
            try
            {
                mdbReader.BaseStream.Seek(52, SeekOrigin.Current);
            }
            catch
            {
                throw new Exception("Unable to reach collision box block in the file.\n");
            }
            //Collision boxes
            try
            {
                ReadBox(mdbReader, parentBox);
            }
            catch
            {
                throw new Exception("Unable to read collision box.\n");
            }
        }

        /// <summary>
        /// Read the collision box and the childs (box inside the box basically).
        /// </summary>
        /// <param name="mdbReader">The mdb from where we read the box.</param>
        /// <param name="box">Box box, box box</param>
        private void ReadBox(BinaryReader mdbReader, CollisionBox box)
        {
            //Give this box a cool name
            box.BoxName = "_BOX" + boxNumber;
            boxNumber++;
            //skip 12 bytes (data bloc length)
            mdbReader.BaseStream.Seek(12, SeekOrigin.Current);
            //Position
            box.Position.X = mdbReader.ReadSingle();
            box.Position.Z = -mdbReader.ReadSingle();
            box.Position.Y = mdbReader.ReadSingle();
            mdbReader.BaseStream.Seek(12, SeekOrigin.Current);
            //Orientation cross
            box.OCross.X = mdbReader.ReadSingle();
            box.OCross.Z = -mdbReader.ReadSingle();
            box.OCross.Y = mdbReader.ReadSingle();
            mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
            //Orientation forward (becomes OUp because z = -y)
            box.OUp.X = mdbReader.ReadSingle();
            box.OUp.Z = -mdbReader.ReadSingle();
            box.OUp.Y = mdbReader.ReadSingle();
            mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
            //Orientation up (becomes OForward because y = z)
            box.OForward.X = mdbReader.ReadSingle();
            box.OForward.Z = -mdbReader.ReadSingle();
            box.OForward.Y = mdbReader.ReadSingle();
            mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
            //Length
            box.Length.X = mdbReader.ReadSingle();
            box.Length.Y = mdbReader.ReadSingle();
            box.Length.Z = mdbReader.ReadSingle();
            mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
            //Radius + level
            mdbReader.BaseStream.Seek(16, SeekOrigin.Current);
            //HasLeftChild
            var hasLeftChild = mdbReader.ReadBoolean();
            mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
            //HasRightChild
            var hasRightChild = mdbReader.ReadBoolean();
            //LeftChild
            if (hasLeftChild)
            {
                mdbReader.BaseStream.Seek(8, SeekOrigin.Current);
                box.Leftchild = new CollisionBox();
                ReadBox(mdbReader, box.Leftchild);
            }
            //RightChild
            if (hasRightChild)
            {
                mdbReader.BaseStream.Seek(8, SeekOrigin.Current);
                box.Rightchild = new CollisionBox();
                ReadBox(mdbReader, box.Rightchild);
            }
            //skip triangles
            mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
            var triCount = mdbReader.ReadUInt32();
            mdbReader.BaseStream.Seek(triCount * 8, SeekOrigin.Current);
        }

        /// <summary>
        /// Export the data to an obj file.
        /// </summary>
        /// <param name="objWriter">The obj file.</param>
        /// <param name="configWriter">The txt file we are writing collision boxes informations in.</param>
        /// <param name="groupName">The mtl file name.</param>
        /// <exception cref="Exception"></exception>
        private void WriteObj(StreamWriter objWriter, StreamWriter configWriter, string groupName)
        {
            try
            {
                foreach (var p in v)
                {
                    objWriter.WriteLine("v " + p.X + " " + p.Z + " " + -p.Y);
                }
                foreach (var p in vt)
                {
                    objWriter.WriteLine("vt " + p.X + " " + (-p.Y));
                }
                foreach (var p in vn)
                {
                    objWriter.WriteLine("vn " + -Math.Sin(p.X) + " " + Math.Sin(p.Y) + " " + -Math.Cos(p.X));
                }
            }
            catch
            {
                ClearLists();
                throw new Exception("Unable to write points.\n");
            }
            
            foreach(var group in groups)
            {
                string matName;
                try
                {
                    objWriter.WriteLine("g " + group.groupName);
                    objWriter.WriteLine("o " + group.groupName);
                }
                catch
                {
                    ClearLists();
                    throw new Exception("Unable to write group " + group.groupName + "\n");
                }

                foreach(var matGroup in group.matGroups)
                {
                    try
                    {
                        matName = currentMat[matGroup.matIndex].MatName;
                        objWriter.WriteLine("usemtl " + matName);
                    }
                    catch
                    {
                        ClearLists();
                        throw new Exception("Material index in " + group.groupName + " out of range.\n");
                    }
                    for (int j = 0; j < matGroup.tris.Count; j++)
                    {
                        var tri = matGroup.tris[j]; //this won't be out of range
                        try
                        {
                            objWriter.WriteLine
                                    ("f " + (tri.P2 + gVCount) + "/" + (tri.P2 + gVtCount) + "/" + (tri.P2 + gVnCount)
                                    + " " + (tri.P1 + gVCount) + "/" + (tri.P1 + gVtCount) + "/" + (tri.P1 + gVnCount)
                                    + " " + (tri.P0 + gVCount) + "/" + (tri.P0 + gVtCount) + "/" + (tri.P0 + gVnCount));
                        }
                        catch
                        {
                            ClearLists();
                            throw new Exception("Unable to write triangle " + j + " of " + group.groupName + ".\n");
                        }
                    }
                }
                gVCount += group.vCount;
                gVtCount += group.vCount;
                gVnCount += group.vCount;
            }
            try
            {
                if(configWriter!=null) //it is null only if we don't want to export the cboxes
                {
                    configWriter.WriteLine("MESH\t" + groupName);
                    WriteBox(objWriter, configWriter, parentBox);
                }
            }
            catch
            {
                ClearLists();
                throw new Exception("Unable to write collision box of the file.\n");
            }
            ClearLists();
        }

        /// <summary>
        /// Write the boxes in the obj file.
        /// </summary>
        /// <param name="objWriter">The obj file we are writing the boxes in.</param>
        /// <param name="configWriter">The txt file used to get the box back when doing obj to mdb.</param>
        /// <param name="box">Box box, box box</param>
        private void WriteBox(StreamWriter objWriter, StreamWriter configWriter, CollisionBox box)
        {
            //box points
            Vector3 a, b, c, d, e, f, g, h;
            a = b = c = d = e = f = g = h = box.Position;
            float X, Y, Z;
            //Orientation cross
            X = box.OCross.X * box.Length.X;
            a.X += X; b.X += X; c.X += X; d.X += X;
            e.X -= X; f.X -= X; g.X -= X; h.X -= X;
            Y = box.OCross.Y * box.Length.X;
            a.Y += Y; b.Y += Y; c.Y += Y; d.Y += Y;
            e.Y -= Y; f.Y -= Y; g.Y -= Y; h.Y -= Y;
            Z = box.OCross.Z * box.Length.X;
            a.Z += Z; b.Z += Z; c.Z += Z; d.Z += Z;
            e.Z -= Z; f.Z -= Z; g.Z -= Z; h.Z -= Z;
            //Orientation up
            X = box.OUp.X * box.Length.Y;
            a.X += X; c.X += X; f.X += X; g.X += X;
            b.X -= X; d.X -= X; e.X -= X; h.X -= X;
            Y = box.OUp.Y * box.Length.Y;
            a.Y += Y; c.Y += Y; f.Y += Y; g.Y += Y;
            b.Y -= Y; d.Y -= Y; e.Y -= Y; h.Y -= Y;
            Z = box.OUp.Z * box.Length.Y;
            a.Z += Z; c.Z += Z; f.Z += Z; g.Z += Z;
            b.Z -= Z; d.Z -= Z; e.Z -= Z; h.Z -= Z;
            //Orientation forward
            X = box.OForward.X * box.Length.Z;
            a.X += X; b.X += X; g.X += X; h.X += X;
            c.X -= X; d.X -= X; e.X -= X; f.X -= X;
            Y = box.OForward.Y * box.Length.Z;
            a.Y += Y; b.Y += Y; g.Y += Y; h.Y += Y;
            c.Y -= Y; d.Y -= Y; e.Y -= Y; f.Y -= Y;
            Z = box.OForward.Z * box.Length.Z;
            a.Z += Z; b.Z += Z; g.Z += Z; h.Z += Z;
            c.Z -= Z; d.Z -= Z; e.Z -= Z; f.Z -= Z;

            //write box points
            objWriter.WriteLine("v " + a.X + " " + a.Y + " " + a.Z);
            objWriter.WriteLine("v " + b.X + " " + b.Y + " " + b.Z);
            objWriter.WriteLine("v " + c.X + " " + c.Y + " " + c.Z);
            objWriter.WriteLine("v " + d.X + " " + d.Y + " " + d.Z);
            objWriter.WriteLine("v " + e.X + " " + e.Y + " " + e.Z);
            objWriter.WriteLine("v " + f.X + " " + f.Y + " " + f.Z);
            objWriter.WriteLine("v " + g.X + " " + g.Y + " " + g.Z);
            objWriter.WriteLine("v " + h.X + " " + h.Y + " " + h.Z);

            //write box tris
            objWriter.WriteLine("o " + box.BoxName);
            objWriter.WriteLine("g " + box.BoxName);
            objWriter.WriteLine("f " + (gVCount + 0) + " " + (gVCount + 1) + " " + (gVCount + 2));
            objWriter.WriteLine("f " + (gVCount + 3) + " " + (gVCount + 4) + " " + (gVCount + 2));
            objWriter.WriteLine("f " + (gVCount + 5) + " " + (gVCount + 6) + " " + (gVCount + 2));
            objWriter.WriteLine("f " + (gVCount + 4) + " " + (gVCount + 7) + " " + (gVCount + 5));
            objWriter.WriteLine("f " + (gVCount + 6) + " " + (gVCount + 7) + " " + (gVCount + 0));
            objWriter.WriteLine("f " + (gVCount + 1) + " " + (gVCount + 7) + " " + (gVCount + 3));
            objWriter.WriteLine("f " + (gVCount + 6) + " " + (gVCount + 0) + " " + (gVCount + 2));
            objWriter.WriteLine("f " + (gVCount + 7) + " " + (gVCount + 1) + " " + (gVCount + 0));
            objWriter.WriteLine("f " + (gVCount + 1) + " " + (gVCount + 3) + " " + (gVCount + 2));
            objWriter.WriteLine("f " + (gVCount + 4) + " " + (gVCount + 5) + " " + (gVCount + 2));
            objWriter.WriteLine("f " + (gVCount + 7) + " " + (gVCount + 6) + " " + (gVCount + 5));
            objWriter.WriteLine("f " + (gVCount + 7) + " " + (gVCount + 4) + " " + (gVCount + 3));

            gVCount += 8;

            //write childs
            if (box.Leftchild != null)
            {
                if (box.Rightchild != null)
                {
                    configWriter.WriteLine("CBOX\t" + box.BoxName + "\t" + box.Leftchild.BoxName + "\t" + box.Rightchild.BoxName);
                    WriteBox(objWriter, configWriter, box.Leftchild);
                    WriteBox(objWriter, configWriter, box.Rightchild);
                }
                else
                {
                    configWriter.WriteLine("CBOX\t" + box.BoxName + "\t" + box.Leftchild.BoxName);
                    WriteBox(objWriter, configWriter, box.Leftchild);
                }
            }
            else
            {
                if (box.Rightchild != null)
                {
                    configWriter.WriteLine("CBOX\t" + box.BoxName + "\t\t" + box.Rightchild.BoxName);
                    WriteBox(objWriter, configWriter, box.Rightchild);
                }
                else
                {
                    configWriter.WriteLine("CBOX\t" + box.BoxName);
                }
            }
        }

        /// <summary>
        /// Write the mtl file using the specified writer.
        /// </summary>
        /// <param name="writer">The mtl file.</param>
        /// <exception cref="Exception"></exception>
        private void WriteMtl(StreamWriter writer)
        {
            try
            {
                foreach (var mat in finalMat)
                {
                    writer.WriteLine("newmtl " + mat.MatName);
                    writer.WriteLine("Ka 0.200000 0.200000 0.200000");
                    writer.WriteLine("Kd 1.000000 1.000000 1.000000");
                    writer.WriteLine("Ks 0.000000 0.000000 0.000000");
                    writer.WriteLine("illum 2");
                    writer.WriteLine("Ns 8.000000");
                    writer.WriteLine("map_Kd " + Form1.settings.TextureDirectory + Path.ChangeExtension(mat.TexName, "dds"));
                    writer.WriteLine();
                }
            }
            catch
            {
                throw new Exception("Unable to write mtl file.\n");
            }
        }

        private void ClearLists()
        {
            currentMat.Clear();
            v.Clear();
            vt.Clear();
            vn.Clear();
            groups.Clear();
            parentBox = new CollisionBox();
        }
    }
}
