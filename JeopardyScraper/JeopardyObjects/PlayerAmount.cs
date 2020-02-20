using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace JeopardyScraper.JeopardyObjects
{
    /// <summary>
    /// Class used to represent a Player's score in the GameState
    /// </summary>
    public class PlayerAmount : IComparable<PlayerAmount>, ICloneable
    {
        /// <summary>
        /// Player 
        /// </summary>
        public Player Player { get; set; }
        /// <summary>
        /// Score of player (how much money they had at that point)
        /// </summary>
        public decimal Amount { get; set; }

        #region Interface Implementations and Overrides

        public object Clone() {
            PlayerAmount newPlayerAmount = new PlayerAmount() {
                Player = (Player)this.Player.Clone(),
                Amount = this.Amount
            };
            return newPlayerAmount;
        }

        public int CompareTo([AllowNull] PlayerAmount other) {
            if (other == null) {
                return -1;
            }
            return this.Player.CompareTo(other.Player);
        }

        public override string ToString() {
            return string.Format("{0} A: {1:c}", Player, Amount);
        }

        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        #endregion Interface Implementations and Overrides
    }
}
