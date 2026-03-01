using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator.GUI
{
    public partial class MapPhysicsEditor : Form
    {
        private const int DEFAULT_walkForce = 140000;
        private const int DEFAULT_walkSpeed = 125;
        private const int DEFAULT_walkDrag = 80000;

        private const int DEFAULT_slipForce = 60000;
        private const int DEFAULT_slipSpeed = 120;
        private const int DEFAULT_floatDrag1 = 100000;
        private const int DEFAULT_floatDrag2 = 10000;

        private const decimal DEFAULT_floatCoefficient = 0.01M;
        private const int DEFAULT_swimForce = 120000;
        private const int DEFAULT_swimSpeed = 140;
        private const int DEFAULT_flyForce = 120000;

        private const int DEFAULT_flySpeed = 200;
        private const int DEFAULT_gravityAcc = 2000;
        private const int DEFAULT_fallSpeed = 670;
        private const int DEFAULT_jumpSpeed = 555;

        private const decimal DEFAULT_maxFriction = 2M;
        private const decimal DEFAULT_minFriction = 0.05M;
        private const decimal DEFAULT_swimSpeedDec = 0.9M;
        private const decimal DEFAULT_flyJumpDec = 0.35M;

        private const string 
            WZ_FILE_NAME = "map",
            WZ_FILE_IMAGE = "Physics.img";
        private WzImage mapPhysicsImage;

        /// <summary>
        /// Constructor
        /// </summary>
        public MapPhysicsEditor()
        {
            InitializeComponent();

            // Events
            Load += MapPhysicsEditor_Load;
        }

        /// <summary>
        /// On loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MapPhysicsEditor_Load(object sender, EventArgs e)
        {
            if (Program.IsPreBBDataWzFormat) {
                MessageBox.Show(string.Format("Editing of {0}/{1} is not available in beta MapleStory.", WZ_FILE_NAME, WZ_FILE_IMAGE));
                Close();
                return;
            }

            // Check if we have a valid data source
            if (Program.DataSource == null && Program.WzManager == null)
            {
                MessageBox.Show("No data source available. Please load WZ files first.");
                Close();
                return;
            }

            mapPhysicsImage = Program.FindImage(WZ_FILE_NAME, WZ_FILE_IMAGE);
            if (!LoadWzValues())
            {
                Close();
                return;
            }
        }

        #region Wz
        /// <summary>
        /// Load from Map.wz/Physics.img
        /// </summary>
        /// <returns></returns>
        private bool LoadWzValues()
        {
            bool bLoadedSuccessfully = false;
            try
            {
                if (mapPhysicsImage != null)
                {
                    numericUpDown_walkForce.Value = (decimal)mapPhysicsImage["walkForce"].GetDouble();
                    numericUpDown_walkSpeed.Value = (decimal)mapPhysicsImage["walkSpeed"].GetDouble();
                    numericUpDown_walkDrag.Value = (decimal)mapPhysicsImage["walkDrag"].GetDouble();
                    numericUpDown_slipForce.Value = (decimal)mapPhysicsImage["slipForce"].GetDouble();
                    numericUpDown_slipSpeed.Value = (decimal)mapPhysicsImage["slipSpeed"].GetDouble();
                    numericUpDown_floatDrag1.Value = (decimal)mapPhysicsImage["floatDrag1"].GetDouble();
                    numericUpDown_floatDrag2.Value = (decimal)mapPhysicsImage["floatDrag2"].GetDouble();
                    numericUpDown_floatCoefficient.Value = (decimal)mapPhysicsImage["floatCoefficient"].GetDouble();
                    numericUpDown_swimForce.Value = (decimal)mapPhysicsImage["swimForce"].GetDouble();
                    numericUpDown_swimSpeed.Value = (decimal)mapPhysicsImage["swimSpeed"].GetDouble();
                    numericUpDown_flyForce.Value = (decimal)mapPhysicsImage["flyForce"].GetDouble();
                    numericUpDown_flySpeed.Value = (decimal)mapPhysicsImage["flySpeed"].GetDouble();
                    numericUpDown_gravityAcc.Value = (decimal)mapPhysicsImage["gravityAcc"].GetDouble();
                    numericUpDown_fallSpeed.Value = (decimal)mapPhysicsImage["fallSpeed"].GetDouble();
                    numericUpDown_jumpSpeed.Value = (decimal)mapPhysicsImage["jumpSpeed"].GetDouble();
                    numericUpDown_maxFriction.Value = (decimal)mapPhysicsImage["maxFriction"].GetDouble();
                    numericUpDown_minFriction.Value = (decimal)mapPhysicsImage["minFriction"].GetDouble();
                    numericUpDown_swimSpeedDec.Value = (decimal)mapPhysicsImage["swimSpeedDec"].GetDouble();
                    numericUpDown_flyJumpDec.Value = (decimal)mapPhysicsImage["flyJumpDec"].GetDouble();

                    bLoadedSuccessfully = true;
                }
            }
            catch (Exception exp)
            {
                MessageBox.Show("Map.wz is not loaded, or Map.wz/Physics.img do not exist.\r\n" + exp.ToString());
                return false;
            }

            if (!bLoadedSuccessfully)
            {
                MessageBox.Show("Map.wz is not loaded, or Map.wz/Physics.img do not exist.");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Save to Map.wz/Physics.img
        /// </summary>
        /// <returns></returns>
        private bool Save()
        {
            // Save 
            string errorMessage = null;
            try
            {
                if (mapPhysicsImage != null)
                {
                    mapPhysicsImage["walkForce"].SetValue(numericUpDown_walkForce.Value);
                    mapPhysicsImage["walkSpeed"].SetValue(numericUpDown_walkSpeed.Value);
                    mapPhysicsImage["walkDrag"].SetValue(numericUpDown_walkDrag.Value);
                    mapPhysicsImage["slipForce"].SetValue(numericUpDown_slipForce.Value);
                    mapPhysicsImage["slipSpeed"].SetValue(numericUpDown_slipSpeed.Value);
                    mapPhysicsImage["floatDrag1"].SetValue(numericUpDown_floatDrag1.Value);
                    mapPhysicsImage["floatDrag2"].SetValue(numericUpDown_floatDrag2.Value);
                    mapPhysicsImage["floatCoefficient"].SetValue(numericUpDown_floatCoefficient.Value);
                    mapPhysicsImage["swimForce"].SetValue(numericUpDown_swimForce.Value);
                    mapPhysicsImage["swimSpeed"].SetValue(numericUpDown_swimSpeed.Value);
                    mapPhysicsImage["flyForce"].SetValue(numericUpDown_flyForce.Value);
                    mapPhysicsImage["flySpeed"].SetValue(numericUpDown_flySpeed.Value);
                    mapPhysicsImage["gravityAcc"].SetValue(numericUpDown_gravityAcc.Value);
                    mapPhysicsImage["fallSpeed"].SetValue(numericUpDown_fallSpeed.Value);
                    mapPhysicsImage["jumpSpeed"].SetValue(numericUpDown_jumpSpeed.Value);
                    mapPhysicsImage["maxFriction"].SetValue(numericUpDown_maxFriction.Value);
                    mapPhysicsImage["minFriction"].SetValue(numericUpDown_minFriction.Value);
                    mapPhysicsImage["swimSpeedDec"].SetValue(numericUpDown_swimSpeedDec.Value);
                    mapPhysicsImage["flyJumpDec"].SetValue(numericUpDown_flyJumpDec.Value);

                    Program.MarkImageUpdated(WZ_FILE_NAME, mapPhysicsImage); // flag as changed
                }
                else
                {
                    errorMessage = "Map.wz is not loaded, or Map.wz/Physics.img do not exist.";
                }
            }
            catch (Exception exp)
            {
                errorMessage = exp.Message;
            }
            finally
            {
                button_save.Enabled = true;
            }


            if (errorMessage == null)
            {
                MessageBox.Show("Updated. Please save the changes to .wz via File -> 'Repack WZ Files'.", "Success");
                Close(); // close window directly
                return true;
            }
            else
            {
                label_error.Text = errorMessage;
            }
            return false;
        }
        #endregion

        #region UI
        /// <summary>
        /// Save button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_save_Click(object sender, EventArgs e)
        {
            // Disable save button until complete
            button_save.Enabled = false;

            Save();
        }

        /// <summary>
        /// Reset to pre-bb map physics values
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_resetPreBB_Click(object sender, EventArgs e)
        {
            /// <summary>
            /// Map.wz/Physics.img
            /// <imgdir name="Physics.img">
            /// <double name = "walkForce" value="140000"/>
            /// <double name = "walkSpeed" value="125"/>
            /// <double name = "walkDrag" value="80000"/>
            /// 
            /// <double name = "slipForce" value="60000"/>
            /// <double name = "slipSpeed" value="120"/>
            /// <double name = "floatDrag1" value="100000"/>
            /// <double name = "floatDrag2" value="10000"/>
            /// 
            /// <double name = "floatCoefficient" value="0.01"/>
            /// <double name = "swimForce" value="120000"/>
            /// <double name = "swimSpeed" value="140"/>
            /// <double name = "flyForce" value="120000"/>
            /// 
            /// <double name = "flySpeed" value="200"/>
            /// <double name = "gravityAcc" value="2000"/>
            /// <double name = "fallSpeed" value="670"/>
            /// <double name = "jumpSpeed" value="555"/>
            /// 
            /// <double name = "maxFriction" value="2"/>
            /// <double name = "minFriction" value="0.05"/>
            /// <double name = "swimSpeedDec" value="0.9"/>
            /// <float name = "flyJumpDec" value="0.35"/>
            /// </imgdir>
            /// </summary>

            numericUpDown_walkForce.Value = DEFAULT_walkForce;
            numericUpDown_walkSpeed.Value = DEFAULT_walkSpeed;
            numericUpDown_walkDrag.Value = DEFAULT_walkDrag;

            numericUpDown_slipForce.Value = DEFAULT_slipForce;
            numericUpDown_slipSpeed.Value = DEFAULT_slipSpeed;
            numericUpDown_floatDrag1.Value = DEFAULT_floatDrag1;
            numericUpDown_floatDrag2.Value = DEFAULT_floatDrag2;

            numericUpDown_floatCoefficient.Value = DEFAULT_floatCoefficient;
            numericUpDown_swimForce.Value = DEFAULT_swimForce;
            numericUpDown_swimSpeed.Value = DEFAULT_swimSpeed;
            numericUpDown_flyForce.Value = DEFAULT_flyForce;

            numericUpDown_flySpeed.Value = DEFAULT_flySpeed;
            numericUpDown_gravityAcc.Value = DEFAULT_gravityAcc;
            numericUpDown_fallSpeed.Value = DEFAULT_fallSpeed;
            numericUpDown_jumpSpeed.Value = DEFAULT_jumpSpeed;

            numericUpDown_maxFriction.Value = DEFAULT_maxFriction;
            numericUpDown_minFriction.Value = DEFAULT_minFriction;
            numericUpDown_swimSpeedDec.Value = DEFAULT_swimSpeedDec;
            numericUpDown_flyJumpDec.Value = DEFAULT_flyJumpDec;
        }

        /// <summary>
        /// Reset to post-bb map physics values
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button_resetPostBB_Click(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// Keydown on the UI window
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UIWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) // Close without saving
            {
                Close();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                //saveButton_Click(null, null);
            }
        }
        #endregion
    }
}
