using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleshipsInterface;

namespace BattleshipsBot
{
    public enum Direction
    {
        Right, Up, Left, Down
    }



    public class Coords
    {
        /*
         * A utility class for storing co-ordinates which does the translation between zero-based
         * co-ordinates and those used by the game
         */

        static char[] alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

        public int RowInt { get; set; }
        public int ColInt { get; set; }

        public char Row
        {
            get
            {
                if (RowInt >= 0 && RowInt < 26)
                {
                    return alpha[RowInt];
                }
                else
                {
                    throw new IndexOutOfRangeException();
                }
            }

            set
            {
                RowInt = Convert.ToInt32(value) - Convert.ToInt32('A');
            }
        }

        public int Col
        {
            get
            {
                return ColInt + 1;
            }

            set
            {
                ColInt = value - 1;
            }
        }
    }



    public class ShipPosition : IShipPosition
    {
        static char[] alpha = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

        private EShipType ship;
        public EShipType Ship { get { return ship; } }
        private int row;
        private int col;

        public char StartingRow { get { return IntToLetter(row); } }
        public int StartingColumn { get { return col + 1; } }

        private int endingRow;
        public char EndingRow
        {
            get
            {
                return IntToLetter(endingRow);
            }
        }

        public int EndingRowInt { get { return endingRow; } }

        private int endingColumn;
        public int EndingColumn { get { return endingColumn + 1; } }


        public ShipPosition(EShipType ship, int startRow, int startColumn, int endRow, int endColumn)
        {
            this.ship = ship;
            this.row = startRow;
            this.col = startColumn;
            this.endingRow = endRow;
            this.endingColumn = endColumn;
        }

        public static char IntToLetter(int input)
        {
            if (input >= 0 && input < 26)
            {
                return alpha[input];
            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }

        public static int LetterToInt(char input)
        {
            return Convert.ToInt32(input) - Convert.ToInt32('A');
        }
    }



    public class BattleshipsPlayer : IBattleshipsPlayer
    {
        static Random rand = new Random();

        public const int width = 10;
        public const int height = 10;

        public enum State
        {
            Searching, Tracking
        }

        public enum TileType
        {
            Blank, Miss, Hit
        }

        public string Name { get { return "An Exercise in Magniloquence"; } }

        // Global state
        private int shotCount = 0;  // Number of shots fired
        private State state = State.Searching;  // Current state of the bot
                                                // (searching first, tracking if a ship has been found)
        private TileType[,] board = new TileType[width, height];  // The state of each tile on the board
        private Coords lastShot = new Coords();  // The location of the last shot the bot fired

        // State whilst searching
        private Coords lastShotSearching = new Coords();  // The location of the last shot the bot fired
                                                            // whilst searching
        private int timeSinceLastHit = 0;  // Time (in turns) since the last successful shot
                                            // (Used to tighten shots if none have succeeded for a while)
        private bool[,] diagonals = new bool[width / 2, height];
        private int diagonalsStart = 0;  // Set by EliminateChequerPattern, can be zero if that's not being used
        private int currentDiagonal = 0;  // Diagonal currently being explored (start in the middle)
        private int[,] tileScores = new int[width / 2, height];  // Scores for each tile in each diagonal
                                                    // Score = length of longest ship which could be on this tile

        // State whilst tracking
        private Coords firstHitLocation = new Coords();  // Where the first hit landed
        private Direction lastTrackingShotDirection = Direction.Right;  // Direction of the last shot when tracking
        private int[] shotDirections = new int[4];  // Specifies how far in each direction the bot has shot
                                                // (-1 => no more hits in this direction)
                                                // Indexed with the Direction enum


        public BattleshipsPlayer()
        {
            ClearState();
        }


        private void ClearBoard()
        {
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    board[i, j] = TileType.Blank;
                }
            }
        }

