const uploadForm = document.getElementById("uploadForm");
const releaseTableBody = document.getElementById("releaseTableBody");
const releaseList = document.getElementById("releaseList");
const refreshBtn = document.getElementById("refreshBtn");
const serverStatus = document.getElementById("serverStatus");
const uploadProgress = document.getElementById("uploadProgress");
const uploadStatus = document.getElementById("uploadStatus");
const uploadBtn = document.getElementById("uploadBtn");
const tokenRow = document.getElementById("tokenRow");
const tokenInput = document.getElementById("tokenInput");
const dropzone = document.getElementById("dropzone");
const fileInput = document.getElementById("fileInput");
const dropzoneFileName = document.getElementById("dropzoneFileName");
const progressWrap = document.getElementById("progressWrap");
const progressFill = document.getElementById("progressFill");
const progressPct = document.getElementById("progressPct");

const liveVersion = document.getElementById("liveVersion");
const liveNotes = document.getElementById("liveNotes");
const liveSize = document.getElementById("liveSize");
const liveFiles = document.getElementById("liveFiles");
const liveTime = document.getElementById("liveTime");
const liveDownloadBtn = document.getElementById("liveDownloadBtn");

let adminTokenRequired = false;

const isEmbed = new URLSearchParams(window.location.search).get("embed") === "1";
if (isEmbed) {
  document.body.classList.add("embed");
  const embedTabs = document.getElementById("embedTabs");
  if (embedTabs) embedTabs.hidden = false;
}

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

function formatIncrementalHint(info) {
  if (!info || !info.fromVersion) return "无增量数据";
  const changed = Number(info.changedCount) || 0;
  const patchSize = formatSize(info.patchSize);
  return `相对 ${info.fromVersion} · ${changed} 文件 · ${patchSize}`;
}

function adminHeaders() {
  const headers = {};
  const token = tokenInput.value.trim();
  if (token) headers["X-Admin-Token"] = token;
  return headers;
}

