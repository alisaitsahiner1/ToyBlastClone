using UnityEngine;

public class InputBroadcaster : MonoBehaviour
{
    [SerializeField] private ToyBlast.Events.GameEventHub eventHub;
    [SerializeField] private ToyBlast.Core.GridSystem grid;

    void Update()
    {
        if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            var m = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            var w = Camera.main.ScreenToWorldPoint(m); w.z = 0f;
            var gp = grid.WorldToGridPosition(w);
            if (grid.IsValidGridPosition(gp))
                eventHub?.CellClicked?.Invoke(gp.x, gp.y);
        }
    }

}
