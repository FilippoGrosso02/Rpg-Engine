using UnityEngine;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// The only reader of keys and mouse buttons for the battle menu. Feeds <see cref="BattleMenuModel"/>; the mouse is NOT used to
    /// point at UI (the cursor is locked to the reticle) — its buttons only commit or back out.
    /// </summary>
    public class BattleInputController : MonoBehaviour
    {
        /// <summary>Holding Q / E rotates a list: nothing for this long after the first press, then repeat at the interval below.</summary>
        public const float RepeatDelaySeconds = 0.35f;
        public const float RepeatIntervalSeconds = 0.12f;

        private readonly BattleMenuModel _model = new BattleMenuModel();
        private float _heldFor;
        private float _nextRepeatAt;
        private KeyCode _heldKey = KeyCode.None;

        public BattleMenuModel Model => _model;

        /// <summary>True while the physical key behind <paramref name="key"/> is held, for the command bar's pressed state.</summary>
        public bool IsHeld(MenuKey key)
        {
            switch (key)
            {
                case MenuKey.Q: return Input.GetKey(KeyCode.Q);
                case MenuKey.E: return Input.GetKey(KeyCode.E);
                case MenuKey.R: return Input.GetKey(KeyCode.R);
                case MenuKey.Tab: return Input.GetKey(KeyCode.Tab);
                case MenuKey.Escape: return Input.GetKey(KeyCode.Escape);
                case MenuKey.Enter: return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter);
                default: return false;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _model.Tick(dt);

            if (Input.GetKeyDown(KeyCode.Tab)) _model.Press(MenuKey.Tab);
            if (Input.GetKeyDown(KeyCode.Escape)) _model.Press(MenuKey.Escape);
            if (Input.GetMouseButtonDown(1)) _model.Press(MenuKey.Back);
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space)) _model.Press(MenuKey.Commit);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) _model.Press(MenuKey.Enter);
            if (Input.GetKeyDown(KeyCode.R)) _model.Press(MenuKey.R);

            HandleRotationKey(KeyCode.Q, MenuKey.Q, dt);
            HandleRotationKey(KeyCode.E, MenuKey.E, dt);
        }

        /// <summary>
        /// Q and E act once on press. Only while a list is open does holding them repeat a rotation; Guard, open, close, Tab and
        /// Enter never repeat.
        /// </summary>
        private void HandleRotationKey(KeyCode keyCode, MenuKey key, float dt)
        {
            if (Input.GetKeyDown(keyCode))
            {
                _model.Press(key);
                _heldKey = keyCode;
                _heldFor = 0f;
                _nextRepeatAt = RepeatDelaySeconds;
                return;
            }

            if (_heldKey != keyCode)
            {
                return;
            }

            if (!Input.GetKey(keyCode))
            {
                _heldKey = KeyCode.None;
                return;
            }

            _heldFor += dt;
            bool listOpen = _model.State == MenuState.Spells || _model.State == MenuState.Items;
            if (listOpen && _heldFor >= _nextRepeatAt)
            {
                _model.Press(key);
                _nextRepeatAt = _heldFor + RepeatIntervalSeconds;
            }
        }
    }
}
