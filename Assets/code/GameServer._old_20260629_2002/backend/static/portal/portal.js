const bgLayer = document.getElementById("bgLayer");
const bgLayerNext = document.getElementById("bgLayerNext");
const serverBadge = document.getElementById("serverBadge");
const versionText = document.getElementById("versionText");
const versionPill = document.getElementById("versionPill");
const sizeText = document.getElementById("sizeText");
const timeText = document.getElementById("timeText");
const fileCountText = document.getElementById("fileCountText");
const notesText = document.getElementById("notesText");
const downloadBtn = document.getElementById("downloadBtn");
const downloadSub = document.getElementById("downloadSub");
const statusMessage = document.getElementById("statusMessage");
const pageOrigin = document.getElementById("pageOrigin");
const consoleCommand = document.getElementById("consoleCommand");
const consoleEnterBtn = document.getElementById("consoleEnterBtn");
const consoleHint = document.getElementById("consoleHint");

const siteNav = document.getElementById("siteNav");
const navLinks = document.getElementById("navLinks");
const navToggle = document.getElementById("navToggle");
const heroVersionText = document.getElementById("heroVersionText");
const heroVersionSub = document.getElementById("heroVersionSub");
const heroDownloadBtn = document.getElementById("heroDownloadBtn");
const bannerDots = document.getElementById("bannerDots");
const newsVersionTitle = document.getElementById("newsVersionTitle");
const newsNotesPreview = document.getElementById("newsNotesPreview");
const newsTime = document.getElementById("newsTime");
const newsServerUrl = document.getElementById("newsServerUrl");
const copyOriginBtn = document.getElementById("copyOriginBtn");
const openConsoleBtn = document.getElementById("openConsoleBtn");
const closeConsoleBtn = document.getElementById("closeConsoleBtn");
const consoleDrawer = document.getElementById("consoleDrawer");
const particleCanvas = document.getElementById("particleCanvas");

let backgrounds = [];
let bgIndex = 0;
let bgFrontIsPrimary = true;
let latestReleaseData = null;

function formatSize(bytes) {
  const n = Number(bytes) || 0;
  if (n < 1024) return n + " B";
  if (n < 1024 * 1024) return (n / 1024).toFixed(1) + " KB";
  if (n < 1024 * 1024 * 1024) return (n / (1024 * 1024)).toFixed(1) + " MB";
  return (n / (1024 * 1024 * 1024)).toFixed(2) + " GB";
}

function formatTime(unix) {
  const ts = Number(unix) || 0;
  if (!ts) return "—";
  return new Date(ts * 1000).toLocaleString();
}

function setBadge(ok, text) {
  if (!serverBadge) return;
  serverBadge.textContent = text;
  serverBadge.className = "badge " + (ok ? "ok" : "err");
}

function updateHeroBanner(data) {
  if (!heroVersionText || !heroVersionSub) return;

  if (!data || !data.hasRelease) {
    heroVersionText.textContent = "暂未发布";
    heroVersionSub.textContent = data?.message || "等待管理员上传客户端";
    return;
  }

  heroVersionText.textContent = "v" + data.version;
  heroVersionSub.textContent = `${formatSize(data.size)} · ${data.fileCount || "—"} 个文件`;
}

function updateNewsPanel(data) {
  if (!newsVersionTitle || !newsNotesPreview || !newsTime) return;

  if (!data || !data.hasRelease) {
    newsVersionTitle.textContent = "暂无版本公告";
    newsNotesPreview.textContent = data?.message || "客户端尚未发布，请关注后续更新。";
    newsTime.textContent = "—";
    return;
  }

  newsVersionTitle.textContent = `v${data.version} 更新说明`;
  const notes = (data.releaseNotes || "").trim();
  newsNotesPreview.textContent = notes || "本次更新暂无文字说明。";
  newsTime.textContent = "发布于 " + formatTime(data.uploadedAtUnix);
}

async function checkHealth() {
  try {
    const res = await fetch("/health", { cache: "no-store" });
    if (!res.ok) throw new Error("HTTP " + res.status);
    setBadge(true, "服务器在线");
  } catch (_) {
    setBadge(false, "服务器离线");
  }
}

async function loadBackgrounds() {
  try {
    const res = await fetch("/api/portal/backgrounds");
    const data = await res.json();
    backgrounds = Array.isArray(data.backgrounds) ? data.backgrounds : [];
  } catch (_) {
    backgrounds = [];
  }

  if (!backgrounds.length) {
    for (let i = 1; i <= 10; i += 1) {
      backgrounds.push(`/backgrounds/bg${String(i).padStart(2, "0")}.png`);
    }
  }

  applyBackground(bgLayer, backgrounds[0]);
  buildBannerDots();
  if (backgrounds.length > 1) {
    setInterval(rotateBackground, 8000);
  }
}