        private void ClearState()
        {
            // Initialise the board to all false (i.e. no shots have been fired yet)
            ClearBoard();

            // Randomise the first shot
            lastShotSearching.RowInt = rand.Next(height);
            lastShotSearching.ColInt = rand.Next(width);

            // Miscellaneous global state
            shotCount = 0;  // Reset the shot count and time since the last hit
            timeSinceLastHit = 0;
            state = State.Searching;  // Start by searching
            currentDiagonal = 0;  // Start searching in the middle

            // Clear a chequer pattern from the board
            EliminateChequerPattern();

            // Calculate initial tile scores
            RecalculateTileScores();
        }

        private int CalculateTileScore(int x, int y)
        {
            // Calculates a 'score' for a given tile by working out the length of the largest ship
            // which could plausibly be under it. This is done by finding the vertical space around
            // the tile and then the horizontal space, and returning the maximum as its score.

            int row = y, col = x;

            // -- Vertical --
            int verticalSpace = 0;

            // Upwards
            while (CoordsInBoard(col, row) && board[col, row] == TileType.Blank)
            {
                row--;
                verticalSpace++;
            }

            row = y;

            // Downwards
            while (CoordsInBoard(col, row) && board[col, row] == TileType.Blank)
            {
                row++;
                verticalSpace++;
            }

            row = y;

            // -- Horizontal --
            int horizontalSpace = 0;

            // Leftwards
            while (CoordsInBoard(col, row) && board[col, row] == TileType.Blank)
            {
                col--;
                horizontalSpace++;
            }

            col = x;

            // Rightwards
            while (CoordsInBoard(col, row) && board[col, row] == TileType.Blank)
            {
                col++;
                horizontalSpace++;
            }


            return Math.Max(verticalSpace, horizontalSpace);
        }

        private void RecalculateTileScores()
        {
            // For each diagonal...
            for (int diag = 0; diag < width / 2; diag++)
            {
                // For each tile...
                for (int tile = 0; tile < height; tile++)
                {
                    Coords pos = DiagonalToActual(diag, tile);
                    int x = pos.ColInt, y = pos.RowInt;
                    tileScores[diag, tile] = CalculateTileScore(x, y);
                }
            }
        }

        private Coords DiagonalToActual(int diag, int row)
        {
            /*
             * Gets the actual coordinates for a row on a particular diagonal
             */
            Coords result = new Coords();
            // X-position without wrapping
            int effectiveXPosition = ((diag * 2) + diagonalsStart + row);
            result.RowInt = row;
            result.ColInt = effectiveXPosition % width;  // Wrap the X-position

            return result;
        }