function escapeHtml(text) {
  return String(text ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}

function setProgress(pct) {
  const value = Math.round(Math.max(0, Math.min(1, pct)) * 100);
  uploadProgress.value = value;
  if (progressFill) progressFill.style.width = value + "%";
  if (progressPct) progressPct.textContent = value + "%";
}

function switchView(viewId) {
  document.querySelectorAll(".side-link, .embed-tab").forEach((btn) => {
    btn.classList.toggle("active", btn.dataset.view === viewId);
  });
  document.querySelectorAll(".view-panel").forEach((panel) => {
    panel.classList.toggle("active", panel.id === "view-" + viewId);
  });
}

document.querySelectorAll(".side-link, .embed-tab").forEach((btn) => {
  btn.addEventListener("click", () => switchView(btn.dataset.view));
});

function updateDropzoneLabel(file) {
  if (!dropzoneFileName) return;
  dropzoneFileName.textContent = file ? `已选择：${file.name} (${formatSize(file.size)})` : "";
}

if (dropzone && fileInput) {
  fileInput.addEventListener("change", () => {
    updateDropzoneLabel(fileInput.files[0] || null);
  });

  dropzone.addEventListener("dragover", (ev) => {
    ev.preventDefault();
    dropzone.classList.add("dragover");
  });

  dropzone.addEventListener("dragleave", () => {
    dropzone.classList.remove("dragover");
  });

  dropzone.addEventListener("drop", (ev) => {
    ev.preventDefault();
    dropzone.classList.remove("dragover");
    const file = ev.dataTransfer?.files?.[0];
    if (!file) return;
    if (!file.name.toLowerCase().endsWith(".zip")) {
      uploadStatus.textContent = "请选择 .zip 文件";
      return;
    }
    const dt = new DataTransfer();
    dt.items.add(file);
    fileInput.files = dt.files;
    updateDropzoneLabel(file);
  });
}

async function checkHealth() {
  try {
    const res = await fetch("/health");
    if (!res.ok) throw new Error("HTTP " + res.status);
    serverStatus.textContent = "服务在线";
    serverStatus.className = "status-pill ok";
  } catch (_) {
    serverStatus.textContent = "服务离线";
    serverStatus.className = "status-pill err";
  }
}

async function loadLiveBuild() {
  if (!liveVersion) return;
  try {
    const res = await fetch("/api/client/public/latest");
    const data = await res.json();
    if (!res.ok || !data.hasRelease || !data.version) {
      liveVersion.textContent = "暂无";
      liveNotes.textContent = data.message || "尚未发布任何客户端版本";
      liveSize.textContent = "—";
      liveFiles.textContent = "—";
      liveTime.textContent = "—";
      if (liveDownloadBtn) liveDownloadBtn.hidden = true;
      return;
    }

    liveVersion.textContent = "v" + data.version;
    liveNotes.textContent = data.releaseNotes || "无更新说明";
    liveSize.textContent = formatSize(data.size);
    liveFiles.textContent = String(data.fileCount || "—");
    liveTime.textContent = formatTime(data.uploadedAtUnix);

    if (liveDownloadBtn && data.downloadUrl) {
      liveDownloadBtn.href = data.downloadUrl;
      liveDownloadBtn.hidden = false;
    }
  } catch (_) {
    liveNotes.textContent = "无法读取当前版本信息";
  }
}

function renderReleaseCards(releases) {
  if (!releaseList) return;

  if (!releases.length) {
    releaseList.innerHTML = '<div class="release-empty">暂无发布版本，请先上传客户端 zip。</div>';
    return;
  }

  releaseList.innerHTML = releases.map((r) => {
    const fileCount = Number(r.fileCount) || 0;
    const inc = r.incrementalFromPrev;
    const needsReindex = fileCount === 0;
    const isLive = !!r.isActive;

    return `
      <article class="release-card${isLive ? " is-live" : ""}">
        <div class="release-version-col">
          <p class="release-version">v${escapeHtml(r.version)}</p>
          <span class="release-tag ${isLive ? "live" : "history"}">${isLive ? "LIVE" : "ARCHIVE"}</span>
        </div>
        <div class="release-info">
          <p class="release-notes">${escapeHtml(r.releaseNotes || "无更新说明")}</p>
          <div class="release-meta">
            <span>完整包 ${formatSize(r.size)}${fileCount ? ` · ${fileCount} 文件` : ""}</span>
            <span>${formatIncrementalHint(inc)}</span>
            <span>${formatTime(r.uploadedAtUnix)}</span>
          </div>
        </div>
        <div class="release-actions">
          <a class="btn ghost small" href="${r.downloadUrl}" target="_blank" rel="noopener">完整包</a>
          ${needsReindex ? `<button class="btn ghost small" data-action="reindex" data-version="${escapeHtml(r.version)}">建立索引</button>` : ""}
          ${isLive ? "" : `<button class="btn secondary small" data-action="activate" data-version="${escapeHtml(r.version)}">设为当前</button>`}
          <button class="btn danger small" data-action="delete" data-version="${escapeHtml(r.version)}">删除</button>
        </div>
      </article>`;
  }).join("");
}

async function loadReleases() {
  if (releaseList) {
    releaseList.innerHTML = '<div class="release-empty">加载中…</div>';
  }

  try {
    const res = await fetch("/api/admin/client/releases");
    const data = await res.json();
    if (!res.ok) throw new Error(data.detail || res.statusText);

    adminTokenRequired = !!data.adminTokenRequired;
    tokenRow.hidden = !adminTokenRequired;

    const releases = data.releases || [];
    renderReleaseCards(releases);
    await loadLiveBuild();

    if (releaseTableBody) {
      releaseTableBody.innerHTML = releases.map((r) => `<tr><td>${escapeHtml(r.version)}</td></tr>`).join("");
    }
  } catch (e) {
    if (releaseList) {
      releaseList.innerHTML = `<div class="release-empty">加载失败: ${escapeHtml(e.message)}</div>`;
    }
  }
}

function bindReleaseActions(container) {
  if (!container) return;
  container.addEventListener("click", async (ev) => {
    const btn = ev.target.closest("button[data-action]");
    if (!btn) return;
    const version = btn.getAttribute("data-version");
    const action = btn.getAttribute("data-action");
    if (!version || !action) return;

    if (action === "delete" && !confirm(`确定删除版本 ${version}？`)) return;

    btn.disabled = true;
    try {
      let method = "POST";
      let url = `/api/admin/client/set-active/${encodeURIComponent(version)}`;
      if (action === "delete") {
        method = "DELETE";
        url = `/api/admin/client/release/${encodeURIComponent(version)}`;
      } else if (action === "reindex") {
        method = "POST";
        url = `/api/admin/client/reindex/${encodeURIComponent(version)}`;
      }
      const res = await fetch(url, { method, headers: adminHeaders() });
      const data = await res.json().catch(() => ({}));
      if (!res.ok) throw new Error(data.detail || res.statusText);
      await loadReleases();
    } catch (e) {
      alert(e.message);
    } finally {
      btn.disabled = false;
    }
  });
}

bindReleaseActions(releaseList);
if (releaseTableBody) bindReleaseActions(releaseTableBody);

uploadForm.addEventListener("submit", async (ev) => {
  ev.preventDefault();
  const file = fileInput.files[0];
  if (!file) {
    uploadStatus.textContent = "请选择 zip 文件";
    return;
  }

  const formData = new FormData();
  formData.append("version", document.getElementById("versionInput").value.trim());
  formData.append("release_notes", document.getElementById("notesInput").value.trim());
  formData.append("force_update", document.getElementById("forceUpdateInput").checked ? "true" : "false");
  formData.append("set_active", document.getElementById("setActiveInput").checked ? "true" : "false");
  formData.append("file", file);

  uploadBtn.disabled = true;
  uploadProgress.hidden = true;
  if (progressWrap) progressWrap.hidden = false;
  setProgress(0);
  uploadStatus.textContent = "上传并建立文件索引中…";

  try {
    const result = await uploadWithProgress("/api/admin/client/upload", formData, (pct) => {
      setProgress(pct);
    });
    let msg = `发布成功 · ${result.fileCount || 0} 个文件已索引`;
    if (result.incrementalFrom) {
      msg += ` · 相对 ${result.incrementalFrom} 增量 ${result.incrementalChangedCount || 0} 文件 / ${formatSize(result.incrementalPatchSize)}`;
    }
    uploadStatus.textContent = msg;
    uploadForm.reset();
    document.getElementById("forceUpdateInput").checked = true;
    document.getElementById("setActiveInput").checked = true;
    updateDropzoneLabel(null);
    await loadReleases();
    switchView("history");
  } catch (e) {
    uploadStatus.textContent = "上传失败: " + e.message;
  } finally {
    uploadBtn.disabled = false;
    if (progressWrap) progressWrap.hidden = true;
    setProgress(0);
  }
});

function uploadWithProgress(url, formData, onProgress) {
  return new Promise((resolve, reject) => {
    const xhr = new XMLHttpRequest();
    xhr.open("POST", url);
    const headers = adminHeaders();
    Object.keys(headers).forEach((k) => xhr.setRequestHeader(k, headers[k]));
    xhr.upload.onprogress = (ev) => {
      if (ev.lengthComputable) onProgress(ev.loaded / ev.total);
    };
    xhr.onload = () => {
      let data = {};
      try { data = JSON.parse(xhr.responseText || "{}"); } catch (_) {}
      if (xhr.status >= 200 && xhr.status < 300) resolve(data);
      else reject(new Error(data.detail || xhr.statusText || "上传失败"));
    };
    xhr.onerror = () => reject(new Error("网络错误"));
    xhr.send(formData);
  });
}

refreshBtn.addEventListener("click", loadReleases);

checkHealth();
loadReleases();
setInterval(checkHealth, 15000);
