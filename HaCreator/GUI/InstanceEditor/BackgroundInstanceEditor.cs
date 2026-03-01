using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;
using HaCreator.MapEditor;
using MapleLib.WzLib.WzStructure.Data;
using HaCreator.MapEditor.Instance;
using HaCreator.MapEditor.UndoRedo;
using System.Windows.Controls;
using HaCreator.MapEditor.Info;
using Spine;
using HaSharedLibrary.Render.DX;
using System.Diagnostics;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class BackgroundInstanceEditor : EditorBase
    {
        private BackgroundInstance item;
        private readonly BackgroundStateBackup initialState; // Store the initial state of the BackgroundItem

        private bool _isLoading = false; // Flag to prevent initial load events from triggering updates
        private bool _FormClosedByOk = false; // Flag to check if the form was closed by clicking OK

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="item"></param>
        public BackgroundInstanceEditor(BackgroundInstance item)
        {
            InitializeComponent();

            _isLoading = true;

            this.item = item;
            initialState = new BackgroundStateBackup(item);

            // Initialize controls
            xInput.Value = item.BaseX;
            yInput.Value = item.BaseY;
            if (item.Z == -1)
                zInput.Enabled = false;
            else
                zInput.Value = item.Z;

            pathLabel.Text = HaCreatorStateManager.CreateItemDescription(item);

            // Populate typeBox from BackgroundType enum with friendly names
            foreach (BackgroundType bgType in Enum.GetValues(typeof(BackgroundType)))
            {
                typeBox.Items.Add(bgType.GetFriendlyName());
            }
            typeBox.SelectedIndex = (int)item.type;

            // Set initial description
            UpdateTypeDescription();
            alphaBox.Value = item.a;
            front.Checked = item.front;

            rxBox.Value = item.rx;
            trackBar_parallaxX.Value = item.rx;
            ryBox.Value = item.ry;
            trackBar_parallaxY.Value = item.ry;

            cxBox.Value = item.cx;
            cyBox.Value = item.cy;

            // Resolutions
            foreach (RenderResolution val in Enum.GetValues(typeof(RenderResolution)))
            {
                ComboBoxItem comboBoxItem = new()
                {
                    Tag = val,
                    Content = RenderResolutionExtensions.ToReadableString(val)
                };
                comboBox_screenMode.Items.Add(comboBoxItem);
            }
            comboBox_screenMode.DisplayMember = "Content";

            int i = 0;
            foreach (ComboBoxItem citem in comboBox_screenMode.Items)
            {
                if ((int)((RenderResolution)citem.Tag) == item.screenMode)
                {
                    comboBox_screenMode.SelectedIndex = i;
                    break;
                }
                i++;
            }
            if (comboBox_screenMode.SelectedIndex == -1) // Ensure a selection
                comboBox_screenMode.SelectedIndex = 0;

            // Spine
            BackgroundInfo baseInfo = (BackgroundInfo)item.BaseInfo;
            if (baseInfo.WzSpineAnimationItem == null)
                groupBox_spine.Enabled = false;
            else
            {
                groupBox_spine.Enabled = true;
                foreach (Animation ani in baseInfo.WzSpineAnimationItem.SkeletonData.Animations)
                {
                    ComboBoxItem comboBoxItem = new ComboBoxItem
                    {
                        Tag = ani,
                        Content = ani.Name
                    };
                    comboBox_spineAnimation.Items.Add(comboBoxItem);
                }
                comboBox_spineAnimation.DisplayMember = "Content";

                int i_animation = 0;
                foreach (ComboBoxItem citem in comboBox_spineAnimation.Items)
                {
                    if (((Animation)citem.Tag).Name == item.SpineAni)
                    {
                        comboBox_spineAnimation.SelectedIndex = i_animation;
                        break;
                    }
                    i_animation++;
                }

                // spineRandomStart checkbox
                checkBox_spineRandomStart.Checked = item.SpineRandomStart;
            }

            // Subscribe to the FormClosed event
            this.FormClosed += BackgroundInstanceEditor_FormClosed;

            // Add real event handlers
            xInput.ValueChanged += (s, e) => UpdateBackgroundItem();
            yInput.ValueChanged += (s, e) => UpdateBackgroundItem();
            if (zInput.Enabled) 
                zInput.ValueChanged += (s, e) => UpdateBackgroundItem();
            alphaBox.ValueChanged += (s, e) => UpdateBackgroundItem();
            front.CheckedChanged += (s, e) => UpdateBackgroundItem();
            comboBox_screenMode.SelectedIndexChanged += (s, e) => UpdateBackgroundItem();
            if (groupBox_spine.Enabled)
            {
                checkBox_spineRandomStart.CheckedChanged += (s, e) => UpdateBackgroundItem();
                comboBox_spineAnimation.SelectedIndexChanged += (s, e) => UpdateBackgroundItem();
            }

            _isLoading = false;
        }

        /// <summary>
        /// Runs after the forms has closed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BackgroundInstanceEditor_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_FormClosedByOk)
                return; // If the form was closed by OK, do not revert to initial state

            ApplyState(item, initialState); // Revert to initial state
        }

        protected override void cancelButton_Click(object sender, EventArgs e)
        {
            if (_FormClosedByOk)
                return; // If the form was closed by OK, do not revert to initial state

            ApplyState(item, initialState); // Revert to initial state
            Close();
        }

        protected override void okButton_Click(object sender, EventArgs e)
        {
            BackgroundType bgType = (BackgroundType)typeBox.SelectedIndex;
            if ((cyBox.Value < 0 && (bgType != BackgroundType.Regular)) ||
                (cxBox.Value < 0 && (bgType == BackgroundType.Regular)))
            {
                MessageBox.Show("You may not select a negative CX or CY value while selecting a non-regular background type.", "Error", MessageBoxButtons.OK);
                return;
            }

            // Create an undo action for the entire edit
            BackgroundStateBackup finalState = new BackgroundStateBackup(item);
            UndoRedoAction action = new UndoRedoAction(item, UndoRedoType.BackgroundPropertiesChanged, initialState, finalState);
            item.Board.UndoRedoMan.AddUndoBatch(new List<UndoRedoAction> { action });

            UpdateBackgroundItem();

            _FormClosedByOk = true; // Set flag to indicate form was closed by OK button, so it doesnt rollback to initial state
            Close();
        }

        /// <summary>
        /// TrackBar for parallaxY
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void trackBar_parallaxY_Scroll(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            TrackBar trackBar = sender as TrackBar;
            ryBox.Value = trackBar.Value;

            UpdateBackgroundItem();
        }

        /// <summary>
        /// TrackBar for parallax X
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void trackBar_parallaxX_Scroll(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            TrackBar trackBar = sender as TrackBar;
            rxBox.Value = trackBar.Value;

            UpdateBackgroundItem();
        }

        /// <summary>
        /// cx changed
        /// Disables the 'ok' button if the user selects a moving type background AND a negative cx or cy value.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cxBox_ValueChanged(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            bool bDisableSaveButton = false;

            BackgroundType bgType = (BackgroundType)typeBox.SelectedIndex;
            if (bgType != BackgroundType.Regular && cxBox.Value < 0)
                bDisableSaveButton = true;

            okButton.Enabled = !bDisableSaveButton;

            UpdateBackgroundItem(); // Apply change for preview
        }

        /// <summary>
        /// cy changed
        /// Disables the 'ok' button if the user selects a moving type background AND a negative cx or cy value.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cyBox_ValueChanged(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            bool bDisableSaveButton = false;

            BackgroundType bgType = (BackgroundType)typeBox.SelectedIndex;
            if (bgType != BackgroundType.Regular && cyBox.Value < 0)
                bDisableSaveButton = true;

            okButton.Enabled = !bDisableSaveButton;

            UpdateBackgroundItem(); // Apply change for preview
        }

        /// <summary>
        /// Background type changed
        /// Disables the 'ok' button if the user selects a moving type background AND a negative cx or cy value.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void typeBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isLoading)
                return;

            bool bDisableSaveButton = false;

            BackgroundType bgType = (BackgroundType)typeBox.SelectedIndex;
            if (bgType != BackgroundType.Regular)
            {
                cxBox.Minimum = 0;
                cyBox.Minimum = 0;

                if (cyBox.Value < 0 || cxBox.Value < 0)
                    bDisableSaveButton = true;
            } else
            {
                cxBox.Minimum = int.MaxValue * -1;
                cyBox.Minimum = int.MaxValue * -1;
            }
            okButton.Enabled = !bDisableSaveButton;

            // Update description label
            UpdateTypeDescription();

            UpdateBackgroundItem(); // Apply change for preview
        }

        /// <summary>
        /// Updates the description label based on the selected background type.
        /// </summary>
        private void UpdateTypeDescription()
        {
            if (typeBox.SelectedIndex >= 0 && typeBox.SelectedIndex < Enum.GetValues(typeof(BackgroundType)).Length)
            {
                BackgroundType bgType = (BackgroundType)typeBox.SelectedIndex;
                labelTypeDescription.Text = bgType.GetDescription();
            }
        }

        #region Update
        /// <summary>
        /// Update the background item with the current values in the editor.
        /// changes are instantly displayed on the Board.
        /// </summary>
        /// <summary>
        /// Update the background item with current values, optionally adding undo actions
        /// </summary>
        private void UpdateBackgroundItem()
        {
            if (_isLoading)
                return;

            lock (item.Board.ParentControl)
            {
                bool sort = false;

                if (xInput.Value != item.BaseX || yInput.Value != item.BaseY)
                {
                    item.MoveBase((int)xInput.Value, (int)yInput.Value);
                }
                if (zInput.Enabled && item.Z != zInput.Value)
                {
                    item.Z = (int)zInput.Value;
                    sort = true;
                }
                if (front.Checked != item.front)
                {
                    (item.front ? item.Board.BoardItems.FrontBackgrounds : item.Board.BoardItems.BackBackgrounds).Remove(item);
                    (front.Checked ? item.Board.BoardItems.FrontBackgrounds : item.Board.BoardItems.BackBackgrounds).Add(item);
                    item.front = front.Checked;
                    sort = true;
                }
                if (sort)
                    item.Board.BoardItems.Sort();

                item.type = (BackgroundType)typeBox.SelectedIndex;
                item.a = (int)alphaBox.Value;
                item.rx = (int)rxBox.Value;
                item.ry = (int)ryBox.Value;
                item.cx = (int)cxBox.Value;
                item.cy = (int)cyBox.Value;
                item.screenMode = (int)((RenderResolution)((ComboBoxItem)comboBox_screenMode.SelectedItem).Tag);

                if (groupBox_spine.Enabled)
                {
                    item.SpineRandomStart = checkBox_spineRandomStart.Checked;
                    item.SpineAni = comboBox_spineAnimation.SelectedItem != null
                        ? ((comboBox_spineAnimation.SelectedItem as ComboBoxItem).Tag as Animation).Name
                        : null;
                }
                else
                {
                    item.SpineRandomStart = false;
                    item.SpineAni = null;
                }
            }
        }

        /// <summary>
        /// Apply a given state to the BackgroundInstance item
        /// </summary>
        private void ApplyState(BackgroundInstance item, BackgroundStateBackup state)
        {
            if (_isLoading)
                return;

            lock (item.Board.ParentControl)
            {
                if (item.BaseX != state.BaseX || item.BaseY != state.BaseY)
                    item.MoveBase(state.BaseX, state.BaseY);

                if (item.Z != state.Z)
                {
                    item.Z = state.Z;
                    item.Board.BoardItems.Sort();
                }

                if (item.front != state.Front)
                {
                    (item.front ? item.Board.BoardItems.FrontBackgrounds : item.Board.BoardItems.BackBackgrounds).Remove(item);
                    (state.Front ? item.Board.BoardItems.FrontBackgrounds : item.Board.BoardItems.BackBackgrounds).Add(item);
                    item.front = state.Front;
                    item.Board.BoardItems.Sort();
                }

                item.type = state.Type;
                item.a = state.A;
                item.rx = state.Rx;
                item.ry = state.Ry;
                item.cx = state.Cx;
                item.cy = state.Cy;
                item.screenMode = state.ScreenMode;
                item.SpineAni = state.SpineAni;
                item.SpineRandomStart = state.SpineRandomStart;
            }
        }
        #endregion
    }
}
