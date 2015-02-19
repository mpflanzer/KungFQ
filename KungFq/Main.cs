//  
//  Main.cs
//  
//  Author:
//       Elena Grassi <grassi.e@gmail.com>
// 
//  Copyright (c) 2010 Elena Grassi
// 
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
// 
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.

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
        static int BUFFER = 1048575;
        
        public static int Main(string[] args)
        {

            bool showHelp = false;
            bool encodeIds = true;
            bool encodeQualities = true;
            string histogram = "";
            int length = 0;
            string filename = "";
            string compression = "gzip";
            string mode = "";
            int cutoff = -1;

            var p = new OptionSet () {
                { "m|mode=", "the mode: encode|decode",
                    v => mode = v },
                { "l|length=", "the length of the reads",
                   (int v) => length = v },
                { "z|compression=", "the compression method to use: none|gzip|lzma - default is gzip",
                   v => compression = v },
                { "i|noId", "do not encode/decode Ids",
                   v => encodeIds = v == null },
                { "c|cutoff=", "cutoff to be used when encoding losing qualities -\n" +
                               "bases associated with a quality lower than the cutoff will be encoded as\n" +
                               "N",
                   (int v) => cutoff = v},
                { "q|noQuality", "do not encode/decode qualities - will use cutoff if given",
                   v => encodeQualities = v == null },
                { "s|qualityStats=", "create a SVG with a graph of fastq qualities and a .txt with quality values\n" +
                                     "associated with counts, the given parameter is\n" +
                                     "the desired basename of files (Warning: if they exist they will be REWRITTEN)\n" +
                                     "will have effects alone or when in encode mode",     
                   (string v) => histogram = v},
                { "h|help", "show this message and exit",
                   v => showHelp = v != null },
            };

            Boolean stop = false;
            List<string> extraArgs = null;
            string e = "";
            try {
                extraArgs = p.Parse(args);
            }
            catch (OptionException oe) {
                stop = true;
                e = oe.Message;
            }

            if ((length <= 0 || mode == "") && (histogram == "")) {
                Console.Error.WriteLine("Wrong (or no) length given or missing mode without the s option");
                stop = true;
            }

            if (!stop && extraArgs.Count <= 1) {
                if (extraArgs.Count != 0)
                    filename = extraArgs[0];
            } else {
                stop = true;
            }
            
            if (mode == "decode" && histogram != "") {
                Console.Error.WriteLine("Warning! The option -s has no effect when decoding a file!");   
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
         
            Stream output = Console.OpenStandardOutput();
            
            if (compression == "lzma") {
                if (BitConverter.IsLittleEndian == false) {
                    throw new Exception("Lzma compression not implemented for big endian machines.");
                }
                if (filename == "") {
                    if (mode == "decode") {
                        throw new InvalidOperationException("When decoding lzma files stdin cannot be used as input! " +
                                                        "Use a straight file instead.");  
                    } else {
                        throw new InvalidOperationException("When encoding with lzma stdout cannot be used as output! " +
                                                        "Use a straight file instead.");  
                    }
                }
                if (mode == "encode")
                    output = new FileStream(filename, FileMode.Create);
            }
            
            
            IFastqDeCompresser fdc = new FastqDeCompresser(length, encodeIds, encodeQualities);          
           
            try {
                if (mode == "encode") {   
                    if (cutoff != -1 && !encodeQualities)
                        encodeQualities = true;
                    //we need to store qualities to put N in sequences when -c has been used!
                    BinaryWriter writer = null;
                    Stream zipWriter = null;
                    if (compression == "none") {
                        writer = new BinaryWriter(output);
                    } else if (compression == "gzip") {
                        zipWriter = new GZipOutputStream(output, BUFFER);
                        writer = new BinaryWriter(zipWriter);
                    } else if (compression == "lzma") {
                        zipWriter = new LzmaStream(output, false);
                        writer = new BinaryWriter(zipWriter);
                    } else {
                        Console.Error.WriteLine("Wrong compression method given");
                        ShowHelp(p);
                        return -1;
                    }
                    FastqReader reader = null;
                    if (filename != "" && compression != "lzma") {
                        if (cutoff == -1) {
                            reader = new FastqReader(filename, length, encodeIds, encodeQualities, histogram);
                        } else {
                            reader = new FastqCutoffReader(filename, length, encodeIds, encodeQualities, cutoff, histogram);
                        }   
                    } else {
                        if (cutoff == -1) {
                            reader = new FastqReader(Console.In, length, encodeIds, encodeQualities, histogram);
                        } else {
                            reader = new FastqCutoffReader(Console.In, length, encodeIds, encodeQualities, cutoff, histogram);
                        }
                    }
                    fdc.Compress(reader, writer);
                    reader.Close();
                    writer.Close();
                } else if (mode == "decode") {
                    EncodedFastqReader reader = null;
                    StreamWriter writer = new StreamWriter(Console.OpenStandardOutput());
                    if (filename != "") {
                        reader = new EncodedFastqReader(File.OpenRead(filename), compression);
                    } else {
                        reader = new EncodedFastqReader(Console.OpenStandardInput(), compression);
                    }
                    fdc.Decompress(reader, writer);
                    reader.Close();
                    writer.Close();
                } else {
                    if (histogram == "") {
                        Console.Error.WriteLine("Wrong or missing mode argument!");
                        ShowHelp(p);
                        return -1;
                    } else {
                        FastqReader fq;
                        if (filename != "") {
                            fq = new FastqReader(filename, histogram);
                        } else {
                            fq = new FastqReader(Console.In, histogram);
                        }
                        fq.Run();
                        fq.Close();
                    }
                }
            } catch (InvalidOperationException ioe) {
                Console.Error.WriteLine(ioe.Message);
                return 1;
            } catch (FileNotFoundException fnfe) {
                Console.Error.WriteLine("File {0} not found {1}!", filename, fnfe.Message);
                return 1;
            }

            return 0;
        }

        public static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[32768];
            while (true) {
                int read = input.Read(buffer, 0, buffer.Length);
            if (read <= 0)
                return;
            output.Write(buffer, 0, read);
        }
}

        
        static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: {0} [Options] fastq|compressed fastq", Environment.GetCommandLineArgs()[0]);
            Console.WriteLine("When using -z none or gzip the input ad output file could be given as stdin and stdout,\n"+
                              "for -z lzma the compressed filename has to be the last argument and the fastq the stdin\n"+
                              "when encoding and the same when decoding (with the resulting fastq printed in the stdout)");
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}

