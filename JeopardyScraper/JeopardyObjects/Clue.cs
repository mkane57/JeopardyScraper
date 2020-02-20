using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Linq;
using AngleSharp.Html.Parser;

namespace JeopardyScraper.JeopardyObjects
{
    /// <summary>
    /// Class used to represent the Clue object.  A clue has a category, the GameState (the score after the Clue was complete), the 
    /// answers given, and the amount of the Clue
    /// </summary>
    public class Clue : IComparable<Clue>
    {
        /// <summary>
        /// Processes a Clue given an IElement for the Clue from the DOM
        /// </summary>
        /// <param name="clue">IElement for the Clue from the DOM</param>
        /// <param name="category">The Category the Clue belongs to</param>
        /// <param name="gameState">The GameState when the Clue was called for</param>
        /// <param name="expectedPrice">The expected price of the Clue (used when the Clue is uncovered)</param>
        public Clue(IElement clue, Category category, List<PlayerAmount> gameState, decimal expectedPrice) {
            this.Category = category;
            this.GameState = gameState;
            this.PlayerAnswers = new List<PlayerAnswer>();
            this.Amount = expectedPrice;

            if (clue.ChildElementCount == 0) {
                // This is an uncovered clue
                this.IsNeverSeen = true;
                this.GameState = null;
            }
            else {
                // grab the Clue value
                var clueValueNode = clue.GetElementsByClassName("clue_value").FirstOrDefault();
                if (clueValueNode == null) {
                    // if it's a daily double, they give it a different class for formatting
                    clueValueNode = clue.GetElementsByClassName("clue_value_daily_double").FirstOrDefault();
                }

                // if we have sub elements but we can't find a clue value node, then we are in final jeopardy, which doesn't have this stuff
                bool isFinalJeopardy = true;
                if (clueValueNode != null) {
                    isFinalJeopardy = false;
                    string questionAmount = clueValueNode.TextContent.Replace("$", string.Empty);
                    this.IsDailyDouble = false;
                    if (questionAmount.StartsWith("DD:")) {
                        this.IsDailyDouble = true;
                        questionAmount = questionAmount.Substring(questionAmount.IndexOf(":") + 1).Trim();
                    }
                    this.Amount = decimal.Parse(questionAmount);
                    this.Order = int.Parse(clue.GetElementsByClassName("clue_order_number").FirstOrDefault().FirstChild.TextContent);
                }

                // grab the text of the clue
                IElement questionElement = clue.GetElementsByClassName("clue_text").FirstOrDefault();
                this.Question = questionElement.TextContent;

                // grab any anchor in the clue so we can attach the media to the clue
                IElement questionAnchor = questionElement.GetElementsByTagName("a").FirstOrDefault();
                if (questionAnchor != null) {
                    this.QuestionHasMedia = true;
                    this.QuestionMediaLink = questionAnchor.Attributes["href"].Value;
                }
                
                if (isFinalJeopardy) {
                    // we need to get a different node for the clue in the case of final jeopardy
                    clue = clue.ParentElement.ParentElement.ParentElement.ParentElement.GetElementsByClassName("category").FirstOrDefault();
                }               

                // grab the clue IElement that holds the mouse over text we need to parse
                IElement clueDiv = clue.GetElementsByTagName("div").FirstOrDefault();
                string mouseOverText = clueDiv.Attributes["onmouseover"].Value;
                this.ParseMouseOverText(mouseOverText, isFinalJeopardy);
            }
            
        }

        /// <summary>
        /// Updates the GameState given the Player Answers as they exist.  (Should be called after PlayerAnswers have been
        /// properly filled in.)  But this is needed to reset the running scores through a round once all the Clues have been
        /// processed and re-ordered given the order they were selected.
        /// </summary>
        /// <param name="preQuestionGameState">GameState before the Answers</param>
        /// <returns>GameState after this clue has been answered</returns>
        public List<PlayerAmount> SetGameState(List<PlayerAmount> preQuestionGameState) {
            // we need to check for null because there is no GameState on questions that are never answered
            if (this.GameState != null) {
                foreach (PlayerAmount playerAmount in preQuestionGameState) {
                    PlayerAnswer playerAnswer = this.PlayerAnswers.Where(p => p.Player.NickName == playerAmount.Player.NickName).FirstOrDefault();
                    if (playerAnswer != null) {
                        playerAmount.Amount += playerAnswer.Wager * (playerAnswer.IsIncorrect ? -1 : 1);
                    }
                }
                // set our game state or the now current game state
                this.GameState = Game.CloneGameState(preQuestionGameState);
            }
            return preQuestionGameState;
        }

        #region Helper Functions

