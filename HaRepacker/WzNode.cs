/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Windows.Forms;
using MapleLib.WzLib;
using MapleLib.WzLib.WzProperties;
using System.Collections;
using System.Drawing;
using HaRepacker.Comparer;

namespace HaRepacker
{
    public class WzNode : TreeNode
    {
        public delegate ContextMenuStrip ContextMenuBuilderDelegate(WzNode node, WzObject obj);
        public static ContextMenuBuilderDelegate ContextMenuBuilder = null;

        private bool isWzObjectAddedManually = false;
        public static Color NewObjectForeColor = Color.Red;

        public WzNode(WzObject SourceObject, bool isWzObjectAddedManually = false)
            : base(SourceObject.Name)
        {
            this.isWzObjectAddedManually = isWzObjectAddedManually;
            if (isWzObjectAddedManually)
            {
                ForeColor = NewObjectForeColor;
            }
            // Childs
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
            try
            {
                Remove();
            }
            catch(Exception) 
            { 
                throw new Exception("Cannot remove/replace an inlinked node"); 
            }

            if (Tag is WzImageProperty property)
                property.ParentImage.Changed = true;
            ((WzObject)Tag).Remove();
        }

        public bool IsWzObjectAddedManually
        {
            get
            {
                return isWzObjectAddedManually;
            }
            private set { }
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
            if (obj is IPropertyContainer container) 
                return container[name] == null;
            else if (obj is WzDirectory directory) 
                return directory[name] == null;
            else if (obj is WzFile file) 
                return file.WzDirectory[name] == null;
            else 
                return false;
        }

        private bool AddObjInternal(WzObject obj)
        {
            WzObject TaggedObject = (WzObject)Tag;
            if (TaggedObject is WzFile file) 
                TaggedObject = file.WzDirectory;

            if (TaggedObject is WzDirectory directory)
            {
                if (obj is WzDirectory wzDirectory)
                    directory.AddDirectory(wzDirectory);
                else if (obj is WzImage wzImgProperty)
                    directory.AddImage(wzImgProperty);
                else
                    return false;
            }
            else if (TaggedObject is WzImage wzImageProperty)
            {
                if (!wzImageProperty.Parsed) 
                    wzImageProperty.ParseImage();
                if (obj is WzImageProperty imgProperty)
                {
                    wzImageProperty.AddProperty(imgProperty);
                    wzImageProperty.Changed = true;
                }
                else 
                    return false;
            }
            else if (TaggedObject is IPropertyContainer container)
            {
                if (obj is WzImageProperty property)
                {
                    container.AddProperty(property);
                    if (TaggedObject is WzImageProperty imgProperty)
                        imgProperty.ParentImage.Changed = true;
                }
                else 
                    return false;
            }
            else 
                return false;

            return true;
        }

        /// <summary>
        /// Adds a node
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public bool AddNode(WzNode node, bool reparseImage)
        {
            if (CanNodeBeInserted(this, node.Text))
            {
                TryParseImage(reparseImage);
                this.Nodes.Add(node);
                AddObjInternal((WzObject)node.Tag);
                return true;
            }
            else
            {
                MessageBox.Show("Cannot insert node \"" + node.Text + "\" because a node with the same name already exists. Skipping.", "Skipping Node", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
        }

        /// <summary>
        /// Try parsing the WzImage if it have not been loaded
        /// </summary>
        private void TryParseImage(bool reparseImage = true)
        {
            if (Tag is WzImage)
            {
                ((WzImage)Tag).ParseImage();
                if (reparseImage)
                {
                    Reparse();
                }
            }
        }

        /// <summary>
        /// Adds a WzObject to the WzNode and returns the newly created WzNode
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="undoRedoMan"></param>
        /// <returns></returns>
        public WzNode AddObject(WzObject obj, UndoRedoManager undoRedoMan)
        {
            if (CanNodeBeInserted(this, obj.Name))
            {
                TryParseImage();
                if (AddObjInternal(obj))
                {
                    WzNode node = new WzNode(obj, true);
                    Nodes.Add(node);

                    if (node.Tag is WzImageProperty property)
                    {
                        property.ParentImage.Changed = true;
                    }
                    undoRedoMan.AddUndoBatch(new System.Collections.Generic.List<UndoRedoAction> { UndoRedoManager.ObjectAdded(this, node) });
                    node.EnsureVisible();
                    return node;
                }
                else
                {
                    Warning.Error("Could not insert property, make sure all types are correct");
                    return null;
                }
            }
            else
            {
                MessageBox.Show("Cannot insert object \"" + obj.Name + "\" because an object with the same name already exists. Skipping.", "Skipping Object", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return null;
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

        /// <summary>
        /// Change the name of the WzNode
        /// </summary>
        /// <param name="name"></param>
        public void ChangeName(string name)
        {
            Text = name;
            ((WzObject)Tag).Name = name;
            if (Tag is WzImageProperty)
                ((WzImageProperty)Tag).ParentImage.Changed = true;

            isWzObjectAddedManually = true;
            ForeColor = NewObjectForeColor;
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
