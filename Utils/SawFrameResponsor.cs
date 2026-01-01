using Modding; // 确保引用了 Modding 命名空间
using UnityEngine;

public class PlatFrameResponder : MonoBehaviour, IHitResponder
{
    public System.Action<Vector2> OnBeenHit;
    public bool AllowHorizontal = true;

    public void Hit(HitInstance hitInstance)
    {
        Vector2 moveDir = Vector2.zero;
        float angle = hitInstance.Direction;

        if (angle == 0f) moveDir = Vector2.right;
        else if (angle == 180f) moveDir = Vector2.left;
        else if (angle == 90f) moveDir = Vector2.up;
        else if (angle == 270f) moveDir = Vector2.down;

        if (!AllowHorizontal && (moveDir == Vector2.right || moveDir == Vector2.left))
        {
            return;
        }

        OnBeenHit?.Invoke(moveDir);
    }
}