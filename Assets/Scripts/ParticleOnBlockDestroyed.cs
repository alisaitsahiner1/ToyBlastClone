using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{

    [SerializeField] private ToyBlast.Events.GameEventHub eventHub;
    [SerializeField] private GameObject[] particlePrefabs; // index == colorIndex


        private void OnEnable()  => eventHub.BlockDestroyed.AddListener(OnBlockDestroyedHandler);
        private void OnDisable() => eventHub.BlockDestroyed.RemoveListener(OnBlockDestroyedHandler);

        // handler
        private void OnBlockDestroyedHandler(int x, int y, int colorIndex)
        {
            if (colorIndex < 0 || colorIndex >= particlePrefabs.Length) return;

            var prefab = particlePrefabs[colorIndex];

        // world pozisyonu almak için GridSystem’a gerek yok — blok zaten yok edildi.
        // partikülü hücre merkezinde oynatmak istiyorsan GridSystem’dan world pos al:
            var grid = Object.FindFirstObjectByType<ToyBlast.Core.GridSystem>();
            var worldPos = grid.GridToWorldPosition(x, y) + Vector3.back * 0.1f;

            var go = Instantiate(prefab, worldPos, Quaternion.identity);
            var ps = go.GetComponentInChildren<ParticleSystem>();
            ps?.Play();
            if (ps != null) Destroy(go, ps.main.duration + ps.main.startLifetime.constantMax);
            else Destroy(go, 2f);
        }
}
