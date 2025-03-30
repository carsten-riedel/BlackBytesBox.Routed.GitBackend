# BlackBytesBox.Routed.GitBackend

This project provides ASP.NET Core middleware to expose bare Git repositories over HTTP using Git's `git-http-backend` mechanism.

## ⚠️ Experimental
This is an experimental setup and **not recommended for production** environments without proper hardening and security review.

---

## 🧰 Prerequisites

- Git for Windows must be installed. You can download it here:  
  👉 https://git-scm.com/downloads/win

Ensure that `git-http-backend.exe` is present (typically found under `mingw64/libexec/git-core/`).

---

## 🚀 Usage

To enable Git HTTP support in your ASP.NET Core app:

```csharp
app.UseGitBackend(
    @"C:\gitremote",
    @"C:\Program Files\Git\mingw64\libexec\git-core\git-http-backend.exe",
    "/gitrepos",
    (repoName, username, password) =>
    {
        // Custom credential validation logic
        return string.Equals(username, "gituser", StringComparison.OrdinalIgnoreCase)
            && string.Equals(password, "secret", StringComparison.Ordinal)
            && repoName.Equals("MyProject.git", StringComparison.OrdinalIgnoreCase);
    });
```

---

## 🗂️ Repository Initialization

Repositories must be manually initialized as bare repositories:

```bash
git -C C:\gitremote init --bare MyProject.git
git -C C:\gitremote\MyProject.git config http.receivepack true
```

---

## 📦 Middleware Overview

The `GitBackendMiddleware` class:
- Invokes `git-http-backend.exe` with CGI-style environment setup.
- Parses requests under a specific base path (e.g., `/gitrepos`).
- Performs Basic Authentication via a user-supplied delegate.
- Streams Git protocol requests and responses.

### Requirements:
- Middleware must be explicitly added in the pipeline.
- Authentication logic must be customized per your use case.

---

## 📚 Related
- Git Smart HTTP Protocol: https://git-scm.com/book/en/v2/Git-on-the-Server-Smart-HTTP
- ASP.NET Core Middleware Docs: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/

---

## 📌 Notes

- Only `.git`-suffixed repo paths will be processed.
- This middleware uses `git-http-backend`, not a custom Git implementation.
- Repository path traversal is blocked and validated.
- This component assumes you understand Git internals and ASP.NET Core middleware.

---

## ✅ Example Clone URL

```bash
git clone http://localhost:5000/gitrepos/MyProject.git
```

---

Enjoy hacking Git over HTTP!

---

MIT License. Provided as-is with no warranty.

