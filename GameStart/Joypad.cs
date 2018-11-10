using SlimDX.XInput;

namespace GameStart
{
    internal class Joypad
    {
        private Controller controller;
        private ButtonInfo oldButtonInfo;
        public bool IsChanged { get; set; }

        public static Joypad TryConnect()
        {
            var pad = new Joypad();
            pad.controller = new Controller(UserIndex.One);

            if (pad.controller.IsConnected)
            {
                return pad;
            }

            return null;
        }

        public bool IsConnected()
        {
            return controller.IsConnected;
        }

        public ButtonInfo GetInput()
        {
            if (controller == null || !controller.IsConnected)
            {
                return null;
            }

            var buttonInfo = new ButtonInfo();

            var state = controller.GetState();

            buttonInfo.A = ((state.Gamepad.Buttons & GamepadButtonFlags.A) != 0);
            buttonInfo.B = ((state.Gamepad.Buttons & GamepadButtonFlags.B) != 0);
            buttonInfo.X = ((state.Gamepad.Buttons & GamepadButtonFlags.X) != 0);
            buttonInfo.Y = ((state.Gamepad.Buttons & GamepadButtonFlags.Y) != 0);

            buttonInfo.DPad.Up = ((state.Gamepad.Buttons & GamepadButtonFlags.DPadUp) != 0);
            buttonInfo.DPad.Down = ((state.Gamepad.Buttons & GamepadButtonFlags.DPadDown) != 0);
            buttonInfo.DPad.Left = ((state.Gamepad.Buttons & GamepadButtonFlags.DPadLeft) != 0);
            buttonInfo.DPad.Right = ((state.Gamepad.Buttons & GamepadButtonFlags.DPadRight) != 0);

            buttonInfo.XAxis = state.Gamepad.LeftThumbX;
            buttonInfo.YAxis = state.Gamepad.LeftThumbY;

            buttonInfo.Start = ((state.Gamepad.Buttons & GamepadButtonFlags.Start) != 0);

            SaveState(buttonInfo);

            return buttonInfo;
        }

        private void SaveState(ButtonInfo newButtonInfo)
        {
            if (oldButtonInfo == null || 
                !ButtonInfo.IsEqual(oldButtonInfo, newButtonInfo))
            {
                oldButtonInfo = newButtonInfo;
                IsChanged = true;
            }
            else
            {
                IsChanged = false;
            }
        }
    }
}