        /// <summary>
        /// The website J!Archive, in order to let the users test themselves, hides the right/wrong answers similar to the 
        /// game. In order to do this, they have to pack a bunch of HTML into the mouse over text of the Clue.  Which means 
        /// we need to parse the information out of the mouse over text.  This function does that.
        /// </summary>
        /// <param name="mouseOverText">Mouse over text from the HTML for a Clue</param>
        /// <param name="isFinalJeopardy">If this is a Final Jeopardy clue which stores the information differently</param>
        private void ParseMouseOverText(string mouseOverText, bool isFinalJeopardy) {
            // 1. split on single quote to get third paramater
            // 2. convert escape characters back to real characters
            // 3. parse out any commentary and wrong answers
            // 4. parse out right answer
            // 5. if final jeopardy, get all the answers and wagers

            // all we really need is the value of the information in the third parameter.  All of the parameters are text, so they are
            // surrounded by single quotes in java script and thus we can grab them.  The first two parameters are ids and won't have single
            // quotes in them.  However, the actual Clue could have single quotes in them so in those cases, we will need to piece the 
            // answers back together again
            string[] parameters = mouseOverText.Split("'");
            if (parameters.Length > 7) {
                // this means there was a single quote somewhere in the response, so we need to put the splits back together into parameters[5] real quick
                for (int x = 6; x < parameters.Length - 1; x++) {
                    parameters[5] = parameters[5] + "'" + parameters[x];
                }
            }
            // we now have only the information we care to process/parse
            string parameterWeCareAbout = parameters[5];
            
            // let's replace any quotes that need to be fixed
            parameterWeCareAbout = parameterWeCareAbout.Replace("&quot;", "\"");

            HtmlParser parser = new HtmlParser();
            INodeList nodes = parser.ParseFragment(parameterWeCareAbout, null);
            // the string we care about is at the front of this HTML, but the parser adds <html> and <body> tags to it that
            // we need to get past first
            INodeList wrongAnswersAndAlexCommentNodes = nodes.FirstOrDefault().FirstChild.NextSibling.ChildNodes;
            
            // we are going to collect a dictionary of comments from Alex as well as wrong answers
            Dictionary<string, string> wrongAnswers = new Dictionary<string, string>();

            foreach (INode node in wrongAnswersAndAlexCommentNodes) {
                // once we get to the 'em' node, we are done getting comments/wrong answers
                if (node.NodeType == NodeType.Element && node.NodeName.ToLower() == "em") {
                    break;
                }

                // the text we care about is just text (and represtented as node type Text), in between these text fragments
                // are <BR> tags, which we will just be ignoring, but happy they are seperating our text nodes.
                if (node.NodeType == NodeType.Text) {
                    // grab the comment/wrong answer.  
                    string wrongAnswerOrAlexComment = node.NodeValue.Trim();

                    // The format of these comments are ([NickName/Alex]:[WrongAnswer/Comment]) 

                    // if it's not longer then 5 characters '(n:a)' it can't be a real comment to process
                    if (wrongAnswerOrAlexComment.Length > 5) {
                        // does it start with a paren?
                        if (wrongAnswerOrAlexComment[0] == '(') {
                            // if the first character is a paren, then we know it's someone speaking, it could be Alex or a contenstant getting
                            // the answer wrong
                            
                            // remove the opening and closing paren
                            wrongAnswerOrAlexComment = wrongAnswerOrAlexComment.Substring(1, wrongAnswerOrAlexComment.Length - 2); 

                            string[] split = wrongAnswerOrAlexComment.Split(":");
                            if (split.Length < 2) {
                                // we saw a typo where they typed ; instead of :, let's try splitting on that
                                split = wrongAnswerOrAlexComment.Split(";");
                            }
                            if (split.Length < 2) {
                                // if it's still less then 2, we can't parse it.  let's continue
                                continue;
                            }

                            // check for an Alex Comment first, after that, we will be looking to match NickNames
                            if (split[0] == "Alex Trebek") {
                                this.AlexComment = split[1].Trim();
                            }
                            else if (split[0] == "Alex") {
                                this.AlexComment = split[1].Trim();
                            }
                            else {
                                string playerGotItWrong = split[0];

                                if (wrongAnswers.ContainsKey(playerGotItWrong) == false) {
                                    wrongAnswers.Add(playerGotItWrong, split[1].Trim());
                                }
                            }
                        }
                    }
                }
            }
            // we got everythign we could so far, now it's time to process the rest using these helper functions.
            // in the case of final geopardy, we need to do somethings differently
            if (isFinalJeopardy) {
                this.CorrectAnswer = nodes.FirstOrDefault().LastChild.LastChild.TextContent;
                ParseFinalJeopardyQuestion(nodes);
            }
            else {
                this.CorrectAnswer = nodes.GetElementsByClassName("correct_response").FirstOrDefault().TextContent;
                ParseNormalQuestion(nodes, wrongAnswers);
            }
            
        }

