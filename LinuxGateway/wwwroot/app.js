const $ = (id) => document.getElementById(id);

const STATUS_LABELS = {
  Online: "在线",
  Offline: "离线",
  Revoked: "已吊销",
  Disabled: "已停用",
  Enabled: "已启用",
  Unknown: "未知",
  Queued: "排队中",
  Running: "执行中",
  Succeeded: "成功",
  Failed: "失败",
  Canceled: "已取消",
  Cancelled: "已取消",
  Idle: "空闲",
};

const ROLE_LABELS = {
  Admin: "管理员",
  Builder: "构建员",
  Viewer: "查看者",
};

const PLATFORM_LABELS = {
  ios: "iOS",
  android: "Android",
  auto: "自动",
};

const ARTIFACT_TYPE_LABELS = {
  ipa: "IPA 包",
  apk: "APK 包",
  aab: "AAB 包",
  archive: "归档",
  log: "日志",
  logs: "日志",
  folder: "文件夹",
  directory: "目录",
  file: "文件",
};

const state = {
  user: null,
  nodes: [],
  jobs: [],
  users: [],
  selectedJobId: "",
  selectedUserId: "",
  activeTab: "overview",
  events: null,
  jobDetailTimer: null,
};

document.addEventListener("DOMContentLoaded", init);

