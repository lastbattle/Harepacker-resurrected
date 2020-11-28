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
                WzDirectory wzMapFile = Program.WzManager[WZ_FILE_NAME];
                WzImage image = (WzImage)wzMapFile[WZ_FILE_IMAGE];
                if (image != null)
                {
                    numericUpDown_walkForce.Value = (decimal)image["walkForce"].GetDouble();
                    numericUpDown_walkSpeed.Value = (decimal)image["walkSpeed"].GetDouble();
                    numericUpDown_walkDrag.Value = (decimal)image["walkDrag"].GetDouble();
                    numericUpDown_slipForce.Value = (decimal)image["slipForce"].GetDouble();
                    numericUpDown_slipSpeed.Value = (decimal)image["slipSpeed"].GetDouble();
                    numericUpDown_floatDrag1.Value = (decimal)image["floatDrag1"].GetDouble();
                    numericUpDown_floatDrag2.Value = (decimal)image["floatDrag2"].GetDouble();
                    numericUpDown_floatCoefficient.Value = (decimal)image["floatCoefficient"].GetDouble();
                    numericUpDown_swimForce.Value = (decimal)image["swimForce"].GetDouble();
                    numericUpDown_swimSpeed.Value = (decimal)image["swimSpeed"].GetDouble();
                    numericUpDown_flyForce.Value = (decimal)image["flyForce"].GetDouble();
                    numericUpDown_flySpeed.Value = (decimal)image["flySpeed"].GetDouble();
                    numericUpDown_gravityAcc.Value = (decimal)image["gravityAcc"].GetDouble();
                    numericUpDown_fallSpeed.Value = (decimal)image["fallSpeed"].GetDouble();
                    numericUpDown_jumpSpeed.Value = (decimal)image["jumpSpeed"].GetDouble();
                    numericUpDown_maxFriction.Value = (decimal)image["maxFriction"].GetDouble();
                    numericUpDown_minFriction.Value = (decimal)image["minFriction"].GetDouble();
                    numericUpDown_swimSpeedDec.Value = (decimal)image["swimSpeedDec"].GetDouble();
                    numericUpDown_flyJumpDec.Value = (decimal)image["flyJumpDec"].GetDouble();

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
                WzDirectory wzMapFile = Program.WzManager[WZ_FILE_NAME];
                WzImage image = (WzImage)wzMapFile[WZ_FILE_IMAGE];
                if (image != null)
                {
                    image["walkForce"].SetValue(numericUpDown_walkForce.Value);
                    image["walkSpeed"].SetValue(numericUpDown_walkSpeed.Value);
                    image["walkDrag"].SetValue(numericUpDown_walkDrag.Value);
                    image["slipForce"].SetValue(numericUpDown_slipForce.Value);
                    image["slipSpeed"].SetValue(numericUpDown_slipSpeed.Value);
                    image["floatDrag1"].SetValue(numericUpDown_floatDrag1.Value);
                    image["floatDrag2"].SetValue(numericUpDown_floatDrag2.Value);
                    image["floatCoefficient"].SetValue(numericUpDown_floatCoefficient.Value);
                    image["swimForce"].SetValue(numericUpDown_swimForce.Value);
                    image["swimSpeed"].SetValue(numericUpDown_swimSpeed.Value);
                    image["flyForce"].SetValue(numericUpDown_flyForce.Value);
                    image["flySpeed"].SetValue(numericUpDown_flySpeed.Value);
                    image["gravityAcc"].SetValue(numericUpDown_gravityAcc.Value);
                    image["fallSpeed"].SetValue(numericUpDown_fallSpeed.Value);
                    image["jumpSpeed"].SetValue(numericUpDown_jumpSpeed.Value);
                    image["maxFriction"].SetValue(numericUpDown_maxFriction.Value);
                    image["minFriction"].SetValue(numericUpDown_minFriction.Value);
                    image["swimSpeedDec"].SetValue(numericUpDown_swimSpeedDec.Value);
                    image["flyJumpDec"].SetValue(numericUpDown_flyJumpDec.Value);

                    Program.WzManager.SetWzFileUpdated(WZ_FILE_NAME, image); // flag as changed 
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
