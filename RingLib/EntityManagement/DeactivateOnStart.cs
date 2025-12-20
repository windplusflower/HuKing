using UnityEngine;

namespace RingLib.EntityManagement
{
    public class DeactivateOnStart : MonoBehaviour
    {
        private void Start()
        {
            gameObject.SetActive(false);
        }
    }
}
