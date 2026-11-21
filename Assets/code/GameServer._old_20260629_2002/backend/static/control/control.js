const TOKEN_KEY = "lgu_console_token";

const loginGate = document.getElementById("loginGate");
const appShell = document.getElementById("appShell");
const loginCommand = document.getElementById("loginCommand");
const loginBtn = document.getElementById("loginBtn");
const loginError = document.getElementById("loginError");
const logoutBtn = document.getElementById("logoutBtn");
const refreshLogsBtn = document.getElementById("refreshLogsBtn");
const logViewer = document.getElementById("logViewer");

function token() {
  return sessionStorage.getItem(TOKEN_KEY) || "";
}

function authHeaders() {
  return { "X-Console-Token": token() };
}

function showApp() {
  loginGate.classList.add("hidden");
  appShell.classList.remove("hidden");
}

function showLogin(msg) {
  loginGate.classList.remove("hidden");
  appShell.classList.add("hidden");
  loginError.textContent = msg || "";
}

async function loginWithCommand(command) {
  loginError.textContent = "";
  loginBtn.disabled = true;
  try {
    const res = await fetch("/api/hub/console/auth", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ command: command || "" }),
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.detail || "鉴权失败");
    sessionStorage.setItem(TOKEN_KEY, data.token);
    showApp();
    await refreshDashboard();
    await refreshLogs();
    setInterval(refreshDashboard, 10000);
    setInterval(refreshLogs, 15000);
  } catch (e) {
    loginError.textContent = e.message || "无法进入控制台";
  } finally {
    loginBtn.disabled = false;
  }
}

async function refreshDashboard() {
  try {
    const res = await fetch("/api/hub/dashboard", { headers: authHeaders() });
    if (res.status === 401) {
      sessionStorage.removeItem(TOKEN_KEY);
      showLogin("登录已过期，请重新输入命令");
      return;
    }
    const data = await res.json();
    if (!res.ok) throw new Error(data.detail || res.statusText);

    document.getElementById("statOnline").textContent = String(data.onlinePlayers ?? 0);
    document.getElementById("statSessions").textContent = String(data.activeSessions ?? 0);
    document.getElementById("statPortalToday").textContent = String(data.portal?.visitsToday ?? 0);
    document.getElementById("statPortalTotal").textContent = String(data.portal?.visitsTotal ?? 0);
    document.getElementById("statPortalUnique").textContent =
      `独立 IP：${data.portal?.uniqueIps ?? 0} · 下载点击：${data.portal?.downloadClicks ?? 0}`;
    document.getElementById("statDownloads").textContent = String(data.portal?.downloadClicks ?? 0);
    document.getElementById("statTime").textContent = new Date((data.serverTimeUnix || 0) * 1000).toLocaleString();
    document.getElementById("statHealth").textContent = "在线";
    document.getElementById("statHealth").className = "stat-value ok";
  } catch (e) {
    document.getElementById("statHealth").textContent = "异常";
    document.getElementById("statHealth").className = "stat-value";
    document.getElementById("statHealth").style.color = "#f87171";
  }
}

async function refreshLogs() {
  try {
    const res = await fetch("/api/hub/logs?limit=250", { headers: authHeaders() });
    if (res.status === 401) {
      sessionStorage.removeItem(TOKEN_KEY);
      showLogin("登录已过期，请重新输入命令");
      return;
    }
    const data = await res.json();
    if (!res.ok) throw new Error(data.detail || res.statusText);

    const lines = [];
    if (Array.isArray(data.logs)) {
      for (let i = data.logs.length - 1; i >= 0; i -= 1) {
        const item = data.logs[i];
        lines.push(`[${item.timeText}] [${item.level}/${item.source}] ${item.message}`);
      }
    }
    if (!lines.length && Array.isArray(data.fileTail)) {
      lines.push(...data.fileTail);
    }
    logViewer.textContent = lines.length ? lines.join("\n") : "暂无日志";
    logViewer.scrollTop = logViewer.scrollHeight;
  } catch (e) {
    logViewer.textContent = "加载日志失败: " + (e.message || e);
  }
}

document.querySelectorAll(".tab").forEach((btn) => {
  btn.addEventListener("click", () => {
    document.querySelectorAll(".tab").forEach((b) => b.classList.remove("active"));
    document.querySelectorAll(".tab-panel").forEach((p) => p.classList.remove("active"));
    btn.classList.add("active");
    document.getElementById("tab-" + btn.dataset.tab).classList.add("active");
  });
});

loginBtn.addEventListener("click", () => loginWithCommand(loginCommand.value.trim()));
loginCommand.addEventListener("keydown", (ev) => {
  if (ev.key === "Enter") loginWithCommand(loginCommand.value.trim());
});

logoutBtn.addEventListener("click", () => {
  sessionStorage.removeItem(TOKEN_KEY);
  showLogin("");
});

refreshLogsBtn.addEventListener("click", refreshLogs);

(function bootstrap() {
  const params = new URLSearchParams(window.location.search);
  const cmdFromPortal = params.get("cmd");
  const saved = token();
  if (cmdFromPortal) {
    loginWithCommand(cmdFromPortal);
    return;
  }
  if (saved) {
    showApp();
    refreshDashboard();
    refreshLogs();
    setInterval(refreshDashboard, 10000);
    setInterval(refreshLogs, 15000);
    return;
  }
  showLogin("");
})();
