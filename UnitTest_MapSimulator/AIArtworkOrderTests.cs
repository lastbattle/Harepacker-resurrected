using System.Drawing;
using System.Threading;
using HaCreator.MapEditor;
using HaCreator.MapEditor.AI;
using HaCreator.MapEditor.Info;
using HaCreator.MapEditor.UndoRedo;
using MapleLib.WzLib.WzStructure.Data;
using Newtonsoft.Json.Linq;
using XnaPoint = Microsoft.Xna.Framework.Point;

namespace UnitTest_MapSimulator;

[Collection("AI placement dataset")]
public class AIArtworkOrderTests
{
    [Fact]
    public void EqualZArtworkKeepsPixelOrderThroughSortAndDeleteUndo()
    {
        Exception? failure=null;
        var thread=new Thread(()=>
        {
            try
            {
                var board=new Board(new XnaPoint(100,100),XnaPoint.Zero,new MultiBoard(),true,null,ItemTypes.All,ItemTypes.All);
                board.CreateMapLayers();
                using var red=new Bitmap(20,20); using var blue=new Bitmap(20,20);
                using(var g=Graphics.FromImage(red)) g.Clear(Color.Red);
                using(var g=Graphics.FromImage(blue)) g.Clear(Color.Blue);
                for(int i=0;i<40;i++)
                {
                    var info=new ObjectInfo(i%2==0?red:blue,Point.Empty,"synthetic","0","0",i.ToString(),null);
                    board.BoardItems.Add(info.CreateInstance(board.Layers[0],board,10,10,0,false),true);
                }
                var original=board.BoardItems.TileObjs.ToArray();
                string Render()=>(string)MapAIVisualRenderer.RenderMap(board,new JObject { ["overlays"]=false })[1]["data"]!;
                string before=Render();
                board.BoardItems.Sort();
                Assert.Equal(original,board.BoardItems.TileObjs);
                Assert.Equal(before,Render());
                var removed=original[7];
                var actions=new List<UndoRedoAction>(); removed.RemoveItem(actions);
                board.UndoRedoMan.AddUndoBatch(actions);
                board.UndoRedoMan.Undo();
                Assert.Equal(original,board.BoardItems.TileObjs);
                Assert.Equal(before,Render());
                board.UndoRedoMan.Redo(); board.UndoRedoMan.Undo();
                Assert.Equal(original,board.BoardItems.TileObjs);
                Assert.Equal(before,Render());
                board.Dispose();
            }
            catch(Exception ex) { failure=ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30))); Assert.Null(failure);
    }
}
