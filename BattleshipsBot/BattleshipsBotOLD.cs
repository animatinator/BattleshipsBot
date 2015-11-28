using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BattleshipsInterface;
//using BattleshipsBot;

namespace BattleshipsBot
{
    public enum Direction
    {
        Right, Up, Left, Down
    }



    public class Coords
    {
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
                                                            // TODO: needed?
        private int timeSinceLastHit = 0;  // Time (in turns) since the last successful shot
                                            // (Used to tighten shots if none have succeeded for a while)
        private bool[,] diagonals = new bool[5, 10];
        private int diagonalsStart = 0;  // Set by EliminateChequerPattern, can be zero if that's not being used
        private int currentDiagonal = 0;  // Diagonal currently being explored (start in the middle)
        private int[,] tileScores = new int[5, 10];  // Scores for each tile in each diagonal
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
            lastShotSearching.RowInt = rand.Next(10);
            lastShotSearching.ColInt = rand.Next(10);

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
            for (int diag = 0; diag < 5; diag++)
            {
                // For each tile...
                for (int tile = 0; tile < 10; tile++)
                {
                    Coords pos = DiagonalToActual(diag, tile);
                    int x = pos.ColInt, y = pos.RowInt;
                    tileScores[diag, tile] = CalculateTileScore(x, y);
                }
            }
        }

        private Coords DiagonalToActual(int diag, int row)
        {
            Coords result = new Coords();
            int effectiveXPosition = ((diag * 2) + diagonalsStart + row);
            result.RowInt = row;
            result.ColInt = effectiveXPosition % width;

            return result;
        }

