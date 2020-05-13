using MapleLib.WzLib.WzProperties;
using Spine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static MapleLib.WzDataReader;

namespace MapleLib.WzLib.Spine
{
    public class SpineAtlasLoader
    {
        /// <summary>
        /// Loads skeleton 
        /// </summary>
        /// <param name="atlasNode"></param>
        /// <param name="textureLoader"></param>
        /// <returns></returns>
        public static SkeletonData LoadSkeleton(WzStringProperty atlasNode, TextureLoader textureLoader)
        {
            string atlasData = atlasNode.GetString();
            if (string.IsNullOrEmpty(atlasData))
            {
                return null;
            }
            StringReader atlasReader = new StringReader(atlasData);

            Atlas atlas = new Atlas(atlasReader, string.Empty, textureLoader);
            SkeletonData skeletonData;

            if (!TryLoadSkeletonJsonOrBinary(atlasNode, atlas, out skeletonData))
            {
                atlas.Dispose();
                return null;
            }
            return skeletonData;
        }

        /// <summary>
        /// Load skeleton data by json or binary automatically
        /// </summary>
        /// <param name="atlasNode"></param>
        /// <param name="atlas"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        private static bool TryLoadSkeletonJsonOrBinary(WzImageProperty atlasNode, Atlas atlas, out SkeletonData data)
        {
            data = null;

            if (atlasNode == null || atlasNode.Parent == null || atlas == null)
            {
                return false;
            }
            WzObject parent = atlasNode.Parent;

            List<WzImageProperty> childProperties;
            if (parent is WzImageProperty)
                childProperties = ((WzImageProperty)parent).WzProperties;
            else
                childProperties = ((WzImage)parent).WzProperties;


            if (childProperties != null)
            {
                WzStringProperty stringJsonProp = (WzStringProperty) childProperties.Where(child => child.Name.EndsWith(".json")).FirstOrDefault();

                if (stringJsonProp != null)
                {
                    StringReader skeletonReader = new StringReader(stringJsonProp.GetString());
                    SkeletonJson json = new SkeletonJson(atlas);
                    data = json.ReadSkeletonData(skeletonReader);

                    return true;
                } else
                {
                    // try read binary based 
                    foreach (WzImageProperty property in childProperties)
                    {
                        if (property is WzSoundProperty)
                        {
                            WzSoundProperty soundProp = (WzSoundProperty)property; // should be called binaryproperty actually

                            byte[] bytes = soundProp.GetBytes(false);

                            SkeletonBinary skeletonBinary = new SkeletonBinary(atlas);

                            using (MemoryStream ms = new MemoryStream(bytes)) 
                            {
                                data = skeletonBinary.ReadSkeletonData(ms);
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

     /*   private static bool TryLoadSkeletonBinary(WzImageProperty atlasNode, Atlas atlas, out SkeletonData data)
        {
            data = null;

            if (atlasNode == null || atlasNode.Parent == null || atlas == null)
            {
                return false;
            }

            var m = Regex.Match(atlasNode.GetString(), @"^(.+)\.atlas$", RegexOptions.IgnoreCase);
            if (!m.Success)
            {
                return false;
            }

            throw new Exception("not implemented");*/

         /*   WzImageProperty node = (WzImageProperty) atlasNode.Parent[m.Result("$1")];
            WzUOLProperty uol;
            while ((uol = node.GetValueEx<Wz_Uol>(null)) != null)
            {
                node = uol.HandleUol(node);
            }

            var skeletonSource = (WzSoundProperty) node;
            if (skeletonSource == null || skeletonSource.SoundType != Wz_SoundType.Binary)
            {
                return false;
            }

            byte[] buffer = new byte[skeletonSource.DataLength];
            skeletonSource.WzFile.FileStream.Seek(skeletonSource.Offset, SeekOrigin.Begin);
            if (skeletonSource.WzFile.FileStream.Read(buffer, 0, buffer.Length) != buffer.Length)
            {
                return false;
            }
            MemoryStream ms = new MemoryStream(buffer);

            SkeletonBinary binary = new SkeletonBinary(atlas);
            data = binary.ReadSkeletonData(ms);
            return true;*/
      //  }
    }
}
