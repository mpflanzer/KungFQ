// 
//  Main.cs
//  
//  Author:
//       Elena Grassi <grassi.e@gmail.com>
// 
//  Copyright (c) 2010 Elena Grassi
// 
//  This program is free software; you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation; either version 2 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU General Public License for more details.
//  
//  You should have received a copy of the GNU General Public License
//  along with this program; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
// 
using System;
using Mono.Options;
using System.IO;
using System.Collections.Generic;
using ICSharpCode.SharpZipLib.GZip;
using SevenZip;

namespace KungFq
{
    class MainClass
    {
        
        public static int Main(string[] args)
        {

            bool showHelp = false;
            bool encodeIds = true;
            int length = 0;
            string filename = "";
            string compression = "gzip";
            string mode = "";
            string prefix = "";
            string suffix = ".qfq";
            int cutoff = -1;
            bool encodeQualities = true;

            var p = new OptionSet () {
                { "m|mode=", "the mode: encode|decode",
                    v => mode = v },
                { "l|length=", "the length of the reads",
                   (int v) => length = v },
                { "z|compression=", "the compression method to use: none|gzip|lzma - default is gzip",
                   v => compression = v },
                { "p|prefix=", "the prefix for the output file",
                   v => prefix = v },
                { "i|noId", "do not encode/decode Ids",
                   v => encodeIds = v == null },
                { "c|cutoff=", "cutoff to be used when encoding losing qualities -\n" +
                               "bases associated with a quality lower than the cutoff will be encoded as\n" +
                               "N",
                   (int v) => cutoff = v},
                { "q|noQuality", "do not encode/decode qualities - will use cutoff if given",
                   v => encodeQualities = v == null },
                { "h|help", "show this message and exit",
                   v => showHelp = v != null },
            };

            bool stop = false;
            List<string> extraArgs = null;
            string e = "";
            try {
                extraArgs = p.Parse(args);
            }
            catch (OptionException oe) {
                stop = true;
                e = oe.Message;
            }

            if (length <= 0 || mode == "" || (mode == "encode" && prefix == ""))
                stop = true;
            
            if (mode == "encode" && cutoff != -1 && encodeQualities) {
                stop = true;
                e = "In encode mode -c option can be used only with -q option!";
            }
            
            if (extraArgs.Count <= 1) {
                if (extraArgs.Count != 0)
                    filename = extraArgs[0];
            } else {
                stop = true;
            }

            if (showHelp) {
                ShowHelp(p);
                return 0;
            }
            if (stop) {
                Console.WriteLine(e);
                ShowHelp(p);
                return -1;
            }

            string outputFile = prefix + suffix;
            if (compression == "gzip") {
                outputFile += ".gz";
            } else if (compression == "lzma") {
                outputFile += ".lzma";
            }    
            

            IFastqDeCompresser fdc = new FastqDeCompresser(length, encodeIds, encodeQualities);

            try {
                if (mode == "encode") {
                    FileStream outStream = new FileStream(outputFile, FileMode.Create);
                    BinaryWriter writer = null;
                    Stream zipWriter = null;
                    try {
                        if (compression == "none") {
                            writer = new BinaryWriter(outStream);
                        } else if (compression == "gzip") {
                            zipWriter = new GZipOutputStream(outStream);
                            writer = new BinaryWriter(zipWriter);
                        }  else if (compression == "lzma") {
                            zipWriter = new LzmaEncodeStream(outStream);
                            writer = new BinaryWriter(zipWriter);
                        } else {
                            Console.Error.WriteLine("Wrong compression method given");
                            ShowHelp(p);
                            return -1;
                        }
                        FastqReader reader = null;
                        if (filename != "") {
                            if (cutoff == -1) {
                                reader = new FastqReader(filename, length);
                            } else {
                                reader = new FastqCutoffReader(filename, length, cutoff);
                            }       
                        } else {
                            if (cutoff == -1) {
                                reader = new FastqReader(Console.In, length);
                            } else {
                                reader = new FastqCutoffReader(Console.In, length, cutoff);
                            }
                        }
                        fdc.Compress(reader, writer);
                        reader.Close();
                    } finally {
                        writer.Close();
                        outStream.Close();
                        //XXX should close correct streams!
                    }
                } else if (mode == "decode") {
                    EncodedFastqReader reader = new EncodedFastqReader(File.OpenRead(outputFile), compression);
                    StreamWriter writer = new StreamWriter(Console.OpenStandardOutput());
                    try {
                        fdc.Decompress(reader, writer);
                    } finally {
                        reader.Close();   
                        writer.Close();
                    }
                } else {
                    ShowHelp(p);
                    return -1;
                }
            } catch (InvalidOperationException ioe) {
                Console.Error.WriteLine(ioe.Message);
                return 1;
            } catch (FileNotFoundException fnfe) {
                Console.Error.WriteLine("File {0} not found {1}!", filename, fnfe.Message);
                return 1;
            } /*finally {
            // TODO ASK    
            }*/

            return 0;
        }

        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: {0} [Options] fastq|compressed fastq", Environment.GetCommandLineArgs()[0]);
            Console.WriteLine("The input file could also be in the standard input.");
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}

