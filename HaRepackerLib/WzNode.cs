/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Collections;

namespace HaRepackerLib
{
    public class WzNode : TreeNode
    {
        public delegate ContextMenuStrip ContextMenuBuilderDelegate(WzNode node, WzObject obj);
        public static ContextMenuBuilderDelegate ContextMenuBuilder = null;

        public WzNode(WzObject SourceObject)
            : base(SourceObject.Name)
        {
            ParseChilds(SourceObject);
        }

        private void ParseChilds(WzObject SourceObject)
        {
            if (SourceObject == null) throw new NullReferenceException("Cannot create a null WzNode");
            Tag = SourceObject;
            SourceObject.HRTag = this;
            if (SourceObject is WzFile) SourceObject = ((WzFile)SourceObject).WzDirectory;
            if (SourceObject is WzDirectory)
            {
                foreach (WzDirectory dir in ((WzDirectory)SourceObject).WzDirectories)
                    Nodes.Add(new WzNode(dir));
                foreach (WzImage img in ((WzDirectory)SourceObject).WzImages)
                    Nodes.Add(new WzNode(img));
            }
            else if (SourceObject is WzImage)
            {
                if (((WzImage)SourceObject).Parsed)
                    foreach (WzImageProperty prop in ((WzImage)SourceObject).WzProperties)
                        Nodes.Add(new WzNode(prop));
            }
            else if (SourceObject is IPropertyContainer)
            {
                foreach (WzImageProperty prop in ((IPropertyContainer)SourceObject).WzProperties)
                    Nodes.Add(new WzNode(prop));
            }
        }

        public void Delete()
        {
            Remove();
            if (Tag is WzImageProperty) ((WzImageProperty)Tag).ParentImage.Changed = true;
            ((WzObject)Tag).Remove();
        }

        public bool CanHaveChilds
        {
            get
            {
                return (Tag is WzFile ||
                    Tag is WzDirectory ||
                    Tag is WzImage ||
                    Tag is IPropertyContainer);
            }
        }

        public static WzNode GetChildNode(WzNode parentNode, string name)
        {
            foreach (WzNode node in parentNode.Nodes)
                if (node.Text == name) 
                    return node;
            return null;
        }

        public static bool CanNodeBeInserted(WzNode parentNode, string name)
        {
            WzObject obj = (WzObject)parentNode.Tag;
            if (obj is IPropertyContainer) return ((IPropertyContainer)obj)[name] == null;
            else if (obj is WzDirectory) return ((WzDirectory)obj)[name] == null;
            else if (obj is WzFile) return ((WzFile)obj).WzDirectory[name] == null;
            else return false;
        }

        private bool addObjInternal(WzObject obj)
        {
            WzObject TaggedObject = (WzObject)Tag;
            if (TaggedObject is WzFile) TaggedObject = ((WzFile)TaggedObject).WzDirectory;
            if (TaggedObject is WzDirectory)
            {
                if (obj is WzDirectory)
                    ((WzDirectory)TaggedObject).AddDirectory((WzDirectory)obj);
                else if (obj is WzImage)
                    ((WzDirectory)TaggedObject).AddImage((WzImage)obj);
                else return false;
            }
            else if (TaggedObject is WzImage)
            {
                if (!((WzImage)TaggedObject).Parsed) ((WzImage)TaggedObject).ParseImage();
                if (obj is WzImageProperty)
                {
                    ((WzImage)TaggedObject).AddProperty((WzImageProperty)obj);
                    ((WzImage)TaggedObject).Changed = true;
                }
                else return false;
            }
            else if (TaggedObject is IPropertyContainer)
            {
                if (obj is WzImageProperty)
                {
                    ((IPropertyContainer)TaggedObject).AddProperty((WzImageProperty)obj);
                    if (TaggedObject is WzImageProperty)
                        ((WzImageProperty)TaggedObject).ParentImage.Changed = true;
                }
                else return false;
            }
            else return false;
            return true;
        }

        public bool AddNode(WzNode node)
        {
            if (CanNodeBeInserted(this, node.Text))
            {
                TryParseImage();
                this.Nodes.Add(node);
                addObjInternal((WzObject)node.Tag);
                return true;
            }
            else
            {
                MessageBox.Show("Cannot insert node \"" + node.Text + "\" because a node with the same name already exists. Skipping.", "Skipping Node", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
        }

        private void TryParseImage()
        {
            if (Tag is WzImage && !((WzImage)Tag).Parsed)
            {
                ((WzImage)Tag).ParseImage();
                Reparse();
            }
        }

        public bool AddObject(WzObject obj, UndoRedoManager undoRedoMan)
        {
            if (CanNodeBeInserted(this, obj.Name))
            {
                TryParseImage();
                if (addObjInternal(obj))
                {
                    WzNode node = new WzNode(obj);
                    Nodes.Add(node);
                    if (node.Tag is WzImageProperty)
                        ((WzImageProperty)node.Tag).ParentImage.Changed = true;
                    undoRedoMan.AddUndoBatch(new System.Collections.Generic.List<UndoRedoAction> { UndoRedoManager.ObjectAdded(this, node) });
                    node.EnsureVisible();
                    return true;
                }
                else
                {
                    Warning.Error("Could not insert property, make sure all types are correct");
                    return false;
                }
            }
            else
            {
                MessageBox.Show("Cannot insert object \"" + obj.Name + "\" because an object with the same name already exists. Skipping.", "Skipping Object", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
        }

        public void Reparse()
        {
            Nodes.Clear();
            ParseChilds((WzObject)Tag);
        }

        public string GetTypeName()
        {
            return Tag.GetType().Name;
        }

        public void ChangeName(string name)
        {
            Text = name;
            ((WzObject)Tag).Name = name;
            if (Tag is WzImageProperty) ((WzImageProperty)Tag).ParentImage.Changed = true;
        }

        public WzNode TopLevelNode
        {
            get
            {
                WzNode parent = this;
                while (parent.Level > 0)
                {
                    parent = (WzNode)parent.Parent;
                }
                return parent;
            }
        }

        public override ContextMenuStrip ContextMenuStrip
        {
            get
            {
                return ContextMenuBuilder == null ? null : ContextMenuBuilder(this, (WzObject)Tag);
            }
            set
            {
                base.ContextMenuStrip = value;
            }
        }
    }
}
