using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HaCreator.MapEditor.TilesDesign
{
    class enH1 : MapTileDesign
    {
        public enH1()
        {
            type = "enH1";

            potentials.Add(new MapTileDesignPotential("bsc", 0, -60));
            potentials.Add(new MapTileDesignPotential("enH0", 0, 0));
            potentials.Add(new MapTileDesignPotential("enH1", -90, 0));
            potentials.Add(new MapTileDesignPotential("enH1", 90, 0));
            potentials.Add(new MapTileDesignPotential("edD", 0, 0));
            potentials.Add(new MapTileDesignPotential("edD", 90, 0));
            potentials.Add(new MapTileDesignPotential("enV0", 90, 0));
            potentials.Add(new MapTileDesignPotential("enV1", 0, 0));
            potentials.Add(new MapTileDesignPotential("slLU", 90, 0));
            potentials.Add(new MapTileDesignPotential("slRU", 0, 0));
            potentials.Add(new MapTileDesignPotential("slLD", 0, -60));
            potentials.Add(new MapTileDesignPotential("slLD", 180, 0));
            potentials.Add(new MapTileDesignPotential("slRD", 90, -60));
            potentials.Add(new MapTileDesignPotential("slRD", -90, 0));
        }
    }
}
