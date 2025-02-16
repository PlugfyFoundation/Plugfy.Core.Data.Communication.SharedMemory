 
![logo_plugfy_core_foundation_256x55](https://raw.githubusercontent.com/PlugfyFoundation/Plugfy.Solution/refs/heads/main/plugfy-core-fundation-header.png)

# Shared Memory Communication Extension

## Overview
The **Shared Memory Communication Extension** is a part of the larger framework designed to facilitate inter-process communication via shared memory using memory mapped files. This extension provides a robust and efficient method for data exchange between different processes, leveraging the high-speed access of shared memory along with synchronization mechanisms to ensure data integrity and consistency.

---

## Features
- **High-Performance Inter-Process Communication**: Utilizes shared memory for rapid data transfer between processes.
- **Synchronization and Signaling**: Implements mutexes and event wait handles to manage access control and notify processes of available data.
- **Flexible Data Handling**: Supports dynamic JSON serialized data structures for communication.
- **Easy Integration**: Seamlessly integrates with existing systems that support .NET, offering a straightforward setup process.
- **Robust Error Handling**: Includes comprehensive error management to ensure stable operation even in edge cases.

---

## Installation
To integrate the Shared Memory Communication Extension into your project, follow these steps:
1. Clone the repository to your local machine.
2. Include the project in your solution or reference the built assembly directly.
3. Ensure your project targets .NET Framework 8 or higher.

---

## Usage
To use the Shared Memory Communication Extension, initialize an instance of the `SharedMemory` class and configure it with a unique memory map name and optional buffer size. Start the communication by calling `InitializeAsync` and `StartListeningAsync` to begin data transmission.

Example:
```csharp
var sharedMemory = new SharedMemory();
await sharedMemory.InitializeAsync(new { MapName = "ExampleMap", MapSize = 4096 });
await sharedMemory.StartListeningAsync();
```

---

## License
This project is licensed under the **GNU General Public License v3.0**. See [GNU GPL v3.0](https://www.gnu.org/licenses/gpl-3.0.en.html) for details.

---

## Contributing
We welcome contributions! To contribute:
1. Fork the repository.
2. Create a new feature branch (`git checkout -b feature-new`).
3. Commit changes (`git commit -m "Added new feature"`).
4. Push to the branch (`git push origin feature-new`).
5. Submit a Pull Request.

For major changes, open an issue first to discuss the proposed changes.

---

## Contact
For inquiries, feature requests, or issues, please open a GitHub issue or contact the **Plugfy Foundation**.

