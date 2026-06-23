const $ = (id) => document.getElementById(id);

const state = {
  nodes: [],
  jobs: [],
  selectedJobId: "",
  events: null,
};

document.addEventListener("DOMContentLoaded", init);

async function init() {
  bindEvents();
  try {
    await api("/api/me");
    showMain();
    await refreshAll();
    startDashboardEvents();
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
  $("nodesList").addEventListener("click", handleNodesClick);
  $("jobsList").addEventListener("click", handleJobsClick);
  $("refreshJobBtn").addEventListener("click", () => {
    if (state.selectedJobId) selectJob(state.selectedJobId);
  });
}

async function login(event) {
  event.preventDefault();
  $("loginError").textContent = "";
  setButtonBusy("loginBtn", true, "登录中...");
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
    startDashboardEvents();
  } catch (error) {
    $("loginError").textContent = error.message;
  } finally {
    setButtonBusy("loginBtn", false);
  }
}

async function logout() {
  await api("/api/auth/logout", { method: "POST" });
  stopDashboardEvents();
  state.selectedJobId = "";
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

function applyDashboard(dashboard) {
  state.nodes = dashboard.nodes || [];
  state.jobs = dashboard.jobs || [];
  renderNodes();
  renderBuildSelectors();
  renderJobs();
}

function startDashboardEvents() {
  stopDashboardEvents();
  state.events = AppRuntime.connectEvents({
    onDashboard: (dashboard) => applyDashboard(dashboard),
    onStatus: (message) => {
      if (!$("mainView").classList.contains("hidden")) showNotice(message);
    },
    onFallbackPoll: () => refreshAll({ silent: true }),
    fallbackIntervalMs: 5000,
  });
}

function stopDashboardEvents() {
  if (state.events) {
    state.events.close();
    state.events = null;
  }
}

async function refreshAll(options = {}) {
  clearError();
  if (!options.silent) {
    showNotice("正在刷新设备和任务。离线节点由后台刷新服务标记，不会阻塞整个页面。");
  }
  setTopStatus("刷新中");
  setButtonBusy("refreshBtn", true, "刷新中...");
  if (state.nodes.length === 0) {
    $("nodesList").innerHTML = loadingItem("正在请求节点列表...");
  }
  if (state.jobs.length === 0) {
    $("jobsList").innerHTML = loadingItem("正在读取任务列表...");
  }
  try {
    applyDashboard(await api("/api/dashboard"));
    if (!options.silent) {
      showNotice("刷新完成。如果设备仍显示 Offline，请先确认 Linux 服务器能 curl 通该设备的 /api/health。");
    }
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("refreshBtn", false);
    setTopStatus("就绪");
  }
}

async function saveNode(event) {
  event.preventDefault();
  clearError();
  showNotice("正在保存设备并刷新节点状态。保存后会立即请求该 BuildServer，请等待状态返回。");
  setTopStatus("保存设备中");
  setButtonBusy("nodeSaveBtn", true, "保存中...");
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
    await refreshAll({ silent: true });
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("nodeSaveBtn", false);
    setTopStatus("就绪");
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
  const selectionError = buildSelectionError();
  if (selectionError) {
    showError(new Error(selectionError));
    updateBuildSubmitState();
    return;
  }
  $("buildSubmitHint").classList.remove("hidden");
  showNotice("正在提交打包任务到选中节点。返回任务后可以在任务列表里查看日志。");
  setTopStatus("提交任务中");
  setButtonBusy("buildSubmitBtn", true, "提交中...");
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
        clientRequestId: AppRuntime.createRequestId("gwbuild"),
        notes: $("buildNotes").value || null,
      }),
    });
    state.selectedJobId = job.id;
    await refreshAll({ silent: true });
    await selectJob(job.id);
  } catch (error) {
    showError(error);
  } finally {
    $("buildSubmitHint").classList.add("hidden");
    setButtonBusy("buildSubmitBtn", false);
    updateBuildSubmitState();
    setTopStatus("就绪");
  }
}

