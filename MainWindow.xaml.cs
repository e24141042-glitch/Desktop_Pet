using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private static readonly List<MainWindow> ActivePets = new List<MainWindow>();

        // [新增] 成長計時器
        private DispatcherTimer growthTimer;

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
        public int MaxHealth { get; private set; } = 100;

        private int foodCount = 0;
        private const int HealthGainPerFood = 30;

        private int fedTimes = 0;

        private const int HealthDecreaseAmount = 5;
        private const int TiredThreshold = 50;
        private const int ExhaustedThreshold = 20;

        private const float HighCpuThreshold = 60.0f;
        private int bottomMargin = 0;
        private int visualFloorOffset = 0;
        private string speciesName = "Default";

        public MainWindow()
        {
            InitializeComponent();

            // [新增] 個體差異初始化
            MaxHealth = random.Next(80, 200);
            currentHealth = MaxHealth;
            walkSpeed = random.NextDouble() * 3 + 1; // 1.0 ~ 4.0
            scale = random.NextDouble() * 0.4 + 0.4; // 0.4 ~ 0.8 (如果不是新生兒)

            this.Loaded += MainWindow_Loaded;
            this.Closed += (s, e) => { ActivePets.Remove(this); foreach (var b in balls) b.Close(); };
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ActivePets.Add(this);
                speciesName = ChooseSpecies();
                ContextMenu menu = new ContextMenu();

                MenuItem feedItem = new MenuItem();
                feedItem.Header = "🍗 餵食 (Feed)";
                feedItem.Click += FeedPet;
                menu.Items.Add(feedItem);

                MenuItem ballItem = new MenuItem();
                ballItem.Header = "🔴 生成食物球 (Catch Ball)";
                ballItem.Click += SpawnFoodBall;
                menu.Items.Add(ballItem);

                MenuItem scatterItem = new MenuItem();
                scatterItem.Header = "✨ 潑灑食物 (Scatter Food)";
                scatterItem.Click += ScatterFood;
                menu.Items.Add(scatterItem);

                MenuItem speciesRoot = new MenuItem();
                speciesRoot.Header = "🦄 切換物種 (Switch Species)";
                BuildSpeciesMenu(speciesRoot);
                menu.Items.Add(speciesRoot);

                MenuItem swarmItem = new MenuItem();
                swarmItem.Header = "👨‍👩‍👧‍👦 召喚夥伴 (Summon Swarm)";
                swarmItem.Click += SummonSwarm;
                menu.Items.Add(swarmItem);

                menu.Items.Add(new Separator());

                MenuItem exitItem = new MenuItem();
                exitItem.Header = "❌ 關閉寵物 (Exit)";
                exitItem.Click += (s, args) => { this.Close(); };
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

                var _panel = GetMainStackPanel();
                if (_panel != null) _panel.LayoutTransform = new ScaleTransform(scale, scale);
                var _img = GetPetImage();
                if (_img != null) _img.RenderTransformOrigin = new Point(0.5, 0.5);

                var _hb = GetHealthBar();
                if (_hb != null)
                {
                    _hb.Maximum = MaxHealth;
                    _hb.Value = currentHealth;
                }
                UpdateStatusUI();

                UpdateState(PetState.Idle);
                ForceToBottomRight();

                Debug.WriteLine("初始化完成");
            }
            catch (Exception ex)
            {
                MessageBox.Show("初始化錯誤: " + ex.Message);
            }
        }

        private void UpdateStatusUI()
        {
            var _hb = GetHealthBar();
            if (_hb != null)
                _hb.ToolTip = $"體力: {currentHealth}/{MaxHealth}\n食物: {foodCount} 🍎\n已餵食: {fedTimes}/3 (繁殖進度)\n體型: {scale:F1}";
        }

        private void SummonSwarm(object sender, RoutedEventArgs e)
        {
            if (ActivePets.Count >= 20)
            {
                MessageBox.Show("寵物太多了！最多只能有 20 隻。");
                return;
            }

            int count = 5;
            for (int i = 0; i < count; i++)
            {
                if (ActivePets.Count >= 20) break;
                SpawnNewPet(true);
            }
        }

        private void ScatterFood(object sender, RoutedEventArgs e)
        {
            var area = SystemParameters.WorkArea;
            int count = 15;
            for (int i = 0; i < count; i++)
            {
                var ball = new BallWindow(true);
                ball.Left = random.Next(0, (int)(area.Width - 80));
                ball.Top = random.Next(-80, 0);
                ball.VelocityX = random.Next(-3, 4);
                ball.VelocityY = random.Next(2, 8);
                ball.Show();
                balls.Add(ball);
            }
        }

        private void SpawnFoodBall(object sender, RoutedEventArgs e)
        {
            var ball = new BallWindow(false);

            ball.BallCaught += () =>
            {
                foodCount++;
                UpdateStatusUI();
                this.Title = "抓到了！食物+1";
            };

            ball.Show();
            balls.Add(ball);
        }

        private void FeedPet(object sender, RoutedEventArgs e)
        {
            if (foodCount > 0)
            {
                if (currentHealth >= MaxHealth)
                {
                    MessageBox.Show("我已經吃飽了！(HP 滿)", "飽飽的");
                    return;
                }

                foodCount--;

                currentHealth = Math.Min(MaxHealth, currentHealth + HealthGainPerFood);
                HealthBar.Value = currentHealth;

                fedTimes++;
                if (fedTimes >= 3)
                {
                    fedTimes = 0;
                    SpawnNewPet(false); // 繁殖！
                }

                UpdateStatusUI();

                if (currentState == PetState.Sleeping || currentState == PetState.Tired)
                {
                    UpdateState(PetState.Idle);
                }
            }
            else
            {
                MessageBox.Show("沒有食物了！\n請點擊右鍵「生成食物球」，然後在螢幕上抓住它！", "肚子餓");
            }
        }

        private void SpawnNewPet(bool isRandom = false)
        {
            if (ActivePets.Count >= 20)
            {
                Debug.WriteLine("已達最大數量 20，無法繁殖");
                return;
            }

            try
            {
                MainWindow newPet = new MainWindow();
                newPet.SetSpecies(this.speciesName);

                if (!isRandom)
                {
                    // [關鍵修改] 將新寵物設定為新生兒狀態
                    newPet.SetAsNewborn();
                }
                else
                {
                     // 隨機生成成體
                     newPet.Left = random.Next(0, (int)SystemParameters.PrimaryScreenWidth - 100);
                     newPet.Top = random.Next(0, (int)SystemParameters.PrimaryScreenHeight - 100);
                }

                if (!isRandom)
                {
                    newPet.Left = this.Left - 50;
                    newPet.Top = this.Top;
                }

                newPet.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("繁殖失敗: " + ex.Message);
            }
        }

        // [新增] 設定為新生兒並啟動成長邏輯
        public void SetAsNewborn()
        {
            this.scale = 0.2; // 初始大小設為 0.2

            // 初始化成長計時器
            growthTimer = new DispatcherTimer();
            growthTimer.Interval = TimeSpan.FromMinutes(30); // 每 30 分鐘長大一次
            growthTimer.Tick += (s, e) =>
            {
                if (scale < 0.6)
                {
                    scale += 0.1;

                    // 簡單的浮點數校正 (避免 0.3000000004 這種情況)
                    if (scale > 0.59 && scale < 0.61) scale = 0.6;

                    // 更新 UI 縮放
                    var _panel = GetMainStackPanel();
                    if (_panel != null) _panel.LayoutTransform = new ScaleTransform(scale, scale);
                    UpdateStatusUI(); // 更新 Tooltip 顯示的體型

                    // 檢查是否達到上限
                    if (scale >= 0.6)
                    {
                        scale = 0.6;
                        growthTimer.Stop(); // 停止成長
                        Debug.WriteLine("寵物已長大成型 (Scale 0.6)");
                    }
                    else
                    {
                        Debug.WriteLine($"寵物長大中... 目前 Scale: {scale}");
                    }
                }
            };
            growthTimer.Start();
        }

        private void AiTimer_Tick(object sender, EventArgs e)
        {
            if (isDragging || velocityY != 0 || currentState == PetState.ReturningHome) return;

            if (currentState == PetState.Sleeping && currentHealth < MaxHealth) return;

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

            // --- Pet Collision Detection ---
            foreach (var otherPet in ActivePets.Where(p => p != this))
            {
                if (!otherPet.IsLoaded) continue;

                Rect thisRect = new Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight);
                Rect otherRect = new Rect(otherPet.Left, otherPet.Top, otherPet.ActualWidth, otherPet.ActualHeight);

                if (thisRect.IntersectsWith(otherRect))
                {
                    Vector pushVector = new Vector(this.Left - otherPet.Left, this.Top - otherPet.Top);

                    if (pushVector.Length < 1) // Exactly overlapping or too close
                    {
                        pushVector = new Vector(random.Next(-5, 5), random.Next(-5, 0));
                    }

                    pushVector.Normalize();
                    
                    double pushAmount = 2.0;
                    this.Left += pushVector.X * pushAmount;
                    this.Top += pushVector.Y * pushAmount;
                    
                    if (this.Top < otherPet.Top && velocityY > 0)
                    {
                        velocityY *= -0.1;
                    }
                }
            }
            // --- End Pet Collision ---


            var workArea = SystemParameters.WorkArea;
            double baseFloorY = workArea.Height - this.ActualHeight - bottomMargin + visualFloorOffset;
            double floorY = baseFloorY;

            tickCounter++;
            if (tickCounter >= 60)
            {
                tickCounter = 0;

                if (currentState == PetState.Walking || currentState == PetState.Tired)
                {
                    currentHealth = Math.Max(0, currentHealth - 2);
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
                    if (currentHealth < MaxHealth)
                    {
                        currentHealth = Math.Min(MaxHealth, currentHealth + 5);
                    }

                    if (currentHealth >= MaxHealth)
                    {
                        Debug.WriteLine("體力已滿，立即起床");
                        UpdateState(PetState.Idle);
                    }
                }

                var _hb = GetHealthBar();
                if (_hb != null) _hb.Value = currentHealth;
                UpdateStatusUI();
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
                        if (ball.IsFood)
                        {
                            // 吃到食物：成長 + 回血
                            ball.Close();
                            scale = Math.Min(2.0, scale + 0.05); // 最大2.0
                            var _panel = GetMainStackPanel();
                            if (_panel != null) _panel.LayoutTransform = new ScaleTransform(scale, scale);
                            currentHealth = Math.Min(MaxHealth, currentHealth + 20);
                            var _hb2 = GetHealthBar();
                            if (_hb2 != null) _hb2.Value = currentHealth;
                            UpdateStatusUI();
                        }
                        else
                        {
                            Vector kickDir = new Vector(ballX - petCenterX, ballY - petCenterY);
                            kickDir.Normalize();
                            if (double.IsNaN(kickDir.X) || double.IsNaN(kickDir.Y))
                                kickDir = new Vector(random.NextDouble() - 0.5, -1);

                            ball.Kick(kickDir * 40);
                        }
                    }
                }

                if (closestBall != null && !closestBall.IsFood)
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

                    if (currentState == PetState.Sleeping && currentHealth >= MaxHealth)
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
            var img = GetPetImage();
            if (img != null) img.RenderTransform = transform;
        }

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

        

        private Image GetPetImage()
        {
            return this.FindName("PetImage") as Image;
        }

        private Image GetPetImage(MainWindow w)
        {
            return w.FindName("PetImage") as Image;
        }

        private ProgressBar GetHealthBar()
        {
            return this.FindName("HealthBar") as ProgressBar;
        }

        private StackPanel GetMainStackPanel()
        {
            return this.FindName("MainStackPanel") as StackPanel;
        }

        private string ChooseSpecies()
        {
            try
            {
                string imagesPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
                if (!System.IO.Directory.Exists(imagesPath)) return "Default";
                var dirs = System.IO.Directory.GetDirectories(imagesPath).Select(System.IO.Path.GetFileName).ToList();
                if (dirs == null || dirs.Count == 0) return "Default";
                return dirs[random.Next(0, dirs.Count)];
            }
            catch
            {
                return "Default";
            }
        }

        private void BuildSpeciesMenu(MenuItem root)
        {
            root.Items.Clear();
            var all = new List<string> { "Default" };
            string imagesPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images");
            if (System.IO.Directory.Exists(imagesPath))
            {
                all.AddRange(System.IO.Directory.GetDirectories(imagesPath).Select(System.IO.Path.GetFileName));
            }
            foreach (var name in all.Distinct())
            {
                var mi = new MenuItem { Header = name, IsCheckable = true, IsChecked = name == speciesName };
                mi.Click += (s, e) => { SetSpecies(name); UpdateState(currentState); BuildSpeciesMenu(root); };
                root.Items.Add(mi);
            }
        }

        public void SetSpecies(string name)
        {
            speciesName = string.IsNullOrWhiteSpace(name) ? "Default" : name;
        }

        private BitmapImage LoadSpeciesImage(string imageName)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string path1 = System.IO.Path.Combine(baseDir, "Images", speciesName, imageName);
                if (System.IO.File.Exists(path1))
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.UriSource = new Uri(path1, UriKind.Absolute);
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.EndInit();
                    img.Freeze();
                    return img;
                }
                string path2 = System.IO.Path.Combine(baseDir, "Images", imageName);
                if (System.IO.File.Exists(path2))
                {
                    var img = new BitmapImage();
                    img.BeginInit();
                    img.UriSource = new Uri(path2, UriKind.Absolute);
                    img.CacheOption = BitmapCacheOption.OnLoad;
                    img.EndInit();
                    img.Freeze();
                    return img;
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入物種圖片失敗: {ex.Message}");
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

            var imageBitmap = LoadSpeciesImage(imageName);
            if (imageBitmap == null)
            {
                imageBitmap = LoadImageFromResource(imageName);
            }

            if (imageBitmap == null)
            {
                imageBitmap = LoadImageFromResource("normal.gif");
            }

            if (imageBitmap == null) return;

            var _img = GetPetImage();
            if (_img != null)
            {
                if (ImageBehavior.GetAnimatedSource(_img) != imageBitmap)
                {
                    ImageBehavior.SetAnimatedSource(_img, imageBitmap);
                }
            }

            if (currentState != PetState.Walking && currentState != PetState.ReturningHome && currentState != PetState.Tired)
            {
                var __img = GetPetImage();
                if (__img != null) __img.RenderTransform = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
            velocityY = 0;
            Point startPoint = new Point(this.Left, this.Top);

            if (currentState != PetState.Sleeping || currentHealth >= MaxHealth)
            {
                UpdateState(PetState.Idle);
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

            UpdateStatusUI();

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

    
}
