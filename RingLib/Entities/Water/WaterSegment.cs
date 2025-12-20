using System.Collections.Generic;
using System.Linq;
using RingLib.StateMachine;
using UnityEngine;
using static RingLib.StateMachine.EntityStateMachine;
using Random = UnityEngine.Random;

namespace RingLib.Entities.Water {
    public class WaterSegment : StateMachine.StateMachine {
        [State]
        public IEnumerator<Transition> Idle() {
            while (true) {
                var collisionEvents = CheckInStateEvents<CollisionEvent>();
                var collisionEvent = collisionEvents.FirstOrDefault(
                    e => e.Direction == CollisionDirection.Top && e.Source.GetComponent<EntityStateMachine>()
                );
                if (collisionEvent != null) {
                    var entityStateMachine = collisionEvent.Source.GetComponent<EntityStateMachine>();
                    v += water.impact * entityStateMachine.Velocity.y;
                }
                yield return new NoTransition();
            }
        }

        public WaterSegment left;
        public WaterSegment right;

        private BoxCollider2D boxCollider2D;
        private Vector2 originalBoxColliderSize;

        public Water water;
        private float y = 0;
        private float v = 0;

        public WaterSegment() : base(
            startState: nameof(Idle),
            globalTransitions: new()) { }

        protected override void StateMachineStart() {
            boxCollider2D = gameObject.GetComponent<BoxCollider2D>();
            originalBoxColliderSize = boxCollider2D.size;
        }

        protected override void StateMachineUpdate() {
            boxCollider2D.offset = new Vector2(0, y / 2);
            boxCollider2D.size = new Vector2(originalBoxColliderSize.x, originalBoxColliderSize.y + y);
        }

        protected override void StateMachineFixedUpdate() {
            var a = -water.stiffness * y - water.dampening * v;
            if (left != null) {
                a += water.spreading * (left.y - y);
            }
            if (right != null) {
                a += water.spreading * (right.y - y);
            }
            a += water.noise * Random.Range(-1f, 1f);
            v += a * Time.deltaTime;
            y += v * Time.deltaTime;
        }
    }
}
