/* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace TreeViewMS
{
	/// <summary>
	/// Summary description for TreeViewMS.
	/// </summary>
	public class TreeViewMS : System.Windows.Forms.TreeView
	{
		protected ArrayList		m_coll;
		protected TreeNode		m_lastNode, m_firstNode;

		public TreeViewMS()
		{
			m_coll = new ArrayList();
		}

		protected override void OnPaint(PaintEventArgs pe)
		{
			// TODO: Add custom paint code here

			// Calling the base class OnPaint
			base.OnPaint(pe);
		}

		public ArrayList SelectedNodes
		{
			get
			{
				return m_coll;
			}
			set
			{
                if (value == null)
                {
                    m_coll.Clear();
                    return;
                }
				removePaintFromNodes();
				m_coll.Clear();
				m_coll = value;
				paintSelectedNodes();
			}
		}



// Triggers
//
// (overriden method, and base class called to ensure events are triggered)


		protected override void OnBeforeSelect(TreeViewCancelEventArgs e)
		{
			base.OnBeforeSelect(e);
				
			bool bControl = (ModifierKeys==Keys.Control);
			bool bShift = (ModifierKeys==Keys.Shift);

			// selecting twice the node while pressing CTRL ?
			if (bControl && m_coll.Contains( e.Node ) )
			{
				// unselect it (let framework know we don't want selection this time)
				e.Cancel = true;
	
				// update nodes
				removePaintFromNodes();
				m_coll.Remove( e.Node );
				paintSelectedNodes();
				return;
			}

			m_lastNode = e.Node;
			if (!bShift) m_firstNode = e.Node; // store begin of shift sequence
		}


		protected override void OnAfterSelect(TreeViewEventArgs e)
		{
			base.OnAfterSelect(e);

			bool bControl = (ModifierKeys==Keys.Control);
			bool bShift = (ModifierKeys==Keys.Shift);

			if (bControl)
			{
				if ( !m_coll.Contains( e.Node ) ) // new node ?
				{
					m_coll.Add( e.Node );
				}
				else  // not new, remove it from the collection
				{
					removePaintFromNodes();
					m_coll.Remove( e.Node );
				}
				paintSelectedNodes();
			}
			else 
			{
				// SHIFT is pressed
				if (bShift)
				{
					Queue myQueue = new Queue();
					
					TreeNode uppernode = m_firstNode;
					TreeNode bottomnode = e.Node;
					// case 1 : begin and end nodes are parent
					bool bParent = isParent(m_firstNode, e.Node); // is m_firstNode parent (direct or not) of e.Node
					if (!bParent)
					{
						bParent = isParent(bottomnode, uppernode);
						if (bParent) // swap nodes
						{
							TreeNode t = uppernode;
							uppernode = bottomnode;
							bottomnode = t;
						}
					}
					if (bParent)
					{
						TreeNode n = bottomnode;
						while ( n != uppernode.Parent)
						{
							if ( !m_coll.Contains( n ) ) // new node ?
								myQueue.Enqueue( n );

							n = n.Parent;
						}
					}
						// case 2 : nor the begin nor the end node are descendant one another
					else
					{
						if ( (uppernode.Parent==null && bottomnode.Parent==null) || (uppernode.Parent!=null && uppernode.Parent.Nodes.Contains( bottomnode )) ) // are they siblings ?
						{
							int nIndexUpper = uppernode.Index;
							int nIndexBottom = bottomnode.Index;
							if (nIndexBottom < nIndexUpper) // reversed?
							{
								TreeNode t = uppernode;
								uppernode = bottomnode;
								bottomnode = t;
								nIndexUpper = uppernode.Index;
								nIndexBottom = bottomnode.Index;
							}

							TreeNode n = uppernode;
							while (nIndexUpper <= nIndexBottom)
							{
								if ( !m_coll.Contains( n ) ) // new node ?
									myQueue.Enqueue( n );
								
								n = n.NextNode;

								nIndexUpper++;
							} // end while
							
						}
						else
						{
							if ( !m_coll.Contains( uppernode ) ) myQueue.Enqueue( uppernode );
							if ( !m_coll.Contains( bottomnode ) ) myQueue.Enqueue( bottomnode );
						}
					}

					m_coll.AddRange( myQueue );

					paintSelectedNodes();
					m_firstNode = e.Node; // let us chain several SHIFTs if we like it
				} // end if m_bShift
				else
				{
					// in the case of a simple click, just add this item
					if (m_coll!=null && m_coll.Count>0)
					{
						removePaintFromNodes();
						m_coll.Clear();
					}
					m_coll.Add( e.Node );
				}
			}
		}



// Helpers
//
//


		protected bool isParent(TreeNode parentNode, TreeNode childNode)
		{
			if (parentNode==childNode)
				return true;

			TreeNode n = childNode;
			bool bFound = false;
			while (!bFound && n!=null)
			{
				n = n.Parent;
				bFound = (n == parentNode);
			}
			return bFound;
		}

		protected void paintSelectedNodes()
		{
			foreach ( TreeNode n in m_coll )
			{
				n.BackColor = SystemColors.Highlight;
				n.ForeColor = SystemColors.HighlightText;
			}
		}

		protected void removePaintFromNodes()
		{
			if (m_coll.Count==0) return;

			TreeNode n0 = (TreeNode) m_coll[0];
			/*Color back = n0.TreeView.BackColor;
			Color fore = n0.TreeView.ForeColor;*/
            Color back = BackColor;
            Color fore = ForeColor;

			foreach ( TreeNode n in m_coll )
			{
				n.BackColor = back;
				n.ForeColor = fore;
			}
		}

        public void RefreshSelectedNodes()
        {
            paintSelectedNodes();
        }
	}
}
