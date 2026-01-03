using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapEditor.TilesDesign
{
    class slLU : MapTileDesign
    {
        public slLU()
        {
            type = "slLU";

            potentials.Add(new MapTileDesignPotential("bsc", 0, -60));
            potentials.Add(new MapTileDesignPotential("bsc", -90, 0));
            potentials.Add(new MapTileDesignPotential("enH0", 0, -60));
            potentials.Add(new MapTileDesignPotential("enH0", -180, 0));
            potentials.Add(new MapTileDesignPotential("enH1", -90, 0));
            potentials.Add(new MapTileDesignPotential("enV1", 0, -60));
            potentials.Add(new MapTileDesignPotential("edU", -90, 0));
            potentials.Add(new MapTileDesignPotential("edU", 0, -60));
            potentials.Add(new MapTileDesignPotential("slLU", 90, -60));
            potentials.Add(new MapTileDesignPotential("slLU", -90, 60));
            potentials.Add(new MapTileDesignPotential("slRU", 0, 0));
            potentials.Add(new MapTileDesignPotential("slRU", -180, 0));
            potentials.Add(new MapTileDesignPotential("slLD", 0, 0));
            potentials.Add(new MapTileDesignPotential("slRD", 0, 0));
            potentials.Add(new MapTileDesignPotential("slRD", 0, -60));
            potentials.Add(new MapTileDesignPotential("slRD", -90, 0));
        }

    }
}
