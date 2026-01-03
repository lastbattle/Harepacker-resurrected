using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapEditor.TilesDesign
{
    class enH0 : MapTileDesign
    {
        public enH0()
        {
            type = "enH0";

            potentials.Add(new MapTileDesignPotential("bsc", 0, 0));
            potentials.Add(new MapTileDesignPotential("enH0", -90, 0));
            potentials.Add(new MapTileDesignPotential("enH0", 90, 0));
            potentials.Add(new MapTileDesignPotential("enH1", 0, 0));
            potentials.Add(new MapTileDesignPotential("edU", 0, 0));
            potentials.Add(new MapTileDesignPotential("edU", 90, 0));
            potentials.Add(new MapTileDesignPotential("enV0", 90, -60));
            potentials.Add(new MapTileDesignPotential("enV1", 0, -60));
            potentials.Add(new MapTileDesignPotential("slLU", 0, 60));
            potentials.Add(new MapTileDesignPotential("slLU", 180, 0));
            potentials.Add(new MapTileDesignPotential("slRU", 90, 60));
            potentials.Add(new MapTileDesignPotential("slRU", -90, 0));
            potentials.Add(new MapTileDesignPotential("slLD", 90, 0));
            potentials.Add(new MapTileDesignPotential("slRD", 0, 0));
        }
    }
}
