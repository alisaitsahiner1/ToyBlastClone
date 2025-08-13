using UnityEngine;

namespace ToyBlast.Core
{
    public class Block : MonoBehaviour
    {
        [SerializeField] private BlockColor blockColor;

        public BlockColor Color => blockColor;
    }

    public enum BlockColor
    {
        Red,
        Green,
        Blue,
        Yellow,
        Purple,
        Orange
    }
}
