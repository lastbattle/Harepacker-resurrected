using HaCreator.MapEditor;
using HaCreator.MapEditor.UndoRedo;
using System.Collections.Generic;
using System.Windows;

namespace HaCreator.GUI.InstanceEditor
{
    public partial class GeneralInstanceEditor : Window
    {
        public BoardItem item;
        public GeneralInstanceEditor(BoardItem item) { InitializeComponent(); this.item=item; InstanceEditorDialogSupport.SetInt(xInput,item.X); InstanceEditorDialogSupport.SetInt(yInput,item.Y); if(item.Z==-1) zInput.IsEnabled=false; else InstanceEditorDialogSupport.SetInt(zInput,item.Z); pathLabel.Text=HaCreatorStateManager.CreateItemDescription(item); }
        private void CancelButton_Click(object sender,RoutedEventArgs e)=>Close();
        private void OkButton_Click(object sender,RoutedEventArgs e){ lock(item.Board.ParentControl){ int x=InstanceEditorDialogSupport.GetInt(xInput,item.X), y=InstanceEditorDialogSupport.GetInt(yInput,item.Y), z=InstanceEditorDialogSupport.GetInt(zInput,item.Z); List<UndoRedoAction> actions=new(); if(x!=item.X||y!=item.Y){actions.Add(UndoRedoManager.ItemMoved(item,new Microsoft.Xna.Framework.Point(item.X,item.Y),new Microsoft.Xna.Framework.Point(x,y)));item.Move(x,y);} if(zInput.IsEnabled&&z!=item.Z){actions.Add(UndoRedoManager.ItemZChanged(item,item.Z,z));item.Z=z;item.Board.BoardItems.Sort();} if(actions.Count>0)item.Board.UndoRedoMan.AddUndoBatch(actions);} Close(); }
    }
}
