using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO; // Stream 需要這個
using System.Linq;
using System.Reflection; // 用於讀取內嵌資源 (Assembly)
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfAnimatedGif;

namespace DesktopPet
{
    public enum PetState
    {
        Idle,
        Walking,
        Sleeping,
        Tired,
        ReturningHome
    }

    public partial class MainWindow : Window
    {
        private PerformanceCounter cpuCounter;
        private DispatcherTimer aiTimer;
        private DispatcherTimer physicsTimer;

        private List<BallWindow> balls = new List<BallWindow>();

        private PetState currentState = PetState.Idle;
        private Random random = new Random();
        private bool isDragging = false;

        private double velocityY = 0;
        private double gravity = 2.0;
        private double bounce = -0.4;
        private double walkSpeed = 2.5;
        private int walkDirection = 1;
        private double scale = 0.5;
        private int tickCounter = 0;

        private int currentHealth = 100;
        private const int MaxHealth = 100;
        private const int HealthDecreaseAmount = 10;
        private const int TiredThreshold = 50;
        private const int ExhaustedThreshold = 10;

        private const float HighCpuThreshold = 60.0f;
        private int bottomMargin = 0;
        private int visualFloorOffset = 0;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closed += (s, e) => { foreach (var b in balls) b.Close(); };
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 在 MainWindow_Loaded 的 try區塊開頭加入：
                this.Icon = LoadImageFromResource("idle.gif");
                // 注意：這裡可以直接用 gif 當視窗圖示，WPF 會自動處理
                // [新增] 建立右鍵選單 (Context Menu) 以便關閉程式
                ContextMenu menu = new ContextMenu();
                MenuItem exitItem = new MenuItem();
                exitItem.Header = "關閉寵物 (Exit)";
                exitItem.Click += (s, args) =>
                {
                    Application.Current.Shutdown();
                };
                menu.Items.Add(exitItem);
                this.ContextMenu = menu;

                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue();

                aiTimer = new DispatcherTimer();
                aiTimer.Interval = TimeSpan.FromSeconds(10);
                aiTimer.Tick += AiTimer_Tick;
                aiTimer.Start();

                physicsTimer = new DispatcherTimer();
                physicsTimer.Interval = TimeSpan.FromMilliseconds(16);
                physicsTimer.Tick += PhysicsTimer_Tick;
                physicsTimer.Start();

                MainStackPanel.LayoutTransform = new ScaleTransform(scale, scale);
                PetImage.RenderTransformOrigin = new Point(0.5, 0.5);

                HealthBar.Maximum = MaxHealth;
                HealthBar.Value = currentHealth;

                UpdateState(PetState.Idle);
                ForceToBottomRight();

