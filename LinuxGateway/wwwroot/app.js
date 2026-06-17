const $ = (id) => document.getElementById(id);

const state = {
  nodes: [],
  jobs: [],
  selectedJobId: "",
};

document.addEventListener("DOMContentLoaded", init);

async function init() {
  bindEvents();
  try {
    await api("/api/me");
    showMain();
    await refreshAll();
  } catch {
    showLogin();
  }
}

function bindEvents() {
  $("loginForm").addEventListener("submit", login);
  $("logoutBtn").addEventListener("click", logout);
  $("refreshBtn").addEventListener("click", refreshAll);
  $("nodeForm").addEventListener("submit", saveNode);
  $("buildForm").addEventListener("submit", startBuild);
  $("buildNode").addEventListener("change", renderProjectOptions);
  $("buildProject").addEventListener("change", renderConfigOptions);
  $("refreshJobBtn").addEventListener("click", () => {
    if (state.selectedJobId) selectJob(state.selectedJobId);
  });
}

async function login(event) {
  event.preventDefault();
  $("loginError").textContent = "";
  try {
    await api("/api/auth/login", {
      method: "POST",
      body: JSON.stringify({
        userName: $("loginUser").value,
        password: $("loginPassword").value,
      }),
    });
    showMain();
    await refreshAll();
  } catch (error) {
    $("loginError").textContent = error.message;
  }
}

async function logout() {
  await api("/api/auth/logout", { method: "POST" });
  showLogin();
}

function showLogin() {
  $("loginView").classList.remove("hidden");
  $("mainView").classList.add("hidden");
}

function showMain() {
  $("loginView").classList.add("hidden");
  $("mainView").classList.remove("hidden");
}

async function refreshAll() {
  clearError();
  try {
    const [nodes, jobs] = await Promise.all([
      api("/api/nodes"),
      api("/api/builds"),
    ]);
    state.nodes = nodes;
    state.jobs = jobs;
    renderNodes();
    renderBuildSelectors();
    renderJobs();
  } catch (error) {
    showError(error);
  }
}

async function saveNode(event) {
  event.preventDefault();
  clearError();
  try {
    await api("/api/nodes", {
      method: "POST",
      body: JSON.stringify({
        id: $("nodeId").value || null,
        name: $("nodeName").value,
        baseUrl: $("nodeBaseUrl").value,
        gatewayToken: $("nodeToken").value,
        platforms: selectedNodePlatforms(),
        enabled: $("nodeEnabled").checked,
      }),
    });
    $("nodeToken").value = "";
    await refreshAll();
  } catch (error) {
    showError(error);
  }
}

function selectedNodePlatforms() {
  const platforms = [];
  if ($("nodeIos").checked) platforms.push("ios");
  if ($("nodeAndroid").checked) platforms.push("android");
  return platforms;
}

async function startBuild(event) {
  event.preventDefault();
  clearError();
  try {
    const job = await api("/api/builds", {
      method: "POST",
      body: JSON.stringify({
        nodeId: $("buildNode").value,
        projectId: $("buildProject").value,
        configId: $("buildConfig").value,
        branch: $("buildBranch").value || null,
        buildNumber: $("buildNumber").value || null,
        dryRun: $("dryRun").checked,
        skipGit: $("skipGit").checked,
        skipUnity: $("skipUnity").checked,
        skipXcode: $("skipXcode").checked,
        allowNonMac: $("allowNonMac").checked,
        notes: $("buildNotes").value || null,
      }),
    });
    state.selectedJobId = job.id;
    await refreshAll();
    await selectJob(job.id);
  } catch (error) {
    showError(error);
  }
}

function renderNodes() {
  if (state.nodes.length === 0) {
    $("nodesList").innerHTML = `<article class="item muted">还没有设备。先添加 Mac 或 Windows BuildServer。</article>`;
    return;
  }

  $("nodesList").innerHTML = state.nodes.map((node) => {
    const remote = node.remote;
    const projects = remote?.projects?.length || 0;
    const configs = remote?.configs?.length || 0;
    const lastSeen = node.lastSeenAt ? new Date(node.lastSeenAt).toLocaleString() : "-";
    return `<article class="item">
      <header>
        <strong>${escapeHtml(node.name)}</strong>
        <span class="status ${escapeHtml(node.lastStatus || "Unknown")}">${escapeHtml(node.lastStatus || "Unknown")}</span>
      </header>
      <div class="muted">${escapeHtml(node.baseUrl)}</div>
      <div>${platforms(node.platforms)}</div>
      <div class="muted">项目 ${projects} / 配置 ${configs} / 最后在线 ${escapeHtml(lastSeen)}</div>
      ${node.lastError ? `<div class="error">${escapeHtml(node.lastError)}</div>` : ""}
      <button class="secondary" type="button" onclick="fillNodeForm('${escapeHtml(node.id)}')">编辑</button>
    </article>`;
  }).join("");
}

function renderBuildSelectors() {
  const enabledNodes = state.nodes.filter((node) => node.enabled && node.remote);
  $("buildNode").innerHTML = enabledNodes.map((node) => `<option value="${escapeHtml(node.id)}">${escapeHtml(node.name)} / ${(node.platforms || []).join(",") || "auto"}</option>`).join("");
  renderProjectOptions();
}

