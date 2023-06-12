using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.ClientLib {

    public class CWvsPhysicalSpace2D {

        public static uint GetConstantCRC(int mapleVersion) {

            int v19 = mapleVersion;                        // version
            uint Crc32 = CCrc32.GetCrc32(v19, 0, false, false);

            v19 = 140000;                                 // dWalkForce
            uint after_dWalkForceCrc = CCrc32.GetCrc32(v19, Crc32, false, false);

            v19 = 125;                                    // dWalkSpeed
            uint after_dWalkSpeedCrc = CCrc32.GetCrc32(v19, after_dWalkForceCrc, false, false);

            v19 = 80000;                                  // dWalkDrag
            uint after_dWalkDragCrc = CCrc32.GetCrc32(v19, after_dWalkSpeedCrc, false, false);

            v19 = 60000;                                  // dSlipForce
            uint after_dSlipForce = CCrc32.GetCrc32(v19, after_dWalkDragCrc, false, false);

            v19 = 120;                                    // dSlipSpeed
            uint after_dSlipSpeedCrc = CCrc32.GetCrc32(v19, after_dSlipForce, false, false);

            v19 = 100000;                                 // dFloatDrag1
            uint after_dFloatDrag1Crc = CCrc32.GetCrc32(v19, after_dSlipSpeedCrc, false, false);

            v19 = 0;                                      // dFloatCoefficient
            uint after_dFloatCoefficientCrc = CCrc32.GetCrc32(v19, after_dFloatDrag1Crc, false, false);

            v19 = 120000;                                 // dSwimForce
            uint after_dSwimForceCrc = CCrc32.GetCrc32(v19, after_dFloatCoefficientCrc, false, false);

            v19 = 140;                                    // dSwimSpeed
            uint after_dSwimSpeedCrc = CCrc32.GetCrc32(v19, after_dSwimForceCrc, false, false);

            v19 = 120000;                                 // dFlyForce
            uint after_dFlyForceCrc = CCrc32.GetCrc32(v19, after_dSwimSpeedCrc, false, false);

            v19 = 200;                                    // dFlySpeed
            uint after_dFlySpeedCrc = CCrc32.GetCrc32(v19, after_dFlyForceCrc, false, false);

            v19 = 2000;                                   // dGravityAcc
            uint after_dGravityAccCrc = CCrc32.GetCrc32(v19, after_dFlySpeedCrc, false, false);

            v19 = 670;                                    // dFallSpeed
            uint after_dFallSpeedCrc = CCrc32.GetCrc32(v19, after_dGravityAccCrc, false, false);

            v19 = 555;                                    // dJumpSpeed
            uint after_dJumpSpeedCrc = CCrc32.GetCrc32(v19, after_dFallSpeedCrc, false, false);

            v19 = 2;                                      // dMaxFriction
            uint after_dMaxFrictionCrc = CCrc32.GetCrc32(v19, after_dJumpSpeedCrc, false, false);

            v19 = 0;                                      // dMinFriction
            uint after_dMinFrictionCrc = CCrc32.GetCrc32(v19, after_dMaxFrictionCrc, false, false);

            v19 = 0;                                      // dSwimSpeedDec
            uint after_dSwimSpeedDecCrc = CCrc32.GetCrc32(v19, after_dMinFrictionCrc, false, false);

            v19 = 0;                                      // dFlyJumpDec
            return CCrc32.GetCrc32(v19, after_dSwimSpeedDecCrc, false, false);
        }
    }
}
