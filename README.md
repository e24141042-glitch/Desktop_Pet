# DesktopPet

A sophisticated virtual pet simulator for your desktop, built with C# and WPF.

## Features

This application brings a dynamic, interactive pet to your desktop with a rich set of features:

### State Management & Behavior
*   **Complex State System**: The pet's behavior is governed by a state machine, transitioning between states like `Idle`, `Walking`, `Sleeping`, `Tired`, and `ReturningHome`.
*   **AI-Driven Decisions**: A timer-based "brain" makes high-level decisions, influencing the pet's state based on factors like health, system CPU usage, and random chance.
*   **Physics Simulation**: A high-frequency physics engine simulates gravity, bouncing, and movement, including collision detection with screen edges.

### Health & Growth
*   **Health System**: The pet's health decreases with activity and increases with rest or feeding. Low health triggers `Tired` and `Sleeping` states to recover.
*   **Growth & Reproduction**: After being fed a certain number of times (by collecting food), the pet can reproduce, spawning a new, smaller "newborn" pet that grows over time.

### Customization and Interaction
*   **Dynamic Appearances**: You can switch the pet's "species" at runtime, which loads different sets of GIF animations from the `Images` folder.
*   **Multiple Instances**: The application supports multiple pets on screen at once, each with its own unique attributes.
*   **Interactive Context Menu**: Right-click the pet to access a menu with options to:
    *   Feed the pet
    *   Spawn interactive balls
    *   Change its species
    *   Summon more pets
    *   Close the application

### Interactive Elements
The application includes interactive objects that both the user and the pet can engage with:

*   **Red Ball (Toy/Collectible)**: The pet can "kick" this ball around. The user can click to "catch" it, which functions as a mini-game to earn food.
*   **Orange Ball (Food)**: The pet can "eat" this ball on contact to regain health and grow larger.

## Technologies Used
- C#
- WPF (Windows Presentation Foundation)
- WpfAnimatedGif (for GIF playback)

## Getting Started

### Prerequisites
- Visual Studio (or a compatible C# IDE)
- .NET Framework (version specified in `DesktopPet.csproj`)

### Building and Running
1. Clone the repository:
   `git clone <repository-url>`
2. Open the `DesktopPet.csproj` file in Visual Studio.
3. Build the solution.
4. Run the application (usually by pressing F5 in Visual Studio).

## Customization
- **Images:** To create your own pet species, add a new folder in the `Images/` directory. Place your custom `.gif` files inside, ensuring they have the standard names (`idle.gif`, `sleep.gif`, `tired.gif`, `walk.gif`). You can then switch to your new species via the pet's right-click context menu.
- **Behavior:** Modify the `MainWindow.xaml.cs` file to change the pet's core behavior, physics, and interactions.

## Screenshot/GIF
(To be added)

## Contributing
(To be added)

## License
(To be added)