using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace JeopardyScraper.JeopardyObjects
{
    /// <summary>
    /// Class representing a player's answer to a Clue.
    /// </summary>
    public class PlayerAnswer : IComparable<PlayerAnswer>
    {
        /// <summary>
        /// Order the Players Answered the question in the Game
        /// </summary>
        public int Order { get; set; }
        /// <summary>
        /// Player who answered the question
        /// </summary>
        public Player Player { get; set; }
        /// <summary>
        /// Player's answer
        /// </summary>
        public string Answer { get; set; }
        /// <summary>
        /// Player's wager
        /// </summary>
        public decimal Wager { get; set; }
        /// <summary>
        /// We used a negative (IsIncorrect) to reduce the eventual JSON because most answers
        /// are correct, so we only see this in the JSON if they got the answer wrong
        /// </summary>
        public bool IsIncorrect { get; set; }

        #region Interface Implementations and Overrides

        public int CompareTo([AllowNull] PlayerAnswer other) {
            if (other == null) {
                return -1;
            }
            return this.Order.CompareTo(other.Order);
        }

        public override string ToString() {
            return string.Format("{0} A: {1} {2}", Player, Answer, IsIncorrect ? "*Incorrect*" : "");
        }

        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        #endregion Interface Implementations and Overrides
    }
}
