using UnityEngine;

namespace ToyBlast.Core
{
    public class Block : MonoBehaviour
    {
        [SerializeField] private BlockColor blockColor;

        [SerializeField] private SpriteRenderer _sr;
        [SerializeField] private Sprite _normalSprite;      // boş ise Awake’te mevcut sprite’tan alacağız
        [SerializeField] private Sprite _rocketHintSprite;
        [SerializeField] private Sprite _tntHintSprite;
        [SerializeField] private Sprite _rubikHintSprite;

        [SerializeField] private bool _isPowerup = false;
        [SerializeField] private HintPowerupKind _powerupKind = HintPowerupKind.None; // Rocket/TNT/Rubik
        [SerializeField] private RocketOrientation _rocketOrientation = RocketOrientation.Vertical;

        [SerializeField] private BlockColor _originColorForVFX = BlockColor.Red; // powerup’ın doğduğu kümenin rengi
        public BlockColor OriginColorForVFX => _originColorForVFX;


        // getter'lar (kısa)
        public bool IsPowerup => _isPowerup;
        public HintPowerupKind PowerupKind => _powerupKind;
        public RocketOrientation RocketOrientation => _rocketOrientation;



        private bool _hintActive = false;

        public BlockColor Color => blockColor;

        private void Awake()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr != null && _normalSprite == null) _normalSprite = _sr.sprite;

        }
        public void ShowHint(HintPowerupKind kind)
        {
            if (_sr == null) return;
            switch (kind)
            {
                case HintPowerupKind.Rocket: _sr.sprite = _rocketHintSprite; break;
                case HintPowerupKind.TNT: _sr.sprite = _tntHintSprite; break;
                case HintPowerupKind.Rubik: _sr.sprite = _rubikHintSprite; break;
                default: return;
            }
            _hintActive = true;
        }

        public void ClearHint()
        {
            if (!_hintActive || _sr == null) return;
            _sr.sprite = _normalSprite;
            _hintActive = false;
        }

        public void SetPowerup(HintPowerupKind kind, RocketOrientation orientation = RocketOrientation.Vertical)
        {
            _isPowerup = (kind != HintPowerupKind.None);
            _powerupKind = kind;
            _rocketOrientation = orientation;
        }

        public void SetOriginColor(BlockColor c)
        {
            _originColorForVFX = c;
        }

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
    public enum HintPowerupKind { None, Rocket, TNT, Rubik }
    public enum RocketOrientation { Horizontal, Vertical }

    


}
