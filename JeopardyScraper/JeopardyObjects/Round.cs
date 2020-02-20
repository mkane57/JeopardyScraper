using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Linq;

namespace JeopardyScraper.JeopardyObjects
{
    public class Round : IComparable<Round>
    {
        /// <summary>
        /// Creates a Round object from the IElement of the Round in the DOM
        /// </summary>
        /// <param name="roundType">Type of round (single, double, or final jeopardy)</param>
        /// <param name="round">Round object in the DOM</param>
        /// <param name="gameState">GameState (Scores) at the beginning of the round</param>
        /// <param name="airDate">Original air date (used to figure out default clue values for uncovered clues)</param>
        /// <param name="players">This is only used in Team games to alter the game state to replace the player who is new to this round</param>
        public Round(RoundType roundType, IElement round, List<PlayerAmount> gameState, DateTime airDate, List<PlayerFull> players) {
            this.RoundType = roundType;
            this.Categories = new List<Category>();
            this.Clues = new List<Clue>();

            if (players.Count > 3) {
                // more then 3 playes means we are in a team game, we need to update the gamestate to have the right players in place now
                var newPlayers = players.Where(p => p.TeamRoundPlayed == RoundType);
                foreach (PlayerAmount playerAmount in gameState) {
                    var newPlayer = newPlayers.Where(np => np.TeamName == playerAmount.Player.TeamName).FirstOrDefault();
                    playerAmount.Player.PlayerId = newPlayer.PlayerId;
                    playerAmount.Player.NickName = newPlayer.NickName;
                }
            }

            // clone the GameState so we aren't updating the referenced object passed in
            List<PlayerAmount> beginningRoundGameState = Game.CloneGameState(gameState);
            this.GameState = gameState;

            // get and load the categories for the round in order
            var categories = round.GetElementsByClassName("category_name");
            int order = 1;
            foreach (var category in categories) {
                this.Categories.Add(new Category() {
                    Name = category.TextContent,
                    Order = order
                });
                order++;
            }

            // get the default clue price per row
            decimal pricePerRow = GetPricePerRowEstimate(this.RoundType, airDate);

            // we are going to keep track of the col/row location because we will read the HTML clues
            // from left to right, top to bottom
            int col = 1;
            int row = 1;
            // Get all of the clues for the round (including uncovered clues)
            var clues = round.GetElementsByClassName("clue");
            foreach(var clue in clues) {
                Clue newClue = new Clue(clue, this.Categories[col-1], GameState, row * pricePerRow);
                this.Clues.Add(newClue);
                col++;
                if (col > Categories.Count()) {
                    col = 1;
                    row++;
                }
            }

            // now that we've gone through all the clues, we need to order them and recalculate gamestate (because we read them
            // left to right, not in the order they were selected.  This will recalcuate the running total score after
            // each clue correctly (except for mistakes/corrections by Alex T) and allos us to store it correctly on each Clue
            // so we could do anaylsis on wagering or risk based on scores
            this.Clues.Sort();
            foreach (Clue clue in this.Clues) {
                beginningRoundGameState = clue.SetGameState(beginningRoundGameState);
            }            
        }

        /// <summary>
        /// Sorts all of the items inside the round (Categories, Clues, GameStates)
        /// </summary>
        public void Sort() {
            this.Categories.Sort();
            this.Clues.Sort();
            this.GameState.Sort();
            foreach (Clue clue in Clues) {
                clue.Sort();
            }
        }

