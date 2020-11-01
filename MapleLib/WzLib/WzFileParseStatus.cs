using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapleLib.WzLib
{
    public enum WzFileParseStatus
    {
        Path_Is_Null = -1,
        Error_Game_Ver_Hash = -2, // Error with game version hash : The specified game version is incorrect and WzLib was unable to determine the version itself

        Failed_Unknown = 0x0,
        Success = 0x1,
    }

    public static class WzFileParseStatusExtension 
    {
        public static string GetErrorDescription(this WzFileParseStatus status)
        {
            switch (status) // TODO localisation via Resources
            {
                case WzFileParseStatus.Success:
                    return "Success";
                case WzFileParseStatus.Failed_Unknown:
                    return "Failed, in this case the causes are undetermined.";

                case WzFileParseStatus.Path_Is_Null:
                    return "Path is null";
                case WzFileParseStatus.Error_Game_Ver_Hash:
                    return "Error with game version hash : The specified game version is incorrect and WzLib was unable to determine the version itself";
            }
            return string.Empty;
        }
    }
}
