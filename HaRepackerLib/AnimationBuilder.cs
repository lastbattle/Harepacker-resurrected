/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib;
using SharpApng;

namespace HaRepackerLib
{
    /// <summary>
    /// Builds animation files from WZ animations
    /// </summary>
    public static class AnimationBuilder
    {
        private static Bitmap OptimizeBitmapTransparent(Bitmap source, WzVectorProperty origin, Point biggestPng, Point SmallestEmptySpace, Point MaximumMapEndingPts)
        {
            Point Size = new Point(biggestPng.X - SmallestEmptySpace.X, biggestPng.Y - SmallestEmptySpace.Y);
            Bitmap empty = new Bitmap(MaximumMapEndingPts.X - SmallestEmptySpace.X, MaximumMapEndingPts.Y - SmallestEmptySpace.Y);
            Graphics process = Graphics.FromImage((Image)empty);
            process.DrawImage(source, Size.X - origin.X.Value, Size.Y - origin.Y.Value);
            return empty;
        }

        public static int PropertySorter(WzCanvasProperty a, WzCanvasProperty b)
        {
            int aIndex = 0;
            int bIndex = 0;
            if (!int.TryParse(a.Name, out aIndex) || !int.TryParse(b.Name, out bIndex)) return 0;
            return aIndex.CompareTo(bIndex);
        }

        public static void ExtractAnimation(WzSubProperty parent, string savePath, bool apngFirstFrame)
        {
            List<Bitmap> bmpList = new List<Bitmap>(parent.WzProperties.Count);
            List<int> delayList = new List<int>(parent.WzProperties.Count);
            Point biggestPng = new Point(0, 0);
            Point SmallestEmptySpace = new Point(65535, 65535);
            Point MaximumPngMappingEndingPts = new Point(0, 0);
            foreach (WzImageProperty subprop in parent.WzProperties)
            {
                if (subprop is WzCanvasProperty)
                {
                    //WzVectorProperty origin = (WzVectorProperty)subprop["origin"];
                    WzPngProperty png = ((WzCanvasProperty)subprop).PngProperty;
                    if (png.Height > biggestPng.Y)
                        biggestPng.Y = png.Height;
                    if (png.Width > biggestPng.X)
                        biggestPng.X = png.Width;
                }
            }
            List<WzCanvasProperty> sortedProps = new List<WzCanvasProperty>();
            foreach (WzImageProperty subprop in parent.WzProperties)
            {
                if (subprop is WzCanvasProperty)
                {
                    sortedProps.Add((WzCanvasProperty)subprop);
                    WzPngProperty png = ((WzCanvasProperty)subprop).PngProperty;
                    WzVectorProperty origin = (WzVectorProperty)subprop["origin"];
                    Point StartPoints = new Point(biggestPng.X - origin.X.Value, biggestPng.Y - origin.Y.Value);
                    Point PngMapppingEndingPts = new Point(StartPoints.X + png.Width, StartPoints.Y + png.Height);
                    if (StartPoints.X < SmallestEmptySpace.X)
                        SmallestEmptySpace.X = StartPoints.X;
                    if (StartPoints.Y < SmallestEmptySpace.Y)
                        SmallestEmptySpace.Y = StartPoints.Y;
                    if (PngMapppingEndingPts.X > MaximumPngMappingEndingPts.X)
                        MaximumPngMappingEndingPts.X = PngMapppingEndingPts.X;
                    if (PngMapppingEndingPts.Y > MaximumPngMappingEndingPts.Y)
                        MaximumPngMappingEndingPts.Y = PngMapppingEndingPts.Y;
                }
            }
            sortedProps.Sort(new Comparison<WzCanvasProperty>(PropertySorter));
            for (int i = 0; i<sortedProps.Count; i++)
            {
                WzCanvasProperty subprop = sortedProps[i];
                if (i.ToString() != subprop.Name)
                {
                    Warning.Error(string.Format(Properties.Resources.AnimError, i.ToString()));
                    return;
                }
                Bitmap bmp = subprop.PngProperty.GetPNG(false);
                WzVectorProperty origin = (WzVectorProperty)subprop["origin"];
                bmpList.Add(OptimizeBitmapTransparent(bmp, origin, biggestPng, SmallestEmptySpace, MaximumPngMappingEndingPts));
                WzIntProperty delayProp = (WzIntProperty)subprop["delay"];
                int delay =100;
                if (delayProp != null) delay = delayProp.Value;
                delayList.Add(delay);
            }
            Apng apngBuilder = new Apng();
            if (apngFirstFrame)
            {
                apngBuilder.AddFrame(new Frame(CreateIncompatibilityFrame(new Size(bmpList[0].Width, bmpList[0].Height)),1,1));
            }
            for (int i = 0; i < bmpList.Count; i++)
            {
                apngBuilder.AddFrame(new Frame(bmpList[i], getNumByDelay(delayList[i]), getDenByDelay(delayList[i])));
            }
            apngBuilder.WriteApng(savePath, apngFirstFrame, true);
        }

        private static int getNumByDelay(int delay)
        {
            int num = delay;
            int den = 1000;
            while (num % 10 == 0 && num != 0)
            {
                num = num / 10;
                den = den / 10;
            }
            return num;
        }

        private static int getDenByDelay(int delay)
        {
            int num = delay;
            int den = 1000;
            while (num % 10 == 0 && num != 0)
            {
                num = num / 10;
                den = den / 10;
            }
            return den;
        }

        private static Bitmap CreateIncompatibilityFrame(Size frameSize)
        {
            Bitmap frame = new Bitmap(frameSize.Width, frameSize.Height);
            using (Graphics g = Graphics.FromImage(frame))
                g.DrawString(Properties.Resources.AnimCompatMessage, System.Drawing.SystemFonts.MessageBoxFont, Brushes.Black, new Rectangle(0, 0, frame.Width, frame.Height));
            return frame;
        }
    }
}
