using UnityEngine;

public class SpriteNumber : MonoBehaviour
{

    // SpriteNumber.cs içinde SADECE ekle/değiştir:
    [SerializeField] private Sprite[] digits = new Sprite[10];     // 0..9 sprite’larını buraya sürükle
    [SerializeField] private UnityEngine.UI.Image digitTemplate;   // Counter içindeki (inactive) Image
    [SerializeField] private RectTransform container;              // Counter (parent)

    [SerializeField] private ToyBlast.Events.GameEventHub eventHub;

    [SerializeField] private float sizeMultiplier = 2f;  // Inspector’dan ayarlanabilir


    private readonly System.Collections.Generic.List<UnityEngine.UI.Image> _images
        = new System.Collections.Generic.List<UnityEngine.UI.Image>();


    void Start()
    {

    }

    void Update()
    {

    }

    public void SetNumber(int value)
    {
        if (value < 0) value = 0;
        string s = value.ToString();

        // eksik Image varsa üret
        while (_images.Count < s.Length)
        {
            var img = Instantiate(digitTemplate, container);
            img.raycastTarget = false;
            img.gameObject.SetActive(true);

            img.SetNativeSize();
            img.rectTransform.sizeDelta *= sizeMultiplier;   // 2x büyüt
            _images.Add(img);
        }

        // fazlaları gizle
        for (int i = 0; i < _images.Count; i++)
            _images[i].gameObject.SetActive(i < s.Length);

        // her karakter için doğru sprite
        for (int i = 0; i < s.Length; i++)
        {
            int d = s[i] - '0';
            var digitImg = _images[i];
            digitImg.sprite = digits[d];
            digitImg.SetNativeSize();
            digitImg.rectTransform.sizeDelta *= sizeMultiplier;  // 2x büyüt
        }
    }
    private void OnEnable()
    {
        if (eventHub != null)
        {
            eventHub.MovesChanged.RemoveListener(SetNumber);
            eventHub.MovesChanged.AddListener(SetNumber);
        }
    }

    private void OnDisable()
    {
        if (eventHub != null)
            eventHub.MovesChanged.RemoveListener(SetNumber);
    }

}
