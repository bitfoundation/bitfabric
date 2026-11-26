# Stage 23: In-App Dev Tools (bit BevTools)

The app ships [bit BevTools](https://github.com/bitfoundation/bitplatform/tree/develop/src/Utilities/BevTools), developer tools for Blazor that run **inside the app**, in **all environments** (Development, Staging and Production) and on **all platforms** (Web, Android, iOS, Windows, macOS). It works on phones too, where the browser's own developer tools are out of reach, which is what makes it the first stop of a support session.

## 🎯 Hands-On Exploration (STRONGLY RECOMMENDED)

1. Visit **https://bitplatform.dev/demos**
2. Open any published demo app (Admin Panel, Sales Dashboard, etc.)
3. Tap **7 times** on the empty space of the header, or press **Ctrl+Shift+D**
4. Look around: the **App** tab, then **Logs**, **Errors**, **Network**, **Components** and **Runtime**

---

## Opening it

| How | Opens on |
| --- | --- |
| Tap 7 times on the empty space of the header ([`DiagnosticSpacer`](/src/Client/Boilerplate.Client.Core/Components/Layout/Header/DiagnosticSpacer.razor.cs)) | the **App** tab |
| **Ctrl+Shift+D** | opens and closes BevTools |
| `App.showDevTools()` in the browser's console | the **App** tab |
| `App.showDevTools('logs level:error')` | any panel, with a filter for it |
| The **Diagnostic** button of the error page ([`AppErrorBoundary`](/src/Client/Boilerplate.Client.Core/Components/AppErrorBoundary.razor.cs)) | **Logs**, filtered to the errors |

All of them publish `ClientAppMessages.SHOW_DEV_TOOLS`, which [`AppDevTools`](/src/Client/Boilerplate.Client.Core/Components/Layout/Diagnostic/AppDevTools.razor.cs) answers. It sits in `MainLayout` and renders `<BevToolsLoaderPanel />`, where BevTools draws itself. **Ctrl+Shift+D** is BevTools' own shortcut; `MainLayout` listens for it only until BevTools is attached, since nothing of BevTools listens before that.

## Where it runs

- **Development and Blazor Hybrid**: BevTools is registered at startup, so it sees everything from the first render.
- **The browser outside Development**: `Bit.BevTools.Loader` lazy loads `Bit.BevTools.wasm` the first time BevTools is opened, then attaches it to the running app (`BlazorWebAssemblyLazyLoad` in `Boilerplate.Client.Web.csproj`). Nobody downloads it until they ask for it. It sees what happens after that, except the logs: `AutoStart = BevToolsAutoStart.Logs` keeps what the app logged from its start, and the **Logs** panel shows that too. AOT builds don't lazy load it, but attach it on demand all the same.
- **Blazor Server outside Development**: BevTools isn't opened there, because its panels would show a visitor the server side objects of their circuit. Asking for it says so in a snack bar; the `/diagnostic` page still shows the report the **App** tab shows.

The registration is in [`IClientCoreServiceCollectionExtensions`](/src/Client/Boilerplate.Client.Core/Infrastructure/Extensions/IClientCoreServiceCollectionExtensions.cs) (`AddBitBevToolsLoaderServices`).

## What BevTools shows

- **Components**: the live component tree, each component's parameters, state, services and the handlers it bound, and why it rendered
- **Profiler** and **Timeline**: render counts and durations, and everything that happened in order
- **Logs**: what the app logged, filtered by text, `/regex/`, `level:warning`, `category:` or `trace:`, each entry with its details and copy
- **Errors**, **Network**, **JS Interop**, **Routing**, **State**, **Forms** and **Services**
- **Runtime**: memory, garbage collections, and **Collect garbage and check** where the app runs on the device
- **Browser**: storage, cookies, caches, the service worker and an accessibility check
- **Diagnostics**: findings with evidence, such as a component that renders far more often than it needs to

Everything it reads is redacted where it is captured: passwords, tokens, keys, cookies and connection strings never reach a panel, an export or a trace. The [BevTools guide](https://github.com/bitfoundation/bitplatform/tree/develop/src/Utilities/BevTools) has the rest, its companion window and its browser extension included.

## The App tab

The app's own tab, [`AppDevToolsPanel`](/src/Client/Boilerplate.Client.Core/Components/Layout/Diagnostic/AppDevToolsPanel.razor), added by [`AppBevTools.Configure`](/src/Client/Boilerplate.Client.Core/Components/Layout/Diagnostic/AppBevTools.cs).

### Diagnostic report

The same report as the `/diagnostic` page:
- **Telemetry context**: the user, the session, the device, the platform, the app's version, the culture and the server's address
- **Show more** asks [`DiagnosticController.PerformDiagnostic`](/src/Server/Boilerplate.Server.Api/Features/Diagnostic/DiagnosticController.cs), which builds its report with [`ServerDiagnosticService`](/src/Server/Boilerplate.Server.Api/Features/Diagnostic/ServerDiagnosticService.cs):
  - Which path the report came in on (`Via:`), the client IP address and port, whether the call arrived through the CDN, the protocol and endpoint, and the HTTP trace identifier
  - Authentication status, the caller's `UserSessionId` when signed in, and (when `multitenant` is on) the current `TenantId`
  - Current culture and UI culture
  - All HTTP request headers - as they arrived, except `Authorization`, reduced to its scheme (`Bearer (redacted)`), and `Cookie`, reduced to its cookie names
  - Server environment name, base URLs and the server's UTC clock
  - A test push notification (if subscribed) and a test SignalR message (if connected)
- The client's own runtime facts follow (AOT, GC configuration and the like), produced by the component, not by the API
- **Copy** puts all of it on the clipboard, for a support ticket

#### 🧭 The same report over three paths

`ServerDiagnosticService` is the single body behind three doors, so the same question can be asked over each and the answers compared - behind a proxy they are separate paths to the origin, and a forwarding rule can be right for one and wrong for another:

| Path | Caller | Access |
| --- | --- | --- |
| `DiagnosticController.PerformDiagnostic` | the `/diagnostic` page and the App tab | anonymous |
| `AppHub.GetDiagnosticReport` | the same page, over the websocket it already holds | anonymous (`signalR`) |
| `GetDiagnosticReport` on `/dev-mcp` | a developer's AI agent | global admin + 2FA |

The two are meant to read almost identically, so the page shows one at a time: the **Ask over SignalR** button re-asks over the socket and replaces what is on screen, and the `Via:` line at the top of each says which path answered. [`ClientIpForwardingTests`](/src/Internal/Boilerplate.Tests.E2E/Features/Diagnostics/ClientIpForwardingTests.cs) checks the resolved client IP on every path against the machine's real public address.

> **Credentials are not in the output.** `Authorization` is reported as its scheme alone and `Cookie` as its cookie names, so a wrong or missing credential is still diagnosable while no token or session value reaches an issue tracker or an AI agent's transcript. Every other header is reported as it arrived.

### ⚠️ Throw test error
- Throws an exception nothing handles, to prove the error page, the logs and the error tracker (Sentry, Application Insights) all get it
- Alternates between `InvalidOperationException` (unknown exception) and `DomainLogicException` (known exception), both with test data

### 🧹 Clear app files
- Signs out, clears the app's storage, local and session storage, cache storages, cookies and the AI chat's conversation kept on the device
- With the offline database (`offlineDb`), first pushes the changes it still holds, then deletes it
- **Web**: uninstalls the service worker and reloads, using `BitBswup.forceRefresh()`
- **Hybrid apps**: reloads the app
- The chatbot's tool asks a user's device for the same over SignalR (`CLEAR_APP_FILES`)

### 🔄 Update app
- Checks for an update and installs it now, through `IAppUpdateService.ForceUpdate()`

### 🗓️ Hangfire dashboard
- For a user who holds `AppFeatures.System.Jobs_Manage`, the feature the dashboard asks for
- Only where the browser itself runs the app and the app's server serves it, Blazor WebAssembly or Auto: the dashboard signs the user in by the `access_token` cookie, which has to land on the app's own origin. Blazor Server, a hybrid app and a statically hosted web app don't show it
- Refreshes the token, has `IUserController.UpdateSession` write the cookie, then opens `/hangfire` in a new tab

---

## Adding a tab of your own

Add it in `AppBevTools.Configure`, which serves both ways BevTools runs:

```csharp
options.AddPanel<MyFeaturePanel>("my-feature", "My feature");
```

A tab is an ordinary component that gets no parameters: it injects the app's services as any component does. Keep it a plain component of the app, as `AppDevToolsPanel` is: the router lists the public types of `Boilerplate.Client.Core` before a lazily loaded `Bit.BevTools.wasm` is there, so a public type that derives from a Bit.BevTools type (`BevToolsTab`, `IBevToolsExtension`) stops the app from starting. Such a type has to be an `internal` C# class. `App.showDevTools('my-feature')` opens it.

## Logs

The `BevTools` section of `Logging` in `appsettings.json` decides what the **Logs** panel gets, `Information` and above by default. The `DiagnosticLogger` provider keeps the recent entries in memory too, for the chatbot's `CheckLastError` tool, which asks a user's device for its last error.

---

### AI Wiki

Ask your own question [here](https://bitplatform.dev/ask)

---
