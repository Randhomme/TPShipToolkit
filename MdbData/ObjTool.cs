using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using TPShipToolkit.Structs;
using TPShipToolkit.Utils;

namespace TPShipToolkit.MdbData
{
    internal class ObjTool
    {
        //mtl file name from the obj file
        private string mtlName = string.Empty;
        //material list from the mtl file
        private List<Material> finalMat = new List<Material>();
        //v for victory and vertexes
        private List<Vector3> v = new List<Vector3>();
        //vertex textures
        private List<Vector2> vt = new List<Vector2>();
        //vertex normals
        private List<Vector3> vn = new List<Vector3>();
        //groups, with group of material containing the triangles
        private List<(string groupName, List<(string matName, List<ObjTriangle> tris)> matGroups)> groups = new();
        //groups with the collision boxes for each mesh
        private List<(string meshName, CollisionBox parentBox)> cboxGroups = new();

        /// <summary>
        /// Converts 1 obj file to X mdb file (by using the groups/objects name).
        /// </summary>
        /// <param name="objs">The obj file(s) to export the mdb files from.</param>
        /// <param name="mdbFolderPath">The folder path to export the mdb files.</param>
        /// <param name="autoCbox">Indicates if collision boxes are automatically generated.</param>
        /// <param name="progress">Progress on the progress bar.</param>
        /// <param name="logs">Logs in the text box.</param>
        public void ObjToXMdb(string[] objs, string mdbFolderPath, bool autoCbox, IProgress<int> progress, IProgress<string> logs)
        {
            try
            {
                for (int i = 0; i < objs.Length; i++)
                {
                    var watch = new System.Diagnostics.Stopwatch();
                    var objPath = objs[i];
                    try
                    {
                        using (StreamReader objReader = new StreamReader(File.OpenRead(objPath)))
                        {
                            logs.Report("Reading " + objPath + " ... ");
                            watch.Start();
                            ReadObj(objReader);
                            watch.Stop();
                            logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                        }
                    }
                    catch
                    {
                        logs.Report("Skipped\nUnable to read obj file.\n");
                        progress.Report(i + 1);
                        continue;
                    }
                    var mtlPath = string.IsNullOrWhiteSpace(mtlName) ? Path.ChangeExtension(objPath, "mtl") : Path.Combine(Path.GetDirectoryName(objPath), mtlName);
                    try
                    {
                        using (StreamReader mtlReader = new StreamReader(File.OpenRead(mtlPath)))
                        {
                            logs.Report("Reading mtl file ... ");
                            watch.Restart();
                            ReadMtl(mtlReader);
                            watch.Stop();
                            logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                        }
                    }
                    catch
                    {
                        logs.Report("Skipped\nUnable to read mtl file.\n");
                        progress.Report(i + 1);
                        continue;
                    }
                    groups.Sort((x, y) => NaturalStringComparer.CompareNatural(x.groupName, y.groupName));
                    if(!autoCbox)
                    {
                        var cboxPath = Path.ChangeExtension(objPath, "txt");
                        try
                        {
                            using (StreamReader cboxReader = new StreamReader(File.OpenRead(cboxPath)))
                            {
                                logs.Report("Reading collision box file ...\n");
                                watch.Restart();
                                ReadCbox(cboxReader);
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                logs.Report("Reading collision boxes values ...\n");
                                watch.Restart();
                                foreach (var cboxGroup in cboxGroups)
                                {
                                    ReadCboxValues(cboxGroup.parentBox, logs);
                                }
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                            }
                        }
                        catch
                        {
                            logs.Report("Unable to read collision box file.\n");
                        }
                    }
                    string currentFileName = string.Empty;
                    BinaryWriter mdbWriter = null;
                    List<(string matName, List<ObjTriangle> tris)> hitBox = null;
                    int modelIndex = 0, tCount = 0;
                    float minX, minY, minZ, maxX, maxY, maxZ;
                    minX = minY = minZ = maxX = maxY = maxZ = 0;
                    try
                    {
                        foreach (var group in groups)
                        {
                            var points = new List<int[]>();
                            var triangles = new List<(ushort matIndex, List<ushort[]> pointsIndex)>();
                            string temp = RealGroupName(group.groupName);
                            if (string.IsNullOrWhiteSpace(temp))
                                temp = "-"; //a default name
                            if (!currentFileName.Equals(temp))
                            {
                                if (mdbWriter != null) //if we previously wrote a file
                                {
                                    try
                                    {
                                        logs.Report("Writing materials texture ... ");
                                        watch.Restart();
                                        WriteMaterials(mdbWriter);
                                        watch.Stop();
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                        logs.Report("Writing bounding values ... ");
                                        watch.Restart();
                                        WriteBoundingValues(mdbWriter, minX, minY, minZ, maxX, maxY, maxZ);
                                        watch.Stop();
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                        var pos = mdbWriter.BaseStream.Position;
                                        mdbWriter.Write(0);
                                        mdbWriter.Write(1);
                                        if (autoCbox)
                                        {
                                            logs.Report("Creating collision boxes ... ");
                                            watch.Restart();
                                            var box = new CollisionBox();
                                            AutoGenerateCBox(box, hitBox);
                                            watch.Stop();
                                            logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                            logs.Report("Writing collision boxes ... ");
                                            watch.Restart();
                                            WriteCollisionBox(mdbWriter, box, tCount);
                                            watch.Stop();
                                            logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                        }
                                        else
                                        {
                                            CollisionBox box = cboxGroups.Find((g) => g.meshName.Equals(currentFileName)).parentBox;
                                            if (box != null)
                                            {
                                                logs.Report("Writing collision boxes ... ");
                                                watch.Restart();
                                                WriteCollisionBox(mdbWriter, box, tCount);
                                                watch.Stop();
                                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                            }
                                            else
                                            {
                                                logs.Report("No collision boxes found for " + currentFileName + ".\n");
                                                logs.Report("Creating new collision boxes ... ");
                                                watch.Restart();
                                                box = new CollisionBox();
                                                AutoGenerateCBox(box, hitBox);
                                                watch.Stop();
                                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                                logs.Report("Writing collision boxes ... ");
                                                watch.Restart();
                                                WriteCollisionBox(mdbWriter, box, tCount);
                                                watch.Stop();
                                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                            }
                                        }
                                        mdbWriter.Write(17); //max level
                                        mdbWriter.Write(5);
                                        logs.Report("Writing hitbox ... ");
                                        watch.Restart();
                                        WriteHitbox(mdbWriter, hitBox, tCount); //hitBox won't be null since we have at least one group here
                                        watch.Stop();
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                        var currentPos = mdbWriter.BaseStream.Position;
                                        var blockLength0 = (int)(currentPos - pos);
                                        logs.Report("Writing file length and strings ... ");
                                        watch.Restart();
                                        mdbWriter.Write(false);
                                        mdbWriter.BaseStream.Seek(0, SeekOrigin.Begin);
                                        mdbWriter.Write(currentPos + 1);
                                        mdbWriter.Write((int)currentPos - 11);
                                        mdbWriter.Write(modelIndex);
                                        mdbWriter.BaseStream.Seek(pos, SeekOrigin.Begin);
                                        mdbWriter.Write(blockLength0);
                                        mdbWriter.BaseStream.Seek(0, SeekOrigin.End);
                                        WriteStrings(mdbWriter);
                                        watch.Stop();
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                        mdbWriter.Dispose(); //free the file we just wrote
                                        modelIndex = 0; //new file means we start at 0 again
                                    }
                                    catch
                                    {
                                        logs.Report("Unable to write file " + currentFileName + ".mdb.\n");
                                        throw;
                                    }
                                }
                                currentFileName = temp;
                                mdbWriter = new BinaryWriter(File.Open(Path.Combine(mdbFolderPath, currentFileName + ".mdb"), FileMode.Create));
                                logs.Report("\n---- " + currentFileName + ".mdb ----\n");
                                for (short j = 0; j < 4; j++) mdbWriter.Write(0);
                            }
                            if (modelIndex == 0)
                            {
                                hitBox = group.matGroups;
                            }
                            try
                            {
                                logs.Report("Processing " + group.groupName + " ... ");
                                watch.Restart();
                                ProcessGroup(group.matGroups, points, triangles);
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                logs.Report("Writing " + group.groupName + " ... ");
                                watch.Restart();
                                WriteGroup(mdbWriter, points, triangles, modelIndex, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ, ref tCount);
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                modelIndex++;
                            }
                            catch (Exception ex)
                            {
                                logs.Report(ex.Message + "\nUnable to write " + group.groupName + " in " + currentFileName + ".mdb.\n");
                                throw;
                            }
                        }
                        if (mdbWriter != null) //if we don't have any group, the writer will be null
                        {
                            try
                            {
                                //write materials, collision box and hitbox of last file
                                logs.Report("Writing materials texture ... ");
                                watch.Restart();
                                WriteMaterials(mdbWriter);
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                logs.Report("Writing bounding values ... ");
                                watch.Restart();
                                WriteBoundingValues(mdbWriter, minX, minY, minZ, maxX, maxY, maxZ);
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                var pos = mdbWriter.BaseStream.Position;
                                mdbWriter.Write(0);
                                mdbWriter.Write(1);
                                if (autoCbox)
                                {
                                    logs.Report("Creating collision boxes ... ");
                                    watch.Restart();
                                    var box = new CollisionBox();
                                    AutoGenerateCBox(box, hitBox);
                                    watch.Stop();
                                    logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                    logs.Report("Writing collision boxes ... ");
                                    watch.Restart();
                                    WriteCollisionBox(mdbWriter, box, tCount);
                                    watch.Stop();
                                    logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                }
                                else
                                {
                                    CollisionBox box = cboxGroups.Find((g) => g.meshName.Equals(currentFileName)).parentBox;
                                    if (box != null)
                                    {
                                        logs.Report("Writing collision boxes ... ");
                                        watch.Restart();
                                        WriteCollisionBox(mdbWriter, box, tCount);
                                        watch.Stop();
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                    }
                                    else
                                    {
                                        logs.Report("No collision boxes found for " + currentFileName + ".\n");
                                        logs.Report("Creating new collision boxes ... ");
                                        watch.Restart();
                                        box = new CollisionBox();
                                        AutoGenerateCBox(box, hitBox);
                                        watch.Stop();
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                        logs.Report("Writing collision boxes ... ");
                                        watch.Restart();
                                        WriteCollisionBox(mdbWriter, box, tCount);
                                        watch.Stop();
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                    }
                                }
                                mdbWriter.Write(17); //max level
                                mdbWriter.Write(5);
                                logs.Report("Writing hitbox ... ");
                                watch.Restart();
                                WriteHitbox(mdbWriter, hitBox, tCount); //hitBox won't be null since we have at least one group here
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                var currentPos = mdbWriter.BaseStream.Position;
                                var blockLength0 = (int)(currentPos - pos);
                                logs.Report("Writing file length and strings ... ");
                                watch.Restart();
                                mdbWriter.Write(false);
                                mdbWriter.BaseStream.Seek(0, SeekOrigin.Begin);
                                mdbWriter.Write(currentPos + 1);
                                mdbWriter.Write((int)currentPos - 11);
                                mdbWriter.Write(modelIndex);
                                mdbWriter.BaseStream.Seek(pos, SeekOrigin.Begin);
                                mdbWriter.Write(blockLength0);
                                mdbWriter.BaseStream.Seek(0, SeekOrigin.End);
                                WriteStrings(mdbWriter);
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                mdbWriter.Dispose(); //free the last file
                            }
                            catch
                            {
                                logs.Report("Unable to write file " + currentFileName + ".mdb.\n");
                                throw;
                            }
                        }
                        ClearObjStuff();
                        progress.Report(i + 1);
                    }
                    catch
                    {
                        ClearObjStuff();
                        logs.Report("\n");
                        progress.Report(i + 1);
                        continue;
                    }
                    logs.Report("\n");
                }
            }
            catch (Exception ex)
            {
                logs.Report(ex.Message);
            }
        }

