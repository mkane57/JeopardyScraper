using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using AngleSharp.Dom;
using JeopardyScraper.JeopardyObjects;

namespace JeopardyScraper
{
    /// <summary>
    /// This application was written by a team of UC Berkeley students for a final project for a Data Visualization class in order
    /// to scrape the information from the J!Archive (http://www.j-archive.com) website that holds nearly every clue for every game
    /// of Jeopardy ever played.  This code is open sourced and offered freely under the MIT license.  Enjoy!
    /// 
    /// Authors:
    /// Matt Kane (mattkane@berkeley.edu)
    /// Elena Petrov
    /// Justin Stanley
    /// </summary>
    class Program
    {
        const string allSeasonsUrl = "http://www.j-archive.com/listseasons.php";

        static void Main(string[] args) {

            Console.WriteLine("Hello World, we are scraping Jeopardy! Archive...");

            using (HttpClient httpClient = new HttpClient()) {

                // We need to get all of the seasons from the J!Archive site's list of seasons                
                IHtmlDocument allSeasonsDocument = GetAllSeasonsDocument(httpClient, allSeasonsUrl);                
                List<Season> seasons = GetSeasons(allSeasonsUrl, allSeasonsDocument);
                Console.WriteLine("Retrieved List of all {0} Seasons.", seasons.Count);
                                
                // Now process the seasons we collected from the All Seasons page
                string outputFilePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                //List<string> failedGames = ProcessGamesInSeasons(httpClient, seasons, new List<int>() { 6229, 6227 });
                List<string> failedGames = ProcessSeasons(httpClient, seasons, outputFilePath, false);

                // Print out any URLs for Games that failed to load
                Console.WriteLine();
                Console.WriteLine("URLs for Games that Failed to Load without any errors...");
                foreach (string failedGame in failedGames) {
                    Console.WriteLine(failedGame);
                }

                Console.ReadLine();
            }
        }

        /// <summary>
        /// Mostly a testing function for easily Processing specific Games by GameId for focused debugging on edge cases.
        /// </summary>
        /// <param name="httpClient">HttpClient to downloading from the internet</param>
        /// <param name="seasons">List of Seasons to process [from GetSeasons() function]</param>
        /// <param name="gameIds">List of GameIds to specifically process</param>
        /// <returns>List of URLs for games we were unable to process successfully.</returns>
        private static List<string> ProcessGamesInSeasons(HttpClient httpClient, List<Season> seasons, List<int> gameIds) {
            List<string> failedGames = new List<string>();

            // for debugging when focusing on a single season
            //string[] seasonsToProcess = new string[] { "Season 20" };

            foreach (Season season in seasons) {

                // for debugging when focusing on a single season
                //if (seasonsToProcess.Contains(season.SeasonName) == false) { continue; }

                Console.WriteLine("Processing a Games for Season - {0}", season.ToString());
                failedGames.AddRange(season.ProcessGamesForSeason(httpClient, gameIds));
            }
            return RemoveFailedGamesThatAreKnownIncomplete(failedGames);
        }

