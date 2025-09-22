// GameEventHub.cs  (Yeni)
using UnityEngine;
using UnityEngine.Events;

namespace ToyBlast.Events
{
    public class GameEventHub : MonoBehaviour
    {
        // Inspector'da argümanlı UnityEvent kullanabilmek için tipini tanımlıyoruz.
        [System.Serializable] public class IntEvent : UnityEvent<int> { }
        [System.Serializable] public class Int2Event : UnityEvent<int, int> { }
        [System.Serializable] public class Int3Event : UnityEvent<int, int, int> { }


        [Header("UI / Moves")]
        public IntEvent MovesChanged;

        [Header("Block Events")]
        public IntEvent BlocksDestroyed;   // Patlayan blok adedini yayınlayacağız

        // class GameEventHub içinde:
        [Header("Board Events")]
        public UnityEvent BoardSettled;


        [Header("Block Lifetime Events")]
        public Int3Event BlockDestroyed;  // (x, y, colorIndex)

        [Header("Input Events")]
        public Int2Event CellClicked; // (x,y)

        // GameEventHub.cs (class içinde)
        [Header("Movement Events")]
        public Int3Event BlockLanded; // (x, y, colorIndex)

        [Header("Spawn Events")]
        public Int3Event BlockSpawnedAndLanded; // (x, y, colorIndex)



    }
}
