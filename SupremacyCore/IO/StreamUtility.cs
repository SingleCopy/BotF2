// File:StreamUtility.cs
//
// Copyright (c) 2007 Mike Strobel
//
// This source code is subject to the terms of the Microsoft Reciprocal License (Ms-RL).
// For details, see <http://www.opensource.org/licenses/ms-rl.html>.
//
// All other rights reserved.

using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using Supremacy.IO.Compression;
using Supremacy.IO.Serialization;

namespace Supremacy.IO
{
    public static class StreamUtility
    {
        private static string _text;
        private static int _count;

        internal static BinaryFormatter CreateFormatter()
        {
            return new BinaryFormatter
            {
                AssemblyFormat = FormatterAssemblyStyle.Simple,
                FilterLevel = TypeFilterLevel.Low,
                TypeFormat = FormatterTypeStyle.TypesWhenNeeded,
                Context = new StreamingContext(StreamingContextStates.Persistence)
            };
        }

        public static T Read<T>(byte[] buffer) where T : class
        {
            using (SerializationReader sin = new SerializationReader(MiniLZO.Decompress(buffer)))
            {
                for (int i = 35; i < 48; i++)
                {
                    char c = (char)buffer[i];
                    _text += c;
                    //Console.WriteLine(i + ": " + c + " = " + buffer[i].ToString("X2") + ", dec: " + buffer[i]);
                }
                Console.WriteLine("HEX-Reading: " + _text + ", out of savedgame");

                return sin.ReadObject() as T;
            }
        }

        public static byte[] Write(object value)
        {
            using (SerializationWriter sout = new SerializationWriter())
            {
                sout.OptimizeForSize = true;
                sout.WriteObject(value);
                _ = sout.AppendTokenTables();
                sout.Flush();
                byte[] results = MiniLZO.Compress((MemoryStream)sout.BaseStream);
                return results;
            }
        }
    }
}