using AngleSharp.Dom;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Linq;
using System.Web;
using System.Collections.Specialized;
using Newtonsoft.Json;

namespace JeopardyScraper.JeopardyObjects
{
    /// <summary>
    /// Player object that includes all information about the player that is known.  This object encapsulates a Player object
    /// which can be accessed by it's GetPlayer() function.
    /// </summary>
    public class PlayerFull : IComparable<PlayerFull>, ICloneable
    {
        private PlayerFull() {}

        /// <summary>
        /// Loads a Player from the contestant IElement object in the DOM
        /// </summary>
        /// <param name="contestant">Contestant IElement object in the DOM</param>
        public PlayerFull(IElement contestant) {
            // gets the link to the players personal page
            var anchor = contestant.GetElementsByTagName("a").FirstOrDefault();
            string playerUrl = anchor.Attributes["href"].Value;
            
            // grabs the player id from the Url
            NameValueCollection queryCollection = HttpUtility.ParseQueryString(playerUrl);
            this.PlayerId = int.Parse(queryCollection.Get(0));
            
            // grams the name and the description from the DOM
            this.Name = anchor.InnerHtml.Trim();            
            this.Desc = contestant.InnerHtml.Substring(contestant.InnerHtml.IndexOf("</a>") + 5).Trim();

            this.TeamName = GetTeamName(contestant);
            if (this.TeamName != null) {
                // this is a team game...crap, ok, we need the round the player played in for later
                this.TeamName = GetTeamName(contestant);
                if (contestant.TextContent.StartsWith("Playing the Jeopardy! Round")) {
                    this.TeamRoundPlayed = RoundType.Jeopardy;
                }
                else if (contestant.TextContent.StartsWith("Playing the Double Jeopardy! Round")) {
                    this.TeamRoundPlayed = RoundType.Double_Jeopardy;
                }
                else {
                    this.TeamRoundPlayed = RoundType.Final_Jeopardy;
                }
                // nick names won't work because the nick names are the team names, so we are just going to try 
                // first names only.  hopefully no one in the team games are named the same first name (or Alex)
                this.NickName = this.Name.Split(" ").FirstOrDefault();
            }
        }

        /// <summary>
        /// Encapsulated smaller player object (PlayerId, NickName)
        /// </summary>
        private Player player = new Player();

        /// <summary>
        /// Gets the smaller Player object used in Game play (in Clues)
        /// </summary>
        /// <returns>Encapsulated Player object</returns>
        public Player GetPlayer() {
            return player;
        }

        /// <summary>
        /// Searches through the list of nick names and attempts to match one of the nick names
        /// to this player.  If one is found, the nick name is assigned to this object and returned from 
        /// the function.
        /// </summary>
        /// <param name="nickNames">List of nick names to search</param>
        /// <param name="matchLessLetters">Matching on less then all the letters in the nick name</param>
        /// <returns>The nick name that we matched on, otherwise null if no match was made.</returns>
        public string AssignUnassignedNickName(List<string> nickNames, int matchLessLetters = 0) {
            // if this is the last one left, assign it by process of eliminiation
            if (nickNames.Count == 1) {
                this.NickName = nickNames[0];
                return this.NickName;
            }

            // loop through the remaining nick names and assign it if it's in there
            foreach (string nickName in nickNames) {
                if (this.Name.StartsWith(nickName)) {
                    this.NickName = nickName;
                    return nickName;
                }
            }

            if (matchLessLetters > 0) {
                // loop through and match less letters of the nick name
                foreach (string nickName in nickNames) {
                    string shortenedNickName = nickName.Substring(0, nickName.Length - matchLessLetters);
                    if (this.Name.StartsWith(shortenedNickName)) {
                        this.NickName = nickName;
                        return nickName;
                    }
                }
            }

            return null;
        }
        
        /// <summary>
        /// Finds the Team Name for a contestant if this is a Team Game
        /// </summary>
        /// <param name="contestant">IElement from DOM for contestant</param>
        /// <returns>Null if not team game, Team Name otherwise</returns>
        private string GetTeamName(IElement contestant) {
            IElement parent = contestant.ParentElement;
            string teamName = null;
            foreach (IElement child in parent.Children) {
                if (child.NodeName.ToLower() == "h3") {
                    // we found a team name, not sure if it is ours
                    teamName = child.TextContent;
                    teamName = teamName.Substring(0, teamName.IndexOf("(")).Trim();
                    continue;
                }
                if (child == contestant) {
                    return teamName;
                }
            }
            return null;
        }

        #region Properties

        /// <summary>
        /// J!Archive Player ID
        /// </summary>
        public int PlayerId {
            get { return player.PlayerId; }
            set { player.PlayerId = value; }
        }
        /// <summary>
        /// Nick Name used to identify the player during the game and on the Clue answers
        /// </summary>
        public string NickName {
            get { return player.NickName; }
            set { player.NickName = value; }
        }
        /// <summary>
        /// If this is a Team game, we need to store the Player with the Team
        /// </summary>
        public string TeamName {
            get { return player.TeamName; }
            set { player.TeamName = value; }
        }
        /// <summary>
        ///  If this is a team game, we need to know the round the player played in
        /// </summary>
        [JsonIgnore]
        public RoundType TeamRoundPlayed {
            get { return player.TeamRoundPlayed; }
            set { player.TeamRoundPlayed = value; }
        }
        /// <summary>
        /// Full Name of the Player
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description used for the player at the beginning of the game when introduced by Alex T
        /// </summary>
        public string Desc { get; set; }

        #endregion Properties

        #region Interface Implementations and Overrides

        public object Clone() {
            PlayerFull newPlayer = new PlayerFull() {
                PlayerId = this.PlayerId,
                Name = this.Name,
                Desc = this.Desc,
                NickName = this.NickName
            };
            return newPlayer;
        }

        public int CompareTo([AllowNull] PlayerFull other) {
            if (other == null) {
                return -1;
            }
            return this.PlayerId.CompareTo(other.PlayerId);
        }

        public override string ToString() {
            return string.Format("{0} ({1})", Name, PlayerId);
        }
        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        #endregion Interface Implementations and Overrides
    }

    /// <summary>
    /// Smaller version of the Player object that only includes the ID and NickName, which is
    /// how players are identified throughout the game (in Clues)
    /// </summary>
    public class Player : IComparable<Player>, ICloneable
    {
        /// <summary>
        /// J!Archive Player ID
        /// </summary>
        public int PlayerId { get; set; }
        /// <summary>
        /// Nick Name used to identify the player during the game and on the Clue answers
        /// </summary>
        public string NickName { get; set; }
        /// <summary>
        /// If this is a Team game, we need to store the Player with the Team
        /// </summary>
        public string TeamName { get; set; }
        /// <summary>
        /// If this is a team game, we need to know the round the player played in
        /// </summary>
        [JsonIgnore]
        public RoundType TeamRoundPlayed { get; set; }

        #region Interface Implementations and Overrides

        public object Clone() {
            Player newPlayer = new Player() {
                PlayerId = this.PlayerId,
                NickName = this.NickName,
                TeamName = this.TeamName,
                TeamRoundPlayed = this.TeamRoundPlayed
            };
            return newPlayer;
        }

        public int CompareTo([AllowNull] Player other) {
            if (other == null) {
                return -1;
            }            
            return this.PlayerId.CompareTo(other.PlayerId);
        }

        public override string ToString() {
            return string.Format("{0} ({1})", TeamName ?? NickName, PlayerId);
        }

        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        #endregion Interface Implementations and Overrides
    }
}