        /// <summary>
        /// We get a list of Nodes inside of a clue which includes who got it right, who got it wrong.  We need to 
        /// parse that and combine it with the incorrect answers already collected to store the answers to a question
        /// in either single or double Jeopardy.
        /// </summary>
        /// <param name="nodes">List of nodes inside of a Clue that includes right/wrong answers</param>
        /// <param name="wrongAnswers">Text of wrong answers given in the commentary</param>
        private void ParseNormalQuestion(INodeList nodes, Dictionary<string, string> wrongAnswers) {
            // the wrong answers had to come first, so we can just keep order as we go along
            int order = 1;

            // get the players who got it wrong first
            var playerGotItWrongElements = nodes.GetElementsByClassName("wrong");
            foreach (var playerGotItWrongElement in playerGotItWrongElements) {
                // grab the player's nickname
                string playerGotItWrong = RemoveEscapeCharacterFromNickName(playerGotItWrongElement.TextContent.Trim());
                // sometimes they drop Triple Stumper in here if no one answered the clue and the alarm sounded
                if (playerGotItWrong == "Triple Stumper" || playerGotItWrong == "Quadruple Stumper") {                    
                    return;
                }                
                
                // get the player that got it wrong from the GameState
                PlayerAmount playerGameState = GameState.Where(x => x.Player.NickName == playerGotItWrong).FirstOrDefault();
                Player gotItWrong = playerGameState.Player;
                
                // look and see if we already have a wrong answer for this player, we get wrong answers from the commentary so it's
                // not guaranteed to be perfect if there was some back and forth with Alex, but it's generally correct becuase the first
                // think anyone says is normally their answer
                string wrongAnswer = null;
                if (wrongAnswers.ContainsKey(playerGotItWrong)) {
                    wrongAnswer = wrongAnswers[playerGotItWrong];
                }

                // Add the wrong answer and update the GameState to reflect the money they lost
                this.PlayerAnswers.Add(new PlayerAnswer() {
                    Player = gotItWrong,
                    Answer = wrongAnswer,
                    IsIncorrect = true,
                    Wager = this.Amount,
                    Order = order
                });
                playerGameState.Amount -= this.Amount;
                order++;
            }

            // now do the same thing for the player that got it right, except we don't have to look up the wrong answer
            // and the score change is positive
            var playerGotItRightElement = nodes.GetElementsByClassName("right").FirstOrDefault();
            if (playerGotItRightElement != null) {
                string playerGotItRight = RemoveEscapeCharacterFromNickName(playerGotItRightElement.TextContent.Trim());
                PlayerAmount playerGameState = GameState.Where(x => x.Player.NickName == playerGotItRight).FirstOrDefault();                
                Player gotItRight = playerGameState.Player;                

                this.PlayerAnswers.Add(new PlayerAnswer() {
                    Player = gotItRight,
                    Answer = this.CorrectAnswer,
                    IsIncorrect = false,
                    Wager = this.Amount,
                    Order = order
                });
                playerGameState.Amount += this.Amount;
            }
        }
        

        /// <summary>
        /// Final Jeopardy questions are organized in a table with classes denoting 
        /// if the player got the answer right or wrong, so we will gram that meta data
        /// from each and pass it to a second helper function to process it
        /// </summary>
        /// <param name="nodes">List of nodes that include Final Jeopardy right/wrong responses</param>
        private void ParseFinalJeopardyQuestion(INodeList nodes) {
            // grab the elements where plays got it write or wrong
            var playersGotItRightElement = nodes.GetElementsByClassName("right");
            var playersGotItWrongElement = nodes.GetElementsByClassName("wrong");

            // final jeopardy order is determined in reverse score order so let's create a quick hash
            // to get this right
            GameState.Sort();
            Dictionary<Player, int> finalJeopardyPlayerOrder = new Dictionary<Player, int>();
            int order = 1;
            foreach (Player player in GameState.Select(p => p.Player)) {
                finalJeopardyPlayerOrder.Add(player, order);
                order++;
            }            
            
            foreach (var playerAnswer in playersGotItRightElement) {
                ParseFinalJeopardyPlayerAnswer(playerAnswer, false, finalJeopardyPlayerOrder);
            }
            foreach (var playerAnswer in playersGotItWrongElement) {
                ParseFinalJeopardyPlayerAnswer(playerAnswer, true, finalJeopardyPlayerOrder);
            }
        }

