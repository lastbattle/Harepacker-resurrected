/* Copyright (C) 2015 haha01haha01

* This Source Code Form is subject to the terms of the Mozilla Public
* License, v. 2.0. If a copy of the MPL was not distributed with this
* file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Drawing;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib;
using HaSharedLibrary.SharpApng;

namespace HaRepacker
{
    /// <summary>
    /// Builds animation files from WZ animations
    /// </summary>
    public static class AnimationBuilder
    {
        #region Extras
        /// <summary>
        /// Is an animation object (it could either be WzSubProperty, or a whole bunch of WzCanvasProperty)
        /// </summary>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static bool IsValidAnimationWzObject(WzObject prop)
        {
            if (!(prop is WzSubProperty))
                return false;

            WzSubProperty castedProp = (WzSubProperty)prop;
            List<WzCanvasProperty> props = new List<WzCanvasProperty>(castedProp.WzProperties.Count);
            int foo;

            foreach (WzImageProperty subprop in castedProp.WzProperties)
            {
                if (!(subprop is WzCanvasProperty))
                    continue;
                if (!int.TryParse(subprop.Name, out foo))
                    return false;
                props.Add((WzCanvasProperty)subprop);
            }
            if (props.Count < 2)
                return false;

            props.Sort(new Comparison<WzCanvasProperty>(AnimationBuilder.PropertySorter));
            for (int i = 0; i < props.Count; i++)
                if (i.ToString() != props[i].Name)
                    return false;
            return true;
        }
        #endregion

        private static Bitmap OptimizeBitmapTransparent(Bitmap source, WzVectorProperty origin, Point biggestPng, Point SmallestEmptySpace, Point MaximumMapEndingPts)
        {
            Point Size = new Point(biggestPng.X - SmallestEmptySpace.X, biggestPng.Y - SmallestEmptySpace.Y);
            Bitmap empty = new Bitmap(MaximumMapEndingPts.X - SmallestEmptySpace.X, MaximumMapEndingPts.Y - SmallestEmptySpace.Y);
            Graphics process = Graphics.FromImage((Image)empty);
            process.DrawImage(source, Size.X - origin.X.Value, Size.Y - origin.Y.Value);
            return empty;
        }

        private static int PropertySorter(WzCanvasProperty a, WzCanvasProperty b)
        {
            int aIndex = 0;
            int bIndex = 0;
            if (!int.TryParse(a.Name, out aIndex) || !int.TryParse(b.Name, out bIndex)) 
                return 0;
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
                    //System.Drawing.PointF origin = ((WzCanvasProperty)subprop).GetCanvasOriginPosition();
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
                if (subprop is WzCanvasProperty property)
                {
                    sortedProps.Add(property);
                    WzPngProperty png = property.PngProperty;
                    System.Drawing.PointF origin = property.GetCanvasOriginPosition();

                    Point StartPoints = new Point(biggestPng.X - (int) origin.X, biggestPng.Y - (int)origin.Y);
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
                Bitmap bmp = subprop.PngProperty.GetImage(false);
                System.Drawing.PointF origin = subprop.GetCanvasOriginPosition();
                bmpList.Add(OptimizeBitmapTransparent(bmp, new WzVectorProperty("", origin.X, origin.Y), biggestPng, SmallestEmptySpace, MaximumPngMappingEndingPts));

                int? delay = subprop[WzCanvasProperty.AnimationDelayPropertyName]?.GetInt();
                if (delay == null)
                    delay = 100;

                delayList.Add((int) delay);
            }
            SharpApng apngBuilder = new SharpApng();
            if (apngFirstFrame)
            {
                apngBuilder.AddFrame(new SharpApngFrame(CreateIncompatibilityFrame(new Size(bmpList[0].Width, bmpList[0].Height)),1,1));
            }
            for (int i = 0; i < bmpList.Count; i++)
            {
                apngBuilder.AddFrame(new SharpApngFrame(bmpList[i], GetNumByDelay(delayList[i]), GetDenByDelay(delayList[i])));
            }
            apngBuilder.WriteApng(savePath, apngFirstFrame, true);
        }

        private static int GetNumByDelay(int delay)
        {
            int num = delay;
            int den = 1000;
            while (num % 10 == 0 && num != 0)
            {
                num /= 10;
                den /= 10;
            }
            return num;
        }

        private static int GetDenByDelay(int delay)
        {
            int num = delay;
            int den = 1000;
            while (num % 10 == 0 && num != 0)
            {
                num /= 10;
                den /= 10;
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
