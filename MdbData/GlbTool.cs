using Pfim;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using StbImageWriteSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TPShipToolkit.MdbData.Classes;
using TPShipToolkit.Utils;

namespace TPShipToolkit.MdbData
{
    public class GlbTool
    {
        private readonly List<MdbMesh> mdbMeshes = new(); // the meshes to be exported
        private readonly List<MdbMaterial> currentMdbMaterials = new(); // material list of the current mdb we are reading
        private readonly List<MdbMaterial> finalMdbMaterials = new(); // material list used for the export

        /// <summary>
        /// Converts X mdb file to 1 glb file.
        /// </summary>
        /// <param name="mdbs">The mdb file(s) path.</param>
        /// <param name="glbPath">The glb file path.</param>
        /// <param name="progress">Progress on the progress bar.</param>
        /// <param name="logs">Logs in the text box.</param>
        public void XMdbTo1Glb(string[] mdbs, string glbPath, bool exportLods, IProgress<int> progress, IProgress<string> logs)
        {
            try
            {
                var watch = new System.Diagnostics.Stopwatch();
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
                            ReadMdb(mdbReader, groupName, exportLods);
                            watch.Stop();
                            logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                        }
                        progress.Report(i + 1);
                    }
                    catch (Exception ex)
                    {
                        logs.Report("\n" + ex.Message);
                        progress.Report(i + 1);
                        continue;
                    }
                }
                logs.Report("Exporting ...");
                watch.Restart();
                WriteGlb(glbPath, logs);
                logs.Report("\nDone in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
            }
            catch (Exception ex)
            {
                logs.Report("\n" + ex.Message);
            }
        }

        /// <summary>
        /// Converts X mdb file to X glb file (1 glb for each mdb).
        /// </summary>
        /// <param name="mdbs">The mdb file(s) path.</param>
        /// <param name="glbFolderPath">The folder path to export the glb file(s).</param>
        /// <param name="progress">Progress on the progress bar.</param>
        /// <param name="logs">Logs in the text box.</param>
        public void XMdbToXGlb(string[] mdbs, string glbFolderPath, bool exportLods, IProgress<int> progress, IProgress<string> logs)
        {
            try
            {
                var watch = new System.Diagnostics.Stopwatch();
                for (int i = 0; i < mdbs.Length; i++)
                {
                    string mdb, groupName;
                    try
                    {
                        mdb = mdbs[i];
                        groupName = Path.GetFileNameWithoutExtension(mdb);
                        var glbPath = Path.Combine(glbFolderPath, Path.GetFileName(Path.ChangeExtension(mdb, "glb")));
                        using (BinaryReader mdbReader = new BinaryReader(File.OpenRead(mdb)))
                        {
                            logs.Report("Reading " + mdb + " ... ");
                            watch.Start();
                            ReadMdb(mdbReader, groupName, exportLods);
                            watch.Stop();
                            logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                        }
                        logs.Report("Exporting ...");
                        watch.Restart();
                        WriteGlb(glbPath, logs);
                        logs.Report("\nDone in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                        progress.Report(i + 1);
                    }
                    catch (Exception ex)
                    {
                        logs.Report("\n" + ex.Message);
                        progress.Report(i + 1);
                        continue;
                    }
                    finally
                    {
                        ClearData();
                    }
                }
            }
            catch (Exception ex)
            {
                logs.Report("\n" + ex.Message);
            }
        }

        /// <summary>
        /// Converts 1 glb file to X mdb file (by using the nodes name).
        /// </summary>
        /// <param name="glbs">The obj file(s) to export the mdb files from.</param>
        /// <param name="mdbFolderPath">The folder path to export the mdb files.</param>
        /// <param name="progress">Progress on the progress bar.</param>
        /// <param name="logs">Logs in the text box.</param>
        public void GlbToXMdb(string[] glbs, string mdbFolderPath, IProgress<int> progress, IProgress<string> logs)
        {
            foreach (var glbPath in glbs)
            {
                
                //foreach (var item in glbScene.Materials)
                //{
                //    if (item.Extras is JsonObject obj)
                //    {
                //        if (obj.TryGetPropertyValue("TextureName", out var value))
                //        {
                //            string name = value!.GetValue<string>();
                //        }
                //    }
                //    var channel = item.GetChannel(KnownChannel.BaseColor);
                //    if (channel != null)
                //    {
                //        var l = channel.Texture.PrimaryImage.Name;
                //    }
                //}
                try
                {
                    var glbScene = SceneBuilder.LoadDefaultScene(glbPath);
                    var currentFileName = string.Empty;
                    var instances = new List<InstanceBuilder>();
                    foreach (var instance in glbScene.Instances)
                    {
                        instances.Add(instance);
                    }
                    instances.Sort((x, y) => NaturalStringComparer.CompareNatural(x.Name, y.Name));
                    var groupedInstances = instances.GroupBy((i) => RealGroupName(i.Name));
                    foreach (var group in groupedInstances)
                    {
                        var mdbPath = Path.Combine(mdbFolderPath, group.Key + ".mdb");
                        using (var mdbWriter = new BinaryWriter(File.Open(mdbPath, FileMode.Create)))
                        {
                            WriteMdb(mdbWriter, group, logs);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logs.Report(ex.Message);
                }
                
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
        public void XGlbToXMdb(string[] glbs, string mdbFolderPath, IProgress<int> progress, IProgress<string> logs)
        {

        }

        Vector3 NormalFromAngles(float theta, float phi)
        {
            float x = (float)(Math.Cos(phi) * Math.Cos(theta));
            float y = (float)Math.Sin(phi);
            float z = (float)(Math.Cos(phi) * Math.Sin(theta));

            return new Vector3(x, z, -y);
        }

        private byte[] ConvertDdsToPngBytes(string ddsPath)
        {
            using var dds = Pfimage.FromFile(ddsPath);
            byte[] rgba;

            switch (dds.Format)
            {
                case Pfim.ImageFormat.Rgba32:
                    rgba = FixBgraToRgba(dds.Data);
                    break;

                case Pfim.ImageFormat.Rgb24:
                    rgba = new byte[dds.Width * dds.Height * 4];

                    for (int i = 0, j = 0; i < dds.Data.Length; i += 3, j += 4)
                    {
                        rgba[j + 2] = dds.Data[i + 2]; // B
                        rgba[j + 1] = dds.Data[i + 1]; // G
                        rgba[j + 0] = dds.Data[i + 0]; // R
                        rgba[j + 3] = 255;             // A
                    }
                    break;

                default:
                    throw new NotSupportedException(
                        $"Unsupported format : {dds.Format}");
            }
            return EncodePng(rgba, dds.Width, dds.Height);

        }

        byte[] FixBgraToRgba(byte[] data)
        {
            var result = new byte[data.Length];

            for (int i = 0; i < data.Length; i += 4)
            {
                result[i + 0] = data[i + 2]; // R
                result[i + 1] = data[i + 1]; // G
                result[i + 2] = data[i + 0]; // B
                result[i + 3] = data[i + 3]; // A
            }

            return result;
        }

        private byte[] EncodePng(byte[] rgba, int width, int height)
        {
            using var stream = new MemoryStream();

            var writer = new ImageWriter();

            writer.WritePng(
                rgba,
                width,
                height,
                StbImageWriteSharp.ColorComponents.RedGreenBlueAlpha,
                stream);

            return stream.ToArray();
        }

        /// <summary>
        /// Read an mdb file and get all the needed data from it.
        /// </summary>
        /// <param name="mdbReader">The mdb file.</param>
        /// <param name="groupName">The base group name to name the groups/object of the mdb.</param>
        /// <exception cref="Exception"></exception>
        private void ReadMdb(BinaryReader mdbReader, string groupName, bool exportLods)
        {
            var mdbMesh = new MdbMesh(groupName);
            uint modelCount, modelLength, modelStart, matCount, vCount, tCount;
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
                if (!exportLods && i > 0) //Skip lods
                {
                    //Vertexes
                    for (uint j = 0; j < vCount; j++)
                    {
                        mdbReader.BaseStream.Seek(36, SeekOrigin.Current);
                    }
                    tCount = mdbReader.ReadUInt32();

                    //Triangles
                    for (uint j = 0; j < tCount; j++)
                    {
                        mdbReader.BaseStream.Seek(12, SeekOrigin.Current);
                    }

                    //skip 4 bytes (potential animation count)
                    mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
                    //check if we reach the end (because potential animation block)
                    var length = mdbReader.BaseStream.Position - modelStart;
                    if (length < modelLength)
                        mdbReader.BaseStream.Seek(modelLength - length, SeekOrigin.Current);
                }
                else
                {
                    var meshModel = new MdbMeshModel();
                    //Vertexes
                    for (uint j = 0; j < vCount; j++)
                    {
                        try
                        {
                            //skip 4 bytes (vertex block length)
                            mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
                            float x, y, z, u, v, nx, ny;
                            byte r, g, b, a;
                            x = mdbReader.ReadSingle();
                            y = mdbReader.ReadSingle();
                            z = mdbReader.ReadSingle();
                            u = mdbReader.ReadSingle();
                            v = mdbReader.ReadSingle();
                            nx = mdbReader.ReadSingle();
                            ny = mdbReader.ReadSingle();
                            r = mdbReader.ReadByte();
                            g = mdbReader.ReadByte();
                            b = mdbReader.ReadByte();
                            a = mdbReader.ReadByte();
                            meshModel.MdbVertices.Add(new(x, y, z, u, v, nx, ny, r, g, b, a));
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
                    for (uint j = 0; j < tCount; j++)
                    {
                        try
                        {
                            //skip 4 bytes (tri block length)
                            mdbReader.BaseStream.Seek(4, SeekOrigin.Current);
                            ushort p0, p1, p2, textureIndex;
                            p0 = mdbReader.ReadUInt16();
                            p1 = mdbReader.ReadUInt16();
                            p2 = mdbReader.ReadUInt16();
                            textureIndex = mdbReader.ReadUInt16();
                            meshModel.MdbTriangles.Add(new(p0, p1, p2, textureIndex));
                        }
                        catch
                        {
                            throw new Exception("Skipped\nUnable to read triangle number " + j + " of model number " + i + ".\n");
                        }
                    }
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
                    mdbMesh.MeshModels.Add(meshModel);
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
                    var mat = new MdbMaterial();
                    mat.TextureName = new string(mdbReader.ReadChars(strlength));
                    //generate material name from texture name
                    mat.MaterialName = Path.GetFileNameWithoutExtension
                        (string.Join("_", mat.TextureName.Split(separator)));
                    //add to current mat
                    currentMdbMaterials.Add(mat);
                    //add to final mat without duplicating
                    if (!finalMdbMaterials.Exists(m => m.MaterialName.Equals(mat.MaterialName, StringComparison.OrdinalIgnoreCase)))
                        finalMdbMaterials.Add(mat);
                    //skip 72 bytes (material data)
                    mdbReader.BaseStream.Seek(72, SeekOrigin.Current);
                }
                catch
                {
                    throw new Exception("Skipped\nUnable to read material " + i + ".\n");
                }
            }
            ReorganizeTextureIndex(mdbMesh);
            currentMdbMaterials.Clear();
            mdbMeshes.Add(mdbMesh);
        }

        private void ReorganizeTextureIndex(MdbMesh mdbMesh)
        {
            for (int i = 0; i < mdbMesh.MeshModels.Count; i++)
            {
                var meshModel = mdbMesh.MeshModels[i];
                for (int j = 0; j < meshModel.MdbTriangles.Count; j++)
                {
                    var mdbTriangle = meshModel.MdbTriangles[j];
                    if(mdbTriangle.TextureIndex < currentMdbMaterials.Count)
                    {
                        var mat = currentMdbMaterials[mdbTriangle.TextureIndex];
                        for (ushort k = 0; k < finalMdbMaterials.Count; k++)
                        {
                            if(mat.TextureName == finalMdbMaterials[k].TextureName)
                            {
                                mdbTriangle.TextureIndex = k;
                                break;
                            }
                        }
                    }
                }
            }
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

        private void WriteGlb(string glbPath, IProgress<string> logs)
        {
            var glbScene = new SceneBuilder();
            var glbMaterials = new List<MaterialBuilder>();
            for (int i = 0; i < finalMdbMaterials.Count; i++)
            {
                var finalMdbMaterial = finalMdbMaterials[i];
                var glbMaterial = new MaterialBuilder(finalMdbMaterial.MaterialName)
                    .WithMetallicRoughness(0, 1f);

                glbMaterial.Extras = new JsonObject
                {
                    ["TextureName"] = Path.GetFileNameWithoutExtension(finalMdbMaterial.TextureName)
                };

                try
                {
                    var pngBytes = ConvertDdsToPngBytes(Form1.settings.TextureDirectory + Path.ChangeExtension(finalMdbMaterial.TextureName, "dds"));
                    var imageBuilder = ImageBuilder.From(pngBytes, Path.GetFileNameWithoutExtension(finalMdbMaterial.TextureName));
                    glbMaterial.WithChannelImage(KnownChannel.BaseColor, imageBuilder);
                }
                catch (Exception ex)
                {
                    logs.Report("\n" + ex.Message);
                }
                glbMaterials.Add(glbMaterial);
            }
            for (int i = 0; i < mdbMeshes.Count; i++)
            {
                var mdbMesh = mdbMeshes[i];
                for (int j = 0; j < mdbMesh.MeshModels.Count; j++)
                {
                    var meshModel = mdbMesh.MeshModels[j];
                    var finalGroupName = $"{mdbMesh.GroupName}_{j}";
                    var glbNode = new NodeBuilder(finalGroupName);
                    var glbMesh = new MeshBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>(finalGroupName);
                    for (int k = 0; k < meshModel.MdbTriangles.Count; k++)
                    {
                        var mdbTriangle = meshModel.MdbTriangles[k];
                        var v0 = meshModel.MdbVertices[mdbTriangle.P0];
                        var v1 = meshModel.MdbVertices[mdbTriangle.P1];
                        var v2 = meshModel.MdbVertices[mdbTriangle.P2];
                        var pos0 = new Vector3(v0.X, v0.Z, -v0.Y);
                        var pos1 = new Vector3(v1.X, v1.Z, -v1.Y);
                        var pos2 = new Vector3(v2.X, v2.Z, -v2.Y);
                        //var n0 = NormalFromAngles(v0.NX, v0.NY);
                        //var n1 = NormalFromAngles(v1.NX, v1.NY);
                        //var n2 = NormalFromAngles(v2.NX, v2.NY);
                        var n0 = Vector3.Normalize(new Vector3((float)-Math.Sin(v0.NX), (float)Math.Sin(v0.NY), (float)-Math.Cos(v0.NX)));
                        var n1 = Vector3.Normalize(new Vector3((float)-Math.Sin(v1.NX), (float)Math.Sin(v1.NY), (float)-Math.Cos(v1.NX)));
                        var n2 = Vector3.Normalize(new Vector3((float)-Math.Sin(v2.NX), (float)Math.Sin(v2.NY), (float)-Math.Cos(v2.NX)));
                        var glbPrim = glbMesh.UsePrimitive(glbMaterials[mdbTriangle.TextureIndex]);
                        var p0 = new VertexBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>
                            (new(pos0, n0), new VertexColor1Texture1(new(v0.R / 255f, v0.G / 255f, v0.B / 255f, 1f), new(v0.U, -v0.V)));
                        var p1 = new VertexBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>
                            (new(pos1, n1), new VertexColor1Texture1(new(v1.R / 255f, v1.G / 255f, v1.B / 255f, 1f), new(v1.U, -v1.V)));
                        var p2 = new VertexBuilder<VertexPositionNormal, VertexColor1Texture1, VertexEmpty>
                            (new(pos2, n2), new VertexColor1Texture1(new(v2.R / 255f, v2.G / 255f, v2.B / 255f, 1f), new(v2.U, -v2.V)));
                        glbPrim.AddTriangle(p2, p1, p0);
                    }
                    glbScene.AddNode(glbNode);
                    glbScene.AddRigidMesh(glbMesh, glbNode);
                }
            }
            glbScene.ToGltf2().SaveGLB(glbPath);
        }

        private void WriteMdb(BinaryWriter mdbWriter, IEnumerable<InstanceBuilder> instances, IProgress<string> logs)
        {
            var instanceIndex = 0;
            mdbWriter.Write(0);
            mdbWriter.Write(0);
            mdbWriter.Write(0);
            mdbWriter.Write(instances.Count());
            foreach(var instance in instances)
            {
                var mesh = instance.Content.GetGeometryAsset();
                var vCount = 0;
                mdbWriter.Write(0);
                mdbWriter.Write(instanceIndex);
                mdbWriter.Write(0);
                if (mesh != null)
                {
                    foreach(var prim in mesh.Primitives)
                    {
                        for (int i = 0; i < prim.Vertices.Count; i++)
                        {
                            mdbWriter.Write(32);
                            var v = prim.Vertices[i];
                            var vGeom = v.GetGeometry();
                            var vMat = v.GetMaterial();
                            var vPos = vGeom.GetPosition();
                            var vTexPos = vMat.GetTexCoord(0);
                            var vColor = vMat.GetColor(0);
                            mdbWriter.Write(vPos.X);
                            mdbWriter.Write(-vPos.Z);
                            mdbWriter.Write(vPos.Y);
                            mdbWriter.Write(vTexPos.X);
                            mdbWriter.Write(-vTexPos.Y);
                            if(vGeom.TryGetNormal(out var vNorm))
                            {
                                if (vNorm.Y < -1)
                                    vNorm.Y = -1;
                                else if (vNorm.Y > 1)
                                    vNorm.Y = 1;
                                if (vNorm.Z < -1)
                                    vNorm.Z = -1;
                                else if (vNorm.Z > 1)
                                    vNorm.Z = 1;
                                if (vNorm.X <= 0)
                                    mdbWriter.Write((float)Math.Acos(-vNorm.Z));
                                else
                                    mdbWriter.Write((float)-Math.Acos(-vNorm.Z));
                                mdbWriter.Write((float)Math.Asin(vNorm.Y));
                            }
                            else
                            {
                                mdbWriter.Write(0);
                                mdbWriter.Write(0);
                            }
                            mdbWriter.Write((byte)(vColor.X * 255));
                            mdbWriter.Write((byte)(vColor.Y * 255));
                            mdbWriter.Write((byte)(vColor.Z * 255));
                            mdbWriter.Write((byte)(vColor.W * 255));
                            vCount += 1;
                        }
                    }
                }
                instanceIndex += 1;
            }
        }

        private void ClearData()
        {
            for (int i = 0; i < mdbMeshes.Count; i++)
            {
                var mdbMesh = mdbMeshes[i];
                for (int j = 0; j < mdbMesh.MeshModels.Count; j++)
                {
                    var meshModel = mdbMesh.MeshModels[j];
                    meshModel.MdbVertices.Clear();
                    meshModel.MdbTriangles.Clear();
                }
                mdbMesh.MeshModels.Clear();
            }
            mdbMeshes.Clear();
            currentMdbMaterials.Clear();
            finalMdbMaterials.Clear();
        }
    }
}
