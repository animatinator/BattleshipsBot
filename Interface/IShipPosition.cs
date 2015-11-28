using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BattleshipsInterface
{
    public enum EShipType
    {
        aircraftCarrier,
        battleship,
        destroyer,
        submarine,
        patrolBoat
    }

    /// <summary>
    /// Objects returned by GetShipPositions must implement this interface. Each
    /// object should represent the position of a single ship.
    /// </summary>
    public interface IShipPosition
    {
        EShipType Ship { get; }
        char StartingRow { get; }
        int StartingColumn { get; }
        char EndingRow { get; }
        int EndingColumn { get; }
    }
}
