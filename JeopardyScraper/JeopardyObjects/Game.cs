using System;
using System.Collections.Generic;
using System.Text;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using System.Net.Http;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using AngleSharp.Dom;

namespace JeopardyScraper.JeopardyObjects
{
    /// <summary>
    /// Class used to represent the Game object, which includes Rounds (which in turn have clues), Players, and Scores
    /// </summary>
    public class Game : IComparable<Game>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        /// <param name="htmlDocument"></param>
        public Game(string url, IHtmlDocument htmlDocument) {
            this.url = url;
            Players = new List<PlayerFull>();
            Rounds = new List<Round>();
            GameState = new List<PlayerAmount>();

            this.ProcessHtmlDocument(htmlDocument);            
        }

        private string url;
        
        /// <summary>
        /// Sorts all of the Players, GameStates, Rounds, and the Games within the Rounds
        /// </summary>
        public void Sort() {
            this.Players.Sort();
            this.GameState.Sort();
            this.Rounds.Sort();
            foreach (Round round in this.Rounds) {
                round.Sort();
            }
        }

        #region Static Functions

        /// <summary>
        /// Helper function to get the GameId from the Game URL
        /// </summary>
        /// <param name="gameUrl">URL for a Game</param>
        /// <returns>GameId or -1 if it was an invalid URL</returns>
        internal static int GetGameIdFromUrl(string gameUrl) {
            // Get the game id from the query string
            NameValueCollection queryCollection = HttpUtility.ParseQueryString(gameUrl);
            int gameId = -1;
            int.TryParse(queryCollection.Get(0), out gameId);
            return gameId;
        }

        /// <summary>
        /// Clones a GameState (which is a list of Player Amounts) to easily allow for storing a GameState at a point in time
        /// </summary>
        /// <param name="gameState">GameState to clone</param>
        /// <returns>Newly cloned GameState</returns>
        internal static List<PlayerAmount> CloneGameState(List<PlayerAmount> gameState) {
            List<PlayerAmount> newGameState = new List<PlayerAmount>();
            foreach (PlayerAmount playerAmount in gameState) {
                newGameState.Add((PlayerAmount)playerAmount.Clone());
            }
            return newGameState;
        }

        #endregion Static Functions

        /// <summary>
        /// Helper function to process the HtmlDocument DOM and fill the Game objects (specifically Players, Rounds and Clues
        /// </summary>
        /// <param name="document">HtmlDocument DOM for the Game</param>
        private void ProcessHtmlDocument(IHtmlDocument document) {
            // Get the game id from the query string
            this.GameId = GetGameIdFromUrl(url);

            if (this.GameId == -1) {
                throw new Exception(string.Format("The Game object was passed an inapropriate url.  {0}", url));
            }

            // grab the game title and split it into show number and air date
            var gameTitle = document.All.Where(x => x.Id == "game_title");
            string[] titleSplit = gameTitle.FirstOrDefault().FirstChild.TextContent.Split("-");
            this.ShowNumber = titleSplit[0].Trim();
            this.AirDate = DateTime.Parse(titleSplit[1]);

            // grab the nick names from the first set of player scores (this is a clear place nick names are used)
            List<string> nickNames = new List<string>();
            var nickNameNodes = document.All.Where(x => x.ClassName == "score_player_nickname");
            foreach (var nickNameNode in nickNameNodes) {
                if (nickNames.Count > 2) {
                    break;
                }
                string nickName = nickNameNode.InnerHtml.Trim();
                if (nickNames.Contains(nickName) == false) {
                    nickNames.Add(nickName);
                }
            }

            // grab the players from the contestants section (this gets full names as well as description
            var contestants = document.All.Where(x => x.ClassName == "contestants");
            foreach (var contestant in contestants) {                

                PlayerFull newPlayer = new PlayerFull(contestant);
                this.Players.Add(newPlayer);
                this.GameState.Add(new PlayerAmount() {
                    Player = newPlayer.GetPlayer(),
                    Amount = 0m
                });
            }

            // call our helper function to assign nick names to players
            AssignUnassignedNickNames(nickNames);

            // Grab the elements that denote each of the rounds of Jeopardy
            var jeopardyRoundElement = document.All.Where(x => x.Id == "jeopardy_round").FirstOrDefault();
            var doubleJeopardyRoundElement = document.All.Where(x => x.Id == "double_jeopardy_round").FirstOrDefault();
            var finalJeopardyRoundElement = document.All.Where(x => x.Id == "final_jeopardy_round").FirstOrDefault();

            // Grab the scores at the end of each round.  While we keep a running tally of scores throughout the round, occasionally
            // an error occurs durign the game and Alex T alters a score later.  We use this to correct the running tally of scores 
            // at the end of each round
            var endOfJeopardyRound = document.All.Where(x => x.InnerHtml == "Scores at the end of the Jeopardy! Round:").FirstOrDefault();
            var endOfDoubleJeopardyRound = document.All.Where(x => x.InnerHtml == "Scores at the end of the Double Jeopardy! Round:").FirstOrDefault();
            var endOfFinalJeopardyRound = document.All.Where(x => x.InnerHtml == "Final scores:").FirstOrDefault();

            // process each round individually in a try catch.  We won't throw a game out simply because of an error within a single round
            // because it could be something simple and/or could still be valid overall for our data.
            try {
                // create the round, and clone the gamestate you are sending into the round.
                Round jeopardyRound = new Round(RoundType.Jeopardy, jeopardyRoundElement, CloneGameState(this.GameState), this.AirDate, this.Players);
                // once the round is corrected, fix any scoring errors and return a cloned game state that can continue to be the running
                // total for the game (and that we will Clone and pass into the beginning of the next round).
                this.GameState = jeopardyRound.FixErrorsWithFinalScore(endOfJeopardyRound, this.Players);
                // store the round on the Game
                this.Rounds.Add(jeopardyRound);
            }
            catch (Exception ex) {
                Console.WriteLine("*** Error Processing Jeopardy Round.");
                Console.WriteLine("Error Message: {0}", ex.Message);
                Console.WriteLine("Stack Trace: {0}", ex.StackTrace);
                this.IncompleteLoad = true;
            }

            try {
                Round doubleJeopardyRound = new Round(RoundType.Double_Jeopardy, doubleJeopardyRoundElement, CloneGameState(this.GameState), this.AirDate, this.Players);
                this.GameState = doubleJeopardyRound.FixErrorsWithFinalScore(endOfDoubleJeopardyRound, this.Players);
                this.Rounds.Add(doubleJeopardyRound);
            }
            catch (Exception ex) {
                Console.WriteLine("*** Error Processing Double Jeopardy Round.");
                Console.WriteLine("Error Message: {0}", ex.Message);
                Console.WriteLine("Stack Trace: {0}", ex.StackTrace);
                this.IncompleteLoad = true;
            }

            try {
                Round finalJeopardyRound = new Round(RoundType.Final_Jeopardy, finalJeopardyRoundElement, CloneGameState(this.GameState), this.AirDate, this.Players);
                finalJeopardyRound.FixErrorsWithFinalScore(endOfFinalJeopardyRound, this.Players);
                this.GameState = CloneGameState(finalJeopardyRound.GameState);
                this.Rounds.Add(finalJeopardyRound);
            }
            catch (Exception ex) {
                Console.WriteLine("*** Error Processing Final Jeopardy Round.");
                Console.WriteLine("Error Message: {0}", ex.Message);
                Console.WriteLine("Stack Trace: {0}", ex.StackTrace);
                this.IncompleteLoad = true;
            }
        }

