namespace GameStart
{
    public struct DirectionalPad
    {
        public bool Up;
        public bool Down;
        public bool Left;
        public bool Right;
    }

    public class ButtonInfo
    {
        public int XAxis { get; set; }
        public int YAxis { get; set; }

        public bool A { get; set; }
        public bool B { get; set; }
        public bool X { get; set; }
        public bool Y { get; set; }

        public bool Start { get; set; }
        public bool Select { get; set; }

        public bool LS { get; set; }
        public bool RS { get; set; }

        public DirectionalPad DPad;

        public static bool IsEqual(ButtonInfo one, ButtonInfo two)
        {
            return 
                (one.A == two.A &&
                one.B == two.B &&
                one.X == two.X &&
                one.Y == two.Y &&
                one.DPad.Up == two.DPad.Up &&
                one.DPad.Down == two.DPad.Down &&
                one.DPad.Left == two.DPad.Left &&
                one.DPad.Right == two.DPad.Right &&
                one.Start == two.Start &&
                one.LS == two.LS &&
                one.RS == two.RS);
        }
    }
}