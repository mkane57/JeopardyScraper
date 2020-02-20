using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace JeopardyScraper.JeopardyObjects
{
    /// <summary>
    /// Class to represent the Categories for each round
    /// </summary>
    public class Category : IComparable<Category>
    {        
        /// <summary>
        /// Category Name on the board
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Order left to right of the categories on the board
        /// </summary>
        public int Order { get; set; }

        #region Interface Implementations and Overrides    

        public int CompareTo([AllowNull] Category other) {
            if (other == null) {
                return -1;
            }
            return this.Order.CompareTo(other.Order);
        }

        public override string ToString() {
            return string.Format("{0} ({1})", Name, Order);
        }

        public override int GetHashCode() {
            return this.ToString().GetHashCode();
        }

        #endregion Interface Implementations and Overrides    
    }
}