function buildBannerDots() {
  if (!bannerDots || backgrounds.length < 2) return;
  bannerDots.innerHTML = "";
  backgrounds.slice(0, 8).forEach((_, i) => {
    const dot = document.createElement("button");
    dot.type = "button";
    dot.className = "banner-dot" + (i === 0 ? " active" : "");
    dot.setAttribute("aria-label", `背景 ${i + 1}`);
    dot.addEventListener("click", () => jumpToBackground(i));
    bannerDots.appendChild(dot);
  });
}

function updateBannerDots() {
  if (!bannerDots) return;
  const dots = bannerDots.querySelectorAll(".banner-dot");
  dots.forEach((dot, i) => {
    dot.classList.toggle("active", i === bgIndex % dots.length);
  });
}

function jumpToBackground(index) {
  if (!backgrounds.length) return;
  bgIndex = index % backgrounds.length;
  applyBackground(bgLayer, backgrounds[bgIndex]);
  applyBackground(bgLayerNext, backgrounds[bgIndex]);
  bgLayer.style.opacity = "1";
  bgLayerNext.style.opacity = "0";
  bgFrontIsPrimary = true;
  updateBannerDots();
}

function applyBackground(el, url) {
  if (!el || !url) return;
  el.style.backgroundImage = `url("${url}")`;
}

function rotateBackground() {
  if (backgrounds.length < 2) return;
  bgIndex = (bgIndex + 1) % backgrounds.length;
  const nextUrl = backgrounds[bgIndex];
  const front = bgFrontIsPrimary ? bgLayer : bgLayerNext;
  const back = bgFrontIsPrimary ? bgLayerNext : bgLayer;

  applyBackground(back, nextUrl);
  back.style.opacity = "1";
  front.style.opacity = "0";
  bgFrontIsPrimary = !bgFrontIsPrimary;
  updateBannerDots();
}

async function loadLatestRelease() {
  versionPill.textContent = "检测中";
  versionPill.className = "version-pill waiting";

  try {
    const res = await fetch("/api/client/public/latest", { cache: "no-store" });
    const data = await res.json();
    if (!res.ok) throw new Error(data.detail || res.statusText);

    latestReleaseData = data;
    updateHeroBanner(data);
    updateNewsPanel(data);

    if (!data.hasRelease) {
      versionText.textContent = "暂未发布";
      versionPill.textContent = "等待发布";
      versionPill.className = "version-pill empty";
      sizeText.textContent = "—";
      timeText.textContent = "—";
      fileCountText.textContent = "—";
      notesText.textContent = data.message || "请联系管理员在后台上传客户端。";
      downloadBtn.classList.add("disabled");
      downloadSub.textContent = "暂不可下载";
      statusMessage.textContent = "";
      return;
    }

    versionText.textContent = "v" + data.version;
    versionPill.textContent = "可下载";
    versionPill.className = "version-pill ready";
    sizeText.textContent = formatSize(data.size);
    timeText.textContent = formatTime(data.uploadedAtUnix);
    fileCountText.textContent = data.fileCount ? `${data.fileCount} 个` : "—";
    notesText.textContent = (data.releaseNotes || "").trim() || "本次更新暂无文字说明。";

    downloadBtn.href = data.downloadUrl;
    downloadBtn.setAttribute("download", data.downloadName || `client_${data.version}.zip`);
    downloadBtn.classList.remove("disabled");
    downloadSub.textContent = data.downloadName || `client_${data.version}.zip`;
    statusMessage.textContent = "下载完成后解压 zip，运行其中的 .exe 即可。";
  } catch (e) {
    latestReleaseData = null;
    updateHeroBanner(null);
    updateNewsPanel(null);
    versionText.textContent = "加载失败";
    versionPill.textContent = "错误";
    versionPill.className = "version-pill empty";
    notesText.textContent = "无法获取版本信息，请确认下载服务已启动。";
    downloadBtn.classList.add("disabled");
    statusMessage.textContent = e.message || "请求失败";
  }
}

downloadBtn.addEventListener("click", () => {
  if (downloadBtn.classList.contains("disabled")) return;
  statusMessage.textContent = "已开始下载，请查看浏览器下载栏…";
  fetch("/api/hub/portal/download-click", { method: "POST" }).catch(() => {});
});

async function enterConsoleFromPortal() {
  const cmd = (consoleCommand?.value || "").trim();
  if (!cmd) {
    if (consoleHint) consoleHint.textContent = "请先输入控制台命令";
    return;
  }
  try {
    const res = await fetch("/api/hub/console/auth", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ command: cmd }),
    });
    const data = await res.json().catch(() => ({}));
    if (!res.ok) throw new Error(data.detail || "命令无效");
    sessionStorage.setItem("lgu_console_token", data.token);
    window.location.href = "/control/";
  } catch (e) {
    if (consoleHint) consoleHint.textContent = "进入失败: " + (e.message || e);
  }
}