                Debug.WriteLine("初始化完成");
            }
            catch (Exception ex)
            {
                MessageBox.Show("初始化錯誤: " + ex.Message);
            }
        }

        private void BallButton_Click(object sender, RoutedEventArgs e)
        {
            var ball = new BallWindow();
            ball.Show();
            balls.Add(ball);
        }

        private void AiTimer_Tick(object sender, EventArgs e)
        {
            if (isDragging || velocityY != 0 || currentState == PetState.ReturningHome) return;

            bool isChasingBall = balls.Any() && currentHealth > ExhaustedThreshold;

            if (currentHealth <= ExhaustedThreshold)
            {
                if (currentState != PetState.Sleeping) UpdateState(PetState.Sleeping);
                return;
            }

            float cpuUsage = cpuCounter.NextValue();
            if (cpuUsage > HighCpuThreshold)
            {
                if (currentState != PetState.Tired)
                {
                    if (walkDirection == 0) walkDirection = random.Next(0, 2) == 0 ? -1 : 1;
                    UpdateState(PetState.Tired);
                }
                return;
            }

            if (isChasingBall)
            {
                if (currentState == PetState.Idle)
                {
                    UpdateState(currentHealth <= TiredThreshold ? PetState.Tired : PetState.Walking);
                }
                return;
            }

            if (random.NextDouble() < 0.4) return;

            int roll = random.Next(0, 100);
            PetState newState;

            if (currentHealth <= TiredThreshold)
            {
                if (roll < 60) newState = PetState.Idle;
                else newState = PetState.Tired;
            }
            else
            {
                if (roll < 60) newState = PetState.Idle;
                else newState = PetState.Walking;
            }

            if (newState == PetState.Walking || newState == PetState.Tired)
            {
                walkDirection = random.Next(0, 2) == 0 ? -1 : 1;
            }

            if (newState != currentState)
            {
                UpdateState(newState);
            }
            else if (currentState == PetState.Idle)
            {
                UpdateState(PetState.Idle);
            }
        }

        private void PhysicsTimer_Tick(object sender, EventArgs e)
        {
            for (int i = balls.Count - 1; i >= 0; i--)
            {
                var ball = balls[i];
                if (!ball.IsLoaded)
                {
                    balls.RemoveAt(i);
                    continue;
                }
                ball.UpdatePhysics();
            }

            if (isDragging) return;

            var workArea = SystemParameters.WorkArea;
            double floorY = workArea.Height - this.ActualHeight - bottomMargin + visualFloorOffset;

            tickCounter++;
            if (tickCounter >= 60)
            {
                tickCounter = 0;

                if (currentState == PetState.Walking || currentState == PetState.Tired)
                {
                    currentHealth = Math.Max(0, currentHealth - 5);
                    if (currentHealth <= ExhaustedThreshold) UpdateState(PetState.Sleeping);
                    else if (currentState == PetState.Walking && currentHealth <= TiredThreshold) UpdateState(PetState.Tired);
                }
                else if (currentState == PetState.Idle)
                {
                    if (currentHealth < MaxHealth)
                    {
                        int oldHealth = currentHealth;
                        currentHealth = Math.Min(MaxHealth, currentHealth + 2);
                        if (oldHealth <= TiredThreshold && currentHealth > TiredThreshold) UpdateState(PetState.Idle);
                    }
                }
                else if (currentState == PetState.Sleeping)
                {
                    if (currentHealth < MaxHealth) currentHealth = Math.Min(MaxHealth, currentHealth + 5);
                }

                HealthBar.Value = currentHealth;
            }

            if (balls.Any() && currentHealth > ExhaustedThreshold && (currentState == PetState.Walking || currentState == PetState.Tired || currentState == PetState.Idle))
            {
                BallWindow closestBall = null;
                double minDistance = double.MaxValue;
                double petCenterX = this.Left + (this.ActualWidth / 2);
                double petCenterY = this.Top + (this.ActualHeight / 2);

                Rect petRect = new Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight);

                foreach (var ball in balls)
                {
                    double ballX = ball.Left + (ball.Width / 2);
                    double ballY = ball.Top + (ball.Height / 2);
                    double dist = Math.Abs(ballX - petCenterX);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestBall = ball;
                    }

                    Rect ballRect = new Rect(ball.Left, ball.Top, ball.Width, ball.Height);

                    if (petRect.IntersectsWith(ballRect))
                    {
                        Vector kickDir = new Vector(ballX - petCenterX, ballY - petCenterY);
                        kickDir.Normalize();
                        if (double.IsNaN(kickDir.X) || double.IsNaN(kickDir.Y))
                            kickDir = new Vector(random.NextDouble() - 0.5, -1);

                        ball.Kick(kickDir * 40);
                    }
                }

                if (closestBall != null)
                {
                    double ballX = closestBall.Left + (closestBall.Width / 2);
                    if (Math.Abs(ballX - petCenterX) > 10)
                    {
                        walkDirection = (ballX > petCenterX) ? 1 : -1;
                        if (currentState == PetState.Idle)
                        {
                            UpdateState(currentHealth <= TiredThreshold ? PetState.Tired : PetState.Walking);
                        }
                    }
                }
            }

            if (this.Top < floorY - 2)
            {
                velocityY += gravity;
                this.Top += velocityY;

                if (this.Top >= floorY)
                {
                    this.Top = floorY;
                    velocityY = (Math.Abs(velocityY) > 2) ? velocityY * bounce : 0;

                    if (currentState == PetState.Sleeping && currentHealth > ExhaustedThreshold)
                    {
                        UpdateState(PetState.Idle);
                    }
                }
            }
            else
            {
                velocityY = 0;
                if (Math.Abs(this.Top - floorY) > 2) this.Top = floorY;

                if (currentState == PetState.Walking || currentState == PetState.Tired || currentState == PetState.ReturningHome)
                {
                    double currentSpeed = (currentState == PetState.Tired) ? 1.0 : walkSpeed;

                    if (currentState == PetState.ReturningHome)
                    {
                        double targetLeft = workArea.Width - this.ActualWidth - 20;
                        if (this.Left < targetLeft)
                        {
                            this.Left += currentSpeed;
                            walkDirection = 1;
                        }
                        else
                        {
                            this.Left = targetLeft;
                            UpdateState(PetState.Idle);
                            return;
                        }
                    }
                    else
                    {
                        this.Left += currentSpeed * walkDirection;

                        if (this.Left <= 0) walkDirection = 1;
                        else if (this.Left + this.ActualWidth >= workArea.Width) walkDirection = -1;
                    }

                    UpdateDirectionTransform(walkDirection);
                }
            }
            this.Topmost = true;
        }

        private void UpdateDirectionTransform(int direction)
        {
            var transform = new ScaleTransform();
            transform.ScaleX = direction == 1 ? -1 : 1;
            transform.ScaleY = 1;
            PetImage.RenderTransform = transform;
        }

        // 讀取內嵌資源的方法
        private BitmapImage LoadImageFromResource(string imageName)
        {
            string resourceName = $"DesktopPet.Images.{imageName}";
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return null;

                    var memoryStream = new MemoryStream();
                    stream.CopyTo(memoryStream);
                    memoryStream.Position = 0;

                    var image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = memoryStream;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.EndInit();
                    image.Freeze();

                    return image;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入圖片失敗: {ex.Message}");
                return null;
            }
        }

        private void UpdateState(PetState newState)
        {
            currentState = newState;
            string imageName = "normal.gif";

            switch (currentState)
            {
                case PetState.Idle:
                    if (currentHealth <= TiredThreshold) imageName = "tired.gif";
                    else imageName = "idle.gif";
                    break;
                case PetState.Walking: case PetState.ReturningHome: imageName = "walk.gif"; break;
                case PetState.Sleeping: imageName = "sleep.gif"; break;
                case PetState.Tired: imageName = "tired.gif"; break;
            }

            var imageBitmap = LoadImageFromResource(imageName);

            if (imageBitmap == null)
            {
                imageBitmap = LoadImageFromResource("normal.gif");
            }

            if (imageBitmap == null) return;

            if (ImageBehavior.GetAnimatedSource(PetImage) != imageBitmap)
            {
                ImageBehavior.SetAnimatedSource(PetImage, imageBitmap);
            }

            if (currentState != PetState.Walking && currentState != PetState.ReturningHome && currentState != PetState.Tired)
            {
                PetImage.RenderTransform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
            velocityY = 0;
            Point startPoint = new Point(this.Left, this.Top);

            if (currentHealth > ExhaustedThreshold)
            {
                if (currentState == PetState.Walking || currentState == PetState.Sleeping || currentState == PetState.ReturningHome || currentState == PetState.Tired)
                {
                    UpdateState(PetState.Idle);
                }
            }

            try
            {
                this.DragMove();
            }
            finally
            {
                isDragging = false;
            }

            double distance = Point.Subtract(new Point(this.Left, this.Top), startPoint).Length;
            if (distance < 5) DecreaseHealth(HealthDecreaseAmount);
        }

        private void DecreaseHealth(int amount)
        {
            currentHealth = Math.Max(0, currentHealth - amount);
            HealthBar.Value = currentHealth;
            if (currentHealth > ExhaustedThreshold) UpdateState(PetState.ReturningHome);
            else UpdateState(PetState.Sleeping);
        }

        private void ForceToBottomRight()
        {
            var workArea = SystemParameters.WorkArea;
            this.Top = workArea.Height - this.ActualHeight - bottomMargin + visualFloorOffset;
            this.Left = workArea.Width - this.ActualWidth - 20;
            velocityY = 0;
            if (currentState != PetState.Tired && currentState != PetState.Sleeping) UpdateState(PetState.Idle);
        }
    }

    public class BallWindow : Window
    {
        public double VelocityX { get; set; }
        public double VelocityY { get; set; }

        private const double Gravity = 0.5;
        private const double Elasticity = 0.9;
        private const double Friction = 0.995;
        private const double GroundFriction = 0.98;

        private DispatcherTimer lifeTimer;

        public BallWindow()
        {
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.Topmost = true;
            this.ShowInTaskbar = false;
            this.Width = 10;
            this.Height = 10;
            this.ResizeMode = ResizeMode.NoResize;

            var ball = new Ellipse
            {
                Fill = Brushes.Red,
                Stroke = Brushes.DarkRed,
                StrokeThickness = 2,
                Width = 10,
                Height = 10
            };

            var grid = new Grid();
            grid.Children.Add(ball);
            grid.Children.Add(new Ellipse
            {
                Width = 3,
                Height = 3,
                Fill = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                Margin = new Thickness(2, 2, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            });
            this.Content = grid;

            var workArea = SystemParameters.WorkArea;
            this.Left = workArea.Width - 100;
            this.Top = 0;

            var rnd = new Random();
            VelocityX = rnd.Next(-15, -5);
            VelocityY = rnd.Next(-5, 5);

            lifeTimer = new DispatcherTimer();
            lifeTimer.Interval = TimeSpan.FromMinutes(10);
            lifeTimer.Tick += (s, e) => { this.Close(); };
            lifeTimer.Start();
        }

        public void UpdatePhysics()
        {
            var workArea = SystemParameters.WorkArea;

            VelocityY += Gravity;
            this.Left += VelocityX;
            this.Top += VelocityY;

            VelocityX *= Friction;
            VelocityY *= Friction;

            if (this.Top + this.Height > workArea.Height)
            {
                this.Top = workArea.Height - this.Height;
                VelocityY = -VelocityY * Elasticity;
                VelocityX *= GroundFriction;
            }
            else if (this.Top < 0)
            {
                this.Top = 0;
                VelocityY = -VelocityY * Elasticity;
            }

            if (this.Left + this.Width > workArea.Width)
            {
                this.Left = workArea.Width - this.Width;
                VelocityX = -VelocityX * Elasticity;
            }
            else if (this.Left < 0)
            {
                this.Left = 0;
                VelocityX = -VelocityX * Elasticity;
            }

            if (Math.Abs(VelocityY) < 0.5 && this.Top > workArea.Height - this.Height - 2)
            {
                VelocityY = 0;
            }
        }

        public void Kick(Vector velocity)
        {
            this.VelocityX = velocity.X;
            this.VelocityY = velocity.Y;
        }
    }
}