function renderNodes() {
  renderSummary();
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
        <div>
          <strong>${escapeHtml(node.name)}</strong>
          <div class="muted small">${escapeHtml(node.baseUrl)}</div>
        </div>
        <span class="status ${escapeHtml(node.lastStatus || "Unknown")}">${escapeHtml(node.lastStatus || "Unknown")}</span>
      </header>
      <div class="item-row">${platforms(node.platforms)}</div>
      <div class="muted">项目 ${projects} / 配置 ${configs} / 最后在线 ${escapeHtml(lastSeen)}</div>
      ${node.lastError ? `<div class="error node-error">${escapeHtml(node.lastError)}<br>如果是 timeout，请优先在 Linux 上测试 curl 该地址的 /api/health。</div>` : ""}
      <button class="secondary" type="button" data-edit-node-id="${escapeHtml(node.id)}">编辑</button>
    </article>`;
  }).join("");
}

function renderBuildSelectors() {
  const selectedNodeId = $("buildNode").value;
  const enabledNodes = state.nodes.filter((node) => node.enabled && node.remote);
  $("buildNode").innerHTML = enabledNodes.length
    ? enabledNodes.map((node) => `<option value="${escapeHtml(node.id)}">${escapeHtml(node.name)} / ${(node.platforms || []).join(",") || "auto"}</option>`).join("")
    : `<option value="">暂无在线设备</option>`;
  if (enabledNodes.some((node) => node.id === selectedNodeId)) {
    $("buildNode").value = selectedNodeId;
  }
  renderProjectOptions();
}

function renderProjectOptions() {
  const selectedProjectId = $("buildProject").value;
  const node = selectedNode();
  const projects = node?.remote?.projects || [];
  $("buildProject").innerHTML = projects.length
    ? projects.map((project) => `<option value="${escapeHtml(project.id)}">${escapeHtml(project.name)} / ${escapeHtml(project.defaultBranch || "main")}</option>`).join("")
    : `<option value="">暂无项目</option>`;
  if (projects.some((project) => project.id === selectedProjectId)) {
    $("buildProject").value = selectedProjectId;
  }
  renderConfigOptions();
}

function renderConfigOptions() {
  const selectedConfigId = $("buildConfig").value;
  const node = selectedNode();
  const projectId = $("buildProject").value;
  const configs = (node?.remote?.configs || []).filter((config) => config.projectId === projectId);
  $("buildConfig").innerHTML = configs.length
    ? configs.map((config) => `<option value="${escapeHtml(config.id)}">${escapeHtml(config.name)} / ${escapeHtml(config.buildPlatform || "ios")}</option>`).join("")
    : `<option value="">暂无配置</option>`;
  if (configs.some((config) => config.id === selectedConfigId)) {
    $("buildConfig").value = selectedConfigId;
  }
  updateBuildSubmitState();
}

function selectedNode() {
  return state.nodes.find((node) => node.id === $("buildNode").value);
}

function buildSelectionError() {
  if (!$("buildNode").value) return "暂无在线可用设备，不能提交打包任务。";
  if (!$("buildProject").value) return "请选择可用项目。";
  if (!$("buildConfig").value) return "请选择可用配置。";
  return "";
}

function updateBuildSubmitState() {
  const button = $("buildSubmitBtn");
  if (!button || button.getAttribute("aria-busy") === "true") return;
  const error = buildSelectionError();
  button.disabled = Boolean(error);
  button.title = error;
}

function renderJobs() {
  renderSummary();
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
    <button class="secondary" type="button" data-view-job-id="${escapeHtml(job.id)}">查看</button>
  </article>`).join("");
}

function handleNodesClick(event) {
  const button = event.target.closest("[data-edit-node-id]");
  if (!button) return;
  fillNodeForm(button.dataset.editNodeId);
}

function handleJobsClick(event) {
  const button = event.target.closest("[data-view-job-id]");
  if (!button) return;
  AppRuntime.runAction(button, () => selectJob(button.dataset.viewJobId), {
    busyText: "读取中...",
    onError: showError,
  });
}

async function selectJob(jobId) {
  clearError();
  state.selectedJobId = jobId;
  $("jobDetail").classList.remove("hidden");
  $("jobLoadingHint").classList.remove("hidden");
  $("jobMeta").innerHTML = loadingItem("正在读取任务状态...");
  $("artifactsList").innerHTML = loadingItem("正在读取产物列表...");
  $("jobLog").textContent = "正在读取远程日志，请稍等...";
  setTopStatus("读取任务中");
  setButtonBusy("refreshJobBtn", true, "读取中...");
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
    await refreshAll({ silent: true });
  } catch (error) {
    showError(error);
  } finally {
    $("jobLoadingHint").classList.add("hidden");
    setButtonBusy("refreshJobBtn", false);
    setTopStatus("就绪");
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
  showNotice("已载入设备信息。Gateway Token 不会回显；不修改 token 时可以留空。");
}

function renderSummary() {
  const onlineNodes = state.nodes.filter((node) => node.remote && node.lastStatus !== "Offline").length;
  const configCount = state.nodes.reduce((total, node) => total + (node.remote?.configs?.length || 0), 0);
  $("nodeCountBadge").textContent = String(state.nodes.length);
  $("onlineNodeCount").textContent = String(onlineNodes);
  $("remoteConfigCount").textContent = String(configCount);
  $("jobCount").textContent = String(state.jobs.length);
}

async function api(path, options = {}) {
  return AppRuntime.requestJson(path, options);
}

async function fetchText(path) {
  return AppRuntime.requestText(path);
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
  $("globalNotice").classList.add("hidden");
}

function clearError() {
  $("globalError").textContent = "";
  $("globalError").classList.add("hidden");
}

function showNotice(message) {
  $("globalNotice").textContent = message;
  $("globalNotice").classList.remove("hidden");
  $("globalError").classList.add("hidden");
}

function setTopStatus(text) {
  $("topStatus").textContent = text;
}

function setButtonBusy(id, busy, busyText = "处理中...") {
  AppRuntime.setButtonBusy(id, busy, busyText);
}

function loadingItem(text) {
  return `<article class="item loading"><span class="spinner"></span><span>${escapeHtml(text)}</span></article>`;
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