        public IEnumerable<IShipPosition> GetShipPositions()
        {
            // First, clear all state and re-initialise
            ClearState();

            // Now return some ships
            List<ShipPosition> ships = new List<ShipPosition>();

            bool[,] shipPositions = new bool[width, height];  // Indicates which board squares are occupied

            // Initialise it to all false
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

                // Effective width and height of the board (allowing for ship sizes)
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
                            if (shipPositions[x, i]) clear = false; ;  // There's a ship in the way!
                        }
                    }
                    else
                    {
                        // Are the spaces clear?
                        for (int i = x; i <= x + (length - 1); i++)
                        {
                            if (shipPositions[i, y]) clear = false; ;  // There's a ship in the way!
                        }
                    }

                    if (clear) break;  // No ships were found in the way, all is good :)
                    // Otherwise, looping again...
                }

                // Calculate the end position of the ship and erase around them on the shipPositions grid
                int endX, endY;

                if (vertical)
                {
                    endX = x;
                    endY = y + (length - 1);
                    if (endY >= 10) throw new IndexOutOfRangeException("Y :/");

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
                    if (endX >= 10) throw new IndexOutOfRangeException("X :/");
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
                case State.Searching:
                    Coords pos = new Coords();
                    pos.RowInt = 0;
                    pos.ColInt = 0;

                    int diagonalPoint = rand.Next(10);
                    pos = DiagonalToActual(currentDiagonal, diagonalPoint);

                    List<int> validDiagonals = new List<int>();

                    for (int i = 0; i < 5; i++)
                    {
                        if (!DiagonalHasNoShotsLeft(i))
                        {
                            validDiagonals.Add(i);
                        }
                    }

                    if (validDiagonals.Count == 0) throw new ArgumentOutOfRangeException("No valid diagonals :(");
                    currentDiagonal = validDiagonals[rand.Next(validDiagonals.Count)];

                    List<int> validRows = new List<int>();
                    Coords curPos = new Coords();

                    for (int i = 0; i < 10; i++)
                    {
                        curPos = DiagonalToActual(currentDiagonal, i);
                        if (board[curPos.ColInt, curPos.RowInt] == TileType.Blank)
                        {
                            validRows.Add(i);
                        }
                    }

                    if (validRows.Count == 0) throw new Exception("No valid rows somehow :(");

                    // Select the row with the highest score assigned to it
                    int currentRow = 0;
                    int score = 0;
                    int maxScore = 0;
                    foreach (int r in validRows)
                    {
                        score = tileScores[currentDiagonal, r];
                        if (score == 0) throw new IndexOutOfRangeException("Score was zero :S");
                        if (score > maxScore)
                        {
                            maxScore = score;
                            currentRow = r;
                        }
                    }

                    //currentRow = validRows[rand.Next(validRows.Count)];

                    pos = DiagonalToActual(currentDiagonal, currentRow);

                    row = pos.Row;
                    column = pos.Col;

                    lastShotSearching.Col = column;
                    lastShotSearching.Row = row;
                    lastShot.Col = column;
                    lastShot.Row = row;

                    shotCount++;
                    timeSinceLastHit++;

                    break;

                case State.Tracking:
                    // Temporary
                    row = 'A';
                    column = 1;

                    // The shot to be made
                    Coords shot = new Coords();

                    // Use a roulette wheel approach to select a direction to shoot in
                    int shotDirection = rand.Next(32);  // 32 seems a nice enough number to randomise with
                    int roulettePos = rand.Next(4);  // Choose a random starting position to minimise bias
                    
                    while (shotDirection > 0)
                    {
                        roulettePos = (roulettePos + 1) % 4;

                        if (shotDirections[roulettePos] != -1)
                        {
                            shotDirection--;
                        }
                    }

                    // Temporary (rubbish) fix for bad selections:
                    // TODO: Just use this instead?
                    while (shotDirections[roulettePos] == -1)
                    {
                        roulettePos = rand.Next(4);
                    }

                    // Check it hasn't picked a silly direction somehow
                    if (shotDirections[roulettePos] == -1)
                    {
                        throw new Exception("Roulette wheel selection failed.");
                    }

                    // Make the shot
                    lastTrackingShotDirection = (Direction)roulettePos;
                    shotDirections[roulettePos]++;

                    switch (lastTrackingShotDirection)
                    {
                        case Direction.Right:
                            shot.RowInt = firstHitLocation.RowInt;
                            shot.ColInt = (firstHitLocation.ColInt + shotDirections[(int)Direction.Right]) % height;
                            break;

                        case Direction.Up:
                            shot.RowInt = (firstHitLocation.RowInt - shotDirections[(int)Direction.Up]) % width;
                            shot.ColInt = firstHitLocation.ColInt;
                            break;

                        case Direction.Left:
                            shot.RowInt = firstHitLocation.RowInt;
                            shot.ColInt = (firstHitLocation.ColInt - shotDirections[(int)Direction.Left]) % width;
                            break;

                        case Direction.Down:
                            shot.RowInt = (firstHitLocation.RowInt + shotDirections[(int)Direction.Down]) % height;
                            shot.ColInt = firstHitLocation.ColInt;
                            break;
                    }

                    if (shot.RowInt < 0)  // C# mod doesn't wrap negatives :O
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

                default:
                    row = 'A';
                    column = 1;
                    break;
            }
        }

        private bool NoShotsLeft()
        {
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
            Coords curPos = new Coords();

            for (int i = 0; i < 10; i++)
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
            // Return true if all directions in the array have been set to -1
            // (i.e. they have all been fully explored; the ship must have been sunk by now)
            foreach(int direction in shotDirections)
            {
                if (direction != -1) return false;
            }

            return true;
        }

        private int getCurrentShotSpacing()
        {
            if (timeSinceLastHit > 2)
            {
                return 3;
            }
            else if (timeSinceLastHit > 4)
            {
                return 2;
            }
            else if (timeSinceLastHit > 8)
            {
                return 1;
            }
            else
            {
                return 4;
            }
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

            RecalculateTileScores();

            // Anything useful to do when a shot doesn't hit in 'searching' mode? Maybe 'panic' if the opponent is winning?
        }

        private void InvalidateTilesAdjacentToShips()
        {
            // Search the board for tiles where a hit was registered and mark all surrounding tiles
            // as misses unless they were hits
            // (Prevents wasteful searching of tiles adjacent to ships, as no ships can be adjacent)
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
            // Invalidate the nine tiles that form the neighbourhood of this space
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
            // Select a random chequerboard pattern (out of the two) and eliminate all squares from it
            // Makes searching more efficient as only half the squares need to be checked, and still
            // guarantees that all ships will be found
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
            return (x >= 0 && x < width) && (y >= 0 && y < height);
        }

        public void OpponentsShot(char row, int column)
        {
            //Logger.Debug(String.Format("Opponent shot at {0}{1}", row, column));
        }
    }
}