        /// <summary>
        /// Main Process function that loops through all Seasons and collects all of the games for that season (along with all the clues, etc.)
        /// and finishes by writing the collected data to disk in a JSON file(s).  The function can write each season as it's own JSON file
        /// or a single file with all of the seasons.  Finally, it returns a list of URLs for the Games within the seasons it encountered soem 
        /// type of error while processing
        /// </summary>
        /// <param name="httpClient">HttpClient to downloading from the internet</param>
        /// <param name="seasons">List of Seasons to process [from GetSeasons() function]</param>
        /// <param name="filePath">Directory where the file(s) will be written.  If a single file, it will be jeopardy_all_seasons.json.  
        /// Otherwise it will have a file per season using the SeasonName from J!Archive.</param>
        /// <param name="saveFilePerSeason">Bool determining if we will save a file per season or a single all seasons file.</param>
        /// <returns>List of URLs for games we were unable to process successfully.</returns>
        private static List<string> ProcessSeasons(HttpClient httpClient, List<Season> seasons, string filePath, bool saveFilePerSeason = true) {
            List<string> failedGames = new List<string>();

            // for debugging of focusing on a single season
            //string[] seasonsToProcess = new string[] { "Season 1", "Season 2", "Season 3" };

            foreach (Season season in seasons) {

                // for debugging of focusing on a single season
                //if (seasonsToProcess.Contains(season.SeasonName) == false) { continue; }

                Console.WriteLine();
                Console.WriteLine("****************************************************************************************");
                Console.WriteLine("Processing Games for Seasons - {0}", season.ToString());
                Console.WriteLine("****************************************************************************************");
                Console.WriteLine();                

                failedGames.AddRange(season.ProcessGamesForSeason(httpClient));

                if (saveFilePerSeason) {
                    Console.WriteLine();
                    Console.WriteLine("Writing JSON Data to Disk for Season {0}...", season.SeasonName);
                    string fileName = Path.Combine(filePath, MakeSafeFilename(season.SeasonName, ' ') + ".json");
                    List<Season> seasonToWrite = new List<Season>() { season };
                    WriteGamesToJson(fileName, seasonToWrite);
                    Console.WriteLine("Finished Writing JSON to Disk.");
                }
            }

            if (saveFilePerSeason == false) {
                Console.WriteLine();
                Console.WriteLine("Writing JSON Data to Disk for All Season...");
                string fileName = Path.Combine(filePath, "jeopardy_all_seasons.json");
                WriteGamesToJson(fileName, seasons);
                Console.WriteLine("Finished Writing JSON to Disk.");
            }
            return RemoveFailedGamesThatAreKnownIncomplete(failedGames);
        }

        

        /// <summary>
        /// Reads the list of seasons available at the URL for all the seasons by reading a preloaded DOM of
        /// the HtmlDocument from that URL
        /// </summary>
        /// <param name="allSeasonsUrl">URL to all seasons</param>
        /// <param name="htmlDocument">Preloaded DOM HtmlDocument for the URL for all seasons</param>
        /// <returns>List of Seasons objects (that have not Loaded the Games for the Season)</returns>
        private static List<Season> GetSeasons(string allSeasonsUrl, IHtmlDocument htmlDocument) {
            List<Season> seasons = new List<Season>();
            
            IElement div = htmlDocument.All.Where(x => x.Id == "content").FirstOrDefault();
            IElement table = div.FirstElementChild;
            var seasonElements = table.GetElementsByTagName("tr");

            foreach (var seasonElement in seasonElements) {
                Season season = new Season(allSeasonsUrl, seasonElement);
                seasons.Add(season);
            }                   
            
            return seasons;
        }

        /// <summary>
        /// Writes a list of Season objects to disk as a Json file with the appropriate serialization settings.
        /// </summary>
        /// <param name="fileName">Output JSON file name</param>
        /// <param name="seasons">List of Seasons to write to disk</param>
        private static void WriteGamesToJson(string fileName, List<Season> seasons) {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings {
                NullValueHandling = NullValueHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Ignore,
                Formatting = Formatting.Indented,
                DateFormatString = "yyyy-MM-dd"
            };
            serializerSettings.Converters.Add(new StringEnumConverter());

            string jsonString = JsonConvert.SerializeObject(seasons, serializerSettings);
            File.WriteAllText(fileName, jsonString);
        }

        #region Helper Functions

