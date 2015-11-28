using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BattleshipsInterface
{
    /// <summary>
    /// This is the interface that needs to be implemented by a Battleships
    /// player.
    /// The methods on this interface will be called in turn by an adjudicator
    /// that controls two players at a time to run a match.
    /// A match is made up of one or more games. The player that wins the most
    /// games wins the match. A single player object is created and used for
    /// the entire match.
    /// The game is played on a board with rows labled 'A' to 'J' and columns
    /// labelled '1' to '10'.
    /// The aim of the game is to sink all of you opponents ships before they
    /// sink all of yours. (A ship is sunk when every square it occupies has
    /// been hit by their opponent.)
    /// A game starts with each player declaring the positions of their own
    /// ships to the adjudicator.
    /// Then one player is asked to choose a square to fire on and is notified
    /// of the result of their shot. Their opponent then gets a chance to fire.
    /// This continues until all of one player's ships are sunk.
    /// 
    /// If a player fails to provide valid ship positions or throws an exception
    /// that player immediately forfeits the game.
    /// </summary>
    public interface IBattleshipsPlayer
    {
        /// <summary>
        /// The name of the bot.
        /// </summary>
        /// <returns></returns>
        string Name { get; }

        /// <summary>
        /// Called at the beginning of a game. Must return the positions of the
        /// player's ships (these are the ships your opponent will be trying to
        /// hit).
        /// Ships must be arranged according to these rules:
        ///   - One of each of the following ships must be present:
        ///         Aircraft Carrier (5 spaces)
        ///         Battleship (4 spaces)
        ///         Destroyer (3 spaces)
        ///         Submarine (3 spaces)
        ///         Patrol boat (2 spaces)
        ///   - Ships can only be placed horizontally or vertically
        ///   - No ship can be adjacent to another ship horizontally,
        ///     vertically, or diagonally.
        /// </summary>
        /// <returns></returns>
        IEnumerable<IShipPosition> GetShipPositions();

        /// <summary>
        /// This method is called whenever it is your turn.
        /// 
        /// It should return the coordinates of the square you wish to
        /// target. The result of your shot will be indicated by the adjudicator
        /// calling ShotResult()
        /// </summary>
        /// <param name="row"></param>
        /// <param name="column"></param>
        void SelectTarget(out char row, out int column);

        /// <summary>
        /// Indicates the result of the previous shot chosen by SelectTarget().
        /// </summary>
        /// <param name="hit"></param>
        void ShotResult(bool wasHit);

        /// <summary>
        /// This method is called at the end of your opponent's turn (assuming
        /// that they didn't throw an exception) to tell you which square they
        /// fired at.
        /// Implementing this method beyond an empty method is not vital to a
        /// functioning bot.
        /// </summary>
        /// <param name="row"></param>
        /// <param name="column"></param>
        void OpponentsShot(char row, int column);
    }
}
