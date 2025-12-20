using System;
using System.Collections.Generic;
using UnityEngine;

namespace RingLib.StateMachine
{
    public class EntityStateMachine : StateMachine
    {
        public Vector3 Position
        {
            get
            {
                return gameObject.transform.position;
            }
            set
            {
                gameObject.transform.position = value;
            }
        }
        public BoxCollider2D BoxCollider2D { get; private set; }
        public Rigidbody2D Rigidbody2D { get; private set; }
        public Vector2 Velocity
        {
            get
            {
                return Rigidbody2D.velocity;
            }
            set
            {
                Rigidbody2D.velocity = value;
            }
        }
        private readonly string terrainLayer;
        private readonly float epsilon;
        private readonly bool horizontalCornerCorrection;
        private readonly bool spriteFacingLeft;

        public enum CollisionDirection
        {
            Top,
            Left,
            Right
        }
        public class CollisionEvent : Event
        {
            public CollisionDirection Direction;
            public Vector2 Position;
            public GameObject Source;
        }
        private List<GameObject> landedRaycastCache = new();
        private List<GameObject> onLeftWallRaycastCache = new();
        private List<GameObject> onRightWallRaycastCache = new();

        public EntityStateMachine(
            string startState, Dictionary<Type, string> globalTransitions,
            string terrainLayer, float epsilon, bool horizontalCornerCorrection, bool spriteFacingLeft)
            : base(startState, globalTransitions)
        {
            this.terrainLayer = terrainLayer;
            this.epsilon = epsilon;
            this.horizontalCornerCorrection = horizontalCornerCorrection;
            this.spriteFacingLeft = spriteFacingLeft;
        }

        protected virtual void EntityStateMachineStart() { }

        protected sealed override void StateMachineStart()
        {
            BoxCollider2D = gameObject.GetComponent<BoxCollider2D>();
            Rigidbody2D = gameObject.GetComponent<Rigidbody2D>();
            EntityStateMachineStart();
        }

        protected virtual void EntityStateMachineUpdate() { }

        protected sealed override void StateMachineUpdate()
        {
            EntityStateMachineUpdate();
        }

        protected virtual void EntityStateMachineFixedUpdate() { }

        private void Raycast(List<Vector2> rays, Vector2 direction, CollisionDirection collisionDirection, ref List<GameObject> cache)
        {
            Dictionary<GameObject, List<Vector2>> collisionPoints = new();
            foreach (var ray in rays)
            {
                var raycastHit2D = Physics2D.Raycast(
                    ray, direction, epsilon, 1 << LayerMask.NameToLayer(terrainLayer));
                if (raycastHit2D.collider != null)
                {
                    var gameObject = raycastHit2D.collider.gameObject;
                    if (!collisionPoints.ContainsKey(gameObject))
                    {
                        collisionPoints[gameObject] = new List<Vector2>();
                    }
                    collisionPoints[gameObject].Add(raycastHit2D.point);
                }
            }
            List<GameObject> newCache = new();
            foreach (var (gameObject, points) in collisionPoints)
            {
                newCache.Add(gameObject);
                if (!cache.Contains(gameObject))
                {
                    var sum = Vector2.zero;
                    foreach (var point in points)
                    {
                        sum += point;
                    }
                    var average = sum / points.Count;
                    Log.LogInfo(GetType().Name, $"Sending collision event ({collisionDirection}, {average}) to {gameObject.name}");
                    gameObject.BroadcastEventInChildren(new CollisionEvent
                    {
                        Direction = collisionDirection,
                        Position = average,
                        Source = this.gameObject
                    });
                }
            }
            cache = newCache;
        }

        private void LandedRaycast()
        {
            var rays = new List<Vector2>
            {
                BoxCollider2D.bounds.min,
                new(BoxCollider2D.bounds.center.x, BoxCollider2D.bounds.min.y),
                new(BoxCollider2D.bounds.max.x, BoxCollider2D.bounds.min.y)
            };
            Raycast(rays, -Vector2.up, CollisionDirection.Top, ref landedRaycastCache);
        }

        public bool Landed()
        {
            if (Velocity.y > 0)
            {
                return false;
            }
            return landedRaycastCache.Count > 0;
        }

        private void OnLeftWallRaycast()
        {
            var rays = new List<Vector2>
            {
                BoxCollider2D.bounds.min,
                new(BoxCollider2D.bounds.min.x, BoxCollider2D.bounds.center.y),
                new(BoxCollider2D.bounds.min.x, BoxCollider2D.bounds.max.y)
            };
            Raycast(rays, -Vector2.right, CollisionDirection.Right, ref onLeftWallRaycastCache);
        }

        public bool OnLeftWall()
        {
            if (Velocity.x > 0)
            {
                return false;
            }
            return onLeftWallRaycastCache.Count > 0;
        }

        private void OnRightWallRaycast()
        {
            var rays = new List<Vector2>
            {
                BoxCollider2D.bounds.max,
                new(BoxCollider2D.bounds.max.x, BoxCollider2D.bounds.center.y),
                new(BoxCollider2D.bounds.max.x, BoxCollider2D.bounds.min.y)
            };
            Raycast(rays, Vector2.right, CollisionDirection.Left, ref onRightWallRaycastCache);
        }

        public bool OnRightWall()
        {
            if (Velocity.x < 0)
            {
                return false;
            }
            return onRightWallRaycastCache.Count > 0;
        }

        protected sealed override void StateMachineFixedUpdate()
        {
            // Collider bounds are only updated in FixedUpdate even if GameObject has been moved in Update

            // Cache raycast results since collider bounds would remain the same until next FixedUpdate
            LandedRaycast();
            OnLeftWallRaycast();
            OnRightWallRaycast();

            // Do corner correction in FixedUpdate to avoid duplicated correction
            if (horizontalCornerCorrection)
            {
                // Physics 2D can get stuck even if Landed() is false
                if (!Landed() && Velocity.x == 0 && Velocity.y <= 0)
                {
                    var rays = new List<Vector2>
                    {
                        new(BoxCollider2D.bounds.min.x - epsilon / 3, BoxCollider2D.bounds.min.y),
                        new(BoxCollider2D.bounds.max.x + epsilon / 3, BoxCollider2D.bounds.min.y)
                    };
                    bool raycast(Vector2 ray)
                    {
                        var raycastHit2D = Physics2D.Raycast(
                           ray, -Vector2.up, epsilon, 1 << LayerMask.NameToLayer(terrainLayer));
                        return raycastHit2D.collider != null;
                    }
                    var left = raycast(rays[0]);
                    var right = raycast(rays[1]);
                    if (left && !right)
                    {
                        Position = new Vector3(Position.x + epsilon / 3 * 2, Position.y, Position.z);
                        Log.LogInfo(GetType().Name, "Horizontal corner correction to the right");
                    }
                    else if (right && !left)
                    {
                        Position = new Vector3(Position.x - epsilon / 3 * 2, Position.y, Position.z);
                        Log.LogInfo(GetType().Name, "Horizontal corner correction to the left");
                    }
                }
            }

            EntityStateMachineFixedUpdate();
        }

        public float Direction()
        {
            var direction = Mathf.Sign(gameObject.transform.localScale.x);
            return spriteFacingLeft ? -direction : direction;
        }

        public void Turn()
        {
            var localScale = gameObject.transform.localScale;
            localScale.x *= -1;
            gameObject.transform.localScale = localScale;
        }
    }
}
