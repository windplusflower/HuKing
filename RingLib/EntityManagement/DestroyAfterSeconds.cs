using UnityEngine;

namespace RingLib.EntityManagement
{
    public class DestroyAfterSeconds : MonoBehaviour
    {
        public float Seconds;

        private void Update()
        {
            Seconds -= Time.deltaTime;
            if (Seconds <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
}
