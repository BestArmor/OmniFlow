<div align="center">
  <h1>⚡ OmniFlow</h1>
  <p><strong>High-Performance Log Aggregator & Tailer for Windows</strong></p>
  <p>Zero-allocation log monitoring powered by <code>MemoryMappedFiles</code>, <code>Span&lt;T&gt;</code> and <code>System.Threading.Channels</code>.</p>
  
  <br/>
  
  ![Windows](https://img.shields.io/badge/Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white)
  ![.NET 8](https://img.shields.io/badge/.NET%208-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
  ![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white)
  ![WPF](https://img.shields.io/badge/WPF-512BD4?style=for-the-badge&logo=windows&logoColor=white)
  ![License](https://img.shields.io/badge/License-MIT-EC4899?style=for-the-badge)
  
  <br/><br/>
</div>

## 📖 Overview
**OmniFlow** is a high-performance, zero-allocation desktop application built for analyzing and monitoring massive log files in real-time. Designed with a Vercel/Linear-inspired dark UI, it provides developers with an intuitive, lag-free experience even when tailing files with millions of lines.

## ✨ Key Features
- 🧠 **Zero-Memory Leaks:** Utilizes `MemoryMappedFiles` and `Span<T>` to parse gigabytes of logs without OOM exceptions.
- ⚡ **Thread-Safe UI:** Implements `System.Threading.Channels` (Producer/Consumer pattern) to decouple I/O reading from UI rendering.
- 📜 **Structured JSON Support:** Zero-allocation `Utf8JsonReader` for Serilog/NLog logs. Extracts properties into dedicated columns.
- 📂 **Multi-File Aggregation:** Tail multiple files simultaneously, merged chronologically.
- 🔍 **Advanced Regex Search:** Instantly filter logs using compiled regular expressions with input debouncing.
- ⏸️ **Auto-Scroll & Pause:** Smart scroll tracking automatically pauses the live stream when you read history, and resumes when you hit the bottom.
- 📊 **Real-time Metrics & Filtering:** Instantly track and isolate Errors and Warnings.

## 🛠 Tech Stack
- **Framework:** .NET 8, WPF
- **Architecture:** MVVM (Model-View-ViewModel), Dependency Injection
- **Memory Management:** `System.Buffers.ArrayPool`, `ReadOnlySpan<byte>`, `Utf8JsonReader`
- **Concurrency:** `System.Threading.Channels`

## 🏗 Architecture
OmniFlow follows a strict Clean Architecture approach:

    OmniFlow/
    ├── Models/             # Value types (readonly record struct)
    ├── Services/           # Core logic (Channels, File Watchers, Parsers)
    ├── ViewModels/         # State & Command logic (INotifyPropertyChanged)
    └── Views/              # XAML UI components

## 🗺 Roadmap
- [x] **Multi-File Aggregation:** Tail multiple files simultaneously.
- [x] **Advanced Regex Search:** Highlight specific patterns and extract data dynamically.
- [x] **Auto-Scroll & Pause:** Smart scroll tracking pauses stream when reading history.
- [ ] **Docker/WSL Integration:** Stream logs directly from Docker containers or WSL distributions.
- [ ] **Export Functionality:** Export filtered logs to a new file or clipboard.
- [ ] **Custom Themes:** User-selectable accent colors and layout configurations.

## 🚀 Getting Started
1. Download the latest release from the [Releases Page](https://github.com/BestArmor/OmniFlow/releases).
2. Extract the archive.
3. Run `OmniFlow.exe`. No .NET runtime installation required.

## 📄 License
This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

<div align="center">
  <br/>
  Made with 🔥 by <a href="https://github.com/BestArmor">BestArmor</a>
</div>