if (consoleEnterBtn) {
  consoleEnterBtn.addEventListener("click", enterConsoleFromPortal);
}
if (consoleCommand) {
  consoleCommand.addEventListener("keydown", (ev) => {
    if (ev.key === "Enter") enterConsoleFromPortal();
  });
}

function initNav() {
  if (siteNav) {
    window.addEventListener("scroll", () => {
      siteNav.classList.toggle("scrolled", window.scrollY > 24);
    }, { passive: true });
  }

  if (navToggle && navLinks) {
    navToggle.addEventListener("click", () => {
      navToggle.classList.toggle("open");
      navLinks.classList.toggle("open");
    });

    navLinks.querySelectorAll(".nav-link").forEach((link) => {
      link.addEventListener("click", () => {
        navToggle.classList.remove("open");
        navLinks.classList.remove("open");
      });
    });
  }

  const sections = document.querySelectorAll(".section-anchor");
  const navItems = document.querySelectorAll(".nav-link[data-section]");

  if (sections.length && navItems.length) {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (!entry.isIntersecting) return;
          const id = entry.target.id;
          navItems.forEach((item) => {
            item.classList.toggle("active", item.dataset.section === id);
          });
        });
      },
      { rootMargin: "-40% 0px -45% 0px", threshold: 0 }
    );
    sections.forEach((section) => observer.observe(section));
  }
}

function initReveal() {
  const prefersReduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  if (prefersReduced) return;

  const targets = document.querySelectorAll(
    ".world-card, .feature-panel, .news-card, .news-item, .download-card, .install-steps, .spec-card, .section-head"
  );
  targets.forEach((el) => el.classList.add("reveal"));

  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add("visible");
          observer.unobserve(entry.target);
        }
      });
    },
    { threshold: 0.12 }
  );
  targets.forEach((el) => observer.observe(el));
}

function initConsoleDrawer() {
  const open = () => {
    if (!consoleDrawer) return;
    consoleDrawer.hidden = false;
    consoleCommand?.focus();
  };
  const close = () => {
    if (!consoleDrawer) return;
    consoleDrawer.hidden = true;
    if (consoleHint) consoleHint.textContent = "";
  };

  openConsoleBtn?.addEventListener("click", open);
  closeConsoleBtn?.addEventListener("click", close);
  consoleDrawer?.addEventListener("click", (ev) => {
    if (ev.target === consoleDrawer) close();
  });
  document.addEventListener("keydown", (ev) => {
    if (ev.key === "Escape" && consoleDrawer && !consoleDrawer.hidden) close();
  });
}

function initCopyOrigin() {
  copyOriginBtn?.addEventListener("click", async () => {
    const url = window.location.origin;
    try {
      await navigator.clipboard.writeText(url);
      copyOriginBtn.textContent = "已复制";
      setTimeout(() => { copyOriginBtn.textContent = "复制下载页地址"; }, 2000);
    } catch (_) {
      copyOriginBtn.textContent = url;
    }
  });
}

function initParticles() {
  if (!particleCanvas) return;
  const prefersReduced = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  if (prefersReduced) return;

  const ctx = particleCanvas.getContext("2d");
  let particles = [];
  let w = 0;
  let h = 0;
  let raf = 0;

  function resize() {
    w = particleCanvas.width = window.innerWidth;
    h = particleCanvas.height = window.innerHeight;
  }

  function spawn(count) {
    for (let i = 0; i < count; i += 1) {
      particles.push({
        x: Math.random() * w,
        y: Math.random() * h,
        r: Math.random() * 1.6 + 0.4,
        vx: (Math.random() - 0.5) * 0.18,
        vy: -Math.random() * 0.35 - 0.08,
        a: Math.random() * 0.45 + 0.15,
      });
    }
  }

  function tick() {
    ctx.clearRect(0, 0, w, h);
    particles.forEach((p) => {
      p.x += p.vx;
      p.y += p.vy;
      if (p.y < -8) {
        p.y = h + 8;
        p.x = Math.random() * w;
      }
      ctx.beginPath();
      ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
      ctx.fillStyle = `rgba(240, 198, 116, ${p.a})`;
      ctx.fill();
    });
    raf = requestAnimationFrame(tick);
  }

  resize();
  spawn(Math.min(80, Math.floor((w * h) / 18000)));
  tick();
  window.addEventListener("resize", resize);
}

if (pageOrigin) pageOrigin.textContent = window.location.origin;
if (newsServerUrl) newsServerUrl.textContent = window.location.origin;

initNav();
initReveal();
initConsoleDrawer();
initCopyOrigin();
initParticles();

checkHealth();
loadBackgrounds();
loadLatestRelease();
setInterval(checkHealth, 15000);
setInterval(loadLatestRelease, 30000);
