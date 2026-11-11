using UnityEngine;
namespace MakeTrap.House
{
    public class HouseLayout : MonoBehaviour
    {
        public float width = 18, depth = 14, storeyHeight = 3.2f, wallThickness = .2f;
        public float corridorWidth = 3, stairWidth = 2.4f, stairRun = 5f, doorWidth = 1.8f, doorHeight = 2.6f;
        [Range(.35f,.65f)] public float frontRoomFraction = .43f;
        public GameObject preservedGround;
    }
}

