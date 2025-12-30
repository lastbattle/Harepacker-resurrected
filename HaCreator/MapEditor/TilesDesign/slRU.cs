using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapEditor.TilesDesign
{
    class slRU : MapTileDesign
    {
        public slRU()
        {
            type = "slRU";

            potentials.Add(new MapTileDesignPotential("bsc", -90, -60));
            potentials.Add(new MapTileDesignPotential("bsc", 0, 0));
            potentials.Add(new MapTileDesignPotential("enH0", -90, -60));
            potentials.Add(new MapTileDesignPotential("enH0", 90, 0));
            potentials.Add(new MapTileDesignPotential("enH1", 0, 0));
            potentials.Add(new MapTileDesignPotential("enV0", 0, -60));
            potentials.Add(new MapTileDesignPotential("edU", 90, 0));
            potentials.Add(new MapTileDesignPotential("edU", 0, -60));
            potentials.Add(new MapTileDesignPotential("slLU", 0, 0));
            potentials.Add(new MapTileDesignPotential("slLU", 180, 0));
            potentials.Add(new MapTileDesignPotential("slRU", 90, 60));
            potentials.Add(new MapTileDesignPotential("slRU", -90, -60));
            potentials.Add(new MapTileDesignPotential("slLD", 0, 0));
            potentials.Add(new MapTileDesignPotential("slLD", 0, -60));
            potentials.Add(new MapTileDesignPotential("slLD", 90, 0));
            potentials.Add(new MapTileDesignPotential("slRD", 0, 0));
        }

    }
}
