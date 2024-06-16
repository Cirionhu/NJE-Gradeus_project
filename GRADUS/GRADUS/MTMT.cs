using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GRADUS
{
    public class PublicationItem
    {
        public List<Identifier> identifiers { get; set; }
        public string title { get; set; }
        public string link { get; set; } // Ez az MTMT link
    }

    public class Identifier
    {
        public string realUrl { get; set; }
    }

    public class PublicationResponse
    {
        public List<PublicationItem> content { get; set; }
    }

    public struct PublicationInfo
    {
        public string Title { get; set; }
        public List<string> Links { get; set; }
        public string MTMTLink { get; set; }

        public PublicationInfo(string title, string mtmtLink)
        {
            Title = title;
            Links = new List<string>();
            MTMTLink = mtmtLink;
        }
    }

    internal class MTMT
    {
        private List<GradusALL> titleToFind;
        private List<PublicationInfo> publications;
        private HttpClient client;
        private List<GradusALL> FinalALL;

        public MTMT(List<GradusALL> cim)
        {
            titleToFind = cim;
            publications = new List<PublicationInfo>();
            client = new HttpClient();
            FinalALL = new List<GradusALL>(); // Lista inicializálása
        }

        public string RealUrl { get; private set; }
        public string MTMTLink { get; private set; }

        public async Task GetDataAsync()
        {
            string apiUrl = "https://m2.mtmt.hu/api/publication?cond=published%3Beq%3Btrue&cond=journal.mtid%3Beq%3B10032898&ty_on=1&ty_on_check=1&st_on=1&st_on_check=1&url_on=1&url_on_check=1&cite_type=2&sort=publishedYear%2Cdesc&sort=firstAuthor%2Casc&size=5000&page=1&format=json";
            Debug.WriteLine("Az összes lista elem most: " + titleToFind.Count);
            try
            {
                HttpResponseMessage response = await client.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    // A JSON válasz deszerializálása PublicationResponse objektummá
                    PublicationResponse publicationResponse = JsonConvert.DeserializeObject<PublicationResponse>(jsonResponse);

                    // Címek tárolása a titleToFind listából egy HashSet-ben a gyorsabb keresés érdekében
                    HashSet<string> titleToFindSet = new HashSet<string>(titleToFind.Select(t => NormalizeString(t.Cim)));

                    // A tartalom elérése és feldolgozása
                    foreach (PublicationItem item in publicationResponse.content)
                    {
                        string normalizedItemTitle = NormalizeString(item.title);
                        if (!titleToFindSet.Contains(normalizedItemTitle))
                        {
                           // Debug.WriteLine($"Nincs benne a titleToFind listában: {item.title}");
                            continue; // Ugrás a következő iterációra
                        }

                        string urlR = "";
                        for (int i = 0; i < titleToFind.Count; i++)
                        {
                            // Ellenőrzés, hogy a cím megegyezik-e a keresett címmel, normálizálás után
                            if (normalizedItemTitle.Equals(NormalizeString(titleToFind[i].Cim), StringComparison.OrdinalIgnoreCase))
                            {
                                // PublicationInfo objektum létrehozása
                                PublicationInfo info = new PublicationInfo(item.title, item.link);

                                // Hivatkozások hozzáadása a PublicationInfo-hoz
                                if (item.identifiers != null)
                                {
                                    foreach (Identifier identifier in item.identifiers)
                                    {
                                        if (!string.IsNullOrEmpty(identifier.realUrl) && (identifier.realUrl.StartsWith("https://doi") || identifier.realUrl.StartsWith("http://doi")))
                                        {
                                            info.Links.Add(identifier.realUrl);
                                        }
                                        else if (!string.IsNullOrEmpty(identifier.realUrl) && (identifier.realUrl.StartsWith("https://real") || identifier.realUrl.StartsWith("http://real")))
                                        {
                                            info.Links.Add(identifier.realUrl);
                                            urlR = identifier.realUrl; // RealUrl beállítása
                                        }
                                    }
                                }

                                // GradusALL objektum hozzáadása a FinalALL listához
                                FinalALL.Add(new GradusALL(
                                    titleToFind[i].date,
                                    titleToFind[i].No,
                                    titleToFind[i].Vol,
                                    titleToFind[i].PdfURL,
                                    titleToFind[i].Abst,
                                    titleToFind[i].Section,
                                    titleToFind[i].Osszef,
                                    titleToFind[i].Cim,
                                    titleToFind[i].Email,
                                    titleToFind[i].Elso,
                                    titleToFind[i].Utolso,
                                    titleToFind[i].Doi,
                                    "https://m2.mtmt.hu/" + info.MTMTLink,
                                    urlR,
                                    titleToFind[i].Szerzok));

                                // PublicationInfo hozzáadása a listához
                                publications.Add(info);
                                break; // Ha megtalálta a megfelelő címet, kiléphet a belső ciklusból
                            }
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"Nem sikerült lekérni az adatokat. Státuszkód: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Hiba: {ex.Message}");
            }
            finally
            {
                client.Dispose(); // HttpClient lezárása
            }
        }

        // Normalize the strings for comparison
        private string NormalizeString(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Távolítsa el az összes pontot és fölösleges szóközt, majd kisbetűsítsen mindent
            char[] arr = input.ToLowerInvariant().ToCharArray();
            arr = Array.FindAll(arr, c => char.IsLetterOrDigit(c));
            return new string(arr);
        }

        // Getter metódus a FinalALL lista visszaadásához
        public List<GradusALL> GetFinalALL()
        {
            return FinalALL;
        }
    }
}
