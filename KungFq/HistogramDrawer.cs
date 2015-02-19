using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace KungFq
{
    public class HistogramDrawer
    {
        public HistogramDrawer(string histogramFile)
        {
            svgWriter = new StreamWriter(File.Open(histogramFile+".svg", FileMode.Create));
            txtWriter = new StreamWriter(File.Open(histogramFile+".txt", FileMode.Create));
        }
        
        StreamWriter svgWriter;
        StreamWriter txtWriter;
        const int RECT_WIDTH = 50;
        const int HALF_RECT_WIDTH = RECT_WIDTH/2;
        const int xSPACE = 20;
        const int Y_LABELS_X = RECT_WIDTH*3;
        const int TEXT_Y_X = 30;
        const int LINE_Y_X = 130;
        const int LINE_X_Y = 1150;
        const long MAX_HEIGHT = 1000;
        const int HEIGHT = 1200;
        const int RECT_Y = 50;
        const string RECT_FILL = "#0099FF";
        const string RECT_STROKE = "#000000";
        const int FONT_SIZE = 20;
        const string FONT = "Verdana";
        
        public void Draw(QualityCounter qCounter)
        {
            XDocument doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "no")
            );
            
            XNamespace ns = "http://www.w3.org/2000/svg";
            XElement svg = new XElement(ns + "svg",
                                            new XAttribute("xmlns", "http://www.w3.org/2000/svg"),
                                            new XAttribute("height", HEIGHT),
                                            new XAttribute("fill", "white")
                                        );
            XElement box = new XElement(ns + "rect",
                                            new XAttribute("height", HEIGHT),
                                            new XAttribute("x", 0),   
                                            new XAttribute("y", 0), 
                                            new XAttribute("fill", "white")
                                        );
            
            svg.Add(box);
            doc.Add(svg);
            int maxHeight = qCounter.QualityCounts.Values.Max();
            int minHeight = qCounter.QualityCounts.Values.Min();
            
            int lastX = RECT_WIDTH*3;
            
            //<rect width="80" height="22" rx="5" x="1" y="1" fill="#eee" stroke="#707070" />
            foreach (var q in qCounter.QualityCounts.OrderBy(x => x.Key)) {
                txtWriter.WriteLine("{0}\t{1}", q.Key, q.Value);
                long wantedHeight = (MAX_HEIGHT*q.Value)/maxHeight;
                svg.Add(new XElement(ns + "rect",
                            new XAttribute("width", RECT_WIDTH),
                            new XAttribute("height", wantedHeight),
                            new XAttribute("x", lastX),   
                            new XAttribute("y", (LINE_X_Y) - wantedHeight), //1150 - WH
                            new XAttribute("fill", RECT_FILL),
                            new XAttribute("stroke", RECT_STROKE) 
                           )
                       );
                
                
                svg.Add(new XElement(ns + "text", 
                                     new XAttribute("x", lastX + HALF_RECT_WIDTH),   
                                     new XAttribute("y", LINE_X_Y + xSPACE),
                                     new XAttribute("font-size", FONT_SIZE),
                                     new XAttribute("fill", RECT_STROKE),
                                     new XAttribute("font-family", FONT), q.Key
                                 )
                        );

                //single rectangles counts
                /*XElement g = new XElement(ns + "g",
                                            new XAttribute("transform", "rotate(-45," + lastX +", " 
                                                                    + ((LINE_X_Y) - wantedHeight + 2) + ")") 
                                        );
                
                g.Add(new XElement(ns + "text", 
                                     new XAttribute("x", lastX),   
                                     new XAttribute("y", (LINE_X_Y) - wantedHeight - 4),
                                     new XAttribute("font-size", FONT_SIZE),
                                     new XAttribute("fill", RECT_STROKE),
                                     new XAttribute("font-family", FONT), q.Value
                                 )
                        );
                
                svg.Add(g);*/
                lastX += RECT_WIDTH + xSPACE;
            }
            //y axis
            svg.Add(new XElement(ns + "line", 
                                     new XAttribute("x1", LINE_Y_X),   
                                     new XAttribute("y1", LINE_X_Y - MAX_HEIGHT),
                                     new XAttribute("x2", LINE_Y_X),
                                     new XAttribute("y2", LINE_X_Y),
                                     new XAttribute("stroke-width", 3),
                                     new XAttribute("stroke", RECT_STROKE)
                                 )
                    );
           //x axis
           svg.Add(new XElement(ns + "line", 
                                     new XAttribute("x1", LINE_Y_X),   
                                     new XAttribute("y1", LINE_X_Y),
                                     new XAttribute("x2", (RECT_WIDTH + xSPACE) * qCounter.QualityCounts.Keys.Count + LINE_Y_X),
                                     new XAttribute("y2", LINE_X_Y),
                                     new XAttribute("stroke-width", 3),
                                     new XAttribute("stroke", RECT_STROKE)
                                 )
                    );
            
            int width =  (qCounter.QualityCounts.Keys.Count + 4)* (RECT_WIDTH + xSPACE);
            svg.SetAttributeValue("width", width);
            box.SetAttributeValue("width", width);
            
            svg.Add(new XElement(ns + "text", 
                                     new XAttribute("x", LINE_Y_X - 5),   
                                     new XAttribute("y", LINE_X_Y - MAX_HEIGHT - 4),
                                     new XAttribute("font-size", FONT_SIZE),
                                     new XAttribute("fill", RECT_STROKE),
                                     new XAttribute("font-family", FONT), "Counts"
                                 )
                    );
            
            svg.Add(new XElement(ns + "text", 
                                     new XAttribute("x", width/2),  
                                     new XAttribute("y", LINE_X_Y + xSPACE*2),
                                     new XAttribute("font-size", FONT_SIZE),
                                     new XAttribute("fill", RECT_STROKE),
                                     new XAttribute("font-family", FONT), "Quality values"
                                 )
                    );
            
            long half = ((long)maxHeight + minHeight) / 2; //beware of overflows
            
            /*XElement g = new XElement(ns + "g",
                                           new XAttribute("transform", "rotate(-45," + TEXT_Y_X +", " 
                                                                    + (LINE_X_Y - MAX_HEIGHT + 15) + ")") 
                                        );*/
                
            
            svg.Add(new XElement(ns + "text", 
                                     new XAttribute("x", TEXT_Y_X),   
                                     new XAttribute("y", LINE_X_Y - MAX_HEIGHT + 15),
                                     new XAttribute("font-size", FONT_SIZE),
                                     new XAttribute("fill", RECT_STROKE),
                                     new XAttribute("font-family", FONT), String.Format("{0:g2}", maxHeight)
                                )
                 );
            //svg.Add(g);
            
            /*XElement g2 = new XElement(ns + "g",
                                           new XAttribute("transform", "rotate(-45," + TEXT_Y_X +", " 
                                                                    + (HEIGHT - RECT_Y) + ")") 
                                        );*/
            svg.Add(new XElement(ns + "text", 
                                     new XAttribute("x", TEXT_Y_X),   
                                     new XAttribute("y", HEIGHT - RECT_Y),
                                     new XAttribute("font-size", FONT_SIZE),
                                     new XAttribute("fill", RECT_STROKE),
                                 new XAttribute("font-family", FONT), String.Format("{0:g2}", minHeight)
                                )
            );
            
            //svg.Add(g2);

            /*XElement g3 = new XElement(ns + "g",
                                            new XAttribute("transform", "rotate(-45," + TEXT_Y_X +", " 
                                                                    + (LINE_X_Y - (MAX_HEIGHT*half)/maxHeight) + ")") 
                                       );*/
            
            svg.Add(new XElement(ns + "text", 
                                    new XAttribute("x", TEXT_Y_X),   
                                    new XAttribute("y", (LINE_X_Y - (MAX_HEIGHT*half)/maxHeight)),
                                    new XAttribute("font-size", FONT_SIZE),
                                    new XAttribute("fill", RECT_STROKE),
                                    new XAttribute("font-family", FONT), String.Format("{0:g2}", half)
                               )
                    );
            
            //svg.Add(g3);

            doc.Save(svgWriter);
            svgWriter.Close();
            txtWriter.Close();
        }
        
    }
}

