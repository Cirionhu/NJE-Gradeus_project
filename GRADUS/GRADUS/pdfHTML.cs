using GRADUS;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace GRADUS
{
    struct GradusALL
    {
        public int No;
        public int Vol;
        public string date;
        public string PdfURL;
        public string Abst;
        public string Section;
        public string Osszef;
        public string Cim;
        public string Email;
        public string Elso;
        public string Utolso;
        public string Doi;
        public string MTMT;
        public string PdfL;
        public string Szerzok;

        public GradusALL(string datef, int no, int vol, string pdfURL, string abst, string section, string osszef, string cim, string email, string elso, string utolso, string doi, string mtmt, string pdfL, string szerzok)
        {
            No = no;
            Vol = vol;
            date = datef;
            PdfURL = pdfURL;
            Abst = abst;
            Section = section;
            Osszef = osszef;
            Cim = cim;
            Email = email;
            Elso = elso;
            Utolso = utolso;
            Doi = doi;
            MTMT = mtmt;
            PdfL = pdfL;
            Szerzok = szerzok;
        }
    }

    internal class PdfHTML
    {
        string pdfFILE = "";
        string fossz = "";
        string fabs = "";
        string femail = "";
        string felso;
        string futolso;
        List<string> pdfElements = new List<string>();
        List<GradusALL> all = new List<GradusALL>();
        

        public PdfHTML(string url, int vol, int no, int date)
        {
            // Alap URL-ből abszolút URL létrehozása
            string baseUrl = "https://gradus.kefo.hu/archive/" + url;
            string datef = date.ToString();

            // Oldal tartalmának letöltése
            string htmlContent;
            using (WebClient client = new WebClient())
            {
                htmlContent = client.DownloadString(baseUrl);
            }

            // HTML betöltése
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);

            // H4 és Table elemek kigyűjtése
            var nodes = doc.DocumentNode.SelectNodes("//h4[@class='tocSectionTitle'] | //table[@class='tocArticle']");

            string currentSection = null;

            if (nodes != null)
            {
                foreach (var node in nodes)
                {
                    if (node.Name == "h4" && node.HasClass("tocSectionTitle"))
                    {
                        currentSection = node.InnerText.Trim();
                    }
                    else if (node.Name == "table" && node.HasClass("tocArticle"))
                    {
                        var PdfE = node.SelectSingleNode(".//td[@class='tocTitle']");
                        var auth = node.SelectSingleNode(".//td[@class='tocAuthors']");
                        var PDFr = node.SelectSingleNode(".//td[@class='tocGalleys']/a");
                        var tocpage = node.SelectSingleNode(".//td[@class='tocPages']");
                        var doiNode = node.SelectSingleNode(".//td[@class='tocAuthors']//span//a");

                        string tocP = tocpage != null ? tocpage.InnerHtml.Trim() : "Nincs megadva";
                        string[] oldalak = tocP.Split('-');
                        felso = oldalak.Length > 0 ? oldalak[0].Trim() : "Nincs megadva";
                        futolso = oldalak.Length > 1 ? oldalak[1].Trim() : "Nincs megadva";

                        string pdfR = PDFr != null ? PDFr.GetAttributeValue("href", string.Empty) : "Nincs link";
                        string doi = doiNode != null ? doiNode.GetAttributeValue("href", string.Empty) : "Nincs LINK";
                        string title = PdfE != null ? PdfE.InnerText.Trim() : "Hiba";
                        string authors = auth != null ? auth.InnerText.Trim() : "";
                       

                        // DOI hivatkozás eltávolítása az authors szövegből, ha van ilyen
                        if (!string.IsNullOrEmpty(doi))
                        {
                            int doiIndex = authors.IndexOf("https://doi.org/");
                            if (doiIndex != -1)
                            {
                                authors = authors.Substring(0, doiIndex).Trim();
                            }
                        }

                        // PDF elemzés hívása és hiba kezelése
                        try
                        {
                            string fullPdfUrl = "https://gradus.kefo.hu/archive/" + url + "/" + pdfR;
                            AnalyzePdf(fullPdfUrl);
                        }
                        catch (Exception ex)
                        {
                           // Debug.WriteLine($"Error handling PDF: {ex.Message}");
                        }

                        // Objektum hozzáadása a listához
                        all.Add(new GradusALL(datef, no, vol, pdfR, fabs, currentSection, fossz, title, femail, felso, futolso, doi, "", "", authors));
                    }
                }
            }
        }

        public List<GradusALL> GradusElements()
        {
            return all;
        }

        public void AnalyzePdf(string pdfUrl)
        {
            try
            {
                // PDF fájl letöltése
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(pdfUrl, "temp.pdf");
                }

                // Ellenőrizzük, hogy a fájl létezik-e és nem üres-e
                FileInfo fileInfo = new FileInfo("temp.pdf");
                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    throw new Exception("Downloaded PDF file is empty or does not exist.");
                }

                // PDF fájl elemzése
                using (PdfDocument pdfDoc = PdfDocument.Open("temp.pdf"))
                {
                    Page firstPage = pdfDoc.GetPage(1);
                    string firstPageText = ExtractTextFromPage(firstPage);

                    // Debug kimenet
                   // Debug.WriteLine($"Extracted text from PDF: {firstPageText}");

                    ExtractEmailAndSummaries(firstPageText);

                    // PDF fájl törlése
                    File.Delete("temp.pdf");
                }
            }
            catch (Exception ex)
            {
              //  Debug.WriteLine($"Error analyzing PDF: {ex.Message}");
            }
        }

        private string ExtractTextFromPage(Page page)
        {
            var text = ContentOrderTextExtractor.GetText(page);
            return text;
        }

        private void ExtractEmailAndSummaries(string text)
        {
            // Sorok szerint feldaraboljuk a szöveget
            var lines = text.Split('\n').Select(line => line.Trim()).ToList();

            // Lehetséges email cím kifejezések
            string[] emailPatterns = { "Email:", "E-mail cím:", "E-mail address:" };
            string email = null;

            // Email cím kinyerése
            foreach (var pattern in emailPatterns)
            {
                email = lines.FirstOrDefault(line => line.Contains(pattern))?.Split(' ').Last().Trim();
                if (!string.IsNullOrEmpty(email))
                {
                    break; // Ha találunk egy érvényes email címet, kilépünk a ciklusból
                }
            }

            // Debugging email extraction
          //  Debug.WriteLine($"Email extracted: {email}");

            // Összefoglalás és Abstract kinyerése
            int summaryStartIndex = lines.FindIndex(line => line.Equals("Összefoglalás", StringComparison.OrdinalIgnoreCase));
            int abstractStartIndex = lines.FindIndex(line => line.Equals("Abstract", StringComparison.OrdinalIgnoreCase));

            string summary = ExtractSectionText(lines, summaryStartIndex, abstractStartIndex);
            string abstractText = ExtractSectionText(lines, abstractStartIndex);

            // Debugging summary and abstract extraction
          //  Debug.WriteLine($"Summary extracted: {summary}");
           // Debug.WriteLine($"Abstract extracted: {abstractText}");

            fabs = abstractText;
            fossz = summary;
            femail = email;
        }

        private string ExtractSectionText(List<string> lines, int startIndex, int endIndex = -1)
        {
            if (startIndex == -1)
            {
                return string.Empty;
            }

            var sectionLines = new List<string>();
            for (int i = startIndex + 1; i < lines.Count; i++)
            {
                if (i == endIndex || string.IsNullOrWhiteSpace(lines[i])) break;
                sectionLines.Add(lines[i]);
            }

            return string.Join(" ", sectionLines);
        }
    }
}