async function init() {
  bindEvents();
  try {
    state.user = await api("/api/me");
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
  $("passwordForm").addEventListener("submit", changeMyPassword);
  $("userForm").addEventListener("submit", saveUser);
  $("userAddBtn").addEventListener("click", () => openUserModal());
  $("userCancelBtn").addEventListener("click", closeUserModal);
  $("userModalClose").addEventListener("click", closeUserModal);
  $("userModal").addEventListener("click", (event) => {
    if (event.target === $("userModal")) closeUserModal();
  });
  $("usersList").addEventListener("click", handleUsersClick);
  $("usersList").addEventListener("keydown", handleUsersKeydown);
  $("userDetailPanel").addEventListener("click", handleUsersClick);
  $("buildNode").addEventListener("change", renderProjectOptions);
  $("buildProject").addEventListener("change", renderConfigOptions);
  $("nodesList").addEventListener("click", handleNodesClick);
  $("jobsList").addEventListener("click", handleJobsClick);
  $("generateTokenBtn").addEventListener("click", generateEnrollmentToken);
  $("copyEnrollBtn").addEventListener("click", copyEnrollConfig);
  $("refreshJobBtn").addEventListener("click", () => {
    if (state.selectedJobId) selectJob(state.selectedJobId);
  });
  document.querySelectorAll(".app-sidebar button[data-tab]").forEach((button) => {
    button.addEventListener("click", () => setTab(button.dataset.tab));
  });
  document.addEventListener("keydown", (event) => {
    if (event.key === "Escape" && isUserModalOpen()) closeUserModal();
  });
}

async function login(event) {
  event.preventDefault();
  $("loginError").textContent = "";
  setButtonBusy("loginBtn", true, "登录中...");
  try {
    state.user = await api("/api/auth/login", {
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
  state.user = null;
  state.users = [];
  stopJobDetailPolling();
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
  $("userInfo").textContent = `${state.user?.displayName || state.user?.userName || "-"} / ${roleLabel(state.user?.role)}`;
  setTab(state.activeTab || "overview");
  renderPermissionChrome();
}

function applyDashboard(dashboard) {
  state.nodes = dashboard.nodes || [];
  state.jobs = dashboard.jobs || [];
  renderNodes();
  renderBuildSelectors();
  renderJobs();
  renderPermissionChrome();
}

function startDashboardEvents() {
  stopDashboardEvents();
  state.events = AppRuntime.connectEvents({
    onDashboard: (dashboard) => {
      applyDashboard(dashboard);
      setTopStatus("实时同步");
    },
    onStatus: (message) => {
      if (!$("mainView").classList.contains("hidden")) {
        setTopStatus(message);
        showNotice(message);
      }
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

function setTab(tab) {
  if (tab === "users" && !isAdmin()) {
    showError(new Error("只有管理员可以进入用户权限模块。"));
    tab = "account";
  }

  state.activeTab = tab;
  if (tab !== "jobs") {
    stopJobDetailPolling();
  }
  document.querySelectorAll(".app-sidebar button[data-tab]").forEach((button) => {
    button.classList.toggle("active", button.dataset.tab === tab);
  });
  document.querySelectorAll(".tab").forEach((panel) => panel.classList.add("hidden"));
  $(`${tab}Tab`).classList.remove("hidden");
  const meta = tabMeta(tab);
  $("pageTitle").textContent = meta.title;
  $("pageSubTitle").textContent = meta.subtitle;
  $("activeRouteTag").textContent = meta.title;

  if (tab === "users" && isAdmin()) {
    refreshUsers().catch(showError);
  }
}

function tabMeta(tab) {
  return {
    overview: { title: "首页概览", subtitle: "查看节点在线数、配置同步数量和最近任务规模。" },
    nodes: { title: "设备节点", subtitle: "维护 Mac / Windows BuildServer 节点，观察同步状态和远端错误。" },
    builds: { title: "发起打包", subtitle: "选择目标设备、项目和配置，提交可追踪的幂等构建任务。" },
    jobs: { title: "任务队列", subtitle: "查看远程任务状态、日志和构建产物。" },
    account: { title: "我的账户", subtitle: "查看当前角色并维护自己的登录密码。" },
    users: { title: "用户权限", subtitle: "集中管理 Gateway 用户、角色和启用状态。" },
  }[tab] || { title: "首页概览", subtitle: "查看节点在线数、配置同步数量和最近任务规模。" };
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
    if (isAdmin()) {
      await refreshUsers();
    }
    if (!options.silent) {
      showNotice("刷新完成。如果设备仍显示离线，请先确认 Linux 服务器能 curl 通该设备的 /api/health。");
    }
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("refreshBtn", false);
    setTopStatus("就绪");
  }
}

function isAdmin() {
  return state.user?.role === "Admin";
}

function canBuild() {
  return state.user?.role === "Admin" || state.user?.role === "Builder";
}

function renderPermissionChrome() {
  $("adminPanel").classList.toggle("hidden", !isAdmin());
  $("usersNav").classList.toggle("hidden", !isAdmin());
  if (state.activeTab === "users" && !isAdmin()) {
    setTab("account");
  }
  $("roleHint").textContent = roleDescription();
  setFormDisabled("nodeForm", !isAdmin(), "只有管理员可以新增或更新节点。");
  setFormDisabled("buildForm", !canBuild(), "当前角色不能提交构建任务。");
  if (isAdmin()) {
    renderUsers();
  }
  updateBuildSubmitState();
}

function roleDescription() {
  if (!state.user) return "未登录。";
  if (isAdmin()) return "管理员可以维护节点、用户并提交构建。";
  if (canBuild()) return "构建员可以提交构建并查看任务，不能维护节点或用户。";
  return "查看者可以查看节点、任务、日志和产物，不能修改配置或提交构建。";
}

function setFormDisabled(formId, disabled, reason) {
  const form = $(formId);
  if (!form) return;
  form.querySelectorAll("input, select, textarea, button").forEach((control) => {
    if (disabled) {
      if (!control.disabled) {
        control.dataset.permissionDisabled = "true";
        control.disabled = true;
      }
      control.title = reason;
      return;
    }

    if (control.dataset.permissionDisabled === "true") {
      control.disabled = false;
      delete control.dataset.permissionDisabled;
      control.removeAttribute("title");
    }
  });
}

async function saveNode(event) {
  event.preventDefault();
  clearError();
  if (!isAdmin()) {
    showError(new Error("只有管理员可以保存节点。"));
    return;
  }
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
  if (!canBuild()) {
    showError(new Error("当前角色不能提交构建任务。"));
    updateBuildSubmitState();
    return;
  }
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
    setTab("jobs");
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
    $("nodesList").innerHTML = `<div class="empty-state compact">还没有设备。先添加 Mac 或 Windows BuildServer。</div>`;
    return;
  }

  $("nodesList").innerHTML = `<div class="node-stack">${state.nodes.map(renderNodeCard).join("")}</div>`;
}

function renderNodeCard(node) {
  const remote = node.remote;
  const projects = remote?.projects?.length || 0;
  const configs = remote?.configs?.length || 0;
  const isReverse = node.connectionMode === "Reverse";
  const revoked = isCredentialRevoked(node);
  const status = nodeDisplayStatus(node);
  const lastSeen = node.lastSeenAt ? new Date(node.lastSeenAt).toLocaleString() : "-";
  const revokedAt = node.credentialRevokedAt ? new Date(node.credentialRevokedAt).toLocaleString() : "";
  const modeBadge = isReverse
    ? `<span class="mode-badge reverse">反向连接</span>`
    : `<span class="mode-badge direct">直连</span>`;
  const statusNote = nodeStatusNote(node);
  return `<article class="node-card${revoked ? " revoked" : ""}">
    <header>
      <div class="node-main">
        <strong>${escapeHtml(node.name)}</strong>
        <span class="node-url">${escapeHtml(node.baseUrl || statusLabel(node.connectionStatus) || "-")}</span>
      </div>
      <span class="status ${escapeHtml(status)}">${escapeHtml(statusLabel(status))}</span>
    </header>
    <div class="item-row">${modeBadge} ${platforms(node.platforms)}</div>
    <dl class="node-metrics">
      <div><dt>项目</dt><dd>${projects}</dd></div>
      <div><dt>配置</dt><dd>${configs}</dd></div>
      <div><dt>最后在线</dt><dd>${escapeHtml(lastSeen)}</dd></div>
      ${revokedAt ? `<div><dt>吊销时间</dt><dd>${escapeHtml(revokedAt)}</dd></div>` : ""}
    </dl>
    ${statusNote ? `<div class="${revoked ? "node-revoked-note" : "error node-error"}">${escapeHtml(statusNote)}</div>` : ""}
    ${node.lastError && !statusNote ? `<div class="error node-error">${escapeHtml(node.lastError)}<br>如果是 timeout，请优先在 Linux 上测试 curl 该地址的 /api/health。</div>` : ""}
    ${isAdmin() ? renderNodeActions(node) : ""}
  </article>`;
}

function nodeDisplayStatus(node) {
  if (isCredentialRevoked(node)) return "Revoked";
  if (!node.enabled) return "Disabled";
  if (node.connectionMode === "Reverse" && node.connectionStatus) return node.connectionStatus;
  return node.lastStatus || "Unknown";
}

function nodeStatusNote(node) {
  if (isCredentialRevoked(node)) {
    return "凭据已吊销，BuildServer 需要重新生成 Enrollment Token 后重新注册。确认不再需要这个旧节点时，可以移除记录。";
  }
  return node.lastError || "";
}

function isCredentialRevoked(node) {
  return Boolean(node?.credentialRevoked || node?.credentialRevokedAt);
}

function renderNodeActions(node) {
  const nodeId = escapeHtml(node.id);
  const isReverse = node.connectionMode === "Reverse";
  const revoked = isCredentialRevoked(node);
  const editButton = `<button class="secondary" type="button" data-edit-node-id="${nodeId}">编辑节点</button>`;
  const revokeButton = isReverse && !revoked
    ? `<button class="danger" type="button" data-revoke-node-id="${nodeId}">吊销凭据</button>`
    : "";
  const removeButton = isReverse && revoked
    ? `<button class="danger outline" type="button" data-remove-node-id="${nodeId}">移除记录</button>`
    : "";
  return `<div class="node-actions">${editButton}${revokeButton}${removeButton}</div>`;
}

function renderBuildSelectors() {
  const selectedNodeId = $("buildNode").value;
  const enabledNodes = state.nodes.filter(isBuildableNode);
  $("buildNode").innerHTML = enabledNodes.length
    ? enabledNodes.map((node) => `<option value="${escapeHtml(node.id)}">${escapeHtml(node.name)} / ${escapeHtml((node.platforms || []).map(platformLabel).join(", ") || platformLabel("auto"))}</option>`).join("")
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

function isBuildableNode(node) {
  return Boolean(node?.enabled &&
    !isCredentialRevoked(node) &&
    node.remote &&
    node.lastStatus !== "Offline" &&
    node.connectionStatus !== "Offline" &&
    node.connectionStatus !== "Revoked");
}

function buildSelectionError() {
  if (!canBuild()) return "当前角色不能提交构建任务。";
  if (!$("buildNode").value) return "暂无在线可用设备，不能提交打包任务。";
  if (!isBuildableNode(selectedNode())) return "所选设备当前不可用，请等待节点同步完成或重新选择设备。";
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
    $("jobsList").innerHTML = `<div class="empty-state">暂无任务。选择在线设备、项目和配置后即可发起打包。</div>`;
    return;
  }

  $("jobsList").innerHTML = `<div class="table-shell jobs-table-shell">
    <table class="data-table jobs-table">
      <thead>
        <tr>
          <th>任务</th>
          <th>状态</th>
          <th>平台</th>
          <th>Build</th>
          <th>创建时间</th>
          <th class="table-actions">操作</th>
        </tr>
      </thead>
      <tbody>
        ${state.jobs.map(renderJobRow).join("")}
      </tbody>
    </table>
  </div>`;
}

function renderJobRow(job) {
  const createdAt = job.createdAt ? new Date(job.createdAt).toLocaleString() : "-";
  return `<tr>
    <td>
      <div class="job-title-cell">
        <strong>${escapeHtml(job.projectName)} / ${escapeHtml(job.configName)}</strong>
        <div class="muted small">${escapeHtml(job.nodeName)}${job.branch ? ` / ${escapeHtml(job.branch)}` : ""}</div>
        ${job.error ? `<div class="error job-error">${escapeHtml(job.error)}</div>` : ""}
      </div>
    </td>
    <td><span class="status ${escapeHtml(job.status)}">${escapeHtml(statusLabel(job.status))}</span></td>
    <td>${platformBadge(job.buildPlatform)}</td>
    <td>${escapeHtml(job.buildNumber || "-")}</td>
    <td class="nowrap">${escapeHtml(createdAt)}</td>
    <td class="table-actions">
      <button class="secondary" type="button" data-view-job-id="${escapeHtml(job.id)}">查看详情</button>
    </td>
  </tr>`;
}

function handleNodesClick(event) {
  const editButton = event.target.closest("[data-edit-node-id]");
  if (editButton) {
    if (!isAdmin()) {
      showError(new Error("只有管理员可以编辑节点。"));
      return;
    }
    fillNodeForm(editButton.dataset.editNodeId);
    return;
  }

  const revokeButton = event.target.closest("[data-revoke-node-id]");
  if (revokeButton) {
    revokeNodeCredential(revokeButton.dataset.revokeNodeId);
    return;
  }

  const removeButton = event.target.closest("[data-remove-node-id]");
  if (removeButton) {
    removeNodeRecord(removeButton.dataset.removeNodeId);
  }
}

async function generateEnrollmentToken() {
  setButtonBusy("generateTokenBtn", true, "生成中...");
  try {
    const result = await api("/api/reverse-nodes/enrollment-tokens", {
      method: "POST",
      body: JSON.stringify({ nodeNameHint: $("enrollNodeNameHint").value || "" }),
    });
    $("enrollGatewayUrl").value = window.location.origin;
    $("enrollTokenValue").value = result.token;
    $("enrollResult").classList.remove("hidden");
    showNotice("Token 已生成，24 小时内有效。将地址和 Token 填入 BuildServer 的 Gateway 连接页面。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("generateTokenBtn", false);
  }
}

function copyEnrollConfig() {
  const url = $("enrollGatewayUrl").value;
  const token = $("enrollTokenValue").value;
  const text = `Gateway 地址: ${url}\nEnrollment Token: ${token}`;
  navigator.clipboard?.writeText(text).then(() => {
    showNotice("配置已复制到剪贴板。");
  }).catch(() => {
    showNotice("请手动复制地址和 Token。");
  });
}

async function revokeNodeCredential(nodeId) {
  const node = state.nodes.find((item) => item.id === nodeId);
  const nodeName = node?.name || nodeId;
  if (!confirm(`确认吊销 ${nodeName} 的凭据？吊销后该 BuildServer 会立即断开，需要重新生成 Enrollment Token 后重新注册。`)) return;
  try {
    await api(`/api/reverse-nodes/${encodeURIComponent(nodeId)}/revoke`, { method: "POST" });
    await refreshAll({ silent: true });
    showNotice("节点凭据已吊销。该记录已标记为已吊销，可重新注册新节点或移除旧记录。");
  } catch (error) {
    showError(error);
  }
}

async function removeNodeRecord(nodeId) {
  const node = state.nodes.find((item) => item.id === nodeId);
  const nodeName = node?.name || nodeId;
  if (!confirm(`确认移除 ${nodeName} 的节点记录？这会从设备列表删除旧记录，但不会删除历史任务。`)) return;
  try {
    await api(`/api/reverse-nodes/${encodeURIComponent(nodeId)}`, { method: "DELETE" });
    await refreshAll({ silent: true });
    showNotice("旧节点记录已移除。");
  } catch (error) {
    showError(error);
  }
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
  stopJobDetailPolling();
  state.selectedJobId = jobId;
  if (state.activeTab !== "jobs") {
    setTab("jobs");
  }
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
    renderJobDetail(job, artifacts, log);
    startJobDetailPolling(job);
    await refreshAll({ silent: true });
  } catch (error) {
    showError(error);
  } finally {
    $("jobLoadingHint").classList.add("hidden");
    setButtonBusy("refreshJobBtn", false);
    setTopStatus("就绪");
  }
}

function renderJobDetail(job, artifacts, log) {
  $("jobDetail").classList.remove("hidden");
  $("jobTitle").textContent = `${job.nodeName} / ${job.projectName} / ${job.configName}`;
  $("jobMeta").innerHTML = [
    ["状态", statusLabel(job.status)],
    ["平台", platformLabel(job.buildPlatform)],
    ["远程任务", job.remoteJobId],
    ["分支", job.branch || "-"],
    ["Build Number", job.buildNumber || "-"],
    ["演练模式", job.dryRun ? "是" : "否"],
    ["更新时间", new Date(job.updatedAt).toLocaleString()],
    ["错误", job.error || "-"],
  ].map(([key, value]) => `<div><strong>${escapeHtml(key)}:</strong> ${escapeHtml(value)}</div>`).join("");
  $("artifactsList").innerHTML = renderArtifactsTable(job, artifacts);
  $("jobLog").textContent = log || "暂无日志。";
}

function startJobDetailPolling(job) {
  stopJobDetailPolling();
  if (!job || isTerminalJobStatus(job.status)) return;
  state.jobDetailTimer = setInterval(() => {
    refreshSelectedJobDetail(job.id).catch(() => {
      setTopStatus("任务详情刷新失败");
    });
  }, 3000);
}

function stopJobDetailPolling() {
  if (!state.jobDetailTimer) return;
  clearInterval(state.jobDetailTimer);
  state.jobDetailTimer = null;
}

async function refreshSelectedJobDetail(jobId) {
  if (state.selectedJobId !== jobId || state.activeTab !== "jobs" || $("jobDetail").classList.contains("hidden")) {
    stopJobDetailPolling();
    return;
  }

  const [detail, artifacts, log] = await Promise.all([
    api(`/api/builds/${encodeURIComponent(jobId)}`),
    api(`/api/builds/${encodeURIComponent(jobId)}/artifacts`),
    fetchText(`/api/builds/${encodeURIComponent(jobId)}/log?lines=600`),
  ]);

  const job = detail.job;
  renderJobDetail(job, artifacts, log);
  if (isTerminalJobStatus(job.status)) {
    stopJobDetailPolling();
  }
}

function isTerminalJobStatus(status) {
  return ["Succeeded", "Failed", "Canceled"].includes(status);
}

function renderArtifactsTable(job, artifacts) {
  if (!artifacts.length) {
    return `<div class="empty-state compact">暂无产物。</div>`;
  }

  return `<div class="table-shell artifacts-table-shell">
    <table class="data-table artifacts-table">
      <thead>
        <tr>
          <th>类型</th>
          <th>路径</th>
          <th>大小</th>
          <th class="table-actions">操作</th>
        </tr>
      </thead>
      <tbody>
        ${artifacts.map((artifact) => `<tr>
          <td><span class="role-pill">${escapeHtml(artifactTypeLabel(artifact.type))}</span></td>
          <td class="path-cell">${escapeHtml(artifact.path || "-")}</td>
          <td>${formatBytes(artifact.sizeBytes)}</td>
          <td class="table-actions">
            <a class="download-link" href="/api/builds/${encodeURIComponent(job.id)}/artifacts/${encodeURIComponent(artifact.id)}/download">下载</a>
          </td>
        </tr>`).join("")}
      </tbody>
    </table>
  </div>`;
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

async function refreshUsers() {
  if (!isAdmin()) return;
  state.users = await api("/api/users");
  renderUsers();
}

function renderUsers() {
  if (!isAdmin()) return;
  renderUserStats();
  $("userDirectoryCount").textContent = `${state.users.length} 人`;
  if (state.users.length === 0) {
    state.selectedUserId = "";
    $("usersList").innerHTML = `<div class="empty-state compact">暂无用户。点击右上角新增用户。</div>`;
    renderUserDetail(null);
    return;
  }

  if (!state.users.some((user) => user.id === state.selectedUserId)) {
    state.selectedUserId = state.users[0].id;
  }
  $("usersList").innerHTML = state.users.map(renderUserListItem).join("");
  renderUserDetail();
}

function renderUserStats() {
  const total = state.users.length;
  const enabled = state.users.filter((user) => user.enabled).length;
  const admins = state.users.filter((user) => user.role === "Admin").length;
  const protectedUsers = state.users.filter(isRootAdminUser).length;
  $("userStats").innerHTML = [
    ["用户总数", total],
    ["启用账号", enabled],
    ["管理员", admins],
    ["受保护主账号", protectedUsers],
  ].map(([label, value]) => `<article><span>${label}</span><strong>${value}</strong></article>`).join("");
}

function renderUserListItem(user) {
  const selected = user.id === state.selectedUserId;
  const protectedText = isRootAdminUser(user) ? `<span class="protect-note">主账号</span>` : "";
  return `<article class="user-list-item${selected ? " selected" : ""}" role="option" tabindex="0" aria-selected="${selected ? "true" : "false"}" data-select-user-id="${escapeHtml(user.id)}">
    <span class="avatar">${avatarText(user)}</span>
    <span class="user-list-main">
      <span class="user-list-title">
        <strong>${escapeHtml(user.displayName || user.userName)}</strong>
        ${protectedText}
      </span>
      <span class="muted small">${escapeHtml(user.userName)}</span>
    </span>
    <span class="user-list-badges">
      <span class="role-pill">${escapeHtml(roleLabel(user.role))}</span>
      <span class="status ${user.enabled ? "Succeeded" : "Canceled"}">${enabledLabel(user.enabled)}</span>
    </span>
  </article>`;
}

function renderUserDetail(user = state.users.find((item) => item.id === state.selectedUserId)) {
  if (!user) {
    $("userDetailPanel").innerHTML = `<div class="user-detail-empty">
      <p class="eyebrow">账号详情</p>
      <h4>还没有可查看的账号</h4>
      <p class="muted">点击右上角“新增用户”为 Gateway 团队成员分配权限。</p>
    </div>`;
    return;
  }

  const protectedReason = protectedUserReason(user);
  const actionDisabled = protectedReason || !user.enabled ? "disabled" : "";
  const actionTitle = protectedReason || (!user.enabled ? "用户已经禁用。" : "");
  $("userDetailPanel").innerHTML = `<div class="user-detail-header">
      <span class="avatar large">${avatarText(user)}</span>
      <div>
        <p class="eyebrow">当前账号</p>
        <h4>${escapeHtml(user.displayName || user.userName)}</h4>
        <p class="muted">${escapeHtml(user.userName)}</p>
      </div>
      <span class="status ${user.enabled ? "Succeeded" : "Canceled"}">${enabledLabel(user.enabled)}</span>
    </div>
    <dl class="user-detail-grid">
      <div>
        <dt>角色</dt>
        <dd><span class="role-pill">${escapeHtml(roleLabel(user.role))}</span></dd>
      </div>
      <div>
        <dt>账号类型</dt>
        <dd>${isRootAdminUser(user) ? "受保护主账号" : "普通 Gateway 账号"}</dd>
      </div>
      <div>
        <dt>创建时间</dt>
        <dd>${user.createdAt ? new Date(user.createdAt).toLocaleString() : "-"}</dd>
      </div>
      <div>
        <dt>账号 ID</dt>
        <dd class="path-cell">${escapeHtml(user.id)}</dd>
      </div>
    </dl>
    <section class="permission-card">
      <p class="eyebrow">权限范围</p>
      <strong>${escapeHtml(roleLabel(user.role))}</strong>
      <p>${escapeHtml(roleDescription(user.role))}</p>
      ${protectedReason ? `<p class="protect-warning">${escapeHtml(protectedReason)}</p>` : ""}
    </section>
    <div class="user-detail-actions">
      <button class="secondary" type="button" data-edit-user-id="${escapeHtml(user.id)}">编辑用户</button>
      <button class="danger" type="button" data-disable-user-id="${escapeHtml(user.id)}" title="${escapeHtml(actionTitle)}" ${actionDisabled}>停用/删除</button>
    </div>`;
}

function roleDescription(role) {
  return {
    Admin: "拥有节点维护、用户权限、任务提交、日志产物查看和系统管理权限。",
    Builder: "可以选择在线节点发起构建，并查看自己可访问的任务、日志和产物。",
    Viewer: "只读查看节点、任务、日志和产物，不能维护节点或提交构建。",
  }[role] || "自定义角色，按后端授权规则执行。";
}

function avatarText(user) {
  const source = (user.displayName || user.userName || "U").trim();
  return escapeHtml(source.slice(0, 1).toUpperCase());
}

function protectedUserReason(user) {
  if (isRootAdminUser(user)) return "主账号不可删除或禁用。";
  if (user.id === state.user?.id) return "不能禁用当前登录账号。";
  return "";
}

function isRootAdminUser(user) {
  return (user.userName || "").toLowerCase() === "admin";
}

async function saveUser(event) {
  event.preventDefault();
  clearError();
  if (!isAdmin()) {
    showError(new Error("只有管理员可以维护用户。"));
    return;
  }

  const userId = $("userId").value;
  const path = userId ? `/api/users/${encodeURIComponent(userId)}` : "/api/users";
  const method = userId ? "PUT" : "POST";
  setButtonBusy("userSaveBtn", true, userId ? "更新中..." : "保存中...");
  try {
    const savedUser = await api(path, {
      method,
      body: JSON.stringify({
        userName: $("userName").value,
        displayName: $("userDisplayName").value,
        role: $("userRole").value,
        password: $("userPassword").value || null,
        enabled: $("userEnabled").checked,
      }),
    });
    state.selectedUserId = savedUser?.id || userId || state.selectedUserId;
    resetUserForm();
    closeUserModal();
    await refreshUsers();
    showNotice(userId ? "用户已更新。" : "用户已创建。");
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("userSaveBtn", false);
  }
}

function handleUsersClick(event) {
  if (!event.target.closest) return;
  const selectButton = event.target.closest("[data-select-user-id]");
  if (selectButton) {
    selectUser(selectButton.dataset.selectUserId);
    return;
  }

  const editButton = event.target.closest("[data-edit-user-id]");
  if (editButton) {
    openUserModal(editButton.dataset.editUserId);
    return;
  }

  const disableButton = event.target.closest("[data-disable-user-id]");
  if (disableButton) {
    AppRuntime.runAction(disableButton, () => disableUser(disableButton.dataset.disableUserId), {
      busyText: "禁用中...",
      onError: showError,
    });
  }
}

function handleUsersKeydown(event) {
  if (event.key !== "Enter" && event.key !== " ") return;
  if (!event.target.closest) return;
  const selectItem = event.target.closest("[data-select-user-id]");
  if (!selectItem) return;
  event.preventDefault();
  selectUser(selectItem.dataset.selectUserId);
}

function selectUser(userId) {
  if (!state.users.some((user) => user.id === userId)) return;
  state.selectedUserId = userId;
  renderUsers();
}

async function disableUser(userId) {
  const user = state.users.find((item) => item.id === userId);
  const reason = user ? protectedUserReason(user) : "";
  if (reason) {
    showError(new Error(reason));
    return;
  }

  await api(`/api/users/${encodeURIComponent(userId)}`, { method: "DELETE" });
  if ($("userId").value === userId) {
    resetUserForm();
  }
  state.selectedUserId = userId;
  await refreshUsers();
  showNotice("用户已停用。系统保留账号审计记录。");
}

function openUserModal(userId = "") {
  resetUserForm();
  if (userId) {
    state.selectedUserId = userId;
    fillUserForm(userId);
  }
  $("userModal").classList.remove("hidden");
  $("userModal").setAttribute("aria-hidden", "false");
  setTimeout(() => $("userName").focus(), 0);
}

function closeUserModal() {
  $("userModal").classList.add("hidden");
  $("userModal").setAttribute("aria-hidden", "true");
}

function isUserModalOpen() {
  return !$("userModal").classList.contains("hidden");
}

function fillUserForm(userId) {
  const user = state.users.find((item) => item.id === userId);
  if (!user) return;
  $("userId").value = user.id;
  $("userName").value = user.userName;
  $("userDisplayName").value = user.displayName || "";
  $("userRole").value = user.role;
  $("userPassword").value = "";
  $("userEnabled").checked = Boolean(user.enabled);
  const rootAdmin = isRootAdminUser(user);
  $("userName").disabled = rootAdmin;
  $("userRole").disabled = rootAdmin;
  $("userEnabled").disabled = rootAdmin || user.id === state.user?.id;
  $("userEnabled").parentElement.title = rootAdmin ? "主账号必须保持管理员角色且启用。" : ($("userEnabled").disabled ? "不能禁用当前登录账号。" : "");
  $("userModalTitle").textContent = "编辑用户";
  $("userModalSubTitle").textContent = "密码留空表示不修改。";
  $("userSaveBtn").textContent = "更新用户";
  $("userSaveBtn").dataset.defaultText = "更新用户";
}

function resetUserForm() {
  $("userForm").reset();
  $("userId").value = "";
  $("userRole").value = "Builder";
  $("userEnabled").checked = true;
  $("userName").disabled = false;
  $("userRole").disabled = false;
  $("userEnabled").disabled = false;
  $("userEnabled").parentElement.title = "";
  $("userModalTitle").textContent = "新增用户";
  $("userModalSubTitle").textContent = "为 LinuxGateway 用户分配合适的访问级别。";
  $("userSaveBtn").textContent = "保存用户";
  $("userSaveBtn").dataset.defaultText = "保存用户";
}

async function changeMyPassword(event) {
  event.preventDefault();
  clearError();
  setButtonBusy("passwordSaveBtn", true, "更新中...");
  try {
    await api("/api/me/password", {
      method: "POST",
      body: JSON.stringify({
        currentPassword: $("currentPassword").value,
        newPassword: $("newPassword").value,
      }),
    });
    state.user = null;
    stopDashboardEvents();
    showLogin();
    $("loginError").textContent = "密码已更新，请使用新密码重新登录。";
  } catch (error) {
    showError(error);
  } finally {
    setButtonBusy("passwordSaveBtn", false);
    $("passwordForm").reset();
  }
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

function statusLabel(value) {
  return labelFromMap(STATUS_LABELS, value);
}

function roleLabel(value) {
  return labelFromMap(ROLE_LABELS, value);
}

function platformLabel(value) {
  return labelFromMap(PLATFORM_LABELS, value || "ios");
}

function artifactTypeLabel(value) {
  return labelFromMap(ARTIFACT_TYPE_LABELS, value);
}

function enabledLabel(enabled) {
  return enabled ? "已启用" : "已停用";
}

function labelFromMap(map, value) {
  const text = String(value ?? "");
  if (!text) return "-";
  const direct = map[text] || map[text.toLowerCase()] || map[text.toUpperCase()];
  if (direct) return direct;
  const matchedKey = Object.keys(map).find((key) => key.toLowerCase() === text.toLowerCase());
  return matchedKey ? map[matchedKey] : text;
}

function platforms(values) {
  const list = values && values.length ? values : ["auto"];
  return list.map((value) => platformBadge(value)).join(" ");
}

function platformBadge(value) {
  const platform = value || "ios";
  return `<span class="platform ${escapeHtml(platform)}">${escapeHtml(platformLabel(platform))}</span>`;
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
