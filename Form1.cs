using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using TPShipToolkit.Dialogs;
using TPShipToolkit.Enums;
using TPShipToolkit.MdbData;
using TPShipToolkit.MsbData;
using TPShipToolkit.Settings;

namespace TPShipToolkit
{
    public partial class Form1 : Form
    {
        private AppSettings settings;
        private MsbTool msbTool = new MsbTool();
        public Form1()
        {
            //To get exceptions message in English
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            InitializeComponent();
            LoadSettings();
            comboBox1.SelectedIndex = 0;
        }

        //Load some settings
        private void LoadSettings()
        {
            settings = new AppSettings();
            settings = settings.Load();
            radioButton1.Checked = settings.XMdbTo1Obj;
            radioButton2.Checked = !settings.XMdbTo1Obj;
            radioButton3.Checked = settings.ObjToXMdb;
            radioButton4.Checked = !settings.ObjToXMdb;
            checkBox1.Checked = settings.ExportCBox;
            checkBox2.Checked = settings.AutoCBox;
        }

        //Help
        private void viewHelpToolStripMenuItem_Click(object sender, System.EventArgs e)
        {
            var helpWindow = new HelpWindow(this);
            helpWindow.Show();
        }

        //Open mdb to obj button
        private void button1_Click(object sender, System.EventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                DefaultExt = "mdb",
                Filter = "Mesh file (*.mdb)|*.mdb",
                Multiselect = true,
                Title = "Select one or multiple mdb to convert.",
                InitialDirectory = settings.OpenMdbDirectory
            };
            if (ofd.ShowDialog()==DialogResult.OK)
            {
                settings.OpenMdbDirectory = Path.GetDirectoryName(ofd.FileName);
                if(radioButton1.Checked)
                {
                    var sfd = new SaveFileDialog
                    {
                        DefaultExt = "obj",
                        Filter = "Obj file (*.obj)|*.obj",
                        InitialDirectory = settings.SaveObjDirectory
                    };
                    if (sfd.ShowDialog()==DialogResult.OK)
                    {
                        settings.SaveObjDirectory = Path.GetDirectoryName(sfd.FileName);
                        var progressDialog = new ProgressDialog("Converting mdb to obj ...", ofd.FileNames.Length);
                        var mdbTool = new MdbTool();
                        Task.Run(() =>
                        {
                            mdbTool.XMdbTo1Obj(ofd.FileNames, sfd.FileName, checkBox1.Checked, progressDialog.Progress, progressDialog.Logs);
                            progressDialog.Invoke(new MethodInvoker(progressDialog.EnableClose));
                        });
                        progressDialog.ShowDialog();
                    }
                }
                else if(radioButton2.Checked)
                {
                    var fbd = new FolderBrowserDialog
                    {
                        Description = "Select the folder to export your mdb(s) to obj(s)."
                    };
                    if (fbd.ShowDialog()==DialogResult.OK)
                    {
                        var progressDialog = new ProgressDialog("Converting mdb to obj ...", ofd.FileNames.Length);
                        var mdbTool = new MdbTool();
                        Task.Run(() =>
                        {
                            mdbTool.XMdbToXObj(ofd.FileNames, fbd.SelectedPath, checkBox1.Checked, progressDialog.Progress, progressDialog.Logs);
                            progressDialog.Invoke(new MethodInvoker(progressDialog.EnableClose));
                        });
                        progressDialog.ShowDialog();
                    }
                }
            }
        }

