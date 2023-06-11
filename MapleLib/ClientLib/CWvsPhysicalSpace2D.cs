using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.ClientLib {

    public class CWvsPhysicalSpace2D {

        public static uint GetConstantCRC(int mapleVersion) {

            int v19 = mapleVersion;                        // version
            uint Crc32 = CCrc32.GetCrc32(v19, 4u, 0, false, false);

            v19 = 140000;                                 // dWalkForce
            uint after_dWalkForceCrc = CCrc32.GetCrc32(v19, 4u, Crc32, false, false);

            v19 = 125;                                    // dWalkSpeed
            uint after_dWalkSpeedCrc = CCrc32.GetCrc32(v19, 4u, after_dWalkForceCrc, false, false);

            v19 = 80000;                                  // dWalkDrag
            uint after_dWalkDragCrc = CCrc32.GetCrc32(v19, 4u, after_dWalkSpeedCrc, false, false);

            v19 = 60000;                                  // dSlipForce
            uint after_dSlipForce = CCrc32.GetCrc32(v19, 4u, after_dWalkDragCrc, false, false);

            v19 = 120;                                    // dSlipSpeed
            uint after_dSlipSpeedCrc = CCrc32.GetCrc32(v19, 4u, after_dSlipForce, false, false);

            v19 = 100000;                                 // dFloatDrag1
            uint after_dFloatDrag1Crc = CCrc32.GetCrc32(v19, 4u, after_dSlipSpeedCrc, false, false);

            v19 = 0;                                      // dFloatCoefficient
            uint v7 = CCrc32.GetCrc32(v19, 4u, after_dFloatDrag1Crc, false, false);

            v19 = 120000;                                 // dSwimForce
            uint v8 = CCrc32.GetCrc32(v19, 4u, v7, false, false);

            v19 = 140;                                    // dSwimSpeed
            uint v9 = CCrc32.GetCrc32(v19, 4u, v8, false, false);

            v19 = 120000;                                 // dFlyForce
            uint v10 = CCrc32.GetCrc32(v19, 4u, v9, false, false);

            v19 = 200;                                    // dFlySpeed
            uint v11 = CCrc32.GetCrc32(v19, 4u, v10, false, false);

            v19 = 2000;                                   // dGravityAcc
            uint v12 = CCrc32.GetCrc32(v19, 4u, v11, false, false);

            v19 = 670;                                    // dFallSpeed
            uint v13 = CCrc32.GetCrc32(v19, 4u, v12, false, false);

            v19 = 555;                                    // dJumpSpeed
            uint v14 = CCrc32.GetCrc32(v19, 4u, v13, false, false);

            v19 = 2;                                      // dMaxFriction
            uint v15 = CCrc32.GetCrc32(v19, 4u, v14, false, false);

            v19 = 0;                                      // dMinFriction
            uint v16 = CCrc32.GetCrc32(v19, 4u, v15, false, false);

            v19 = 0;                                      // dSwimSpeedDec
            uint v17 = CCrc32.GetCrc32(v19, 4u, v16, false, false);

            v19 = 0;                                      // dFlyJumpDec
            return CCrc32.GetCrc32(v19, 4u, v17, false, false);
        }
    }
}