        /// <summary>
        /// Converts X obj file to X mdb file (1 mdb for each obj).
        /// </summary>
        /// <param name="objs">The obj file(s) to export the mdb files from.</param>
        /// <param name="mdbFolderPath">The folder path to export the mdb files.</param>
        /// <param name="autoCbox">Indicates if collision boxes are automatically generated.</param>
        /// <param name="progress">Progress on the progress bar.</param>
        /// <param name="logs">Logs in the text box.</param>
        public void XObjToXMdb(string[] objs, string mdbFolderPath, bool autoCbox, IProgress<int> progress, IProgress<string> logs)
        {
            try
            {
                for (int i = 0; i < objs.Length; i++)
                {
                    var objPath = objs[i];
                    var currentFileName = Path.ChangeExtension(Path.GetFileName(objPath), "mdb");
                    logs.Report("---- " + currentFileName + " ----\n");
                    var watch = new System.Diagnostics.Stopwatch();
                    try
                    {
                        using (StreamReader objReader = new StreamReader(File.OpenRead(objPath)))
                        {
                            logs.Report("Reading " + objPath + " file ... ");
                            watch.Start();
                            ReadObj(objReader);
                            watch.Stop();
                            logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                        }
                    }
                    catch
                    {
                        logs.Report("Skipped\nUnable to read obj file.\n");
                        progress.Report(i + 1);
                        continue;
                    }
                    var mtlPath = string.IsNullOrWhiteSpace(mtlName) ? Path.ChangeExtension(objPath, "mtl") : Path.Combine(Path.GetDirectoryName(objPath), mtlName);
                    using (StreamReader mtlReader = new StreamReader(File.OpenRead(mtlPath)))
                    {
                        logs.Report("Reading mtl file ... ");
                        watch.Restart();
                        ReadMtl(mtlReader);
                        watch.Stop();
                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                    }
                    groups.Sort((x, y) => NaturalStringComparer.CompareNatural(x.groupName, y.groupName));
                    if (!autoCbox)
                    {
                        var cboxPath = Path.ChangeExtension(objPath, "txt");
                        try
                        {
                            using (StreamReader cboxReader = new StreamReader(File.OpenRead(cboxPath)))
                            {
                                logs.Report("Reading collision box file ... ");
                                watch.Restart();
                                ReadCbox(cboxReader);
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                logs.Report("Reading collision boxes values ... \n");
                                watch.Restart();
                                foreach (var cboxGroup in cboxGroups)
                                {
                                    ReadCboxValues(cboxGroup.parentBox, logs);
                                }
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                            }
                        }
                        catch
                        {
                            logs.Report("Unable to read collision box file.\n");
                        }
                    }
                    try
                    {
                        using (BinaryWriter mdbWriter = new BinaryWriter(File.Open(Path.Combine(mdbFolderPath, currentFileName), FileMode.Create)))
                        {
                            int modelIndex = 0, tCount = 0;
                            float minX, minY, minZ, maxX, maxY, maxZ;
                            minX = minY = minZ = maxX = maxY = maxZ = 0;
                            List<(string matName, List<ObjTriangle> tris)> hitBox = null;
                            for (short j = 0; j < 4; j++) mdbWriter.Write(0);
                            foreach (var group in groups)
                            {
                                if (modelIndex == 0)
                                {
                                    hitBox = group.matGroups;
                                }
                                try
                                {
                                    var points = new List<int[]>();
                                    var triangles = new List<(ushort matIndex, List<ushort[]> pointsIndex)>();
                                    logs.Report("Processing " + group.groupName + " ... ");
                                    watch.Restart();
                                    ProcessGroup(group.matGroups, points, triangles);
                                    watch.Stop();
                                    logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                    logs.Report("Writing " + group.groupName + " ... ");
                                    watch.Restart();
                                    WriteGroup(mdbWriter, points, triangles, modelIndex, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ, ref tCount);
                                    watch.Stop();
                                    logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                    modelIndex++;
                                }
                                catch (Exception ex)
                                {
                                    throw new Exception(ex.Message + "\nUnable to write " + group.groupName + " in " + currentFileName + ".mdb.");
                                }
                            }
                            try
                            {
                                //write materials, collision box and hitbox of last file
                                logs.Report("Writing materials texture ... ");
                                watch.Restart();
                                WriteMaterials(mdbWriter);
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                logs.Report("Writing bounding values ... ");
                                watch.Restart();
                                WriteBoundingValues(mdbWriter, minX, minY, minZ, maxX, maxY, maxZ);
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                var pos = mdbWriter.BaseStream.Position;
                                mdbWriter.Write(0);
                                mdbWriter.Write(1);
                                if (autoCbox)
                                {
                                    logs.Report("Creating collision boxes ... ");
                                    watch.Restart();
                                    var box = new CollisionBox();
                                    AutoGenerateCBox(box, hitBox);
                                    watch.Stop();
                                    logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                    logs.Report("Writing collision boxes ... ");
                                    watch.Restart();
                                    WriteCollisionBox(mdbWriter, box, tCount);
                                    watch.Stop();
                                    logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                }
                                else
                                {
                                    CollisionBox box = cboxGroups.Find((g) => g.meshName.Equals(currentFileName)).parentBox;
                                    if (box != null)
                                    {
                                        logs.Report("Writing collision boxes ... ");
                                        watch.Restart();
                                        WriteCollisionBox(mdbWriter, box, tCount);
                                        watch.Stop();
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                    }
                                    else
                                    {
                                        logs.Report("No collision boxes found for " + currentFileName + ".\n");
                                        logs.Report("Creating new collision boxes ... ");
                                        watch.Restart();
                                        box = new CollisionBox();
                                        AutoGenerateCBox(box, hitBox);
                                        watch.Stop();
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                        logs.Report("Writing collision boxes ... ");
                                        watch.Restart();
                                        WriteCollisionBox(mdbWriter, box, tCount);
                                        watch.Stop();
                                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                    }
                                }
                                mdbWriter.Write(17); //max level
                                mdbWriter.Write(5);
                                logs.Report("Writing hitbox ... ");
                                watch.Restart();
                                WriteHitbox(mdbWriter, hitBox, tCount); //hitBox won't be null since we have at least one group here
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                var currentPos = mdbWriter.BaseStream.Position;
                                var blockLength0 = (int)(currentPos - pos);
                                logs.Report("Writing file length and strings ... ");
                                watch.Restart();
                                mdbWriter.Write(false);
                                mdbWriter.BaseStream.Seek(0, SeekOrigin.Begin);
                                mdbWriter.Write(currentPos + 1);
                                mdbWriter.Write((int)currentPos - 11);
                                mdbWriter.Write(modelIndex);
                                mdbWriter.BaseStream.Seek(pos, SeekOrigin.Begin);
                                mdbWriter.Write(blockLength0);
                                mdbWriter.BaseStream.Seek(0, SeekOrigin.End);
                                WriteStrings(mdbWriter);
                                watch.Stop();
                                logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                                mdbWriter.Dispose(); //free the last file
                            }
                            catch
                            {
                                throw new Exception("Unable to write file " + currentFileName + ".mdb.");
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        logs.Report(ex.Message + "\n\n");
                        ClearObjStuff();
                        progress.Report(i + 1);
                        continue;
                    }
                    logs.Report("\n");
                    ClearObjStuff();
                    progress.Report(i + 1);
                }
            }
            catch (Exception ex)
            {
                logs.Report(ex.Message);
            }
        }

        private void ReadObj(StreamReader objReader)
        {
            var group = ("UnamedMesh", new List<(string, List<ObjTriangle>)>());
            var matGroup = (string.Empty, new List<ObjTriangle>());
            string line;
            char[] separator = { ' ' };
            while ((line = objReader.ReadLine()) != null)
            {
                //vertex
                if (line.StartsWith("v ", StringComparison.OrdinalIgnoreCase))
                {
                    var s = line.Split(separator, 4);
                    try
                    {
                        v.Add(new Vector3(float.Parse(s[1]), float.Parse(s[2]), float.Parse(s[3])));
                    }
                    catch { }
                }
                //vertex texture
                else if (line.StartsWith("vt ", StringComparison.OrdinalIgnoreCase))
                {
                    var s = line.Split(separator, 3);
                    try
                    {
                        vt.Add(new Vector2(float.Parse(s[1]), float.Parse(s[2])));
                    }
                    catch { }
                }
                //vertex normal
                else if (line.StartsWith("vn ", StringComparison.OrdinalIgnoreCase))
                {
                    var s = line.Split(separator, 4);
                    try
                    {
                        vn.Add(new Vector3(float.Parse(s[1]), float.Parse(s[2]), float.Parse(s[3])));
                    }
                    catch { }
                }
                //triangle outside group
                else if (line.StartsWith("f ", StringComparison.OrdinalIgnoreCase))
                {
                    var s = line.Split(separator, 4);
                    try
                    {
                        var p0 = s[1].Split('/');
                        var p1 = s[2].Split('/');
                        var p2 = s[3].Split('/');
                        var tri = new ObjTriangle();
                        for (int i = 0; i < Math.Min(p0.Length, 3); i++)
                        {
                            int.TryParse(p0[i], out tri.P0[i]);
                        }
                        for (int i = 0; i < Math.Min(p1.Length, 3); i++)
                        {
                            int.TryParse(p1[i], out tri.P1[i]);
                        }
                        for (int i = 0; i < Math.Min(p2.Length, 3); i++)
                        {
                            int.TryParse(p2[i], out tri.P2[i]);
                        }
                        matGroup.Item2.Add(tri);
                    }
                    catch { }
                }
                //triangle material
                else if (line.StartsWith("usemtl ", StringComparison.OrdinalIgnoreCase))
                {
                    //Add the mat group to the group if it's not empty.
                    if (matGroup.Item2.Count > 0)
                    {
                        group.Item2.Add(matGroup);
                        matGroup.Item2 = new List<ObjTriangle>();
                    }
                    matGroup.Item1 = line.Substring(7);
                }
                //material file
                else if (line.StartsWith("mtllib ", StringComparison.OrdinalIgnoreCase))
                {
                    mtlName = line.Substring(7);
                }
                //group/object
                else if (line.StartsWith("g ", StringComparison.OrdinalIgnoreCase) || line.StartsWith("o ", StringComparison.OrdinalIgnoreCase))
                {
                    var groupName = line.Substring(2);
                    //if it's not the same group (if it is, do nothing)
                    if (!group.Item1.Equals(groupName))
                    {
                        var groupExists = false;
                        foreach (var tempGroup in groups)
                        {
                            if (tempGroup.groupName.Equals(groupName))
                            {
                                groupExists = true;
                                group = tempGroup;
                                break;
                            }
                        }
                        //if it's a new group
                        if (!groupExists)
                        {
                            //Add the current mat group to the current group before creating a new one
                            if (matGroup.Item2.Count > 0)
                            {
                                group.Item2.Add(matGroup);
                                matGroup.Item2 = new List<ObjTriangle>();
                            }
                            //Add the group to the group list if it's not empty. The empty mat group check is done in the usemtl block.
                            if (group.Item2.Count > 0)
                            {
                                groups.Add(group);
                                group.Item2 = new List<(string, List<ObjTriangle>)>();
                            }
                            group.Item1 = line.Substring(2);
                        }

                    }
                }
            }
            //Don't forget to add the last group to the list
            //Add the current mat group to the current group before creating a new one
            if (matGroup.Item2.Count > 0)
            {
                group.Item2.Add(matGroup);
                matGroup.Item2 = new List<ObjTriangle>();
            }
            //Add the group to the group list if it's not empty. The empty mat group check is done in the usemtl block.
            if (group.Item2.Count > 0)
            {
                groups.Add(group);
                group.Item2 = new List<(string, List<ObjTriangle>)>();
            }
        }

        private void ReadMtl(StreamReader mtlReader)
        {
            string line;
            Material mat = null;
            while((line = mtlReader.ReadLine())!=null)
            {
                if (line.StartsWith("newmtl ", StringComparison.OrdinalIgnoreCase))
                {
                    if (mat != null && !finalMat.Exists(m => m.MatName.Equals(mat.MatName, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (finalMat.Count > 65535)
                            throw new Exception("Material count can't exceed 65536.");
                        finalMat.Add(mat);
                    }
                    mat = new Material(line.Substring(7), "NULL");
                }
                else if (line.StartsWith("map_kd ", StringComparison.OrdinalIgnoreCase))
                {
                    if (mat != null)
                        mat.TexName = Path.ChangeExtension(line.Substring(7), "tga");
                }
            }
            //add the last mat
            if (mat != null && !finalMat.Exists(m => m.MatName.Equals(mat.MatName, StringComparison.OrdinalIgnoreCase)))
            {
                if (finalMat.Count > 65535)
                    throw new Exception("Material count can't exceed 65536.");
                finalMat.Add(mat);
            }
        }

        private void ReadCbox(StreamReader cboxReader)
        {
            var cboxGroup = (string.Empty, new List<CollisionBox>());
            string line;
            char[] separator = { '\t' };
            while((line = cboxReader.ReadLine())!=null)
            {
                if(line.StartsWith("MESH\t"))
                {
                    //add the group to the list if it's not empty
                    if(cboxGroup.Item2.Count>0)
                    {
                        cboxGroups.Add((cboxGroup.Item1, cboxGroup.Item2[0]));
                        cboxGroup.Item2.Clear();
                    }
                    cboxGroup.Item1 = line.Substring(5);
                }
                else if(line.StartsWith("CBOX\t"))
                {
                    //if we have a mesh for the box
                    if(!string.IsNullOrWhiteSpace(cboxGroup.Item1))
                    {
                        var s = line.Split(separator, 4, StringSplitOptions.RemoveEmptyEntries);
                        CollisionBox box;
                        try
                        {
                            //get the box if it exists
                            box = cboxGroup.Item2.Find((b) => b.BoxName.Equals(s[1]));
                        }
                        catch
                        {
                            //if any trouble to get the box name, go to next line
                            continue;
                        }
                        //if we haven't created the box yet
                        if(box is null)
                        {
                            box = new CollisionBox() { BoxName = s[1] };
                            cboxGroup.Item2.Add(box);
                            try
                            {
                                //set leftchild
                                var leftChild = cboxGroup.Item2.Find((b) => b.BoxName.Equals(s[2]));
                                if(leftChild is null)
                                {
                                    leftChild = new CollisionBox() { BoxName = s[2], Level = box.Level + 1 };
                                    cboxGroup.Item2.Add(leftChild);
                                }
                                box.Leftchild = leftChild;
                                //set rightchild
                                var rightChild = cboxGroup.Item2.Find((b) => b.BoxName.Equals(s[3]));
                                if (rightChild is null)
                                {
                                    rightChild = new CollisionBox() { BoxName = s[3], Level = box.Level + 1 };
                                    cboxGroup.Item2.Add(rightChild);
                                }
                                box.Rightchild = rightChild;
                            }
                            //catch s[2] or s[3] null if we don't have leftchild and/or rightchild
                            catch { }
                        }
                        //if the box already exists, and is not level 5 or higher in the tree
                        //if the box level is >= 5, ignore it (for now)
                        else if(box.Level < 5)
                        {
                            try
                            {
                                //set leftchild
                                var leftChild = cboxGroup.Item2.Find((b) => b.BoxName.Equals(s[2]));
                                if (leftChild is null)
                                {
                                    leftChild = new CollisionBox() { BoxName = s[2], Level = box.Level + 1 };
                                    cboxGroup.Item2.Add(leftChild);
                                }
                                box.Leftchild = leftChild;
                                //set rightchild
                                var rightChild = cboxGroup.Item2.Find((b) => b.BoxName.Equals(s[3]));
                                if (rightChild is null)
                                {
                                    rightChild = new CollisionBox() { BoxName = s[3], Level = box.Level + 1 };
                                    cboxGroup.Item2.Add(rightChild);
                                }
                                box.Rightchild = rightChild;
                            }
                            //catch s[2] or s[3] null if we don't have leftchild and/or rightchild
                            catch { }
                        }
                    }
                }
            }
            //don't forget to add the last group to the list if it's not empty
            if (cboxGroup.Item2.Count > 0)
                cboxGroups.Add((cboxGroup.Item1, cboxGroup.Item2[0]));
        }

        private void ReadCboxValues(CollisionBox box, IProgress<string> logs)
        {
            for(int i=0;i<groups.Count;i++)
            {
                var group = groups[i];
                //if we find the group for the box
                if(group.groupName.Equals(box.BoxName))
                {
                    var points = GetPointsFromCBoxGroup(group.matGroups);
                    AllPca(box, points, out _);
                    groups.RemoveAt(i); //i never out of range
                    if (box.Leftchild!=null)
                        ReadCboxValues(box.Leftchild, logs);
                    if (box.Rightchild != null)
                        ReadCboxValues(box.Rightchild, logs);
                    return;
                }
            }
            //if we didn't find the group
            logs.Report("Warning : the group " + box.BoxName + " doesn't exist.\n");
        }

        private List<int> GetPointsFromCBoxGroup(List<(string matName, List<ObjTriangle> tris)> matGroups)
        {
            List<int> points = new List<int>();
            for(int i=0;i<matGroups.Count;i++)
            {
                var matGroup = matGroups[i];
                for(int j=0;j<matGroup.tris.Count; j++)
                {
                    var tri = matGroup.tris[j];
                    bool hasFoundP0, hasFoundP1, hasFoundP2;
                    hasFoundP0 = hasFoundP1 = hasFoundP2 = false;
                    for(int k=0;k<points.Count;k++)
                    {
                        var p = points[k];
                        if (p == tri.P0[0])
                        {
                            hasFoundP0 = true;
                            if (hasFoundP0 && hasFoundP1 && hasFoundP2)
                                break;
                        }
                        if (p == tri.P1[0])
                        {
                            hasFoundP1 = true;
                            if (hasFoundP0 && hasFoundP1 && hasFoundP2)
                                break;
                        }
                        if (p == tri.P2[0])
                        {
                            hasFoundP2 = true;
                            if (hasFoundP0 && hasFoundP1 && hasFoundP2)
                                break;
                        }
                    }
                    if (!hasFoundP0)
                        points.Add(tri.P0[0]);
                    if (!hasFoundP1)
                        points.Add(tri.P1[0]);
                    if (!hasFoundP2)
                        points.Add(tri.P2[0]);
                }
            }
            return points;
        }

        private void AutoGenerateCBox(CollisionBox box, List<(string matName, List<ObjTriangle> tris)> triangles)
        {
            var points = GetPointsFromCBoxGroup(triangles);
            AllPca(box, points, out Vector3 mean);
            if (box.Level<5)
            {
                box.Leftchild = new CollisionBox() { Level = box.Level + 1 };
                box.Rightchild = new CollisionBox() { Level = box.Level + 1 };
                var leftChild = new List<ObjTriangle>();
                var rightChild = new List<ObjTriangle>();
                var maxLength = Math.Max(box.Length.X, Math.Max(box.Length.Y, box.Length.Z));
                if (maxLength == box.Length.X)
                {
                    var tempPosX = box.OCross.X * mean.X + box.OCross.Y * mean.Y + box.OCross.Z * mean.Z;
                    for (int i = 0; i < triangles.Count; i++)
                    {
                        var triGroup = triangles[i];
                        for(int j=0;j< triGroup.tris.Count;j++)
                        {
                            var tri = triGroup.tris[j];
                            Vector3 p0 = v[tri.P0[0] - 1], p1 = v[tri.P1[0] - 1], p2 = v[tri.P2[0] - 1];
                            var center = new Vector3((Math.Min(Math.Min(p0.X, p1.X), p2.X) + Math.Max(Math.Max(p0.X, p1.X), p2.X)) / 2,
                                                     (Math.Min(Math.Min(p0.Y, p1.Y), p2.Y) + Math.Max(Math.Max(p0.Y, p1.Y), p2.Y)) / 2,
                                                     (Math.Min(Math.Min(p0.Z, p1.Z), p2.Z) + Math.Max(Math.Max(p0.Z, p1.Z), p2.Z)) / 2);
                            var centerTempX = box.OCross.X * center.X + box.OCross.Y * center.Y + box.OCross.Z * center.Z;
                            if (centerTempX < tempPosX)
                                leftChild.Add(tri);
                            else
                                rightChild.Add(tri);
                        }
                    }
                    AutoGenerateCBox(box.Leftchild, new List<(string, List<ObjTriangle>)> { (string.Empty, leftChild) });
                    AutoGenerateCBox(box.Rightchild, new List<(string, List<ObjTriangle>)> { (string.Empty, rightChild) });
                }
                else if(maxLength == box.Length.Y)
                {
                    var tempPosY = box.OUp.X * mean.X + box.OUp.Y * mean.Y + box.OUp.Z * mean.Z;
                    for (int i = 0; i < triangles.Count; i++)
                    {
                        var triGroup = triangles[i];
                        for (int j = 0; j < triGroup.tris.Count; j++)
                        {
                            var tri = triGroup.tris[j];
                            Vector3 p0 = v[tri.P0[0] - 1], p1 = v[tri.P1[0] - 1], p2 = v[tri.P2[0] - 1];
                            var center = new Vector3((Math.Min(Math.Min(p0.X, p1.X), p2.X) + Math.Max(Math.Max(p0.X, p1.X), p2.X)) / 2,
                                                     (Math.Min(Math.Min(p0.Y, p1.Y), p2.Y) + Math.Max(Math.Max(p0.Y, p1.Y), p2.Y)) / 2,
                                                     (Math.Min(Math.Min(p0.Z, p1.Z), p2.Z) + Math.Max(Math.Max(p0.Z, p1.Z), p2.Z)) / 2);
                            var centerTempY = box.OUp.X * center.X + box.OUp.Y * center.Y + box.OUp.Z * center.Z;
                            if (centerTempY < tempPosY)
                                leftChild.Add(tri);
                            else
                                rightChild.Add(tri);
                        }
                    }
                    AutoGenerateCBox(box.Leftchild, new List<(string, List<ObjTriangle>)> { (string.Empty, leftChild) });
                    AutoGenerateCBox(box.Rightchild, new List<(string, List<ObjTriangle>)> { (string.Empty, rightChild) });
                }
                else
                {
                    var tempPosZ = box.OForward.X * mean.X + box.OForward.Y * mean.Y + box.OForward.Z * mean.Z;
                    for (int i = 0; i < triangles.Count; i++)
                    {
                        var triGroup = triangles[i];
                        for (int j = 0; j < triGroup.tris.Count; j++)
                        {
                            var tri = triGroup.tris[j];
                            Vector3 p0 = v[tri.P0[0] - 1], p1 = v[tri.P1[0] - 1], p2 = v[tri.P2[0] - 1];
                            var center = new Vector3((Math.Min(Math.Min(p0.X, p1.X), p2.X) + Math.Max(Math.Max(p0.X, p1.X), p2.X)) / 2,
                                                     (Math.Min(Math.Min(p0.Y, p1.Y), p2.Y) + Math.Max(Math.Max(p0.Y, p1.Y), p2.Y)) / 2,
                                                     (Math.Min(Math.Min(p0.Z, p1.Z), p2.Z) + Math.Max(Math.Max(p0.Z, p1.Z), p2.Z)) / 2);
                            var centerTempZ = box.OForward.X * center.X + box.OForward.Y * center.Y + box.OForward.Z * center.Z;
                            if (centerTempZ < tempPosZ)
                                leftChild.Add(tri);
                            else
                                rightChild.Add(tri);
                        }
                    }
                    AutoGenerateCBox(box.Leftchild, new List<(string, List<ObjTriangle>)> { (string.Empty, leftChild) });
                    AutoGenerateCBox(box.Rightchild, new List<(string, List<ObjTriangle>)> { (string.Empty, rightChild) });
                }
            }
        }

        private void AllPca(CollisionBox box, List<int> points, out Vector3 mean)
        {
            for (int i = 0; i < points.Count; i++)
            {
                try
                {
                    var p = v[points[i] - 1];
                    box.Position.X += p.X;
                    box.Position.Y += p.Y;
                    box.Position.Z += p.Z;
                }
                catch { }
            }
            if (points.Count!=0)
            {
                box.Position /= points.Count;
            }
            mean = box.Position;
            Vector3 covMatRow1 = Vector3.Zero, covMatRow2 = covMatRow1, covMatRow3 = covMatRow1;
            for(int i=0;i<points.Count;i++)
            {
                var point = points[i];
                try
                {
                    var p = v[point - 1];
                    covMatRow1.X += (p.X - box.Position.X) * (p.X - box.Position.X);
                    covMatRow1.Y += (p.X - box.Position.X) * (p.Y - box.Position.Y);
                    covMatRow1.Z += (p.X - box.Position.X) * (p.Z - box.Position.Z);
                    covMatRow2.Y += (p.Y - box.Position.Y) * (p.Y - box.Position.Y);
                    covMatRow2.Z += (p.Y - box.Position.Y) * (p.Z - box.Position.Z);
                    covMatRow3.Z += (p.Z - box.Position.Z) * (p.Z - box.Position.Z);
                }
                catch { }
            }
            if (points.Count != 0)
            {
                covMatRow1 /= points.Count;
                covMatRow2 /= points.Count;
                covMatRow3 /= points.Count;
            }
            //Symetry in the covariance matrix
            covMatRow2.X = covMatRow1.Y;
            covMatRow3.X = covMatRow1.Z;
            covMatRow3.Y = covMatRow2.Z;
            var eigenValues = MatrixEigenStuff.EigenValues(covMatRow1, covMatRow2, covMatRow3);
            box.OCross = Vector3.Normalize(MatrixEigenStuff.EigenVector(covMatRow1, covMatRow2, eigenValues.X));
            box.OUp = Vector3.Normalize(MatrixEigenStuff.EigenVector(covMatRow1, covMatRow2, eigenValues.Z));
            box.OForward = Vector3.Cross(box.OCross, box.OUp);
            Vector3 tempx = new Vector3(box.OCross.X, box.OUp.X, box.OForward.X),
                    tempy = new Vector3(box.OCross.Y, box.OUp.Y, box.OForward.Y),
                    tempz = new Vector3(box.OCross.Z, box.OUp.Z, box.OForward.Z);
            float minx, miny, minz, maxx, maxy, maxz;
            minx = miny = minz = float.MaxValue;
            maxx = maxy = maxz = float.MinValue;

            for(int i=0;i<points.Count;i++)
            {
                var point = points[i];
                try
                {
                    var p = v[point - 1];
                    float vTempx = tempx.X * p.X + tempy.X * p.Y + tempz.X * p.Z,
                          vTempy = tempx.Y * p.X + tempy.Y * p.Y + tempz.Y * p.Z,
                          vTempz = tempx.Z * p.X + tempy.Z * p.Y + tempz.Z * p.Z;
                    if (vTempx < minx)
                        minx = vTempx;
                    if (vTempy < miny)
                        miny = vTempy;
                    if (vTempz < minz)
                        minz = vTempz;
                    if (vTempx > maxx)
                        maxx = vTempx;
                    if (vTempy > maxy)
                        maxy = vTempy;
                    if (vTempz > maxz)
                        maxz = vTempz;
                }
                catch { }
            }

            box.Position.X = (minx + maxx) / 2;
            box.Position.Y = (miny + maxy) / 2;
            box.Position.Z = (minz + maxz) / 2;
            box.Length.X = (maxx - minx) / 2;
            box.Length.Y = (maxy - miny) / 2;
            box.Length.Z = (maxz - minz) / 2;
            var tempPos = box.Position;
            box.Position.X = box.OCross.X * tempPos.X + box.OUp.X * tempPos.Y + box.OForward.X * tempPos.Z;
            box.Position.Y = box.OCross.Y * tempPos.X + box.OUp.Y * tempPos.Y + box.OForward.Y * tempPos.Z;
            box.Position.Z = box.OCross.Z * tempPos.X + box.OUp.Z * tempPos.Y + box.OForward.Z * tempPos.Z;
        }

        private bool PointsEquals(int[] p0, int[] p1)
        {
            if(p0.Length == p1.Length)
            {
                for(int i=0;i<p0.Length;i++)
                    if (p0[i] != p1[i])
                        return false;
                return true;
            }
            return false;
        }

        private ushort FindMatIndex(string matName)
        {
            ushort nullMatIndex = 0, matCount = (ushort)finalMat.Count;
            bool hasFoundNull = false;
            for (ushort i = 0; i < matCount; i++)
            {
                var mat = finalMat[i];
                if (mat.MatName.Equals(matName))
                    return i;
                if(!hasFoundNull && mat.TexName.Equals("NULL"))
                {
                    hasFoundNull = true;
                    nullMatIndex = i;
                }
            }
            if (hasFoundNull)
                return nullMatIndex;
            else if (finalMat.Count <= 65535)
            {
                finalMat.Add(new Material("", "NULL"));
                return (ushort)(finalMat.Count - 1);
            }
            else
                return 0; //idk
        }

        private void ProcessGroup(List<(string matName, List<ObjTriangle> tris)> matGroups,
            List<int[]> points, List<(ushort matIndex, List<ushort[]> pointsIndex)> triangles)
        {
            for(int i=0;i<matGroups.Count;i++)
            {
                var matGroup = matGroups[i];
                var triGroup = (FindMatIndex(matGroup.matName), new List<ushort[]>());
                for (int j=0;j<matGroup.tris.Count;j++)
                {
                    var tri = matGroup.tris[j];
                    ushort index, p0Index, p1Index, p2Index;
                    index = p0Index = p1Index = p2Index = 0;
                    bool hasFoundP0, hasFoundP1, hasFoundP2;
                    hasFoundP0 = hasFoundP1 = hasFoundP2 = false;
                    for(int k=0;k<points.Count;k++)
                    {
                        var p = points[k];
                        if (PointsEquals(tri.P0, p))
                        {
                            p0Index = index;
                            hasFoundP0 = true;
                            if (hasFoundP0 && hasFoundP1 && hasFoundP2)
                                break;
                        }
                        if (PointsEquals(tri.P1, p))
                        {
                            p1Index = index;
                            hasFoundP1 = true;
                            if (hasFoundP0 && hasFoundP1 && hasFoundP2)
                                break;
                        }
                        if (PointsEquals(tri.P2, p))
                        {
                            p2Index = index;
                            hasFoundP2 = true;
                            if (hasFoundP0 && hasFoundP1 && hasFoundP2)
                                break;
                        }
                        index++;
                    }
                    if (!hasFoundP2)
                    {
                        if (points.Count > 65535)
                            throw new Exception("Model vertex count exceeded 65536.\n");
                        p2Index = (ushort)points.Count;
                        points.Add(tri.P2);
                    }
                    if (!hasFoundP1)
                    {
                        if (points.Count > 65535)
                            throw new Exception("Model vertex count exceeded 65536.\n");
                        p1Index = (ushort)points.Count;
                        points.Add(tri.P1);
                    }
                    if (!hasFoundP0)
                    {
                        if (points.Count > 65535)
                            throw new Exception("Model vertex count exceeded 65536.\n");
                        p0Index = (ushort)points.Count;
                        points.Add(tri.P0);
                    }
                    triGroup.Item2.Add(new ushort[] { p0Index, p1Index, p2Index });
                }
                if(triGroup.Item2.Count>0) //add if not empty
                    triangles.Add(triGroup);
            }
        }

        private void WriteGroup(BinaryWriter mdbWriter, List<int[]> points,
            List<(ushort matIndex, List<ushort[]> pointsIndex)> triangles, int modelIndex,
            ref float minX, ref float minY, ref float minZ, ref float maxX, ref float maxY, ref float maxZ, ref int tCount)
        {
            var pos = mdbWriter.BaseStream.Position;
            mdbWriter.Write(0);
            float tempMinX, tempMinY, tempMinZ, tempMaxX, tempMaxY, tempMaxZ;
            tempMinX = tempMinY = tempMinZ = float.MaxValue;
            tempMaxX = tempMaxY = tempMaxZ = float.MinValue;
            mdbWriter.Write(modelIndex);
            mdbWriter.Write(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                var pV = v[p[0] - 1];
                var pVt = vt[p[1] - 1];
                var pVn = vn[p[2] - 1];
                if (pV.X < tempMinX)
                    tempMinX = pV.X;
                if (pV.Y < tempMinY)
                    tempMinY = pV.Y;
                if (pV.Z < tempMinZ)
                    tempMinZ = pV.Z;
                if (pV.X > tempMaxX)
                    tempMaxX = pV.X;
                if (pV.Y > tempMaxY)
                    tempMaxY = pV.Y;
                if (pV.Z > tempMaxZ)
                    tempMaxZ = pV.Z;
                mdbWriter.Write(32);
                mdbWriter.Write(pV.X);
                mdbWriter.Write(-pV.Z);
                mdbWriter.Write(pV.Y);
                mdbWriter.Write(pVt.X);
                mdbWriter.Write(-pVt.Y);
                if (pVn.Z < -1)
                    pVn.Z = -1;
                else if (pVn.Z > 1)
                    pVn.Z = 1;
                if (pVn.X <= 0)
                    mdbWriter.Write((float)Math.Acos(-pVn.Z));
                else
                    mdbWriter.Write((float)-Math.Acos(-pVn.Z));
                mdbWriter.Write((float)Math.Asin(pVn.Y));
                //FF FF FF FF
                mdbWriter.Write(-1);
            }
            var tPos = mdbWriter.BaseStream.Position;
            var tempTCount = 0;
            mdbWriter.Write(0);
            for (int i = 0; i < triangles.Count; i++)
            {
                var triGroup = triangles[i];
                for (int j = 0; j < triGroup.pointsIndex.Count; j++)
                {
                    var tri = triGroup.pointsIndex[j];
                    mdbWriter.Write(8);
                    mdbWriter.Write(tri[2]);
                    mdbWriter.Write(tri[1]);
                    mdbWriter.Write(tri[0]);
                    mdbWriter.Write(triGroup.matIndex);
                }
                tempTCount += triGroup.pointsIndex.Count;
            }
            int blockLength = (int)(mdbWriter.BaseStream.Position - pos);
            mdbWriter.BaseStream.Seek(tPos, SeekOrigin.Begin);
            mdbWriter.Write(tempTCount);
            mdbWriter.BaseStream.Seek(pos, SeekOrigin.Begin);
            mdbWriter.Write(blockLength);
            mdbWriter.BaseStream.Seek(0, SeekOrigin.End);
            mdbWriter.Write(0); //potential animation (or whatever this is) count
            if (modelIndex == 0)
            {
                minX = tempMinX;
                minY = tempMinY;
                minZ = tempMinZ;
                maxX = tempMaxX;
                maxY = tempMaxY;
                maxZ = tempMaxZ;
                tCount = tempTCount;
            }
        }

        private void WriteMaterials(BinaryWriter mdbWriter)
        {
            mdbWriter.Write(finalMat.Count);
            for(int i=0;i<finalMat.Count;i++)
            {
                var mat = finalMat[i];
                string texture = Path.GetFileName(mat.TexName);
                int taillebloc = 76 + texture.Length;
                mdbWriter.Write(taillebloc);
                mdbWriter.Write(texture.Length);
                mdbWriter.Write(Encoding.Default.GetBytes(texture));
                for (int j = 0; j < 8; j++)
                {
                    //00 00 80 3F
                    mdbWriter.Write(1065353216);
                }
                for (int j = 0; j < 3; j++)
                {
                    //00 00 00 00
                    mdbWriter.Write(0);
                }
                //00 00 80 3F
                mdbWriter.Write(1065353216);
                for (int j = 0; j < 3; j++)
                {
                    //00 00 00 00
                    mdbWriter.Write(0);
                }
                //00 00 80 3F
                mdbWriter.Write(1065353216);
                for (int j = 0; j < 2; j++)
                {
                    //00 00 00 00
                    mdbWriter.Write(0);
                }
            }
            mdbWriter.Write(0); //bones count, maybe one day
        }

        private void WriteBoundingValues(BinaryWriter mdbWriter, float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            var posx = (minX + maxX) / 2;
            var posy = (minY + maxY) / 2;
            var posz = (minZ + maxZ) / 2;
            var lenx = maxX - minX;
            var leny = maxY - minY;
            var lenz = maxZ - minZ;
            //data block
            mdbWriter.Write(minX);
            mdbWriter.Write(-maxZ);
            mdbWriter.Write(minY);
            mdbWriter.Write(maxX);
            mdbWriter.Write(-minZ);
            mdbWriter.Write(maxY);
            mdbWriter.Write(posx);
            mdbWriter.Write(-posz);
            mdbWriter.Write(posy);
            var diag = (float)Math.Sqrt(lenx * lenx + leny * leny + lenz * lenz) / 2;
            mdbWriter.Write(diag);
        }

        private void WriteCollisionBox(BinaryWriter mdbWriter, CollisionBox box, int collisionTriangles)
        {
            var pos = mdbWriter.BaseStream.Position;
            mdbWriter.Write(0);
            mdbWriter.Write(2);
            mdbWriter.Write(72);
            mdbWriter.Write(3);
            mdbWriter.Write(box.Position.X);
            mdbWriter.Write(-box.Position.Z);
            mdbWriter.Write(box.Position.Y);
            mdbWriter.Write(4);
            mdbWriter.Write(0);
            mdbWriter.Write(5);
            mdbWriter.Write(box.OCross.X);
            mdbWriter.Write(-box.OCross.Z);
            mdbWriter.Write(box.OCross.Y);
            mdbWriter.Write(6);
            mdbWriter.Write(box.OUp.X);
            mdbWriter.Write(-box.OUp.Z);
            mdbWriter.Write(box.OUp.Y);
            mdbWriter.Write(7);
            mdbWriter.Write(box.OForward.X);
            mdbWriter.Write(-box.OForward.Z);
            mdbWriter.Write(box.OForward.Y);
            mdbWriter.Write(8);
            mdbWriter.Write(box.Length.X);
            mdbWriter.Write(box.Length.Y);
            mdbWriter.Write(box.Length.Z);
            mdbWriter.Write(9);
            mdbWriter.Write(Math.Max(Math.Max(box.Length.X, box.Length.Y), box.Length.Z));
            mdbWriter.Write(10);
            mdbWriter.Write(box.Level);
            mdbWriter.Write(11);
            if(box.Leftchild!=null)
            {
                mdbWriter.Write(true);
                mdbWriter.Write(12);
                if (box.Rightchild != null)
                {
                    mdbWriter.Write(true);
                    mdbWriter.Write(13);
                    WriteCollisionBox(mdbWriter, box.Leftchild, collisionTriangles);
                    mdbWriter.Write(16);
                    WriteCollisionBox(mdbWriter, box.Rightchild, collisionTriangles);
                }
                else
                {
                    mdbWriter.Write(false);
                    mdbWriter.Write(13);
                    WriteCollisionBox(mdbWriter, box.Leftchild, collisionTriangles);
                }
            }
            else
            {
                mdbWriter.Write(false);
                mdbWriter.Write(12);
                if (box.Rightchild != null)
                {
                    mdbWriter.Write(true);
                    mdbWriter.Write(16);
                    WriteCollisionBox(mdbWriter, box.Rightchild, collisionTriangles);
                }
                else
                {
                    mdbWriter.Write(false);
                }
            }
            mdbWriter.Write(14);
            if(box.Level != 0)
                mdbWriter.Write(0);
            else
            {
                mdbWriter.Write(collisionTriangles);
                for(int i=0;i<collisionTriangles;i++)
                {
                    mdbWriter.Write(15);
                    mdbWriter.Write(i);
                }
            }
            var blockLength1 = (int)(mdbWriter.BaseStream.Position - pos);
            mdbWriter.BaseStream.Seek(pos, SeekOrigin.Begin);
            mdbWriter.Write(blockLength1);
            mdbWriter.BaseStream.Seek(0, SeekOrigin.End);
        }

        private void WriteHitbox(BinaryWriter mdbWriter, List<(string matName, List<ObjTriangle> tris)> hitBox, int tCount)
        {
            mdbWriter.Write(18);
            mdbWriter.Write(tCount);
            for(int i=0;i<hitBox.Count;i++)
            {
                var matGroup = hitBox[i];
                for(int j=0;j<matGroup.tris.Count;j++)
                {
                    var tri = matGroup.tris[j];
                    var p2 = v[tri.P0[0] - 1];
                    var p1 = v[tri.P1[0] - 1];
                    var p0 = v[tri.P2[0] - 1];
                    mdbWriter.Write(19);
                    mdbWriter.Write(48);
                    mdbWriter.Write(20);
                    mdbWriter.Write(p0.X);
                    mdbWriter.Write(-p0.Z);
                    mdbWriter.Write(p0.Y);
                    mdbWriter.Write(21);
                    mdbWriter.Write(p1.X);
                    mdbWriter.Write(-p1.Z);
                    mdbWriter.Write(p1.Y);
                    mdbWriter.Write(22);
                    mdbWriter.Write(p2.X);
                    mdbWriter.Write(-p2.Z);
                    mdbWriter.Write(p2.Y);
                }
            }
        }

        private void WriteStrings(BinaryWriter mdbWriter)
        {
            mdbWriter.Write(21);
            mdbWriter.Write(8);
            mdbWriter.Write(Encoding.Default.GetBytes("MeshData"));
            mdbWriter.Write(4);
            mdbWriter.Write(Encoding.Default.GetBytes("Root"));
            mdbWriter.Write(10);
            mdbWriter.Write(Encoding.Default.GetBytes("LocalBasis"));
            mdbWriter.Write(8);
            mdbWriter.Write(Encoding.Default.GetBytes("Position"));
            mdbWriter.Write(20);
            mdbWriter.Write(Encoding.Default.GetBytes("LookAt Vector Length"));
            mdbWriter.Write(19);
            mdbWriter.Write(Encoding.Default.GetBytes("Orientation - Cross"));
            mdbWriter.Write(21);
            mdbWriter.Write(Encoding.Default.GetBytes("Orientation - Forward"));
            mdbWriter.Write(16);
            mdbWriter.Write(Encoding.Default.GetBytes("Orientation - Up"));
            mdbWriter.Write(6);
            mdbWriter.Write(Encoding.Default.GetBytes("Length"));
            mdbWriter.Write(6);
            mdbWriter.Write(Encoding.Default.GetBytes("Radius"));
            mdbWriter.Write(5);
            mdbWriter.Write(Encoding.Default.GetBytes("Level"));
            mdbWriter.Write(12);
            mdbWriter.Write(Encoding.Default.GetBytes("HasLeftChild"));
            mdbWriter.Write(13);
            mdbWriter.Write(Encoding.Default.GetBytes("HasRightChild"));
            mdbWriter.Write(39);
            mdbWriter.Write(Encoding.Default.GetBytes("Valid Collision Triangle Indices - Size"));
            mdbWriter.Write(42);
            mdbWriter.Write(Encoding.Default.GetBytes("Valid Collision Triangle Indices - Element"));
            mdbWriter.Write(8);
            mdbWriter.Write(Encoding.Default.GetBytes("MaxLevel"));
            mdbWriter.Write(25);
            mdbWriter.Write(Encoding.Default.GetBytes("CollisionTriangles - Size"));
            mdbWriter.Write(28);
            mdbWriter.Write(Encoding.Default.GetBytes("CollisionTriangles - Element"));
            mdbWriter.Write(2);
            mdbWriter.Write(Encoding.Default.GetBytes("P0"));
            mdbWriter.Write(2);
            mdbWriter.Write(Encoding.Default.GetBytes("P1"));
            mdbWriter.Write(2);
            mdbWriter.Write(Encoding.Default.GetBytes("P2"));

        }

        private string RealGroupName(string groupname)
        {
            int index = groupname.LastIndexOf('_');
            if (index != -1)
            {
                string temp = groupname.Substring(index + 1);
                return int.TryParse(temp, out _) ? groupname.Remove(index) : groupname;
            }
            else
            {
                return groupname;
            }
        }

        /// <summary>
        /// Clear obj related stuff to read a new obj file.
        /// </summary>
        private void ClearObjStuff()
        {
            mtlName = string.Empty;
            finalMat.Clear();
            v.Clear();
            vt.Clear();
            vn.Clear();
            groups.Clear();
            cboxGroups.Clear();
        }
    }
}