        /// <summary>
        /// Takes the recorded scores at the end of the round and checks to see if they match our running totals.  If 
        /// it doesnt match, it corrects our running total for the round and marks it as corrected
        /// </summary>
        /// <param name="endOfRoundScores">IElement of the end of round scores object</param>
        /// <param name="players">List of players to match against scores</param>
        /// <returns>A new GameState with the correct scores for the end of the round</returns>
        public List<PlayerAmount> FixErrorsWithFinalScore(IElement endOfRoundScores, List<PlayerFull> players) {
            this.HasScoreCorrection = false;
            List<PlayerAmount> endOfRoundGameState = new List<PlayerAmount>();
            // get the first element for nick name and score from the end of the round scores html table
            var playerNickName = endOfRoundScores.NextElementSibling.FirstElementChild.FirstElementChild.FirstElementChild;
            var playerScore = endOfRoundScores.NextElementSibling.FirstElementChild.FirstElementChild.NextElementSibling.FirstElementChild;

            // for the 3 players, iterate through the siblings to get the scores and store them in the new GameState
            for (int x = 0; x < 3; x++) {
                // create the player's game state
                Player player = players.Where(p => p.NickName == playerNickName.TextContent || (p.TeamName == playerNickName.TextContent && p.TeamRoundPlayed == this.RoundType)).FirstOrDefault().GetPlayer();
                decimal amount = decimal.Parse(playerScore.InnerHtml.Replace("$", string.Empty).Replace(",", string.Empty));
                PlayerAmount playerAmount = new PlayerAmount() {
                    Player = player,
                    Amount = amount
                };
                // add to the end of the round
                endOfRoundGameState.Add(playerAmount);
                
                // go to the next player's elements in the html table
                playerNickName = playerNickName.NextElementSibling;
                playerScore = playerScore.NextElementSibling;

                // check if the score matched to see if we need to correct it and flag HasScoreCorrection = true
                PlayerAmount finalPlayerAmount = this.GameState.Where(gs => gs.Player.NickName == playerAmount.Player.NickName || (gs.Player.TeamName == playerAmount.Player.NickName && gs.Player.TeamRoundPlayed == this.RoundType)).FirstOrDefault();
                if (finalPlayerAmount.Amount != playerAmount.Amount) {
                    this.
                        HasScoreCorrection = true;
                    finalPlayerAmount.Amount = playerAmount.Amount;
                }
            }
            return endOfRoundGameState;
        }

        /// <summary>
        /// Helper function that will estimate the price of a clue that doesnt have a price (this happens when a clue is
        /// never uncovered on the board).
        /// </summary>
        /// <param name="roundType">Jeopardy Round</param>
        /// <param name="airDate">The air date, because they doubled pricing in 2001</param>
        /// <returns>Estimated price of the first row of clues for the round</returns>
        private decimal GetPricePerRowEstimate(RoundType roundType, DateTime airDate) {
            bool afterPriceChange = airDate > new DateTime(2001, 11, 25);
            if (roundType == RoundType.Jeopardy) {
                return 100m * (afterPriceChange ? 2 : 1);
            }
            else if (roundType == RoundType.Double_Jeopardy) {
                return 200m * (afterPriceChange ? 2 : 1);
            }
            return 0m;
        }

        #region Properties
        /// <summary>
        /// Round Type (Jeopardy, Double Jeopardy, Final Jeopardy)
        /// </summary>
        public RoundType RoundType { get; set; }
        /// <summary>
        /// List of Categories for the Round
        /// </summary>
        public List<Category> Categories { get; set; }
        /// <summary>
        /// List of Clues for the Round
        /// </summary>
        public List<Clue> Clues { get; set; }
        /// <summary>
        /// GameState (Score) at the end of the Round
        /// </summary>
        public List<PlayerAmount> GameState { get; set; }
        /// <summary>
        /// If the Score needed a correction at the end of the Round
        /// </summary>
        public bool HasScoreCorrection { get; set; }

        #endregion Properties


        #region Interface Implementations and Overrides  

        public int CompareTo([AllowNull] Round other) {
            if (other == null) {
                return -1;
            }
            return this.RoundType.CompareTo(other.RoundType);
        }

        public override string ToString() {
            return string.Format("{0}", RoundType);
        }

        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        #endregion Interface Implementations and Overrides  
    }
}
