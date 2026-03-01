using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using MapleLib.WzLib.WzStructure.Data;
using HaCreator.MapEditor.Instance;
using MapleLib.WzLib.WzStructure.Data.QuestStructure;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class ObjQuestInput : EditorBase
    {
        public ObjectInstanceQuest result;

        public ObjQuestInput()
        {
            InitializeComponent();

            foreach (QuestStateType state in Enum.GetValues(typeof(QuestStateType)))
            {
                stateInput.Items.Add(state.ToString());
            }

            DialogResult = System.Windows.Forms.DialogResult.No;
            stateInput.SelectedIndex = 0;
        }

        protected override void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void okButton_Click(object sender, EventArgs e)
        {
            result = new ObjectInstanceQuest((int)idInput.Value, (QuestStateType)stateInput.SelectedIndex);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
