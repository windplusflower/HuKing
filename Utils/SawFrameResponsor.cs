using UnityEngine;

// 挂在电锯边框父物体上的脚本
public class SawFrameResponder : MonoBehaviour, IHitResponder
{
    // 定义一个动作，当被打时通知 Boss
    public System.Action<Vector2> OnBeenHit;

    public void Hit(HitInstance hitInstance)
    {
        // Direction 是攻击角度：0右, 90上, 180左, 270下
        // 我们将其转化为移动向量
        Vector2 moveDir = Vector2.zero;
        float angle = hitInstance.Direction;

        if (angle == 0f) moveDir = Vector2.right;      // 往右移
        else if (angle == 180f) moveDir = Vector2.left; // 往左移
        else if (angle == 90f) moveDir = Vector2.up;    // 往上移
        else if (angle == 270f) moveDir = Vector2.down; // 往下移

        // 触发回调，通知 Boss
        OnBeenHit?.Invoke(moveDir);
    }
}