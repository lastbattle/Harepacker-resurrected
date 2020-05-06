using HaCreator.MapEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HaCreator
{
    public class TabItemContainer
    {

        public TabItemContainer (string text, MultiBoard multiBoard, string tooltip, System.Windows.Controls.ContextMenu menu, Board board)
        {
            this.text = text;
            this.multiBoard = multiBoard;
            this.tooltip = tooltip;
            this.menu = menu;
            this.board = board;
        }

        private string text;
        public string Text
        {
            get { return text; }
            set { this.text = value; }
        }

        private MultiBoard multiBoard;
        public MultiBoard MultiBoard
        {
            get { return multiBoard; }
            private set {  }
        }

        private string tooltip;
        public string Tooltip
        {
            get { return tooltip; }
            private set { }
        }

        private System.Windows.Controls.ContextMenu menu;
        public System.Windows.Controls.ContextMenu Menu
        {
            get { return menu; }
            private set { }
        }

        private Board board;
        public Board Board
        {
            get { return board; }
            private set { }
        }
    }
}
