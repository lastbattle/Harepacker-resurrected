using System;
using System.Windows;
using System.Windows.Input;

namespace HaCreator.GUI
{
    public class EditorBase : Window
    {
        public EditorBase()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            PreviewKeyDown += InstanceEditorBase_KeyDown;
        }

        protected virtual void InstanceEditorBase_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { e.Handled = true; cancelButton_Click(this, EventArgs.Empty); }
            else if (e.Key == Key.Enter) { e.Handled = true; okButton_Click(this, EventArgs.Empty); }
        }

        protected virtual void cancelButton_Click(object sender, EventArgs e) { }
        protected virtual void okButton_Click(object sender, EventArgs e) { }
    }
}
