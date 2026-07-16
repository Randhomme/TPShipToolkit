using Pfim;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using StbImageWriteSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TPShipToolkit.MdbData.Classes;
using TPShipToolkit.Structs;
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
            for (int i = 0; i < glbs.Length; i++)
            {
                var glbPath = glbs[i];
                try
                {
                    var watch = new System.Diagnostics.Stopwatch();
                    logs.Report("Reading " + glbPath + " ... ");
                    watch.Start();
                    var glbScene = SceneBuilder.LoadDefaultScene(glbPath);
                    var instances = new List<InstanceBuilder>();
                    foreach (var instance in glbScene.Instances)
                    {
                        instances.Add(instance);
                    }
                    instances.Sort((x, y) => NaturalStringComparer.CompareNatural(x.Name, y.Name));
                    var groupedInstances = instances.GroupBy((i) => RealGroupName(i.Name));
                    watch.Stop();
                    logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                    foreach (var group in groupedInstances)
                    {
                        var mdbPath = Path.Combine(mdbFolderPath, group.Key + ".mdb");
                        logs.Report("\n---- " + group.Key + ".mdb ----\n");
                        using (var mdbWriter = new BinaryWriter(File.Open(mdbPath, FileMode.Create)))
                        {
                            WriteMdb(mdbWriter, group, glbScene.Materials, logs, watch);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logs.Report(ex.Message);
                }
                finally
                {
                    progress.Report(i + 1);
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

        private void WriteMdb(BinaryWriter mdbWriter, IEnumerable<InstanceBuilder> instances, IEnumerable<MaterialBuilder> materials, IProgress<string> logs, System.Diagnostics.Stopwatch watch)
        {
            int instanceIndex = 0, ctCount = 0;
            float minX = 0, minY = 0, minZ = 0, maxX = 0, maxY = 0, maxZ = 0;
            mdbWriter.Write(0);
            mdbWriter.Write(0);
            mdbWriter.Write(0);
            mdbWriter.Write(instances.Count());
            foreach(var instance in instances)
            {
                var mesh = instance.Content.GetGeometryAsset();
                mdbWriter.Write(0);
                mdbWriter.Write(instanceIndex);
                if (mesh != null)
                {
                    logs.Report($"Writing {instance.Name} vertices ... ");
                    watch.Restart();
                    if (instanceIndex == 0)
                    {
                        WriteVerticesInstance0ToMdb(mdbWriter, mesh, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                        watch.Stop();
                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                        logs.Report($"Writing {instance.Name} triangles ... ");
                        watch.Restart();
                        WriteTrianglesInstance0ToMdb(mdbWriter, mesh, materials, ref ctCount);
                        watch.Stop();
                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                    }
                    else
                    {
                        WriteVerticesToMdb(mdbWriter, mesh);
                        watch.Stop();
                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                        logs.Report($"Writing {instance.Name} triangles ... ");
                        watch.Restart();
                        WriteTrianglesToMdb(mdbWriter, mesh, materials);
                        watch.Stop();
                        logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
                    }
                    mdbWriter.Write(0); //potential animation count (or whatever this is)
                }
                instanceIndex += 1;
            }
            logs.Report($"Writing textures ... ");
            watch.Restart();
            WriteTexturesToMdb(mdbWriter, materials);
            watch.Stop();
            logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
            mdbWriter.Write(0); //bones count, maybe one day
            logs.Report("Writing bounding values ... ");
            watch.Restart();
            var posx = (minX + maxX) / 2;
            var posy = (minY + maxY) / 2;
            var posz = (minZ + maxZ) / 2;
            var lenx = maxX - minX;
            var leny = maxY - minY;
            var lenz = maxZ - minZ;
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
            watch.Stop();
            logs.Report("Done in " + TimeSpanFormat.Get(watch.Elapsed) + "\n");
        }

        private void WriteVerticesToMdb(BinaryWriter mdbWriter, IMeshBuilder<MaterialBuilder> mesh)
        {
            var tempPos = mdbWriter.BaseStream.Position;
            int vCount = 0;
            mdbWriter.Write(0); // vCount
            foreach (var prim in mesh.Primitives)
            {
                for (int i = 0; i < prim.Vertices.Count; i++)
                {
                    var v = prim.Vertices[i];
                    var vGeom = v.GetGeometry();
                    var vMat = v.GetMaterial();
                    var vPos = vGeom.GetPosition();
                    var vTexPos = vMat.GetTexCoord(0);
                    var vColor = vMat.GetColor(0);
                    mdbWriter.Write(32);
                    mdbWriter.Write(vPos.X);
                    mdbWriter.Write(-vPos.Z);
                    mdbWriter.Write(vPos.Y);
                    mdbWriter.Write(vTexPos.X);
                    mdbWriter.Write(-vTexPos.Y);
                    if (vGeom.TryGetNormal(out var vNorm))
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
                }
                vCount += prim.Vertices.Count;
            }
            mdbWriter.BaseStream.Seek(tempPos, SeekOrigin.Begin);
            mdbWriter.Write(vCount);
            mdbWriter.BaseStream.Seek(0, SeekOrigin.End);
        }

        private void WriteVerticesInstance0ToMdb(BinaryWriter mdbWriter, IMeshBuilder<MaterialBuilder> mesh, ref float minX, ref float minY, ref float minZ, ref float maxX, ref float maxY, ref float maxZ)
        {
            var tempPos = mdbWriter.BaseStream.Position;
            int vCount = 0;
            mdbWriter.Write(0); // vCount
            foreach (var prim in mesh.Primitives)
            {
                for (int i = 0; i < prim.Vertices.Count; i++)
                {
                    var v = prim.Vertices[i];
                    var vGeom = v.GetGeometry();
                    var vMat = v.GetMaterial();
                    var vPos = vGeom.GetPosition();
                    var vTexPos = vMat.GetTexCoord(0);
                    var vColor = vMat.GetColor(0);
                    if (vPos.X < minX)
                        minX = vPos.X;
                    if (vPos.Y < minY)
                        minY = vPos.Y;
                    if (vPos.Z < minZ)
                        minZ = vPos.Z;
                    if (vPos.X > maxX)
                        maxX = vPos.X;
                    if (vPos.Y > maxY)
                        maxY = vPos.Y;
                    if (vPos.Z > maxZ)
                        maxZ = vPos.Z;
                    mdbWriter.Write(32);
                    mdbWriter.Write(vPos.X);
                    mdbWriter.Write(-vPos.Z);
                    mdbWriter.Write(vPos.Y);
                    mdbWriter.Write(vTexPos.X);
                    mdbWriter.Write(-vTexPos.Y);
                    if (vGeom.TryGetNormal(out var vNorm))
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
                }
                vCount += prim.Vertices.Count;
            }
            mdbWriter.BaseStream.Seek(tempPos, SeekOrigin.Begin);
            mdbWriter.Write(vCount);
            mdbWriter.BaseStream.Seek(0, SeekOrigin.End);
        }

        private void WriteTrianglesToMdb(BinaryWriter mdbWriter, IMeshBuilder<MaterialBuilder> mesh, IEnumerable<MaterialBuilder> materials)
        {
            int tCount = 0;
            var tempPos = mdbWriter.BaseStream.Position;
            mdbWriter.Write(0); // tCount
            foreach (var prim in mesh.Primitives)
            {
                ushort matIndex = 0, j = 0;
                foreach (var mat in materials)
                {
                    if (prim.Material == mat)
                    {
                        matIndex = j;
                        break;
                    }
                    j += 1;
                }
                for (int i = 0; i < prim.Triangles.Count; i++)
                {
                    var t = prim.Triangles[i];
                    mdbWriter.Write(8);
                    mdbWriter.Write((ushort)(t.C + tCount));
                    mdbWriter.Write((ushort)(t.B + tCount));
                    mdbWriter.Write((ushort)(t.A + tCount));
                    mdbWriter.Write(matIndex);
                }
                tCount += prim.Triangles.Count;
            }
            mdbWriter.BaseStream.Seek(tempPos, SeekOrigin.Begin);
            mdbWriter.Write(tCount);
            mdbWriter.BaseStream.Seek(0, SeekOrigin.End);
        }

        private void WriteTrianglesInstance0ToMdb(BinaryWriter mdbWriter, IMeshBuilder<MaterialBuilder> mesh, IEnumerable<MaterialBuilder> materials, ref int ctCount)
        {
            int tCount = 0;
            var tempPos = mdbWriter.BaseStream.Position;
            mdbWriter.Write(0); // tCount
            foreach (var prim in mesh.Primitives)
            {
                ushort matIndex = 0, j = 0;
                foreach (var mat in materials)
                {
                    if (prim.Material == mat)
                    {
                        matIndex = j;
                        break;
                    }
                    j += 1;
                }
                for (int i = 0; i < prim.Triangles.Count; i++)
                {
                    var t = prim.Triangles[i];
                    mdbWriter.Write(8);
                    mdbWriter.Write((ushort)(t.C + tCount));
                    mdbWriter.Write((ushort)(t.B + tCount));
                    mdbWriter.Write((ushort)(t.A + tCount));
                    mdbWriter.Write(matIndex);
                }
                tCount += prim.Triangles.Count;
            }
            ctCount = tCount;
            mdbWriter.BaseStream.Seek(tempPos, SeekOrigin.Begin);
            mdbWriter.Write(tCount);
            mdbWriter.BaseStream.Seek(0, SeekOrigin.End);
        }

        private void WriteTexturesToMdb(BinaryWriter mdbWriter, IEnumerable<MaterialBuilder> materials)
        {
            mdbWriter.Write(materials.Count());
            foreach (var material in materials)
            {
                string texture = "NULL";
                if (material.Extras is JsonObject obj)
                {
                    if (obj.TryGetPropertyValue("TextureName", out var value))
                    {
                        texture = Path.GetFileNameWithoutExtension(value!.GetValue<string>()) + ".tga";
                    }
                    else
                    {
                        var channel = material.GetChannel(KnownChannel.BaseColor);
                        if (channel != null)
                        {
                            texture = Path.GetFileNameWithoutExtension(channel.Texture.PrimaryImage.Name) + ".tga";
                        }
                    }
                }
                else
                {
                    var channel = material.GetChannel(KnownChannel.BaseColor);
                    if (channel != null)
                    {
                        texture = Path.GetFileNameWithoutExtension(channel.Texture.PrimaryImage.Name) + ".tga";
                    }
                }
                int taillebloc = 76 + texture.Length;
                mdbWriter.Write(taillebloc);
                mdbWriter.Write(texture.Length);
                mdbWriter.Write(Encoding.Default.GetBytes(texture));
                mdbWriter.Write(1.0f);
                mdbWriter.Write(1.0f);
                mdbWriter.Write(1.0f);
                mdbWriter.Write(1.0f);
                mdbWriter.Write(1.0f);
                mdbWriter.Write(1.0f);
                mdbWriter.Write(1.0f);
                mdbWriter.Write(1.0f);
                mdbWriter.Write(0.0f);
                mdbWriter.Write(0.0f);
                mdbWriter.Write(0.0f);
                mdbWriter.Write(1.0f);
                mdbWriter.Write(0.0f);
                mdbWriter.Write(0.0f);
                mdbWriter.Write(0.0f);
                mdbWriter.Write(1.0f);
                mdbWriter.Write(0.0f);
                mdbWriter.Write(0.0f);
            }
        }

        private List<int> GetPointsFromCBoxGroup(List<(string matName, List<ObjTriangle> tris)> matGroups)
        {
            List<int> points = new List<int>();
            for (int i = 0; i < matGroups.Count; i++)
            {
                var matGroup = matGroups[i];
                for (int j = 0; j < matGroup.tris.Count; j++)
                {
                    var tri = matGroup.tris[j];
                    bool hasFoundP0, hasFoundP1, hasFoundP2;
                    hasFoundP0 = hasFoundP1 = hasFoundP2 = false;
                    for (int k = 0; k < points.Count; k++)
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
            //var points = GetPointsFromCBoxGroup(triangles);
            AllPca(box, points, out Vector3 mean);
            if (box.Level < 5)
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
                        for (int j = 0; j < triGroup.tris.Count; j++)
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
                else if (maxLength == box.Length.Y)
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

        private void AllPca(CollisionBox box, List<Vector3> points, out Vector3 mean)
        {
            for (int i = 0; i < points.Count; i++)
            {
                try
                {
                    var p = points[i];
                    box.Position.X += p.X;
                    box.Position.Y += p.Y;
                    box.Position.Z += p.Z;
                }
                catch { }
            }
            if (points.Count != 0)
            {
                box.Position /= points.Count;
            }
            mean = box.Position;
            Vector3 covMatRow1 = Vector3.Zero, covMatRow2 = covMatRow1, covMatRow3 = covMatRow1;
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                try
                {
                    var p = points[i];
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

            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                try
                {
                    var p = points[i];
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
