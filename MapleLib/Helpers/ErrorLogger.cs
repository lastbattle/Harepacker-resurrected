/*  MapleLib - A general-purpose MapleStory library
 * Copyright (C) 2009, 2010, 2015 Snow and haha01haha01
   
 * This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

 * This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

 * You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MapleLib.Helpers
{
    public static class ErrorLogger
    {
        private static readonly List<Error> errorList = new List<Error>();

        public static void Log(ErrorLevel level, string message)
        {
            lock (errorList)
                errorList.Add(new Error(level, message));
        }

        /// <summary>
        /// Returns the numbers of errors currently in the pending queue
        /// </summary>
        /// <returns></returns>
        public static int NumberOfErrorsPresent()
        {
            return errorList.Count;
        }

        /// <summary>
        /// Errors present currently in the pending queue
        /// </summary>
        /// <returns></returns>
        public static bool ErrorsPresent()
        {
            return errorList.Count > 0;
        }

        /// <summary>
        /// Clears all errors currently in the pending queue
        /// </summary>
        public static void ClearErrors()
        {
            lock (errorList)
                errorList.Clear();
        }

        /// <summary>
        /// Logs all pending errors in the queue to file, and clears the queue
        /// </summary>
        /// <param name="filename"></param>
        public static void SaveToFile(string filename)
        {
            if (!ErrorsPresent())
                return;

            using (StreamWriter sw = new StreamWriter(File.Open(filename, FileMode.Append, FileAccess.Write, FileShare.Read)))
            {
                sw.Write("----- Start of the error log. [");
                sw.Write(DateTime.Today.ToString());
                sw.Write("] -----");
                sw.WriteLine();

                List<Error> errorList_;
                lock (errorList)
                {
                    errorList_ = new List<Error>(errorList); // make a copy before writing
                    ClearErrors();
                }

                foreach (Error e in errorList_) 
                {
                    sw.Write("[");
                    sw.Write(e.level.ToString());
                    sw.Write("] : ");
                    sw.Write(e.message);

                    sw.WriteLine();
                }
                sw.WriteLine();
            }
        }
    }

    public class Error
    {
        internal ErrorLevel level;
        internal string message;

        internal Error(ErrorLevel level, string message)
        {
            this.level = level;
            this.message = message;
        }
    }

    public enum ErrorLevel
    {
        MissingFeature,
        IncorrectStructure,
        Critical,
        Crash
    }
}