        /// <summary>
        /// Helper function to Process a URL and return the IHtmlDocument DOM object after it downloads.
        /// </summary>
        /// <param name="httpClient">HttpClient to downloading from the internet</param>
        /// <param name="url">URL to download from the internet</param>
        /// <param name="ct"></param>
        /// <returns>HtmlDocument DOM object for processing</returns>
        internal static IHtmlDocument ProcessUrl(HttpClient httpClient, string url) {
            // GetAsync returns a Task<HttpResponseMessage>. Since we are running an async process on 
            // a sync thread we need to wrap in Task.Run()
            var task = Task.Run(async () => await httpClient.GetAsync(url));
            task.Wait();
            HttpResponseMessage response = task.Result;

            // Retrieve the website contents from the HttpResponseMessage.  
            var task2 = Task.Run(async () => await response.Content.ReadAsStringAsync());
            task2.Wait();
            string content = task2.Result;

            HtmlParser parser = new HtmlParser();
            return parser.ParseDocument(content);
        }

        /// <summary>
        /// Helper function to return the DOM HtmlDocument object needed to read all of the seasons 
        /// from the URL for all Seasons.  Made so that the IHtmlDocument can be passed into the GetSeasons() function
        /// </summary>
        /// <param name="httpClient">HttpClient to retrieve information from the internet</param>
        /// <param name="allSeasonsUrl">URL to all seasons</param>
        /// <returns>IHtmlDocument DOM object that can be passed into GetSeasons() function</returns>
        private static IHtmlDocument GetAllSeasonsDocument(HttpClient httpClient, string allSeasonsUrl) {
            using (var task = Task.Run(() => httpClient.GetAsync(allSeasonsUrl))) {
                task.Wait();
                HttpResponseMessage httpResponse = task.Result;
                using (HttpContent content = httpResponse.Content) {

                    var task2 = Task.Run(() => content.ReadAsStringAsync());
                    task2.Wait();
                    string result = task2.Result;

                    HtmlParser parser = new HtmlParser();
                    return parser.ParseDocument(result);
                }
            }
        }

        /// <summary>
        /// Helper function to remove invalid characters and replace spaces with underscores (_) in a file name we
        /// are writing to disk
        /// </summary>
        /// <param name="filename">File name to make safe</param>
        /// <param name="replaceChar">char to replace invalid characters with</param>
        /// <returns></returns>
        private static string MakeSafeFilename(string filename, char replaceChar) {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) {
                filename = filename.Replace(c, replaceChar);
            }
            return filename.Replace("  ", " ").Replace(" ", "_").ToLower();
        }

        /// <summary>
        /// Removes from the Failed Games list any games we know are incomplete on the website (and thus the error is justified)
        /// </summary>
        /// <param name="failedGames">List of Failed Games from our Processing of Games</param>
        /// <returns>List of failed games that aren't known to be flawed</returns>
        private static List<string> RemoveFailedGamesThatAreKnownIncomplete(List<string> failedGames) {
            Queue<string> failedQueue = new Queue<string>(failedGames);
            List<string> stillFailedGames = new List<string>();
            while (failedQueue.Count > 0) {
                string failedGame = failedQueue.Dequeue();
                if (KnownIncompleteGames.Contains(failedGame) == false) {
                    stillFailedGames.Add(failedGame);
                }
            }
            return stillFailedGames;
        }

        /// <summary>
        /// List of known games thare are not complete on the J!Archive site (they are missing at least one round)
        /// </summary>
        private static List<string> incompleteGames = new List<string>();
        private static List<string> KnownIncompleteGames {
            get {
                if (incompleteGames.Count == 0) {

                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6227");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6226");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6224");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6223");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=3576");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=3575");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1752");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1748");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1736");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1734");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1733");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1165");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1139");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1138");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1137");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=3552");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1151");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1153");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6361");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1133");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=320");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6359");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6358");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6362");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4983");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1134");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6317");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6085");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4760");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4759");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4758");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4757");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4767");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4766");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4765");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4764");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4763");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1135");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6054");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6065");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4960");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=5361");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6064");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=1132");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=5348");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6067");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6061");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6089");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4246");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4264");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=6056");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4284");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4256");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=5773");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4273");
                    incompleteGames.Add("http://www.j-archive.com/showgame.php?game_id=4271");
                }
                return incompleteGames;

            }
        }

        #endregion Helper Functions

    }
}