        //Open obj to mdb button
        private void button2_Click(object sender, System.EventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                DefaultExt = "obj",
                Filter = "Object file (*.obj)|*.obj",
                Multiselect = true,
                RestoreDirectory = true,
                Title = "Select one or multiple obj to convert.",
                InitialDirectory = settings.OpenObjDirectory
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                settings.OpenObjDirectory = Path.GetDirectoryName(ofd.FileName);
                if (radioButton3.Checked)
                {
                    var fbd = new FolderBrowserDialog
                    {
                        Description = "Select the folder to export your obj(s) to mdb(s)."
                    };
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        var progressDialog = new ProgressDialog("Converting obj to mdb ...", ofd.FileNames.Length);
                        var objTool = new ObjTool();
                        Task.Run(() =>
                        {
                            objTool.ObjToXMdb(ofd.FileNames, fbd.SelectedPath, checkBox2.Checked, progressDialog.Progress, progressDialog.Logs);
                            progressDialog.Invoke(new MethodInvoker(progressDialog.EnableClose));
                        });
                        progressDialog.ShowDialog();
                    }
                }
                else if (radioButton4.Checked)
                {
                    var fbd = new FolderBrowserDialog
                    {
                        Description = "Select the folder to export your obj(s) to mdb(s)."
                    };
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        var progressDialog = new ProgressDialog("Converting obj to mdb ...", ofd.FileNames.Length);
                        var objTool = new ObjTool();
                        Task.Run(() =>
                        {
                            objTool.XObjToXMdb(ofd.FileNames, fbd.SelectedPath, checkBox2.Checked, progressDialog.Progress, progressDialog.Logs);
                            progressDialog.Invoke(new MethodInvoker(progressDialog.EnableClose));
                        });
                        progressDialog.ShowDialog();
                    }
                }
            }
        }

        //Save some settings when closing
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            settings.XMdbTo1Obj = radioButton1.Checked;
            settings.ObjToXMdb = radioButton3.Checked;
            settings.ExportCBox = checkBox1.Checked;
            settings.AutoCBox = checkBox2.Checked;
            settings.Save();
        }

        /// <summary>
        /// TreeView double click on check box fix. From <see href="https://stackoverflow.com/a/48061985"/>
        /// </summary>
        private class MyTreeView : TreeView
        {
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x0203 && this.CheckBoxes)
                {
                    var localPos = this.PointToClient(Cursor.Position);
                    var hitTestInfo = this.HitTest(localPos);
                    if (hitTestInfo.Location == TreeViewHitTestLocations.StateImage)
                    {
                        m.Msg = 0x0201;
                    }
                }
                base.WndProc(ref m);
            }
        }

        //Import msb
        private void button3_Click(object sender, System.EventArgs e)
        {
            var ofd = new OpenFileDialog
            {
                DefaultExt = "msb",
                Filter = "Mesh scene file (*.msb)|*.msb",
                Multiselect = true,
                Title = "Select a mesh scene file.",
                InitialDirectory = settings.OpenMsbDirectory
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                settings.OpenMsbDirectory = Path.GetDirectoryName(ofd.FileName);
                var progressDialog = new ProgressDialog("Import msb ...", ofd.FileNames.Length);
                Task.Run(() =>
                {
                    msbTool.Import(ofd.FileNames, treeView1, progressDialog.Progress, progressDialog.Logs);
                    progressDialog.Invoke(new MethodInvoker(progressDialog.EnableClose));
                });
                progressDialog.ShowDialog();
                comboBox1.SelectedIndex = (int)msbTool.GetMeshSceneType();
            }
        }

        //Export msb
        private void button4_Click(object sender, System.EventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                DefaultExt = "msb",
                Filter = "Mesh scene file (*.msb)|*.msb",
                InitialDirectory = settings.SaveMsbDirectory
            };
            if(sfd.ShowDialog()==DialogResult.OK)
            {
                settings.SaveMsbDirectory = Path.GetDirectoryName(sfd.FileName);
                var progressDialog = new ProgressDialog("Export msb ...", 1);
                Task.Run(() =>
                {
                    msbTool.Export(sfd.FileName, progressDialog.Logs);
                    progressDialog.Invoke(new MethodInvoker(progressDialog.EnableClose));
                    progressDialog.Progress.Report(1);
                });
                progressDialog.ShowDialog();
            }
        }

        //Check/Uncheck the node collection of a node
        private void treeView1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Checked)
            {
                TreeNodeCollection treeNodeCollection = e.Node.Nodes;
                for (int i = 0; i < treeNodeCollection.Count; i++)
                {
                    treeNodeCollection[i].Checked = true;
                }
            }
            else
            {
                TreeNodeCollection treeNodeCollection = e.Node.Nodes;
                for (int i = 0; i < treeNodeCollection.Count; i++)
                {
                    treeNodeCollection[i].Checked = false;
                }
            }
        }

        //Updates the property grid from the selected node in the tree view
        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            TreeNode parent = e.Node.Parent;
            if (parent != null)
            {
                if (e.Node.Index != 0)
                {
                    button5.Enabled = true;
                }
                else
                {
                    button5.Enabled = false;
                }
                if (e.Node.Index != parent.Nodes.Count - 1)
                {
                    button6.Enabled = true;
                }
                else
                {
                    button6.Enabled = false;
                }
                if (parent.Equals(treeView1.Nodes[0]))
                {
                    try
                    {
                        propertyGrid1.SelectedObject = msbTool.GetNodes()[e.Node.Index];
                    }
                    catch
                    {
                        propertyGrid1.SelectedObject = null;
                        button5.Enabled = false;
                        button6.Enabled = false;
                    }
                }
                else if (parent.Equals(treeView1.Nodes[1]))
                {
                    try
                    {
                        propertyGrid1.SelectedObject = msbTool.GetMeshes()[e.Node.Index];
                    }
                    catch
                    {
                        propertyGrid1.SelectedObject = null;
                        button5.Enabled = false;
                        button6.Enabled = false;
                    }
                }
                else if (parent.Equals(treeView1.Nodes[2]))
                {
                    try
                    {
                        propertyGrid1.SelectedObject = msbTool.GetBones()[e.Node.Index];
                    }
                    catch
                    {
                        propertyGrid1.SelectedObject = null;
                        button5.Enabled = false;
                        button6.Enabled = false;
                    }
                }
                else if (parent.Equals(treeView1.Nodes[3]))
                {
                    try
                    {
                        propertyGrid1.SelectedObject = msbTool.GetAnimations()[e.Node.Index];
                    }
                    catch
                    {
                        propertyGrid1.SelectedObject = null;
                        button5.Enabled = false;
                        button6.Enabled = false;
                    }
                }
            }
            else
            {
                propertyGrid1.SelectedObject = null;
                button5.Enabled = false;
                button6.Enabled = false;
            }
        }

        //Updates the mesh scene type
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            msbTool.SetMeshSceneType((MeshSceneType)comboBox1.SelectedIndex);
        }

        //Add element
        private void button10_Click(object sender, EventArgs e)
        {
            var addDialog = new AddMsbElementDialog(msbTool.GetElementsName());
            if (addDialog.ShowDialog() == DialogResult.OK)
            {
                //node
                if (addDialog.ElementType == 0)
                {
                    msbTool.AddNode(addDialog.ElementName);
                    treeView1.Nodes[0].Nodes.Add(addDialog.ElementName);
                }
                //mesh
                else if (addDialog.ElementType == 1)
                {
                    msbTool.AddMesh(addDialog.ElementName);
                    treeView1.Nodes[1].Nodes.Add(addDialog.ElementName);
                }
                //bone
                else if (addDialog.ElementType == 2)
                {
                    msbTool.AddBone(addDialog.ElementName);
                    treeView1.Nodes[2].Nodes.Add(addDialog.ElementName);
                }
                //animation
                else if (addDialog.ElementType == 3)
                {
                    msbTool.AddAnimation(addDialog.ElementName);
                    treeView1.Nodes[3].Nodes.Add(addDialog.ElementName);
                }
            }
        }

        //Select all
        private void button9_Click(object sender, EventArgs e)
        {
            treeView1.Nodes[0].Checked = true;
            treeView1.Nodes[1].Checked = true;
            treeView1.Nodes[2].Checked = true;
            treeView1.Nodes[3].Checked = true;
        }

        //Unselect all
        private void button8_Click(object sender, EventArgs e)
        {
            treeView1.Nodes[0].Checked = false;
            treeView1.Nodes[1].Checked = false;
            treeView1.Nodes[2].Checked = false;
            treeView1.Nodes[3].Checked = false;
        }

        //Remove selected
        private void button7_Click(object sender, EventArgs e)
        {
            TreeNodeCollection nodes = treeView1.Nodes[0].Nodes;
            TreeNodeCollection meshes = treeView1.Nodes[1].Nodes;
            TreeNodeCollection bones = treeView1.Nodes[2].Nodes;
            TreeNodeCollection animations = treeView1.Nodes[3].Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Checked)
                {
                    try
                    {
                        msbTool.GetNodes().RemoveAt(i);
                    }
                    finally 
                    {
                        msbTool.RemoveElementName(nodes[i].Text); //will never be out of range, we're safe
                        nodes[i].Remove(); //same
                        i--;
                    }
                }
            }
            for (int i = 0; i < meshes.Count; i++)
            {
                if (meshes[i].Checked)
                {
                    try
                    {
                        msbTool.GetMeshes().RemoveAt(i);
                    }
                    finally
                    {
                        msbTool.RemoveElementName(meshes[i].Text); //will never be out of range, we're safe
                        meshes[i].Remove(); //same
                        i--;
                    }
                }
            }
            for (int i = 0; i < bones.Count; i++)
            {
                if (bones[i].Checked)
                {
                    try
                    {
                        msbTool.GetBones().RemoveAt(i);
                    }
                    finally
                    {
                        msbTool.RemoveElementName(bones[i].Text); //will never be out of range, we're safe
                        bones[i].Remove(); //same
                        i--;
                    }
                }
            }
            for (int i = 0; i < animations.Count; i++)
            {
                if (animations[i].Checked)
                {
                    try
                    {
                        msbTool.GetAnimations().RemoveAt(i);
                    }
                    finally
                    {
                        msbTool.RemoveElementName(animations[i].Text); //will never be out of range, we're safe
                        animations[i].Remove(); //same
                        i--;
                    }
                }
            }
        }

        //Arrow down
        private void button6_Click(object sender, EventArgs e)
        {
            TreeNode selectedNode = treeView1.SelectedNode;
            var selectedNodeIndex = selectedNode.Index;
            var parentNodes = selectedNode.Parent.Nodes;
            if (selectedNode.Parent.Equals(treeView1.Nodes[0]))
            {
                (msbTool.GetNodes()[selectedNodeIndex], msbTool.GetNodes()[selectedNodeIndex + 1]) = (msbTool.GetNodes()[selectedNodeIndex + 1], msbTool.GetNodes()[selectedNodeIndex]);
                (msbTool.GetElementsName()[selectedNodeIndex], msbTool.GetElementsName()[selectedNodeIndex + 1]) = (msbTool.GetElementsName()[selectedNodeIndex + 1], msbTool.GetElementsName()[selectedNodeIndex]);
            }
            else if (selectedNode.Parent.Equals(treeView1.Nodes[1]))
            {
                (msbTool.GetMeshes()[selectedNodeIndex], msbTool.GetMeshes()[selectedNodeIndex + 1]) = (msbTool.GetMeshes()[selectedNodeIndex + 1], msbTool.GetMeshes()[selectedNodeIndex]);
                (msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count], msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + 1]) = (msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + 1], msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count]);
            }
            else if (selectedNode.Parent.Equals(treeView1.Nodes[2]))
            {
                (msbTool.GetBones()[selectedNodeIndex], msbTool.GetBones()[selectedNodeIndex + 1]) = (msbTool.GetBones()[selectedNodeIndex + 1], msbTool.GetBones()[selectedNodeIndex]);
                (msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count], msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count + 1]) = (msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count + 1], msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count]);
            }
            else if (selectedNode.Parent.Equals(treeView1.Nodes[3]))
            {
                (msbTool.GetAnimations()[selectedNodeIndex], msbTool.GetAnimations()[selectedNodeIndex + 1]) = (msbTool.GetAnimations()[selectedNodeIndex + 1], msbTool.GetAnimations()[selectedNodeIndex]);
                (msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count + msbTool.GetBones().Count], msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count + msbTool.GetBones().Count + 1]) = (msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count + msbTool.GetBones().Count + 1], msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count + msbTool.GetBones().Count]);
            }
            (parentNodes[selectedNodeIndex].Text, parentNodes[selectedNodeIndex + 1].Text) = (parentNodes[selectedNodeIndex + 1].Text, parentNodes[selectedNodeIndex].Text);
            (parentNodes[selectedNodeIndex].Checked, parentNodes[selectedNodeIndex + 1].Checked) = (parentNodes[selectedNodeIndex + 1].Checked, parentNodes[selectedNodeIndex].Checked);
            treeView1.SelectedNode = parentNodes[selectedNodeIndex + 1];
            treeView1.Focus();
        }

        //Arrow up
        private void button5_Click(object sender, EventArgs e)
        {
            TreeNode selectedNode = treeView1.SelectedNode;
            var selectedNodeIndex = selectedNode.Index;
            var parentNodes = selectedNode.Parent.Nodes;
            if (selectedNode.Parent.Equals(treeView1.Nodes[0]))
            {
                (msbTool.GetNodes()[selectedNodeIndex], msbTool.GetNodes()[selectedNodeIndex - 1]) = (msbTool.GetNodes()[selectedNodeIndex - 1], msbTool.GetNodes()[selectedNodeIndex]);
                (msbTool.GetElementsName()[selectedNodeIndex], msbTool.GetElementsName()[selectedNodeIndex - 1]) = (msbTool.GetElementsName()[selectedNodeIndex - 1], msbTool.GetElementsName()[selectedNodeIndex]);
            }
            else if (selectedNode.Parent.Equals(treeView1.Nodes[1]))
            {
                (msbTool.GetMeshes()[selectedNodeIndex], msbTool.GetMeshes()[selectedNodeIndex - 1]) = (msbTool.GetMeshes()[selectedNodeIndex - 1], msbTool.GetMeshes()[selectedNodeIndex]);
                (msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count], msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count - 1]) = (msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count - 1], msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count]);
            }
            else if (selectedNode.Parent.Equals(treeView1.Nodes[2]))
            {
                (msbTool.GetBones()[selectedNodeIndex], msbTool.GetBones()[selectedNodeIndex - 1]) = (msbTool.GetBones()[selectedNodeIndex - 1], msbTool.GetBones()[selectedNodeIndex]);
                (msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count], msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count - 1]) = (msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count - 1], msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count]);
            }
            else if (selectedNode.Parent.Equals(treeView1.Nodes[3]))
            {
                (msbTool.GetAnimations()[selectedNodeIndex], msbTool.GetAnimations()[selectedNodeIndex - 1]) = (msbTool.GetAnimations()[selectedNodeIndex - 1], msbTool.GetAnimations()[selectedNodeIndex]);
                (msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count + msbTool.GetBones().Count], msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count + msbTool.GetBones().Count - 1]) = (msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count + msbTool.GetBones().Count - 1], msbTool.GetElementsName()[selectedNodeIndex + msbTool.GetNodes().Count + msbTool.GetMeshes().Count + msbTool.GetBones().Count]);
            }
            (parentNodes[selectedNodeIndex].Text, parentNodes[selectedNodeIndex - 1].Text) = (parentNodes[selectedNodeIndex - 1].Text, parentNodes[selectedNodeIndex].Text);
            (parentNodes[selectedNodeIndex].Checked, parentNodes[selectedNodeIndex - 1].Checked) = (parentNodes[selectedNodeIndex - 1].Checked, parentNodes[selectedNodeIndex].Checked);
            treeView1.SelectedNode = parentNodes[selectedNodeIndex - 1];
            treeView1.Focus();
        }
    }
}