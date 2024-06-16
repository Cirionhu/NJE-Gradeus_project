using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using HtmlAgilityPack;

using System;
using System.Collections.Generic;
using System.Net;
using HtmlAgilityPack;
using Aspose.Pdf.Plugins;
using DocumentFormat.OpenXml.Office2013.Drawing.ChartStyle;

namespace GRADUS
{
    

    struct GradusURL
    {
        public int No;
        public int Vol;
        public int Date;
        public string DateM;
        public string DateS;
        public string PdfURL;

        public GradusURL(int no, int vol, int date, string dateM, string dateS, string pdfURL)
        {
            No = no;
            Vol = vol;
            Date = date;
            DateM = dateM;
            DateS = dateS;
            PdfURL = pdfURL;
        }
    }

    internal class HtmlRead
    {
        List<GradusURL> urls;
        List<string> h4Elements = new List<string>();
        public List<GradusALL> all = new List<GradusALL>();

        public HtmlRead()
        {
            urls = new List<GradusURL>();
            string url = "https://gradus.kefo.hu/archive/";

            // Oldal tartalmának letöltése
            string htmlContent;
            using (WebClient client = new WebClient())
            {
                htmlContent = client.DownloadString(url);
            }

            // HTML betöltése
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // H4 elemek és linkek kigyűjtése
            var h4Nodes = doc.DocumentNode.SelectNodes("//h4");

            if (h4Nodes != null)
            {
                foreach (var h4Node in h4Nodes)
                {
                    string text = h4Node.InnerText.Trim();
                    var linkNode = h4Node.SelectSingleNode(".//a");
                    string pdfURL = linkNode != null ? linkNode.GetAttributeValue("href", string.Empty) : string.Empty;

                    h4Elements.Add(text);

                    string[] parts = text.Split(new char[] { ' ', ',', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);

                    int vol = int.Parse(parts[1]);
                    int no = int.Parse(parts[3]);
                    int date = int.Parse(parts[4]);
                    string dateM = parts[5];
                    string dateS = string.Join(" ", parts, 6, parts.Length - 6);
                    urls.Add(new GradusURL(no, vol, date, dateM, dateS, pdfURL));
                    

                    // Létrehozunk egy PdfHTML objektumot és hozzáadjuk az elemeket az all listához
                    PdfHTML pdfHtml = new PdfHTML(pdfURL, vol, no, date);
                    all.AddRange(pdfHtml.GradusElements());
                }
            }
        }

        public List<GradusALL> GradusElements()
        {
            return all;
        }

        public List<string> RetrieveH4Elements()
        {
            return h4Elements;
        }

        public List<GradusURL> RetrieveGradusURLs()
        {
            return urls;
        }
    }
}




