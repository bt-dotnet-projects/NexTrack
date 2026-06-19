\# 🛰️ NexTrack



\*\*NexTrack\*\* is a Windows background activity monitoring service designed to track system activities, user sessions, application usage, and synchronize collected data securely with the server.



\---



\## ✨ Features



\- 🧩 Runs silently as a Windows Service

\- 🚀 Automatically starts with Windows

\- 👤 Tracks user activities and system events

\- 🖥️ Monitors active applications and idle time

\- 💾 Stores activity data locally

\- ☁️ Synchronizes data with remote API

\- ⚙️ Background execution with minimal system resources

\- 📝 Logging and error tracking



\---



\## 💻 System Requirements



\- 🪟 Windows 10 / Windows 11

\- 🔑 Administrator privileges for installation

\- 🧱 .NET Runtime (if framework-dependent build)



\---



\## 🏗️ Build and Publish the Service



Open a terminal in the project directory and publish the application:



```bash

dotnet publish -c Release -r win-x64 --self-contained true

```



The published files will be generated in:



```

bin\\Release\\net10.0\\win-x64\\publish\\

```



📂 Copy the complete `publish` folder to the target machine.



\---



\## 📥 Installing the Windows Service



Open \*\*Command Prompt as Administrator\*\* and run:



```cmd

sc create NexTrackService binPath="C:\\NexTrack\\NexTrack.exe" start=auto

```



Verify the service is created:



```cmd

sc query NexTrackService

```



\---



\## ▶️ Starting the Service



Start the NexTrack service using:



```cmd

sc start NexTrackService

```



Check the current status:



```cmd

sc query NexTrackService

```



Expected output:



```

STATE              : 4 RUNNING

```



\---



\## ⏹️ Stopping the Service



Stop the service:



```cmd

sc stop NexTrackService

```



Check the status:



```cmd

sc query NexTrackService

```



Expected output:



```

STATE              : 1 STOPPED

```



\---



\## 🔄 Restarting the Service



Restart the service using:



```cmd

sc stop NexTrackService

sc start NexTrackService

```



\---



\## ⚡ Configuring Startup Mode



Set the service to start automatically with Windows:



```cmd

sc config NexTrackService start= auto

```



Set manual startup:



```cmd

sc config NexTrackService start= demand

```



Disable the service:



```cmd

sc config NexTrackService start= disabled

```



\---



\## 🗑️ Removing the Service



First stop the service:



```cmd

sc stop NexTrackService

```



Delete the service:



```cmd

sc delete NexTrackService

```



Verify deletion:



```cmd

sc query NexTrackService

```



\---



\## 📜 Logs



NexTrack generates logs for:



\- 🟢 Service startup and shutdown

\- 👤 User activity tracking

\- ☁️ Synchronization operations

\- ⚠️ Errors and exceptions



Check log files inside:



```

C:\\NexTrack\\Logs\\

```



\---



\## 🛠️ Troubleshooting



\### ❌ Service does not start



\- ✅ Verify the executable path is correct.

\- ✅ Ensure the service is installed with administrator privileges.

\- ✅ Check Windows Event Viewer for service errors.

\- ✅ Confirm all required configuration files are available.



\### 🔒 Access Denied Errors



Run Command Prompt or PowerShell as \*\*Administrator\*\*.



\---



\## 📋 Service Commands Quick Reference



| Action           | Command                                                                   |

| ---------------- | -------------------------------------------------------------------------- |

| ➕ Create Service  | `sc create NexTrackService binPath="C:\\NexTrack\\NexTrack.exe" start=auto` |

| ▶️ Start Service   | `sc start NexTrackService`                                                |

| ⏹️ Stop Service    | `sc stop NexTrackService`                                                 |

| 🔍 Check Status    | `sc query NexTrackService`                                                |

| 🔄 Restart Service | `sc stop NexTrackService` then `sc start NexTrackService`                 |

| 🗑️ Delete Service  | `sc delete NexTrackService`                                               |



\---



\## 🏷️ Version



\*\*NexTrack v1.0\*\*



Developed as a secure and lightweight Windows Activity Monitoring Service. 🔐

