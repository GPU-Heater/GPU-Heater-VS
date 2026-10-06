# GPU-Heater Coder

GPU-Heater Code Assistant & Architect for VSCode.

## 🚀 Features

*   **FIM Auto-Complete**: Real-time ghost text code completion (Fill-In-the-Middle) as you type.
*   **AI Editor Actions**: Highlight code, right-click, and choose from the **GPU-Heater** menu to:
    *   **Fix Error / Debug**: Provide error logs and get automated fixes.
    *   **Refactor Code**: Optimize and improve selected code snippets.
    *   **Explain Code**: Get detailed explanations for complex logic directly in the chat window.
    *   **Generate Tests**: Automatically create unit tests for your functions.
*   **Train with Selection**: Send specific code snippets to your local Vector DB to improve future AI suggestions and context.
*   **Inspect Workspace / Folder**: Right-click any folder in the Project View to run an AI analysis on the directory contents.
*   **Dedicated Tool Window**: A built-in chat interface for interactive coding assistance and context sharing.

## ⚙️ Configuration

To connect the plugin to your backend:
1. Open your Settings/Extensions.
2. Navigate to **Tools > GPU-Heater AI**.
3. Configure your **Backend API Base URL** (Default is `http://127.0.0.1:2004`).
4. (Optional) Toggle Auto-Complete and set your preferred language via the tool window interface.

## 📋 Requirements

*   Visual Studio 2022 or higher.
*   An active **GPU-Heater backend server** running at the configured API URL to process LLM requests.

## 📥 Installation

* Download the latest **`GPU-Heater-VS.vsix`** package from the **Releases** page.
* Double-click the downloaded **`.vsix`** file to launch the **VSIX Installer** (make sure Visual Studio is closed during installation).

* Alternatively, inside Visual Studio:
* Go to **Extensions** > **Manage Extensions**.
* Look for **GPU-Heater Coder** in the **Installed** or available extensions list.
* Click **Install** / **Modify** and wait for the installation to finish.
* Restart Visual Studio to enable the extension.

## 🛠️ Usage

*   **Auto-Complete**: Simply start typing. If enabled, ghost text will appear. Press the designated key (usually Tab) to accept.
*   **Context Menu**: Right-click in the editor or project explorer to access GPU-Heater actions.
*   **Chat & Explanation**: Open the `GPU-Heater` tool window anchored on the right side of your IDE to interact with the AI directly.
