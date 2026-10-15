using UnityEngine;

namespace Farhad
{
    // The far side of the mountain. Reaching it ends the prototype run.
    [RequireComponent(typeof(Collider2D))]
    public class GoalZone : MonoBehaviour
    {
        public bool Reached { get; private set; }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<FarhadController>() != null) Reached = true;
        }
    }
}