        public IEnumerable<IShipPosition> GetShipPositions()
        {
            // First, clear all state and re-initialise
            ClearState();

            // Now return some ships
            List<ShipPosition> ships = new List<ShipPosition>();

            bool[,] shipPositions = new bool[width, height];  // Indicates which board squares are occupied

            // Initialise shipPositions to all false as none have been placed
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    shipPositions[i, j] = false;
                }
            }

            // Add each ship
            foreach (EShipType ship in Enum.GetValues(typeof(EShipType)))
            {
                int length;

                // Get the appropriate length for this ship type
                switch (ship)
                {
                    case EShipType.aircraftCarrier:
                        length = 5;
                        break;
                    case EShipType.battleship:
                        length = 4;
                        break;
                    case EShipType.destroyer:
                        length = 3;
                        break;
                    case EShipType.submarine:
                        length = 3;
                        break;
                    case EShipType.patrolBoat:
                        length = 2;
                        break;
                    default:
                        length = 0;  // Shouldn't happen \ Won't happen.
                        break;
                }

                bool vertical = (rand.Next(2) == 1);  // Decide whether the ship should be placed horizontally or vertically

                int x, y;

                // Effective width and height of the board
                // (Treating it as being smaller so that a ship cannot be accidentally placed off the side)
                int effectiveWidth = width;
                int effectiveHeight = height;
                if (vertical) effectiveHeight -= (length - 1);
                else effectiveWidth -= (length - 1);

                while (true)
                {
                    // Pick x and y
                    x = rand.Next(effectiveWidth);
                    y = rand.Next(effectiveHeight);

                    bool clear = true;

                    if (vertical)
                    {
                        // Are the spaces clear?
                        for (int i = y; i <= y + (length - 1); i++)
                        {
                            if (shipPositions[x, i]) clear = false;  // There's a ship in the way!
                        }
                    }
                    else
                    {
                        // Are the spaces clear?
                        for (int i = x; i <= x + (length - 1); i++)
                        {
                            if (shipPositions[i, y]) clear = false;  // There's a ship in the way!
                        }
                    }

                    if (clear) break;  // No ships were found in the way, all is good :)
                    // Otherwise, looping again...
                }

                // Calculate the end position of the ship
                int endX, endY;

                if (vertical)
                {
                    endX = x;
                    endY = y + (length - 1);

                    // Erase all tiles around the ship, ie:
                    // ........
                    // .XXXXXX.
                    // .XSHIPX.
                    // .XXXXXX.
                    // ........
                    for (int i = x - 1; i <= x + 1; i++)
                    {
                        for (int j = y - 1; j <= endY + 1; j++)
                        {
                            if (i >= 0 && i < width && j >= 0 && j < height)
                            {
                                shipPositions[i, j] = true;
                            }
                        }
                    }
                }
                else
                {
                    endX = x + (length - 1);
                    endY = y;

                    for (int i = y - 1; i <= y + 1; i++)
                    {
                        for (int j = x - 1; j <= endX + 1; j++)
                        {
                            if (i >= 0 && i < height && j >= 0 && j < width)
                            {
                                shipPositions[j, i] = true;
                            }
                        }
                    }
                }

                // Add the new ship to the list
                ShipPosition newShip = new ShipPosition(ship, y, x, endY, endX);
                ships.Add(newShip);
            }

            return ships;
        }

        public void SelectTarget(out char row, out int column)
        {
            switch(state)
            {
                // Whilst blindly searching for ships
                case State.Searching:
                    // Initialise a new Coords object to hold the shot
                    Coords pos = new Coords() { RowInt = 0, ColInt = 0 };

                    int diagonalPoint = rand.Next(height);
                    pos = DiagonalToActual(currentDiagonal, diagonalPoint);

                    // Make a list of diagonals which have not yet been fully explored
                    List<int> validDiagonals = new List<int>();

                    for (int i = 0; i < 5; i++)
                    {
                        if (!DiagonalHasNoShotsLeft(i))
                        {
                            validDiagonals.Add(i);
                        }
                    }

                    if (validDiagonals.Count == 0) throw new Exception("No valid diagonals :(");

                    // Randomly select one
                    currentDiagonal = validDiagonals[rand.Next(validDiagonals.Count)];

                    // Make a list of rows in the selecteed diagonal which have not yet been shot at
                    List<int> validRows = new List<int>();
                    Coords curPos = new Coords();

                    for (int i = 0; i < height; i++)
                    {
                        curPos = DiagonalToActual(currentDiagonal, i);
                        if (board[curPos.ColInt, curPos.RowInt] == TileType.Blank)
                        {
                            validRows.Add(i);
                        }
                    }

                    // Use a roulette wheel selection approach to select a row (biased towards
                    // those with higher scores)
                    int score = 0;

                    List<Tuple<int, int>> scores = new List<Tuple<int, int>>();
                    foreach (int r in validRows)
                    {
                        score = tileScores[currentDiagonal, r];
                        scores.Add(new Tuple<int, int>(r, score));
                    }

                    // Make the scores stored for each cumulative
                    for (int i = 1; i < scores.Count; i++)
                    {
                        scores[i] = new Tuple<int, int>(scores[i].Item1, scores[i].Item2 + scores[i - 1].Item2);
                    }

                    // Get the total score
                    int totalScore = scores[scores.Count - 1].Item2;

                    // Generate a random indexer between 0 and totalScore
                    int indexer = rand.Next(totalScore);

                    // Search through for the first item with cumulative score larger than the
                    // indexer and select that one
                    int selection = 0;
                    foreach (Tuple<int, int> scorePair in scores)
                    {
                        if (indexer < scorePair.Item2) selection = scorePair.Item1;
                        else continue;
                    }

                    // Find the position of the selected diagonal and row
                    pos = DiagonalToActual(currentDiagonal, selection);

                    // Return it
                    row = pos.Row;
                    column = pos.Col;

                    // Save this position as the last searching shot and the last shot
                    lastShotSearching.Col = column;
                    lastShotSearching.Row = row;
                    lastShot.Col = column;
                    lastShot.Row = row;

                    // Increment the number of shots fired and the time since the last hit
                    shotCount++;
                    timeSinceLastHit++;

                    break;

                // Whilst attempting to sink a particular ship
                case State.Tracking:
                    // The shot to be made
                    Coords shot = new Coords();

                    // Pick a direction to shoot in
                    int roulettePos = rand.Next(4);  // Choose a random starting position

                    // Randomly spin until a valid direction has been found
                    while (shotDirections[roulettePos] == -1)
                    {
                        roulettePos = rand.Next(4);
                    }

                    // Check it hasn't picked a silly direction somehow
                    if (shotDirections[roulettePos] == -1)
                    {
                        throw new Exception("Roulette wheel selection failed.");
                    }

                    // Make the shot - save its direction, get the next shot in that direction and
                    // then increment the shotDirections array element for that direction
                    lastTrackingShotDirection = (Direction)roulettePos;
                    shot = GetNextSquareInDirection(lastTrackingShotDirection);
                    
                    shotDirections[roulettePos]++;

                    if (shot.RowInt < 0)  // The C# '%' operator doesn't wrap negatives :O
                    {
                        shot.RowInt = width + shot.RowInt;
                    }
                    if (shot.ColInt < 0)
                    {
                        shot.ColInt = height + shot.ColInt;
                    }
                    row = shot.Row;
                    column = shot.Col;
                    lastShot.Row = row;
                    lastShot.Col = column;

                    break;

                default:  // Definitely shouldn't happen
                    throw new Exception(String.Format("Invalid state: {0}", state));
            }
        }

        private Coords GetNextSquareInDirection(Direction dir)
        {
            /*
             * Gets the next square in a particular direction (used when tracking a ship)
             */

            Coords shot = new Coords();

            switch (dir)
            {
                case Direction.Right:
                    shot.RowInt = firstHitLocation.RowInt;
                    shot.ColInt = (firstHitLocation.ColInt + shotDirections[(int)Direction.Right] + 1) % height;
                    break;

                case Direction.Up:
                    shot.RowInt = (firstHitLocation.RowInt - shotDirections[(int)Direction.Up] - 1) % width;
                    shot.ColInt = firstHitLocation.ColInt;
                    break;

                case Direction.Left:
                    shot.RowInt = firstHitLocation.RowInt;
                    shot.ColInt = (firstHitLocation.ColInt - shotDirections[(int)Direction.Left] - 1) % width;
                    break;

                case Direction.Down:
                    shot.RowInt = (firstHitLocation.RowInt + shotDirections[(int)Direction.Down] + 1) % height;
                    shot.ColInt = firstHitLocation.ColInt;
                    break;
            }

            return shot;
        }

        private bool NoShotsLeft()
        {
            /*
             * Checks all diagonals to see whether they have shots left.
             * If none of them do, return true.
             */

            for (int i = 0; i < 5; i++)
            {
                if (!DiagonalHasNoShotsLeft(i))
                {
                    return false;
                }
            }

            return true;
        }

        private bool DiagonalHasNoShotsLeft(int diagonal)
        {
            /*
             * Find out whether a given diagonal has any spaces which have not yet been shot at
             */

            Coords curPos = new Coords();

            for (int i = 0; i < height; i++)
            {
                curPos = DiagonalToActual(diagonal, i);
                if (board[curPos.ColInt, curPos.RowInt] == TileType.Blank)
                {
                    return false;
                }
            }

            return true;
        }

        private bool NoShotDirectionsLeft()
        {
            /* 
             * Return true if all directions in the array have been set to -1
             * (i.e. they have all been fully explored; the ship must have been sunk by now)
             * (Used when tracking a particular ship)
             * (Will also mark directions which are about to go off the board as -1)
             */

            for(int i = 0; i < 4; i++)
            {
                // First check if the next shot in this direction would be on the board
                Coords nextShot = GetNextSquareInDirection((Direction) i);
                int direction = shotDirections[i];

                if (direction == -1) continue;  // If it's already invalid, continue onto the next one

                // If it's not invalid and the next shot would be on the board, return false -
                // this direction is fine
                else if (CoordsInBoard(nextShot.ColInt, nextShot.RowInt)) return false;

                // Otherwise, mark the direction as invalid and continue
                else
                {
                    shotDirections[i] = -1;
                    continue;
                }
            }

            return true;
        }

        public void ShotResult(bool wasHit)
        {
            // Update the board
            board[lastShot.ColInt, lastShot.RowInt] = wasHit ? TileType.Hit : TileType.Miss;

            if (state == State.Searching && wasHit)
            {
                timeSinceLastHit = 0;
                state = State.Tracking;  // Change state

                // Set the location of the first hit on this ship (subsequent shots will be relative to this)
                firstHitLocation.Col = lastShot.Col;
                firstHitLocation.Row = lastShot.Row;

                // Zero all shot directions (no shots have been made in any direction yet)
                for (int d = 0; d < 4; d++)
                {
                    shotDirections[d] = 0;
                }
            }
            else if (state == State.Tracking)
            {
                if (wasHit)
                {
                    // Boats are either vertical or horizontal - can now remove the other dimension
                    if (lastTrackingShotDirection == Direction.Left || lastTrackingShotDirection == Direction.Right)
                    {
                        shotDirections[(int)Direction.Up] = -1;
                        shotDirections[(int)Direction.Down] = -1;
                    }
                    else
                    {
                        shotDirections[(int)Direction.Left] = -1;
                        shotDirections[(int)Direction.Right] = -1;
                    }
                }
                else
                {
                    shotDirections[(int)lastTrackingShotDirection] = -1;
                }

                // Check if there are now no directions left to shoot in
                if (NoShotDirectionsLeft())
                {
                    // If so, go back to searching
                    state = State.Searching;

                    InvalidateTilesAdjacentToShips();
                }
            }

            // Re-score all tiles now that another shot has been made
            RecalculateTileScores();
        }

        private void InvalidateTilesAdjacentToShips()
        {
            /*
             * Search the board for tiles where a hit was registered and mark all surrounding tiles
             * as misses unless they were hits
             * (Prevents wasteful searching of tiles adjacent to ships, as no ships can be adjacent)
             */

            for (int row = 0; row < height; row++)
            {
                for (int col = 0; col < height; col++)
                {
                    if (board[col, row] == TileType.Hit)
                    {
                        InvalidateSurroundingTiles(col, row);
                    }
                }
            }
        }

        private void InvalidateSurroundingTiles(int x, int y)
        {
            /*
             * Invalidates the nine tiles that form the neighbourhood of this space
             */

            for (int row = y - 1; row <= y + 1; row++)
            {
                for (int col = x - 1; col <= x + 1; col++)
                {
                    if (CoordsInBoard(row, col) && board[col, row] == TileType.Blank)
                    {
                        board[col, row] = TileType.Miss;
                    }
                }
            }
        }

        private void EliminateChequerPattern()
        {
            /*
             * Select a random chequerboard pattern and eliminate all squares from it
             * Makes searching more efficient as only half the squares need to be checked, and still
             * guarantees that all ships will be found
             */

            int index = rand.Next(2);
            diagonalsStart = (index == 1) ? 0 : 1;
            bool skipFirst = (index == 1);
            int row = 0;

            while (row < height)
            {
                board[index, row] = TileType.Miss;

                index = index + 2;
                if (index >= width)  // Wrapping
                {
                    row++;

                    skipFirst = !skipFirst;
                    if (skipFirst)
                    {
                        index = 1;
                    }
                    else
                    {
                        index = 0;
                    }
                }
            }
        }

        private bool CoordsInBoard(int x, int y)
        {
            /*
             * Simple function which returns true if a given co-ordinate is on the board
             */

            return (x >= 0 && x < width) && (y >= 0 && y < height);
        }

        public void OpponentsShot(char row, int column)
        {
            // ...
        }
    }
}