        /// <summary>
        /// Parses the answers for Final Jeopardy based on the IElement from the DOM (these are different
        /// then answers in Single and Double Jeopardy).
        /// </summary>
        /// <param name="playerAnswer">IElement of the Player Answer</param>
        /// <param name="isIncorrect">If it was correct or not (was recorded in the class of the parent object, so we already have this)</param>
        /// <param name="playerOrderLoopup">Order in which the players answered the question</param>
        private void ParseFinalJeopardyPlayerAnswer(IElement playerAnswer, bool isIncorrect, Dictionary<Player, int> playerOrderLoopup) {
            // grab the relevent information from the DOM IElement
            string playerNickName = RemoveEscapeCharacterFromNickName(playerAnswer.TextContent.Trim()); 
            string answer = playerAnswer.NextElementSibling.InnerHtml;
            string wagerStr = playerAnswer.ParentElement.NextElementSibling.FirstElementChild.TextContent;
            decimal wager = decimal.Parse(wagerStr.Replace("$", string.Empty).Replace(",", ""));

            // get the players current game state
            PlayerAmount playerGameState = GameState.Where(x => x.Player.NickName == playerNickName).FirstOrDefault();
            Player player = playerGameState.Player;
            // adds the player's answer to the list of answers on this Clue
            this.PlayerAnswers.Add(new PlayerAnswer() {
                Player = player,
                Answer = this.CorrectAnswer,
                IsIncorrect = isIncorrect,
                Wager = wager,
                Order = playerOrderLoopup[player]
            });
            // Updates the game state for this player based on whether they got it right or not
            playerGameState.Amount += wager * (isIncorrect ? -1 : 1);
        }

        /// <summary>
        /// Helper function to remove escape character when needed for names that have a single apostrophe
        /// </summary>
        /// <param name="nickName">Nick Name to remove slach from</param>
        /// <returns>Nick name without slash</returns>
        private string RemoveEscapeCharacterFromNickName(string nickName) {
            if (nickName.Contains("'")) {
                // because the names are caught inside single quotes, if the name has a single quote, we have to remove the escape character because there is an extra \
                return nickName.Replace(@"\", @"");
            }
            return nickName;
        }


        #endregion Helper Functions

        /// <summary>
        /// Sorts the Players answers and the GameState
        /// </summary>
        public void Sort() {
            this.GameState.Sort();
            this.PlayerAnswers.Sort();
        }

        #region Properties
        /// <summary>
        /// The order in which the Clues were selected.  Zero (0) means they were not selected.
        /// </summary>
        public int Order { get; set; }
        /// <summary>
        /// The category the Clue belonged to
        /// </summary>
        public Category Category { get; set; }
        /// <summary>
        /// The amount of the clue.  If it was a Daily Double, this will reflect the amount wagered.
        /// </summary>
        public decimal Amount { get; set; }
        /// <summary>
        /// The GameState (score) after this Clue was played
        /// </summary>
        public List<PlayerAmount> GameState { get; set; }
        /// <summary>
        /// A comment made by Alex Trebek during the Clue.  If he made multiple comments (in a back and forth dialogue), this
        /// will just store the first.
        /// </summary>
        public string AlexComment { get; set; }
        /// <summary>
        /// The Question for the Clue (which is the Clue itself, but don't get confused, this isn't the "What is...")
        /// </summary>
        public string Question { get; set; }
        /// <summary>
        /// The correct answer for the Clue (though often recorded not in the form of a question like they answer in the game)
        /// </summary>
        public string CorrectAnswer { get; set; }
        /// <summary>
        /// If the question had some kind of media (a picture, sound, or celebrity clue reader)
        /// </summary>
        public bool QuestionHasMedia { get; set; }
        /// <summary>
        /// Link to the media for the Clue (a picture, sound, or celebrity clue reader)
        /// </summary>
        public string QuestionMediaLink { get; set; }
        /// <summary>
        /// If the clue was never uncovered before the round's time ran out
        /// </summary>
        public bool IsNeverSeen { get; set; }
        /// <summary>
        /// If the Clue was a Daily Double
        /// </summary>
        public bool IsDailyDouble { get; set; }   
        /// <summary>
        /// List of all the answers given (correct and incorrect) by the players who tried to answer the Clue
        /// </summary>
        public List<PlayerAnswer> PlayerAnswers { get; set; }

        #endregion Properties

        #region Interface Implementations and Overrides    

        public int CompareTo([AllowNull] Clue other) {
            if (other == null) {
                return -1;
            }
            // we need to handle how to sort clues that have never been seen correctly, we will put them all at the 
            // end of the list, but ordered by category/amount
            if (this.IsNeverSeen && other.IsNeverSeen) {
                if (this.Category == other.Category) {
                    return this.Amount.CompareTo(other.Amount);
                }
                return this.Category.CompareTo(other.Category);
            }
            if (other.IsNeverSeen) {
                return -1;
            }
            if (this.IsNeverSeen) {
                return 1;
            }
            return this.Order.CompareTo(other.Order);
        }

        public override string ToString() {
            return string.Format("Q: {0} A: {1} {2}", Question, CorrectAnswer, IsDailyDouble ? "*DD*" : "");
        }

        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        #endregion Interface Implementations and Overrides    
    }
}
