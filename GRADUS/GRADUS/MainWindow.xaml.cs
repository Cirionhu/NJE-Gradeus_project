using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using OfficeOpenXml;

namespace GRADUS
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            startButton.IsEnabled = false;

            try
            {
                
                    startButton.Content = "Adatok kiolvasása a Grauds.hu oldalról";
               

                HtmlRead read = new HtmlRead();

                // MTMT objektum létrehozása a GradusElements visszatérési értékével
                MTMT mtmt = new MTMT(read.GradusElements());
                Debug.WriteLine("Előtte valami: " + read.GradusElements().Count);

                
                    startButton.Content = "Adatok kinyerése a MTMT oldalról";
               
                
                // Aszinkron adatok lekérése
                await mtmt.GetDataAsync();

                // Adatok elérése a FinalALL listában
                List<GradusALL> all = mtmt.GetFinalALL();
                startButton.Content = "Linkek ellenörzése";
                List<UrlChecker> linkStatuses = new List<UrlChecker>();

                // Linkek ellenőrzése és állapotok tárolása
                foreach (var item in all)
                {
                    if (item.Doi.Contains("http"))
                    {
                        linkStatuses.Add(new UrlChecker(item.Doi));
                    }
                    if (item.PdfL.Contains("http"))
                    {
                        linkStatuses.Add(new UrlChecker(item.PdfL));
                    }
                    if (item.MTMT.Contains("http"))
                    {
                        linkStatuses.Add(new UrlChecker(item.MTMT));
                    }
                }

                // Párhuzamos URL ellenőrzés
                using (var client = new HttpClient())
                {
                    var tasks = new List<Task>();
                    foreach (var checker in linkStatuses)
                    {
                        tasks.Add(checker.CheckUrlAsync(client));
                    }
                    await Task.WhenAll(tasks);
                }
                
                    startButton.Content = "Excel fájl létrehozása";
                
               
                
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*";
                saveFileDialog.FilterIndex = 1;
                saveFileDialog.RestoreDirectory = true;

                if (saveFileDialog.ShowDialog() == true)
                {
                    // Fájl neve
                    string filePath = saveFileDialog.FileName;

                    // Excel létrehozása
                    CreateExcelFile(all, linkStatuses, filePath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in StartButton_Click: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}");
            }
            startButton.IsEnabled = true;
        }

        private void CreateExcelFile(List<GradusALL> data, List<UrlChecker> linkStatuses, string filePath)
        {
            // Valóban van e ilyen hely
            if (string.IsNullOrWhiteSpace(filePath))
            {
                MessageBox.Show("Invalid file path.");
                return;
            }

            using (ExcelPackage package = new ExcelPackage())
            {
                // Adatok írása az első munkalapra
                ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Gradus Adatok");

                
                worksheet.Cells[1, 1].Value = "Évszám";
                worksheet.Cells[1, 2].Value = "Vol";
                worksheet.Cells[1, 3].Value = "No";
                worksheet.Cells[1, 4].Value = "PDF Fájlneve";
                worksheet.Cells[1, 5].Value = "Kezdő oldalszám";
                worksheet.Cells[1, 6].Value = "Záró oldalszám";
                worksheet.Cells[1, 7].Value = "Szerzők";
                worksheet.Cells[1, 8].Value = "Eredeti cím";
                worksheet.Cells[1, 9].Value = "Section";
                worksheet.Cells[1, 10].Value = "Absztrakt eredeti nyelven";
                worksheet.Cells[1, 11].Value = "Absztrakt angolul";
                worksheet.Cells[1, 12].Value = "E-mail címe";
                worksheet.Cells[1, 13].Value = "MTA REPO URL (Reál)";
                worksheet.Cells[1, 14].Value = "DOI";
                worksheet.Cells[1, 15].Value = "MTMT link";

                // Adat
                for (int i = 0; i < data.Count; i++)
                {
                    var item = data[i];
                    worksheet.Cells[i + 2, 1].Value = item.date; 
                    worksheet.Cells[i + 2, 2].Value = item.Vol;
                    worksheet.Cells[i + 2, 3].Value = item.No;
                    worksheet.Cells[i + 2, 4].Value = System.IO.Path.GetFileNameWithoutExtension(item.PdfURL);
                    worksheet.Cells[i + 2, 5].Value = item.Elso;
                    worksheet.Cells[i + 2, 6].Value = item.Utolso;
                    worksheet.Cells[i + 2, 7].Value = item.Szerzok;
                    worksheet.Cells[i + 2, 8].Value = item.Cim;
                    worksheet.Cells[i + 2, 9].Value = item.Section;
                    worksheet.Cells[i + 2, 10].Value = item.Osszef;
                    worksheet.Cells[i + 2, 11].Value = item.Abst;
                    worksheet.Cells[i + 2, 12].Value = item.Email;
                    worksheet.Cells[i + 2, 13].Value = item.PdfL;
                    worksheet.Cells[i + 2, 14].Value = item.Doi;
                    worksheet.Cells[i + 2, 15].Value = item.MTMT;
                }

                // Létrehozunk egy új munkalapot a linkek státuszának mentésére
                ExcelWorksheet linkWorksheet = package.Workbook.Worksheets.Add("Link allapot");

                
                linkWorksheet.Cells[1, 1].Value = "Link";
                linkWorksheet.Cells[1, 2].Value = "Elerhetoseg";

                // Adat a linkeknek
                for (int i = 0; i < linkStatuses.Count; i++)
                {
                    var status = linkStatuses[i];
                    linkWorksheet.Cells[i + 2, 1].Value = status.Url;
                    linkWorksheet.Cells[i + 2, 2].Value = status.IsAccessible ? "Elerheto" : "Nem elerheto";
                }

                // Létrehozunk egy új munkalapot a kimutatás mentésére
                ExcelWorksheet summaryWorksheet = package.Workbook.Worksheets.Add("Osszefoglalas");

                // Kimutatás készítése
                var summaryData = data
                    .GroupBy(x => new { x.date, x.Vol, x.No, x.Section })
                    .Select(g => new
                    {
                        Year = g.Key.date,
                        Volume = g.Key.Vol,
                        Number = g.Key.No,
                        Section = g.Key.Section,
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Year)
                    .ThenBy(x => x.Volume)
                    .ThenBy(x => x.Number)
                    .ThenBy(x => x.Section)
                    .ToList();

                
                summaryWorksheet.Cells[1, 1].Value = "Évszám";
                summaryWorksheet.Cells[1, 2].Value = "Vol";
                summaryWorksheet.Cells[1, 3].Value = "No";
                summaryWorksheet.Cells[1, 4].Value = "Section";
                summaryWorksheet.Cells[1, 5].Value = "Cikkek száma";

                // Adat az osszefoglaláshoz
                for (int i = 0; i < summaryData.Count; i++)
                {
                    var item = summaryData[i];
                    summaryWorksheet.Cells[i + 2, 1].Value = item.Year;
                    summaryWorksheet.Cells[i + 2, 2].Value = item.Volume;
                    summaryWorksheet.Cells[i + 2, 3].Value = item.Number;
                    summaryWorksheet.Cells[i + 2, 4].Value = item.Section;
                    summaryWorksheet.Cells[i + 2, 5].Value = item.Count;
                }

                // Létrehozunk egy új munkalapot az egyes szerzők cikkszámainak mentésére
                ExcelWorksheet authorWorksheet = package.Workbook.Worksheets.Add("Szerzok Osszefoglalas");

               // Author summary data collection
                Dictionary<string, int> authorCounts = new Dictionary<string, int>();

            
                foreach (var item in data)
                {
                   
                    string[] authors = item.Szerzok.Split(',');

                 
                    foreach (var author in authors)
                    {
                   
                        string trimmedAuthor = author.Trim();

                    
                        if (!authorCounts.ContainsKey(trimmedAuthor))
                        {
                            authorCounts[trimmedAuthor] = 1;
                        }
                        else
                        {
                            
                            authorCounts[trimmedAuthor]++;
                        }
                    }
                }

              
                authorWorksheet.Cells[1, 1].Value = "Szerző neve";
                authorWorksheet.Cells[1, 2].Value = "Cikkek száma";

                // Szerzők
                int rowIndex = 2;
                foreach (var kvp in authorCounts)
                {
                    authorWorksheet.Cells[rowIndex, 1].Value = kvp.Key;
                    authorWorksheet.Cells[rowIndex, 2].Value = kvp.Value;
                    rowIndex++;
                }

          
                FileInfo fileInfo = new FileInfo(filePath);
                package.SaveAs(fileInfo);
            }

            MessageBox.Show("Excel file created successfully!");
        }

    }
}
