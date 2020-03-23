using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Linq;
using Flurl;
using AngleSharp.Html.Dom;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;

namespace JeopardyScraper.JeopardyObjects
{
    /// <summary>
    /// Class used to represent a Season of Jeopardy, which is a list of Games
    /// </summary>
    public class Season : IComparable<Season>
    {
        /// <summary>
        /// Loads a Season's basic information (except for all the Games) from the Season's page.
        /// </summary>
        /// <param name="baseUrl">The base URL for helping complete any relative URLs</param>
        /// <param name="season">HtmlDocument DOM for the Season's page.</param>
        public Season(string baseUrl, IElement season) {
            Games = new List<Game>();

            // Get the season name
            IElement anchor = season.GetElementsByTagName("a").FirstOrDefault();
            if (anchor.FirstElementChild == null) {
                this.SeasonName = anchor.TextContent;
            }
            else {
                // special seasons they put in an italic tag
                this.SeasonName = anchor.FirstElementChild.TextContent;
            }
            
            // Save our URL for later use
            string seasonUrl = anchor.Attributes["href"].Value;
            this.SeasonUrl = Url.Combine(Url.GetRoot(baseUrl), seasonUrl);

            // Grab the date range for the season
            string dateRange = anchor.ParentElement.NextElementSibling.TextContent;
            if (dateRange.Contains("and")) {
                // this is a special case for the pilot season
                dateRange = dateRange.Replace("and", "to").Replace("?", "-1-1");
            }
            string[] dateRangeSplit = dateRange.Split("to");
            this.StartDate = DateTime.Parse(dateRangeSplit[0].Trim());
            this.EndDate = DateTime.Parse(dateRangeSplit[1].Trim());

        }

        /// <summary>
        /// Processes all of the Games for this Season and load them into the Games list.
        /// </summary>
        /// <param name="httpClient">HttpClient to downloading from the internet</param>
        /// <param name="gamesToProcess">List of GameIds to process.  If null, we will process all games in the season.</param>
        /// <returns>List of Game URLs that failed to load</returns>
        public List<string> ProcessGamesForSeason(HttpClient httpClient, List<int> gamesToProcess = null) {
            List<string> failedGames = new List<string>();

            // download the DOM for the list of all the games in a season
            IHtmlDocument seasonDocument = Program.ProcessUrl(httpClient, this.SeasonUrl);

            // grab the html table of all the game links
            var tableOfGames = seasonDocument.All.Where(x => x.ClassName == "season").FirstOrDefault().NextElementSibling;

            // grab all of the anchor tags that exist in the table of games for the season
            foreach (var anchor in tableOfGames.GetElementsByTagName("a")) {

                string gameUrl = anchor.Attributes["href"].Value;
                string anchorText = anchor.InnerHtml;

                // there are occationaly links in the description to point to other games, so we only care about links to games that have the word 'aired' or 'taped'
                if (anchorText.Contains("aired") == false && anchorText.Contains("taped") == false) {
                    continue;
                }

                // if they used a relative link, we want to get the full link
                if (gameUrl.StartsWith("http") == false) {
                    // it looks like we got a relative url, let's fix it
                    gameUrl = Url.Combine(Url.GetRoot(SeasonUrl), gameUrl);
                }

                // there are anchor tags that point to media (jpg and youtube videos) on the page that get picked up sometimes
                // so this will skip all of those
                if (gameUrl.Contains("showgame.php") == false) {
                    continue;
                }
                
                // this is available if we want to test a certain set of games by id
                if (gamesToProcess != null) {
                    int gameId = Game.GetGameIdFromUrl(gameUrl);
                    if (gameId == -1) {
                        continue;
                    }
                    if (gamesToProcess.Contains(gameId) == false) {
                        continue;
                    }
                }

                string gameDesc = string.Empty;
                // we also need to go up and over to get the game description, so we will take the anchor's parent, then it's second sibling to get the 
                // game description
                try {
                    gameDesc = anchor.ParentElement.NextElementSibling.NextElementSibling.TextContent.Replace('\n', ' ').Trim();
                }
                catch (Exception ex) {
                    ex.ToString();
                    Console.WriteLine("*** Minor error attempting to get Game Desc, we will ignore this.");
                    Console.WriteLine();
                }

                // download the DOM document for the game
                IHtmlDocument gameDocument = Program.ProcessUrl(httpClient, gameUrl);
                Console.WriteLine("Downloaded Game Url - {0}", gameUrl);

                // process the game and store any failed game loads
                try {                    
                    Game game = new Game(gameUrl, gameDesc, gameDocument);                    
                    Games.Add(game);
                    Console.WriteLine("Successfully Processed Game - {0}", game.ToString());

                    if (game.IncompleteLoad) {
                        failedGames.Add(gameUrl);
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine();
                    Console.WriteLine("*** Failed to Process Game for Url - {0}", gameUrl);
                    Console.WriteLine("Error Message: {0}", ex.Message);
                    Console.WriteLine("Stack Trace: {0}", ex.StackTrace);
                    Console.WriteLine("***");
                    Console.WriteLine();
                    failedGames.Add(gameUrl);

                }
            }
            return failedGames;
        }

        #region Properties

        /// <summary>
        /// Season Name on J!Archive.  Also used to write JSON files per Season
        /// </summary>
        public string SeasonName { get; set; }
        /// <summary>
        /// Season Start Date
        /// </summary>
        public DateTime StartDate { get; set; }
        /// <summary>
        /// Season End Date
        /// </summary>
        public DateTime EndDate { get; set; }
        /// <summary>
        /// All of the Games in the Season
        /// </summary>
        public List<Game> Games { get; set; }
        /// <summary>
        /// URL for the Season on J!Archive that holds all of the links to the Games in the Season
        /// </summary>
        public string SeasonUrl { get; set; }

        #endregion Properties

        #region Interface Implementations and Overrides

        public int CompareTo([AllowNull] Season other) {
            if (other == null) {
                return -1;
            }
            return this.StartDate.CompareTo(other.StartDate);
        }

        public override string ToString() {
            return string.Format("{0} ({1}-{2})", SeasonName, StartDate.ToShortDateString(), EndDate.ToShortDateString());
        }

        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        #endregion Interface Implementations and Overrides
    }
}