        /// <summary>
        /// Helper function to assign nick names to the player object.  Nick names are used to denote who gets a question right
        /// but are not initially tied to the players in the game.  So we need to do a little searching and smart assignment to 
        /// get the names right.
        /// </summary>
        /// <param name="nickNames">List of nick names to assign to the players in the game</param>
        private void AssignUnassignedNickNames(List<string> nickNames) {            
            
            // first pass for the easy match
            foreach (PlayerFull player in this.Players) {
                if (player.TeamName != null) {
                    // in team games, nick names are out the window
                    return;
                }
                string assignedNickName = player.AssignUnassignedNickName(nickNames);
                if (assignedNickName != null) {
                    nickNames.Remove(assignedNickName);
                }
            }
            
            // if we didnt match on the first pass, we now ant to try and match on less letters using only the players
            // we have left to match.  We can get it right either from process of elimination or from matching less letters.
            // The less letters works for names like Mike and Michael, which wouldn't match at first, but would match when
            // we got down to checking Mi vs. Mi.
            int matchLessLetters = 1;
            while (nickNames.Count > 0) {
                IEnumerable<PlayerFull> playersWithoutNickNames = this.Players.Where(p => p.NickName == null);
                foreach (PlayerFull player in playersWithoutNickNames) {
                    string assignedNickName = player.AssignUnassignedNickName(nickNames, matchLessLetters);
                    if (assignedNickName != null) {
                        nickNames.Remove(assignedNickName);
                    }
                }
                matchLessLetters++;

                if (matchLessLetters > 20) {
                    throw new Exception(string.Format("We failed to match a player to their nickname.  GameId: {0} Show #: {1} Unmatched Nicknames: {2}", this.GameId, this.ShowNumber, string.Join(",", nickNames)));
                }
            }
        }

        #region Properties

        /// <summary>
        /// GameId as stored in J!Archive
        /// </summary>
        public int GameId { get; set; }
        /// <summary>
        /// The Show Number (generally in numeric order, but not always for special seasons or shows)
        /// </summary>
        public string ShowNumber { get; set; }
        /// <summary>
        /// Original Air Date
        /// </summary>
        public DateTime AirDate { get; set; }
        /// <summary>
        /// Players with desc for the Game
        /// </summary>
        public List<PlayerFull> Players { get; set; }
        /// <summary>
        /// The rounds of Jeopardy (that include the Clues for each round)
        /// </summary>
        public List<Round> Rounds { get; set; }
        /// <summary>
        /// Final GameState (Score) of the game.  When in process, can be a running total.
        /// </summary>
        public List<PlayerAmount> GameState { get; set; }
        /// <summary>
        /// Failed to load/scrape the entire game because the J!Archive site was incomplete
        /// </summary>
        public bool IncompleteLoad { get; set; }

        #endregion Properties

        #region Interface Implementations and Overrides    
        
        public int CompareTo([AllowNull] Game other) {
            if (other == null) {
                return -1;
            }
            return this.AirDate.CompareTo(other.AirDate);
        }

        public override string ToString() {
            return string.Format("#{0} ({1}) - AD: {2})", ShowNumber, GameId, AirDate.ToShortDateString());
        }

        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        #endregion Interface Implementations and Overrides
    }
}
