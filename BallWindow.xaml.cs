using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace DesktopPet
{
    public partial class BallWindow : Window
    {
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }
        public bool IsFood { get; private set; }
        public event Action BallCaught;

        private const double Gravity = 0.5;
        private const double BounceFactor = -0.7;
        private const double Friction = 0.98;

        public BallWindow(bool isFood = false)
        {
            InitializeComponent();
            this.IsFood = isFood;

            if (isFood)
            {
                BallShape.Fill = Brushes.Orange;
                BallShape.Stroke = Brushes.DarkOrange;
            }
            else
            {
                BallShape.Fill = Brushes.Red;
                BallShape.Stroke = Brushes.DarkRed;
            }

            this.MouseDown += BallWindow_MouseDown;
        }

        private void BallWindow_MouseDown(object sender, MouseButtonEventArgs e)
        {
            BallCaught?.Invoke();
            this.Close();
        }

        public void Kick(Vector force)
        {
            VelocityX = force.X;
            VelocityY = force.Y;
        }

        public void UpdatePhysics()
        {
            VelocityY += Gravity;
            VelocityX *= Friction;

            this.Left += VelocityX;
            this.Top += VelocityY;
            
            double floorY = SystemParameters.WorkArea.Height - this.ActualHeight;
            if (this.Top > floorY)
            {
                this.Top = floorY;
                VelocityY *= BounceFactor;
                if (Math.Abs(VelocityY) < 1.0) VelocityY = 0;
            }
            
            double rightWall = SystemParameters.WorkArea.Width - this.ActualWidth;
            if (this.Left < 0)
            {
                this.Left = 0;
                VelocityX *= -1;
            }
            else if (this.Left > rightWall)
            {
                this.Left = rightWall;
                VelocityX *= -1;
            }
        }
    }
}