function renderProjectOptions() {
  const node = selectedNode();
  const projects = node?.remote?.projects || [];
  $("buildProject").innerHTML = projects.map((project) => `<option value="${escapeHtml(project.id)}">${escapeHtml(project.name)} / ${escapeHtml(project.defaultBranch || "main")}</option>`).join("");
  renderConfigOptions();
}

function renderConfigOptions() {
  const node = selectedNode();
  const projectId = $("buildProject").value;
  const configs = (node?.remote?.configs || []).filter((config) => config.projectId === projectId);
  $("buildConfig").innerHTML = configs.map((config) => `<option value="${escapeHtml(config.id)}">${escapeHtml(config.name)} / ${escapeHtml(config.buildPlatform || "ios")}</option>`).join("");
}

function selectedNode() {
  return state.nodes.find((node) => node.id === $("buildNode").value);
}

function renderJobs() {
  if (state.jobs.length === 0) {
    $("jobsList").innerHTML = `<article class="item muted">暂无任务。</article>`;
    return;
  }

  $("jobsList").innerHTML = state.jobs.map((job) => `<article class="item">
    <header>
      <strong>${escapeHtml(job.nodeName)} / ${escapeHtml(job.projectName)} / ${escapeHtml(job.configName)}</strong>
      <span class="status ${escapeHtml(job.status)}">${escapeHtml(job.status)}</span>
    </header>
    <div class="muted">${new Date(job.createdAt).toLocaleString()} / ${platformBadge(job.buildPlatform)} / build ${escapeHtml(job.buildNumber || "-")}</div>
    ${job.error ? `<div class="error">${escapeHtml(job.error)}</div>` : ""}
    <button class="secondary" type="button" onclick="selectJob('${escapeHtml(job.id)}')">查看</button>
  </article>`).join("");
}

async function selectJob(jobId) {
  clearError();
  state.selectedJobId = jobId;
  try {
    const detail = await api(`/api/builds/${encodeURIComponent(jobId)}`);
    const artifacts = await api(`/api/builds/${encodeURIComponent(jobId)}/artifacts`);
    const log = await fetchText(`/api/builds/${encodeURIComponent(jobId)}/log?lines=600`);
    const job = detail.job;
    $("jobDetail").classList.remove("hidden");
    $("jobTitle").textContent = `${job.nodeName} / ${job.projectName} / ${job.configName}`;
    $("jobMeta").innerHTML = [
      ["状态", job.status],
      ["平台", job.buildPlatform],
      ["远程任务", job.remoteJobId],
      ["分支", job.branch || "-"],
      ["Build Number", job.buildNumber || "-"],
      ["dry-run", job.dryRun ? "true" : "false"],
      ["更新时间", new Date(job.updatedAt).toLocaleString()],
      ["错误", job.error || "-"],
    ].map(([key, value]) => `<div><strong>${escapeHtml(key)}:</strong> ${escapeHtml(value)}</div>`).join("");
    $("artifactsList").innerHTML = artifacts.length
      ? artifacts.map((artifact) => `<article class="item">
          <strong>${escapeHtml(artifact.type)}</strong>
          <div class="muted">${escapeHtml(artifact.path)} / ${formatBytes(artifact.sizeBytes)}</div>
          <a href="/api/builds/${encodeURIComponent(job.id)}/artifacts/${encodeURIComponent(artifact.id)}/download">下载</a>
        </article>`).join("")
      : `<article class="item muted">暂无产物。</article>`;
    $("jobLog").textContent = log || "暂无日志。";
    await refreshAll();
  } catch (error) {
    showError(error);
  }
}

function fillNodeForm(nodeId) {
  const node = state.nodes.find((item) => item.id === nodeId);
  if (!node) return;
  $("nodeId").value = node.id;
  $("nodeName").value = node.name;
  $("nodeBaseUrl").value = node.baseUrl;
  $("nodeToken").value = "";
  $("nodeIos").checked = (node.platforms || []).includes("ios");
  $("nodeAndroid").checked = (node.platforms || []).includes("android");
  $("nodeEnabled").checked = node.enabled;
}

async function api(path, options = {}) {
  const response = await fetch(path, {
    credentials: "same-origin",
    headers: { "Content-Type": "application/json", ...(options.headers || {}) },
    ...options,
  });
  if (!response.ok) {
    let message = response.statusText;
    try {
      const error = await response.json();
      message = error.error || message;
    } catch {}
    throw new Error(message);
  }
  if (response.status === 204) return null;
  return await response.json();
}

async function fetchText(path) {
  const response = await fetch(path, { credentials: "same-origin" });
  if (!response.ok) {
    throw new Error(response.statusText);
  }
  return await response.text();
}

function platforms(values) {
  const list = values && values.length ? values : ["auto"];
  return list.map((value) => platformBadge(value)).join(" ");
}

function platformBadge(value) {
  const platform = value || "ios";
  return `<span class="platform ${escapeHtml(platform)}">${escapeHtml(platform)}</span>`;
}

function formatBytes(size) {
  const value = Number(size || 0);
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  if (value < 1024 * 1024 * 1024) return `${(value / 1024 / 1024).toFixed(1)} MB`;
  return `${(value / 1024 / 1024 / 1024).toFixed(1)} GB`;
}

function showError(error) {
  $("globalError").textContent = error.message || String(error);
  $("globalError").classList.remove("hidden");
}

function clearError() {
  $("globalError").textContent = "";
  $("globalError").classList.add("hidden");
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
