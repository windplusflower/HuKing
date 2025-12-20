using System;
using System.Collections.Generic;
using UnityEngine;

namespace RingLib.Utils
{
    public class InputManager : MonoBehaviour
    {
        public interface Input
        {
            public void Update();
            public bool Pressed();
            public void Clear();
        }

        public class ContinuousInput : Input
        {
            private readonly InputManager inputManager;
            private readonly Func<bool> check;

            private bool pressed;

            public ContinuousInput(InputManager inputManager, Func<bool> check)
            {
                this.inputManager = inputManager;
                this.check = check;
            }

            public void Update()
            {
                pressed = check();
            }

            public bool Pressed()
            {
                inputManager.InternalUpdate();
                return pressed;
            }

            public void Clear()
            {
                pressed = false;
            }
        }

        private class DisjointInputs : Input
        {
            private readonly Input[] inputs;
            private readonly bool[] currentlyPressed;
            private readonly List<int> currentlyPressedIndices = new();

            public DisjointInputs(Input[] inputs)
            {
                this.inputs = inputs;
                currentlyPressed = new bool[inputs.Length];
            }

            public void Update()
            {
                for (int i = 0; i < inputs.Length; i++)
                {
                    var input = inputs[i];
                    input.Update();
                    if (input.Pressed() && !currentlyPressed[i])
                    {
                        currentlyPressed[i] = true;
                        currentlyPressedIndices.Add(i);
                    }
                    else if (!input.Pressed() && currentlyPressed[i])
                    {
                        currentlyPressed[i] = false;
                        currentlyPressedIndices.Remove(i);
                    }
                }
                for (int i = 0; i + 1 < currentlyPressedIndices.Count; i++)
                {
                    var input = inputs[currentlyPressedIndices[i]];
                    input.Clear();
                }
            }

            public bool Pressed()
            {
                Log.LogError(GetType().Name, "DisjointInputs.Pressed() is not implemented");
                return false;
            }

            public void Clear()
            {
                Log.LogError(GetType().Name, "DisjointInputs.Clear() is not implemented");
            }
        }

        public class InstantInput : Input
        {
            private readonly InputManager inputManager;
            private readonly Func<bool> check;
            private readonly float buffer;

            private float timer;
            private bool pressed;

            public delegate void OnPressedHandler();
            public event OnPressedHandler OnPressed;

            public InstantInput(InputManager inputManager, Func<bool> check, float buffer)
            {
                this.inputManager = inputManager;
                this.check = check;
                this.buffer = buffer;
            }

            public void Update()
            {
                if (check())
                {
                    timer = buffer;
                    pressed = true;
                    if (OnPressed != null)
                    {
                        OnPressed();
                        Clear();
                    }
                }
                else
                {
                    timer = Math.Max(0, timer - Time.deltaTime);
                    pressed = timer > 0;
                }
            }

            public bool Pressed()
            {
                inputManager.InternalUpdate();
                return pressed;
            }

            public void Clear()
            {
                timer = 0;
                pressed = false;
            }
        }

        private readonly List<Input> inputs = new();
        private bool updated;

        private void InternalUpdate()
        {
            if (updated)
            {
                return;
            }
            updated = true;
            foreach (var input in inputs)
            {
                input.Update();
            }
        }

        public void LateUpdate()
        {
            InternalUpdate();
            updated = false;
        }

        public ContinuousInput RegisterContinuousInput(Func<bool> check)
        {
            var input = new ContinuousInput(this, check);
            inputs.Add(input);
            return input;
        }

        public void SetDisjoint(ContinuousInput[] inputs)
        {
            foreach (var input in inputs)
            {
                if (!this.inputs.Contains(input))
                {
                    Log.LogError(GetType().Name, "One of the input may already be in a disjoint group");
                    return;
                }
            }
            foreach (var input in inputs)
            {
                this.inputs.Remove(input);
            }
            this.inputs.Add(new DisjointInputs(inputs));
        }

        public InstantInput RegisterInstantInput(Func<bool> check, float buffer = 0)
        {
            var input = new InstantInput(this, check, buffer);
            inputs.Add(input);
            return input;
        }
    }
}
